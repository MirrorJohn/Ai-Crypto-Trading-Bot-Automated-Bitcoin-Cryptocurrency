using System;
using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;
using TraderTools.Simulation;

namespace TraderTools.Test.Strategies
{
    public class StrategyBuyWithExpiredOrder : StrategyBase
    {
        private readonly long _expireTimeTicks;
        private bool _ordered = false;

        public StrategyBuyWithExpiredOrder(long expireTimeTicks)
        {
            _expireTimeTicks = expireTimeTicks;
            SetMarkets("UP");
            SetTimeframes(Timeframe.H1);
        }

        public override void ProcessCandles(List<AddedCandleTimeframe> addedCandleTimeframes)
        {
            if (addedCandleTimeframes.All(c => c.Timeframe != Timeframe.H1)) return;

            if (!_ordered)
            {
                OrderLong("UP", 10000M, 6000M, expire: new DateTime(_expireTimeTicks));
                _ordered = true;
            }
        }
    }
}