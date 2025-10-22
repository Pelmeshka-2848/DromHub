using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace DromHub.Views
{
    /// <summary>
    /// Класс BrandSettingsPage отвечает за логику компонента BrandSettingsPage.
    /// </summary>
    public sealed partial class BrandSettingsPage : Page
    {
        private readonly IReadOnlyList<Expander> _accordion;
        /// <summary>
        /// Конструктор BrandSettingsPage инициализирует экземпляр класса.
        /// </summary>

        public BrandSettingsPage()
        {
            InitializeComponent();

            _accordion = new[] { ExpCore, ExpAliases, ExpPricing, ExpUser };

            foreach (var ex in _accordion)
                ex.Expanding += Accordion_Expanding;

            Loaded += (_, __) =>
            {
                // открыть первый, остальные закрыть
                ExpCore.IsExpanded = true;
                for (int i = 1; i < _accordion.Count; i++)
                    _accordion[i].IsExpanded = false;
            };
        }
        /// <summary>
        /// Метод Accordion_Expanding выполняет основную операцию класса.
        /// </summary>

        private void Accordion_Expanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            foreach (var ex in _accordion)
                if (!ReferenceEquals(ex, sender))
                    ex.IsExpanded = false;
        }
    }
}