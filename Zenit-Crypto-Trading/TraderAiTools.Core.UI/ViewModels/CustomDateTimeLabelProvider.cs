using System;
using SciChart.Charting.Visuals.Axes.LabelProviders;

namespace TraderTools.Core.UI.ViewModels
{
    /// <summary>
    ///  To create a LabelProvider for a NumericAxis or Log Axis, inherit NumericLabelProvider
    /// ..  for a DateTimeAxis, inherit DateTimeLabelProvider
    /// ..  for a TimeSpanAxis, inherit TimeSpanLabelProvider
    /// ..  for a CategoryDateTimeAxis, inherit TradeChartAxisLabelProvider
    /// </summary>
    public class CustomDateTimeLabelProvider : TradeChartAxisLabelProvider
    {
        /// <summary>
        /// Formats a label for the axis from the specified data-value passed in
        /// </summary>
        /// <param name="dataValue">The data-value to format</param>
        /// <returns>
        /// The formatted label string
        /// </returns>
        public override string FormatLabel(IComparable dataValue)
        {
            // Note: Implement as you wish, converting Data-Value to string
            var d = (DateTime)dataValue;


            return d.ToString("dd-MM-yy HH:mm");

            // NOTES:
            // dataValue is always a double.
            // For a NumericAxis this is the double-representation of the data
            // For a DateTimeAxis, the conversion to DateTime is new DateTime((long)dataValue)
            // For a TimeSpanAxis the conversion to TimeSpan is new TimeSpan((long)dataValue)
            // For a CategoryDateTimeAxis, dataValue is the index to the data-series
        }
    }
}