using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace DromHub.Views
{
    public sealed partial class BrandOverviewPage : Page
    {
        public BrandOverviewViewModel ViewModel { get; }

        public BrandOverviewPage()
        {
            InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<BrandOverviewViewModel>();
            DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Guid id)
                await ViewModel.InitializeAsync(id, this.XamlRoot);
        }
    }
}