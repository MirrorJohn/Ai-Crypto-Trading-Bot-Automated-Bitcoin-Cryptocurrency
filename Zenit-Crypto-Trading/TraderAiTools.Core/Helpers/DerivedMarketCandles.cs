using System;
using System.Collections.Generic;
using Hallupa.Library.Extensions;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;

namespace TraderTools.Core.Helpers
{
    public class DerivedMarketCandles
    {
        private readonly IBroker _broker;
        private readonly IBrokersCandlesService _candlesService;

        public DerivedMarketCandles(IBroker broker, IBrokersCandlesService candlesService)
        {
            _broker = broker;
            _candlesService = candlesService;
        }

        private List<Candle> GetBTCCandles(string symbol,
            bool updateCandles, DateTime? minOpenTimeUtc = null, DateTime? maxCloseTimeUtc = null)
        {
            var candles = _candlesService.GetCandles(_broker, $"{symbol}BTC", Timeframe.M5, updateCandles, minOpenTimeUtc, maxCloseTimeUtc);
            if (candles != null && candles.Count > 0) return candles;

            var usdtCandles = _candlesService.GetCandles(_broker, $"{symbol}USDT", Timeframe.M5, updateCandles, minOpenTimeUtc, maxCloseTimeUtc);
            if (usdtCandles != null && usdtCandles.Count > 0)
            {
                var btcUsdtCandles = _candlesService.GetCandles(_broker, "BTCUSDT", Timeframe.M5, updateCandles, minOpenTimeUtc, maxCloseTimeUtc);
                candles = new List<Candle>();
                int btcCandleIndex = 0;

                foreach (var c in usdtCandles)
                {
                    btcCandleIndex = btcUsdtCandles.BinarySearchGetItem(i => btcUsdtCandles[i].CloseTimeTicks,
                        btcCandleIndex, c.CloseTimeTicks, BinarySearchMethod.PrevLowerValueOrValue);

                    var btcPrice = btcUsdtCandles[btcCandleIndex].OpenBid;
                    candles.Add(new Candle()
                    {
                        OpenBid = c.OpenBid / btcPrice,
                        CloseBid = c.CloseBid / btcPrice,
                        HighBid = c.HighBid / btcPrice,
                        LowBid = c.LowBid / btcPrice,
                        OpenTimeTicks = c.OpenTimeTicks,
                        CloseTimeTicks = c.CloseTimeTicks
                    });
                }
            }
            else
            {
                var bnbCandles = _candlesService.GetCandles(_broker, $"{symbol}BNB", Timeframe.M5, updateCandles, minOpenTimeUtc, maxCloseTimeUtc);
                if (bnbCandles != null && bnbCandles.Count > 0)
                {
                    var bnbUsdtCandles = _candlesService.GetCandles(_broker, "BNBUSDT", Timeframe.M5, updateCandles, minOpenTimeUtc, maxCloseTimeUtc);
                    candles = new List<Candle>();
                    int bnbCandleIndex = 0;

                    foreach (var c in bnbCandles)
                    {
                        bnbCandleIndex = bnbUsdtCandles.BinarySearchGetItem(i => bnbUsdtCandles[i].CloseTimeTicks,
                            bnbCandleIndex, c.CloseTimeTicks, BinarySearchMethod.PrevLowerValueOrValue);

                        if (bnbCandleIndex != -1)
                        {
                            var btcPrice = bnbUsdtCandles[bnbCandleIndex].OpenBid;
                            candles.Add(new Candle()
                            {
                                OpenBid = c.OpenBid / btcPrice,
                                CloseBid = c.CloseBid / btcPrice,
                                HighBid = c.HighBid / btcPrice,
                                LowBid = c.LowBid / btcPrice,
                                OpenTimeTicks = c.OpenTimeTicks,
                                CloseTimeTicks = c.CloseTimeTicks
                            });
                        }
                    }
                }
            }

            return candles;
        }

        public List<Candle> CreateCandlesSeries(string pair, Timeframe timeframe,
            bool updateCandles, DateTime? minOpenTimeUtc = null, DateTime? maxCloseTimeUtc = null)
        {
            var candles = _candlesService.GetCandles(_broker, pair, timeframe, updateCandles, minOpenTimeUtc, maxCloseTimeUtc);
            if (candles != null && candles.Count > 0) return candles;

            // Get first/second symbol from pair
            var firstSymbol = string.Empty;
            var secondSymbol = string.Empty;
            var allSymbols = _broker.GetSymbols();
            for (var l = 5;
                l >= 3;
                l--) // Go backwards because e.g. LUNABTC - for some reason there already is a LUNBTC in the symbols
            {
                if (allSymbols.Contains($"{pair.Substring(0, l)}BTC")
                    || allSymbols.Contains($"{pair.Substring(0, l)}BNB")
                    || allSymbols.Contains($"{pair.Substring(0, l)}USDT"))
                {
                    firstSymbol = pair.Substring(0, l);
                    secondSymbol = pair.Substring(firstSymbol.Length, pair.Length - firstSymbol.Length);
                    break;
                }
            }

            if (string.IsNullOrEmpty(firstSymbol))
            {
                // Try reverse
                firstSymbol = string.Empty;
                secondSymbol = string.Empty;
                for (var l = 5;
                    l >= 3;
                    l--) // Go backwards because e.g. LUNABTC - for some reason there already is a LUNBTC in the symbols
                {
                    if (allSymbols.Contains($"{pair.Substring(pair.Length - l, l)}BTC")
                        || allSymbols.Contains($"{pair.Substring(pair.Length - l, l)}BNB")
                        || allSymbols.Contains($"{pair.Substring(pair.Length - l, l)}USDT"))
                    {
                        firstSymbol = pair.Substring(0, pair.Length - l);
                        secondSymbol = pair.Substring(firstSymbol.Length, pair.Length - firstSymbol.Length);
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(firstSymbol)) throw new ApplicationException($"Unable to find market {pair}");

            return CreateCandlesSeries(firstSymbol, secondSymbol, timeframe, updateCandles, minOpenTimeUtc, maxCloseTimeUtc);
        }

        public List<Candle> CreateCandlesSeries(
            string firstSymbol, string secondSymbol, Timeframe timeframe,
            bool updateCandles, DateTime? minOpenTimeUtc = null, DateTime? maxCloseTimeUtc = null)
        {
            var candles = new List<Candle>();

            var swap = false;
            if (firstSymbol == "USDT")
            {
                var t = firstSymbol;
                firstSymbol = secondSymbol;
                secondSymbol = t;
                swap = true;
            }

            // Get candles for each symbol with matching 2nd asset pair
            var c1c2 = GetSymbolsMatchingAssets(firstSymbol, secondSymbol, updateCandles, minOpenTimeUtc, maxCloseTimeUtc);
            var c1 = c1c2.C1;
            var c2 = c1c2.C2;
            if (c2 == null || c2.Count == 0 | c1 == null || c1.Count == 0) return new List<Candle>();

            // Construct candles
            var startCloseTime =
                c1[0].CloseTimeTicks < c2[0].CloseTimeTicks ? c2[0].CloseTime() : c1[0].CloseTime();

            var c1Prev = 0;
            for (var d = new DateTime(startCloseTime.AddDays(1).Year, startCloseTime.AddDays(1).Month,
                    startCloseTime.AddDays(1).Day).Ticks;
                d < DateTime.Now.Ticks;
                d += TimeSpan.FromSeconds((int)timeframe).Ticks)
            {
                if (new DateTime(d) > new DateTime(2020, 1, 1))
                {

                }


                var e = d + TimeSpan.FromSeconds((int)timeframe).Ticks;
                var c1IndexStart = c1.BinarySearchGetItem(i => c1[i].CloseTimeTicks, c1Prev, d,
                    BinarySearchMethod.NextHigherValue);
                var c1IndexEnd = c1.BinarySearchGetItem(i => c1[i].CloseTimeTicks, c1Prev, e,
                    BinarySearchMethod.PrevLowerValueOrValue);

                c1Prev = c1IndexStart != -1 ? c1IndexStart : 0;

                if (c1IndexStart == -1 && c1IndexEnd == -1) break;
                if (c1IndexStart != -1 && c1IndexEnd == -1) continue;
                if (c1IndexStart == -1 && c1IndexEnd != -1) continue;

                float h = -1, l = -1, o = -1, c = -1;

                for (var i = c1IndexStart; i <= c1IndexEnd; i++)
                {
                    var s1Candle = c1[i];
                    var s2Candle =
                        c2[c2.BinarySearchGetItem(z => c2[z].CloseTimeTicks, 0, c1[i].CloseTimeTicks,
                                BinarySearchMethod.PrevLowerValueOrValue)];
                    var value = s1Candle.CloseBid / s2Candle.CloseBid;

                    if (o == -1) o = value;
                    c = value;
                    if (h == -1 || value > h) h = value;
                    if (l == -1 || value < l) l = value;
                }

                if (!h.Equals(-1))
                {
                    candles.Add(new Candle
                    {
                        OpenBid = !swap ? o : 1 / o,
                        CloseBid = !swap ? c : 1 /c,
                        HighBid = !swap ? h : 1/h,
                        LowBid = !swap ? l : 1/l,
                        OpenTimeTicks = d,
                        CloseTimeTicks = d + TimeSpan.FromSeconds((int)timeframe).Ticks,
                        IsComplete = 1
                    });
                }
            }

            return candles;
        }

        private (List<Candle> C1, List<Candle> C2) GetSymbolsMatchingAssets(string firstSymbol, string secondSymbol,
            bool updateCandles, DateTime? minOpenTimeUtc = null, DateTime? maxCloseTimeUtc = null)
        {
            var c1 = _candlesService.GetCandles(_broker, $"{firstSymbol}BTC", Timeframe.M5, updateCandles, minOpenTimeUtc, maxCloseTimeUtc);
            var c2 = _candlesService.GetCandles(_broker, $"{secondSymbol}BTC", Timeframe.M5, updateCandles, minOpenTimeUtc, maxCloseTimeUtc);

            if (c1.Count > 0 && c2.Count == 0 && secondSymbol == "USDT")
            {
                c2 = _candlesService.GetCandles(_broker, $"BTC{secondSymbol}", Timeframe.M5, updateCandles, minOpenTimeUtc, maxCloseTimeUtc);

                var c2New = new List<Candle>();
                foreach (var c in c2)
                {
                    c2New.Add(new Candle
                    {
                        CloseAsk = 1 / c.CloseAsk,
                        CloseBid = 1 / c.CloseBid,
                        HighAsk = 1 / c.HighAsk,
                        HighBid = 1 / c.HighBid,
                        LowAsk = 1 / c.LowAsk,
                        LowBid = 1 / c.LowBid,
                        OpenAsk = 1 / c.OpenAsk,
                        OpenBid = 1 / c.OpenBid,
                        CloseTimeTicks = c.CloseTimeTicks,
                        OpenTimeTicks = c.OpenTimeTicks,
                        IsComplete = c.IsComplete
                    });
                }

                return (c1, c2New);
            }


            if (c1 == null || c2 == null || c1.Count == 0 || c2.Count == 0)
            {
                c1 = _candlesService.GetCandles(_broker, $"{firstSymbol}USDT", Timeframe.M5, updateCandles,
                    minOpenTimeUtc, maxCloseTimeUtc);

                if ($"{secondSymbol}USDT" == "USDTUSDT")
                {
                    c2 = new List<Candle>();
                    foreach (var c in c1)
                    {
                        c2.Add(new Candle
                        {
                            CloseAsk = 1,
                            CloseBid = 1,
                            HighAsk = 1,
                            HighBid = 1,
                            LowAsk = 1,
                            LowBid = 1,
                            OpenAsk = 1,
                            OpenBid = 1,
                            CloseTimeTicks = c.CloseTimeTicks,
                            OpenTimeTicks = c.OpenTimeTicks,
                            IsComplete = 1
                        });
                    }
                }
                else
                {
                    c2 = _candlesService.GetCandles(_broker, $"{secondSymbol}USDT", Timeframe.M5, updateCandles,
                        minOpenTimeUtc, maxCloseTimeUtc);
                }
            }

            if (c1 == null || c2 == null || c1.Count == 0 || c2.Count == 0)
            {
                c1 = _candlesService.GetCandles(_broker, $"{firstSymbol}BNB", Timeframe.M5, updateCandles,
                    minOpenTimeUtc, maxCloseTimeUtc);

                if ($"{secondSymbol}BNB" == "BNBBNB")
                {
                    c2 = new List<Candle>();
                    foreach (var c in c1)
                    {
                        c2.Add(new Candle
                        {
                            CloseAsk = 1,
                            CloseBid = 1,
                            HighAsk = 1,
                            HighBid = 1,
                            LowAsk = 1,
                            LowBid = 1,
                            OpenAsk = 1,
                            OpenBid = 1,
                            CloseTimeTicks = c.CloseTimeTicks,
                            OpenTimeTicks = c.OpenTimeTicks,
                            IsComplete = 1
                        });
                    }
                }
                else
                {
                    c2 = _candlesService.GetCandles(_broker, $"{secondSymbol}BNB", Timeframe.M5, updateCandles,
                        minOpenTimeUtc, maxCloseTimeUtc);

                    if (secondSymbol == "USDT")
                    {
                        c2 = _candlesService.GetCandles(_broker, $"BNB{secondSymbol}", Timeframe.M5, updateCandles,
                            minOpenTimeUtc, maxCloseTimeUtc);

                        var c2New = new List<Candle>();
                        foreach (var c in c2)
                        {
                            c2New.Add(new Candle
                            {
                                CloseAsk = 1 / c.CloseAsk,
                                CloseBid = 1 / c.CloseBid,
                                HighAsk = 1 / c.HighAsk,
                                HighBid = 1 / c.HighBid,
                                LowAsk = 1 / c.LowAsk,
                                LowBid = 1 / c.LowBid,
                                OpenAsk = 1 / c.OpenAsk,
                                OpenBid = 1 / c.OpenBid,
                                CloseTimeTicks = c.CloseTimeTicks,
                                OpenTimeTicks = c.OpenTimeTicks,
                                IsComplete = c.IsComplete
                            });

                            return (c1, c2New);
                        }
                    }
                }
            }

            if (c1 == null || c2 == null || c1.Count == 0 || c2.Count == 0)
            {
                c1 = _candlesService.GetCandles(_broker, $"{firstSymbol}BTC", Timeframe.M5, updateCandles,
                    minOpenTimeUtc, maxCloseTimeUtc);

                if ($"{secondSymbol}BTC" == "BTCBTC")
                {
                    c2 = new List<Candle>();
                    foreach (var c in c1)
                    {
                        c2.Add(new Candle
                        {
                            CloseAsk = 1,
                            CloseBid = 1,
                            HighAsk = 1,
                            HighBid = 1,
                            LowAsk = 1,
                            LowBid = 1,
                            OpenAsk = 1,
                            OpenBid = 1,
                            CloseTimeTicks = c.CloseTimeTicks,
                            OpenTimeTicks = c.OpenTimeTicks,
                            IsComplete = 1
                        });
                    }
                }
                else
                {
                    c2 = _candlesService.GetCandles(_broker, $"{secondSymbol}BTC", Timeframe.M5, updateCandles,
                        minOpenTimeUtc, maxCloseTimeUtc);
                }

                if (c1 == null || c1.Count == 0) c1 = GetBTCCandles(firstSymbol, updateCandles, minOpenTimeUtc, maxCloseTimeUtc);
                if (c2 == null || c2.Count == 0) c2 = GetBTCCandles(secondSymbol, updateCandles, minOpenTimeUtc, maxCloseTimeUtc);
            }

            return (c1, c2);
        }

        private (List<Candle> c1, List<Candle> c2) GetPairFirstAssetCandles(string pair, string matchingAsset,
            bool updateCandles, DateTime? minOpenTimeUtc = null, DateTime? maxCloseTimeUtc = null)
        {
            var candles = new List<Candle>();
            List<Candle> c1 = null;
            string s1 = string.Empty;

            for (var l = 3; l <= 5; l++)
            {
                s1 = pair.Substring(0, l);
                c1 = _candlesService.GetCandles(_broker, $"{s1}{matchingAsset}", Timeframe.M5, updateCandles,
                    minOpenTimeUtc, maxCloseTimeUtc);
                if (c1 != null && c1.Count > 0) break;
            }

            if (c1 == null || c1.Count == 0) return (null, null);

            var s2 = pair.Replace(s1, string.Empty);

            var c2 = _candlesService.GetCandles(_broker, $"{s2}{matchingAsset}", Timeframe.M5, updateCandles,
                minOpenTimeUtc, maxCloseTimeUtc);

            return (c1, c2);
        }
    }
}