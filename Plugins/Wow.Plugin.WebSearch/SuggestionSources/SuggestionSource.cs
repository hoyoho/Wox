using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wow.Plugin.WebSearch.SuggestionSources
{
    public abstract class SuggestionSource
    {
        public abstract Task<List<string>> Suggestions(string query);
    }
}