using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class CommodityChannelIndex : LengthIndicator
    {
        private readonly MeanDeviation _mean = new MeanDeviation();

        public CommodityChannelIndex()
        {
            Length = 15;
        }

        public CommodityChannelIndex(int length)
        {
            Length = length;
        }

        public override string Name => "CCI";

        public override bool IsFormed => _mean.IsFormed;

        public override SignalAndValue Process(Candle candle)
        {
            var aveP = (candle.HighBid + candle.LowBid + candle.CloseBid) / 3.0;

            var meanValue = _mean.Process(
                new Candle
                {
                    CloseBid = (float)aveP,
                    IsComplete = candle.IsComplete
                });

            if (IsFormed && !meanValue.Value.Equals(0.0F))
                return new SignalAndValue((((float)aveP - _mean.Sma.CurrentValue) / (0.015F * meanValue.Value)), IsFormed);

            return new SignalAndValue(0.0F, false);
        }
    }
}