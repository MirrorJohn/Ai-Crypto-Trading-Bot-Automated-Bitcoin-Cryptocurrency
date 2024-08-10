using System;
using System.Globalization;
using System.Windows.Data;

namespace TraderTools.Core.UI.Converters
{
    public class LimitDecimalPlaces : IValueConverter
    {
        public int DecimalPlaces { get; set; } = 1;


        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float f)
            {
                return Math.Round(f, DecimalPlaces);
            }

            if (value is double d)
            {
                return Math.Round(d, DecimalPlaces);
            }

            if (value is decimal dc)
            {
                return Math.Round(dc, DecimalPlaces);
            }

            if (value == null)
            {
                return 0M;
            }

            throw new ApplicationException("Error LimitDecimalPlaces");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}