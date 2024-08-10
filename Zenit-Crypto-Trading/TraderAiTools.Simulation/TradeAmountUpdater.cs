using System;
using System.Collections.Generic;
using Hallupa.TraderTools.Basics;
using TraderTools.Basics;

namespace Hallupa.TraderTools.Simulation
{
    public class TradeAmountUpdater
    {
        public void UpdateTradeAndBalance(
            Trade trade, decimal commission, BrokerKind brokerKind, Dictionary<string, AssetBalance> currentAssetBalances)
        {
            if (brokerKind == BrokerKind.Trade)
            {
                UpdateBasedOnTradeBroker(trade, commission, brokerKind, currentAssetBalances);
            }

            if (brokerKind == BrokerKind.SpreadBet)
            {
            }

            //t.RiskPercentOfBalance = t.RiskAmount != 0 ? currentBalance.Balance / t.RiskAmount : 0; // TODO
        }

        private static void UpdateBasedOnTradeBroker(Trade trade, decimal commission, BrokerKind brokerKind,
        Dictionary<string, AssetBalance> currentAssetBalances)
        {
            var quoteAsset = trade.Market.Replace(trade.BaseAsset, string.Empty); // e.g. USDT
            var baseAsset = trade.BaseAsset; // e.g. ETH

            var quoteAssetBalance =
                currentAssetBalances.ContainsKey(quoteAsset) ? currentAssetBalances[quoteAsset].Balance : 0M;
            var baseAssetBalance = currentAssetBalances.ContainsKey(baseAsset) ? currentAssetBalances[baseAsset].Balance : 0M;

            if (trade.TradeDirection == TradeDirection.Long)
            {
                var costExcludingFee = trade.EntryQuantity.Value * trade.EntryPrice.Value; // Cost in quote asset (e.g. USDT)
                var fee = commission * costExcludingFee;

                if (costExcludingFee + fee > quoteAssetBalance)
                {
                    costExcludingFee = quoteAssetBalance / (1M + commission);
                    fee = quoteAssetBalance - costExcludingFee;
                    trade.EntryQuantity = costExcludingFee / trade.EntryPrice.Value;
                }

                currentAssetBalances[quoteAsset] = new AssetBalance(quoteAsset, quoteAssetBalance - fee - costExcludingFee);
                currentAssetBalances[baseAsset] = new AssetBalance(baseAsset, baseAssetBalance + trade.EntryQuantity.Value);
                trade.Commission = fee;
                trade.CommissionAsset = quoteAsset;
            }

            if (trade.TradeDirection == TradeDirection.Short)
            {
                if (trade.EntryQuantity > baseAssetBalance)
                {
                    trade.EntryQuantity = baseAssetBalance;
                }

                var fee = trade.EntryQuantity.Value * trade.EntryPrice.Value * commission;
                var quoteAssetReturn = (trade.EntryQuantity.Value * trade.EntryPrice.Value)
                                       - fee;
                currentAssetBalances[quoteAsset] = new AssetBalance(quoteAsset, quoteAssetBalance + quoteAssetReturn);
                currentAssetBalances[baseAsset] = new AssetBalance(baseAsset, baseAssetBalance - trade.EntryQuantity.Value);
                trade.Commission = fee;
                trade.CommissionAsset = quoteAsset;
            }

            if (brokerKind == BrokerKind.Trade)
            {
                trade.RiskAmount = 0;
            }
            else
            {
                throw new ApplicationException("This needs implementing");
            }
        }
    }
}
