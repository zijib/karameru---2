using System;
using System.Collections.Generic;

namespace Karameru2.Plugins
{
    public interface IArchivePlugin
    {
        string Name { get; }
        string[] FileExtensions { get; }
        string Description { get; }
        bool IsMatch(string filePath);
        List<string> ListFiles(string filePath);
        Arc0Reader.ArchiveNode GetArchiveTree(string filePath);
        byte[] ExtractFile(string archivePath, string internalPath);
    }
} 