using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using DromHub;

namespace DromHub
{
    public sealed partial class Search : Page
    {
        private ObservableCollection<Part> ResultsCollection { get; } = new ObservableCollection<Part>();

        public Search()
        {
            this.InitializeComponent();
            ResultsList.ItemsSource = ResultsCollection;
        }

        private async void ShowButton_Click(object sender, RoutedEventArgs e)
        {
            var results = await DatabaseHelper.SearchPartsByNumberAsync(SearchBox.Text);
            UpdateResults(results);
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = (sender as TextBox)?.Text ?? "";
            var results = await DatabaseHelper.SearchPartsByNumberAsync(searchText);
            UpdateResults(results);
        }

        private void UpdateResults(IEnumerable<Part> results)
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            if (dispatcherQueue != null)
            {
                dispatcherQueue.TryEnqueue(() =>
                {
                    ResultsCollection.Clear();
                    foreach (var item in results)
                        ResultsCollection.Add(item);
                });
            }
            else
            {
                ResultsCollection.Clear();
                foreach (var item in results)
                    ResultsCollection.Add(item);
            }
        }
    }
}
