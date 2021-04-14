using System.Windows;

namespace VersionManagementDemo
{
  public class AlertService
  {
    public void Show(string alertMessage)
    {
      MessageBox.Show(alertMessage,"Alert",MessageBoxButton.OK, MessageBoxImage.Information);

    }
  }
}
