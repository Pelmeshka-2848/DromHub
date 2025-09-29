using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DromHub.Views
{
    public sealed partial class BrandsHomePage : Page
    {
        public BrandsHomeViewModel ViewModel { get; }

        public BrandsHomePage()
        {
            InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<BrandsHomeViewModel>();
            DataContext = ViewModel;
            Loaded += async (_, __) => await ViewModel.LoadAsync();
        }
    }
}