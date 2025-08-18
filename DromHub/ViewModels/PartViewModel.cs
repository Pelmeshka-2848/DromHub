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
            _context = context;
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
            _part.CatalogNumber = _part.CatalogNumber?.Trim();

            if (_part.BrandId == Guid.Empty || string.IsNullOrWhiteSpace(_part.CatalogNumber))
            {
                Debug.WriteLine("Заполните Бренд и Каталог.");
                return;
            }

            if (_part.Id == Guid.Empty)
            {
                // Добавление новой детали
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
                    _context.Entry(entity).State = EntityState.Detached;
                    ResetPart();
                }
                catch (DbUpdateException ex)
                {
                    Debug.WriteLine("Ошибка сохранения (возможно, нарушение уникальности): " + ex.Message);
                }
            }
            else
            {
                // Редактирование существующей детали
                var entity = await _context.Parts.FindAsync(_part.Id);
                if (entity != null)
                {
                    entity.BrandId = _part.BrandId;
                    entity.CatalogNumber = _part.CatalogNumber;
                    entity.Name = _part.Name;

                    await _context.SaveChangesAsync();
                    _context.Entry(entity).State = EntityState.Detached;
                }
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}