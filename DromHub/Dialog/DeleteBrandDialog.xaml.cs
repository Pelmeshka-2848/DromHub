using Microsoft.UI.Xaml.Controls;

namespace DromHub.Views
{
    public sealed partial class DeleteBrandDialog : ContentDialog
    {
        public DeleteBrandDialog(string brandName)
        {
            InitializeComponent();
            BrandNameText.Text = brandName;
        }
    }
}