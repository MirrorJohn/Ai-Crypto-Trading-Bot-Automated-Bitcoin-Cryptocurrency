using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class StochasticRelativeStrengthIndex : IIndicator
    {
        private readonly int _length;
        private List<float> Buffer { get; } = new List<float>();
        private RelativeStrengthIndex _rsi;
        public bool IsFormed => _rsi.IsFormed;
        public string Name => "Stoch RSI";

        public StochasticRelativeStrengthIndex(int length = 14)
        {
            _length = length;
            _rsi = new RelativeStrengthIndex();
        }

        public SignalAndValue Process(Candle candle)
        {
            var rsi = _rsi.Process(candle);

            Buffer.Add(rsi.Value);

            if (Buffer.Count > _length) Buffer.RemoveAt(0);

            var minRsi = Buffer.Min();
            var maxRsi = Buffer.Max();

            // If candle isn't complete, remove it
            if (candle.IsComplete == 0)
            {
                Buffer.RemoveAt(Buffer.Count - 1);
            }

            return new SignalAndValue(
                rsi.IsFormed && maxRsi - minRsi != 0 
                    ? (rsi.Value - minRsi) / (maxRsi - minRsi)
                    : 0,
                rsi.IsFormed);
        }

        public void Reset()
        {
            Buffer.Clear();
            _rsi.Reset();
        }
    }
}
