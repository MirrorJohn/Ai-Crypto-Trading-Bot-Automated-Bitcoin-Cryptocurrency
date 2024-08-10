using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using SciChart.Charting.ChartModifiers;
using SciChart.Charting.Model.ChartData;
using SciChart.Charting.Visuals.RenderableSeries;

namespace TraderTools.Core.UI.ChartModifiers
{
    public class FilteringLegendModifier : LegendModifier
    {
        public static bool GetIncludeSeries(DependencyObject obj)
        {
            return (bool)obj.GetValue(IncludeSeriesProperty);
        }

        public static void SetIncludeSeries(DependencyObject obj, bool value)
        {
            obj.SetValue(IncludeSeriesProperty, value);
        }

        // Using a DependencyProperty as the backing store for IncludeSeries.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IncludeSeriesProperty = DependencyProperty.RegisterAttached("IncludeSeries", typeof(bool), typeof(FilteringLegendModifier), new PropertyMetadata(true));

        protected override ObservableCollection<SeriesInfo> GetSeriesInfo(IEnumerable<IRenderableSeries> allSeries)
        {
            var filteredSeries = allSeries.Where(s => s is UIElement)
                .Where(s => (bool)((UIElement)s).GetValue(IncludeSeriesProperty));
            return base.GetSeriesInfo(filteredSeries);
        }
    }
}