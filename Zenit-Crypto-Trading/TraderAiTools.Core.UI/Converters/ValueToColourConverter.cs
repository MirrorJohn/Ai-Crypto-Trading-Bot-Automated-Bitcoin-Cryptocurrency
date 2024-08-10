using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TraderTools.Core.UI.Converters
{
   public class ValueToColourConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var v = (decimal?)value;

            if (v != null)
            {
                if (v.Value < 0)
                {
                    return new SolidColorBrush(Color.FromRgb(255, 210, 210));
                }

                if (v.Value > 0)
                {
                    return new SolidColorBrush(Color.FromRgb(210, 255, 210));
                }
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
