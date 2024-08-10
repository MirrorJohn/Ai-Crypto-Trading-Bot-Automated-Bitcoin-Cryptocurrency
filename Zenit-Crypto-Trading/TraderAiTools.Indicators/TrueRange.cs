using System;
using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class TrueRange : IIndicator
    {
        private Candle? _prevCandle;

        public string Name => "TrueRange";

        /// <summary>
        /// To get price components to select the maximal value.
        /// </summary>
        /// <param name="currentCandle">The current candle.</param>
        /// <param name="prevCandle">The previous candle.</param>
        /// <returns>Price components.</returns>
        private float GetPriceMovementsMax(Candle currentCandle, Candle prevCandle)
        {
            var ret = currentCandle.HighBid - currentCandle.LowBid;
            var a = Math.Abs(prevCandle.CloseBid - currentCandle.HighBid);
            var b = Math.Abs(prevCandle.CloseBid - currentCandle.LowBid);

            if (a > ret)
            {
                ret = a;

                if (b > ret)
                {
                    ret = b;
                }
            }
            else if (b > ret)
            {
                ret = b;
            }

            return ret;
        }

        public void Reset()
        {
            _prevCandle = null;
            IsFormed = false;
        }

        public bool IsFormed { get; set; }

        public SignalAndValue Process(Candle candle)
        {
            if (_prevCandle != null)
            {
                if (candle.IsComplete == 1)
                    IsFormed = true;

                var priceMovementsMax = GetPriceMovementsMax(candle, _prevCandle.Value);

                if (candle.IsComplete == 1)
                    _prevCandle = candle;

                return new SignalAndValue(priceMovementsMax, IsFormed);
            }

            if (candle.IsComplete == 1)
                _prevCandle = candle;

            return new SignalAndValue(candle.HighBid - candle.LowBid, IsFormed);
        }
    }
}