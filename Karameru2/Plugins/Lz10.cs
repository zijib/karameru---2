using System;
using System.IO;

namespace Karameru2.Plugins
{
    public static class Lz10
    {
        // Décompresse un flux LZ10 (Nintendo)
        public static byte[] Decompress(byte[] input)
        {
            using var ms = new MemoryStream(input);
            using var br = new BinaryReader(ms);
            // Header LZ10 : [0]=0x10, [1-3]=taille décompressée (little endian)
            if (br.ReadByte() != 0x10)
                throw new InvalidDataException("Données LZ10 invalides (pas de header 0x10)");
            int decompressedSize = br.ReadByte() | (br.ReadByte() << 8) | (br.ReadByte() << 16);
            var output = new byte[decompressedSize];
            int outPos = 0;
            while (outPos < decompressedSize)
            {
                byte flag = br.ReadByte();
                for (int i = 0; i < 8; i++)
                {
                    if ((flag & (0x80 >> i)) == 0)
                    {
                        // Donnée brute
                        output[outPos++] = br.ReadByte();
                    }
                    else
                    {
                        // Donnée compressée
                        int b1 = br.ReadByte();
                        int b2 = br.ReadByte();
                        int disp = ((b1 & 0xF) << 8) | b2;
                        int len = (b1 >> 4) + 3;
                        int refPos = outPos - (disp + 1);
                        for (int j = 0; j < len; j++)
                        {
                            output[outPos++] = output[refPos + j];
                        }
                    }
                    if (outPos >= decompressedSize || br.BaseStream.Position >= br.BaseStream.Length)
                        break;
                }
            }
            return output;
        }
    }
} 