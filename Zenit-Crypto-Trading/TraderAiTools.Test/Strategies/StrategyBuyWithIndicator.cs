using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;
using TraderTools.Simulation;

namespace TraderTools.Test.Strategies
{
    public class StrategyBuyWithIndicator : StrategyBase
    {
        private IndicatorValues _ema;

        public StrategyBuyWithIndicator()
        {
            SetMarkets("UP");
            SetTimeframes(Timeframe.H1);
            _ema = EMA("UP", Timeframe.H1, 3);
        }

        public override void ProcessCandles(List<AddedCandleTimeframe> addedCandleTimeframes)
        {
            if (addedCandleTimeframes.All(c => c.Timeframe != Timeframe.H1)) return;

            var candles = Candles["UP"][Timeframe.H1];
            var candle = candles[candles.Count - 1];
            if (_ema.HasValue && _ema.Value > 4000 && !Trades.AnyOpen && !Trades.AllTrades.Any())
            {
                MarketLong("UP", Balance);
            }
        }
    }
}