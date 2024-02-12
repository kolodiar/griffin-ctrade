using System;
using System.Text;
using System.Net.Http;
using System.Collections.Generic;
using System.Text.Json;
using cAlgo.API.Internals;


public class TradeDecisionService
{
    private string buyUrl = "/api/v1/mt/buy";
    private string sellUrl = "/api/v1/mt/sell";
    private string sellUpdateUrl = "/api/v1/mt/sell_update";

    private HttpClient client = new HttpClient();

    public string mainCurrency { get; set; }
    public string secondCurrency { get; set; }
    public string ohlcSymbol { get; set; }
    public string obdSymbol { get; set; }

    public cAlgo.API.Robot robot;

    public TradeDecisionService(cAlgo.API.Robot robot)
    {
        robot = robot;
        
        // Assuming initialization with specific values
        client.BaseAddress = new Uri("http://localhost:8001");
        
        mainCurrency = "DOGE";
        secondCurrency = "USD"; // Placeholder, adjust as needed
        ohlcSymbol = $"{mainCurrency}-{secondCurrency}";
        obdSymbol = $"{mainCurrency}{secondCurrency}T";
    }

    public AiEnterResponse GetBuyDecision()
    {
        var data = new Dictionary<string, string>
        {
            { "main_currency", mainCurrency },
            { "second_currency", secondCurrency },
            { "ohlc_symbol", ohlcSymbol },
            { "obd_symbol", obdSymbol }
        };
        // Convert dictionary to JSON string
        string jsonRequestData = JsonSerializer.Serialize(data);
        var content = new StringContent(jsonRequestData, Encoding.UTF8, "application/json");

        var response = "1,0.01,0.826,0.8"; //client.PostAsync(buyUrl, content).Result.Content.ReadAsStringAsync().Result;
        Console.WriteLine($"AI buy decision response: {response}");
        robot.Print($"AI buy decision response: {response} (robot print)");


        // Splitting response and parsing
        var responseParams = response.Split(',');
        bool buyDecision = responseParams[0] == "1";
        double volume = double.Parse(responseParams[1]);
        double buyPrice = double.Parse(responseParams[2]); // Assuming this conversion is necessary
        double stopLoss = double.Parse(responseParams[3]);

        return new AiEnterResponse
        {
            decision = buyDecision,
            volume = volume,
            orderPrice = buyPrice,
            stopLoss = stopLoss
        };
    }

    public AiExitResponse GetSellDecision(double boughtPrice, double stopLoss, string timestamp)
    {
        var data = new Dictionary<string, string>
        {
            { "main_currency", mainCurrency },
            { "second_currency", secondCurrency },
            { "ohlc_symbol", ohlcSymbol },
            { "obd_symbol", obdSymbol },
            { "bought_price", boughtPrice.ToString() },
            { "stop_loss", stopLoss.ToString() },
            { "timestamp", timestamp }
        };

        // Convert dictionary to JSON string
        string jsonRequestData = JsonSerializer.Serialize(data);
        var content = new StringContent(jsonRequestData, Encoding.UTF8, "application/json");

        // Sending a synchronous request, but consider using async/await pattern in real applications
        var response = client.PostAsync(sellUrl, content).Result.Content.ReadAsStringAsync().Result;
        Console.WriteLine($"AI sell decision response: {response}");

        // Splitting response and parsing
        var responseParams = response.Split(',');
        bool sellDecision = responseParams[0] == "1";
        double newStopLoss = double.Parse(responseParams[1]);
        double newTakeProfit = double.Parse(responseParams[2]);

        return new AiExitResponse
        {
            decision = sellDecision,
            stopLoss = newStopLoss,
            takeProfit = newTakeProfit
        };
    }

    public AiExitResponse GetSellUpdateDecision(double boughtPrice, double takeProfit, double stopLoss, string timestamp)
    {
        var data = new Dictionary<string, string>
        {
            { "main_currency", mainCurrency },
            { "second_currency", secondCurrency },
            { "ohlc_symbol", ohlcSymbol },
            { "obd_symbol", obdSymbol },
            { "bought_price", boughtPrice.ToString() },
            { "take_profit", takeProfit.ToString() },
            { "stop_loss", stopLoss.ToString() },
            { "timestamp", timestamp }
        };

        // Convert dictionary to JSON string
        string jsonRequestData = JsonSerializer.Serialize(data);
        var content = new StringContent(jsonRequestData, Encoding.UTF8, "application/json");

        // Sending a synchronous request, but consider using async/await pattern in real applications
        var responseString = client.PostAsync(sellUpdateUrl, content).Result.Content.ReadAsStringAsync().Result;
        Console.WriteLine($"AI sell decision response: {responseString}");

        // Splitting response and parsing
        var responseParams = responseString.Split(',');
        bool sellDecision = responseParams[0] == "1";
        double newStopLoss = double.Parse(responseParams[1]);
        double newTakeProfit = double.Parse(responseParams[2]);

        return new AiExitResponse
        {
            decision = sellDecision,
            stopLoss = newStopLoss,
            takeProfit = newTakeProfit
        };
    }
}
