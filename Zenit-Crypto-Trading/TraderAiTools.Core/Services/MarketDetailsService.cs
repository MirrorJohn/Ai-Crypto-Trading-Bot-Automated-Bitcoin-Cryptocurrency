using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using Hallupa.Library;
using log4net;
using Newtonsoft.Json;
using TraderTools.Basics;

namespace TraderTools.Core.Services
{
    [Export(typeof(IMarketDetailsService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class MarketDetailsService : IMarketDetailsService
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Dictionary<string, MarketDetails> _marketDetailsList = new Dictionary<string, MarketDetails>();
        private IDataDirectoryService _dataDirectoryService;

        [ImportingConstructor]
        public MarketDetailsService(IDataDirectoryService dataDirectoryService)
        {
            _dataDirectoryService = dataDirectoryService;

            LoadMarketDetailsList();
        }

        public MarketDetails GetMarketDetails(string broker, string market)
        {
            lock (_marketDetailsList)
            {
                _marketDetailsList.TryGetValue(MarketDetails.GetKey(broker, market), out var ret);

                return ret;
            }
        }

        public List<MarketDetails> GetAllMarketDetails()
        {
            lock (_marketDetailsList)
            {
                return _marketDetailsList.Values.ToList();
            }
        }


        public void AddMarketDetails(MarketDetails marketDetails)
        {
            lock (_marketDetailsList)
            {
                _marketDetailsList[MarketDetails.GetKey(marketDetails.Broker, marketDetails.Name)] = marketDetails;
            }
        }

        public void SaveMarketDetailsList()
        {
            using (new LogRunTime("Load market details list"))
            {
                lock (_marketDetailsList)
                {
                    var marketDetailsListPath = Path.Combine(_dataDirectoryService.MainDirectory, "MarketDetails.json");

                    File.WriteAllText(
                        marketDetailsListPath,
                        JsonConvert.SerializeObject(_marketDetailsList.Values.ToList()));
                }
            }
        }

        public bool HasMarketDetails(string broker, string market)
        {
            lock (_marketDetailsList)
            {
                return _marketDetailsList.ContainsKey(MarketDetails.GetKey(broker, market));
            }
        }

        private void LoadMarketDetailsList()
        {
            using (new LogRunTime("Load market details list"))
            {
                lock (_marketDetailsList)
                {
                    var marketDetailsListPath = Path.Combine(_dataDirectoryService.MainDirectory, "MarketDetails.json");

                    if (File.Exists(marketDetailsListPath))
                    {
                        var data = JsonConvert.DeserializeObject<List<MarketDetails>>(
                            File.ReadAllText(marketDetailsListPath));

                        _marketDetailsList.Clear();
                        foreach (var d in data)
                        {
                            _marketDetailsList[MarketDetails.GetKey(d.Broker, d.Name)] = d;
                        }
                    }
                }
            }
        }
    }
}