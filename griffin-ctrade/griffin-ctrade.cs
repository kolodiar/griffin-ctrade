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
        [Parameter(DefaultValue = "Hello world!")]
        public string Message { get; set; }

        protected override void OnStart()
        {
            // To learn more about cTrader Automate visit our Help Center:
            // https://help.ctrader.com/ctrader-automate
            AiEnterResponse aiEnterResponse = new AiEnterResponse
                {
                    decision = true,       // Example value for the decision
                    volume = 100.0,        // Example value for the volume
                    orderPrice = 1.2345,   // Example value for the order price
                    stopLoss = 1.2300      // Example value for the stop loss
                };

            Print(aiEnterResponse);


            Print(Message);
        }

        protected override void OnTick()
        {
            // Handle price updates here
        }

        protected override void OnStop()
        {
            // Handle cBot stop here
        }
    }
}