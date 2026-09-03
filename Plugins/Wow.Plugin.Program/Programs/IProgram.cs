using System.Collections.Generic;

namespace Wow.Plugin.Program.Programs
{
    public interface IProgram
    {
        List<Result> ContextMenus(IPublicAPI api);
        Result Result(string query, IPublicAPI api);
        string Name { get; }
        string Location { get; }
    }
}
