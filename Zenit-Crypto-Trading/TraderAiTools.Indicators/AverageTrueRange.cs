using System;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class AverageTrueRange : IIndicator
    {
        public AverageTrueRange(int length = 14)
        {
            _movingAverage = new SimpleMovingAverage(length);
        }

        private SimpleMovingAverage _movingAverage;
        private Candle _prevCandle;


        /// <summary>
        /// Whether the indicator is set.
        /// </summary>
        public bool IsFormed { get; set; }

        public void Reset()
        {
            IsFormed = false;

            _movingAverage.Reset();
        }

        public string Name => $"ATR{_movingAverage.Length}";

        public float GetTrueRange(Candle candle, Candle candlePrev)
        {
            var hl = Math.Abs(candle.HighBid - candle.LowBid);
            var hc = Math.Abs(candle.HighBid - candlePrev.CloseBid);
            var lc = Math.Abs(candle.LowBid - candlePrev.CloseBid);

            var tr = hl;
            if (tr < hc) tr = hc;
            if (tr < lc) tr = lc;
            return tr;
        }

        public SignalAndValue Process(Candle candle)
        {
            var ma = _movingAverage.Process(GetTrueRange(candle, _prevCandle), _prevCandle.IsComplete == 1);
            IsFormed = ma.IsFormed;

            _prevCandle = candle;
            return new SignalAndValue(ma.Value, ma.IsFormed);
        }
    }
}