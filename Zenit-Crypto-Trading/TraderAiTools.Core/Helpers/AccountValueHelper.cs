using System;
using System.Collections.Generic;
using System.Linq;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.Broker;
using TraderTools.Core.Extensions;
using TraderTools.Core.Services;

namespace TraderTools.Core.Helpers
{
    public static class AccountValueHelper
    {
        public static List<(DateTime Time, decimal Value)> GetDailyTotalValues(IBroker broker, BrokerAccount account, IBrokersCandlesService candlesService)
        {
            var ret = new List<(DateTime Time, decimal Value)>();
            /*var now = DateTime.UtcNow;

            for (var date = new DateTime(2018, 1, 1); date <= now; date = date.AddDays(1))
            {
                var assetBalances = account.GetAssetBalances(broker, candlesService, date);

                if (date == now)
                {
                }

                // Convert to USDs
                var value = 0.0M;
                foreach (var assetBalance in assetBalances)
                {
                    value += CryptoValueHelper.GetAssetUsdValue(candlesService, broker, assetBalance.Asset, date, assetBalance.Amount);
                }

                ret.Add((date, value));
            }*/

            return ret;
        }

        public static List<(DateTime Time, decimal Value)> GetDailyTotalNetMoneyIn(IBroker broker, BrokerAccount account, IBrokersCandlesService candlesService)
        {
            var ret = new List<(DateTime Time, decimal Value)>();

            /*for (var date = new DateTime(2018, 1, 1); date <= DateTime.UtcNow; date = date.AddDays(1))
            {
                var netMoneyIn = 0.0M;
                foreach (var depositWithdrawal in account.DepositsWithdrawals.Where(x => x.Time <= date))
                {
                    netMoneyIn += CryptoValueHelper.GetAssetUsdValue(candlesService, broker, depositWithdrawal.Asset, depositWithdrawal.Time, depositWithdrawal.Amount);
                }

                ret.Add((date, netMoneyIn));
            }*/

            return ret;
        }

        public static decimal GetTotalMoneyIn(List<DepositWithdrawal> depositsWithdrawals, BrokersCandlesService candleService, IBroker fxcm, DateTime endDate, bool moneyInOnly = false)
        {
            var totalCurrencyInAtEndDate = 0M;
            foreach (var depositWithdrawal in depositsWithdrawals.Where(x => x.Time < endDate) .OrderBy(x => x.Time))
            {
                if (depositWithdrawal.Amount < 0 && moneyInOnly)
                {
                    continue;
                }

                if (depositWithdrawal.Asset == "USD")
                {
                    var gbpUsd = candleService.GetLastClosedCandle("GBPUSD", fxcm, Timeframe.D1, depositWithdrawal.Time, false);
                    var amount = depositWithdrawal.Amount / (decimal)gbpUsd.Value.CloseBid;
                    totalCurrencyInAtEndDate += amount;
                }

                if (depositWithdrawal.Asset == "EUR")
                {
                    var gbpUsd = candleService.GetLastClosedCandle("EURUSD", fxcm, Timeframe.D1, depositWithdrawal.Time, false);
                    var amount = depositWithdrawal.Amount / (decimal)gbpUsd.Value.CloseBid;
                    totalCurrencyInAtEndDate += amount;
                }

                if (depositWithdrawal.Asset == "GBP")
                {
                    totalCurrencyInAtEndDate += depositWithdrawal.Amount;
                }
            }

            return totalCurrencyInAtEndDate;
        }
    }
}