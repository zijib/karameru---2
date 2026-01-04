using System;
using System.IO;

class Program
{
    static void Main()
    {
        string inputFaPath  = @"C:\Users\user\Documents\000400000019AC00\yw2_a.fa";
        string outputFaPath = @"C:\Users\user\Documents\000400000019AC00\yw2_a_patched.fa";

        string nameTablePath = @"C:\Users\user\Documents\karameru 2\karameru2\Lz10compressor\bin\Release\net9.0\name_table_raw.bin";

        if (!File.Exists(inputFaPath))
        {
            Console.WriteLine("Fichier .fa introuvable :");
            Console.WriteLine(inputFaPath);
            Console.ReadLine();
            return;
        }

        if (!File.Exists(nameTablePath))
        {
            Console.WriteLine("name_table_raw.bin introuvable :");
            Console.WriteLine(nameTablePath);
            Console.ReadLine();
            return;
        }

        byte[] nameTable = File.ReadAllBytes(nameTablePath);

        using var fsIn = new FileStream(inputFaPath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fsIn);

        fsIn.Seek(0x10, SeekOrigin.Begin);
        int directoryEntriesOffset = br.ReadInt32();
        int directoryHashOffset    = br.ReadInt32();
        int fileEntriesOffset      = br.ReadInt32();
        int nameOffset             = br.ReadInt32();
        int dataOffset             = br.ReadInt32();

        Console.WriteLine($"nameOffset = 0x{nameOffset:X}");
        Console.WriteLine($"dataOffset = 0x{dataOffset:X}");

        int originalNameSize = dataOffset - nameOffset;
        Console.WriteLine($"Taille table noms originale : {originalNameSize} octets");
        Console.WriteLine($"Taille name_table_raw.bin   : {nameTable.Length} octets");

        if (nameTable.Length != originalNameSize)
        {
            Console.WriteLine("ERREUR : la taille de name_table_raw.bin ne correspond pas à la taille originale.");
            Console.ReadLine();
            return;
        }

        using var fsOut = new FileStream(outputFaPath, FileMode.Create, FileAccess.Write);

        fsIn.Seek(0, SeekOrigin.Begin);
        CopyRange(fsIn, fsOut, nameOffset);

        fsOut.Write(nameTable, 0, nameTable.Length);

        fsIn.Seek(dataOffset, SeekOrigin.Begin);
        CopyRange(fsIn, fsOut, (int)(fsIn.Length - dataOffset));

        Console.WriteLine("Réinjection terminée !");
        Console.WriteLine("Fichier généré :");
        Console.WriteLine(outputFaPath);
        Console.ReadLine();
    }

    static void CopyRange(Stream input, Stream output, int bytesToCopy)
    {
        byte[] buffer = new byte[8192];
        int remaining = bytesToCopy;

        while (remaining > 0)
        {
            int toRead = Math.Min(remaining, buffer.Length);
            int read = input.Read(buffer, 0, toRead);
            if (read <= 0)
                break;

            output.Write(buffer, 0, read);
            remaining -= read;
        }
    }
}
