using System;
using System.Collections.Specialized;
using System.ComponentModel;
using DromHub.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
            if (DataContext is MailParserViewModel vm)
            {
                vm.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
                vm.PropertyChanged += ViewModelOnPropertyChanged;
                PasswordBox.Password = vm.Password ?? string.Empty;
            }
        }

        private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MailParserViewModel.Password))
            {
                var password = ViewModel?.Password ?? string.Empty;
                if (!string.Equals(PasswordBox.Password, password, StringComparison.Ordinal))
                {
                    PasswordBox.Password = password;
                }
            }
        }

        private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                var password = passwordBox.Password ?? string.Empty;
                if (!string.Equals(ViewModel.Password, password, StringComparison.Ordinal))
                {
                    ViewModel.UpdatePassword(password);
                }
            }
        }

        private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            LogScrollViewer?.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
        }
    }
}
