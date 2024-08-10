using System;
using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Indicators
{
    public class ParabolicSar : IIndicator
    {
        public string Name { get; } = "ParabolicSar";

        private double _prevValue;
        private readonly List<Candle> _candles = new List<Candle>();
        private bool _longPosition;
        private double _xp;        // Extreme Price
        private double _af;         // Acceleration factor
        private int _prevBar;
        private double _currentValue;
        private bool _afIncreased;
        private int _reverseBar;
        private double _reverseValue;
        private double _prevSar;
        private double _todaySar;

        public ParabolicSar()
        {
            Acceleration = 0.02;
            AccelerationStep = 0.02;
            AccelerationMax = 0.2;
        }

        public double Acceleration { get; set; }

        public bool IsFormed { get; private set; }

        public double AccelerationStep { get; set; }

        public double AccelerationMax { get; set; }

        public Signal Signal { get; private set; }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public SignalAndValue Process(Candle candle)
        {
            if (_candles.Count == 0)
                _candles.Add(candle);

            _prevValue = _currentValue;

            if (candle.OpenTimeTicks != _candles[_candles.Count - 1].OpenTimeTicks)
            {
                _candles.Add(candle);
            }
            else
                _candles[_candles.Count - 1] = candle;

            if (_candles.Count < 3)
            {
                _currentValue = _prevValue;
                return new SignalAndValue((float) _prevValue, IsFormed);
            }

            if (_candles.Count == 3)
            {
                _longPosition = _candles[_candles.Count - 1].HighBid > _candles[_candles.Count - 2].HighBid;
                var max = _candles.Max(t => t.HighBid);
                var min = _candles.Min(t => t.LowBid);
                _xp = _longPosition ? max : min;
                _af = Acceleration;
                var v = (float) (_xp + (_longPosition ? -1 : 1) * (max - min) * _af);
                _currentValue = v;
                return new SignalAndValue(v, IsFormed);
            }

            if (_afIncreased && _prevBar != _candles.Count)
                _afIncreased = false;

            if (candle.IsComplete == 1)
                IsFormed = true;

            var value = _prevValue;

            if (_reverseBar != _candles.Count)
            {
                _todaySar = TodaySar(_prevValue + _af * (_xp - _prevValue));

                for (var x = 1; x <= 2; x++)
                {
                    if (_longPosition)
                    {
                        if (_todaySar > _candles[_candles.Count - 1 - x].LowBid)
                            _todaySar = _candles[_candles.Count - 1 - x].LowBid;
                    }
                    else
                    {
                        if (_todaySar < _candles[_candles.Count - 1 - x].HighBid)
                            _todaySar = _candles[_candles.Count - 1 - x].HighBid;
                    }
                }

                if ((_longPosition && (_candles[_candles.Count - 1].LowBid < _todaySar || _candles[_candles.Count - 2].LowBid < _todaySar))
                        || (!_longPosition && (_candles[_candles.Count - 1].HighBid > _todaySar || _candles[_candles.Count - 2].HighBid > _todaySar)))
                {
                    var v = (float)Reverse();
                    _currentValue = v;
                    return new SignalAndValue(v, IsFormed);
                }

                if (_longPosition)
                {
                    if (_prevBar != _candles.Count || _candles[_candles.Count - 1].LowBid < _prevSar)
                    {
                        value = _todaySar;
                        _prevSar = _todaySar;
                    }
                    else
                        value = _prevSar;

                    if (_candles[_candles.Count - 1].HighBid > _xp)
                    {
                        _xp = _candles[_candles.Count - 1].HighBid;
                        AfIncrease();
                    }
                }
                else if (!_longPosition)
                {
                    if (_prevBar != _candles.Count || _candles[_candles.Count - 1].HighBid > _prevSar)
                    {
                        value = _todaySar;
                        _prevSar = _todaySar;
                    }
                    else
                        value = _prevSar;

                    if (_candles[_candles.Count - 1].LowBid < _xp)
                    {
                        _xp = _candles[_candles.Count - 1].LowBid;
                        AfIncrease();
                    }
                }

            }
            else
            {
                if (_longPosition && _candles[_candles.Count - 1].HighBid > _xp)
                    _xp = _candles[_candles.Count - 1].HighBid;
                else if (!_longPosition && _candles[_candles.Count - 1].LowBid < _xp)
                    _xp = _candles[_candles.Count - 1].LowBid;

                value = _prevSar;

                _todaySar = TodaySar(_longPosition ? Math.Min(_reverseValue, _candles[_candles.Count - 1].LowBid) :
                    Math.Max(_reverseValue, _candles[_candles.Count - 1].HighBid));
            }

            _prevBar = _candles.Count;

            _currentValue = value;
            return new SignalAndValue((float)value, IsFormed);
        }

        private double TodaySar(double todaySar)
        {
            if (Signal == Signal.Long)
            {
                var lowestSar = Math.Min(Math.Min(todaySar, (double)_candles[_candles.Count - 1].LowBid), (double)_candles[_candles.Count - 2].LowBid);
                todaySar = (double)_candles[_candles.Count - 1].LowBid > lowestSar ? lowestSar : Reverse();
            }
            else
            {
                var highestSar = Math.Max(Math.Max(todaySar, (double)_candles[_candles.Count - 1].HighBid), (double)_candles[_candles.Count - 2].HighBid);
                todaySar = (double)_candles[_candles.Count - 1].HighBid < highestSar ? highestSar : Reverse();
            }

            return todaySar;
        }

        private double Reverse()
        {
            var todaySar = _xp;

            if ((Signal == Signal.Long && _prevSar > (double)_candles[_candles.Count - 1].LowBid) ||
                (Signal != Signal.Long && _prevSar < (double)_candles[_candles.Count - 1].HighBid) || _prevBar != _candles.Count)
            {
                Signal = Signal == Signal.Long ? Signal.Short : Signal.Long;
                _reverseBar = _candles.Count;
                _reverseValue = _xp;
                _af = Acceleration;
                _xp = Signal == Signal.Long ? (double)_candles[_candles.Count - 1].HighBid : (double)_candles[_candles.Count - 1].LowBid;
                _prevSar = todaySar;
            }
            else
                todaySar = _prevSar;

            return todaySar;
        }

        private void AfIncrease()
        {
            if (_afIncreased)
                return;

            _af = Math.Min(AccelerationMax, _af + AccelerationStep);
            _afIncreased = true;
        }
    }
}