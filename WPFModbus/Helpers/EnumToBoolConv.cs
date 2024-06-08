using System.Globalization;
using System.Windows.Data;

namespace WPFModbus.Helpers
{
    public class EnumToBoolConv : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string enumValue = value.ToString();
            string targetValue = parameter.ToString();

            return enumValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return null;

            bool isChecked = (bool)value;
            if (isChecked)
                return Enum.Parse(targetType, parameter.ToString());

            return null;
        }
    }
}
