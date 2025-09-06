using Microsoft.UI.Xaml.Controls;
using DromHub.ViewModels;

namespace DromHub.Views
{
    public sealed partial class AddAliasDialog : ContentDialog
    {
        public string AliasName { get; set; } = string.Empty;

        public AddAliasDialog()
        {
            InitializeComponent();
        }
    }
}
