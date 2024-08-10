using TraderTools.Basics;

namespace TraderTools.Indicators
{
    /// <summary>
    /// Convergence/divergence of moving averages with signal line.
    /// </summary>
    public class MovingAverageConvergenceDivergenceSignal : IIndicator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MovingAverageConvergenceDivergenceSignal"/>.
        /// </summary>
        public MovingAverageConvergenceDivergenceSignal(string name = "MACD Signal")
        {
            Name = name;
            Macd = new MovingAverageConvergenceDivergence();
            SignalMa = new ExponentialMovingAverage(9);
        }

        public bool IsFormed => SignalMa.IsFormed;
        public string Name { get; }

        public SignalAndValue Process(Candle candle)
        {
            var value = Macd.Process(candle);
            return SignalMa.Process(new Candle
            {
                CloseBid = value.Value,
                IsComplete = candle.IsComplete
            });
        }

        public MovingAverageConvergenceDivergence Macd { get; }

        public ExponentialMovingAverage SignalMa { get; }


        public void Reset()
        {
            Macd.Reset();
            SignalMa.Reset();
        }
    }
}