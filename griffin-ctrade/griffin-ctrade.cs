using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None)]
    public class GriffinCtrade : Robot
    {

        private DateTime lastActionTime = DateTime.MinValue;
        private TradeDecisionService tradeDecisionService = new TradeDecisionService();

        protected override void OnStart()
        {

            
        }

        protected override void OnTick()
        {
            // Check if it's too early to process data; adjust to cAlgo environment
            if (lastActionTime == MarketData.GetBars(TimeFrame.Hour).Last(1).OpenTime || Server.Time.Minute < 3)
            {
                return;
            }

            lastActionTime = MarketData.GetBars(TimeFrame.Hour).Last(1).OpenTime;
            Print(Server.Time);

            State currentState = IdentifyCurrentState();

            switch (currentState)
            {
                case State.NA:
                    Print("State: NA");
                    var decision = tradeDecisionService.GetBuyDecision(); // Implement this method
                    Print("Decision: ", decision.decision, " ", decision.volume, " ", decision.orderPrice, " ", decision.stopLoss);
                    if (decision.decision)
                    {
                        Print("Create instant buy order");
                        OpenPositionInstant(decision); // Implement this method
                    }
                    else
                    {
                        Print("No buy opportunity in this tick");
                    }
                    break;

                case State.PendingBuyOrder:
                    Print("State: PENDING_BUY_ORDER");
                    // Implement logic to check and cancel the pending order if necessary
                    break;

                case State.OpenPosition:
                    Print("State: OPEN_POSITION");
                    // var sellDecision = GetSellDecision(); // Implement this method
                    // if (sellDecision.Decision)
                    // {
                    //     Print("Prepare position for exit - add take_profit");
                    //     UpdatePosition(sellDecision); // Implement this method
                    // }
                    // else
                    // {
                    //     Print("No sell opportunity in this tick");
                    //     Print("Run update stop_loss");
                    //     var updateDecision = GetSellUpdateDecision(); // Implement this method
                    //     UpdatePosition(updateDecision);
                    // }
                    break;

                case State.AddedTakeProfit:
                    Print("State: ADDED_TAKE_PROFIT");
                    Print("Run update take_profit and stop_loss");
                    // var updateTPSLDecision = GetSellUpdateDecision(); // Again, implement this method
                    // if (updateTPSLDecision.Decision)
                    // {
                    //     UpdatePosition(updateTPSLDecision);
                    // }
                    // else
                    // {
                    //     Print("No update in this tick");
                    // }
                    break;

                default:
                    Print("Unknown state with ", PendingOrders.Count, " orders and ", Positions.Count, " positions!");
                    // Stop();
                    break;
            }
        }

        protected override void OnStop()
        {
            // Handle cBot stop here
        }

        // TRADE FUNCTIONS

        private AiEnterResponse OpenPositionInstant(AiEnterResponse decisiom){
            // TODO
            return new AiEnterResponse
                {
                    decision = false,
                    orderPrice = 0.0,
                    stopLoss = 0.0,
                    volume = 0.0
                };
        }

        private AiEnterResponse OpenPositionPendingWithRequest(AiEnterResponse decisiom){
            // TODO
            return new AiEnterResponse
                {
                    decision = false,
                    orderPrice = 0.0,
                    stopLoss = 0.0,
                    volume = 0.0
                };
        }

        private AiEnterResponse OpenPositionPending(AiEnterResponse decisiom){
            // TODO
            return new AiEnterResponse
                {
                    decision = false,
                    orderPrice = 0.0,
                    stopLoss = 0.0,
                    volume = 0.0
                };
        }

        private AiEnterResponse OpenPositionPending(AiEnterResponse decisiom, bool updatetakeProfit = true){
            // TODO
            return new AiEnterResponse
                {
                    decision = false,
                    orderPrice = 0.0,
                    stopLoss = 0.0,
                    volume = 0.0
                };
        }

        // UTILITIES

        private double CalculateVolume(double orderPrice)
        {
            Print("Balance before: ", Account.Balance);
            double unitsToBuy = Account.Balance * 0.7 / orderPrice;
            double standardLots = unitsToBuy / Symbol.QuantityToVolumeInUnits(1); // Convert 1 unit of quantity to volume in lots for the symbol
            
            // Calculate the volume in steps that are valid for the symbol
            double volumeStep = Symbol.VolumeStep;
            double roundedLots = Math.Floor(standardLots / volumeStep) * volumeStep;
            
            Print("Volume rounded lots: ", roundedLots);
            return roundedLots; // How much we can buy for 70% of our balance with the given price
        }


        // VALIDATION

        private double GetStopLevelPoints()
        {
            // As there's no direct equivalent in cAlgo for SYMBOL_TRADE_STOPS_LEVEL,
            // you might use the current spread as a proxy or a predefined value based on your broker's requirements.
            // This example demonstrates using the current spread as an approximation.
            
            // Note: The spread is in terms of the symbol's price units, not points.
            // If you need the value in points, you'd adjust based on the symbol's pip size.
            double spreadInPriceUnits = Symbol.Spread;
            double spreadInPoints = spreadInPriceUnits / Symbol.PipSize; // Convert spread from price units to points
            
            // You might want to add a buffer to the spread to ensure compliance with minimum stop levels during volatile periods.
            double buffer = 5; // Example buffer in points TODO
            double stopLevelPoints = spreadInPoints + buffer;

            return stopLevelPoints;
        }

        private void ValidateEnterDecisionForBuyLimit(ref AiEnterResponse decision)
        {
            Print("Run decision validation (buyLimit):");
            Print($"Input: orderPrice={decision.orderPrice}, stopLoss={decision.stopLoss}");

            // Normalize prices using the Symbol's pip size
            decision.orderPrice = Math.Round(decision.orderPrice, Symbol.Digits);
            decision.stopLoss = Math.Round(decision.stopLoss, Symbol.Digits);
            Print($"After normalization: orderPrice={decision.orderPrice}, stopLoss={decision.stopLoss}");

            // Ensure the order price is not too close to the current ask price, considering the minimum stop level
            double stopLevelInPrice = GetStopLevelPoints() * Symbol.PipSize;
            var askPrice = Symbol.Ask;
            if (decision.orderPrice > askPrice- stopLevelInPrice)
            {
                decision.orderPrice = askPrice - stopLevelInPrice;
            }

            // For a buy limit order, the stop loss must be below the order price
            // Ensure stop loss is not set too close to the order price, considering the minimum stop level
            if (decision.stopLoss >= decision.orderPrice - stopLevelInPrice)
            {
                decision.stopLoss = decision.orderPrice - stopLevelInPrice; // Adjust this logic based on your risk management
            }
            
            Print($"After checking with stop_level: orderPrice={decision.orderPrice}, stopLoss={decision.stopLoss}");
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

            // Ensure the stop loss is not set too close to the current bid price, considering the minimum stop level
            double stopLevelInPrice = GetStopLevelPoints() * Symbol.PipSize;
            if (decision.stopLoss > Symbol.Bid - stopLevelInPrice)
            {
                decision.stopLoss = Symbol.Bid - stopLevelInPrice; // Adjust based on risk management
            }
            
            Print($"After checking with stop_level: orderPrice={decision.orderPrice} (no validation for instant buy), stopLoss={decision.stopLoss}");
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

            double currentstopLossLevel = Positions[0].StopLoss ?? 0;
            double stopLevelInPrice = currentstopLossLevel * Symbol.PipSize;

            // Assuming decision.takeProfit is a price, not a distance
            if (decision.takeProfit != 0 && decision.takeProfit < Symbol.Bid + stopLevelInPrice)
            {
                decision.takeProfit = Symbol.Bid + stopLevelInPrice; // Adjust as necessary
            }

            // Ensure stopLoss respects the minimum distance from the current price
            if (decision.stopLoss > Symbol.Bid - stopLevelInPrice)
            {
                decision.stopLoss = Symbol.Bid - stopLevelInPrice; // Adjust as necessary
            }
            Print($"After checking with stop_level: stopLoss={decision.stopLoss}, takeProfit={decision.takeProfit}");
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
            else if (PendingOrders.Count == 0 && Positions.Count == 1 && Positions[0].TakeProfit == 0)
            {
                return State.OpenPosition;
            }
            // Check for no orders and a single open position with a take profit
            else if (PendingOrders.Count == 0 && Positions.Count == 1 && Positions[0].TakeProfit != 0)
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
}