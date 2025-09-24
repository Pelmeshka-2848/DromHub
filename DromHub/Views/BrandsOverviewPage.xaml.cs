using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DromHub.Views
{
    public sealed partial class BrandsOverviewPage : Page
    {
        public BrandsOverviewViewModel ViewModel { get; }

        public BrandsOverviewPage()
        {
            InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<BrandsOverviewViewModel>();
            DataContext = ViewModel;
            Loaded += async (_, __) => await ViewModel.LoadAsync();
        }

        private void OpenBrands_Click(object sender, RoutedEventArgs e) =>
            Frame?.Navigate(typeof(BrandsListPage));

        private void OpenMergeWizard_Click(object sender, RoutedEventArgs e) =>
            Frame?.Navigate(typeof(BrandMergePage));
    }
}