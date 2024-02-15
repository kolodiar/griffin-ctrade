using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

[Robot(AccessRights = AccessRights.None)]
public class GriffinCtrade : Robot
{


    private DateTime lastActionTime = DateTime.MinValue;
    private TradeDecisionService tradeDecisionService;

    public GriffinCtrade() : base()
    {
        tradeDecisionService = new TradeDecisionService(this);
    }

    protected override void OnStart()
    {


    }

    protected override void OnTick()
    {
        // Assuming identifyCurrentState() is adapted and implemented
        State currentState = IdentifyCurrentState();

        switch (currentState)
        {
            case State.NA:
                Print("State: NA");
                var buyDecision = tradeDecisionService.GetBuyDecision();
                Print($"Decision: {buyDecision.decision} {buyDecision.volume} {buyDecision.orderPrice} {buyDecision.stopLoss}");
                if (buyDecision.decision)
                {
                    //Print("Create instant buy order");
                    //OpenPositionInstant(buyDecision);
                    Print("Create pending buy order");
                    OpenPositionPending(buyDecision);
                }
                else
                {
                    Print("No buy opportunity in this tick");
                }
                break;

            case State.PendingBuyOrder:
                Print("State: PENDING_BUY_ORDER");
                //var result = CancelPendingOrder(PendingOrders.FirstOrDefault()); // we always have at most one order
                //Print(result.IsSuccessful ? "Buy order cancelled due to timeout." : "Failed to cancel buy order.");
                break;

            case State.OpenPosition:
                Print("State: OPEN_POSITION");
                var position = Positions.FirstOrDefault();
                var sellDecision = tradeDecisionService.GetSellDecision(position.EntryPrice, position.StopLoss.Value, position.EntryTime.ToString());
                if (sellDecision.decision)
                {
                    Print("Prepare position for exit - add take_profit");
                    UpdatePosition(sellDecision, true);
                }
                else
                {
                    Print("No sell opportunity in this tick");
                    UpdatePosition(sellDecision, false);
                }
                break;

            case State.AddedTakeProfit:
                Print("State: ADDED_TAKE_PROFIT");
                position = Positions.FirstOrDefault(); // defined in previous case block
                var updateTPSLDecision = tradeDecisionService.GetSellUpdateDecision(position.EntryPrice, position.TakeProfit.Value, position.StopLoss.Value, position.EntryTime.ToString());
                if (updateTPSLDecision.decision)
                {
                    UpdatePosition(updateTPSLDecision, true);
                }
                else
                {
                    Print("No update in this tick");
                }
                break;

            default:
                Print("Unknown state with ", PendingOrders.Count, " orders and ", Positions.Count, " positions!");
                Stop();
                break;
        }
    }

    protected override void OnStop()
    {
        // Handle cBot stop here
    }

    // TRADE FUNCTIONS

    private void OpenPositionInstant(AiEnterResponse decision)
    {
        ValidateEnterDecisionForInstantBuy(ref decision); // Assuming this method adjusts decision in-place
        double volume = CalculateVolume(Symbol.Ask);
        Print("Current Ask price: ", Symbol.Ask);
        Print($"Creating instant buy order: volume={volume}, order_price=0.0 (actual Ask will be used), stop_loss={decision.stopLoss}");

        // Execute a market order to buy immediately at the current ask price
        var tradeResult = ExecuteMarketOrder(TradeType.Buy, SymbolName, volume, "Instant buy order on current Ask price", null, decision.stopLoss);

        if (tradeResult.IsSuccessful)
        {
            Print($"Buy order created, position ID: {tradeResult.Position.Id}");
        }
        else
        {
            Print($"Error executing buy order: {tradeResult.Error}");
        }
    }


    private void OpenPositionPending(AiEnterResponse decision)
    {
        ValidateEnterDecisionForBuyLimit(ref decision);
        double volume = CalculateVolume(decision.orderPrice); // Ensure this calculates based on the decision's order price
    
        var StopLossInPips = (decision.orderPrice - decision.stopLoss) / Symbol.PipSize;
        Print("StopLoss_pips: ", (decision.orderPrice - decision.stopLoss) / Symbol.PipSize);
        Print($"Creating pending buy order: volume={volume}, order_price={decision.orderPrice}, stop_loss={StopLossInPips}");

        // Place a Buy Limit order
        var tradeResult = PlaceLimitOrder(TradeType.Buy, SymbolName, volume, decision.orderPrice, "Pending buy order", StopLossInPips, null);

        if (tradeResult.IsSuccessful)
        {
            // Use tradeResult.PendingOrder.Id to access the ID of the placed order
            Print($"Buy limit order created, order ID: {tradeResult.PendingOrder.Id}");
        }
        else
        {
            // Error handling
            Print($"Error executing buy limit order: {tradeResult.Error}");
        }
    }


    private void UpdatePosition(AiExitResponse decision, bool updateTakeProfit = true)
    {
        ValidateExitDecision(ref decision); // Ensure this adjusts the decision object as needed
        Print("Current Ask price: ", Symbol.Ask);
        Print($"Updating position: stop_loss={decision.stopLoss}, take_profit={(updateTakeProfit ? decision.takeProfit.ToString() : "Unchanged")}");

        // Assuming you have identified the position you want to modify, for example, the first or only position
        if (Positions.Count > 0)
        {
            var position = Positions[0]; // This is a simplification, you might need to select the position more carefully based on your logic
            Print($"Current state: stop_loss={position.StopLoss}, take_profit={position.TakeProfit}");

            TradeResult tradeResult;
            if (updateTakeProfit)
            {
                tradeResult = ModifyPosition(position, decision.stopLoss, decision.takeProfit);
            }
            else
            {
                tradeResult = ModifyPosition(position, decision.stopLoss, position.TakeProfit);
            }

            if (tradeResult.IsSuccessful)
            {
                Print($"Position modified - new stop_loss: {position.StopLoss}, new take_profit: {position.TakeProfit}");
            }
            else
            {
                Print($"Error modifying position: {tradeResult.Error}");
            }
        }
        else
        {
            Print("No position to modify");
        }
    }

    // UTILITIES

    private double CalculateVolume(double orderPrice)
    {
        Print("Balance before: ", Account.Balance);
        // How much we can buy for 70% of our balance with the given price
        double unitsToBuy = Account.Balance * 0.7 / orderPrice;
        Print("unitsToBuy: ", unitsToBuy);
        // double standardLots = unitsToBuy / Symbol.QuantityToVolumeInUnits(1); // Convert 1 unit of quantity to volume in lots for the symbol
        // Print("standardLots: ", standardLots);

        // Calculate the volume in steps that are valid for the symbol
        double volumeStep = Symbol.VolumeInUnitsStep; // in units
        //double roundedLots = Math.Floor(standardLots / volumeStep) * volumeStep;
        double roundedUnits = Math.Floor(unitsToBuy / volumeStep) * volumeStep;
        return roundedUnits;
        // Print("Volume rounded lots: ", roundedLots);
        // return roundedLots;
    }


    // VALIDATION

    private void ValidateEnterDecisionForBuyLimit(ref AiEnterResponse decision)
    {
        Print("Run decision validation (buyLimit):");
        Print($"Input: orderPrice={decision.orderPrice}, stopLoss={decision.stopLoss}");

        // Normalize prices using the Symbol's pip size
        decision.orderPrice = Math.Round(decision.orderPrice, Symbol.Digits);
        decision.stopLoss = Math.Round(decision.stopLoss, Symbol.Digits);
        Print($"After normalization: orderPrice={decision.orderPrice}, stopLoss={decision.stopLoss}");
        Print("Finish validation");
    }

    private void ValidateEnterDecisionForInstantBuy(ref AiEnterResponse decision)
    {
        Print("Run decision validation (buy instant):");
        Print($"Input: orderPrice={decision.orderPrice}, stopLoss={decision.stopLoss}");

        // Normalize prices using the Symbol's pip size
        decision.orderPrice = Math.Round(decision.orderPrice, Symbol.Digits);
        decision.stopLoss = Math.Round(decision.stopLoss, Symbol.Digits);
        Print($"After normalization: orderPrice={decision.orderPrice}, stopLoss={decision.stopLoss}");
        Print("Finish validation");
    }

    private void ValidateExitDecision(ref AiExitResponse decision)
    {
        Print("Run decision validation: (exit/update)");
        Print($"Input: stopLoss={decision.stopLoss}, takeProfit={decision.takeProfit}");

        // Normalize prices using the Symbol's pip size
        decision.takeProfit = Math.Round(decision.takeProfit, Symbol.Digits);
        decision.stopLoss = Math.Round(decision.stopLoss, Symbol.Digits);
        Print($"After normalization: stopLoss={decision.stopLoss}, takeProfit={decision.takeProfit}");

        // Assuming you have access to the current position's stopLoss
        var currentPositionSL = Positions[0].StopLoss ?? 0; // Replace currentPosition with your actual position object

        // Verify that stopLoss can only be updated to a higher value (never let stopLoss go lower)
        if (decision.stopLoss < currentPositionSL)
        {
            decision.stopLoss = currentPositionSL;
        }
        Print($"After checking if stopLoss is not getting lower: stopLoss={decision.stopLoss}, takeProfit={decision.takeProfit}");
        Print("Finish validation");
    }


    // STATE MANAGEMENT

    private State IdentifyCurrentState()
    {
        // Check for no orders and no positions
        if (PendingOrders.Count == 0 && Positions.Count == 0)
        {
            return State.NA;
        }
        // Check for a single pending order and no positions
        else if (PendingOrders.Count == 1 && Positions.Count == 0)
        {
            return State.PendingBuyOrder;
        }
        // Check for no orders and a single open position without a take profit
        else if (PendingOrders.Count == 0 && Positions.Count == 1 && (Positions[0].TakeProfit == null || Positions[0].TakeProfit == 0))
        {
            return State.OpenPosition;
        }
        // Check for no orders and a single open position with a take profit
        else if (PendingOrders.Count == 0 && Positions.Count == 1 && Positions[0].TakeProfit != null && Positions[0].TakeProfit != 0)
        {
            return State.AddedTakeProfit;
        }
        // If none of the above conditions are met
        else
        {
            return State.Error;
        }
    }
}
