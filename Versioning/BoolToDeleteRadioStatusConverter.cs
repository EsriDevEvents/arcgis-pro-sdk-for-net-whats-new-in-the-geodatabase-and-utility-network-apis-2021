using System;
using System.Globalization;
using System.Windows.Data;

namespace VersionManagementDemo
{
  public class BoolToDeleteRadioStatusConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (value != null)
      {
        if (value is bool boolValue)
        {
          if (boolValue)
          {
            return false;
          }

          return true;
        }
      }

      return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }
}
