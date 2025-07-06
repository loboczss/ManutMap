using System;
using System.Globalization;
using System.Windows.Data;

namespace ManutMap.Helpers
{
    public class ProportionalHeightConverter : IMultiValueConverter
    {
        public double MaxHeight { get; set; } = 100;

        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length < 2) return 0.0;
            if (!double.TryParse(values[0]?.ToString(), out double val)) return 0.0;
            if (!double.TryParse(values[1]?.ToString(), out double max)) return 0.0;
            if (max <= 0) return 0.0;
            return val / max * MaxHeight;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
