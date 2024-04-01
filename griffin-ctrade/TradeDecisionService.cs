using System;
using System.Text;
using System.Net.Http;
using System.Collections.Generic;
using System.Text.Json;
using System.Globalization;
using cAlgo.API.Internals;
using System.Threading;


public class TradeDecisionService
{
    private string buyUrl = "/api/v1/ctrader/buy";
    private string sellUrl = "/api/v1/ctrader/sell";
    private string sellUpdateUrl = "/api/v1/ctrader/sell-update";

    private HttpClient client = new HttpClient();

    public string mainCurrency { get; set; }
    public string secondCurrency { get; set; }
    public string ohlcSymbol { get; set; }
    public string obdSymbol { get; set; }

    public cAlgo.API.Robot robot;

    public TradeDecisionService(cAlgo.API.Robot robot)
    {
        this.robot = robot;
        
        // Assuming initialization with specific values
        client.BaseAddress = new Uri("http://localhost:8001");
        
        mainCurrency = "BTC";  // "DOGE";
        secondCurrency = "USD"; // Placeholder, adjust as needed
        ohlcSymbol = $"{mainCurrency}-{secondCurrency}";
        obdSymbol = $"{mainCurrency}{secondCurrency}T";
    }

    public AiEnterResponse GetBuyDecision(double ask, double bid)
    {
        var data = new Dictionary<string, string>
        {
            { "main_currency", mainCurrency },
            { "second_currency", secondCurrency },
            { "ohlc_symbol", ohlcSymbol },
            { "obd_symbol", obdSymbol },
            { "current_ask_price", ask.ToString() },
            { "current_bid_price", bid.ToString() }

        };

        // Convert dictionary to JSON string
        string jsonRequestData = JsonSerializer.Serialize(data);
        var content = new StringContent(jsonRequestData, Encoding.UTF8, "application/json");

        var response = client.PostAsync(buyUrl, content).Result.Content.ReadAsStringAsync().Result;  // "1,0.01,0.0855,0.0845";
        robot.Print($"AI buy decision response: {response}");
        
        // Splitting response and parsing
        var responseParams = response.Split(',');
        //robot.Print($"Parsed params list: {responseParams[0]}, {responseParams[1]}, {responseParams[2]}, {responseParams[3]}");
        bool buyDecision = responseParams[0] == "1";
        double volume   = responseParams[1] == "None" ? double.NaN : (double.TryParse(responseParams[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedValue) ? parsedValue : double.NaN);
        // double buyPrice = responseParams[2] == "None" ? double.NaN : (double.TryParse(responseParams[2], NumberStyles.Any, CultureInfo.InvariantCulture, out parsedValue) ? parsedValue : double.NaN);
        // double stopLoss = responseParams[3] == "None" ? double.NaN : (double.TryParse(responseParams[3], NumberStyles.Any, CultureInfo.InvariantCulture, out parsedValue) ? parsedValue : double.NaN);
  
        return new AiEnterResponse
        {
            decision = buyDecision,
            volume = volume,
            // orderPrice = buyPrice,
            // stopLoss = stopLoss
        };
    }

    public AiExitResponse GetSellDecision(double ask, double bid, double boughtPrice, double stopLoss, string timestamp)
     {
         var data = new Dictionary<string, string>
         {
             { "main_currency", mainCurrency },
             { "second_currency", secondCurrency },
             { "ohlc_symbol", ohlcSymbol },
             { "obd_symbol", obdSymbol },
             { "current_ask_price", ask.ToString(CultureInfo.InvariantCulture) },
             { "current_bid_price", bid.ToString(CultureInfo.InvariantCulture) },
             { "entered_trade_price", boughtPrice.ToString(CultureInfo.InvariantCulture) },
             { "current_stop_loss", stopLoss.ToString(CultureInfo.InvariantCulture) },
             { "timestamp_bought", timestamp }
         };

         // Convert dictionary to JSON string
         string jsonRequestData = JsonSerializer.Serialize(data);
         var content = new StringContent(jsonRequestData, Encoding.UTF8, "application/json");
         
         // Sending a synchronous request, but consider using async/await pattern in real applications
        var response = client.PostAsync(sellUrl, content).Result.Content.ReadAsStringAsync().Result; // "1,0.0841,0.088"; 
        robot.Print($"AI sell decision response: {response}");

        // Splitting response and parsing
        var responseParams = response.Split(',');
        bool sellDecision = responseParams[0] == "1";
        // double newTakeProfit = responseParams[1] == "None" ? double.NaN : (double.TryParse(responseParams[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedValue) ? parsedValue : double.NaN);
        // double newStopLoss =   responseParams[2] == "None" ? double.NaN : (double.TryParse(responseParams[2], NumberStyles.Any, CultureInfo.InvariantCulture, out parsedValue) ? parsedValue : double.NaN);
  
        return new AiExitResponse
        {
            decision = sellDecision,
            // stopLoss = newStopLoss,
            // takeProfit = newTakeProfit
        };
     }

    public AiExitResponse GetSellUpdateDecision(double ask, double bid, double boughtPrice, double takeProfit, double stopLoss, string timestamp)
    {
        
        var data = new Dictionary<string, string>
        {
            { "main_currency", mainCurrency },
            { "second_currency", secondCurrency },
            { "ohlc_symbol", ohlcSymbol },
            { "obd_symbol", obdSymbol },
            { "current_ask_price", ask.ToString() },
            { "current_bid_price", bid.ToString() },
            { "entered_trade_price", boughtPrice.ToString() },
            { "current_take_profit", takeProfit.ToString() },
            { "current_stop_loss", stopLoss.ToString() },
            { "timestamp_bought", timestamp }
        };

        // Convert dictionary to JSON string
        string jsonRequestData = JsonSerializer.Serialize(data);
        var content = new StringContent(jsonRequestData, Encoding.UTF8, "application/json");
        
        // Sending a synchronous request, but consider using async/await pattern in real applications
        var responseString = client.PostAsync(sellUpdateUrl, content).Result.Content.ReadAsStringAsync().Result; // "1,0.085,0.0872"; 
        robot.Print($"AI sell decision response: {responseString}");

        // Splitting response and parsing
        var responseParams = responseString.Split(',');
        bool sellDecision = responseParams[0] == "1";
        // double newTakeProfit = responseParams[1] == "None" ? double.NaN : (double.TryParse(responseParams[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedValue) ? parsedValue : double.NaN);
        // double newStopLoss =   responseParams[2] == "None" ? double.NaN : (double.TryParse(responseParams[2], NumberStyles.Any, CultureInfo.InvariantCulture, out parsedValue) ? parsedValue : double.NaN);
  
        return new AiExitResponse
        {
            decision = sellDecision,
            // stopLoss = newStopLoss,
            // takeProfit = newTakeProfit
        };
    }
}


// TODO:
// 1) define what we want for stop loss and take profit
// 2) if stoploss is invalid, but takeprofit is valid, don't cancell update - we have to separate rhose things
// 3)  
// Let's for now do: imidiate enter on signal, imidiate exit on signal. Stop loss by AI, moving up, keeping min distance
// now for testing sell decision let's just set stoploss to 1% for more freedom

// possible approaches:
// 1) run decision process when the position fails on stoploss. how to detect: flag 'position_clossed_intentionally'. So when the stoploss is trigerred, new position can be opened.:
//    - does not make sense as there are no new data,
//    - enters again beccause the global trend is bullish, but fails as the local one is bearish. 
//    + somehow exit decision detects bearish trend now, but enter decision considers trend that might happen in foloowing ticks.
//    ? maybe specify in enter prompt that the position will be entered instantly at that moment (so it knows that we are not waiting)
// 2) update stop_loss in each tick, not once per hour

// exit and then enter right after it??? how?
// ai response: decision=<ExitTradeDecision.EXIT: 'exit'> description='The decision to exit the trade is based on several key indicators and market conditions. Firstly, the RSI at 72.11 indicates that the asset is currently overbought, suggesting a potential reversal or pullback in price. Additionally, the MACD value is significantly above its signal line, which could indicate that the market is overextended to the upside and might correct soon. The current price is also very close to the Bollinger High, further suggesting that the price is at a relative extreme and could revert. The order book depth data shows significant resistance near the current price level with a large number of asks stacked just above the current price, indicating potential selling pressure that could cap further upside. Given these conditions, exiting the trade seems prudent to lock in profits and avoid potential downside risk. Risk management strategy involves setting a tight stop loss just above the bought price to ensure any adverse move is quickly mitigated. The expected profit is calculated based on the current market conditions and the potential for a pullback from overbought levels.' exit_trade_confidence=0.85 wait_confidence=0.15 total_cost=0.06116 total_tokens=5606 prompt_tokens=5351 completion_tokens=255 expected_profit=67068.34
// ai response: decision=<EnterTradeDecision.ENTER: 'enter'> description='Based on the preprocessed indicators, the market shows a bullish trend. The EMA (66109.87) is above the SMA (65980.41), indicating a potential upward momentum. The RSI at 75.21 suggests the market is currently overbought, which usually precedes a continuation of the current trend in high momentum markets. The MACD (548.76) being above its signal line (351.78) further confirms the bullish momentum. The order book depth data shows a significant amount of bids just below the current price, providing a strong support level, which decreases the risk of a sharp decline. Considering these factors, entering a long trade is advisable. Risk management strategy involves setting a stop-loss order at the lower Bollinger Band (64694.33) to minimize potential losses. The expected profit is calculated based on the current momentum and resistance levels, aiming for a target just below the upper Bollinger Band (67266.49).' enter_trade_confidence=0.75 wait_confidence=0.25 total_cost=0.060390000000000006 total_tokens=5551 prompt_tokens=5307 completion_tokens=244 expected_profit=67200.0
//





