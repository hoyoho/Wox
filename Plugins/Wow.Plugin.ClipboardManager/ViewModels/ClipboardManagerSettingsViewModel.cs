using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Wow.Infrastructure.Storage;

namespace Wow.Plugin.ClipboardManager.ViewModels
{
    public class ClipboardManagerSettingsViewModel
    {
        private readonly object _lock = new object();
        private readonly PluginJsonStorage<Settings> _storage;
        private readonly Settings _settings;

        public ClipboardManagerSettingsViewModel()
        {
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();
            Items = new ObservableCollection<ClipboardItem>(_settings.Items.OrderByDescending(o => o.CreatedAt));
        }

        public ObservableCollection<ClipboardItem> Items { get; }

        public Settings Settings => _settings;

        public bool AddItem(string name, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var content = text;
            var displayName = name;
            lock (_lock)
            {
                var duplicated = Items.FirstOrDefault(o => string.Equals(o.Text, content, StringComparison.Ordinal));
                if (duplicated != null)
                {
                    Items.Remove(duplicated);
                }

                var item = new ClipboardItem { Name = displayName, Text = content };
                Items.Insert(0, item);
                Persist();
                return true;
            }
        }

        public ClipboardItem AddRow()
        {
            lock (_lock)
            {
                var item = new ClipboardItem();
                Items.Add(item);
                Persist();
                return item;
            }
        }

        public void RemoveItem(ClipboardItem item)
        {
            if (item == null)
            {
                return;
            }

            lock (_lock)
            {
                Items.Remove(item);
                Persist();
            }
        }

        public void ClearItems()
        {
            lock (_lock)
            {
                Items.Clear();
                Persist();
            }
        }

        public ClipboardItem[] GetItemsSnapshot()
        {
            lock (_lock)
            {
                return Items
                    .Where(IsMeaningful)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToArray();
            }
        }

        public void Persist()
        {
            _settings.Items = Items.Where(IsMeaningful).ToList();
            _storage.Save();
        }

        private static bool IsMeaningful(ClipboardItem item)
        {
            return !(string.IsNullOrWhiteSpace(item.Name) && string.IsNullOrWhiteSpace(item.Text));
        }
    }
}
