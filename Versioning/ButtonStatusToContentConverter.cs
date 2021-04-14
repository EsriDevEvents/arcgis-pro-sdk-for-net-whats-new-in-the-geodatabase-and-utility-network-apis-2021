using System;
using System.Globalization;
using System.Windows.Data;

namespace VersionManagementDemo
{
  public class ButtonStatusToContentConverter:IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (value == null) return "Select";
      if (!(value is bool boolValue)) return "Select";
      return boolValue ? "Selected" : "Select";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }
}
