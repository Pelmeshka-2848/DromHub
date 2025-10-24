using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DromHub.Views
{
    /// <summary>
    /// Класс BrandsHomePage отвечает за логику компонента BrandsHomePage.
    /// </summary>
    public sealed partial class BrandsHomePage : Page
    {
        /// <summary>
        /// Свойство ViewModel предоставляет доступ к данным ViewModel.
        /// </summary>
        public BrandsHomeViewModel ViewModel { get; }
        /// <summary>
        /// Конструктор BrandsHomePage инициализирует экземпляр класса.
        /// </summary>

        public BrandsHomePage()
        {
            InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<BrandsHomeViewModel>();
            DataContext = ViewModel;
            Loaded += async (_, __) => await ViewModel.LoadAsync();
        }
    }
}