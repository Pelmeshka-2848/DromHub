using System;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DromHub.Views
{
    /// <summary>
    /// Класс BrandChangesPage отвечает за отображение истории изменений бренда.
    /// </summary>
    public sealed partial class BrandChangesPage : Page
    {
        /// <summary>
        /// Создаёт экземпляр страницы.
        /// </summary>
        public BrandChangesPage()
        {
            InitializeComponent();

            ViewModel = App.ServiceProvider.GetRequiredService<BrandChangeLogViewModel>();
            DataContext = ViewModel;

            NavigationCacheMode = NavigationCacheMode.Required;
        }

        /// <summary>
        /// Текущая view-model страницы.
        /// </summary>
        public BrandChangeLogViewModel ViewModel { get; }

        /// <inheritdoc />
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Guid id && id != Guid.Empty)
            {
                await ViewModel.LoadAsync(id);
                return;
            }

            if (e.Parameter is string s && Guid.TryParse(s, out var parsed) && parsed != Guid.Empty)
            {
                await ViewModel.LoadAsync(parsed);
            }
            else
            {
                ViewModel.Reset();
            }
        }

        /// <inheritdoc />
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.Reset();
        }
    }
}
