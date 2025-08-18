using Microsoft.UI.Xaml.Controls;

namespace DromHub.Views
{
    public sealed partial class DeletePartDialog : ContentDialog
    {
        public string ConfirmationMessage { get; }

        public DeletePartDialog(string partName)
        {
            this.InitializeComponent();
            ConfirmationMessage = $"�� ������������� ������ ������� ������ \"{partName}\"?";
            this.DataContext = this;
        }
    }
}
