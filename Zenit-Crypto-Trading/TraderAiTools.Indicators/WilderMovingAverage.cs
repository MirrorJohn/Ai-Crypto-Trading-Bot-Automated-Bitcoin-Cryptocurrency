using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class WilderMovingAverage : IIndicator
    {
        private float _prevValue = 0;
        private int _valueCount = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="WilderMovingAverage"/>.
        /// </summary>
        public WilderMovingAverage()
        {
            Length = 32;
        }

        public int Length { get; }

        public WilderMovingAverage(int length)
        {
            Length = length;
        }


        public bool IsFormed => _valueCount >= Length;

        public string Name => $"WMA{Length}";

        public SignalAndValue Process(Candle candle)
        {
            var newValue = candle.CloseBid;

            var valueCount = _valueCount + 1;
            if (valueCount > Length) valueCount = Length;

            if (candle.IsComplete == 1)
            {
                _valueCount = valueCount;
            }

            var v = (_prevValue * (valueCount - 1) + newValue) / valueCount;

            if (candle.IsComplete == 1)
            {
                _prevValue = v;
            }

            return new SignalAndValue(v, IsFormed);
        }

        public void Reset()
        {
            _prevValue = 0;
            _valueCount = 0;
        }
    }
}