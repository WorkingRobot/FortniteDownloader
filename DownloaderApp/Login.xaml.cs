using FortniteDownloader;
using System;
using System.Windows;

namespace DownloaderApp
{
    /// <summary>
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        public Authorization Auth;

        public Login()
        {
            InitializeComponent();
        }

        private async void LoginClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsrTxt.Text))
            {
                StatusTxt.Text = "Logged in, you may now close the window";
                MessageBox.Show(this, "Enter a username!", "DownloaderApp", MessageBoxButton.OK);
                return;
            }
            if (string.IsNullOrWhiteSpace(PwdTxt.Password))
            {
                MessageBox.Show(this, "Enter a password!", "DownloaderApp", MessageBoxButton.OK);
                return;
            }
            StatusTxt.Text = "Logging in";
            var auth = new Authorization(UsrTxt.Text, PwdTxt.Password);
            try
            {
                await auth.Login();
            }
            catch (ArgumentException ex)
            {
                StatusTxt.Text = ex.Message;
                return;
            }
            Auth = auth;
            StatusTxt.Text = "Logged in, you may now close the window";
        }
    }
}