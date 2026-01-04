using System;
using System.IO;

public static class Lz10
{
    public static byte[] Compress(byte[] input)
    {
        using var output = new MemoryStream();
        output.WriteByte(0x10); // LZ10 signature

        // Write uncompressed size (little endian)
        output.WriteByte((byte)(input.Length & 0xFF));
        output.WriteByte((byte)((input.Length >> 8) & 0xFF));
        output.WriteByte((byte)((input.Length >> 16) & 0xFF));

        int pos = 0;
        while (pos < input.Length)
        {
            int flagPos = (int)output.Position;
            output.WriteByte(0); // placeholder for flags
            byte flags = 0;

            for (int i = 0; i < 8; i++)
            {
                if (pos >= input.Length)
                    break;

                int bestLength = 0;
                int bestDisp = 0;

                int maxDisp = Math.Min(pos, 0xFFF);
                for (int disp = 1; disp <= maxDisp; disp++)
                {
                    int length = 0;
                    while (length < 18 &&
                           pos + length < input.Length &&
                           input[pos + length] == input[pos + length - disp])
                    {
                        length++;
                    }

                    if (length >= 3 && length > bestLength)
                    {
                        bestLength = length;
                        bestDisp = disp;
                    }
                }

                if (bestLength >= 3)
                {
                    flags |= (byte)(1 << (7 - i));
                    int lengthField = bestLength - 3;
                    int dispField = bestDisp - 1;

                    output.WriteByte((byte)(((lengthField << 4) | (dispField >> 8)) & 0xFF));
                    output.WriteByte((byte)(dispField & 0xFF));

                    pos += bestLength;
                }
                else
                {
                    output.WriteByte(input[pos]);
                    pos++;
                }
            }

            long cur = output.Position;
            output.Position = flagPos;
            output.WriteByte(flags);
            output.Position = cur;
        }

        return output.ToArray();
    }
}
