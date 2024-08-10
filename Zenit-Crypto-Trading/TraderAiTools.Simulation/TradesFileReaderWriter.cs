using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Hallupa.TraderTools.Basics;
using log4net;

namespace Hallupa.TraderTools.Simulation
{
    public class TradesFileReaderWriter
    {
        private string TradingRecordDirectory;
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private string TradingRecordPath;
        private bool _isLive;

        public TradesFileReaderWriter()
        {
            TradingRecordDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                @"TraderTools\LiveTrading\");
        }

        public void Start(bool isLive)
        {
            _isLive = isLive;
            TradingRecordPath = Path.Combine(TradingRecordDirectory, isLive ? "TradingLIVE.txt" : "TradingTEST.txt");

            Log.Info($"Trading record file set to: {TradingRecordPath}");

            if (File.Exists(TradingRecordPath))
            {
                File.Copy(TradingRecordPath, TradingRecordPath + $"_{DateTime.Now:yy-MM-dd HHmmss}");
            }
        }

        public Dictionary<string, AssetBalance> ReadCurrentTradingBalances()
        {
            if (_isLive) Log.Info($"Trading strategy record path: {TradingRecordPath}");

            if (!_isLive)
            {
                Log.Info($"** Trading strategy set to not write to file **");
                return null;
            }

            if (!File.Exists(TradingRecordPath))
            {
                if (_isLive) Log.Info($"** Trading strategy file doesn't exist **");
                return null;
            }

            var lines = File.ReadAllLines(TradingRecordPath);
            var values = lines[^1].Split(',');
            var ret = new List<AssetBalance>();

            if (_isLive) Log.Info($"Initial balance:");

            for (var i = 3; i < values.Length; i += 2)
            {
                var assetBalance = new AssetBalance(values[i], Convert.ToDecimal(values[i + 1]));
                ret.Add(assetBalance);
                if (_isLive) Log.Info($"{assetBalance.Asset} {assetBalance.Balance:0.0000}");
            }

            return ret.ToDictionary(x => x.Asset, x => x);
        }

        public void WriteTradeLine(string message, decimal accountUsdtValue, Dictionary<string, AssetBalance> currentBalances)
        {
            if (!_isLive) return;

            var lines = File.Exists(TradingRecordPath)
                ? File.ReadAllLines(TradingRecordPath).ToList()
                : new List<string>();

            var tradingBalancevalueUsdt = accountUsdtValue;
            var assetBalancesString = string.Join(',', currentBalances.Values.Select(x => $"{x.Asset},{x.Balance:0.00000000}"));
            lines.Add($"{DateTime.UtcNow:yyMMdd HH:mm:ss},{message},{tradingBalancevalueUsdt},{assetBalancesString}");

            File.WriteAllLines(TradingRecordPath + ".tmp", lines);
            Thread.Sleep(10);

            File.Move(TradingRecordPath + ".tmp", TradingRecordPath, true);
        }
    }
}