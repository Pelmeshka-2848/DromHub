using DromHub.Models;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;

namespace DromHub.Views
{
    public sealed partial class BrandMergePage : Page
    {
        public BrandMergeViewModel ViewModel { get; }

        public BrandMergePage()
        {
            this.InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<BrandMergeViewModel>();
            DataContext = ViewModel;

            Loaded += async (_, __) =>
            {
                ViewModel.XamlRoot = this.XamlRoot;
                await ViewModel.LoadAsync();
            };
        }

        // Поиск
        private void OnSearchSourcesKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key.ToString() == "Enter") ViewModel.ApplySourcesFilter();
        }
        private void OnSearchTargetKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key.ToString() == "Enter") ViewModel.ApplyTargetFilter();
        }
        private void ApplySourcesFilter_Click(object sender, RoutedEventArgs e) => ViewModel.ApplySourcesFilter();
        private void ApplyTargetFilter_Click(object sender, RoutedEventArgs e) => ViewModel.ApplyTargetFilter();

        // Источники
        private void OnSourceItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Brand b) ViewModel.AddSource(b.Id);
        }
        private void AddSource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid id) ViewModel.AddSource(id);
        }
        private void RemoveSource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid id) ViewModel.RemoveSource(id);
        }

        // Целевой бренд
        private void OnTargetItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Brand b) ViewModel.SetTarget(b.Id);
        }
        private void SetTarget_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid id) ViewModel.SetTarget(id);
        }

        // Низ
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}