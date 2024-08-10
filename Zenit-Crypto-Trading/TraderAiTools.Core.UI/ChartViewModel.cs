using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Hallupa.Library;
using SciChart.Charting.ViewportManagers;
using SciChart.Charting.Visuals.Annotations;
using SciChart.Data.Model;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Core.UI.ChartModifiers;
using TraderTools.Core.UI.Services;
using TraderTools.Indicators;

namespace TraderTools.Core.UI
{
    public class ChartViewModel : DependencyObject, INotifyPropertyChanged
    {
        [Import] public IBrokersCandlesService BrokerCandles { get; private set; }
        [Import] private IBrokersService _brokers;
        public event PropertyChangedEventHandler PropertyChanged;
        private Dispatcher _dispatcher;
        private Timeframe _chartTimeframe = Timeframe.H2;
        private DateRange _xAxisVisibleRange;

        public ChartViewModel()
        {
            DependencyContainer.ComposeParts(this);
            _dispatcher = Dispatcher.CurrentDispatcher;

            ChartTimeframeOptions.Add(Timeframe.D1);
            ChartTimeframeOptions.Add(Timeframe.H4);
            ChartTimeframeOptions.Add(Timeframe.H2);
            ChartTimeframeOptions.Add(Timeframe.H1);
            ChartTimeframeOptions.Add(Timeframe.M30);
            ChartTimeframeOptions.Add(Timeframe.M15);
            ChartTimeframeOptions.Add(Timeframe.M5);
            ChartTimeframeOptions.Add(Timeframe.M1);

            ChartTimeframe = Timeframe.H2;

            XVisibleRange = new DateRange();
        }

        /// <summary>
        /// Used in xaml.
        /// </summary>
        [Import] public ChartingService ChartingService { get; private set; }

        public ObservableCollection<Timeframe> ChartTimeframeOptions { get; } = new ObservableCollection<Timeframe>();

        public ObservableCollection<ChartPaneViewModel> ChartPaneViewModels { get; } = new ObservableCollection<ChartPaneViewModel>();

        public IViewportManager ViewportManager { get; private set; } = new DefaultViewportManager();

        public Timeframe ChartTimeframe
        {
            get => _chartTimeframe;
            set
            {
                if (_chartTimeframe == value) return;

                _chartTimeframe = value;

                if (SelectedChartTimeframeIndex != ChartTimeframeOptions.IndexOf(value))
                {
                    SelectedChartTimeframeIndex = ChartTimeframeOptions.IndexOf(value);
                }

                ChartTimeframeChangedAction?.Invoke();
            }
        }

        public Action ChartTimeframeChangedAction { get; set; }

        public static readonly DependencyProperty SelectedChartTimeframeIndexProperty = DependencyProperty.Register(
            "SelectedChartTimeframeIndex", typeof(int), typeof(ChartViewModel), new PropertyMetadata(0, SelectedChartTimeframeIndexPropertyChangedCallback));

        private static void SelectedChartTimeframeIndexPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var cvm = ((ChartViewModel) d);
            cvm.PropertyChanged(d, new PropertyChangedEventArgs("SelectedChartTimeframeIndex"));
            cvm.ChartTimeframe = cvm.ChartTimeframeOptions[cvm.SelectedChartTimeframeIndex];
        }

        public int SelectedChartTimeframeIndex
        {
            get => (int)GetValue(SelectedChartTimeframeIndexProperty);
            set
            {
                _dispatcher.Invoke(() =>
                {
                    SetValue(SelectedChartTimeframeIndexProperty, value);
                    if (ChartTimeframe != ChartTimeframeOptions[value])
                    {
                        ChartTimeframe = ChartTimeframeOptions[value];
                    }
                });
            }
        }

        ///<summary>
        /// Shared XAxis VisibleRange for all charts
        ///</summary>
        public DateRange XVisibleRange
        {
            get { return _xAxisVisibleRange; }
            set
            {
                if (Equals(_xAxisVisibleRange, value)) return;

                _xAxisVisibleRange = value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public List<LineAnnotation> GetSelectedLines()
        {
            if (ChartPaneViewModels.Count > 0)
            {
                return ChartPaneViewModels[0].TradeAnnotations.OfType<LineAnnotation>()
                    .Where(x => x.Tag is string s && s.StartsWith("Added") && x.IsSelected).ToList();
            }

            return null;
        }

        public List<LineAnnotation> GetLines(string tag)
        {
            if (ChartPaneViewModels.Count > 0)
            {
                return ChartPaneViewModels[0].TradeAnnotations.OfType<LineAnnotation>()
                    .Where(x => x.Tag is string s && tag.Equals(s)).ToList();
            }

            return null;
        }

        public void RemoveLines(List<LineAnnotation> lines)
        {
            if (lines == null) return;

            foreach (var toRemove in lines)
            {
                ChartPaneViewModels[0].TradeAnnotations.Remove(toRemove);
            }
        }

        public void ViewCandles(
            string market, Timeframe chartTimeframe,
            List<Candle> chartCandles,
            List<(IIndicator Indicator, Color Color, bool ShowInLegend)> indicators)
        {
            ChartTimeframe = chartTimeframe;
            ChartHelper.SetChartViewModelPriceData(chartCandles, this);
            //ChartHelper.SetChartViewModelIndicatorPaneData(chartCandles, this, new AverageTrueRange());
            ChartHelper.SetChartViewModelIndicatorPaneData(chartCandles, this, new StochasticRelativeStrengthIndex());

            if (ChartPaneViewModels[0].TradeAnnotations == null)
            {
                ChartPaneViewModels[0].TradeAnnotations = new AnnotationCollection();
            }
            else
            {
                ChartPaneViewModels[0].TradeAnnotations.Clear();
            }

            foreach (var i in indicators)
            {
                i.Indicator.Reset();
                ChartHelper.AddIndicator(
                    ChartPaneViewModels[0],
                    market,
                    i.Indicator,
                    i.Color,
                    ChartTimeframe,
                    chartCandles,
                    i.ShowInLegend);
            }
        }

        public void AddTradeLines(Trade t)
        {
            if (t.ChartLines != null)
            {
                var id = 1;
                foreach (var line in t.ChartLines)
                {
                    var addedLine = ChartHelper.CreateChartLineAnnotation(ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries, line.DateTimeUTC1, line.Price1, line.DateTimeUTC2, line.Price2);
                    addedLine.Tag = "Added_" + id;
                    addedLine.StrokeThickness = AddLinesModifier.StrokeThickness;
                    addedLine.Opacity = AddLinesModifier.Opacity;
                    addedLine.Stroke = AddLinesModifier.Stroke;
                    addedLine.IsEditable = true;
                    ChartPaneViewModels[0].TradeAnnotations.Add(addedLine);
                    id++;
                }
            }
        }

        public void ShowTrade(
            Trade trade, Timeframe chartTimeframe, bool updateCandles = false,
            Action<string> updateProgressAction = null,
            List<(IIndicator Indicator, Color Color, bool ShowInLegend)> indicators = null,
            bool useHeikenAshi = false)
        {
            var broker = _brokers.Brokers.First(b => b.Name == trade.Broker);
            
            //_showingTradeSetup = false;
            DateTime? start = null, end = null;

            if (chartTimeframe == Timeframe.M1)
            {
                start = trade.StartDateTime.Value.AddMinutes(-20);
                end = trade.CloseDateTime != null
                    ? trade.CloseDateTime.Value.AddMinutes(20)
                    : trade.StartDateTime.Value.AddMinutes(240);
            }

            var chartCandles = BrokerCandles.GetCandles(broker, trade.Market, chartTimeframe, updateCandles, cacheData: false, minOpenTimeUtc: start, maxCloseTimeUtc: end, progressUpdate: updateProgressAction);

            if (useHeikenAshi)
            {
                chartCandles = chartCandles.CreateHeikinAshiCandles();
            }

            ChartTimeframe = chartTimeframe;

            ViewCandles(trade.Market, chartTimeframe, chartCandles, indicators);

            ChartHelper.SetChartViewModelVisibleRange(trade, this, chartCandles, chartTimeframe);

            ChartPaneViewModels[0].TradeAnnotations?.Clear();

            ChartHelper.CreateTradeAnnotations(ChartPaneViewModels[0].TradeAnnotations,
                this, TradeAnnotationsToShow.All, chartCandles, trade);

            AddTradeLines(trade);
        }
    }
}