using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DromHub.Views
{
    public sealed partial class BrandPage : Page
    {
        public BrandViewModel ViewModel { get; }

        public BrandPage()
        {
            this.InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<BrandViewModel>();
            this.DataContext = ViewModel;
            Loaded += async (_, __) => await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
        }

        private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                _ = ViewModel.SearchBrandsCommand.ExecuteAsync(null);
            }
        }
    }
}