using System;
using System.IO;
using System.Collections.Generic;

namespace Karameru2.Plugins
{
    public class ArcPlugin : IArchivePlugin
    {
        public string Name => "Nintendo DARC Archive";
        public string[] FileExtensions => new[] { ".arc" };
        public string Description => "Archive DARC utilisée dans de nombreux jeux Nintendo (NDS, 3DS, etc.).";

        public bool IsMatch(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".arc", StringComparison.OrdinalIgnoreCase);
        }

        public List<string> ListFiles(string filePath)
        {
            throw new NotSupportedException("ListFiles non implémenté pour DARC encore.");
        }

        public Arc0Reader.ArchiveNode GetArchiveTree(string filePath)
        {
            throw new NotSupportedException("GetArchiveTree non implémenté pour DARC encore.");
        }

        public byte[] ExtractFile(string archivePath, string internalPath)
        {
            throw new NotSupportedException("ExtractFile non implémenté pour DARC encore.");
        }
    }
} 