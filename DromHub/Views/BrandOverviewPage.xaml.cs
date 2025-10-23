using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace DromHub.Views
{
    /// <summary>
    /// Класс BrandOverviewPage отвечает за логику компонента BrandOverviewPage.
    /// </summary>
    public sealed partial class BrandOverviewPage : Page
    {
        /// <summary>
        /// Свойство ViewModel предоставляет доступ к данным ViewModel.
        /// </summary>
        public BrandOverviewViewModel ViewModel { get; }
        /// <summary>
        /// Конструктор BrandOverviewPage инициализирует экземпляр класса.
        /// </summary>

        public BrandOverviewPage()
        {
            InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<BrandOverviewViewModel>();
            DataContext = ViewModel;
        }
        /// <summary>
        /// Метод OnNavigatedTo выполняет основную операцию класса.
        /// </summary>

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Guid id)
            {
                if (ViewModel.BrandId == id)
                {
                    return;
                }

                await ViewModel.InitializeAsync(id, this.XamlRoot);
            }
        }
        /// <summary>
        /// Метод OnOpenInBrowserClicked выполняет основную операцию класса.
        /// </summary>

        private async void OnOpenInBrowserClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ViewModel.OpenWebsiteAsync();
        }
    }
}