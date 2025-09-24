using DromHub.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace DromHub.ViewModels
{
    public class BrandsOverviewViewModel
    {
        private readonly ApplicationDbContext _db;
        public string TotalBrandsText { get; private set; }
        public string WithMarkupText { get; private set; }
        public string NoAliasesText { get; private set; }
        public string NoPartsText { get; private set; }

        public BrandsOverviewViewModel(ApplicationDbContext db) => _db = db;

        public async Task LoadAsync()
        {
            var total = await _db.Brands.CountAsync();
            var withMarkup = await _db.BrandMarkups.CountAsync(m => m.MarkupPct > 0);
            var noAliases = await _db.Brands.CountAsync(b => !b.Aliases.Any(a => !a.IsPrimary));
            var noParts = await _db.Brands.CountAsync(b => !b.Parts.Any());

            TotalBrandsText = total.ToString();
            WithMarkupText = withMarkup.ToString();
            NoAliasesText = noAliases.ToString();
            NoPartsText = noParts.ToString();
        }
    }
}