using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace BfgPortalExtension
{
    public class WpfPanel : Window
    {
        public WpfPanel(string title, string url)
        {
            Title = title;
            Width = 800;
            Height = 600;
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;

            var browser = new WebBrowser
            {
                NavigationUIVisible = false,
                IsScriptEnabled = true
            };

            browser.Navigated += (sender, e) =>
                ((WebBrowser)sender).NavigateToString(e.Uri.ToString());

            browser.Navigate(url);
            Content = browser;
        }
    }
}