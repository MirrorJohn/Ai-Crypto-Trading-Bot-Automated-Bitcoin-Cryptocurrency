using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using Hallupa.Library;
using TraderTools.Basics;

namespace TraderTools.Core.UI.ViewModels
{
    public class TradesResultViewModel : DependencyObject, INotifyPropertyChanged
    {
        private string _name;
        private int _tradesCount;
        private decimal _percentSuccessfulTrades;
        private decimal _avRWinningTrades;
        private decimal _avRLosingTrades;
        private decimal _rSum;
        private decimal _avAdverseRFor10Candles;
        private decimal _avPositiveRFor20Candles;
        private decimal _expectancyR;
        private decimal _totalR;
        private string _averageTradeDuration;
        private int _successTradesCount;
        private int _failedTradesCount;
        private decimal _maxDrawdownPercent;

        public ObservableCollectionEx<Trade> Trades { get; } = new ObservableCollectionEx<Trade>();

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public int TradesCount
        {
            get => _tradesCount;
            set
            {
                _tradesCount = value;
                OnPropertyChanged();
            }
        }

        public int SuccessTradesCount
        {
            get => _successTradesCount;
            set
            {
                _successTradesCount = value;
                OnPropertyChanged();
            }
        }

        public int FailedTradesCount
        {
            get => _failedTradesCount;
            set
            {
                _failedTradesCount = value;
                OnPropertyChanged();
            }
        }

        public decimal PercentSuccessfulTrades
        {
            get => _percentSuccessfulTrades;
            set
            {
                _percentSuccessfulTrades = value;
                OnPropertyChanged();
            }
        }

        public decimal AvRWinningTrades
        {
            get => _avRWinningTrades;
            set
            {
                _avRWinningTrades = value;
                OnPropertyChanged();
            }
        }

        public decimal AvRLosingTrades
        {
            get => _avRLosingTrades;
            set
            {
                _avRLosingTrades = value;
                OnPropertyChanged();
            }
        }

        public decimal AvAdverseRFor10Candles
        {
            get => _avAdverseRFor10Candles;
            set
            {
                _avAdverseRFor10Candles = value;
                OnPropertyChanged();
            }
        }

        public decimal TotalR
        {
            get => _totalR;
            set
            {
                _totalR = value;
                OnPropertyChanged();
            }

        }

        public decimal RExpectancy
        {
            get => _expectancyR;
            set
            {
                _expectancyR = value;
                OnPropertyChanged();
            }
        }

        public decimal MaxDrawdownPercent
        {
            get => _maxDrawdownPercent;
            set
            {
                _maxDrawdownPercent = value;
                OnPropertyChanged();
            }
        }

        public string AverageTradeDuration
        {
            get => _averageTradeDuration;
            set
            {
                _averageTradeDuration = value; 
                OnPropertyChanged();
            }
        }

        public decimal AvPositiveRFor20Candles
        {
            get => _avPositiveRFor20Candles;
            set
            {
                _avPositiveRFor20Candles = value;
                OnPropertyChanged();
            }
        }

        public decimal RSum
        {
            get => _rSum;
            set
            {
                _rSum = value;
                OnPropertyChanged();
            }
        }

        public decimal Profit { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void UpdateStats()
        {
            var positiveRMultipleTrades = Trades.Where(t => t.RMultiple != null && t.RMultiple > 0).ToList();
            var negativeRMultipleTrades = Trades.Where(t => t.RMultiple != null && t.RMultiple <= 0).ToList();
            var profitableTrades = Trades.Where(t => t.Profit > 0 || t.RMultiple > 0).ToList();
            TradesCount = Trades.Count;
            SuccessTradesCount = positiveRMultipleTrades.Count;
            FailedTradesCount = negativeRMultipleTrades.Count;
            Profit = Trades.Sum(t => t.Profit ?? 0);
            PercentSuccessfulTrades = profitableTrades.Count != 0 ? (profitableTrades.Count * 100M) / (decimal)Trades.Count(x => x.Profit != null || x.RMultiple != null) : 0;
            RSum = Trades.Where(t => t.RMultiple != null).Sum(t => t.RMultiple.Value);

            AvRWinningTrades = positiveRMultipleTrades.Count != 0 ? positiveRMultipleTrades.Where(t => t.RMultiple != null).Sum(t => t.RMultiple.Value) / (decimal)positiveRMultipleTrades.Count : 0;
            AvRLosingTrades = negativeRMultipleTrades.Count != 0 ? negativeRMultipleTrades.Where(t => t.RMultiple != null).Sum(t => t.RMultiple.Value) / (decimal)negativeRMultipleTrades.Count : 0;
            TotalR = Trades.Sum(x => x.RMultiple != null ? x.RMultiple.Value : 0M);
            RExpectancy = TradingCalculator.CalculateExpectancy(Trades.ToList());
            MaxDrawdownPercent = TradingCalculator.CalculateMaxDrawdownPercent(10000M, Trades.ToList());

            var completedTrades = Trades.Where(t => t.CloseDateTime != null && t.EntryDateTime != null).ToList();
            var avDurationMins = completedTrades.Count > 0 ? TimeSpan.FromMinutes(completedTrades.Average(t => (t.CloseDateTime.Value - t.EntryDateTime.Value).TotalMinutes)) : new TimeSpan(0);

            if (completedTrades.Count == 0)
            {
                AverageTradeDuration = string.Empty;
            }
            else if (avDurationMins.TotalMinutes > 60 * 24)
            {
                AverageTradeDuration = $"{avDurationMins.TotalDays:0.00} days";
            }
            else if (avDurationMins.TotalMinutes > 60)
            {
                AverageTradeDuration = $"{avDurationMins.TotalHours:0.00} hours";
            }
            else
            {
                AverageTradeDuration = $"{avDurationMins.TotalMinutes:0.00} mins";
            }
        }
    }
}