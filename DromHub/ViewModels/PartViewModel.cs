using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using DromHub.Data;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DromHub.ViewModels
{
    public class PartViewModel : INotifyPropertyChanged
    {
        private readonly ApplicationDbContext _context;
        private Part _part;

        public PartViewModel(ApplicationDbContext context)
        {
            _context = context;
            _part = new Part();
            Brands = new ObservableCollection<Brand>();
            LoadBrandsCommand = new AsyncRelayCommand(LoadBrands);
            SavePartCommand = new AsyncRelayCommand(SavePartAsync);
        }

        public PartViewModel(ApplicationDbContext context, Part part) : this(context)
        {
            _part = part ?? new Part();
        }

        public Guid Id
        {
            get => _part.Id;
            set
            {
                if (_part.Id != value)
                {
                    _part.Id = value;
                    OnPropertyChanged();
                }
            }
        }

        public Guid BrandId
        {
            get => _part.BrandId;
            set
            {
                if (_part.BrandId != value)
                {
                    _part.BrandId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CatalogNumber
        {
            get => _part.CatalogNumber;
            set
            {
                if (_part.CatalogNumber != value)
                {
                    _part.CatalogNumber = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Article));
                }
            }
        }

        public string Article => _part.Article;

        public string Name
        {
            get => _part.Name;
            set
            {
                if (_part.Name != value)
                {
                    _part.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime CreatedAt => _part.CreatedAt;
        public DateTime UpdatedAt => _part.UpdatedAt;

        private Brand _selectedBrand;
        public Brand SelectedBrand
        {
            get => _selectedBrand ?? _part.Brand;
            set
            {
                if (_selectedBrand != value)
                {
                    _selectedBrand = value;
                    BrandId = value?.Id ?? Guid.Empty;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<Brand> Brands { get; }

        public IAsyncRelayCommand LoadBrandsCommand { get; }
        public IAsyncRelayCommand SavePartCommand { get; }

        private async Task LoadBrands()
        {
            var brands = await _context.Brands.ToListAsync();
            Brands.Clear();
            foreach (var brand in brands)
            {
                Brands.Add(brand);
            }
        }

        private void ResetPart()
        {
            _part = new Part();
            _selectedBrand = null;
            OnPropertyChanged(nameof(SelectedBrand));
            OnPropertyChanged(nameof(CatalogNumber));
            OnPropertyChanged(nameof(Article));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(CreatedAt));
            OnPropertyChanged(nameof(UpdatedAt));
        }

        // ВНИМАНИЕ данный метод срабатывает только 1 раз, в противном случае он изменяет объект, который был только что добавлен. Возможные фиксы:
        // - Добавление запчастей через диалоговое окно, после нажатия кнопки сохранить оно закрывается. Редактирование сделать в поиске, также через диалоговое окно.
        // - Заняться изменением данного метода, для приведения его к нормальному виду.
        public async Task SavePartAsync()
        {
            // 1) Блокируем любое редактирование в этом методе
            if (_part.Id != Guid.Empty)
            {
                // Здесь покажите пользователю уведомление/диалог
                Debug.WriteLine("Редактирование запрещено: используйте форму/диалог редактирования.");
                return;
            }

            // 2) Базовая валидация
            _part.CatalogNumber = _part.CatalogNumber?.Trim();
            if (_part.BrandId == Guid.Empty || string.IsNullOrWhiteSpace(_part.CatalogNumber))
            {
                Debug.WriteLine("Заполните Бренд и Артикул.");
                return;
            }

            // 3) Предварительная проверка уникальности (дублирует БД, но даёт мгновенную обратную связь)
            bool exists = await _context.Parts
                .AsNoTracking()
                .AnyAsync(p => p.BrandId == _part.BrandId &&
                               p.CatalogNumber == _part.CatalogNumber);
            if (exists)
            {
                Debug.WriteLine("Такая запчасть уже существует для выбранного бренда.");
                return;
            }

            // 4) Вставка через отдельный экземпляр (не трогаем _part, чтобы не менять состояние формы)
            var entity = new Part
            {
                BrandId = _part.BrandId,
                CatalogNumber = _part.CatalogNumber,
                Name = _part.Name
            };

            try
            {
                await _context.Parts.AddAsync(entity);
                await _context.SaveChangesAsync();

                // На всякий случай снимаем отслеживание вставленной сущности
                _context.Entry(entity).State = EntityState.Detached;

                // 5) Готовим форму к следующему вводу
                ResetPart();
            }
            catch (DbUpdateException ex)
            {
                // Если всё-таки пришёл конфликт уникальности из БД — сообщаем
                Debug.WriteLine("Ошибка сохранения (возможно, нарушение уникальности): " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}