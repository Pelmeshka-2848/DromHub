using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DromHub.ViewModels
{
    /// <summary>
    /// Группа для алфавитной группировки ListView.
    /// Важно: свойство Items нужно для CollectionViewSource (ItemsPath="Items").
    /// </summary>
    public class AlphaKeyGroup<T> : ObservableCollection<T>
    {
        public string Key { get; }

        // CollectionViewSource будет читать элементы группы по этому свойству
        public IList<T> Items => this;

        public AlphaKeyGroup(string key) => Key = key;

        public static ObservableCollection<AlphaKeyGroup<T>> CreateGroups(
            IEnumerable<T> items,
            Func<T, string> getKeyFunc,
            bool sort)
        {
            var groups = new List<AlphaKeyGroup<T>>();

            foreach (var item in items ?? Enumerable.Empty<T>())
            {
                var name = getKeyFunc(item) ?? string.Empty;
                var key = string.IsNullOrWhiteSpace(name) ? "#" : char.ToUpper(name[0]).ToString();

                var group = groups.FirstOrDefault(g => g.Key == key);
                if (group == null)
                {
                    group = new AlphaKeyGroup<T>(key);
                    groups.Add(group);
                }

                group.Add(item);
            }

            if (sort)
                groups = groups.OrderBy(g => g.Key, StringComparer.Ordinal).ToList();

            return new ObservableCollection<AlphaKeyGroup<T>>(groups);
        }
    }
}