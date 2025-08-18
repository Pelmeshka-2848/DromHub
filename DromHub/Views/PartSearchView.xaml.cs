using Microsoft.UI.Xaml.Controls;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Microsoft.UI.Xaml.Input;

namespace DromHub.Views
{
    public sealed partial class PartSearchView : Page
    {
        public PartSearchViewModel ViewModel { get; }

        public PartSearchView()
        {
            this.InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<PartSearchViewModel>();
            this.DataContext = ViewModel;
        }

        private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ViewModel.SearchPartsCommand.ExecuteAsync(null);
            }
        }
    }
}