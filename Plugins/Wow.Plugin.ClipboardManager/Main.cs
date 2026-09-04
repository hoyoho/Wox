using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Wow.Infrastructure;
using Wow.Infrastructure.Storage;
using Wow.Plugin.ClipboardManager.ViewModels;
using Wow.Plugin.ClipboardManager.Views;

namespace Wow.Plugin.ClipboardManager
{
    public class Main : IPlugin, IPluginI18n, ISavable, ISettingProvider
    {
        private const string ClipboardIco = "Images\\clipboard.png";
        private const int MaxTitleLength = 300;

        private PluginInitContext _context;
        private ClipboardManagerSettingsViewModel _viewModel;

        public void Init(PluginInitContext context)
        {
            _context = context;
            _viewModel = new ClipboardManagerSettingsViewModel();
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            if (_viewModel == null)
            {
                return results;
            }

            var items = _viewModel.GetItemsSnapshot();
            if (items.Length == 0)
            {
                return EmptyResult();
            }

            var search = query?.Search ?? string.Empty;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var contentLine = ToSingleLine(item.Text ?? string.Empty);
                var nameLine = ToSingleLine(item.Name ?? string.Empty);
                var hasName = nameLine.Length > 0;
                var title = CapText(hasName ? nameLine : contentLine, MaxTitleLength);

                if (search.Length == 0)
                {
                    // 空搜索列出全部(含暂无名称的条目),越高分越新,Wow 保持最新在上
                    results.Add(BuildResult(item, title, contentLine, hasName, items.Length - i, null));
                    continue;
                }

                // 只按名称匹配:先原文包含,再拼音/英文模糊
                if (!hasName)
                {
                    continue;
                }

                if (ContainsIgnoreCase(nameLine, search))
                {
                    results.Add(BuildResult(item, title, contentLine, true, 100, null));
                    continue;
                }

                var nameMatch = StringMatcher.FuzzySearch(search, nameLine);
                if (nameMatch.Success)
                {
                    results.Add(BuildResult(item, title, contentLine, true, nameMatch.Score, nameMatch.MatchData));
                    continue;
                }

                // 拼音候选:全拼(如 chifa)与首字母(如 cf)均可命中,不依赖全局拼音开关
                if (MatchPinyin(nameLine, search))
                {
                    results.Add(BuildResult(item, title, contentLine, true, 90, null));
                }
            }

            // 结果条数由 Wow 主程序(搜索显示上限)统一控制,插件不再自行截断
            return search.Length == 0
                ? results
                : results.OrderByDescending(o => o.Score).ToList();
        }

        private List<Result> EmptyResult()
        {
            return new List<Result>
            {
                new Result
                {
                    Title = _context.API.GetTranslation("wow_plugin_clipboardmanager_no_item"),
                    SubTitle = _context.API.GetTranslation("wow_plugin_clipboardmanager_no_item_subtitle"),
                    IcoPath = ClipboardIco,
                    Action = _ =>
                    {
                        _context.API.OpenSettingDialog();
                        return true;
                    }
                }
            };
        }

        private Result BuildResult(ClipboardItem item, string title, string contentLine, bool hasName, int score, List<int> highlightData)
        {
            var created = item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            var hint = _context.API.GetTranslation("wow_plugin_clipboardmanager_copy_to_clipboard") +
                       "  ·  " + created;
            var subtitle = hasName
                ? CapText(contentLine, 180) + "   ·   " + hint
                : hint;

            List<int> data = null;
            if (highlightData != null)
            {
                data = highlightData.Where(o => o < title.Length && o >= 0).ToList();
            }

            return new Result
            {
                Title = title,
                SubTitle = subtitle,
                IcoPath = ClipboardIco,
                Score = score,
                TitleHighlightData = data,
                ContextData = item,
                Action = _ => CopyToClipboard(item)
            };
        }

        private bool CopyToClipboard(ClipboardItem item)
        {
            if (ClipboardHelper.SetText(item.Text))
            {
                return true;
            }

            _context.API.ShowMsg(_context.API.GetTranslation("wow_plugin_clipboardmanager_copy_failed"),
                string.Empty, ClipboardIco);
            return false;
        }

        private static bool MatchPinyin(string nameLine, string search)
        {
            var full = SafePinyin(nameLine, full: true);
            var initials = SafePinyin(nameLine, full: false);
            foreach (var candidate in new[] { full, initials })
            {
                if (string.IsNullOrEmpty(candidate) ||
                    string.Equals(candidate, nameLine, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (ContainsIgnoreCase(candidate, search))
                {
                    return true;
                }

                var match = StringMatcher.FuzzySearch(search, candidate);
                if (match.Success)
                {
                    return true;
                }
            }

            return false;
        }

        private static string SafePinyin(string text, bool full)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            try
            {
                return full ? ToolGood.Words.WordsHelper.GetPinyin(text, false)
                            : ToolGood.Words.WordsHelper.GetFirstPinyin(text);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool ContainsIgnoreCase(string source, string search)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ToSingleLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                builder.Append(c == '\r' || c == '\n' || c == '\t' ? ' ' : c);
            }

            return builder.ToString();
        }

        private static string CapText(string text, int maxLength)
        {
            if (text.Length <= maxLength)
            {
                return text;
            }

            var cut = maxLength;
            if (char.IsHighSurrogate(text[cut - 1]))
            {
                cut--;
            }

            return text.Substring(0, cut) + "…";
        }

        public Control CreateSettingPanel()
        {
            return new ClipboardManagerSettings(_viewModel);
        }

        public void Save()
        {
            _viewModel?.Persist();
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("wow_plugin_clipboardmanager_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("wow_plugin_clipboardmanager_plugin_description");
        }
    }
}
