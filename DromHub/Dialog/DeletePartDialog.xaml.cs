using Microsoft.UI.Xaml.Controls;

namespace DromHub.Views
{
    /// <summary>
    /// Класс DeletePartDialog отвечает за логику компонента DeletePartDialog.
    /// </summary>
    public sealed partial class DeletePartDialog : ContentDialog
    {
        /// <summary>
        /// Свойство ConfirmationMessage предоставляет доступ к данным ConfirmationMessage.
        /// </summary>
        public string ConfirmationMessage { get; }
        /// <summary>
        /// Конструктор DeletePartDialog инициализирует экземпляр класса.
        /// </summary>

        public DeletePartDialog(string partName)
        {
            this.InitializeComponent();
            ConfirmationMessage = $"     \"{partName}\"?";
            this.DataContext = this;
        }
    }
}
