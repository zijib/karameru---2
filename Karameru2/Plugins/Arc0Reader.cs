using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Karameru2.Plugins
{
    public class Arc0Reader
    {
        // -----------------------------
        // Structures internes
        // -----------------------------

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

        public class HashEntry
        {
            public uint Hash;
            public uint NameOffset;
            public uint FileIndex;
        }

        public class ArchiveNode
        {
            public string Name = string.Empty;
            public bool IsDirectory;
            public List<ArchiveNode> Children = new List<ArchiveNode>();
            public int? FileIndex;

            public bool IsFile => !IsDirectory;
        }

        public Arc0Header Header { get; private set; } = new Arc0Header();

        // -----------------------------
        // Lecture du header ARC0
        // -----------------------------

        public void ReadHeader(BinaryReader br)
        {
            var stream = br.BaseStream;

            stream.Seek(0x10, SeekOrigin.Begin);

            Header = new Arc0Header
            {
                directoryEntriesOffset = br.ReadInt32(),
                directoryHashOffset    = br.ReadInt32(),
                fileEntriesOffset      = br.ReadInt32(),
                nameOffset             = br.ReadInt32(),
                dataOffset             = br.ReadInt32(),
            };

            stream.Seek(0x38, SeekOrigin.Begin);
            Header.directoryEntriesCount = br.ReadInt16();
            stream.Seek(0x3C, SeekOrigin.Begin);
            Header.fileEntriesCount = br.ReadInt32();
        }

        // -----------------------------
        // Lecture de la hash table
        // -----------------------------

        private List<HashEntry> ReadHashTable(BinaryReader br)
        {
            var list = new List<HashEntry>();

            br.BaseStream.Position = Header.directoryHashOffset;

            while (br.BaseStream.Position + 12 <= br.BaseStream.Length)
            {
                uint hash = br.ReadUInt32();
                uint nameOffset = br.ReadUInt32();
                uint fileIndex = br.ReadUInt32();

                if (hash == 0 && nameOffset == 0 && fileIndex == 0)
                    break;

                list.Add(new HashEntry
                {
                    Hash = hash,
                    NameOffset = nameOffset,
                    FileIndex = fileIndex
                });
            }

            return list;
        }

        // -----------------------------
        // Lecture de la table des noms
        // -----------------------------

        private byte[] ReadNameTable(BinaryReader br)
        {
            br.BaseStream.Position = Header.nameOffset;
            int size = Header.dataOffset - Header.nameOffset;

            var comp = br.ReadBytes(size);

            if (comp.Length > 0 && comp[0] == 0x10)
                return Lz10.Decompress(comp);

            return comp;
        }

        private string ReadStringAtOffset(BinaryReader br, long offset)
        {
            var s = br.BaseStream;

            if (offset < 0 || offset >= s.Length)
                return string.Empty;

            s.Position = offset;

            var bytes = new List<byte>();
            while (s.Position < s.Length)
            {
                byte b = br.ReadByte();
                if (b == 0)
                    break;
                bytes.Add(b);
            }

            return Encoding.GetEncoding("shift-jis").GetString(bytes.ToArray());
        }

        private string ReadNullTerminated(BinaryReader br)
        {
            var s = br.BaseStream;
            var bytes = new List<byte>();

            while (s.Position < s.Length)
            {
                byte b = br.ReadByte();
                if (b == 0)
                    break;
                bytes.Add(b);
            }

            return Encoding.GetEncoding("shift-jis").GetString(bytes.ToArray());
        }

        // -----------------------------
        // Lecture des entrées ARC0
        // -----------------------------

        private List<Arc0DirectoryEntry> ReadDirectories(BinaryReader br)
        {
            var list = new List<Arc0DirectoryEntry>();

            br.BaseStream.Position = Header.directoryEntriesOffset;

            for (int i = 0; i < Header.directoryEntriesCount; i++)
            {
                list.Add(new Arc0DirectoryEntry
                {
                    directoryNameStartOffset = br.ReadInt32(),
                    fileNameStartOffset      = br.ReadInt32(),
                    fileCount                = br.ReadInt16(),
                    firstFileIndex           = br.ReadUInt16(),
                });

                br.BaseStream.Seek(4, SeekOrigin.Current);
            }

            return list;
        }

        private List<Arc0FileEntry> ReadFiles(BinaryReader br)
        {
            var list = new List<Arc0FileEntry>();

            br.BaseStream.Position = Header.fileEntriesOffset;

            for (int i = 0; i < Header.fileEntriesCount; i++)
            {
                list.Add(new Arc0FileEntry
                {
                    nameOffsetInFolder = br.ReadInt32(),
                    fileOffset         = br.ReadInt32(),
                    fileSize           = br.ReadInt32(),
                });

                br.BaseStream.Seek(4, SeekOrigin.Current);
            }

            return list;
        }

        // -----------------------------
        // Reconstruction de l'arborescence
        // -----------------------------

        public ArchiveNode ReadArchiveTree(string filePath)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            ReadHeader(br);

            var dirs        = ReadDirectories(br);
            var files       = ReadFiles(br);
            var hashEntries = ReadHashTable(br);

            var nameTable = ReadNameTable(br);
            using var nameStream = new MemoryStream(nameTable);
            using var nameReader = new BinaryReader(nameStream, Encoding.GetEncoding("shift-jis"));

            var root = new ArchiveNode { Name = "root", IsDirectory = true };

            foreach (var dir in dirs)
            {
                // Dossier : hash avec FileIndex = firstFileIndex - 1
                var dirHash = hashEntries.FirstOrDefault(h => h.FileIndex == dir.firstFileIndex - 1);

                string dirName;
                if (dirHash != null)
                {
                    dirName = ReadStringAtOffset(nameReader, dirHash.NameOffset);
                }
                else
                {
                    // Fallback : ancien système, sécurisé
                    nameStream.Position = Math.Clamp(dir.directoryNameStartOffset, 0, (int)nameStream.Length);
                    dirName = ReadNullTerminated(nameReader);
                }

                if (!string.IsNullOrEmpty(dirName))
                    dirName = dirName.ToLowerInvariant();

                var dirNode = new ArchiveNode { Name = dirName, IsDirectory = true };

                for (int i = 0; i < dir.fileCount; i++)
                {
                    int fileIdx = dir.firstFileIndex + i;

                    var hashEntry = hashEntries.FirstOrDefault(h => h.FileIndex == fileIdx);

                    string fileName = hashEntry != null
                        ? ReadStringAtOffset(nameReader, hashEntry.NameOffset)
                        : $"file_{fileIdx}";

                    if (!string.IsNullOrEmpty(fileName))
                        fileName = fileName.ToLowerInvariant();

                    dirNode.Children.Add(new ArchiveNode
                    {
                        Name        = fileName,
                        IsDirectory = false,
                        FileIndex   = fileIdx
                    });
                }

                root.Children.Add(dirNode);
            }

            return root;
        }

        // -----------------------------
        // Extraction d’un fichier
        // -----------------------------

        public byte[] ExtractFile(string archivePath, int fileIndex)
        {
            using var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            ReadHeader(br);

            fs.Position = Header.fileEntriesOffset;
            var fileEntries = new List<Arc0FileEntry>();

            for (int i = 0; i < Header.fileEntriesCount; i++)
            {
                fileEntries.Add(new Arc0FileEntry
                {
                    nameOffsetInFolder = br.ReadInt32(),
                    fileOffset         = br.ReadInt32(),
                    fileSize           = br.ReadInt32(),
                });

                br.BaseStream.Seek(4, SeekOrigin.Current);
            }

            if (fileIndex < 0 || fileIndex >= fileEntries.Count)
                throw new ArgumentOutOfRangeException(nameof(fileIndex));

            var entry = fileEntries[fileIndex];

            fs.Position = Header.dataOffset + entry.fileOffset;
            return br.ReadBytes(entry.fileSize);
        }
    }
}
