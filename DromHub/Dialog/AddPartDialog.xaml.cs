using Microsoft.UI.Xaml.Controls;
using DromHub.ViewModels;

namespace DromHub.Views
{
    public sealed partial class AddPartDialog : ContentDialog
    {
        public PartViewModel ViewModel { get; }

        public AddPartDialog(PartViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
            this.DataContext = ViewModel;
        }
    }
}
