using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class BollingerBand : IIndicator
    {
        private readonly float _width;
        private SimpleMovingAverage _sma;
        private StandardDeviation _dev;
        public bool IsFormed => _sma.IsFormed && _dev.IsFormed;
        public string Name => $"Bollinger Band {_width:0.0}";

        public BollingerBand()
            : this(2.0F, 20)
        {
        }

        public BollingerBand(float width, int length)
        {
            _width = width;
            _sma = new SimpleMovingAverage(length);
            _dev = new StandardDeviation(length);
        }

        public void Reset()
        {
            _sma.Reset();
            _dev.Reset();
        }

        public SignalAndValue Process(Candle candle)
        {
            _dev.Process(candle);
            _sma.Process(candle);

            return new SignalAndValue(
                _sma.CurrentValue + (_width * _dev.CurrentValue),
                _sma.IsFormed && _dev.IsFormed);
        }
    }
}