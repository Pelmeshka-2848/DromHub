using DromHub.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DromHub.Views
{
    public sealed partial class PartView : Page
    {
        public PartViewModel ViewModel { get; }

        public PartView()
        {
            this.InitializeComponent();

            // Получаем ViewModel через DI
            ViewModel = App.GetService<PartViewModel>();

            // Устанавливаем DataContext
            this.DataContext = ViewModel;

            // Загружаем бренды при инициализации
            Loaded += async (s, e) => await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
        }
    }
}