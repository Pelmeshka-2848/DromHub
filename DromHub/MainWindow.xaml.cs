using DromHub.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DromHub
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Activated += MainWindow_Activated_FirstTime;
            //DataSeeder.SeedAll();
        }
        private async void MainWindow_Activated_FirstTime(object sender, WindowActivatedEventArgs e)
        {
            this.Activated -= MainWindow_Activated_FirstTime; 
        }

        private void OpenTab(string header, UIElement content)
        {
            foreach (var item in MainTabView.TabItems)
            {
                if (item is TabViewItem t && t.Header?.ToString() == header)
                {
                    MainTabView.SelectedItem = t;
                    return;
                }
            }

            var newTab = new TabViewItem
            {
                Header = header,
                Content = content,
                IsClosable = true
            };

            MainTabView.TabItems.Add(newTab);
            MainTabView.SelectedItem = newTab;
        }

        private void MainTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            sender.TabItems.Remove(args.Tab);
        }

        private void OpenSearch_Click(object s, RoutedEventArgs e) => OpenTab("Поиск", new PartView());
        private void OpenTab2_Click(object s, RoutedEventArgs e) => OpenTab("Вкладка 2", new Tab2Page());
        private void OpenTab3_Click(object s, RoutedEventArgs e) => OpenTab("Вкладка 3", new Tab3Page());
        private void OpenTab4_Click(object s, RoutedEventArgs e) => OpenTab("Вкладка 4", new Tab4Page());
    }
}
