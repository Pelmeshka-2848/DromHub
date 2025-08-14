using DromHub.Data;
using DromHub.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Diagnostics;

namespace DromHub
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            InitializeDatabaseAsync();
        }

        private async void InitializeDatabaseAsync()
        {
            try
            {
                using (var scope = App.ServiceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await DatabaseInitializer.InitializeAsync(dbContext, forceReset: true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"������ ������������� ��: {ex.Message}");

                // �������������: �������� ��������� �� ������
                var dialog = new ContentDialog
                {
                    Title = "������",
                    Content = $"�� ������� ���������������� ��: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private void OpenTab(string header, UIElement content)
        {
            // ���������, �� ������� �� ��� ������� � ����� ����������
            foreach (var item in MainTabView.TabItems)
            {
                if (item is TabViewItem existingTab && existingTab.Header?.ToString() == header)
                {
                    MainTabView.SelectedItem = existingTab;
                    return;
                }
            }

            // ������ ����� �������
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

        private void OpenSearch_Click(object sender, RoutedEventArgs e)
        {
            var searchView = new PartSearchView();

            // ������� ���� ������ � ��������� �������������
            if (searchView.FindName("SearchTextBox") is TextBox searchTextBox)
            {
                // ������������� �� ������� ������� �������
                searchTextBox.KeyDown += SearchTextBox_KeyDown;
            }

            OpenTab("�����", searchView);
        }

        private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (sender is TextBox searchTextBox &&
                    MainTabView.SelectedItem is TabViewItem selectedTab &&
                    selectedTab.Content is PartSearchView searchView)
                {
                    // ��������� SearchText �� ViewModel
                    searchView.ViewModel.SearchText = searchTextBox.Text;
                    // �������� ������� ������
                    searchView.ViewModel.SearchPartsCommand.ExecuteAsync(null);
                }
            }
        }

        private void OpenPartView_Click(object sender, RoutedEventArgs e)
            => OpenTab("��������", new PartView());

        private void OpenTab3_Click(object sender, RoutedEventArgs e)
            => OpenTab("������� 3", new Tab3Page());

        private void OpenTab4_Click(object sender, RoutedEventArgs e)
            => OpenTab("������� 4", new Tab4Page());
    }
}