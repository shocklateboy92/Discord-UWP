using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Discord_UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LoginPage : Page
    {
        public LoginPage()
        {
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            App.AuthManager.StateChanged += OnStateChanged;
            App.Client.Ready += OnClientReady;

            OnStateChanged(this, App.AuthManager.CurrentState);
        }

        private void OnClientReady(object sender, EventArgs e) =>
            Helpers.RunInUiThread(
                () => ((Frame)Window.Current.Content).Navigate(typeof(MainPage))
            );

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            App.AuthManager.StateChanged -= OnStateChanged;
            App.Client.Ready -= OnClientReady;
        }

        private void OnStateChanged(object sender, AuthenticationManager.AuthenticationState e) =>
            Helpers.RunInUiThread(() =>
            {
                switch (e)
                {
                    case AuthenticationManager.AuthenticationState.InProgress:
                    // Even once we succced, we want to show the spinner until the
                    // next page has finished its network requests and is ready
                    case AuthenticationManager.AuthenticationState.Authenticated:
                        _spinnerFrame.Visibility = Visibility.Visible;
                        break;
                    case AuthenticationManager.AuthenticationState.Failed:
                    case AuthenticationManager.AuthenticationState.UserInteractionRequired:
                        _spinnerFrame.Visibility = Visibility.Collapsed;
                        break;
                }
            });

        private async void OnAccepted(object sender, object args) =>
            await App.AuthManager.DoAuthentication(_usernameText.Text, _passwordText.Password);

        private void OnTextBoxKeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                OnAccepted(sender, e);
                e.Handled = true;
            }
        }
    }
}
