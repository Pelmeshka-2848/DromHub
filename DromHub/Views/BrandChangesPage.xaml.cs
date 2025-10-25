using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using DromHub.ViewModels;

namespace DromHub.Views
{
    /// <summary>
    /// <para>Обеспечивает отображение журнала изменений бренда в WinUI-странице, настраивая привязки и жизненный цикл модели представления.</para>
    /// <para>Используется администраторами для анализа аудита и проксирует вызовы к <see cref="BrandChangesViewModel"/>.</para>
    /// <para>Не содержит бизнес-логики фильтрации; отвечает только за навигационные сценарии и DI.</para>
    /// </summary>
    /// <remarks>
    /// Потокобезопасность: экземпляр используется строго в UI-потоке навигационного фрейма WinUI.
    /// Побочные эффекты: запрашивает зависимости из <see cref="App.ServiceProvider"/> и инициирует асинхронные загрузки данных.
    /// Сложность типичных операций: O(1) при навигации, так как делегирует работу модели.
    /// </remarks>
    public sealed partial class BrandChangesPage : Page
    {
        /// <summary>
        /// <para>Предоставляет модель представления, с которой связаны элементы интерфейса страницы.</para>
        /// <para>Используется кодом-защитой и XAML-привязками для обращения к состоянию и командам аудита.</para>
        /// <para>Создается один раз за жизнь страницы и повторно используется при повторных навигациях.</para>
        /// </summary>
        /// <value>Экземпляр <see cref="BrandChangesViewModel"/> из контейнера зависимостей; не бывает <see langword="null"/>.</value>
        /// <remarks>Потокобезопасность: доступ только из UI-потока.</remarks>
        public BrandChangesViewModel VM { get; }

        /// <summary>
        /// <para>Инициализирует страницу, подключая модель представления из контейнера и устанавливая контекст данных.</para>
        /// <para>Используйте стандартной навигацией WinUI; конструктор не выполняет тяжелых операций.</para>
        /// <para>Необходим для корректной работы XAML-привязок и команд.</para>
        /// </summary>
        /// <remarks>
        /// Побочные эффекты: обращается к <see cref="App.ServiceProvider"/> для разрешения зависимостей.
        /// Потокобезопасность: вызывать в UI-потоке, как и любой конструктор страницы WinUI.
        /// </remarks>
        public BrandChangesPage()
        {
            InitializeComponent();
            VM = App.ServiceProvider.GetRequiredService<BrandChangesViewModel>();
            DataContext = VM;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Дополнительно к базовой реализации запускает загрузку аудита при наличии идентификатора бренда и очищает состояние иначе.
        /// </remarks>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Guid id && id != Guid.Empty)
            {
                await VM.InitializeAsync(id);
            }
            else
            {
                VM.ResetState("BrandId не передан — изменений нет.");
            }
        }
    }
}
