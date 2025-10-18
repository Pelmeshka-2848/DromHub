using Microsoft.UI.Xaml.Controls;
using DromHub.ViewModels;
using System.Collections.Specialized;

namespace DromHub.Views
{
    public sealed partial class MailParserView : Page
    {
        public MailParserViewModel ViewModel { get; }

        public MailParserView()
        {
            this.InitializeComponent();
            ViewModel = App.GetService<MailParserViewModel>();
            DataContext = ViewModel;
            if (DataContext is DromHub.ViewModels.MailParserViewModel vm)
            {
                // �������� ����� �������� ���������
                vm.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
            }
        }
        private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // �������� ���� ����� ����������
            LogScrollViewer?.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
        }
    }
}