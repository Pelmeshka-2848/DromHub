using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DromHub.Models;
using DromHub.Services;

namespace DromHub.ViewModels;

/// <summary>
/// <para>Управляет состоянием экрана истории изменений бренда, объединяя фильтры, пагинацию и команды обновления.</para>
/// <para>Используется страницей администратора для расследования правок и аудита, обращаясь к <see cref="BrandAuditService"/> для получения данных.</para>
/// <para>Не выполняет запись и кеширование; каждый пересчет фильтра инициирует новое чтение журнала.</para>
/// </summary>
/// <remarks>
/// Потокобезопасность: экземпляр предназначен для использования только в UI-потоке и не потокобезопасен.
/// Побочные эффекты: выполняет операции чтения через <see cref="BrandAuditService"/> и обновляет коллекции UI.
/// Сложность типичных операций: O(n) относительно размера текущей страницы при загрузке.
/// См. также: <see cref="BrandAuditService"/>.
/// </remarks>
public sealed partial class BrandChangesViewModel : ObservableObject
{
    /// <summary>
    /// Сохраняет ссылку на сервис аудита для построения выдачи журнала изменений.
    /// </summary>
    private readonly BrandAuditService _service;

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
    /// Хранит идентификатор бренда, историю которого просматривает пользователь.
    /// </summary>
    private Guid _brandId;

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
    /// Представляет информационную строку пагинации для отображения диапазона записей.
    /// </summary>
    private string _pageInfo = "Нет данных.";

    /// <summary>
    /// Фиксирует необходимость повторной загрузки после завершения текущей операции.
    /// </summary>
    private bool _pendingReload;

    /// <summary>
    /// Инициализирует модель представления зависимостью от <see cref="BrandAuditService"/> и настраивает команды.
    /// </summary>
    /// <param name="service">Сервис чтения лога аудита брендов; не допускает значение <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Возникает, когда <paramref name="service"/> не предоставлен контейнером.</exception>
    /// <remarks>
    /// Предусловия: контейнер внедрения зависимостей должен предоставить корректный экземпляр сервиса.
    /// Постусловия: коллекции и команды готовы к использованию страницей.
    /// Побочные эффекты: заполняет списки параметров фильтров.
    /// </remarks>
    /// <example>
    /// <code>
    /// var vm = new BrandChangesViewModel(service);
    /// await vm.InitializeAsync(existingBrandId);
    /// </code>
    /// </example>
    public BrandChangesViewModel(BrandAuditService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));

        Rows = new ObservableCollection<BrandAuditRow>();
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
    /// <value>Наблюдаемая коллекция, синхронизированная с результатами <see cref="BrandAuditService"/>.</value>
    /// <remarks>
    /// Коллекция очищается и заполняется заново при каждой загрузке.
    /// </remarks>
    public ObservableCollection<BrandAuditRow> Rows { get; }

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
    /// Возвращает или задает идентификатор бренда, журнал изменений которого отображается.
    /// </summary>
    /// <value>GUID бренда; значение по умолчанию — <see cref="Guid.Empty"/>, что означает отсутствие выбранного бренда.</value>
    /// <remarks>
    /// Изменение свойства очищает текущие данные и требует повторной инициализации через <see cref="InitializeAsync(Guid)"/>.
    /// </remarks>
    public Guid BrandId
    {
        get => _brandId;
        private set => SetProperty(ref _brandId, value);
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
                ScheduleReload(resetPage: true);
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
                ScheduleReload(resetPage: true);
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
                ScheduleReload(resetPage: true);
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
                ScheduleReload(resetPage: true);
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
                ScheduleReload(resetPage: true);
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
    /// Выполняет первичную загрузку аудита для переданного бренда.
    /// </summary>
    /// <param name="brandId">Идентификатор бренда; не допускается <see cref="Guid.Empty"/>.</param>
    /// <returns>Задача, завершающаяся после подготовки первой страницы журнала.</returns>
    /// <exception cref="ArgumentException">Выбрасывается, когда <paramref name="brandId"/> равен <see cref="Guid.Empty"/>.</exception>
    /// <remarks>
    /// Предусловия: страница еще не инициализирована другим брендом.
    /// Постусловия: установлены фильтры по умолчанию и загружены первые данные.
    /// Побочные эффекты: выполняет обращение к базе данных через сервис.
    /// </remarks>
    /// <example>
    /// <code>
    /// await viewModel.InitializeAsync(brandId);
    /// // Далее можно менять фильтры: viewModel.OnlyChangedFields = true;
    /// </code>
    /// </example>
    public async Task InitializeAsync(Guid brandId)
    {
        if (brandId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор бренда не может быть пустым.", nameof(brandId));
        }

        BrandId = brandId;
        PageIndex = 0;
        await _loadCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Строит объект фильтра и выполняет запрос сервиса аудита.
    /// </summary>
    /// <returns>Задача, завершающаяся после обновления коллекции <see cref="Rows"/>.</returns>
    /// <remarks>
    /// Предусловия: <see cref="BrandId"/> задан и не равен <see cref="Guid.Empty"/>.
    /// Постусловия: <see cref="Rows"/>, <see cref="TotalCount"/> и <see cref="PageInfo"/> отражают актуальное состояние.
    /// Побочные эффекты: выполняет чтение из БД и обновляет наблюдаемые коллекции.
    /// Идемпотентность: повторные вызовы с неизменными фильтрами возвращают одинаковые данные.
    /// </remarks>
    private async Task LoadInternalAsync()
    {
        if (BrandId == Guid.Empty)
        {
            Rows.Clear();
            TotalCount = 0;
            PageInfo = "Бренд не выбран.";
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

            Rows.Clear();
            foreach (var row in rows)
            {
                Rows.Add(row);
            }

            TotalCount = total;
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
    /// Создает объект фильтра на основе текущего состояния модели представления.
    /// </summary>
    /// <returns>Экземпляр <see cref="BrandAuditFilter"/> с заполненными параметрами.</returns>
    /// <remarks>
    /// Конвертирует даты в границы суток и очищает пустые строки поиска.
    /// </remarks>
    private BrandAuditFilter BuildFilter()
    {
        DateTime? from = FromDate?.Date;
        DateTime? to = ToDate?.Date.AddDays(1).AddTicks(-1);
        string? search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();

        return new BrandAuditFilter
        {
            BrandId = BrandId,
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
