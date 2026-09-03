using System.Windows;
using System.Windows.Input;

namespace Wow.Plugin
{
    public delegate void WowKeyDownEventHandler(WowKeyDownEventArgs e);
    public delegate void AfterWowQueryEventHandler(WowQueryEventArgs e);

    public delegate void ResultItemDropEventHandler(Result result, IDataObject dropObject, DragEventArgs e);

    /// <summary>
    /// Global keyboard events
    /// </summary>
    /// <param name="keyevent">WM_KEYDOWN = 256,WM_KEYUP = 257,WM_SYSKEYUP = 261,WM_SYSKEYDOWN = 260</param>
    /// <param name="vkcode"></param>
    /// <param name="state"></param>
    /// <returns>return true to continue handling, return false to intercept system handling</returns>
    public delegate bool WowGlobalKeyboardEventHandler(int keyevent, int vkcode, SpecialKeyState state);

    public class WowKeyDownEventArgs
    {
        public string Query { get; set; }
        public KeyEventArgs keyEventArgs { get; set; }
    }

    public class WowQueryEventArgs
    {
        public Query Query { get; set; }
    }
}
