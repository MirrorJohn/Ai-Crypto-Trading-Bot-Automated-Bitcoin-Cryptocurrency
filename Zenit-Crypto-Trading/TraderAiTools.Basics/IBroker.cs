using System;
using System.Collections.Generic;
using Hallupa.TraderTools.Basics;

namespace TraderTools.Basics
{
    public enum ConnectStatus
    {
        None,
        Connecting,
        Connected,
        Disconnecting,
        Disconnected
    }

    public enum BrokerKind
    {
        SpreadBet,
        Trade
    }

    public interface IBroker
    {
        string Name { get; }
        Dictionary<string, AssetBalance> GetBalance(DateTime? dateTimeUtc = null);
        List<string> GetSymbols();
        bool UpdateAccount(IBrokerAccount account, IBrokersCandlesService candlesService, IMarketDetailsService marketsService, Action<string> updateProgressAction, out List<Trade> addedOrUpdatedTrades);
        bool UpdateCandles(List<Candle> candles, string market, Timeframe timeframe, DateTime start, Action<string> progressUpdate);
        void Connect();
        ConnectStatus Status { get; }
        BrokerKind Kind { get; }
        List<TickData> GetTickData(IBroker broker, string market, DateTime utcStart, DateTime utcEnd);
        List<MarketDetails> GetMarketDetailsList();
        Candle? GetSingleCandle(string market, Timeframe timeframe, DateTime date);
    }
}