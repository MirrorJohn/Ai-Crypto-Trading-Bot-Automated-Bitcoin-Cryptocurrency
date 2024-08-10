using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TraderTools.Basics;

namespace TraderTools.Core.Services
{
    [Export(typeof(MarketsService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class MarketsService
    {
        private string _path;
        private List<Market> _marketData;
        private List<IDisposable> _marketSubscriptions = new List<IDisposable>();
        private IDataDirectoryService _dataDirectoryService;

        [ImportingConstructor]
        public MarketsService(IDataDirectoryService dataDirectoryService)
        {
            _dataDirectoryService = dataDirectoryService;

            _path = Path.Combine(_dataDirectoryService.MainDirectory, "Markets", "MarketData.json");
            var directory = Path.GetDirectoryName(_path);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(_path))
            {
                _marketData = JsonConvert.DeserializeObject<List<Market>>(File.ReadAllText(_path));

                var changes = false;
                foreach (var marketName in GetSpreadBetMarkets())
                {
                    if (_marketData.All(m => m.Name != marketName))
                    {
                        _marketData.Add(new Market
                        {
                            Name = marketName
                        });
                        changes = true;
                    }
                }

                if (changes)
                {
                    SaveMarketData();
                }
            }
            else
            {
                _marketData = new List<Market>();
                foreach (var marketName in GetSpreadBetMarkets())
                {
                    _marketData.Add(new Market
                    {
                        Name = marketName
                    });
                }

                SaveMarketData();
            }

            foreach (var market in _marketData)
            {
                _marketSubscriptions.Add(market.UpdatedObservable.Subscribe(m => { SaveMarketData(); }));
            }
        }

        public List<Market> GetMarkets()
        {
            return _marketData;
        }

        private void SaveMarketData()
        {
            File.WriteAllText(_path, JsonConvert.SerializeObject(_marketData));
        }

        private static List<string> GetSpreadBetMarkets()
        {
            return new List<string>
            {
                "EUR/USD",
                "GBP/USD",
                "XAU/USD",
                "USD/CHF",
                "AUD/USD",
                "USD/JPY",
                "NZD/USD",
                "USD/CAD",
                "USD/MXN",
                "EUR/JPY",
                "GBP/JPY",
                "AUD/JPY",
                "CHF/JPY",
                "EUR/CHF",
                "GBP/CHF",
                "EUR/GBP",
                "EUR/CAD",
                "AUD/CAD",
                "GBP/AUD",
                "EUR/AUD",
                "CAD/JPY",
                "NZD/JPY",
                "GBP/NZD",
                "GBP/CAD",
                "AUD/NZD",
                "AUD/CHF",
                "EUR/NZD",
                "NZD/CHF",
                "CAD/CHF",
                "NZD/CAD",
                "GER30",
                "US30",
                "AUS200",
                "FRA40",
                "UK100",
                "NAS100",
                "SPX500",
                "Bund",
                "NGAS",
                "Copper",
                "CHN50",
                "EUR/NOK",
                "EUR/SEK",
                "EUR/TRY",
                "USD/CNH",
                "USD/HKD",
                "USD/SEK",
                "USOil",
                "USD/TRY"
            };
        }
    }
}