using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DromHub.Models;
using DromHub.Services;

namespace DromHub.ViewModels;

/// <summary>
/// <para>Управляет состоянием экрана истории изменений запчасти, объединяя фильтры, пагинацию и команды обновления.</para>
/// <para>Используется страницей администратора для расследования правок и аудита, обращаясь к <see cref="PartAuditService"/> для получения данных.</para>
/// <para>Не выполняет запись и кеширование; каждый пересчёт фильтра инициирует новое чтение журнала.</para>
/// </summary>
/// <remarks>
/// Потокобезопасность: экземпляр предназначен для использования только в UI-потоке и не потокобезопасен.
/// Побочные эффекты: выполняет операции чтения через <see cref="PartAuditService"/> и обновляет коллекции UI.
/// Сложность типичных операций: O(n) относительно размера текущей страницы при загрузке.
/// См. также: <see cref="PartAuditService"/>.
/// </remarks>
public sealed class PartChangesViewModel : ObservableObject
{
    /// <summary>
    /// Сохраняет ссылку на сервис аудита для построения выдачи журнала изменений.
    /// </summary>
    private readonly PartAuditService _service;

    /// <summary>
    /// Инкапсулирует команду обновления данных, чтобы повторно использовать ее как источник <see cref="RefreshCommand"/>.
    /// </summary>
    private readonly AsyncRelayCommand _loadCommand;

    /// <summary>
    /// Инкапсулирует команду перехода на следующую страницу, чтобы управлять жизненным циклом CanExecute.
    /// </summary>
    private readonly AsyncRelayCommand _nextPageCommand;

    /// <summary>
    /// Инкапсулирует команду перехода на предыдущую страницу, обеспечивая централизованное управление доступностью.
    /// </summary>
    private readonly AsyncRelayCommand _prevPageCommand;

    /// <summary>
    /// Инкапсулирует команду очистки фильтров, чтобы синхронизировать доступность с состоянием загрузки.
    /// </summary>
    private readonly RelayCommand _clearFiltersCommand;

    /// <summary>
    /// Инкапсулирует команду выбора всех строк, обеспечивая централизованный контроль CanExecute.
    /// </summary>
    /// <example>
    /// <code>
    /// _selectAllCommand.Execute(null);
    /// </code>
    /// </example>
    private readonly RelayCommand _selectAllCommand;

    /// <summary>
    /// Инкапсулирует команду удаления выбранных записей аудита, объединяя проверки и асинхронный вызов сервиса.
    /// </summary>
    /// <example>
    /// <code>
    /// await _deleteSelectedCommand.ExecuteAsync(null);
    /// </code>
    /// </example>
    private readonly AsyncRelayCommand _deleteSelectedCommand;

    /// <summary>
    /// Хранит идентификатор детали, историю которой просматривает пользователь.
    /// </summary>
    private Guid _partId;

    /// <summary>
    /// Запоминает выбранную пользователем начальную дату фильтрации.
    /// </summary>
    private DateTimeOffset? _fromDate;

    /// <summary>
    /// Запоминает выбранную пользователем конечную дату фильтрации.
    /// </summary>
    private DateTimeOffset? _toDate;

    /// <summary>
    /// Хранит текущее значение фильтра по типу действия аудита.
    /// </summary>
    private AuditActionFilter _selectedAction = AuditActionFilter.All;

    /// <summary>
    /// Показывает, нужно ли ограничивать выдачу событиями с реальными изменениями полей.
    /// </summary>
    private bool _onlyChangedFields;

    /// <summary>
    /// Содержит поисковую подстроку, применяемую к JSON-представлениям записей.
    /// </summary>
    private string? _search;

    /// <summary>
    /// Указывает, сколько строк отображать на странице.
    /// </summary>
    private int _pageSize = 25;

    /// <summary>
    /// Фиксирует текущий индекс страницы для пагинации.
    /// </summary>
    private int _pageIndex;

    /// <summary>
    /// Содержит общее количество записей, доступных при заданных фильтрах.
    /// </summary>
    private int _totalCount;

    /// <summary>
    /// Показывает, выполняется ли в настоящий момент загрузка данных.
    /// </summary>
    private bool _isBusy;

    /// <summary>
    /// Содержит текст ошибки, отображаемый пользователю при сбоях загрузки.
    /// </summary>
    private string? _errorMessage;

    /// <summary>
    /// Фиксирует наличие выбранных пользователем строк, чтобы упрощать логику доступности команд.
    /// </summary>
    /// <example>
    /// <code>
    /// if (_hasSelection) { /* активировать дополнительный UI */ }
    /// </code>
    /// </example>
    private bool _hasSelection;

    /// <summary>
    /// Представляет информационную строку пагинации для отображения диапазона записей.
    /// </summary>
    private string _pageInfo = "Нет данных.";

    /// <summary>
    /// Фиксирует необходимость повторной загрузки после завершения текущей операции.
    /// </summary>
    private bool _pendingReload;

    /// <summary>
    /// Блокирует автоматический запуск перезагрузки при массовом изменении фильтров.
    /// </summary>
    private bool _suppressReload;

    /// <summary>
    /// Инициализирует модель представления зависимостью от <see cref="PartAuditService"/> и настраивает команды.
    /// </summary>
    /// <param name="service">Сервис чтения лога аудита запчастей; не допускает значение <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Возникает, когда <paramref name="service"/> не предоставлен контейнером.</exception>
    /// <remarks>
    /// Предусловия: контейнер внедрения зависимостей должен предоставить корректный экземпляр сервиса.
    /// Постусловия: коллекции и команды готовы к использованию страницей.
    /// Побочные эффекты: заполняет списки параметров фильтров.
    /// </remarks>
    /// <example>
    /// <code>
    /// var vm = new PartChangesViewModel(service);
    /// await vm.InitializeAsync(existingPartId);
    /// </code>
    /// </example>
    public PartChangesViewModel(PartAuditService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));

        Rows = new ObservableCollection<PartAuditRow>();
        PageSizes = new ObservableCollection<int>(new[] { 10, 25, 50, 100 });
        ActionItems = new ReadOnlyCollection<AuditActionFilter>(new[]
        {
            AuditActionFilter.All,
            AuditActionFilter.Insert,
            AuditActionFilter.Update,
            AuditActionFilter.Delete
        });

        _loadCommand = new AsyncRelayCommand(LoadInternalAsync, () => !IsBusy);
        _nextPageCommand = new AsyncRelayCommand(NextPageInternalAsync, CanGoNext);
        _prevPageCommand = new AsyncRelayCommand(PrevPageInternalAsync, CanGoPrevious);
        _clearFiltersCommand = new RelayCommand(ClearFilters, () => !IsBusy);
        _selectAllCommand = new RelayCommand(SelectAll, CanSelectAll);
        _deleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, CanDeleteSelected);
    }

    /// <summary>
    /// Возвращает коллекцию значений фильтра по действию, доступную элементу ComboBox.
    /// </summary>
    /// <value>Набор значений перечисления <see cref="AuditActionFilter"/> в фиксированном порядке.</value>
    /// <remarks>
    /// Коллекция иммутабельна и переиспользуется привязками.
    /// </remarks>
    public IReadOnlyList<AuditActionFilter> ActionItems { get; }

    /// <summary>
    /// Возвращает коллекцию доступных размеров страницы для выбора пользователем.
    /// </summary>
    /// <value>Редактируемая коллекция целых чисел; значения выражены в количестве строк.</value>
    /// <remarks>
    /// Изменение содержимого при необходимости отразится в UI автоматически.
    /// </remarks>
    public ObservableCollection<int> PageSizes { get; }

    /// <summary>
    /// Предоставляет последовательность строк аудита, привязанную к элементу списка.
    /// </summary>
    /// <value>Наблюдаемая коллекция, синхронизированная с результатами <see cref="PartAuditService"/>.</value>
    /// <remarks>
    /// Коллекция очищается и заполняется заново при каждой загрузке.
    /// </remarks>
    public ObservableCollection<PartAuditRow> Rows { get; }

    /// <summary>
    /// Представляет команду принудительного обновления данных журнала.
    /// </summary>
    /// <value>Экземпляр <see cref="IAsyncRelayCommand"/>, выполняющий запрос к сервису аудита.</value>
    /// <remarks>
    /// Команда отключена во время выполнения асинхронной загрузки.
    /// </remarks>
    public IAsyncRelayCommand RefreshCommand => _loadCommand;

    /// <summary>
    /// Представляет команду перехода на следующую страницу аудита.
    /// </summary>
    /// <value>Экземпляр <see cref="IAsyncRelayCommand"/>, изменяющий <see cref="PageIndex"/> и выполняющий повторную загрузку.</value>
    /// <remarks>
    /// Команда недоступна, когда текущая страница отображает последний диапазон записей.
    /// </remarks>
    public IAsyncRelayCommand NextPageCommand => _nextPageCommand;

    /// <summary>
    /// Представляет команду возврата на предыдущую страницу аудита.
    /// </summary>
    /// <value>Экземпляр <see cref="IAsyncRelayCommand"/>, уменьшающий <see cref="PageIndex"/>.</value>
    /// <remarks>
    /// Команда недоступна на первой странице.
    /// </remarks>
    public IAsyncRelayCommand PrevPageCommand => _prevPageCommand;

    /// <summary>
    /// <para>Представляет команду сброса фильтров к значениям по умолчанию, обеспечивая быстрый возврат к чистому состоянию.</para>
    /// <para>Применяйте при переходе между деталями или перед повторным поиском, чтобы исключить устаревшие параметры.</para>
    /// </summary>
    /// <value>Экземпляр <see cref="IRelayCommand"/>, который очищает даты, поиск и тип действия.</value>
    /// <remarks>
    /// Команда недоступна во время загрузки данных, чтобы избежать гонок состояния.
    /// Потокобезопасность: обращаться из UI-потока, поскольку реализация изменяет состояние модели.
    /// </remarks>
    public IRelayCommand ClearFiltersCommand => _clearFiltersCommand;

    /// <summary>
    /// <para>Предоставляет команду выделения всех записей текущей страницы для последующих пакетных операций.</para>
    /// <para>Удобна при массовом удалении технических записей, чтобы избежать ручного клика по каждой строке.</para>
    /// </summary>
    /// <value>Экземпляр <see cref="IRelayCommand"/>, отмечающий строки без перезагрузки данных.</value>
    /// <remarks>
    /// Команда недоступна, когда идет загрузка или на странице нет записей.
    /// </remarks>
    /// <example>
    /// <code>
    /// viewModel.SelectAllCommand.Execute(null);
    /// </code>
    /// </example>
    public IRelayCommand SelectAllCommand => _selectAllCommand;

    /// <summary>
    /// <para>Предоставляет команду удаления всех выбранных записей аудита из базы данных.</para>
    /// <para>Выполняет проверку наличия выбора и блокирует UI на время операции для консистентности.</para>
    /// </summary>
    /// <value>Экземпляр <see cref="IAsyncRelayCommand"/>, использующий <see cref="PartAuditService.DeleteAsync(Guid, IEnumerable{Guid}, CancellationToken)"/>.</value>
    /// <remarks>
    /// Команда недоступна при отсутствии выбора или активной фоновой операции.
    /// </remarks>
    /// <example>
    /// <code>
    /// await viewModel.DeleteSelectedCommand.ExecuteAsync(null);
    /// </code>
    /// </example>
    public IAsyncRelayCommand DeleteSelectedCommand => _deleteSelectedCommand;

    /// <summary>
    /// Возвращает или задает идентификатор детали, журнал изменений которой отображается.
    /// </summary>
    /// <value>GUID детали; значение по умолчанию — <see cref="Guid.Empty"/>, что означает отсутствие выбранной детали.</value>
    /// <remarks>
    /// Изменение свойства очищает текущие данные и требует повторной инициализации через <see cref="InitializeAsync(Guid)"/>.
    /// </remarks>
    public Guid PartId
    {
        get => _partId;
        private set => SetProperty(ref _partId, value);
    }

    /// <summary>
    /// Возвращает или задает начальную дату диапазона фильтрации.
    /// </summary>
    /// <value>Дата в локальной временной зоне; допускает <see langword="null"/> для отключения фильтра.</value>
    /// <remarks>
    /// Изменение автоматически перезагружает первую страницу журнала.
    /// </remarks>
    public DateTimeOffset? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                if (!_suppressReload)
                {
                    ScheduleReload(resetPage: true);
                }
            }
        }
    }

    /// <summary>
    /// Возвращает или задает конечную дату диапазона фильтрации.
    /// </summary>
    /// <value>Дата в локальной временной зоне; допускает <see langword="null"/>.</value>
    /// <remarks>
    /// Изменение свойства вызывает перезагрузку с возвратом на первую страницу.
    /// </remarks>
    public DateTimeOffset? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                if (!_suppressReload)
                {
                    ScheduleReload(resetPage: true);
                }
            }
        }
    }

    /// <summary>
    /// Возвращает или задает выбранный тип действия аудита.
    /// </summary>
    /// <value>Одно из значений <see cref="AuditActionFilter"/>; по умолчанию — <see cref="AuditActionFilter.All"/>.</value>
    /// <remarks>
    /// При изменении фильтра выполняется повторная загрузка первой страницы.
    /// </remarks>
    public AuditActionFilter SelectedAction
    {
        get => _selectedAction;
        set
        {
            if (SetProperty(ref _selectedAction, value))
            {
                if (!_suppressReload)
                {
                    ScheduleReload(resetPage: true);
                }
            }
        }
    }

    /// <summary>
    /// Возвращает или задает признак «только записи с измененными полями».
    /// </summary>
    /// <value><see langword="true"/>, если нужно показывать только события с заполненным списком столбцов.</value>
    /// <remarks>
    /// Фильтр применим только к событиям обновления; сервис обрабатывает остальное.
    /// </remarks>
    public bool OnlyChangedFields
    {
        get => _onlyChangedFields;
        set
        {
            if (SetProperty(ref _onlyChangedFields, value))
            {
                if (!_suppressReload)
                {
                    ScheduleReload(resetPage: true);
                }
            }
        }
    }

    /// <summary>
    /// Возвращает или задает поисковую строку для фильтрации данных.
    /// </summary>
    /// <value>Подстрока без ограничений по длине; пустая строка приравнивается к отсутствию фильтра.</value>
    /// <remarks>
    /// Поиск выполняется по текстовому представлению JSON-столбцов.
    /// </remarks>
    public string? Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value))
            {
                if (!_suppressReload)
                {
                    ScheduleReload(resetPage: true);
                }
            }
        }
    }

    /// <summary>
    /// Возвращает или задает размер страницы для запросов аудита.
    /// </summary>
    /// <value>Положительное целое число; значение ограничивается сервисом диапазоном [1; 200].</value>
    /// <remarks>
    /// При изменении размера страница сбрасывается на начало и выполняется повторная загрузка.
    /// </remarks>
    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (SetProperty(ref _pageSize, value))
            {
                PageIndex = 0;
                ScheduleReload(resetPage: false);
            }
        }
    }

    /// <summary>
    /// Возвращает текущий индекс страницы.
    /// </summary>
    /// <value>Ненегативное целое число; по умолчанию — 0.</value>
    /// <remarks>
    /// Свойство изменяется только внутренними командами, обеспечивая согласованность пагинации.
    /// </remarks>
    public int PageIndex
    {
        get => _pageIndex;
        private set
        {
            if (SetProperty(ref _pageIndex, value))
            {
                UpdatePaginationCommands();
            }
        }
    }

    /// <summary>
    /// Возвращает или задает общее количество записей, соответствующих текущим фильтрам.
    /// </summary>
    /// <value>Ненегативное целое число; значение 0 означает отсутствие данных.</value>
    /// <remarks>
    /// Обновляется после каждого обращения к сервису и влияет на команды пагинации.</remarks>
    public int TotalCount
    {
        get => _totalCount;
        private set
        {
            if (SetProperty(ref _totalCount, value))
            {
                UpdatePaginationCommands();
                UpdatePageInfo();
            }
        }
    }

    /// <summary>
    /// Показывает, выполняется ли сейчас загрузка.
    /// </summary>
    /// <value><see langword="true"/>, когда модель занята; по умолчанию — <see langword="false"/>.</value>
    /// <remarks>
    /// Состояние влияет на доступность команды обновления.
    /// </remarks>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _loadCommand.NotifyCanExecuteChanged();
                _clearFiltersCommand.NotifyCanExecuteChanged();
                _selectAllCommand.NotifyCanExecuteChanged();
                _deleteSelectedCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Возвращает или задает текст ошибки для отображения пользователю.
    /// </summary>
    /// <value>Локализованное сообщение или <see langword="null"/>, если ошибок нет.</value>
    /// <remarks>
    /// Значение очищается перед каждой новой загрузкой.
    /// </remarks>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// Показывает, выбраны ли какие-либо записи на текущей странице.
    /// </summary>
    /// <value><see langword="true"/>, если хотя бы одна строка помечена; иначе — <see langword="false"/>.</value>
    /// <remarks>
    /// Изменение свойства влияет на доступность команды удаления и может использоваться в XAML для визуальной индикации.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (viewModel.HasSelection) { /* отобразить кнопку */ }
    /// </code>
    /// </example>
    public bool HasSelection
    {
        get => _hasSelection;
        private set
        {
            if (SetProperty(ref _hasSelection, value))
            {
                _deleteSelectedCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Возвращает строку с информацией о текущем диапазоне записей.
    /// </summary>
    /// <value>Текст, например «Показаны 1-25 из 120» или «Нет записей».</value>
    /// <remarks>
    /// Значение автоматически обновляется при изменении коллекции данных или счетчиков.
    /// </remarks>
    public string PageInfo
    {
        get => _pageInfo;
        private set => SetProperty(ref _pageInfo, value);
    }

    /// <summary>
    /// <para>Сбрасывает состояние модели представления, когда страница теряет контекст детали или должна показать пустой экран.</para>
    /// <para>Используйте перед навигацией без идентификатора или после удаления детали, чтобы очистить коллекции и уведомить пользователя.</para>
    /// <para>Не запускает загрузку и тем самым предотвращает бессмысленные запросы к <see cref="PartAuditService"/>.</para>
    /// </summary>
    /// <param name="emptyStateMessage">Сообщение для пользователя; допускает <see langword="null"/> для использования стандартного текста.</param>
    /// <remarks>
    /// Предусловия: вызов допустим в любой момент, даже во время загрузки; метод отменяет отложенную перезагрузку.
    /// Постусловия: <see cref="PartId"/> равен <see cref="Guid.Empty"/>, коллекции очищены, команды возвращены в исходное состояние.
    /// Побочные эффекты: очищает привязанные коллекции и сбрасывает счетчики UI.
    /// Потокобезопасность: вызывать только из UI-потока.
    /// </remarks>
    /// <example>
    /// <code>
    /// viewModel.ResetState("Деталь не выбрана");
    /// // UI отображает пустые списки и пояснение для пользователя.
    /// </code>
    /// </example>
    public void ResetState(string? emptyStateMessage = null)
    {
        _pendingReload = false;
        IsBusy = false;
        PartId = Guid.Empty;
        DetachAllRowHandlers();
        Rows.Clear();
        TotalCount = 0;
        ErrorMessage = emptyStateMessage;
        PageInfo = string.IsNullOrWhiteSpace(emptyStateMessage) ? "Нет записей." : emptyStateMessage;
        HasSelection = false;
        _selectAllCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Выполняет первичную загрузку аудита для переданной детали.
    /// </summary>
    /// <param name="partId">Идентификатор детали; не допускается <see cref="Guid.Empty"/>.</param>
    /// <returns>Задача, завершающаяся после подготовки первой страницы журнала.</returns>
    /// <exception cref="ArgumentException">Выбрасывается, когда <paramref name="partId"/> равен <see cref="Guid.Empty"/>.</exception>
    /// <remarks>
    /// Предусловия: страница еще не инициализирована другой деталью.
    /// Постусловия: установлены фильтры по умолчанию и загружены первые данные.
    /// Побочные эффекты: выполняет обращение к базе данных через сервис.
    /// </remarks>
    /// <example>
    /// <code>
    /// await viewModel.InitializeAsync(partId);
    /// // Далее можно менять фильтры: viewModel.OnlyChangedFields = true;
    /// </code>
    /// </example>
    public async Task InitializeAsync(Guid partId)
    {
        if (partId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор детали не может быть пустым.", nameof(partId));
        }

        PartId = partId;
        PageIndex = 0;
        await _loadCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Строит объект фильтра и выполняет запрос сервиса аудита.
    /// </summary>
    /// <returns>Задача, завершающаяся после обновления коллекции <see cref="Rows"/>.</returns>
    /// <remarks>
    /// Предусловия: <see cref="PartId"/> задан и не равен <see cref="Guid.Empty"/>.
    /// Постусловия: <see cref="Rows"/>, <see cref="TotalCount"/> и <see cref="PageInfo"/> отражают актуальное состояние.
    /// Побочные эффекты: выполняет чтение из БД и обновляет наблюдаемые коллекции.
    /// Идемпотентность: повторные вызовы с неизменными фильтрами возвращают одинаковые данные.
    /// </remarks>
    private async Task LoadInternalAsync()
    {
        if (PartId == Guid.Empty)
        {
            DetachAllRowHandlers();
            Rows.Clear();
            TotalCount = 0;
            PageInfo = "Деталь не выбрана.";
            HasSelection = false;
            _selectAllCommand.NotifyCanExecuteChanged();
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var filter = BuildFilter();
            var (rows, total) = await _service.GetAsync(filter);

            DetachAllRowHandlers();
            Rows.Clear();
            foreach (var row in rows)
            {
                Rows.Add(row);
                AttachRowHandlers(row);
            }

            TotalCount = total;
            UpdateSelectionState();
            _selectAllCommand.NotifyCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Загрузка отменена.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;

            if (_pendingReload)
            {
                _pendingReload = false;
                _ = _loadCommand.ExecuteAsync(null);
            }
        }
    }

    /// <summary>
    /// <para>Сбрасывает пользовательские фильтры к значениям по умолчанию и инициирует обновление списка.</para>
    /// <para>Поддерживает повторяемость сценариев анализа, устраняя накопившиеся критерии поиска.</para>
    /// </summary>
    /// <remarks>
    /// Метод не выполняет действий, если загрузка уже идет, чтобы избежать двойных запросов.
    /// Побочные эффекты: очищает текст поиска и сбрасывает флаги фильтров.
    /// Потокобезопасность: вызывайте только из UI-потока.
    /// </remarks>
    private void ClearFilters()
    {
        if (IsBusy)
        {
            return;
        }

        _suppressReload = true;
        try
        {
            FromDate = null;
            ToDate = null;
            SelectedAction = AuditActionFilter.All;
            OnlyChangedFields = false;
            Search = null;
        }
        finally
        {
            _suppressReload = false;
        }

        ScheduleReload(resetPage: true);
    }

    /// <summary>
    /// <para>Отмечает все строки текущей страницы как выбранные, подготавливая их к пакетным действиям (например, удалению).</para>
    /// <para>Предназначен для сценариев, где требуется быстро выделить значительное число технических записей.</para>
    /// </summary>
    /// <remarks>
    /// Метод не инициирует перезагрузку данных и работает только с текущей страницей, сохраняя выбранные элементы при последующем удалении.
    /// Потокобезопасность: вызывать из UI-потока, поскольку происходит изменение свойств элементов коллекции.
    /// </remarks>
    /// <example>
    /// <code>
    /// viewModel.SelectAllCommand.Execute(null);
    /// </code>
    /// </example>
    private void SelectAll()
    {
        if (!CanSelectAll())
        {
            return;
        }

        foreach (var row in Rows)
        {
            row.IsSelected = true;
        }

        UpdateSelectionState();
    }

    /// <summary>
    /// <para>Проверяет, доступна ли команда «Выбрать всё» с учётом текущего состояния модели.</para>
    /// <para>Запрещает выбор, когда страница пуста, все записи уже отмечены или идёт фоновой запрос.</para>
    /// </summary>
    /// <returns><see langword="true"/>, если выбор всех записей допустим; иначе — <see langword="false"/>.</returns>
    /// <remarks>Сложность: O(n) из-за проверки наличия неотмеченных элементов.</remarks>
    /// <example>
    /// <code>
    /// var canSelect = viewModel.SelectAllCommand.CanExecute(null);
    /// </code>
    /// </example>
    private bool CanSelectAll() => !IsBusy && Rows.Count > 0 && Rows.Any(row => !row.IsSelected);

    /// <summary>
    /// <para>Удаляет выбранные записи аудита из хранилища и инициирует повторную загрузку данных страницы.</para>
    /// <para>Инкапсулирует всю логику проверки выбора, вызова сервиса и постобработки результата.</para>
    /// </summary>
    /// <returns>Асинхронная задача, завершающаяся после обновления данных или фиксации ошибки.</returns>
    /// <remarks>
    /// Предусловия: <see cref="HasSelection"/> должно быть <see langword="true"/>.
    /// Побочные эффекты: вызывает <see cref="PartAuditService.DeleteAsync(Guid, IEnumerable{Guid}, CancellationToken)"/>, что приводит к удалению строк из БД.
    /// Потокобезопасность: работать в UI-потоке; метод изменяет состояние модели и коллекций.
    /// </remarks>
    /// <example>
    /// <code>
    /// await viewModel.DeleteSelectedCommand.ExecuteAsync(null);
    /// </code>
    /// </example>
    private async Task DeleteSelectedAsync()
    {
        if (!CanDeleteSelected())
        {
            return;
        }

        var ids = Rows
            .Where(row => row.IsSelected)
            .Select(row => row.Id)
            .ToArray();

        if (ids.Length == 0)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        var removed = 0;

        try
        {
            removed = await _service.DeleteAsync(PartId, ids);

            if (removed == 0)
            {
                foreach (var row in Rows)
                {
                    row.IsSelected = false;
                }

                UpdateSelectionState();
            }
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Удаление отменено.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }

        if (removed > 0)
        {
            HasSelection = false;
            await _loadCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// <para>Определяет, можно ли выполнить удаление выбранных записей.</para>
    /// <para>Учитывает наличие выбора и отсутствие активных фоновых операций.</para>
    /// </summary>
    /// <returns><see langword="true"/>, если команда удаления должна быть активна.</returns>
    /// <example>
    /// <code>
    /// var canDelete = viewModel.DeleteSelectedCommand.CanExecute(null);
    /// </code>
    /// </example>
    private bool CanDeleteSelected() => HasSelection && !IsBusy;

    /// <summary>
    /// <para>Присоединяет обработчик событий изменения свойств для переданной строки аудита.</para>
    /// <para>Необходим для отслеживания выбора элементов и своевременного обновления команд.</para>
    /// </summary>
    /// <param name="row">Строка аудита; метод игнорирует <see langword="null"/>.</param>
    /// <remarks>Многократный вызов безопасен: обработчик повторно не добавляется благодаря предварительному отписыванию.</remarks>
    /// <example>
    /// <code>
    /// AttachRowHandlers(row);
    /// </code>
    /// </example>
    private void AttachRowHandlers(PartAuditRow row)
    {
        if (row is null)
        {
            return;
        }

        row.PropertyChanged -= OnRowPropertyChanged;
        row.PropertyChanged += OnRowPropertyChanged;
    }

    /// <summary>
    /// <para>Отсоединяет обработчики свойств от всех текущих строк, предотвращая утечки памяти при очистке коллекции.</para>
    /// <para>Используется перед полной заменой списка записей или при сбросе состояния.</para>
    /// </summary>
    /// <example>
    /// <code>
    /// DetachAllRowHandlers();
    /// </code>
    /// </example>
    private void DetachAllRowHandlers()
    {
        foreach (var row in Rows)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
        }
    }

    /// <summary>
    /// <para>Обновляет агрегированное состояние выбора при изменении свойств строк аудита.</para>
    /// <para>Триггерится обработчиком <see cref="INotifyPropertyChanged.PropertyChanged"/> каждой строки.</para>
    /// </summary>
    /// <param name="sender">Строка аудита, изменившая свойство.</param>
    /// <param name="e">Аргументы события, содержащие имя изменённого свойства.</param>
    /// <example>
    /// <code>
    /// OnRowPropertyChanged(row, new PropertyChangedEventArgs(nameof(PartAuditRow.IsSelected)));
    /// </code>
    /// </example>
    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PartAuditRow.IsSelected))
        {
            UpdateSelectionState();
        }
    }

    /// <summary>
    /// <para>Пересчитывает агрегированное состояние выбора и обновляет связанные команды.</para>
    /// <para>Используется после массовых операций или индивидуальных изменений чекбоксов.</para>
    /// </summary>
    /// <remarks>Сложность: O(n), где n — число строк на текущей странице.</remarks>
    /// <example>
    /// <code>
    /// UpdateSelectionState();
    /// </code>
    /// </example>
    private void UpdateSelectionState()
    {
        HasSelection = Rows.Any(row => row.IsSelected);
        _selectAllCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Создает объект фильтра на основе текущего состояния модели представления.
    /// </summary>
    /// <returns>Экземпляр <see cref="PartAuditFilter"/> с заполненными параметрами.</returns>
    /// <remarks>
    /// Конвертирует даты в границы суток и очищает пустые строки поиска.
    /// </remarks>
    private PartAuditFilter BuildFilter()
    {
        DateTime? from = FromDate?.Date;
        DateTime? to = ToDate?.Date.AddDays(1).AddTicks(-1);
        string? search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();

        return new PartAuditFilter
        {
            PartId = PartId,
            From = from,
            To = to,
            Action = SelectedAction,
            Search = search,
            OnlyChangedFields = OnlyChangedFields,
            PageIndex = PageIndex,
            PageSize = PageSize
        };
    }

    /// <summary>
    /// Вычисляет и сохраняет строку с диапазоном отображаемых записей.
    /// </summary>
    /// <remarks>
    /// Использует текущие значения <see cref="PageIndex"/>, <see cref="PageSize"/> и размер коллекции <see cref="Rows"/>.
    /// </remarks>
    private void UpdatePageInfo()
    {
        if (TotalCount == 0 || Rows.Count == 0)
        {
            PageInfo = "Нет записей.";
            return;
        }

        var start = PageIndex * PageSize + 1;
        var end = Math.Min(TotalCount, start + Rows.Count - 1);
        PageInfo = string.Format(CultureInfo.CurrentCulture, "Показаны {0}-{1} из {2}", start, end, TotalCount);
    }

    /// <summary>
    /// Обновляет состояние доступности команд пагинации.
    /// </summary>
    /// <remarks>
    /// Вызывается после изменения счетчиков и индексов.
    /// </remarks>
    private void UpdatePaginationCommands()
    {
        _nextPageCommand.NotifyCanExecuteChanged();
        _prevPageCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Проверяет возможность перехода на следующую страницу.
    /// </summary>
    /// <returns><see langword="true"/>, если следующая страница содержит записи.</returns>
    private bool CanGoNext() => (PageIndex + 1) * PageSize < TotalCount;

    /// <summary>
    /// Проверяет возможность возврата на предыдущую страницу.
    /// </summary>
    /// <returns><see langword="true"/>, если текущий индекс больше нуля.</returns>
    private bool CanGoPrevious() => PageIndex > 0;

    /// <summary>
    /// Переходит на следующую страницу и инициирует загрузку данных.
    /// </summary>
    /// <returns>Задача, завершающаяся после обновления данных.</returns>
    /// <remarks>
    /// Игнорирует вызов, если следующей страницы не существует.
    /// </remarks>
    private async Task NextPageInternalAsync()
    {
        if (!CanGoNext())
        {
            return;
        }

        PageIndex++;
        await LoadInternalAsync();
    }

    /// <summary>
    /// Возвращается на предыдущую страницу и перезагружает данные.
    /// </summary>
    /// <returns>Задача, завершающаяся после чтения предыдущего диапазона.</returns>
    /// <remarks>
    /// Игнорирует вызов на первой странице.
    /// </remarks>
    private async Task PrevPageInternalAsync()
    {
        if (!CanGoPrevious())
        {
            return;
        }

        PageIndex--;
        await LoadInternalAsync();
    }

    /// <summary>
    /// Планирует обновление данных после изменения фильтров.
    /// </summary>
    /// <param name="resetPage">Нужно ли сбрасывать индекс страницы на начало.</param>
    /// <remarks>
    /// Вызов не выполняет загрузку, если она уже идет; пользователь может инициировать обновление вручную.
    /// </remarks>
    private void ScheduleReload(bool resetPage)
    {
        if (resetPage)
        {
            PageIndex = 0;
        }

        if (IsBusy)
        {
            _pendingReload = true;
            return;
        }

        _ = _loadCommand.ExecuteAsync(null);
    }
}
