using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NLog;
using Wow.Infrastructure;
using Wow.Infrastructure.Exception;
using Wow.Infrastructure.Logger;
using Wow.Infrastructure.UserSettings;
using Wow.Plugin;

namespace Wow.Core.Plugin
{
    public static class PluginsLoader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static List<PluginPair> Plugins(List<PluginMetadata> metadatas, PluginsSettings settings)
        {
            var csharpPlugins = CSharpPlugins(metadatas).ToList();
            var executablePlugins = ExecutablePlugins(metadatas);
            var plugins = csharpPlugins.Concat(executablePlugins).ToList();
            return plugins;
        }

        public static IEnumerable<PluginPair> CSharpPlugins(List<PluginMetadata> source)
        {
            var plugins = new List<PluginPair>();
            var metadatas = source.Where(o => o.Language.ToUpper() == AllowedLanguage.CSharp);

            Parallel.ForEach(metadatas, metadata =>
            {
                var milliseconds = Logger.StopWatchDebug($"Constructor init cost for {metadata.Name}", () =>
                {

#if DEBUG
                    var assembly = Assembly.Load(AssemblyName.GetAssemblyName(metadata.ExecuteFilePath));
                    var types = assembly.GetTypes();
                    var type = types.First(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Contains(typeof(IPlugin)));
                    var plugin = (IPlugin)Activator.CreateInstance(type);
#else
                    Assembly assembly;
                    try
                    {
                        assembly = Assembly.Load(AssemblyName.GetAssemblyName(metadata.ExecuteFilePath));
                    }
                    catch (Exception e)
                    {
                        e.Data.Add(nameof(metadata.ID), metadata.ID);
                        e.Data.Add(nameof(metadata.Name), metadata.Name);
                        e.Data.Add(nameof(metadata.PluginDirectory), metadata.PluginDirectory);
                        e.Data.Add(nameof(metadata.Website), metadata.Website);
                        Logger.WowError($"Couldn't load assembly for {metadata.Name}", e);
                        return;
                    }
                    var types = assembly.GetTypes();
                    Type type;
                    try
                    {
                        type = types.First(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Contains(typeof(IPlugin)));
                    }
                    catch (InvalidOperationException e)
                    {
                        e.Data.Add(nameof(metadata.ID), metadata.ID);
                        e.Data.Add(nameof(metadata.Name), metadata.Name);
                        e.Data.Add(nameof(metadata.PluginDirectory), metadata.PluginDirectory);
                        e.Data.Add(nameof(metadata.Website), metadata.Website);
                        Logger.WowError($"Can't find class implement IPlugin for <{metadata.Name}>", e);
                        return;
                    }
                    IPlugin plugin;
                    try
                    {
                        plugin = (IPlugin)Activator.CreateInstance(type);
                    }
                    catch (Exception e)
                    {
                        e.Data.Add(nameof(metadata.ID), metadata.ID);
                        e.Data.Add(nameof(metadata.Name), metadata.Name);
                        e.Data.Add(nameof(metadata.PluginDirectory), metadata.PluginDirectory);
                        e.Data.Add(nameof(metadata.Website), metadata.Website);
                        Logger.WowError($"Can't create instance for <{metadata.Name}>", e);
                        return;
                    }
#endif
                    PluginPair pair = new PluginPair
                    {
                        Plugin = plugin,
                        Metadata = metadata
                    };
                    plugins.Add(pair);
                });
                metadata.InitTime += milliseconds;

            });
            return plugins;
        }

        public static IEnumerable<PluginPair> ExecutablePlugins(IEnumerable<PluginMetadata> source)
        {
            var metadatas = source.Where(o => o.Language.ToUpper() == AllowedLanguage.Executable);

            var plugins = metadatas.Select(metadata => new PluginPair
            {
                Plugin = new ExecutablePlugin(metadata.ExecuteFilePath),
                Metadata = metadata
            });
            return plugins;
        }

    }
}