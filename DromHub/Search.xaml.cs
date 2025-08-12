using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DromHub;
using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace DromHub
{
    public sealed partial class Search : Page
    {
        public Search()
        {
            this.InitializeComponent();
        }
        /*
       private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
       {
           var results = SearchHandler.HandleSearchText(SearchBox.Text);
           ResultsList.ItemsSource = results.Select(r => new { r.Brand, r.Number, Price = r.Price.ToString("F2") });
       }

       private void ShowButton_Click(object sender, RoutedEventArgs e)
       {
           var results = SearchHandler.HandleSearchText(SearchBox.Text);
           ResultsList.ItemsSource = results.Select(r => new { r.Brand, r.Number, Price = r.Price.ToString("F2") });
       }
       */
       private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
       {
           var results = SqlServerDatabaseHelper.SearchPartsByNumber(SearchBox.Text);
           ResultsList.ItemsSource = results;
       }

       private void ShowButton_Click(object sender, RoutedEventArgs e)
       {
           var results = SqlServerDatabaseHelper.SearchPartsByNumber(SearchBox.Text);
           ResultsList.ItemsSource = results;
       }
    }
}