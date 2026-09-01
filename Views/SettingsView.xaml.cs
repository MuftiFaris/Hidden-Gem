using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;
using Assistant.ViewModels;

namespace Assistant.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// PasswordBox does not support two-way binding (by design — it avoids
        /// holding the password as a plain-text DependencyProperty in memory).
        /// We read the value here and push it to the ViewModel manually.
        /// </summary>
        private void ApiKeyPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm && sender is PasswordBox pb)
                vm.ApiKeyInput = pb.Password;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch { }
        }
    }
}
