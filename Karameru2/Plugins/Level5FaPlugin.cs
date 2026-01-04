using System;
using System.IO;
using System.Collections.Generic; // Added for List
using System.Text.RegularExpressions; // Added for Regex

namespace Karameru2.Plugins
{
    public class Level5FaPlugin : IArchivePlugin
    {
        public string Name => "Level-5 FA Archive";
        public string[] FileExtensions => new[] { ".fa" };
        public string Description => "Archive de ressources utilisée dans les jeux Level-5 (Yo-kai Watch, Inazuma Eleven, etc.).";

        public bool IsMatch(string filePath)
        {
            // Détection simple par extension
            return Path.GetExtension(filePath).Equals(".fa", StringComparison.OrdinalIgnoreCase);
        }

        public List<string> ListFiles(string filePath)
        {
            var files = new List<string>();
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                // Version simplifiée : recherche de noms de fichiers en Shift-JIS dans l'archive
                // (Remplacer par la vraie logique Arc0 plus tard)
                fs.Position = 0;
                var data = br.ReadBytes((int)fs.Length);
                var text = System.Text.Encoding.GetEncoding("shift-jis").GetString(data);
                // On simule la détection de fichiers internes par regex sur les extensions courantes
                var matches = System.Text.RegularExpressions.Regex.Matches(text, @"[\w/\\.-]+\.(bmd|bch|msg|bin|dat|wav|ogg|png|jpg|bmp)");
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    if (!files.Contains(m.Value))
                        files.Add(m.Value);
                }
            }
            return files;
        }

        public Arc0Reader.ArchiveNode GetArchiveTree(string filePath)
        {
            var reader = new Arc0Reader();
            return reader.ReadArchiveTree(filePath);
        }

        public byte[] ExtractFile(string archivePath, string internalPath)
        {
            var reader = new Arc0Reader();
            using var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);
            
            // Lire le header
            reader.ReadHeader(br);
            
            // Lire les entrées de dossiers
            fs.Position = reader.Header.directoryEntriesOffset;
            var dirEntries = new List<Arc0Reader.Arc0DirectoryEntry>();
            for (int i = 0; i < reader.Header.directoryEntriesCount; i++)
            {
                var entry = new Arc0Reader.Arc0DirectoryEntry
                {
                    directoryNameStartOffset = br.ReadInt32(),
                    fileNameStartOffset = br.ReadInt32(),
                    fileCount = br.ReadInt16(),
                    firstFileIndex = br.ReadUInt16(),
                };
                br.BaseStream.Seek(4, SeekOrigin.Current); // skip unused
                dirEntries.Add(entry);
            }

            // Lire les entrées de fichiers
            fs.Position = reader.Header.fileEntriesOffset;
            var fileEntries = new List<Arc0Reader.Arc0FileEntry>();
            for (int i = 0; i < reader.Header.fileEntriesCount; i++)
            {
                var entry = new Arc0Reader.Arc0FileEntry
                {
                    nameOffsetInFolder = br.ReadInt32(),
                    fileOffset = br.ReadInt32(),
                    fileSize = br.ReadInt32(),
                };
                br.BaseStream.Seek(4, SeekOrigin.Current); // skip unused
                fileEntries.Add(entry);
            }

            // Lire la table des noms (décompression LZ10)
            fs.Position = reader.Header.nameOffset;
            var nameTableSize = reader.Header.dataOffset - reader.Header.nameOffset;
            var nameTableComp = br.ReadBytes(nameTableSize);
            
            byte[] nameTable;
            if (nameTableComp.Length > 0 && nameTableComp[0] == 0x10)
            {
                nameTable = Lz10.Decompress(nameTableComp);
            }
            else
            {
                nameTable = nameTableComp;
            }
            
            var nameStream = new MemoryStream(nameTable);
            var nameReader = new BinaryReader(nameStream, System.Text.Encoding.GetEncoding("shift-jis"));

            // Chercher le fichier par son chemin
            var pathParts = internalPath.Split('/');
            if (pathParts.Length != 2) // Format attendu: "dossier/fichier"
                throw new ArgumentException("Format de chemin invalide. Attendu: dossier/fichier");

            var targetDirName = pathParts[0];
            var targetFileName = pathParts[1];
            
            // Chercher le dossier
            Arc0Reader.Arc0DirectoryEntry targetDir = null;
            foreach (var dir in dirEntries)
            {
                nameStream.Position = dir.directoryNameStartOffset;
                var dirName = ReadNullTerminatedString(nameReader);
                if (dirName == targetDirName)
                {
                    targetDir = dir;
                    break;
                }
            }
            
            if (targetDir == null)
                throw new FileNotFoundException($"Dossier '{targetDirName}' non trouvé dans l'archive");

            // Chercher le fichier dans le dossier
            for (int i = 0; i < targetDir.fileCount; i++)
            {
                var fileIdx = targetDir.firstFileIndex + i;
                var fileEntry = fileEntries[fileIdx];
                nameStream.Position = targetDir.fileNameStartOffset + fileEntry.nameOffsetInFolder;
                var fileName = ReadNullTerminatedString(nameReader);
                
                if (fileName == targetFileName)
                {
                    // Extraire le fichier
                    fs.Position = reader.Header.dataOffset + fileEntry.fileOffset;
                    return br.ReadBytes(fileEntry.fileSize);
                }
            }
            
            throw new FileNotFoundException($"Fichier '{targetFileName}' non trouvé dans le dossier '{targetDirName}'");
        }

        private string ReadNullTerminatedString(BinaryReader br)
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = br.ReadByte()) != 0)
                bytes.Add(b);
            return System.Text.Encoding.GetEncoding("shift-jis").GetString(bytes.ToArray());
        }
    }
} 