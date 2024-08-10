using System;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class AwesomeOscillator : IIndicator
    {
        private readonly int _fm;
        private readonly int _sm;
        private readonly SimpleMovingAverage _smaSm;
        private readonly SimpleMovingAverage _smaFm;

        public AwesomeOscillator(int fm = 5, int sm = 35)
        {
            _fm = fm;
            _sm = sm;

            _smaFm = new SimpleMovingAverage(_fm);
            _smaSm = new SimpleMovingAverage(_sm);

            if (_fm >= _sm) throw new ApplicationException();
        }

        public bool IsFormed => _smaFm.IsFormed && _smaSm.IsFormed;

        public string Name => "Awesome Oscillator";

        public SignalAndValue Process(Candle candle)
        {
            var fmResult = _smaFm.Process(candle);
            var smResult = _smaSm.Process(candle);
            return new SignalAndValue(fmResult.Value - smResult.Value, _smaFm.IsFormed && _smaSm.IsFormed);
        }
        
        public void Reset()
        {
            _smaFm.Reset();
            _smaSm.Reset();
        }
    }
}