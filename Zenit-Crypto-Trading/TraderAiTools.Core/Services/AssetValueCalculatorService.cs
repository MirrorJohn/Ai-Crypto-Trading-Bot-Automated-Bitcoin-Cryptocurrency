using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Hallupa.Library;
using log4net;
using Newtonsoft.Json;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;

namespace TraderTools.Core.Services
{
    [Export(typeof(AssetValueCalculatorService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AssetValueCalculatorService
    {
        [Import] private IDataDirectoryService _dataDirectoryService;

        private class CachedPrice
        {
            public string Broker { get; set; }
            public string Market { get; set; }
            public DateTime DateTime { get; set; }
            public double Price { get; set; }
        }

        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Dictionary<(string Broker, string Asset, DateTime Date), CachedPrice> _cachedPrices = new Dictionary<(string Broker, string Asset, DateTime Date), CachedPrice>();
        private const Timeframe TimeframeForGettingPrices = Timeframe.H1;
        private DateTime? _lastSave;

        public string FilePath => Path.Combine(_dataDirectoryService.MainDirectory, "CachedPrices.json");

        public AssetValueCalculatorService()
        {
            DependencyContainer.ComposeParts(this);
            Load();
        }

        private void Load()
        {
            if (File.Exists(FilePath))
            {
                _cachedPrices = JsonConvert.DeserializeObject<List<CachedPrice>>(File.ReadAllText(FilePath))
                    .ToDictionary(x => (x.Broker, x.Market, x.DateTime), x => x);
            }
        }

        private void Save()
        {
            lock (_cachedPrices)
            {
                if (!Directory.Exists(Path.GetDirectoryName(FilePath)))
                {
                    Directory.CreateDirectory(FilePath);
                }

                if (_lastSave == null || _lastSave.Value < DateTime.Now.AddSeconds(-5))
                {
                    File.WriteAllText(FilePath, JsonConvert.SerializeObject(_cachedPrices.Values));
                    _lastSave = DateTime.Now;
                }
            }
        }

        /*public static decimal GetAsetGbpValue(
            IBrokersCandlesService candleService, IBroker fxcmBroker,
            string asset, DateTime date, decimal amount, out bool success, bool updateCandles = false)
        {
            success = false;
            if (asset == "GBP")
            {
                success = true;
                return amount;
            }

            var valueUsd = GetAssetUsdValue(candleService, fxcmBroker, asset, date, amount,
                out success, updateCandles);
            var gbpUsd =
                candleService.GetFirstCandleThatClosesBeforeDateTime("GBP/USD", fxcmBroker, TimeframeForGettingPrices,
                    date,
                    updateCandles);

            return valueUsd / (decimal)gbpUsd.Close;
        }*/

        private double? GetSinglePrice(IBroker broker, string market, DateTime date)
        {
            lock (_cachedPrices)
            {
                if (_cachedPrices.TryGetValue((broker.Name, market, date), out var value))
                {
                    return value.Price;
                }
            }

            var timeframeForCalculating = Timeframe.H1;
            var candle = broker.GetSingleCandle(market, timeframeForCalculating, date);
            if (candle != null)
            {
                lock (_cachedPrices)
                {
                    _cachedPrices[(broker.Name, market, date)] = new CachedPrice
                    {
                        Broker = broker.Name,
                        DateTime = date,
                        Market = market,
                        Price = candle.Value.CloseBid
                    };
                }

                Save();

                return candle.Value.CloseBid;
            }

            return null;
        }

        /*public void CalculateUSDTProceeds(List<Trade> trades, Action<string> updateProgress, IBrokersCandlesService candlesService, IBroker broker)
        {
            var groupedTrades = trades.GroupBy(x => x.OrderId + " " + x.OrderDateTime.Value.ToString("yyyyMMddHHmm")).ToList();
            var count = 0;
            var producerConsumer = new ProducerConsumer<IGrouping<string, Trade>>(10, tradeGroup =>
            {
                try
                {
                    var t = tradeGroup.First();
                    var asset = t.BaseAsset;

                    if (t.EntryDateTime != null && t.EntryQuantity != null)
                    {
                        var usdtSingleAssetValue = GetCryptoAssetApproxUsdValue(candlesService, broker,
                            asset,
                            t.EntryDateTime.Value, 1,
                            out var success, true);

                        if (!success)
                        {
                            return ProducerConsumerActionResult.Stop;
                        }

                        var usdtSingleCommissionValue = !string.IsNullOrEmpty(t.CommissionAsset)
                            ? GetCryptoAssetApproxUsdValue(candlesService, broker,
                                t.CommissionAsset,
                                t.EntryDateTime.Value, 1,
                                out success, true)
                            : 0M;

                        if (!success)
                        {
                            return ProducerConsumerActionResult.Stop;
                        }

                        foreach (var trade in tradeGroup)
                        {
                            trade.EntryValue = usdtSingleAssetValue * trade.EntryQuantity.Value;
                            trade.EntryValueCurrency = "USD";

                            trade.CommissionValue = usdtSingleCommissionValue * (trade.Commission ?? 0M);
                            trade.CommissionValueCurrency = "USD";
                        }

                        foreach (var trade in tradeGroup)
                        {
                            if (trade.Commission == null || trade.EntryValue == null)
                            {

                            }
                        }

                        return ProducerConsumerActionResult.Success;
                    }

                    return ProducerConsumerActionResult.Success;
                }
                finally
                {
                    var updatedCount = Interlocked.Increment(ref count);

                    if (updatedCount % 10 == 0)
                    {
                        updateProgress($"Updated USDT value of {updatedCount}/{groupedTrades.Count}");
                    }
                }
            });

            foreach (var groupedTrade in groupedTrades)
            {
                producerConsumer.Add(groupedTrade);
            }

            producerConsumer.SetProducerCompleted();
            producerConsumer.Start();
            producerConsumer.WaitUntilConsumersFinished();
        }*/

        public decimal GetCryptoAssetApproxUsdValue(IBrokersCandlesService candleService, IBroker binanceBroker,
            string asset, DateTime date, decimal amount, out bool success, bool updateCandles = false)
        {
            success = false;
            if (amount == 0M) return 0M;

            if (asset == "USDT" || asset == "USDC")
            {
                success = true;
                return amount;
            }

            if (asset == "BTC")
            {
                var dailyCandle = GetSinglePrice(binanceBroker, $"ETH{asset}", date);
                if (dailyCandle == null)
                {
                    //Log.Error($"Unable to calculate accurate assets value - ETH{asset} candles not loaded");
                    return 0M;
                }

                var ethAmount = amount / (decimal)dailyCandle;
                var ethUsd = GetSinglePrice(binanceBroker, "ETHUSDT", date);
                var usdAmount = ethAmount * (decimal)ethUsd;
                success = true;
                Save();
                return usdAmount;
            }

            
            var btcUsd = GetSinglePrice(binanceBroker, "BTCUSDT", date);
            if (asset == "GAS" && btcUsd != null)
            {
                var candlePrice = GetSinglePrice(binanceBroker, "GASBTC", date);
                if (candlePrice != null)
                {
                    var btcAmount = (decimal)candlePrice * amount;
                    var usdAmount = btcAmount * (decimal)btcUsd;
                    success = true;
                    Save();
                    return usdAmount;
                }
            }

            if (asset != "ETH")
            {
                var candlePrice = GetSinglePrice(binanceBroker, $"{asset}ETH", date);
                if (candlePrice != null)
                {
                    var ethAmount = (decimal)candlePrice * amount;
                    var ethUsd = GetSinglePrice(binanceBroker, "ETHUSDT", date);
                    var usdAmount = ethAmount * (decimal)ethUsd;
                    success = true;
                    Save();
                    return usdAmount;
                }
            }

            if (asset != "BTC")
            {
                var candlePrice = GetSinglePrice(binanceBroker, $"{asset}BTC", date);
                if (candlePrice != null)
                {
                    var btcAmount = (decimal)candlePrice * amount;
                    var usdAmount = btcAmount * (decimal)btcUsd;
                    success = true;
                    return usdAmount;
                }
            }

            if (asset == "ETH")
            {
                var ethUsd = GetSinglePrice(binanceBroker, "ETHUSDT", date);
                if (ethUsd != null)
                {
                    var ethAmount = amount;
                    var usdAmount = ethAmount * (decimal) ethUsd;
                    success = true;
                    Save();
                    return usdAmount;
                }
            }

            return 0M;
        }

        public static decimal GetAssetUsdValue(
            IBrokersCandlesService candleService, IBroker fxcmBroker,
            string asset, DateTime date, decimal amount, out bool success, bool updateCandles = false)
        {
            success = false;
            if (amount == 0M) return 0M;

            if (asset == "EUR")
            {
                var eurUsd = candleService.GetLastClosedCandle("EUR/USD", fxcmBroker,
                    TimeframeForGettingPrices, date, updateCandles);
                success = true;
                return amount * (decimal)eurUsd.Value.CloseBid;
            }

            if (asset == "GBP")
            {
                var gbpUsd = candleService.GetLastClosedCandle("GBP/USD", fxcmBroker,
                    TimeframeForGettingPrices, date, updateCandles);
                success = true;
                return amount * (decimal)gbpUsd.Value.CloseBid;
            }

            if (asset == "USD")
            {
                success = true;
                return amount;
            }

            return 0M;
        }
    }
}