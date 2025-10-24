using Microsoft.UI.Xaml.Controls;
using DromHub.ViewModels;

namespace DromHub.Views
{
    /// <summary>
    /// Класс AddPartDialog отвечает за логику компонента AddPartDialog.
    /// </summary>
    public sealed partial class AddPartDialog : ContentDialog
    {
        /// <summary>
        /// Свойство ViewModel предоставляет доступ к данным ViewModel.
        /// </summary>
        public PartViewModel ViewModel { get; }
        /// <summary>
        /// Конструктор AddPartDialog инициализирует экземпляр класса.
        /// </summary>

        public AddPartDialog(PartViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
            this.DataContext = ViewModel;
        }
    }
}
