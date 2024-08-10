using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Hallupa.Library;
using Newtonsoft.Json;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;

namespace TraderTools.Core.Services
{
    public struct LastUpdatedMarket
    {
        public string BrokerName { get; set; }
        public string Market { get; set; }
        public Timeframe Timeframe { get; set; }

        public override int GetHashCode()
        {
            return BrokerName.GetHashCode() ^ Market.GetHashCode() ^ Timeframe.GetHashCode();
        }
    }

    [Export(typeof(IBrokersCandlesService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class BrokersCandlesService : IBrokersCandlesService
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static DateTime _earliestDateTime = new DateTime(2010, 1, 1);
        private IDataDirectoryService _dataDirectoryService;

        private Dictionary<(IBroker broker, string Market, Timeframe TimeFrame), List<Candle>> _candlesLookup
            = new Dictionary<(IBroker broker, string Market, Timeframe TimeFrame), List<Candle>>();

        private Dictionary<LastUpdatedMarket, DateTime> _lastUpdated = new Dictionary<LastUpdatedMarket, DateTime>();

        [ImportingConstructor]
        public BrokersCandlesService(IDataDirectoryService dataDirectoryService)
        {
            _dataDirectoryService = dataDirectoryService;
            _savePath = Path.Combine(_dataDirectoryService.MainDirectory, "LastBrokerMarketUpdates.json");
            if (File.Exists(_savePath))
            {
                var txt = File.ReadAllText(_savePath);
                if (!string.IsNullOrEmpty(txt))
                {
                    var data = JsonConvert.DeserializeObject<List<(LastUpdatedMarket, DateTime)>>(txt);
                    _lastUpdated = data.ToDictionary(x => x.Item1, x => x.Item2);
                }
            }
        }

        public void UpdateCandles(IBroker broker, string market, Timeframe timeframe, bool forceUpdate = true, bool saveCandles = true)
        {
            GetCandles(broker, market, timeframe, true, forceUpdate: forceUpdate, saveCandles: saveCandles, cacheData: false);
        }

        private Dictionary<(IBroker Broker, string Market, Timeframe Timeframe), object> _lockLookups = new Dictionary<(IBroker Broker, string Market, Timeframe Timeframe), object>();
        private string _savePath;

        private object GetLock(IBroker broker, string market, Timeframe timeframe)
        {
            lock (_lockLookups)
            {
                if (_lockLookups.TryGetValue((broker, market, timeframe), out var l))
                {
                    return l;
                }

                var newLock = new object();
                _lockLookups[(broker, market, timeframe)] = newLock;
                return newLock;
            }
        }

        public List<Candle> GetCandles(IBroker broker, string market, Timeframe timeframe,
            bool updateCandles, DateTime? minOpenTimeUtc = null, DateTime? maxCloseTimeUtc = null, bool cacheData = true,
            bool forceUpdate = false, Action<string> progressUpdate = null, bool saveCandles = true)
        {
            var lck = GetLock(broker, market, timeframe);
            var start = _earliestDateTime;

            lock (lck)
            {
                List<Candle> candles = null;
                lock (_candlesLookup)
                {
                    if (_candlesLookup.TryGetValue((broker, market, timeframe), out var existingCandles))
                    {
                        candles = existingCandles.ToList();
                    }
                }

                if (candles == null || candles.Count == 0)
                {
                    var loadedCandles = LoadBrokerCandles(broker, market, timeframe);

                    if (cacheData && loadedCandles != null)
                    {
                        lock (_candlesLookup)
                        {
                            // Re-check if candles lookup contains the key as it may have changed
                            if (!_candlesLookup.ContainsKey((broker, market, timeframe)))
                            {
                                _candlesLookup.Add((broker, market, timeframe), loadedCandles);
                            }
                        }
                    }

                    candles = loadedCandles.ToList();
                }

                var returnCandles = !updateCandles;

                if (!returnCandles && _lastUpdated.TryGetValue(new LastUpdatedMarket
                {
                    BrokerName = broker.Name,
                    Market = market,
                    Timeframe = timeframe
                }, out var updatedDatetime))
                {
                    if (candles != null && candles.Count > 0 && !forceUpdate && (DateTime.UtcNow - updatedDatetime).TotalSeconds < 60 * 30)
                    {
                        returnCandles = true;
                    }
                }

                // Only update candles is the latest candle wanted is later than the latest-1 candle stored (-1 because it assumes the latest candle is incomplete)
                if (!returnCandles && (maxCloseTimeUtc == null || candles.Count <= 2 || candles[candles.Count - 2].CloseTime() < maxCloseTimeUtc))
                {
                    if (candles.Any())
                    {
                        var change = timeframe == Timeframe.M1 ? -2 : -10;
                        if (timeframe == Timeframe.M5) change = -6;
                        start = new DateTime(candles[candles.Count - 1].OpenTimeTicks).AddMinutes(change);
                    }
                    
                    if (broker.UpdateCandles(candles, market, timeframe, start, progressUpdate))
                    {
                        if (saveCandles)
                        {
                            if (candles.Any(c => c.HighAsk.Equals(0.0F) && !c.HighBid.Equals(0.0F)))
                            {
                                var newCandles = new List<Candle>();
                                foreach (var c in candles)
                                {
                                    if (c.HighAsk.Equals(0.0F))
                                    {
                                        var candle = new Candle
                                        {
                                            CloseAsk = c.CloseBid,
                                            CloseBid = c.CloseBid,
                                            HighAsk = c.HighBid,
                                            HighBid = c.HighBid,
                                            LowAsk = c.LowBid,
                                            LowBid = c.LowBid,
                                            CloseTimeTicks = c.CloseTimeTicks,
                                            IsComplete = c.IsComplete,
                                            OpenAsk = c.OpenBid,
                                            OpenBid = c.OpenBid,
                                            OpenTimeTicks = c.OpenTimeTicks,
                                            Volume = c.Volume
                                        };
                                        newCandles.Add(candle);
                                    }
                                    else
                                    {
                                        newCandles.Add(c);
                                    }
                                }

                                candles.Clear();
                                candles.AddRange(newCandles);
                            }

                            SaveCandles(candles, broker, market, timeframe);
                        }

                        Log.Debug($"Updated {broker.Name} {market} {timeframe} candles");
                    }

                    lock (_lastUpdated)
                    {
                        _lastUpdated[new LastUpdatedMarket
                        {
                            BrokerName = broker.Name,
                            Market = market,
                            Timeframe = timeframe
                        }] = DateTime.UtcNow;

                        File.WriteAllText(_savePath, JsonConvert.SerializeObject(_lastUpdated.Select(x => (x.Key, x.Value)).ToList()));
                    }
                }

                if (maxCloseTimeUtc != null || minOpenTimeUtc != null)
                {
                    var ret = new List<Candle>();
                    for (var i = 0; i < candles.Count; i++)
                    {
                        var candle = candles[i];
                        if ((maxCloseTimeUtc == null || candle.CloseTimeTicks <= maxCloseTimeUtc.Value.Ticks)
                            && (minOpenTimeUtc == null || candle.OpenTimeTicks >= minOpenTimeUtc.Value.Ticks))
                        {
                            ret.Add(candle);

                            if (maxCloseTimeUtc != null && candle.CloseTimeTicks > maxCloseTimeUtc.Value.Ticks)
                            {
                                break;
                            }
                        }
                    }

                    return ret;
                }

                return candles;
            }
        }

        public string GetBrokerCandlesPath(IBroker broker, string market, Timeframe timeframe)
        {
            return Path.Combine(_dataDirectoryService.MainDirectory, "Candles", $"{broker.Name}_{market.Replace("/", "")}_{timeframe}.bin");
        }

        public static Candle[] BytesToCandles(byte[] data)
        {
            int structSize = Marshal.SizeOf(typeof(Candle));
            var ret = new Candle[data.Length / structSize]; // Array of structs we want to push the bytes into
            var handle2 = GCHandle.Alloc(ret, GCHandleType.Pinned);// get handle to that array
            Marshal.Copy(data, 0, handle2.AddrOfPinnedObject(), data.Length);// do the copy
            handle2.Free();// cleanup the handle

            return ret;
        }

        private const bool DoOrderChecking = true;
        private List<Candle> LoadBrokerCandles(IBroker broker, string market, Timeframe timeframe)
        {
            var candlesPath = GetBrokerCandlesPath(broker, market, timeframe);

            if (File.Exists(candlesPath))
            {
                byte[] data;

                using (new LogRunTime($"Loaded {broker.Name} {market} {timeframe} candles data"))
                {
                    data = File.ReadAllBytes(candlesPath);
                }
                
                var ret = new List<Candle>(BytesToCandles(data));

                if (DoOrderChecking)
                {
                    var current = ret[0];
                    foreach (var c in ret)
                    {
                        if (c.OpenTimeTicks < current.OpenTimeTicks || c.CloseTimeTicks < current.CloseTimeTicks)
                        {
                            throw new ApplicationException("Candles are not in order");
                        }

                        current = c;
                    }
                }

                return ret;
            }

            return new List<Candle>();
        }

        public static byte[] CandlesToBytes(List<Candle> candles)
        {
            var candlesArray = candles.ToArray();
            var size = Marshal.SizeOf(typeof(Candle)) * candlesArray.Length;
            var bytes = new byte[size];
            var gcHandle = GCHandle.Alloc(candlesArray, GCHandleType.Pinned);
            var ptr = gcHandle.AddrOfPinnedObject();
            Marshal.Copy(ptr, bytes, 0, size);
            gcHandle.Free();

            return bytes;
        }

        public void SaveCandles(List<Candle> candles, IBroker broker, string market, Timeframe timeframe)
        {
            var directory = Path.Combine(_dataDirectoryService.MainDirectory, "Candles");
            var candlesTmpPath = Path.Combine(directory, $"{broker.Name}_{market.Replace("/", "")}_{timeframe}_tmp.bin");
            var candlesFinalPath = Path.Combine(directory, $"{broker.Name}_{market.Replace("/", "")}_{timeframe}.bin");

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(candlesTmpPath))
            {
                File.Delete(candlesTmpPath);
            }

            File.WriteAllBytes(candlesTmpPath, CandlesToBytes(candles));

            if (File.Exists(candlesFinalPath))
            {
                File.Delete(candlesFinalPath);
            }

            File.Move(candlesTmpPath, candlesFinalPath);

            Log.Debug($"Completed saving {broker.Name} candles");
        }

        public void UnloadCandles(string market, Timeframe timeframe, IBroker broker)
        {
            lock (_candlesLookup)
            {
                _candlesLookup.Remove((broker, market, timeframe));
            }
        }

        public void Lock()
        {
            Monitor.Enter(_lockLookups);
        }

        public void Unlock()
        {
            Monitor.Exit(_lockLookups);
        }
    }
}