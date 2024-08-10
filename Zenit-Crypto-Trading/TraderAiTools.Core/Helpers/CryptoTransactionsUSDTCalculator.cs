using System;
using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;

namespace TraderTools.Core.Helpers
{
    public class CryptoTransactionsUSDTCalculator
    {
        #region Fields
        private readonly IBrokersCandlesService _candlesService;
        private readonly IBroker _binanceBroker;
        #endregion

        #region Constructors
        public CryptoTransactionsUSDTCalculator(IBrokersCandlesService candlesService, IBroker binanceBroker)
        {
            _candlesService = candlesService;
            _binanceBroker = binanceBroker;
        }
        #endregion

        public List<Transaction> CombineBuySell(List<Transaction> txns)
        {
            var openBuyTxnsLookup = new Dictionary<string, List<Transaction>>();
            foreach (var t in txns)
            {
                // Process buys
                if (t.TransactionType == TransactionType.Buy)
                {
                    if (!openBuyTxnsLookup.TryGetValue(t.Asset1, out var openBuyTxns))
                    {
                        openBuyTxnsLookup[t.Asset1] = (openBuyTxns = new List<Transaction>());
                    }

                    openBuyTxns.Add(t);
                    continue;
                }

                // Process sells
                if (t.TransactionType == TransactionType.Sell)
                {
                    t.DateTimeEnd = t.DateTimeStart;
                    if (!openBuyTxnsLookup.TryGetValue(t.Asset1, out var openBuyTxns))
                    {
                        continue;
                    }

                    var amountToSell = t.Amount;

                    for (var i = 0; i < openBuyTxns.Count; i++)
                    {
                        var openBuy = openBuyTxns[i];
                        var q = Math.Min(amountToSell, openBuy.Amount);

                        if (i == 0)
                        {
                            t.DateTimeStart = openBuy.DateTimeStart;
                        }

                        openBuy.Amount -= q;
                        openBuy.ProceedsGBP = openBuy.PriceGBP * openBuy.Amount;
                        t.TransactionType = TransactionType.BuySell;
                        t.Amount += q;
                        t.ProceedsGBP -= openBuy.PriceGBP * q;

                        if (openBuy.Amount == 0)
                        {
                            openBuyTxns.RemoveAt(i);
                            i--;
                        }

                        if (amountToSell == 0) break;
                    }
                }
            }

            // Remove zero txns
            for (var i = txns.Count - 1; i >= 0; i--)
            {
                if (txns[i].TransactionType == TransactionType.Buy && txns[i].Amount == 0)
                {
                    txns.RemoveAt(i);
                }
            }

            return txns;
        }

       /*public List<Transaction> CalculateTransactions(
            List<TradeDetails> trades, List<DepositWithdrawal> depositWithdrawals, bool updateCandles)
        {
            var ret = new List<Transaction>();

            // Add transactions
            AddTransactions(depositWithdrawals, ret);
            AddTransactions(trades, ret);
            ret = ret.OrderBy(t => t.DateTimeStart).ToList();

            // Get fiat values
            foreach (var t in ret)
            {
                var gbpValue = AssetValueCalculator.GetCryptoAssetApproxUsdValue(_candlesService, _binanceBroker, t.Asset1, t.DateTimeStart, t.Amount, out var success, updateCandles);

                if (!success && !string.IsNullOrEmpty(t.Asset2))
                {
                    // Try calculating with asset 2
                    gbpValue = AssetValueCalculator.GetCryptoAssetApproxUsdValue(_candlesService, _binanceBroker, t.Asset2, t.DateTimeStart, t.Amount * t.Price, out success, updateCandles);
                }

                if (!success && t.TransactionType != TransactionType.Withdraw && t.TransactionType != TransactionType.Deposit)
                    throw new ApplicationException($"Unable to calculate asset value");

                t.ProceedsGBP = gbpValue;
                t.PriceGBP = gbpValue / t.Amount;
            }

            return ret;
        }

        private void AddTransactions(List<TradeDetails> trades, List<Transaction> ret)
        {
            // Trades to transactions
            foreach (var t in trades)
            {
                if (t.EntryDateTime == null || t.EntryQuantity == 0M) continue;

                ret.Add(new Transaction
                {
                    DateTimeStart = t.EntryDateTime.Value,
                    Asset1 = t.BaseAsset,
                    Asset2 = t.Market.Replace(t.BaseAsset, string.Empty),
                    Price = t.EntryPrice.Value,
                    Broker = t.Broker,
                    Amount = t.EntryQuantity.Value,
                    TransactionType = t.TradeDirection == TradeDirection.Long ? TransactionType.Buy : TransactionType.Sell,
                    Id = t.Id
                });

                if (t.Commission > 0M)
                {
                    ret.Add(new Transaction
                    {
                        DateTimeStart = t.EntryDateTime.Value,
                        DateTimeEnd = t.EntryDateTime.Value,
                        Asset1 = t.CommissionAsset,
                        Price = t.CommissionAsset == t.BaseAsset ? t.EntryPrice.Value : 1 / t.EntryPrice.Value,
                        Asset2 = t.Market.Replace(t.CommissionAsset, string.Empty),
                        Broker = t.Broker,
                        Amount = t.Commission.Value,
                        TransactionType = TransactionType.Commission,
                        Id = t.Id
                    });
                }
            }
        }*/

        private static void AddTransactions(List<DepositWithdrawal> depositWithdrawals, List<Transaction> ret)
        {
            // Depoits/withdrawals to transactions
            foreach (var dw in depositWithdrawals)
            {
                if (dw.Amount == 0M) continue;

                ret.Add(new Transaction
                {
                    DateTimeStart = dw.Time,
                    DateTimeEnd = dw.Time,
                    Asset1 = dw.Asset,
                    Broker = dw.Broker,
                    Amount = Math.Abs(dw.Amount),
                    TransactionType = dw.Amount < 0 ? TransactionType.Withdraw : TransactionType.Deposit,
                    Id = dw.Id
                });

                if (dw.Commission > 0)
                {
                    ret.Add(new Transaction
                    {
                        DateTimeStart = dw.Time,
                        DateTimeEnd = dw.Time,
                        Asset1 = dw.CommissionAsset,
                        Broker = dw.Broker,
                        Amount = dw.Commission,
                        TransactionType = TransactionType.Commission,
                        Id = dw.Id
                    });
                }
            }
        }

        public static List<Trade> GroupTrades(List<Trade> trades)
        {
            // TODO: This needs finishing
            var id = 0;
            var groupedTradesByOrderIdList = trades.GroupBy(t => new
            { OrderId = string.IsNullOrEmpty(t.OrderId) ? (id++).ToString() : t.OrderId, t.Broker }).ToList();

            var simplifiedTrades = new List<Trade>();
            foreach (var groupedTradesByOrderId in groupedTradesByOrderIdList)
            {
                var firstTrade = groupedTradesByOrderId.First();
                var trade = new Trade
                {
                    BaseAsset = firstTrade.BaseAsset,
                    Broker = firstTrade.Broker,
                    Comments = firstTrade.Comments,
                    Strategies = firstTrade.Strategies,
                    OrderId = firstTrade.OrderId,
                    CommissionAsset = firstTrade.CommissionAsset,
                    Commission = firstTrade.Commission,
                    OrderDateTime = firstTrade.OrderDateTime,
                    OrderAmount = groupedTradesByOrderId.Any(x => x.OrderAmount != null)
                        ? groupedTradesByOrderId.Where(x => x.OrderAmount != null).Sum(x => x.OrderAmount)
                        : null,
                    EntryDateTime = firstTrade.EntryDateTime,
                    EntryQuantity = groupedTradesByOrderId.Any(x => x.EntryQuantity != null)
                        ? groupedTradesByOrderId.Where(x => x.EntryQuantity != null).Sum(x => x.EntryQuantity)
                        : null,
                    Timeframe = firstTrade.Timeframe,
                    Market = firstTrade.Market,
                    Id = firstTrade.Id,
                    TradeDirection = firstTrade.TradeDirection
                };

                trade.EntryPrice = groupedTradesByOrderId.Any(x => x.EntryPrice != null)
                    ? groupedTradesByOrderId.Where(x => x.EntryPrice != null).Sum(x => x.EntryPrice * x.EntryQuantity) /
                      trade.EntryQuantity
                    : null;

                trade.OrderPrice = groupedTradesByOrderId.Any(x => x.OrderPrice != null)
                    ? groupedTradesByOrderId.Where(x => x.OrderPrice != null).Sum(x => x.OrderPrice * x.OrderAmount) /
                      trade.OrderAmount
                    : null;

                trade.CommissionValueCurrency = groupedTradesByOrderId.First().CommissionValueCurrency;
                trade.EntryValueCurrency = groupedTradesByOrderId.First().EntryValueCurrency;
                trade.EntryValue = groupedTradesByOrderId.Where(x => x.EntryValue != null).Sum(x => x.EntryValue.Value);
                trade.CommissionValue = groupedTradesByOrderId.Where(x => x.CommissionValue != null).Sum(x => x.CommissionValue.Value);

                simplifiedTrades.Add(trade);
            }

            return simplifiedTrades;
        }
    }

    public enum TransactionType
    {
        Buy,
        Sell,
        Commission,
        Withdraw,
        Deposit,
        BuySell
    }

    public class Transaction
    {
        public DateTime DateTimeStart { get; set; }
        public DateTime? DateTimeEnd { get; set; }
        public TransactionType TransactionType { get; set; }
        public string Asset1 { get; set; }
        public string Asset2 { get; set; }
        public string Broker { get; set; }
        public decimal Amount { get; set; }
        public string Id { get; set; }
        public decimal Price { get; set; }
        public decimal ProceedsGBP { get; set; }
        public decimal PriceGBP { get; set; }

        public override string ToString()
        {
            return $"{DateTimeStart} {DateTimeEnd} {TransactionType} {Broker} {Asset1} {Asset2} {Price:0.000} £{PriceGBP:0.00} £{ProceedsGBP:0.00}";
        }
    }
}
