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
            TradeDecisionService tradeDecisionService = new TradeDecisionService();

            AiEnterResponse aiEnterResponse = tradeDecisionService.GetBuyDecision();

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