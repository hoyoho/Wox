using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.Win32;
using NHotkey;
using NHotkey.Wpf;
using Ookii.Dialogs.Wpf; // may be removed later https://github.com/dotnet/wpf/issues/438

using Wow.Core;
using Wow.Core.Plugin;
using Wow.Core.Resource;
using Wow.Infrastructure.Hotkey;
using Wow.Infrastructure.Storage;
using Wow.Infrastructure.UserSettings;
using Wow.Plugin;
using Wow.ViewModel;

namespace Wow
{
    public partial class SettingWindow
    {
        private const string StartupPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        public readonly IPublicAPI _api;
        private Settings _settings;
        private SettingWindowViewModel _viewModel;
        private Grid[] _pages;
        private List<PluginViewModel> _allPlugins;

        public SettingWindow(IPublicAPI api, SettingWindowViewModel viewModel)
        {
            InitializeComponent();
            _settings = Settings.Instance;
            DataContext = viewModel;
            _viewModel = viewModel;
            _api = api;
            _pages = new[] { GeneralPage, PluginPage, ThemePage };
            _allPlugins = viewModel.PluginViewModels.ToList();
            SourceInitialized += OnSourceInitialized;
        }

        private const int GwlStyle = -16;
        private const long WsMinimizeBox = 0x20000L;
        private const long WsMaximizeBox = 0x10000L;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var style = GetWindowLong(handle, GwlStyle);
            style = style & (int)~(WsMaximizeBox | WsMinimizeBox);
            SetWindowLong(handle, GwlStyle, style);
        }

        private void PluginFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allPlugins == null)
            {
                return;
            }

            var keyword = PluginFilterBox.Text.Trim();
            if (keyword.Length == 0)
            {
                PluginList.ItemsSource = _allPlugins;
                return;
            }

            PluginList.ItemsSource = _allPlugins
                .Where(p =>
                    (p.PluginPair.Metadata.Name ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (p.PluginPair.Metadata.Description ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        #region Navigation

        private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_pages == null)
            {
                return;
            }

            var index = NavList.SelectedIndex;
            if (index < 0 || index >= _pages.Length)
            {
                return;
            }
            for (var i = 0; i < _pages.Length; i++)
            {
                _pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #endregion

        #region General

        private void OnAutoStartupChecked(object sender, RoutedEventArgs e)
        {
            SetStartup();
        }

        private void OnAutoStartupUncheck(object sender, RoutedEventArgs e)
        {
            RemoveStartup();
        }

        public static void SetStartup()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupPath, true))
            {
                key?.SetValue(Infrastructure.Constant.Wow, Infrastructure.Constant.ExecutablePath);
            }
        }

        private void RemoveStartup()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupPath, true))
            {
                key?.DeleteValue(Infrastructure.Constant.Wow, false);
            }
        }

        public static bool StartupSet()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StartupPath, true))
            {
                var path = key?.GetValue(Infrastructure.Constant.Wow) as string;
                if (path != null)
                {
                    return path == Infrastructure.Constant.ExecutablePath;
                }
                else
                {
                    return false;
                }
            }
        }

        private void OnHotkeyControlLoaded(object sender, RoutedEventArgs e)
        {
            HotkeyControl.SetHotkey(_viewModel.Settings.Hotkey, false);
        }

        void OnHotkeyChanged(object sender, EventArgs e)
        {
            if (HotkeyControl.CurrentHotkeyAvailable)
            {
                SetHotkey(HotkeyControl.CurrentHotkey, (o, args) =>
                {
                    if (!Application.Current.MainWindow.IsVisible)
                    {
                        Application.Current.MainWindow.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        Application.Current.MainWindow.Visibility = Visibility.Hidden;
                    }
                });
                RemoveHotkey(_settings.Hotkey);
                _settings.Hotkey = HotkeyControl.CurrentHotkey.ToString();
            }
        }

        void SetHotkey(HotkeyModel hotkey, EventHandler<HotkeyEventArgs> action)
        {
            string hotkeyStr = hotkey.ToString();
            try
            {
                HotkeyManager.Current.AddOrReplace(hotkeyStr, hotkey.CharKey, hotkey.ModifierKeys, action);
            }
            catch (Exception)
            {
                string errorMsg =
                    string.Format(InternationalizationManager.Instance.GetTranslation("registerHotkeyFailed"), hotkeyStr);
                MessageBox.Show(errorMsg);
            }
        }

        void RemoveHotkey(string hotkeyStr)
        {
            if (!string.IsNullOrEmpty(hotkeyStr))
            {
                HotkeyManager.Current.Remove(hotkeyStr);
            }
        }

        private void OnPluginHotkeyLoaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is HotkeyControl control) || !(control.DataContext is PluginViewModel vm))
            {
                return;
            }

            var keyword = PrimaryKeyword(vm);
            var hotkey = GetCustomHotkey(keyword);
            control.SetHotkey(hotkey ?? string.Empty, false);
        }

        private void OnPluginHotkeyChanged(object sender, EventArgs e)
        {
            if (!(sender is HotkeyControl control) || !(control.DataContext is PluginViewModel vm))
            {
                return;
            }

            if (!control.CurrentHotkeyAvailable)
            {
                return;
            }

            var keyword = PrimaryKeyword(vm);
            var newHotkey = control.CurrentHotkey.ToString();
            var existing = _settings.CustomPluginHotkeys?.FirstOrDefault(o => o.ActionKeyword == keyword);
            if (existing != null && existing.Hotkey != newHotkey)
            {
                RemoveHotkey(existing.Hotkey);
            }

            if (existing == null)
            {
                if (_settings.CustomPluginHotkeys == null)
                {
                    _settings.CustomPluginHotkeys = new ObservableCollection<CustomPluginHotkey>();
                }

                _settings.CustomPluginHotkeys.Add(new CustomPluginHotkey
                {
                    ActionKeyword = keyword,
                    Hotkey = newHotkey
                });
            }
            else
            {
                existing.Hotkey = newHotkey;
            }

            SetPluginQueryHotkey(control.CurrentHotkey, keyword);
        }

        private void OnPluginKeywordLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is PluginViewModel vm)
            {
                textBox.Text = PrimaryKeyword(vm);
            }
        }

        private void OnPluginKeywordLostFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox textBox) || !(textBox.DataContext is PluginViewModel vm))
            {
                return;
            }

            var pair = vm.PluginPair;
            var oldKeyword = PrimaryKeyword(vm);
            var newKeyword = textBox.Text.Trim();
            if (string.IsNullOrEmpty(newKeyword))
            {
                newKeyword = "*";
            }

            if (newKeyword == oldKeyword)
            {
                return;
            }

            if (newKeyword != "*" && PluginManager.ActionKeywordRegistered(newKeyword))
            {
                MessageBox.Show(InternationalizationManager.Instance.GetTranslation("newActionKeywordsHasBeenAssigned"));
                textBox.Text = oldKeyword;
                return;
            }

            PluginManager.ReplaceActionKeyword(pair.Metadata.ID, oldKeyword, newKeyword);

            if (_settings.PluginSettings.Plugins.TryGetValue(pair.Metadata.ID, out var stored))
            {
                stored.ActionKeywords = pair.Metadata.ActionKeywords.ToList();
            }
        }

        private static string PrimaryKeyword(PluginViewModel vm)
        {
            var keywords = vm.PluginPair.Metadata.ActionKeywords;
            if (keywords != null && keywords.Count > 0)
            {
                return keywords[0];
            }

            return vm.PluginPair.Metadata.ActionKeyword;
        }

        private string GetCustomHotkey(string keyword)
        {
            return _settings.CustomPluginHotkeys?.FirstOrDefault(o => o.ActionKeyword == keyword)?.Hotkey;
        }

        private void SetPluginQueryHotkey(HotkeyModel hotkey, string keyword)
        {
            SetHotkey(hotkey, (o, args) =>
            {
                App.API.ChangeQuery(keyword);
                Application.Current.MainWindow.Visibility = Visibility.Visible;
            });
        }

        #endregion

        #region Plugin

        private static void ClearPluginHotkeyDisplay(FrameworkElement element)
        {
            if (element == null)
            {
                return;
            }

            var scope = element;
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(element);
            while (parent != null && !(parent is System.Windows.Controls.ItemsControl) && !(parent is System.Windows.Controls.ScrollViewer))
            {
                scope = parent as FrameworkElement ?? scope;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }

            if (!(element.DataContext is PluginViewModel target))
            {
                return;
            }

            foreach (var hotkeyControl in FindVisualChildren<HotkeyControl>(scope))
            {
                if (hotkeyControl.DataContext is PluginViewModel viewModel &&
                    viewModel.PluginPair.Metadata.ID == target.PluginPair.Metadata.ID)
                {
                    hotkeyControl.SetHotkey(string.Empty, false);
                }
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                yield break;
            }

            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private void OnPluginToggleClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is PluginViewModel plugin)
            {
                var pair = plugin.PluginPair;
                pair.Metadata.Disabled = !pair.Metadata.Disabled;
                _settings.PluginSettings.Plugins[pair.Metadata.ID].Disabled = pair.Metadata.Disabled;

                if (pair.Metadata.Disabled)
                {
                    var keyword = PrimaryKeyword(plugin);
                    var existing = _settings.CustomPluginHotkeys?.FirstOrDefault(o => o.ActionKeyword == keyword);
                    if (existing != null)
                    {
                        RemoveHotkey(existing.Hotkey);
                        _settings.CustomPluginHotkeys.Remove(existing);
                    }

                    foreach (var actionKeyword in pair.Metadata.ActionKeywords)
                    {
                        if (actionKeyword == Wow.Plugin.Query.GlobalPluginWildcardSign)
                        {
                            continue;
                        }

                        if (PluginManager.NonGlobalPlugins.TryGetValue(actionKeyword, out var current) && current == pair)
                        {
                            PluginManager.NonGlobalPlugins.Remove(actionKeyword);
                        }
                    }

                    ClearPluginHotkeyDisplay(element);
                }
                else
                {
                    foreach (var actionKeyword in pair.Metadata.ActionKeywords)
                    {
                        if (actionKeyword == Wow.Plugin.Query.GlobalPluginWildcardSign)
                        {
                            continue;
                        }

                        if (!PluginManager.NonGlobalPlugins.ContainsKey(actionKeyword))
                        {
                            PluginManager.NonGlobalPlugins[actionKeyword] = pair;
                        }
                    }
                }
            }
        }

        private void OnPluginCardActivate(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (sender is FrameworkElement element && element.DataContext is PluginViewModel plugin)
            {
                plugin.IsExpanded = !plugin.IsExpanded;
            }
        }

        private void OnPluginDirectoryClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is PluginViewModel plugin)
            {
                var directory = plugin.PluginPair.Metadata.PluginDirectory;
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    Process.Start(directory);
                }
            }
        }

        #endregion

        #region Proxy

        private void OnTestProxyClick(object sender, RoutedEventArgs e)
        { // TODO: change to command
            var msg = _viewModel.TestProxy();
            MessageBox.Show(msg); // TODO: add message box service
        }

        #endregion

        private void OnCheckUpdates(object sender, RoutedEventArgs e)
        {
            _viewModel.UpdateApp(); // TODO: change to command
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _viewModel.Save();
            PluginManager.Save();
        }

        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }
    }
}
