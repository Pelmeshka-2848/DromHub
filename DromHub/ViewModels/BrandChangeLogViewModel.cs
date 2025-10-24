using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DromHub.Models;
using DromHub.Services;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DromHub.ViewModels
{
    /// <summary>
    /// Управляет жизненным циклом загрузки, отмены и представления истории изменений бренда.
    /// Используйте экземпляр в рамках страницы патчноутов, чтобы собирать данные из <see cref="ChangeLogService"/>
    /// и структурировать их в группы, пригодные для визуализации в WinUI.
    /// Класс координирует асинхронные запросы, гарантируя сброс состояния при смене бренда и корректную работу Empty State.
    /// </summary>
    /// <remarks>
    /// Потокобезопасность: экземпляр не потокобезопасен; все вызовы выполняйте из UI-потока.
    /// Побочные эффекты: обращается к БД через <see cref="ChangeLogService"/>, создаёт и отменяет <see cref="CancellationTokenSource"/>.
    /// Сложность типичных операций: загрузка истории O(n), где n — количество записей в патче.
    /// См. также: <see cref="ChangeLogPatchGroup"/>, <see cref="ChangeLogSectionGroup"/>, <see cref="ChangeLogEntryItem"/>.
    /// </remarks>
    public sealed partial class BrandChangeLogViewModel : ObservableObject
    {
        /// <summary>
        /// Хранит сервис истории изменений, чтобы избегать повторного разрешения зависимостей и обеспечить единый источник данных.
        /// </summary>
        private readonly ChangeLogService _changeLogService;

        /// <summary>
        /// Управляет отменой текущей операции загрузки, когда пользователь переключает бренд или покидает страницу.
        /// </summary>
        private CancellationTokenSource? _loadCts;

        /// <summary>
        /// Коллекция патчей для привязки в UI; перезаписывается при каждом обновлении истории.
        /// </summary>
        private readonly ObservableCollection<ChangeLogPatchGroup> _patches;

        /// <summary>
        /// Идентификатор бренда, для которого актуально текущее состояние представления.
        /// </summary>
        private Guid _currentBrandId;

        /// <summary>
        /// Отражает, выполняется ли в данный момент асинхронная операция загрузки истории.
        /// </summary>
        private bool _isLoading;

        /// <summary>
        /// Показывает, что история содержит хотя бы один патч после последней загрузки.
        /// </summary>
        private bool _hasVisibleHistory;

        /// <summary>
        /// Текст сообщения для пустого состояния; допускает локализацию и переопределение из XAML.
        /// </summary>
        private string _emptyStateMessage = "Для бренда пока нет зафиксированных изменений.";

        /// <summary>
        /// Создаёт view-model и инициализирует команды и коллекции.
        /// </summary>
        /// <param name="changeLogService">Экземпляр сервиса истории; не должен быть <see langword="null"/>.</param>
        /// <exception cref="ArgumentNullException">Возникает, когда <paramref name="changeLogService"/> не предоставлен.</exception>
        /// <remarks>
        /// Предусловия: сервис истории предварительно зарегистрирован в DI и готов к использованию.
        /// Побочные эффекты: создаёт команду <see cref="LoadCommand"/> и коллекцию <see cref="Patches"/>.
        /// Потокобезопасность: инициализация должна выполняться в UI-потоке, если далее используется привязка.
        /// </remarks>
        /// <example>
        /// <code>
        /// var viewModel = new BrandChangeLogViewModel(changeLogService);
        /// await viewModel.LoadAsync(brandId);
        /// </code>
        /// </example>
        public BrandChangeLogViewModel(ChangeLogService changeLogService)
        {
            _changeLogService = changeLogService ?? throw new ArgumentNullException(nameof(changeLogService));
            _patches = new ObservableCollection<ChangeLogPatchGroup>();
            LoadCommand = new AsyncRelayCommand<Guid>(LoadAsync);
        }

        /// <summary>
        /// Предоставляет изменяемую коллекцию патчей для XAML-привязки; коллекция обновляется на месте, чтобы не нарушать биндинги.
        /// </summary>
        /// <value>Набор патчей в порядке убывания даты выпуска; по умолчанию — пустой.</value>
        /// <remarks>
        /// Побочные эффекты: при обновлении истории коллекция очищается и наполняется заново.
        /// Потокобезопасность: использовать только в UI-потоке.
        /// </remarks>
        public ObservableCollection<ChangeLogPatchGroup> Patches => _patches;

        /// <summary>
        /// Команда, инициирующая асинхронную загрузку истории бренда; совместима с XAML кнопками и событиями навигации.
        /// </summary>
        /// <value>Экземпляр <see cref="IAsyncRelayCommand{T}"/>; всегда инициализирован.</value>
        /// <remarks>
        /// Побочные эффекты: вызывает <see cref="LoadAsync(Guid)"/>.
        /// Потокобезопасность: вызывать из UI-потока.
        /// </remarks>
        public IAsyncRelayCommand<Guid> LoadCommand { get; }

        /// <summary>
        /// Текущий идентификатор бренда, для которого показана история изменений.
        /// </summary>
        /// <value>Не пустой <see cref="Guid"/> после успешной загрузки; <see cref="Guid.Empty"/> — если состояние сброшено.</value>
        /// <remarks>
        /// Предусловия: устанавливается только методами view-model; внешний код должен вызывать <see cref="LoadAsync(Guid)"/>.
        /// Потокобезопасность: обращаться из UI-потока.
        /// </remarks>
        public Guid CurrentBrandId
        {
            get => _currentBrandId;
            private set => SetProperty(ref _currentBrandId, value);
        }

        /// <summary>
        /// Показывает, выполняется ли загрузка; пригодно для отображения индикатора прогресса.
        /// </summary>
        /// <value><see langword="true"/>, если операция в процессе; иначе — <see langword="false"/>.</value>
        /// <remarks>
        /// Потокобезопасность: изменяется только из UI-потока.
        /// </remarks>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Информирует представление о наличии истории и позволяет переключать Empty State.
        /// </summary>
        /// <value><see langword="true"/>, если список патчей не пуст; иначе — <see langword="false"/>.</value>
        /// <remarks>
        /// Потокобезопасность: изменяется только из UI-потока.
        /// </remarks>
        public bool HasVisibleHistory
        {
            get => _hasVisibleHistory;
            private set => SetProperty(ref _hasVisibleHistory, value);
        }

        /// <summary>
        /// Сообщение, отображаемое при отсутствии патчноутов.
        /// </summary>
        /// <value>Строка для пользовательского интерфейса; по умолчанию содержит локализованный текст.</value>
        /// <remarks>
        /// Потокобезопасность: устанавливать из UI-потока.
        /// Nullability: значение не должно быть <see langword="null"/> или пустой строкой.
        /// </remarks>
        public string EmptyStateMessage
        {
            get => _emptyStateMessage;
            set => SetProperty(ref _emptyStateMessage, string.IsNullOrWhiteSpace(value)
                ? "Для бренда пока нет зафиксированных изменений."
                : value);
        }

        /// <summary>
        /// Асинхронно загружает историю изменений указанного бренда, поддерживая отмену и сброс состояния.
        /// </summary>
        /// <param name="id">Идентификатор бренда; если передан <see cref="Guid.Empty"/>, состояние очищается.</param>
        /// <returns>Задача, завершающаяся после обновления коллекции <see cref="Patches"/>.</returns>
        /// <remarks>
        /// Предусловия: <see cref="ChangeLogService"/> зарегистрирован и доступен.
        /// Постусловия: <see cref="Patches"/> заполнена данными для бренда либо очищена.
        /// Идемпотентность: повторный вызов с тем же идентификатором обновляет данные и не дублирует записи.
        /// Отмена: повторный вызов отменяет предыдущий через <see cref="CancellationTokenSource"/>.
        /// Потокобезопасность: вызывать из UI-потока.
        /// </remarks>
        /// <example>
        /// <code>
        /// await viewModel.LoadAsync(navigationBrandId);
        /// if (!viewModel.HasVisibleHistory)
        /// {
        ///     // Показать заглушку Empty State
        /// }
        /// </code>
        /// </example>
        public async Task LoadAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                Reset();
                return;
            }

            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();

            CurrentBrandId = id;
            IsLoading = true;

            try
            {
                var history = await _changeLogService.GetBrandHistoryAsync(id, _loadCts.Token);
                ApplyHistory(history);
            }
            catch (OperationCanceledException)
            {
                // подавляем исключение, чтобы не шуметь в UI при намеренной отмене
            }
            finally
            {
                IsLoading = false;
                _loadCts?.Dispose();
                _loadCts = null;
            }
        }

        /// <summary>
        /// Применяет результат загрузки, преобразуя доменные модели в представления для XAML.
        /// </summary>
        /// <param name="patches">Коллекция патчей из <see cref="ChangeLogService"/>; допускается пустая.</param>
        /// <remarks>
        /// Предусловия: коллекция не <see langword="null"/>.
        /// Постусловия: <see cref="Patches"/> соответствует переданным данным.
        /// Побочные эффекты: очищает и заполняет коллекцию <see cref="Patches"/>.
        /// Потокобезопасность: вызывать из UI-потока.
        /// </remarks>
        /// <example>
        /// <code>
        /// var history = await _changeLogService.GetBrandHistoryAsync(id, token);
        /// ApplyHistory(history);
        /// </code>
        /// </example>
        private void ApplyHistory(IReadOnlyList<ChangeLogPatchResult> patches)
        {
            if (patches is null)
            {
                throw new ArgumentNullException(nameof(patches));
            }

            _patches.Clear();

            foreach (var patch in patches)
            {
                var patchVm = new ChangeLogPatchGroup(
                    patch.PatchId,
                    patch.Version,
                    patch.Title,
                    patch.ReleaseDate,
                    patch.Sections
                        .Select(section => new ChangeLogSectionGroup(
                            section.SectionId,
                            section.Title,
                            section.Category,
                            section.Entries
                                .Select(entry => new ChangeLogEntryItem(
                                    entry.EntryId,
                                    entry.Headline,
                                    entry.Description,
                                    entry.ImpactLevel,
                                    entry.IconAsset,
                                    entry.BrandName,
                                    entry.PartName,
                                    entry.PartCatalogNumber))
                                .ToList()))
                        .ToList());

                _patches.Add(patchVm);
            }

            HasVisibleHistory = _patches.Count > 0;
        }

        /// <summary>
        /// Сбрасывает состояние view-model, отменяя незавершённую загрузку и очищая коллекцию патчей.
        /// </summary>
        /// <remarks>
        /// Постусловия: <see cref="CurrentBrandId"/> становится <see cref="Guid.Empty"/>, <see cref="Patches"/> очищена, <see cref="HasVisibleHistory"/> равно <see langword="false"/>.
        /// Побочные эффекты: отменяет текущий <see cref="CancellationTokenSource"/>.
        /// Потокобезопасность: вызывать из UI-потока.
        /// </remarks>
        /// <example>
        /// <code>
        /// viewModel.Reset();
        /// </code>
        /// </example>
        public void Reset()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = null;

            CurrentBrandId = Guid.Empty;
            _patches.Clear();
            HasVisibleHistory = false;
        }
    }

    /// <summary>
    /// Представляет патч для XAML-привязки, объединяя метаданные и коллекцию разделов.
    /// Используется для отображения шапки патчноута и группировки секций.
    /// Не отвечает за загрузку данных и изменения содержимого после инициализации.
    /// </summary>
    /// <remarks>
    /// Потокобезопасность: экземпляр неизменяемый после создания.
    /// Побочные эффекты: отсутствуют.
    /// Сложность типичных операций: получение строковых представлений O(1).
    /// См. также: <see cref="ChangeLogSectionGroup"/>.
    /// </remarks>
    public sealed class ChangeLogPatchGroup
    {
        /// <summary>
        /// Российская культура для вывода дат в формате патчноутов.
        /// </summary>
        private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

        /// <summary>
        /// Создаёт представление патча для UI.
        /// </summary>
        /// <param name="id">Идентификатор патча.</param>
        /// <param name="version">Строковое обозначение версии; отображается в шапке.</param>
        /// <param name="title">Дополнительный заголовок; может быть <see langword="null"/>.</param>
        /// <param name="releaseDate">Дата релиза патча.</param>
        /// <param name="sections">Коллекция разделов; не должна быть <see langword="null"/>.</param>
        /// <exception cref="ArgumentNullException">Возникает, если <paramref name="version"/> или <paramref name="sections"/> не заданы.</exception>
        /// <remarks>
        /// Предусловия: список разделов уже отсортирован требуемым образом.
        /// Постусловия: свойства класса отражают переданные значения.
        /// Потокобезопасность: экземпляр безопасен для чтения из разных потоков.
        /// </remarks>
        /// <example>
        /// <code>
        /// var patchGroup = new ChangeLogPatchGroup(id, "1.2.0", "Весеннее обновление", DateTime.Today, sections);
        /// </code>
        /// </example>
        public ChangeLogPatchGroup(Guid id, string version, string? title, DateTime releaseDate, IList<ChangeLogSectionGroup> sections)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentNullException(nameof(version));
            }

            Sections = new ReadOnlyCollection<ChangeLogSectionGroup>(sections ?? throw new ArgumentNullException(nameof(sections)));
            PatchId = id;
            Version = version;
            Title = title;
            ReleaseDate = releaseDate;
        }

        /// <summary>
        /// Уникальный идентификатор патча.
        /// </summary>
        /// <value>Значение <see cref="Guid"/> из доменной модели.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public Guid PatchId { get; }

        /// <summary>
        /// Строковое обозначение версии патча.
        /// </summary>
        /// <value>Неформатированная строка версии (например, «1.3.5»).</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public string Version { get; }

        /// <summary>
        /// Дополнительный заголовок патча.
        /// </summary>
        /// <value>Может быть <see langword="null"/> или пустым, если заголовок не задан.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public string? Title { get; }

        /// <summary>
        /// Дата выхода патча.
        /// </summary>
        /// <value>Дата в часовом поясе сервера; предполагается отображение в локализованном формате.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public DateTime ReleaseDate { get; }

        /// <summary>
        /// Коллекция разделов патча, предназначенных для визуализации.
        /// </summary>
        /// <value>Неизменяемый список разделов.</value>
        /// <remarks>
        /// Потокобезопасность: список неизменяем; безопасен для многопоточного чтения.
        /// </remarks>
        public IReadOnlyList<ChangeLogSectionGroup> Sections { get; }

        /// <summary>
        /// Отформатированная строка для отображения версии и заголовка.
        /// </summary>
        /// <value>Версия либо сочетание «версия — заголовок», если заголовок задан.</value>
        /// <remarks>
        /// Потокобезопасность: вычисляется на лету и не изменяет состояние.
        /// </remarks>
        public string HeaderDisplay => string.IsNullOrWhiteSpace(Title) ? Version : $"{Version} — {Title}";

        /// <summary>
        /// Локализованная дата релиза для вывода в UI.
        /// </summary>
        /// <value>Строка в формате «dd MMMM yyyy».</value>
        /// <remarks>
        /// Потокобезопасность: вычисляется на лету и не изменяет состояние.
        /// </remarks>
        public string ReleaseDateDisplay => ReleaseDate.ToString("dd MMMM yyyy", RuCulture);
    }

    /// <summary>
    /// Представляет раздел патча с категорией, акцентным цветом и списком записей.
    /// Класс обеспечивает согласованные подписи категорий и их визуальные цвета.
    /// </summary>
    /// <remarks>
    /// Потокобезопасность: экземпляр неизменяем после конструирования.
    /// Побочные эффекты: создание кистей <see cref="SolidColorBrush"/>.
    /// См. также: <see cref="ChangeLogEntryItem"/>.
    /// </remarks>
    public sealed class ChangeLogSectionGroup
    {
        /// <summary>
        /// Сопоставление категорий с отображаемыми именами и кистями, чтобы единообразно подсвечивать разделы.
        /// </summary>
        private static readonly IReadOnlyDictionary<ChangeLogCategory, (string Name, SolidColorBrush Brush)> CategoryMap =
            new Dictionary<ChangeLogCategory, (string, SolidColorBrush)>
            {
                [ChangeLogCategory.Brand] = ("Бренд", CreateBrush("#FF4C7CF3")),
                [ChangeLogCategory.Parts] = ("Детали", CreateBrush("#FF53C678")),
                [ChangeLogCategory.Pricing] = ("Цены", CreateBrush("#FFE5A323")),
                [ChangeLogCategory.General] = ("Общее", CreateBrush("#FF7F8C8D")),
                [ChangeLogCategory.Logistics] = ("Логистика", CreateBrush("#FF36C2D8"))
            };

        /// <summary>
        /// Создаёт раздел патча и подбирает визуальные атрибуты для категории.
        /// </summary>
        /// <param name="id">Идентификатор раздела.</param>
        /// <param name="title">Заголовок раздела; не должен быть пустым.</param>
        /// <param name="category">Категория изменения; определяет визуальный стиль.</param>
        /// <param name="entries">Список записей; не должен быть <see langword="null"/>.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="title"/> или <paramref name="entries"/> не заданы.</exception>
        /// <remarks>
        /// Предусловия: список записей уже подготовлен и отсортирован.
        /// Постусловия: свойства раздела доступны для привязки.
        /// Побочные эффекты: создание кистей через <see cref="CreateBrush(string)"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// var section = new ChangeLogSectionGroup(id, "Корректировки цен", ChangeLogCategory.Pricing, entries);
        /// </code>
        /// </example>
        public ChangeLogSectionGroup(Guid id, string title, ChangeLogCategory category, IList<ChangeLogEntryItem> entries)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentNullException(nameof(title));
            }

            Entries = new ReadOnlyCollection<ChangeLogEntryItem>(entries ?? throw new ArgumentNullException(nameof(entries)));
            SectionId = id;
            Title = title;
            Category = category;

            if (!CategoryMap.TryGetValue(category, out var info))
            {
                info = (category.ToString(), CreateBrush("#FF7F8C8D"));
            }

            CategoryDisplay = info.Name;
            AccentBrush = info.Brush;
        }

        /// <summary>
        /// Уникальный идентификатор раздела.
        /// </summary>
        /// <value>Значение <see cref="Guid"/> из БД.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public Guid SectionId { get; }

        /// <summary>
        /// Заголовок раздела для отображения.
        /// </summary>
        /// <value>Непустая строка.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public string Title { get; }

        /// <summary>
        /// Категория, используемая для группировки и визуализации.
        /// </summary>
        /// <value>Одно из значений <see cref="ChangeLogCategory"/>.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public ChangeLogCategory Category { get; }

        /// <summary>
        /// Записи, входящие в раздел.
        /// </summary>
        /// <value>Неизменяемая коллекция записей патча.</value>
        /// <remarks>
        /// Потокобезопасность: коллекция неизменяема и безопасна для многопоточного чтения.
        /// </remarks>
        public IReadOnlyList<ChangeLogEntryItem> Entries { get; }

        /// <summary>
        /// Отображаемое имя категории на русском языке.
        /// </summary>
        /// <value>Локализованная подпись категории.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public string CategoryDisplay { get; }

        /// <summary>
        /// Акцентная кисть для визуализации раздела.
        /// </summary>
        /// <value><see cref="SolidColorBrush"/>, совместимый с WinUI.</value>
        /// <remarks>
        /// Потокобезопасность: использовать только в UI-потоке из-за требований WinUI к кистям.
        /// </remarks>
        public SolidColorBrush AccentBrush { get; }

        /// <summary>
        /// Создаёт кисть из HEX-представления цвета.
        /// </summary>
        /// <param name="hex">Цвет в формате «#AARRGGBB» или «#RRGGBB».</param>
        /// <returns>Экземпляр <see cref="SolidColorBrush"/> с указанным цветом.</returns>
        /// <remarks>
        /// Предусловия: строка не <see langword="null"/>.
        /// Потокобезопасность: безопасно вызывать из разных потоков.
        /// </remarks>
        private static SolidColorBrush CreateBrush(string hex)
        {
            var color = ParseColor(hex);
            return new SolidColorBrush(color);
        }

        /// <summary>
        /// Преобразует HEX-представление в структуру <see cref="Windows.UI.Color"/>.
        /// </summary>
        /// <param name="hex">Строка HEX; допускается с альфа-каналом.</param>
        /// <returns>Цвет <see cref="Windows.UI.Color"/>; при ошибке форматирования возвращает серый.</returns>
        /// <remarks>
        /// Предусловия: строка не <see langword="null"/>; пустая строка трактуется как серый цвет.
        /// Потокобезопасность: безопасно вызывать из разных потоков.
        /// </remarks>
        private static Color ParseColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return Color.FromArgb(255, 128, 128, 128);
            }

            var span = hex.AsSpan().TrimStart('#');
            byte a = 255;
            int idx = 0;

            if (span.Length == 8)
            {
                a = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);
                idx += 2;
            }

            var r = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);
            idx += 2;
            var g = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);
            idx += 2;
            var b = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);

            return Color.FromArgb(a, r, g, b);
        }
    }

    /// <summary>
    /// Представляет отдельную запись патчноута с вычисленными подписями и визуальными индикаторами важности.
    /// Обеспечивает готовые к привязке свойства для иконок, текста и связанной сущности.
    /// </summary>
    /// <remarks>
    /// Потокобезопасность: экземпляр неизменяем после создания.
    /// Побочные эффекты: создание кистей для уровней влияния.
    /// См. также: <see cref="ChangeLogImpactLevel"/>.
    /// </remarks>
    public sealed class ChangeLogEntryItem
    {
        /// <summary>
        /// Словарь подписей и кистей для уровней влияния, обеспечивающий единообразие в UI.
        /// </summary>
        private static readonly IReadOnlyDictionary<ChangeLogImpactLevel, (string Label, SolidColorBrush Brush)> ImpactMap =
            new Dictionary<ChangeLogImpactLevel, (string, SolidColorBrush)>
            {
                [ChangeLogImpactLevel.Low] = ("Низкий", CreateBrush("#FF5DADE2")),
                [ChangeLogImpactLevel.Medium] = ("Средний", CreateBrush("#FFF1C40F")),
                [ChangeLogImpactLevel.High] = ("Высокий", CreateBrush("#FFE74C3C")),
                [ChangeLogImpactLevel.Critical] = ("Критичный", CreateBrush("#FF922B21"))
            };

        /// <summary>
        /// Создаёт запись патчноута с вычислением ярлыков и иконок.
        /// </summary>
        /// <param name="id">Идентификатор записи.</param>
        /// <param name="headline">Краткий заголовок; при отсутствии подставляется дефолтный.</param>
        /// <param name="description">Подробное описание изменения; не должно быть пустым.</param>
        /// <param name="impactLevel">Уровень влияния изменения.</param>
        /// <param name="iconAsset">Путь к SVG-иконке; допускается <see langword="null"/>.</param>
        /// <param name="brandName">Имя бренда, которого касается изменение; может быть <see langword="null"/>.</param>
        /// <param name="partName">Название детали; может быть <see langword="null"/>.</param>
        /// <param name="partCatalog">Каталожный номер детали; может быть <see langword="null"/>.</param>
        /// <exception cref="ArgumentNullException">Возникает, если <paramref name="description"/> не задан.</exception>
        /// <remarks>
        /// Предусловия: данные получены из валидационного слоя и корректны.
        /// Постусловия: свойства объекта отражают доменную модель.
        /// Побочные эффекты: создаёт кисть для уровня влияния.
        /// </remarks>
        /// <example>
        /// <code>
        /// var entry = new ChangeLogEntryItem(entryId, "Обновлены цены", "Цена снижена на 5%", ChangeLogImpactLevel.Medium, "/Assets/price.svg", "BrandX", "Фара", "FR-001");
        /// </code>
        /// </example>
        public ChangeLogEntryItem(
            Guid id,
            string? headline,
            string description,
            ChangeLogImpactLevel impactLevel,
            string? iconAsset,
            string? brandName,
            string? partName,
            string? partCatalog)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentNullException(nameof(description));
            }

            EntryId = id;
            Headline = string.IsNullOrWhiteSpace(headline) ? "Обновление" : headline;
            Description = description;
            ImpactLevel = impactLevel;
            IconAsset = string.IsNullOrWhiteSpace(iconAsset) ? "/Assets/info.svg" : iconAsset;
            BrandName = brandName;
            PartName = partName;
            PartCatalogNumber = partCatalog;

            if (!ImpactMap.TryGetValue(impactLevel, out var info))
            {
                info = (impactLevel.ToString(), CreateBrush("#FF5DADE2"));
            }

            ImpactLabel = info.Label;
            ImpactBrush = info.Brush;
        }

        /// <summary>
        /// Уникальный идентификатор записи.
        /// </summary>
        /// <value>Значение <see cref="Guid"/>.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public Guid EntryId { get; }

        /// <summary>
        /// Заголовок записи, пригодный для отображения списком.
        /// </summary>
        /// <value>Нестандартный или дефолтный заголовок.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public string Headline { get; }

        /// <summary>
        /// Детальное описание изменения.
        /// </summary>
        /// <value>Непустая строка с HTML-независимым описанием.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public string Description { get; }

        /// <summary>
        /// Уровень влияния изменения.
        /// </summary>
        /// <value>Одно из значений <see cref="ChangeLogImpactLevel"/>.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public ChangeLogImpactLevel ImpactLevel { get; }

        /// <summary>
        /// Путь к иконке, используемой для визуального обозначения записи.
        /// </summary>
        /// <value>Путь внутри пакета приложения; при необходимости нормализуется.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public string IconAsset { get; }

        /// <summary>
        /// Имя бренда, на который влияет запись.
        /// </summary>
        /// <value>Может быть <see langword="null"/>.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public string? BrandName { get; }

        /// <summary>
        /// Имя детали, затронутой изменением.
        /// </summary>
        /// <value>Может быть <see langword="null"/>.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public string? PartName { get; }

        /// <summary>
        /// Каталожный номер детали, если доступен.
        /// </summary>
        /// <value>Может быть <see langword="null"/>.</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public string? PartCatalogNumber { get; }

        /// <summary>
        /// Локализованная подпись уровня влияния.
        /// </summary>
        /// <value>Строка вроде «Высокий».</value>
        /// <remarks>
        /// Потокобезопасность: доступно только для чтения из любых потоков.
        /// </remarks>
        public string ImpactLabel { get; }

        /// <summary>
        /// Кисть, визуализирующая уровень влияния.
        /// </summary>
        /// <value>Экземпляр <see cref="SolidColorBrush"/>.</value>
        /// <remarks>
        /// Потокобезопасность: используйте только в UI-потоке из-за требований WinUI к кистям.
        /// </remarks>
        public SolidColorBrush ImpactBrush { get; }

        /// <summary>
        /// Полный URI иконки с префиксом <c>ms-appx:///</c>, готовый к привязке.
        /// </summary>
        /// <value>Строка с абсолютным URI.</value>
        /// <remarks>
        /// Потокобезопасность: вычисляется на лету и не изменяет состояние.
        /// </remarks>
        public string IconAssetUri => IconAsset.StartsWith("ms-appx", StringComparison.OrdinalIgnoreCase)
            ? IconAsset
            : $"ms-appx:///{IconAsset.TrimStart('/')}";

        /// <summary>
        /// Краткое описание сущностей, затронутых изменением.
        /// </summary>
        /// <value>Строка с именем бренда и/или детали либо <see langword="null"/>.</value>
        /// <remarks>
        /// Потокобезопасность: вычисляется на лету и не изменяет состояние.
        /// </remarks>
        public string? TargetSummary
        {
            get
            {
                var parts = new List<string>();

                if (!string.IsNullOrWhiteSpace(BrandName))
                {
                    parts.Add(BrandName);
                }

                if (!string.IsNullOrWhiteSpace(PartName))
                {
                    var catalog = string.IsNullOrWhiteSpace(PartCatalogNumber) ? string.Empty : $" ({PartCatalogNumber})";
                    parts.Add($"{PartName}{catalog}");
                }

                return parts.Count == 0 ? null : string.Join(" • ", parts);
            }
        }

        /// <summary>
        /// Создаёт кисть по HEX-коду цвета для отображения уровня влияния.
        /// </summary>
        /// <param name="hex">HEX-значение цвета.</param>
        /// <returns>Экземпляр <see cref="SolidColorBrush"/>.</returns>
        /// <remarks>
        /// Потокобезопасность: безопасно вызывать из разных потоков.
        /// </remarks>
        private static SolidColorBrush CreateBrush(string hex) => new SolidColorBrush(ParseColor(hex));

        /// <summary>
        /// Преобразует HEX-представление в цвет <see cref="Windows.UI.Color"/>.
        /// </summary>
        /// <param name="hex">Строка в формате «#AARRGGBB» или «#RRGGBB».</param>
        /// <returns>Цвет для заполнения кисти.</returns>
        /// <remarks>
        /// Потокобезопасность: безопасно вызывать из разных потоков.
        /// </remarks>
        private static Color ParseColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return Color.FromArgb(255, 128, 128, 128);
            }

            var span = hex.AsSpan().TrimStart('#');
            byte a = 255;
            int idx = 0;

            if (span.Length == 8)
            {
                a = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);
                idx += 2;
            }

            var r = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);
            idx += 2;
            var g = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);
            idx += 2;
            var b = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);

            return Color.FromArgb(a, r, g, b);
        }
    }
}
