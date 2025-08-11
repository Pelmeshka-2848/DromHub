using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace DromHub
{
    public sealed partial class Search : Page
    {
        public Search()
        {
            this.InitializeComponent();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = SearchBox.Text;
            SearchHandler.HandleSearchText(text);
        }

        private void ShowButton_Click(object sender, RoutedEventArgs e)
        {
            string currentText = SearchBox.Text;
            SearchResultTextBlock.Text = $"Текущий текст поиска:\n{currentText}";
        }
    }
}