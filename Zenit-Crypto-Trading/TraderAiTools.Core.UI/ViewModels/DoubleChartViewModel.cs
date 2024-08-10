using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Hallupa.Library;
using TraderTools.Basics;
using TraderTools.Core.UI.Services;
using TraderTools.Indicators;

namespace TraderTools.Core.UI.ViewModels
{
    public class DoubleChartViewModel : DependencyObject
    {

        public DoubleChartViewModel()
        {
            DependencyContainer.ComposeParts(this);

            RemoveSelectedLineCommand = new DelegateCommand(t => RemoveSelectedLine());
        }

        public GridLength SmallerChartWidth { get; set; } = new GridLength(1, GridUnitType.Star);

        public ChartViewModel RightChartViewModel { get; } = new ChartViewModel();

        [Import] public ChartingService ChartingService { get; private set; }

        public ChartViewModel LeftChartViewModel { get; } = new ChartViewModel();


        public event PropertyChangedEventHandler PropertyChanged;

        public DelegateCommand RemoveSelectedLineCommand { get; private set; }

        private void RemoveSelectedLine()
        {
            var toRemoveList = RightChartViewModel.GetSelectedLines();
            if (toRemoveList != null)
            {
                foreach (var toRemove in toRemoveList)
                {
                    RightChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Remove(toRemove);

                    var linked = LeftChartViewModel.GetLines((string)toRemove.Tag);
                    if (linked != null)
                    {
                        foreach (var l in linked)
                        {
                            LeftChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Remove(l);
                        }
                    }
                }
            }

            toRemoveList = LeftChartViewModel.GetSelectedLines();
            if (toRemoveList != null)
            {
                foreach (var toRemove in toRemoveList)
                {
                    LeftChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Remove(toRemove);

                    var linked = RightChartViewModel.GetLines((string)toRemove.Tag);
                    if (linked != null)
                    {
                        foreach (var l in linked)
                        {
                            // TODO ChartViewModel.ChartPaneViewModels[0].TradeAnnotations.Remove(linked);
                        }
                    }
                }
            }
        }


        protected void ViewCandles(string market, Timeframe smallChartTimeframe, List<Candle> smallChartCandles,
            Timeframe largeChartTimeframe, List<Candle> largeChartCandles)
        {
            /* TODO LargeChartTimeframe = largeChartTimeframe;
            ChartHelper.SetChartViewModelPriceData(largeChartCandles, ChartViewModel);

            if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA8_EMA25_EMA50)
            {
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], market,
                    new ExponentialMovingAverage(8), Colors.DarkBlue, largeChartTimeframe, largeChartCandles);
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], market,
                    new ExponentialMovingAverage(25), Colors.Blue, largeChartTimeframe, largeChartCandles);
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], market,
                    new ExponentialMovingAverage(50), Colors.LightBlue, largeChartTimeframe, largeChartCandles);
            }
            else if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA20_MA50_MA200)
            {
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], market,
                    new ExponentialMovingAverage(8), Colors.DarkBlue, largeChartTimeframe, largeChartCandles);
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], market,
                    new SimpleMovingAverage(50), Colors.Blue, largeChartTimeframe, largeChartCandles);
                ChartHelper.AddIndicator(ChartViewModel.ChartPaneViewModels[0], market,
                    new SimpleMovingAverage(200), Colors.LightBlue, largeChartTimeframe, largeChartCandles);
            }

            ChartHelper.SetChartViewModelPriceData(smallChartCandles, ChartViewModelSmaller1);

            if (SelectedMainIndicatorsIndex == (int)MainIndicators.EMA8_EMA25_EMA50)
            {
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], market,
                    new ExponentialMovingAverage(8), Colors.DarkBlue, smallChartTimeframe, smallChartCandles);
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], market,
                    new ExponentialMovingAverage(25), Colors.Blue, smallChartTimeframe, smallChartCandles);
                ChartHelper.AddIndicator(ChartViewModelSmaller1.ChartPaneViewModels[0], market,
                    new ExponentialMovingAverage(50), Colors.LightBlue, smallChartTimeframe, smallChartCandles);
            }*/
        }

        /*protected void ShowTrade(Trade trade, Timeframe leftChartTimeframe, Timeframe rightChartTimeframe)
        {
            _dispatcher.BeginInvoke((Action)(() =>
            {
                LeftChartViewModel.ShowTrade(trade, leftChartTimeframe, false, s => { }, );
                RightChartViewModel.ShowTrade(trade, rightChartTimeframe, false, s => { }, );
            }));
        }*/

        protected void ViewCandles(string market, Timeframe leftChartTimeframe, List<Candle> leftChartCandles,
            Timeframe rightChartTimeframe, List<Candle> rightChartCandles,
            List<(IIndicator Indicator, Color Color, bool ShowInLegend)> smallChartIndicators,
            List<(IIndicator Indicator, Color Color, bool ShowInLegend)> largeChartIndicators)
        {
            RightChartViewModel.ViewCandles(market, rightChartTimeframe, rightChartCandles, largeChartIndicators);
            LeftChartViewModel.ViewCandles(market, leftChartTimeframe, leftChartCandles, smallChartIndicators);
        }
    }
}