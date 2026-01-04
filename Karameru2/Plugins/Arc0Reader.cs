using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Karameru2.Plugins
{
    public class Arc0Reader
    {
        public class Arc0Header
        {
            public int directoryEntriesOffset;
            public int directoryHashOffset;
            public int fileEntriesOffset;
            public int nameOffset;
            public int dataOffset;
            public short directoryEntriesCount;
            public int fileEntriesCount;
        }

        public class Arc0DirectoryEntry
        {
            public int directoryNameStartOffset;
            public int fileNameStartOffset;
            public short fileCount;
            public ushort firstFileIndex;
        }

        public class Arc0FileEntry
        {
            public int nameOffsetInFolder;
            public int fileOffset;
            public int fileSize;
        }

        public class ArchiveNode
        {
            public string Name;
            public bool IsDirectory;
            public List<ArchiveNode> Children = new List<ArchiveNode>();
            public int? FileIndex; // null pour les dossiers
        }

        public Arc0Header Header { get; private set; }

        public void ReadHeader(BinaryReader br)
        {
            var stream = br.BaseStream;
            if (stream.Length < 0x40)
                throw new InvalidDataException("Fichier trop petit pour contenir un header ARC0 valide.");

            // Lecture simplifiée du header (offsets principaux)
            stream.Seek(0x10, SeekOrigin.Begin); // skip magic, version, etc.
            Header = new Arc0Header
            {
                directoryEntriesOffset = br.ReadInt32(),
                directoryHashOffset   = br.ReadInt32(),
                fileEntriesOffset     = br.ReadInt32(),
                nameOffset            = br.ReadInt32(),
                dataOffset            = br.ReadInt32(),
            };

            stream.Seek(0x38, SeekOrigin.Begin); // position des counts
            Header.directoryEntriesCount = br.ReadInt16();
            stream.Seek(0x3C, SeekOrigin.Begin);
            Header.fileEntriesCount = br.ReadInt32();

            // Sanity checks basiques
            if (Header.directoryEntriesOffset < 0 ||
                Header.fileEntriesOffset < 0 ||
                Header.nameOffset < 0 ||
                Header.dataOffset < 0 ||
                Header.nameOffset > Header.dataOffset ||
                Header.dataOffset > stream.Length)
            {
                throw new InvalidDataException("Header ARC0 invalide (offsets incohérents).");
            }

            if (Header.directoryEntriesCount < 0 || Header.fileEntriesCount < 0)
                throw new InvalidDataException("Header ARC0 invalide (counts négatifs).");
        }

        public List<(string Path, bool IsDirectory)> ListEntries(string filePath)
        {
            var result = new List<(string, bool)>();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            ReadHeader(br);

            // EXTRACTION DE LA TABLE DES NOMS BRUTE
            fs.Position = Header.nameOffset;
            int rawSize = Header.dataOffset - Header.nameOffset;
            if (rawSize > 0 && fs.Position + rawSize <= fs.Length)
            {
                byte[] nameTableRaw = br.ReadBytes(rawSize);
                File.WriteAllBytes("name_table_raw.bin", nameTableRaw);
            }
            // FIN EXTRACTION

            // --- Lire les entrées de dossiers ---
            fs.Position = Header.directoryEntriesOffset;
            var dirEntries = new List<Arc0DirectoryEntry>();

            // Taille théorique d'une entrée de dossier : 4 + 4 + 2 + 2 + 4 (unused) = 16 octets
            long bytesNeededForDirs = Header.directoryEntriesCount * 16L;
            if (fs.Position + bytesNeededForDirs > fs.Length)
                throw new InvalidDataException("Fichier ARC0 invalide : zone des dossiers dépasse la taille du fichier.");

            for (int i = 0; i < Header.directoryEntriesCount; i++)
            {
                var entry = new Arc0DirectoryEntry
                {
                    directoryNameStartOffset = br.ReadInt32(),
                    fileNameStartOffset      = br.ReadInt32(),
                    fileCount                = br.ReadInt16(),
                    firstFileIndex           = br.ReadUInt16(),
                };
                br.BaseStream.Seek(4, SeekOrigin.Current); // skip unused
                dirEntries.Add(entry);
            }

            // --- Lire les entrées de fichiers ---
            fs.Position = Header.fileEntriesOffset;
            var fileEntries = new List<Arc0FileEntry>();

            // Taille théorique d'une entrée de fichier : 4 + 4 + 4 + 4 (unused) = 16 octets
            long bytesNeededForFiles = Header.fileEntriesCount * 16L;
            if (fs.Position + bytesNeededForFiles > fs.Length)
                throw new InvalidDataException("Fichier ARC0 invalide : zone des fichiers dépasse la taille du fichier.");

            for (int i = 0; i < Header.fileEntriesCount; i++)
            {
                var entry = new Arc0FileEntry
                {
                    nameOffsetInFolder = br.ReadInt32(),
                    fileOffset         = br.ReadInt32(),
                    fileSize           = br.ReadInt32(),
                };
                br.BaseStream.Seek(4, SeekOrigin.Current); // skip unused
                fileEntries.Add(entry);
            }

            // --- Lire la table des noms (décompression LZ10) ---
            fs.Position = Header.nameOffset;
            var nameTableSize = Header.dataOffset - Header.nameOffset;
            if (nameTableSize <= 0 || fs.Position + nameTableSize > fs.Length)
                throw new InvalidDataException("Fichier ARC0 invalide : taille de table des noms incorrecte.");

            var nameTableComp = br.ReadBytes(nameTableSize);
            if (nameTableComp.Length == 0)
                throw new InvalidDataException("Table des noms vide.");

            if (nameTableComp[0] != 0x10)
                throw new InvalidDataException(
                    $"Table des noms non LZ10 : offset=0x{Header.nameOffset:X}, premier octet=0x{nameTableComp[0]:X2}");

            var nameTable = Lz10.Decompress(nameTableComp);
            using var nameStream = new MemoryStream(nameTable);
            using var nameReader = new BinaryReader(nameStream, Encoding.GetEncoding("shift-jis"));

            // --- Reconstituer l'arborescence plate ---
            foreach (var dir in dirEntries)
            {
                if (dir.directoryNameStartOffset < 0 || dir.directoryNameStartOffset >= nameStream.Length)
                    continue; // on évite les crashs si offsets foireux

                nameStream.Position = dir.directoryNameStartOffset;
                var dirName = SafeReadNullTerminatedString(nameReader, nameStream);

                if (!string.IsNullOrWhiteSpace(dirName))
                    result.Add((dirName, true));

                for (int i = 0; i < dir.fileCount; i++)
                {
                    int fileIdx = dir.firstFileIndex + i;
                    if (fileIdx < 0 || fileIdx >= fileEntries.Count)
                        continue; // évite IndexOutOfRange

                    var fileEntry = fileEntries[fileIdx];
                    long namePos = dir.fileNameStartOffset + fileEntry.nameOffsetInFolder;

                    if (namePos < 0 || namePos >= nameStream.Length)
                        continue;

                    nameStream.Position = namePos;
                    var fileName = SafeReadNullTerminatedString(nameReader, nameStream);
                    result.Add(($"{dirName}{fileName}", false));
                }
            }

            return result;
        }

        public ArchiveNode ReadArchiveTree(string filePath)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            ReadHeader(br);

            // EXTRACTION DE LA TABLE DES NOMS BRUTE
            fs.Position = Header.nameOffset;
            int rawSize = Header.dataOffset - Header.nameOffset;
            if (rawSize > 0 && fs.Position + rawSize <= fs.Length)
            {
                byte[] nameTableRaw = br.ReadBytes(rawSize);
                File.WriteAllBytes("name_table_raw.bin", nameTableRaw);
            }
            // FIN EXTRACTION

            // --- Lire les entrées de dossiers ---
            fs.Position = Header.directoryEntriesOffset;
            var dirEntries = new List<Arc0DirectoryEntry>();

            long bytesNeededForDirs = Header.directoryEntriesCount * 16L;
            if (fs.Position + bytesNeededForDirs > fs.Length)
                throw new InvalidDataException("Fichier ARC0 invalide : zone des dossiers dépasse la taille du fichier.");

            for (int i = 0; i < Header.directoryEntriesCount; i++)
            {
                var entry = new Arc0DirectoryEntry
                {
                    directoryNameStartOffset = br.ReadInt32(),
                    fileNameStartOffset      = br.ReadInt32(),
                    fileCount                = br.ReadInt16(),
                    firstFileIndex           = br.ReadUInt16(),
                };
                br.BaseStream.Seek(4, SeekOrigin.Current); // skip unused
                dirEntries.Add(entry);
            }

            // --- Lire les entrées de fichiers ---
            fs.Position = Header.fileEntriesOffset;
            var fileEntries = new List<Arc0FileEntry>();

            long bytesNeededForFiles = Header.fileEntriesCount * 16L;
            if (fs.Position + bytesNeededForFiles > fs.Length)
                throw new InvalidDataException("Fichier ARC0 invalide : zone des fichiers dépasse la taille du fichier.");

            for (int i = 0; i < Header.fileEntriesCount; i++)
            {
                var entry = new Arc0FileEntry
                {
                    nameOffsetInFolder = br.ReadInt32(),
                    fileOffset         = br.ReadInt32(),
                    fileSize           = br.ReadInt32(),
                };
                br.BaseStream.Seek(4, SeekOrigin.Current); // skip unused
                fileEntries.Add(entry);
            }

            // --- Lire la table des noms (décompression LZ10 ou brut) ---
            fs.Position = Header.nameOffset;
            var nameTableSize = Header.dataOffset - Header.nameOffset;
            if (nameTableSize <= 0 || fs.Position + nameTableSize > fs.Length)
                throw new InvalidDataException("Fichier ARC0 invalide : taille de table des noms incorrecte.");

            var nameTableComp = br.ReadBytes(nameTableSize);
            if (nameTableComp.Length == 0)
                throw new InvalidDataException("Table des noms vide.");

            byte[] nameTable;
            if (nameTableComp[0] == 0x10)
            {
                nameTable = Lz10.Decompress(nameTableComp);
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Avertissement : Table des noms non LZ10 (offset=0x{Header.nameOffset:X}, premier octet=0x{nameTableComp[0]:X2}).\nTentative de lecture brute.",
                    "Archive .fa inhabituelle",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);

                nameTable = nameTableComp;
            }

            using var nameStream = new MemoryStream(nameTable);
            using var nameReader = new BinaryReader(nameStream, Encoding.GetEncoding("shift-jis"));

            // --- Construction de l'arbre ---
            var root = new ArchiveNode { Name = "root", IsDirectory = true };

            foreach (var dir in dirEntries)
            {
                if (dir.directoryNameStartOffset < 0 || dir.directoryNameStartOffset >= nameStream.Length)
                    continue;

                nameStream.Position = dir.directoryNameStartOffset;
                var dirName = SafeReadNullTerminatedString(nameReader, nameStream);
                if (dirName == null)
                    dirName = $"dir_{root.Children.Count}";

                var dirNode = new ArchiveNode { Name = dirName, IsDirectory = true };

                for (int i = 0; i < dir.fileCount; i++)
                {
                    int fileIdx = dir.firstFileIndex + i;
                    if (fileIdx < 0 || fileIdx >= fileEntries.Count)
                        continue;

                    var fileEntry = fileEntries[fileIdx];
                    long namePos = dir.fileNameStartOffset + fileEntry.nameOffsetInFolder;

                    if (namePos < 0 || namePos >= nameStream.Length)
                        continue;

                    nameStream.Position = namePos;
                    var fileName = SafeReadNullTerminatedString(nameReader, nameStream) ?? $"file_{fileIdx}";

                    dirNode.Children.Add(new ArchiveNode
                    {
                        Name = fileName,
                        IsDirectory = false,
                        FileIndex = fileIdx
                    });
                }

                root.Children.Add(dirNode);
            }

            return root;
        }

        private string ReadNullTerminatedString(BinaryReader br)
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = br.ReadByte()) != 0)
                bytes.Add(b);
            return Encoding.GetEncoding("shift-jis").GetString(bytes.ToArray());
        }

        // Version "safe" qui ne dépasse jamais la fin du stream
        private string? SafeReadNullTerminatedString(BinaryReader br, Stream s)
        {
            var bytes = new List<byte>();
            while (s.Position < s.Length)
            {
                byte b = br.ReadByte();
                if (b == 0)
                    break;
                bytes.Add(b);
            }

            if (bytes.Count == 0)
                return string.Empty;

            return Encoding.GetEncoding("shift-jis").GetString(bytes.ToArray());
        }
    }
}
