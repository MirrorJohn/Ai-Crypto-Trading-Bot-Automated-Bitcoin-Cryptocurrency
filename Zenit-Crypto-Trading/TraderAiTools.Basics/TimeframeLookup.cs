using System;
using System.Collections;
using System.Collections.Generic;

namespace TraderTools.Basics
{
    public class TimeframeLookup<T> : IEnumerable<KeyValuePair<Timeframe, T>>
    {
        private T[] _items = new T[11];

        public T this[Timeframe timeframe]
        {
            get => _items[GetLookupIndex(timeframe)];
            set => _items[GetLookupIndex(timeframe)] = value;
        }

        public List<Timeframe> GetSetTimeframes()
        {
            var timeframes = new List<Timeframe>();
            for (var i = 0; i < _items.Length; i++)
            {
                if (_items[i] != null)
                {
                    timeframes.Add(GetTimeframe(i));
                }
            }

            return timeframes;
        }

        public T this[int timeframeIndex]
        {
            get => _items[timeframeIndex];
            set => _items[timeframeIndex] = value;
        }

        public void Add(Timeframe timeframe, T instance)
        {
            _items[GetLookupIndex(timeframe)] = instance;
        }

        public static int GetLookupIndex(Timeframe timeframe)
        {
            switch (timeframe)
            {
                case Timeframe.M1:
                    return 0;
                case Timeframe.M5:
                    return 1;
                case Timeframe.M15:
                    return 2;
                case Timeframe.M30:
                    return 3;
                case Timeframe.H1:
                    return 4;
                case Timeframe.H2:
                    return 5;
                case Timeframe.H4:
                    return 6;
                case Timeframe.H8:
                    return 7;
                case Timeframe.D1:
                    return 8;
                case Timeframe.D7:
                    return 9;
                case Timeframe.D30:
                    return 10;
            }

            throw new ApplicationException($"TimeframeLookup - {timeframe} not supported");
        }

        private Timeframe GetTimeframe(int index)
        {
            switch (index)
            {
                case 0:
                    return Timeframe.M1;
                case 1:
                    return Timeframe.M5;
                case 2:
                    return Timeframe.M15;
                case 3:
                    return Timeframe.M30;
                case 4:
                    return Timeframe.H1;
                case 5:
                    return Timeframe.H2;
                case 6:
                    return Timeframe.H4;
                case 7:
                    return Timeframe.H8;
                case 8:
                    return Timeframe.D1;
                case 9:
                    return Timeframe.D7;
                case 10:
                    return Timeframe.D30;
            }

            throw new ApplicationException($"TimeframeLookup - {index} not supported");
        }

        public IEnumerator<KeyValuePair<Timeframe, T>> GetEnumerator()
        {
            for (var i = 0; i < _items.Length; i++)
            {
                yield return new KeyValuePair<Timeframe, T>(GetTimeframe(i), _items[i]);
            }
            
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}