using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace DromHub.Views
{
    public sealed partial class EditBrandDialog : ContentDialog
    {
        public string BrandName { get; set; }
        public bool IsPrimaryAlias { get; set; }

        // ����������� � ����� ���������� (��� �������� �������������)
        public EditBrandDialog(string currentName) : this(currentName, false)
        {
        }

        // �������� ����������� � ����� �����������
        public EditBrandDialog(string currentName, bool isPrimary)
        {
            InitializeComponent();
            BrandName = currentName;
            IsPrimaryAlias = isPrimary;
            DataContext = this;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(BrandName))
            {
                ErrorMessageText.Text = "�������� �� ����� ���� ������";
                ErrorMessageText.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            ErrorMessageText.Visibility = Visibility.Collapsed;
        }
    }
}