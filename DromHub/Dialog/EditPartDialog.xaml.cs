using Microsoft.UI.Xaml.Controls;
using DromHub.ViewModels;

namespace DromHub.Views
{
    public sealed partial class EditPartDialog : ContentDialog
    {
        public PartViewModel ViewModel { get; }

        public EditPartDialog(PartViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
            this.DataContext = ViewModel;
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ViewModel.SavePartCommand.CanExecute(null))
            {
                await ViewModel.SavePartCommand.ExecuteAsync(null);
            }
        }
    }
}
