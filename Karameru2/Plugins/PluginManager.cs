using System;
using System.Collections.Generic;

namespace Karameru2.Plugins
{
    public static class PluginManager
    {
        public static List<IArchivePlugin> ArchivePlugins { get; } = new List<IArchivePlugin>
        {
            new Level5FaPlugin(),
            new ArcPlugin(),
            // Tu pourras ajouter d'autres plugins ici (cpk, narc, garc, etc.)
        };
    }
} 