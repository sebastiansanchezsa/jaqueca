using Jaqueca.Sprites;
using System.IO.Compression;

namespace Jaqueca.ArtGen;

/// <summary>Escritor de PNG mínimo (RGBA 8 bits, sin filtros) usando zlib del framework.</summary>
public static class Png
{
    private static readonly uint[] CrcTable = BuildCrc();

    public static void Write(string path, Canvas c)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var fs = File.Create(path);
        fs.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        var ihdr = new byte[13];
        WriteBE(ihdr, 0, (uint)c.W);
        WriteBE(ihdr, 4, (uint)c.H);
        ihdr[8] = 8;  // bits
        ihdr[9] = 6;  // RGBA
        Chunk(fs, "IHDR", ihdr);

        var raw = new byte[(c.W * 4 + 1) * c.H];
        int o = 0;
        for (int y = 0; y < c.H; y++)
        {
            raw[o++] = 0;
            for (int x = 0; x < c.W; x++)
            {
                uint p = c.Px[y * c.W + x];
                raw[o++] = Col.R(p);
                raw[o++] = Col.G(p);
                raw[o++] = Col.B(p);
                raw[o++] = Col.A(p);
            }
        }
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);
        Chunk(fs, "IDAT", ms.ToArray());
        Chunk(fs, "IEND", Array.Empty<byte>());
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBE(len, 0, (uint)data.Length);
        s.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        uint crc = 0xFFFFFFFF;
        crc = Crc(crc, typeBytes);
        crc = Crc(crc, data);
        var crcBytes = new byte[4];
        WriteBE(crcBytes, 0, crc ^ 0xFFFFFFFF);
        s.Write(crcBytes);
    }

    private static void WriteBE(byte[] b, int o, uint v)
    {
        b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
    }

    private static uint Crc(uint crc, byte[] data)
    {
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildCrc()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }
}
