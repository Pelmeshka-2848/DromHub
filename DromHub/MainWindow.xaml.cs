using System;
using DromHub.Models;
using DromHub.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DromHub
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // ��������� ��������� ��������
            contentFrame.Navigate(typeof(MainPage));
        }

        private void NavigationView_SelectionChanged(NavigationView sender,
                                                   NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer != null)
            {
                string tag = args.SelectedItemContainer.Tag.ToString();
                Type pageType = null;

                // ���������� ��� �������� �� ����
                switch (tag)
                {
                    case "MainPage":
                        pageType = typeof(MainPage);
                        break;
                    case "PartSearchPage":
                        pageType = typeof(PartSearchPage);
                        break;
                }

                // ���� ��� �������� ��������� � ��� �� ������� ��������
                if (pageType != null && contentFrame.CurrentSourcePageType != pageType)
                {
                    contentFrame.Navigate(pageType);
                }
            }
        }
    }
}