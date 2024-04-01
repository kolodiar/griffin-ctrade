using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.IO;
using System.Threading;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

[Robot(AccessRights = AccessRights.FullAccess)] // None - default, FullAccess - if errors with creating orders
public class GriffinCtrade : Robot
{
    private DateTime lastActionTime = DateTime.Now.AddHours(-2); // move time back so the first iteration is successfull
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

        DateTime currentTime = Server.Time; // Get the current time
        // if already executed in current hour or it is too early for indicators and orderbook - wait and come back later
        if (lastActionTime.Hour == currentTime.Hour || currentTime.Minute < 3) {
            return;
        }
        lastActionTime = currentTime; // Update the last execution hour

        State currentState = IdentifyCurrentState();

        switch (currentState)
        {
            case State.NA:
                Print("State: NA");
                var buyDecision = tradeDecisionService.GetBuyDecision(Symbol.Ask, Symbol.Bid);
                //Print($"Decision: {buyDecision.decision} {buyDecision.volume} {buyDecision.orderPrice} {buyDecision.stopLoss}");

                if (buyDecision.decision)
                {
                    Print("Run exit decision service to confirm enter signal.");
                    var sellDecisionOnEnter = tradeDecisionService.GetSellDecision(Symbol.Ask, Symbol.Bid, Symbol.Ask,
                                                                                   Symbol.Ask * 0.99, DateTime.Now.ToString());
                    // If no signall for selling, then entering is safe, confirm enter signal and open the position
                    if(!sellDecisionOnEnter.decision) 
                    {
                        Print("Create instant buy order");
                        OpenPositionInstant(buyDecision);
                        //Print("Create pending buy order");
                        //OpenPositionPending(buyDecision);
                    } else {
                        Print("Enter signal was not confirmed buy exit check. No buy opportunity in this tick");
                    }
                }
                else
                {
                    Print("No buy opportunity in this tick");
                }
                break;

            case State.PendingBuyOrder:
                Print("State: PENDING_BUY_ORDER");
                var result = CancelPendingOrder(PendingOrders.FirstOrDefault()); // we always have at most one order
                Print(result.IsSuccessful ? "Buy order cancelled due to timeout." : "Failed to cancel buy order.");
                break;

            case State.OpenPosition:
                Print("State: OPEN_POSITION");
                var position = Positions.FirstOrDefault();
                Print("Position found");

                var sellDecision = tradeDecisionService.GetSellDecision(Symbol.Ask, Symbol.Bid, position.EntryPrice, 
                                                                        position.StopLoss.Value, position.EntryTime.ToString());
                if (sellDecision.decision)
                {
                    ExitPositionInstant();

                    // Print("Prepare position for exit - add take_profit and update stop_loss");
                    // UpdatePosition(sellDecision, true);
                } 
                else
                {
                    Print("No sell opportunity in this tick");
                    //UpdatePosition(sellDecision, false);

                    double current_stoploss_percentage = (Symbol.Ask - position.StopLoss.Value) / Symbol.Ask;
                    Print($"Current stop loss: {current_stoploss_percentage * 100}%");
                    if(current_stoploss_percentage > 0.01) // keep the potential loss of 1%, not bigger
                    {
                        Print($"Updating stop_loss to 1% ({Symbol.Ask * 0.99})");
                        ModifyPosition(position, Symbol.Ask * 0.99, null);
                    } else 
                    {
                        Print("No update in this tick");
                    }
                }
                break;

            case State.AddedTakeProfit:
                Print("State: ADDED_TAKE_PROFIT");
                position = Positions.FirstOrDefault(); // defined in previous case block
                Print("Position got");
                double current_stop_loss_percentage = (Symbol.Ask - position.StopLoss.Value) / Symbol.Ask;
                Print($"Current stop loss: {current_stop_loss_percentage * 100}%");
                if(current_stop_loss_percentage > 0.01) // keep the potential loss of 1%, not bigger
                {
                    Print($"Updating stop_loss to 1% ({Symbol.Ask * 0.99})");
                    ModifyPosition(position, Symbol.Ask * 0.99, null);
                } else 
                {
                    Print("No update in this tick");
                }
                // var updateSellDecision = tradeDecisionService.GetSellUpdateDecision(Symbol.Ask, Symbol.Bid, position.EntryPrice, 
                //                                                                     position.TakeProfit.Value, position.StopLoss.Value, 
                //                                                                     position.EntryTime.ToString());

                // if (updateSellDecision.takeProfit != double.NaN || updateSellDecision.stopLoss != double.NaN)
                // {
                //     UpdatePosition(updateSellDecision, true);
                // }
                // else
                // {
                //     Print("No update in this tick");
                // }
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
        //ValidateEnterDecisionForInstantBuy(ref decision); // Assuming this method adjusts decision in-place
        double volume = CalculateVolume(Symbol.Ask); //ConvertVolume(decision.volume);
        Print("Current Ask price: ", Symbol.Ask);
        double stopLoss = Symbol.Ask * 0.99; // set the potential loss of 1%
        Print($"Creating instant buy order: volume={volume}, order_price=0.0 (actual Ask will be used), stop_loss={stopLoss}");

        var StopLossInPips = (Symbol.Ask - stopLoss) / Symbol.PipSize;
        Print("StopLoss_pips: ", StopLossInPips);
        // Execute a market order to buy immediately at the current ask price
        var tradeResult = ExecuteMarketOrder(TradeType.Buy, SymbolName, volume, "Instant buy order on current Ask price", StopLossInPips, null); // takeprofit - the last parameter, only for testing 

        if (tradeResult.IsSuccessful)
        {
            Print($"Buy order created, position ID: {tradeResult.Position.Id}");
        }
        else
        {
            Print($"Error executing buy order: {tradeResult.Error}");
        }
    }


    // private void OpenPositionPending(AiEnterResponse decision)
    // {
    //     //ValidateEnterDecisionForBuyLimit(ref decision);
    //     double volume = ConvertVolume(decision.volume); // Ensure this calculates based on the decision's order price
    
    //     var StopLossInPips = (decision.orderPrice - decision.stopLoss) / Symbol.PipSize;
    //     Print("StopLoss_pips: ", StopLossInPips);
    //     Print($"Creating pending buy order: volume={volume}, order_price={decision.orderPrice}, stop_loss={StopLossInPips}");

    //     // Place a Buy Limit order
    //     var tradeResult = PlaceLimitOrder(TradeType.Buy, SymbolName, volume, decision.orderPrice, "Pending buy order", StopLossInPips, null);

    //     if (tradeResult.IsSuccessful)
    //     {
    //         // Use tradeResult.PendingOrder.Id to access the ID of the placed order
    //         Print($"Buy limit order created, order ID: {tradeResult.PendingOrder.Id}");
    //     }
    //     else
    //     {
    //         // Error handling
    //         Print($"Error executing buy limit order: {tradeResult.Error}");
    //     }
    // }

    private void ExitPositionInstant()
    {
        Print("Closing position instant");
        ClosePosition(Positions[0]);
    }

    // private void UpdatePosition(AiExitResponse decision, bool updateTakeProfit = true)
    // {
    //     Print("Current Ask price: ", Symbol.Ask);
    //     bool validationSuccessfull = ValidateExitDecision(ref decision);

    //     if(!validationSuccessfull) {
    //         //Print("Update cancelled because of unsucessfull validation. - !for testing purposes deactivated, uncomment return to activate this! ");
    //         return;
    //     }
        
    //     Print($"Updating position: stop_loss={decision.stopLoss}, take_profit={(updateTakeProfit ? decision.takeProfit.ToString() : "Unchanged")}");
   
    //     if (Positions.Count > 0)
    //     {
    //         var position = Positions[0];

    //         TradeResult tradeResult;
    //         if (updateTakeProfit)
    //         {

    //             tradeResult = ModifyPosition(position, decision.stopLoss == double.NaN ? position.StopLoss : decision.stopLoss, 
    //                                                    decision.takeProfit == double.NaN ? position.TakeProfit : decision.takeProfit);
    //         }
    //         else
    //         {
    //             tradeResult = ModifyPosition(position, decision.stopLoss == double.NaN ? position.StopLoss : decision.stopLoss, position.TakeProfit); // keep current takeProfit
    //         }

    //         if (tradeResult.IsSuccessful)
    //         {
    //             Print($"Position modified - new stop_loss: {position.StopLoss}, new take_profit: {position.TakeProfit}");
    //         }
    //         else
    //         {
    //             Print($"Error modifying position: {tradeResult.Error}");
    //         }
    //     }
    //     else
    //     {
    //         Print("No position to modify");
    //     }
    // }

    // UTILITIES

    private double CalculateVolume(double orderPrice)
    {
        Print("Balance before: ", Account.Balance);
        double percentageOfBalanceToUse = 0.007; // 0.007 for testing, 0.7 for real
        // How much we can buy for specified percentage of our balance with the given price
        double unitsToBuy = Account.Balance * percentageOfBalanceToUse / orderPrice;
        Print("unitsToBuy: ", unitsToBuy);
        //Print("unitsToBuy: ", unitsToBuy);
        // double standardLots = unitsToBuy / Symbol.QuantityToVolumeInUnits(1); // Convert 1 unit of quantity to volume in lots for the symbol
        // Print("standardLots: ", standardLots);

        // Calculate the volume in steps that are valid for the symbol
        double volumeStep = Symbol.VolumeInUnitsStep; // in units
        Print("volumeStep: ", volumeStep);
        //double roundedLots = Math.Floor(standardLots / volumeStep) * volumeStep;
        double roundedUnits = Math.Floor(unitsToBuy / volumeStep) * volumeStep;
        Print("roundedUnits: ", roundedUnits);
        return roundedUnits > 0 ? roundedUnits : Symbol.VolumeInUnitsMin; // set min volume if calculated volume is too low
        // Print("Volume rounded lots: ", roundedLots);
        // return roundedLots;
    }

    private double ConvertVolume(double volume)
    {
        // Calculate the volume in steps that are valid for the symbol
        double volumeStep = Symbol.VolumeInUnitsStep; // in units
        //double roundedLots = Math.Floor(standardLots / volumeStep) * volumeStep;
        double roundedUnits = Math.Floor(volume / volumeStep) * volumeStep;
        Print("roundedVolumeUnits: ", roundedUnits);
        return roundedUnits;
    }


    // VALIDATION

    // private void ValidateEnterDecisionForBuyLimit(ref AiEnterResponse decision)
    // {
    //     Print("Run decision validation (buyLimit):");
    //     //Print($"Input: orderPrice={decision.orderPrice}, stopLoss={decision.stopLoss}");

    //     // Normalize prices using the Symbol's pip size
    //     decision.orderPrice = Math.Round(decision.orderPrice, Symbol.Digits);
    //     decision.stopLoss = Math.Round(decision.stopLoss, Symbol.Digits);
    //     //Print($"After normalization: orderPrice={decision.orderPrice}, stopLoss={decision.stopLoss}");

    //     if(decision.stopLoss > Symbol.Bid) {
    //         Print($"ERROR: Stop loss {decision.stopLoss} higher than bid price! Invalid input");
    //         Thread.Sleep(2000);
    //         // for testing purposes, move stoploss just to resume testing
    //         decision.stopLoss = Symbol.Ask * 0.995; // 0.5% loss, calculated from buy price //Symbol.Bid * 0.99;
    //         Print("New stop loss for testing purposes: ", decision.stopLoss);
    //         //throw new InvalidDataException("Stop loss higher then bid price! Invalid input");
    //     }

    //     Print("Finish validation");
    // }

    // private void ValidateEnterDecisionForInstantBuy(ref AiEnterResponse decision)
    // {
    //     Print("Run decision validation (buy instant):");
    //     Print($"Enter decision: orderPrice={decision.orderPrice}, stopLoss={decision.stopLoss}");

    //     // Normalize prices using the Symbol's pip size
    //     decision.orderPrice = Math.Round(decision.orderPrice, Symbol.Digits);
    //     decision.stopLoss = Math.Round(decision.stopLoss, Symbol.Digits);
    //     //Print($"After normalization: orderPrice={decision.orderPrice}, stopLoss={decision.stopLoss}");

    
    //     if(decision.stopLoss > Symbol.Bid) {
    //         Print($"ERROR: Stop loss {decision.stopLoss} higher than bid price! Invalid input");
    //         Thread.Sleep(2000);
    //         // for testing purposes, move stoploss just to resume testing
    //         decision.stopLoss = Symbol.Ask * 0.995; // 0.5% loss, calculated from buy price //Symbol.Bid * 0.999; // 0.1% stop loss //Symbol.Bid - 10 * Symbol.PipSize;
    //         Print("New stop loss for testing purposes: ", decision.stopLoss);
    //         //throw new InvalidDataException("Stop loss higher then bid price! Invalid input");
    //     }

    //     if((Symbol.Bid - decision.stopLoss) / Symbol.Bid < 0.001) { // if gap between bid price and stop loss is < 0.1%
    //         Print($"WARN: Stop loss {decision.stopLoss} too close to bid price! Invalid input");
    //         Thread.Sleep(2000);
    //         // for testing purposes, move stoploss just to resume testing
    //         decision.stopLoss = Symbol.Ask * 0.995; // 0.5% loss, calculated from buy price //Symbol.Bid * 0.999; // 0.1% stop loss //Symbol.Bid - 10 * Symbol.PipSize;
    //         Print("New stop loss for testing purposes: ", decision.stopLoss);
    //         //throw new InvalidDataException("Stop loss higher then bid price! Invalid input");
    //     }

    //     decision.stopLoss = Symbol.Ask * 0.99;
    //     Print("for testing sell decision let's just set stoploss to 1% for more freedom");

    //     Print("Finish validation");
    // }

    // private bool ValidateExitDecision(ref AiExitResponse decision)
    // {
    //     Print("Run decision validation: (exit/update)");

    //     decision.stopLoss = Symbol.Ask * 0.99;
    //     Print("for testing sell decision let's just set stoploss to 1% for more freedom");

    //     Print($"Update input: stopLoss={decision.stopLoss}, takeProfit={decision.takeProfit}");

    //     // Assuming you have access to the current position's stopLoss
    //     var currentPositionSL = Positions[0].StopLoss ?? 0;
    //     var currentPositionTP = Positions[0].TakeProfit ?? 0;

    //     Print($"Current state: stopLoss={currentPositionSL}, takeProfit={currentPositionTP}");


    //     // Normalize prices using the Symbol's pip size
    //     decision.takeProfit = Math.Round(decision.takeProfit, Symbol.Digits);
    //     decision.stopLoss = Math.Round(decision.stopLoss, Symbol.Digits);
    //     //Print($"After normalization: stopLoss={decision.stopLoss}, takeProfit={decision.takeProfit}");

    //     if(decision.stopLoss > Symbol.Bid) {
    //         Print($"ERROR: Stop loss {decision.stopLoss} higher than bid price! Invalid input");

    //         // for testing purposes, move stoploss just to resume testing
    //         decision.stopLoss = Symbol.Ask * 0.995; // 0.5% loss, calculated from buy price //Symbol.Bid * 0.999; // 0.1% stop loss //Symbol.Bid - 10 * Symbol.PipSize;
    //         Print("New stop loss for testing purposes: ", decision.stopLoss);
    //         //throw new InvalidDataException("Stop loss higher then bid price! Invalid input");
    //     }

    //     if((Symbol.Bid - decision.stopLoss) / Symbol.Bid < 0.001) { // if gap between Ask price and stop loss is < 0.5%
    //         Print($"WARN: Stop loss {decision.stopLoss} too close to bid price (<0.1%)! Invalid input. Update refused.");
    //         return false;
    //     }

    //     if((Symbol.Ask - decision.stopLoss) / Symbol.Bid < 0.003) { // if gap between Ask price and stop loss is < 0.3%
    //         Print($"WARN: Stop loss {decision.stopLoss} too close to ask price (<0.3%)! Invalid input. Update refused.");
    //         return false;
    //     }

    //     if(decision.takeProfit < Symbol.Bid) {
    //         Print($"ERROR: Take profit {decision.takeProfit} lower than bid price! Invalid input");

    //         // for testing purposes, move takeProfit just to resume testing
    //         decision.takeProfit = Symbol.Bid * 1.001; // 0.1% take profit
    //         Print("New take profit for testing purposes: ", decision.takeProfit);
    //         //throw new InvalidDataException("Take profit lower then bid price! Invalid input");
    //     }

    //     // Verify that stopLoss can only be updated to a higher value (never let stopLoss go lower!!!)
    //     if (decision.stopLoss < currentPositionSL)
    //     {
    //         decision.stopLoss = currentPositionSL;
    //         Print("Requested stop loss goes lower. Update rejected. Never let stopLoss go lower!");
    //         return false;
    //     }
    //     Print($"After checking if stopLoss is not getting lower: stopLoss={decision.stopLoss}, takeProfit={decision.takeProfit}");
        
    //     Print("Finish validation");
    //     return true;
    // }


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
