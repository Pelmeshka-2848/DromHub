using DromHub.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DromHub.Views
{
    public sealed partial class BrandDetailsSettingsPage : Page
    {
        public BrandDetailsSettingsPage()
        {
            InitializeComponent();
        }

        // --- Наценка ---
        private void PresetMarkup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && double.TryParse(b.Tag?.ToString(), out var v))
                ((BrandDetailsViewModel)DataContext).MarkupEditor = v;
        }

        private void ConfirmDisable_Click(object sender, RoutedEventArgs e)
        {
            ((BrandDetailsViewModel)DataContext).ConfirmDisable();
        }

        private void DisableInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
        {
            ((BrandDetailsViewModel)DataContext).CancelDisable();
        }

        // --- Алиасы ---
        private async void AddAlias_Click(object sender, RoutedEventArgs e) =>
            await ((BrandDetailsViewModel)DataContext).AddAliasAsync();

        private async void EditAlias_Click(object sender, RoutedEventArgs e) =>
            await ((BrandDetailsViewModel)DataContext).EditAliasAsync();

        private async void DeleteAlias_Click(object sender, RoutedEventArgs e) =>
            await ((BrandDetailsViewModel)DataContext).DeleteAliasAsync();
    }
}