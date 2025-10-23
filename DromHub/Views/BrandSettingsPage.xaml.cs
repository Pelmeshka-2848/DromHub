using System;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;

namespace DromHub.Views
{
    /// <summary>
    /// Класс BrandSettingsPage отвечает за логику компонента BrandSettingsPage.
    /// </summary>
    public sealed partial class BrandSettingsPage : Page
    {
        private readonly IReadOnlyList<Expander> _accordion;
        private bool _suppressNavigationPrompt;

        private readonly struct PendingNavigationRequest
        {
            public PendingNavigationRequest(NavigatingCancelEventArgs args)
            {
                Mode = args.NavigationMode;
                TargetPageType = args.SourcePageType;
                Parameter = args.Parameter;
                TransitionInfo = args.NavigationTransitionInfo;
            }

            public NavigationMode Mode { get; }

            public Type? TargetPageType { get; }

            public object? Parameter { get; }

            public NavigationTransitionInfo? TransitionInfo { get; }
        }

        /// <summary>
        /// Gets the page view model.
        /// </summary>
        public BrandSettingsViewModel ViewModel { get; }

        /// <summary>
        /// Конструктор BrandSettingsPage инициализирует экземпляр класса.
        /// </summary>
        public BrandSettingsPage()
        {
            InitializeComponent();

            ViewModel = App.ServiceProvider.GetRequiredService<BrandSettingsViewModel>();
            DataContext = ViewModel;

            NavigationCacheMode = NavigationCacheMode.Required;

            _accordion = new[] { ExpCore, ExpAliases, ExpPricing, ExpUser };

            foreach (var ex in _accordion)
            {
                ex.Expanding += Accordion_Expanding;
            }

            ApplyAccordionState();
        }

        /// <summary>
        /// Handles navigation to the page and triggers loading of the brand data.
        /// </summary>
        /// <param name="e">Navigation arguments.</param>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var brandId = e?.Parameter switch
            {
                Guid g => g,
                string s when Guid.TryParse(s, out var parsed) => parsed,
                _ => Guid.Empty
            };

            if (brandId != Guid.Empty)
            {
                if (ViewModel.BrandId == brandId)
                {
                    return;
                }

                await ViewModel.InitializeAsync(brandId, XamlRoot);
            }

            ApplyAccordionState();
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (_suppressNavigationPrompt)
            {
                _suppressNavigationPrompt = false;
                base.OnNavigatingFrom(e);
                return;
            }

            if (ViewModel.HasChanges && !ViewModel.SaveCommand.IsRunning)
            {
                var pendingNavigation = new PendingNavigationRequest(e);

                e.Cancel = true;

                var dialog = new ContentDialog
                {
                    Title = "Есть несохранённые изменения",
                    Content = "Сохранить изменения перед выходом?",
                    PrimaryButtonText = "Сохранить",
                    SecondaryButtonText = "Не сохранять",
                    CloseButtonText = "Отмена",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    if (ViewModel.SaveCommand.CanExecute(null))
                    {
                        await ViewModel.SaveCommand.ExecuteAsync(null);
                    }

                    if (!ViewModel.HasChanges)
                    {
                        ResumeNavigation(pendingNavigation);
                    }
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    ResumeNavigation(pendingNavigation);
                }

                return;
            }

            base.OnNavigatingFrom(e);
        }

        private void ResumeNavigation(in PendingNavigationRequest request)
        {
            if (Frame is null)
            {
                return;
            }

            _suppressNavigationPrompt = true;

            var resumed = false;

            switch (request.Mode)
            {
                case NavigationMode.Back:
                    if (Frame.CanGoBack)
                    {
                        Frame.GoBack();
                        resumed = true;
                    }

                    break;
                case NavigationMode.Forward:
                    if (Frame.CanGoForward)
                    {
                        Frame.GoForward();
                        resumed = true;
                    }

                    break;
                case NavigationMode.New:
                case NavigationMode.Refresh:
                default:
                    if (request.TargetPageType is not null)
                    {
                        if (request.TransitionInfo is NavigationTransitionInfo info)
                        {
                            Frame.Navigate(request.TargetPageType, request.Parameter, info);
                        }
                        else
                        {
                            Frame.Navigate(request.TargetPageType, request.Parameter);
                        }

                        resumed = true;
                    }

                    break;
            }

            if (!resumed)
            {
                _suppressNavigationPrompt = false;
            }
        }

        /// <summary>
        /// Метод Accordion_Expanding выполняет основную операцию класса.
        /// </summary>
        private void Accordion_Expanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            if (!string.IsNullOrEmpty(sender.Name))
            {
                ViewModel.LastExpandedSection = sender.Name;
            }

            foreach (var ex in _accordion)
            {
                if (!ReferenceEquals(ex, sender))
                {
                    ex.IsExpanded = false;
                }
            }
        }

        private void ApplyAccordionState()
        {
            var target = ExpCore;
            var targetName = ViewModel.LastExpandedSection;

            if (!string.IsNullOrWhiteSpace(targetName))
            {
                foreach (var expander in _accordion)
                {
                    if (string.Equals(expander.Name, targetName, StringComparison.Ordinal))
                    {
                        target = expander;
                        break;
                    }
                }
            }

            foreach (var expander in _accordion)
            {
                expander.IsExpanded = ReferenceEquals(expander, target);
            }
        }
    }
}
