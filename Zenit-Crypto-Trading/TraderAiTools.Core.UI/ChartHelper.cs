using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using SciChart.Charting.Common.Extensions;
using SciChart.Charting.Model.ChartSeries;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.Annotations;
using SciChart.Charting.Visuals.RenderableSeries;
using TraderTools.Basics;
using TraderTools.Basics.Extensions;
using TraderTools.Basics.Helpers;
using TraderTools.Core.UI.ChartModifiers;
using TraderTools.Core.UI.Controls;
using TraderTools.Indicators;

namespace TraderTools.Core.UI
{
    [Flags]
    public enum TradeAnnotationsToShow
    {
        All = 1,
        StopsLines = 2,
        LimitsLines = 4,
        OrderOrEntryLines = 8,
        OrderMarker = 16,
        EntryMarker = 32,
        CloseMarker = 64,
        MakeEntryCloseMarkerSmaller = 128
    }

    public static class ChartHelper
    {
        static ChartHelper()
        {
            LocalUtcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
        }

        public static TimeSpan LocalUtcOffset { get; private set; }

        public enum PositionType
        {
            Int,
            DateTime
        }

        public static IComparable FindChartPosition(IDataSeries data, DateTime date)
        {
            PositionType? positionType = null;
            return FindChartPosition(data, date, ref positionType);
        }

        /// <summary>
        /// Because SciChart messes up lines that use DateTime but are not correctly on a axis point (the line appears to move
        /// while scrolling) use index instead of DateTime apart from when the DateTime is after the last data point - in this
        /// situation use DateTime, otherwise FindIndex will just return the latest chart point.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="date"></param>
        /// <param name="positionType"></param>
        /// <returns></returns>
        public static IComparable FindChartPosition(IDataSeries data, DateTime date, ref PositionType? positionType)
        {
            if ((positionType != null && positionType == PositionType.DateTime) || date > (DateTime)data.XMax)
            {
                positionType = PositionType.DateTime;
                return date;
            }
            else
            {
                positionType = PositionType.Int;
                return data.FindIndex(date, SearchMode.Nearest);
            }
        }

        public static void SetChartViewModelIndicatorPaneData(IList<Candle> candles, ChartViewModel cvm, IIndicator indicator)
        {
            indicator.Reset();
            var indicatorDataSeries = new XyDataSeries<DateTime, double>();

            foreach (var c in candles)
            {
                var time = new DateTime(c.CloseTimeTicks, DateTimeKind.Utc);

                var v = indicator.Process(c);
                indicatorDataSeries.Append(time, v.IsFormed ? v.Value : float.NaN);
            }

            indicatorDataSeries.SeriesName = indicator.Name;

            var indicatorPaneVm = cvm.ChartPaneViewModels.Count > 1 ? cvm.ChartPaneViewModels[1] : null;
            if (indicatorPaneVm == null)
            {
                var indicatorPane = new ChartPaneViewModel(cvm, cvm.ViewportManager)
                {
                    IsFirstChartPane = false,
                    IsLastChartPane = true,
                    YAxisTextFormatting = "0.0000"
                };

                indicatorPane.ChartSeriesViewModels.Add(new ChartSeriesViewModel(indicatorDataSeries, new FastLineRenderableSeries
                {
                    AntiAliasing = false,
                    // TODO SeriesColor = Colors.DarkBlue,
                    StrokeThickness = 2
                }));

                cvm.ChartPaneViewModels.Add(indicatorPane);
            }
            else
            {
                indicatorPaneVm.ChartSeriesViewModels.Clear();
                var fastLineRenderableSeries = new FastLineRenderableSeries
                {
                    AntiAliasing = false,
                    // TODO SeriesColor = Colors.DarkBlue,
                    StrokeThickness = 2
                };

                indicatorPaneVm.ChartSeriesViewModels.Add(new ChartSeriesViewModel(indicatorDataSeries, fastLineRenderableSeries));
            }
        }

        public static void SetChartViewModelPriceData(IList<Candle> candles, ChartViewModel cvm, string seriesName = "Price")
        {
            var priceDataSeries = new OhlcDataSeries<DateTime, double>();
            var xvalues = new List<DateTime>();
            var openValues = new List<double>();
            var highValues = new List<double>();
            var lowValues = new List<double>();
            var closeValues = new List<double>();

            var atr = new AverageTrueRange();

            foreach (var c in candles)
            {
                var time = new DateTime(c.CloseTimeTicks, DateTimeKind.Utc);

                xvalues.Add(time);
                openValues.Add((double)c.OpenBid);
                highValues.Add((double)c.HighBid);
                lowValues.Add((double)c.LowBid);
                closeValues.Add((double)c.CloseBid);
            }

            priceDataSeries.Append(xvalues, openValues, highValues, lowValues, closeValues);
            priceDataSeries.SeriesName = seriesName;

            var pricePaneVm = cvm.ChartPaneViewModels.Count > 0 ? cvm.ChartPaneViewModels[0] : null;
            var atrPaneVm = cvm.ChartPaneViewModels.Count > 1 ? cvm.ChartPaneViewModels[1] : null;
            if (pricePaneVm == null)
            {
                pricePaneVm = new ChartPaneViewModel(cvm, cvm.ViewportManager)
                {
                    IsFirstChartPane = true,
                    IsLastChartPane = false
                };

                var series = new FastCandlestickRenderableSeries { AntiAliasing = false };
                series.SetValue(FilteringLegendModifier.IncludeSeriesProperty, false);
                // TODO series.SeriesColor = Colors.DarkBlue;
                pricePaneVm.ChartSeriesViewModels.Add(new ChartSeriesViewModel(priceDataSeries, series));
                cvm.ChartPaneViewModels.Add(pricePaneVm);
            }
            else
            {
                pricePaneVm.ChartSeriesViewModels.Clear();
                var renderableSeries = new FastCandlestickRenderableSeries { AntiAliasing = false };
                renderableSeries.SetValue(FilteringLegendModifier.IncludeSeriesProperty, false);
                pricePaneVm.ChartSeriesViewModels.Add(new ChartSeriesViewModel(priceDataSeries, renderableSeries));
            }
        }

        public static IDataSeries CreateIndicatorSeries(string market, IIndicator indicator, Color color, Timeframe timeframe, IList<Candle> candles)
        {
            var series = new XyDataSeries<DateTime, double>();
            var xvalues = new List<DateTime>();
            var yvalues = new List<double>();

            foreach (var candle in candles)
            {
                var signalAndValue = indicator.Process(candle);

                DateTime time = new DateTime(candle.OpenTimeTicks, DateTimeKind.Utc);

                if (indicator.IsFormed)
                {
                    xvalues.Add(time);
                    yvalues.Add((double)signalAndValue.Value);
                }
                else
                {
                    xvalues.Add(time);
                    yvalues.Add(double.NaN);
                }
            }

            series.Append(xvalues, yvalues);
            series.SeriesName = indicator.Name;

            return series;
        }

        public static void AddIndicator(ChartPaneViewModel paneViewModel, string market, IIndicator indicator, Color color, Timeframe timeframe, IList<Candle> candles, bool showInLegend = true)
        {
            var series = CreateIndicatorSeries(market, indicator, color, timeframe, candles);
            var renderableSeries = new FastLineRenderableSeries
            {
                AntiAliasing = false,
                // TODO SeriesColor = color,
                StrokeThickness = 2
            };

            renderableSeries.SetValue(FilteringLegendModifier.IncludeSeriesProperty, showInLegend);

            paneViewModel.ChartSeriesViewModels.Add(new ChartSeriesViewModel(series, renderableSeries));
        }

        public static void CreateTradeAnnotations(AnnotationCollection annotations, ChartViewModel cvm, TradeAnnotationsToShow annotationsToShow, IList<Candle> candles, Trade trade)
        {
            // Setup annotations
            if (candles.Count == 0) return;
            if (trade.EntryDateTime != null && trade.EntryDateTime > candles[candles.Count - 1].CloseTime()) return;
            if (trade.OrderDateTime != null && trade.OrderDateTime > candles[candles.Count - 1].CloseTime()) return;

            var dataSeries = cvm.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries;
            var startTime = trade.StartDateTimeLocal;
            var entryCloseMarketSmaller = annotationsToShow.HasFlag(TradeAnnotationsToShow.MakeEntryCloseMarkerSmaller);

            if (startTime != null && (trade.OrderPrice != null || trade.EntryPrice != null))
            {
                if (trade.EntryPrice != null && (annotationsToShow.HasFlag(TradeAnnotationsToShow.All) || annotationsToShow.HasFlag(TradeAnnotationsToShow.EntryMarker)))
                {
                    var profit = trade.NetProfitLoss ?? trade.GrossProfitLoss;
                    var colour = (profit != null && profit < 0) || (trade.RMultiple != null && trade.RMultiple.Value < 0) ? Colors.DarkRed : Colors.Green;
                    AddBuySellMarker(trade.TradeDirection.Value, annotations, trade, trade.EntryDateTimeLocal.Value, trade.EntryPrice.Value,
                        entryCloseMarketSmaller, colour: colour);
                }

                if (annotationsToShow.HasFlag(TradeAnnotationsToShow.All) || annotationsToShow.HasFlag(TradeAnnotationsToShow.OrderOrEntryLines))
                {
                    if (trade.OrderPrice != null && trade.OrderDateTimeLocal != null)
                    {
                        var orderPrices = trade.OrderPrices.ToList();
                        if (orderPrices.Count > 0)
                        {
                            orderPrices.Add(new DatePrice(
                                trade.EntryDateTime ?? trade.CloseDateTime ?? new DateTime(candles[candles.Count - 1].CloseTimeTicks, DateTimeKind.Utc),
                                null));

                            AddLineAnnotations(orderPrices, cvm.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries, annotations, Colors.Gray);
                        }
                    }
                }
            }

            // Add order price line
            if (trade.OrderPrice != null && trade.CloseDateTime == null && trade.EntryPrice == null)
            {
                var brush = new SolidColorBrush(Colors.Gray) { Opacity = 0.3 };
                var annotation = new LineAnnotation
                {
                    X1 = 0,
                    X2 = cvm.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries.Count - 1,
                    Y1 = trade.OrderPrice,
                    Y2 = trade.OrderPrice,
                    Stroke = brush,
                    StrokeDashArray = new DoubleCollection(new[] { 5.0, 3.0 })
                };

                annotations.Add(annotation);
            }

            // Add close price line
            if (trade.ClosePrice != null)
            {
                var oppositeTradeDirection = trade.TradeDirection.Value == TradeDirection.Long
                    ? TradeDirection.Short
                    : TradeDirection.Long;

                if (annotationsToShow.HasFlag(TradeAnnotationsToShow.All) || annotationsToShow.HasFlag(TradeAnnotationsToShow.CloseMarker))
                {
                    var profit = trade.NetProfitLoss ?? trade.GrossProfitLoss;
                    var colour = (profit != null && profit < 0) || (trade.RMultiple != null && trade.RMultiple.Value < 0) ? Colors.DarkRed : Colors.Green;
                    AddBuySellMarker(oppositeTradeDirection, annotations, trade, trade.CloseDateTimeLocal.Value, trade.ClosePrice.Value, entryCloseMarketSmaller, colour: colour);

                    // Add line between buy/sell marker
                    /*var brush = new SolidColorBrush(colour) { Opacity = 0.6 };
                    var annotation = new LineAnnotation
                    {
                        X1 = dataSeries.FindIndex(trade.EntryDateTimeLocal, SearchMode.Nearest),
                        X2 = dataSeries.FindIndex(trade.CloseDateTimeLocal, SearchMode.Nearest),
                        Y1 = trade.EntryPrice.Value,
                        Y2 = trade.ClosePrice.Value,
                        Stroke = brush,
                        StrokeThickness = 3,
                        StrokeDashArray = new DoubleCollection(new[] { 2.0, 2.0 })
                    };

                    annotations.Add(annotation);*/
                }
            }

            // Add stop prices
            if (annotationsToShow.HasFlag(TradeAnnotationsToShow.All) || annotationsToShow.HasFlag(TradeAnnotationsToShow.StopsLines))
            {
                var stopPrices = trade.StopPrices.ToList();
                if (stopPrices.Count > 0)
                {
                    stopPrices.Add(new DatePrice(trade.CloseDateTime != null
                        ? trade.CloseDateTime.Value
                        : new DateTime(candles[candles.Count - 1].CloseTimeTicks, DateTimeKind.Utc), null));

                    AddLineAnnotations(stopPrices, cvm.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries, annotations, Colors.Red);
                }

                // Add dummy series just to add stop to legend
                if (cvm.ChartPaneViewModels[0].ChartSeriesViewModels.All(x => x.DataSeries.SeriesName != "Stop"))
                {
                    var series = new FastLineRenderableSeries() { 
                        AntiAliasing = false, // TODO SeriesColor = Colors.Red
                    };
                        series.SetValue(FilteringLegendModifier.IncludeSeriesProperty, true);
                    var d = new XyDataSeries<DateTime, double>();
                    d.SeriesName = "Stop";
                    cvm.ChartPaneViewModels[0].ChartSeriesViewModels.Add(new ChartSeriesViewModel(d, series));
                }
            }

            // Add current stop price line
            if (trade.StopPrice != null && trade.CloseDateTime == null)
            {
                var brush = new SolidColorBrush(Colors.Red) { Opacity = 0.3 };
                var annotation = new LineAnnotation
                {
                    X1 = 0,
                    X2 = cvm.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries.Count - 1,
                    Y1 = trade.StopPrice.Value,
                    Y2 = trade.StopPrice.Value,
                    Stroke = brush,
                    StrokeDashArray = new DoubleCollection(new[] { 5.0, 3.0 })
                };

                annotations.Add(annotation);
            }

            // Add limit prices
            if (annotationsToShow.HasFlag(TradeAnnotationsToShow.All) || annotationsToShow.HasFlag(TradeAnnotationsToShow.LimitsLines))
            {
                var limitPrices = trade.LimitPrices.ToList();
                if (limitPrices.Count > 0)
                {
                    limitPrices.Add(new DatePrice(trade.CloseDateTime ?? new DateTime(candles[candles.Count - 1].CloseTimeTicks, DateTimeKind.Utc), null));

                    AddLineAnnotations(limitPrices, cvm.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries, annotations, Colors.DarkGreen);
                }
            }

            // Add current limit price line
            if (trade.LimitPrice != null && trade.CloseDateTime == null)
            {
                var brush = new SolidColorBrush(Colors.DarkGreen) { Opacity = 0.3 };
                var annotation = new LineAnnotation
                {
                    X1 = 0,
                    X2 = cvm.ChartPaneViewModels[0].ChartSeriesViewModels[0].DataSeries.Count - 1,
                    Y1 = trade.LimitPrice.Value,
                    Y2 = trade.LimitPrice.Value,
                    Stroke = brush,
                    StrokeDashArray = new DoubleCollection(new[] { 5.0, 3.0 })
                };

                annotations.Add(annotation);
            }
        }

        public static void AddBuySellMarker(
            TradeDirection direction, AnnotationCollection annotations, Trade trade,
            DateTime time, decimal price, bool makeSmaller, bool isFilled = true, Color? colour = null)
        {
            annotations.Add(CreateBuySellMarker(direction, trade, time, price, makeSmaller, isFilled, colour));
        }

        public static AnnotationBase CreateBuySellMarker(
            TradeDirection direction, Trade trade,
            DateTime time, decimal price, bool makeSmaller, bool isFilled = true, Color? colour = null)
        {
            var buyMarker = new BuyMarkerAnnotation();
            var sellMarker = new SellMarkerAnnotation();
            var annotation = direction == TradeDirection.Long ? buyMarker : (CustomAnnotation)sellMarker;
            annotation.Width = makeSmaller ? 12 : 24;
            annotation.Height = makeSmaller ? 12 : 24;
            ((Path)annotation.Content).Stretch = Stretch.Fill;
            annotation.Margin = new Thickness(0, direction == TradeDirection.Long ? 5 : -5, 0, 0);
            annotation.DataContext = trade;

            var brush = new SolidColorBrush
            {
                Color = colour ?? (direction == TradeDirection.Long ? Colors.Green : Colors.DarkRed)
            };

            if (isFilled)
            {
                ((Path)annotation.Content).Fill = brush;
            }

            buyMarker.StrokeBrush = brush;
            buyMarker.Opacity = makeSmaller ? 0.6 : 0.8;
            sellMarker.StrokeBrush = brush;
            sellMarker.Opacity = makeSmaller ? 0.6 : 0.8;
            annotation.X1 = time;
            annotation.BorderThickness = new Thickness(20);
            annotation.Y1 = (double)price;
            return annotation;
        }

        public static void AddHorizontalLine(decimal price, DateTime start, DateTime end, IDataSeries dataSeries,
            AnnotationCollection annotations, Trade trade, Color colour, bool extendLeftAndRight = false,
            bool extendRightIfZeroLength = false, DoubleCollection strokeDashArray = null)
        {
            var dateStartIndex = dataSeries.FindIndex(start, SearchMode.RoundDown);
            var dateEndIndex = dataSeries.FindIndex(end, SearchMode.RoundUp);

            if (extendLeftAndRight) dateStartIndex -= 4;
            if (dateStartIndex < 0) dateStartIndex = 0;
            if (extendLeftAndRight) dateEndIndex += 4;

            if (extendRightIfZeroLength && dateStartIndex == dateEndIndex) dateEndIndex++;

            var lineAnnotation = new LineAnnotation
            {
                DataContext = trade,
                X1 = dateStartIndex,
                Y1 = price,
                X2 = dateEndIndex,
                Y2 = price,
                StrokeThickness = 3,
                Opacity = 0.8,
                Stroke = new SolidColorBrush(colour)
            };

            if (strokeDashArray != null)
            {
                lineAnnotation.StrokeDashArray = strokeDashArray;
            }
            annotations.Add(lineAnnotation);
        }

        public static LineAnnotation CreateChartLineAnnotation(IDataSeries series, DateTime date1, IComparable value1, DateTime date2, IComparable value2)
        {
            var startIndex = series.FindIndex(date1, SearchMode.Nearest);
            var endIndex = series.FindIndex(date2, SearchMode.Nearest);

            // Would be good to use ((ICategoryCoordinateCalculator)surface.XAxis.GetCurrentCoordinateCalculator()).TransformDataToIndex(date1) but how to get the surface?
            return new LineAnnotation
            {
                X1 = date1 < (DateTime)series.XMin || date1 > (DateTime)series.XMax ? (IComparable)date1 : startIndex,
                X2 = date2 < (DateTime)series.XMin || date2 > (DateTime)series.XMax ? (IComparable)date2 : endIndex,
                Y1 = value1,
                Y2 = value2
            };
        }

        private static void AddLineAnnotations(
            List<DatePrice> prices, IDataSeries series, AnnotationCollection annotations, Color colour)
        {
            int? startIndex = null;
            DateTime? startDate = null;
            decimal? currentPrice = null;
            foreach (var p in prices)
            {
                if (p.Price != currentPrice)
                {
                    // Price changed
                    if (currentPrice != null && startIndex != null)
                    {
                        var endIndex = series.FindIndex(p.Date, SearchMode.Nearest);

                        if (endIndex == startIndex.Value)
                        {
                            endIndex++;
                        }

                        var brush = new SolidColorBrush(colour) { Opacity = 0.3 };
                        var annotation = new LineAnnotation
                        {
                            X1 = startDate, //startIndex.Value,
                            X2 = p.Date, //endIndex,
                            Y1 = currentPrice.Value,
                            Y2 = currentPrice.Value,
                            Stroke = brush
                        };

                        annotations.Add(annotation);
                    }

                    startIndex = series.FindIndex(p.Date, SearchMode.Nearest);
                    startDate = p.Date;
                    currentPrice = p.Price;
                }
            }
        }

        public static void SetChartViewModelVisibleRange(
            Trade trade, ChartViewModel cvm, IList<Candle> candles, Timeframe timeframe)
        {
            if (candles.Count == 0) return;

            var startTime = trade.OrderDateTime ?? trade.EntryDateTime.Value;
            var endTime = trade.CloseDateTime ?? new DateTime(candles.Last().CloseTimeTicks, DateTimeKind.Utc);

            var startCandle = CandlesHelper.GetFirstCandleThatClosesBeforeDateTime(candles, startTime);

            var endCandle = CandlesHelper.GetFirstCandleThatClosesBeforeDateTime(candles, endTime) ?? candles.Last();

            var candlesBeforeTrade = 0;

            switch (timeframe)
            {
                case Timeframe.M1:
                    candlesBeforeTrade = (1 * 60);
                    break;
                case Timeframe.M5:
                    candlesBeforeTrade = (6 * 12);
                    break;
                case Timeframe.M15:
                    candlesBeforeTrade = (2 * 12);
                    break;
                case Timeframe.M30:
                    candlesBeforeTrade = (2 * 12);
                    break;
                case Timeframe.H1:
                    candlesBeforeTrade = (2 * 24);
                    break;
                case Timeframe.H2:
                    candlesBeforeTrade = (5 * 12);
                    break;
                case Timeframe.H4:
                    candlesBeforeTrade = (10 * 6);
                    break;
                case Timeframe.H8:
                    candlesBeforeTrade = (20 * 3);
                    break;
                case Timeframe.D1:
                    candlesBeforeTrade = 30;
                    break;
                default:
                    throw new ApplicationException(timeframe + " timeframe not found for chart helper vis range");
            }

            var candlesAfterTrade = (int)(candlesBeforeTrade * 0.1);

            if (startCandle != null)
            {
                var min = startCandle.Value.OpenTime() - TimeSpan.FromSeconds(candlesBeforeTrade * (int) timeframe); //candles.IndexOf(startCandle.Value) - candlesBeforeTrade;
                var max = endCandle.CloseTime() + TimeSpan.FromSeconds(candlesAfterTrade * (int) timeframe); //candles.IndexOf(endCandle) + candlesAfterTrade;

                /*if (min < 0)
                {
                    min = 0;
                }*/

                SetChartXVisibleRange(cvm, min, max);
            }

            /*
            var miny = double.NaN;
            var maxy = double.NaN;
            for (var i = min; i < candles.Count; i++)
            {
                if (double.IsNaN(miny) || candles[i].LowBid < miny) miny = candles[i].LowBid;
                if (double.IsNaN(maxy) || candles[i].HighBid > maxy) maxy = candles[i].HighBid;
            }

            if (trade.LimitPrice != null && trade.LimitPrice < (decimal)miny) miny = (double)trade.LimitPrice;
            if (trade.LimitPrice != null && trade.LimitPrice > (decimal)maxy) maxy = (double)trade.LimitPrice;
            if (trade.StopPrice != null && trade.StopPrice < (decimal)miny) miny = (double)trade.StopPrice;
            if (trade.StopPrice != null && trade.StopPrice > (decimal)maxy) maxy = (double)trade.StopPrice;*/
        }

        public static void SetChartXVisibleRange(ChartViewModel cvm, DateTime min, DateTime max)
        {
            if (min <= (DateTime)cvm.XVisibleRange.Max)
            {
                cvm.XVisibleRange.Min = min;
                cvm.XVisibleRange.Max = max;
            }
            else
            {
                cvm.XVisibleRange.Max = max;
                cvm.XVisibleRange.Min = min;
            }
        }
    }
}