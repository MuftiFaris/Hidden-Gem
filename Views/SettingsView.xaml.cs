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
            Loaded += (_, _) => AttachViewModel();
            Unloaded += (_, _) => DetachViewModel();
            DataContextChanged += (_, _) => { DetachViewModel(); AttachViewModel(); };
        }

        private SettingsViewModel? _attachedVm;
        private void AttachViewModel()
        {
            if (_attachedVm == DataContext) return;
            if (DataContext is SettingsViewModel vm)
            {
                _attachedVm = vm;
                vm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }
        private void DetachViewModel()
        {
            if (_attachedVm != null) _attachedVm.PropertyChanged -= ViewModel_PropertyChanged;
            _attachedVm = null;
        }
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.ApiKeyInput) && _attachedVm?.ApiKeyInput.Length == 0)
                ApiKeyPasswordBox.Clear();
        }
        private void TokenInput_ValidationError(object sender, ValidationErrorEventArgs e)
        {
            if (DataContext is SettingsViewModel vm) vm.HasInputErrors = (Validation.GetHasError(TokenInput) || Validation.GetHasError(TimeoutInput));
        }
        private void SaveSettings_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            TimeoutInput.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            TokenInput.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (DataContext is SettingsViewModel vm) vm.HasInputErrors = (Validation.GetHasError(TokenInput) || Validation.GetHasError(TimeoutInput));
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
