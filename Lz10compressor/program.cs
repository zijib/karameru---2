using System;
using System.IO;

class Program
{
    static void Main()
    {
        byte[] raw = File.ReadAllBytes("name_table_raw.bin");
        byte[] comp = Lz10.Compress(raw);
        File.WriteAllBytes("name_table_lz10.bin", comp);

        Console.WriteLine("Compression OK !");
        Console.WriteLine($"Taille brute : {raw.Length} octets");
        Console.WriteLine($"Taille LZ10 : {comp.Length} octets");

        Console.WriteLine("Terminé !");
        Console.ReadLine();
    }
}
