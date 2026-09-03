using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wow.Plugin.ClipboardManager.ViewModels;

namespace Wow.Plugin.ClipboardManager.Views
{
    public partial class ClipboardManagerSettings : UserControl
    {
        private readonly ClipboardManagerSettingsViewModel _viewModel;

        public ClipboardManagerSettings(ClipboardManagerSettingsViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
            DataContext = _viewModel;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var item = _viewModel.AddRow();
            if (item == null)
            {
                return;
            }

            ItemsGrid.ScrollIntoView(item);
            ItemsGrid.SelectedItem = item;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ItemsGrid.CurrentCell = new DataGridCellInfo(item, ItemsGrid.Columns[0]);
                ItemsGrid.BeginEdit();
            }));
        }

        private void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelected();
        }

        private void ItemsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DeleteSelected();
                e.Handled = true;
            }
        }

        private void DeleteSelected()
        {
            if (ItemsGrid.SelectedItem is ClipboardItem item)
            {
                _viewModel.RemoveItem(item);
            }
        }

        private void ItemsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
            {
                return;
            }

            if (e.Row.DataContext is ClipboardItem item && string.IsNullOrWhiteSpace(item.Text) && string.IsNullOrWhiteSpace(item.Name))
            {
                _viewModel.RemoveItem(item);
            }
            else
            {
                _viewModel.Persist();
            }
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                Translate("wox_plugin_clipboardmanager_confirm_clear_message"),
                Translate("wox_plugin_clipboardmanager_confirm_clear_title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.ClearItems();
            }
        }

        private static string Translate(string key)
        {
            return Application.Current.TryFindResource(key) as string ?? key;
        }
    }
}
