using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        public async Task SavePartAsync()
        {
            if (_part.Id == Guid.Empty)
            {
                await _context.Parts.AddAsync(_part);
            }
            else
            {
                _context.Parts.Update(_part);
            }

            await _context.SaveChangesAsync();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}