using System.Windows.Controls;

namespace Wow.Plugin
{
    public interface ISettingProvider
    {
        Control CreateSettingPanel();
    }
}
