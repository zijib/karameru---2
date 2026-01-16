using System;
using System.Collections.Generic;
using System.IO;

namespace Karameru2.Plugins
{
    public class Level5FaPlugin : IArchivePlugin
    {
        public string Name => "Level-5 ARC0 (.fa)";
        public string[] FileExtensions => new[] { ".fa" };
        public string Description => "Lecteur d'archives Level-5 ARC0 (.fa)";

        private readonly Arc0Reader _reader = new Arc0Reader();

        // Cache pour éviter de relire l'arborescence à chaque opération
        private readonly Dictionary<string, Arc0Reader.ArchiveNode> _treeCache =
            new Dictionary<string, Arc0Reader.ArchiveNode>(StringComparer.OrdinalIgnoreCase);

        public bool IsMatch(string filePath)
        {
            return File.Exists(filePath) &&
                   Path.GetExtension(filePath).Equals(".fa", StringComparison.OrdinalIgnoreCase);
        }

        private Arc0Reader.ArchiveNode GetOrLoadTree(string filePath)
        {
            if (!_treeCache.TryGetValue(filePath, out var tree))
            {
                tree = _reader.ReadArchiveTree(filePath);
                _treeCache[filePath] = tree;
            }
            return tree;
        }

        public List<string> ListFiles(string filePath)
        {
            var tree = GetOrLoadTree(filePath);
            var list = new List<string>();

            void Walk(Arc0Reader.ArchiveNode node, string path)
            {
                string current = string.IsNullOrEmpty(path) ? node.Name : $"{path}/{node.Name}";

                if (node.IsFile)
                {
                    list.Add(current);
                }
                else
                {
                    foreach (var child in node.Children)
                        Walk(child, current);
                }
            }

            foreach (var child in tree.Children)
                Walk(child, "");

            return list;
        }

        public Arc0Reader.ArchiveNode GetArchiveTree(string filePath)
        {
            return GetOrLoadTree(filePath);
        }

        public byte[] ExtractFile(string archivePath, string internalPath)
        {
            var tree = GetOrLoadTree(archivePath);

            Arc0Reader.ArchiveNode? found = null;

            void Search(Arc0Reader.ArchiveNode node, string currentPath)
            {
                if (found != null)
                    return;

                string full = string.IsNullOrEmpty(currentPath)
                    ? node.Name
                    : $"{currentPath}/{node.Name}";

                if (node.IsFile && full.Equals(internalPath, StringComparison.OrdinalIgnoreCase))
                {
                    found = node;
                    return;
                }

                foreach (var child in node.Children)
                    Search(child, full);
            }

            foreach (var child in tree.Children)
                Search(child, "");

            if (found == null || found.FileIndex == null)
                throw new FileNotFoundException($"Fichier introuvable : {internalPath}");

            return _reader.ExtractFile(archivePath, found.FileIndex.Value);
        }
    }
}
