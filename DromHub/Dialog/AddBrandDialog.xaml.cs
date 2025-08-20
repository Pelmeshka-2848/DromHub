using Microsoft.UI.Xaml.Controls;
using DromHub.ViewModels;

namespace DromHub.Views
{
    public sealed partial class AddBrandDialog : ContentDialog
    {
        public BrandViewModel ViewModel { get; }

        public AddBrandDialog(BrandViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
            this.DataContext = ViewModel;
        }
    }
}
