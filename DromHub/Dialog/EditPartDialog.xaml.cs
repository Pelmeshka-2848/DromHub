using Microsoft.UI.Xaml.Controls;
using DromHub.ViewModels;

namespace DromHub.Views
{
    /// <summary>
    /// Класс EditPartDialog отвечает за логику компонента EditPartDialog.
    /// </summary>
    public sealed partial class EditPartDialog : ContentDialog
    {
        /// <summary>
        /// Свойство ViewModel предоставляет доступ к данным ViewModel.
        /// </summary>
        public PartViewModel ViewModel { get; }
        /// <summary>
        /// Конструктор EditPartDialog инициализирует экземпляр класса.
        /// </summary>

        public EditPartDialog(PartViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
            this.DataContext = ViewModel;
        }
        /// <summary>
        /// Метод ContentDialog_PrimaryButtonClick выполняет основную операцию класса.
        /// </summary>

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ViewModel.SavePartCommand.CanExecute(null))
            {
                await ViewModel.SavePartCommand.ExecuteAsync(null);
            }
        }
    }
}
