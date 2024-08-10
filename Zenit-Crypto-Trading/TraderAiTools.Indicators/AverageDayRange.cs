using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class AverageDayRange : IIndicator
    {
        public AverageDayRange()
        {
            MovingAverage = new WilderMovingAverage();
        }

        public WilderMovingAverage MovingAverage { get; }

        /// <summary>
        /// Whether the indicator is set.
        /// </summary>
        public bool IsFormed => MovingAverage.IsFormed;

        public void Reset()
        {
            MovingAverage.Reset();
        }

        public string Name => "ADR";

        public SignalAndValue Process(Candle candle)
        {
            return MovingAverage.Process(new Candle
            {
                CloseBid = candle.HighAsk - candle.LowAsk,
                IsComplete = candle.IsComplete
            });
        }
    }
}