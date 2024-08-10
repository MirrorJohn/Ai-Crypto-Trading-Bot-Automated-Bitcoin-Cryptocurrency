using System;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;

namespace Hallupa.TraderTools.Basics
{
    public interface ITradeFactory
    {
        Trade CreateOrder(string broker, decimal entryOrder, Candle latestCandle,
            TradeDirection direction, decimal amount, string market, string baseAsset, DateTime? orderExpireTime,
            decimal? stop, decimal? limit, CalculateOptions calculateOptions = CalculateOptions.Default);

        Trade CreateMarketEntry(string broker, decimal entryPrice, DateTime entryTime,
            TradeDirection direction, decimal amount, string market, string baseAsset,
            decimal? stop, decimal? limit,
            Timeframe? timeframe = null, string strategies = null, string comments = null, bool alert = false,
            CalculateOptions calculateOptions = CalculateOptions.Default,
            TradeUpdateMode updateMode = TradeUpdateMode.Default);

        void UpdateTrade(Trade trade);
    }

    public class TradeFactory : ITradeFactory

    {
        public Trade CreateOrder(string broker, decimal entryOrder, Candle latestCandle,
            TradeDirection direction, decimal amount, string market, string baseAsset, DateTime? orderExpireTime,
            decimal? stop, decimal? limit, CalculateOptions calculateOptions = CalculateOptions.Default)
        {
            var orderDateTime = latestCandle.CloseTime();

            var trade = new Trade { CalculateOptions = calculateOptions };

            trade.SetOrder(orderDateTime, entryOrder, market, direction, amount, orderExpireTime);
            if (stop != null) trade.AddStopPrice(orderDateTime, stop.Value);
            if (limit != null) trade.AddLimitPrice(orderDateTime, limit.Value);
            trade.Broker = broker;
            trade.BaseAsset = baseAsset;

            if (direction == TradeDirection.Long)
            {
                trade.OrderType = (float)entryOrder <= latestCandle.CloseAsk ? OrderType.LimitEntry : OrderType.StopEntry;
            }
            else
            {
                trade.OrderType = (float)entryOrder <= latestCandle.CloseBid ? OrderType.StopEntry : OrderType.LimitEntry;
            }

            return trade;
        }

        public Trade CreateMarketEntry(string broker, decimal entryPrice, DateTime entryTime,
            TradeDirection direction, decimal amount, string market, string baseAsset,
            decimal? stop, decimal? limit,
            Timeframe? timeframe = null, string strategies = null, string comments = null, bool alert = false,
            CalculateOptions calculateOptions = CalculateOptions.Default,
            TradeUpdateMode updateMode = TradeUpdateMode.Default)
        {
            var trade = new Trade { Broker = broker, CalculateOptions = calculateOptions };

            if (stop != null) trade.AddStopPrice(entryTime, stop.Value);
            if (limit != null) trade.AddLimitPrice(entryTime, limit.Value);
            trade.Market = market;
            trade.BaseAsset = baseAsset;
            trade.TradeDirection = direction;
            trade.EntryPrice = entryPrice;
            trade.EntryDateTime = entryTime;
            trade.EntryQuantity = amount;
            trade.Timeframe = timeframe;
            trade.Alert = alert;
            trade.Comments = comments;
            trade.Strategies = strategies;
            trade.UpdateMode = updateMode;
            return trade;
        }

        public void UpdateTrade(Trade trade)
        {
        }
    }
}