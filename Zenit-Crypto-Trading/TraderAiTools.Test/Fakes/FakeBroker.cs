using System;
using System.Collections.Generic;
using TraderTools.Basics;

namespace TraderTools.Test.Fakes
{
    public class FakeBroker : IBroker
    {
        public string Name { get; }

        public List<string> GetSymbols()
        {
            throw new NotImplementedException();
        }

        public bool UpdateAccount(IBrokerAccount account, IBrokersCandlesService candlesService, IMarketDetailsService marketsService,
            Action<string> updateProgressAction, out List<Trade> addedOrUpdatedTrades)
        {
            throw new NotImplementedException();
        }

        public bool UpdateCandles(List<Candle> candles, string market, Timeframe timeframe, DateTime start, Action<string> progressUpdate)
        {
            throw new NotImplementedException();
        }

        public void Connect()
        {
            throw new NotImplementedException();
        }

        public ConnectStatus Status { get; }
        public BrokerKind Kind { get; }
        public List<TickData> GetTickData(IBroker broker, string market, DateTime utcStart, DateTime utcEnd)
        {
            throw new NotImplementedException();
        }

        public List<MarketDetails> GetMarketDetailsList()
        {
            throw new NotImplementedException();
        }

        public Candle? GetSingleCandle(string market, Timeframe timeframe, DateTime date)
        {
            throw new NotImplementedException();
        }
    }
}