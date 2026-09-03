using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wow.Plugin;
using Wow.Core.Resource;
using Wow.Image;

namespace Wow.ViewModel
{
    public class PluginViewModel : BaseModel
    {
        private readonly Internationalization _translator = InternationalizationManager.Instance;
        private PluginPair _pluginPair;
        private Control _settingsControl;
        private bool _isExpanded;

        public PluginPair PluginPair
        {
            get { return _pluginPair; }
            set
            {
                if (_pluginPair == value)
                {
                    return;
                }

                if (_pluginPair?.Metadata != null)
                {
                    _pluginPair.Metadata.PropertyChanged -= Metadata_PropertyChanged;
                }

                _pluginPair = value;

                if (_pluginPair?.Metadata != null)
                {
                    _pluginPair.Metadata.PropertyChanged += Metadata_PropertyChanged;
                }

                OnPropertyChanged(nameof(PluginPair));
            }
        }

        private void Metadata_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PluginMetadata.Disabled))
            {
                OnPropertyChanged(nameof(EnableButtonText));
            }
        }

        public ImageSource Image => ImageLoader.Load(PluginPair.Metadata.IcoPath);

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (_isExpanded == value)
                {
                    return;
                }

                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
                if (_isExpanded)
                {
                    OnPropertyChanged(nameof(SettingsControl));
                }
            }
        }

        /// <summary>
        /// 插件设置面板,仅在展开时惰性创建,高度由插件自身决定。
        /// </summary>
        public Control SettingsControl
        {
            get
            {
                if (!_isExpanded)
                {
                    return null;
                }

                if (_settingsControl == null && PluginPair.Plugin is ISettingProvider provider)
                {
                    _settingsControl = provider.CreateSettingPanel();
                    _settingsControl.HorizontalAlignment = HorizontalAlignment.Stretch;
                    _settingsControl.VerticalAlignment = VerticalAlignment.Stretch;
                }

                return _settingsControl;
            }
        }

        public string EnableButtonText =>
            PluginPair.Metadata.Disabled
                ? _translator.GetTranslation("enable")
                : _translator.GetTranslation("disable");

        public Visibility HasSettingsVisibility => PluginPair.Plugin is ISettingProvider ? Visibility.Visible : Visibility.Collapsed;
        public string InitilizaTime => string.Format(_translator.GetTranslation("plugin_init_time"), PluginPair.Metadata.InitTime);
        public string QueryTime => string.Format(_translator.GetTranslation("plugin_query_time"), PluginPair.Metadata.AvgQueryTime);
    }
}
