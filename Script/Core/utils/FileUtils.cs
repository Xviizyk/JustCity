#nullable enable

using Godot;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using RandomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator;

namespace JustCity.Script.Core.Utils;

public static class FileUtils
{
    private static readonly byte[] Magic = "XV.STUDIOS.JUST.CITY"u8.ToArray();
    private static readonly byte[] Key = SHA256.HashData(Encoding.UTF8.GetBytes("TetoDayo"));
    private const int NonceSize = 12, TagSize = 16;

    private static string Resolve(string path)
    {
        bool editor = OS.HasFeature("editor");
        string dir;
        if (editor) dir = ProjectSettings.GlobalizePath("res://");
        else
        {
            string? exeDir = Path.GetDirectoryName(OS.GetExecutablePath());
            if (string.IsNullOrEmpty(exeDir)) throw new InvalidOperationException("File is corrupted");
            dir = exeDir;
        }
        if (!editor && OS.GetName() == "macOS") dir = Path.Combine(dir, "../../..");
        dir = Path.GetFullPath(dir);
        string full = Path.GetFullPath(Path.Combine(dir, (Core.Instance.IsDebug ? "tmp/" : "") + path));
        string rel = Path.GetRelativePath(dir, full);
        if (rel == ".." || rel.StartsWith(".." + Path.DirectorySeparatorChar)) throw new UnauthorizedAccessException("File is corrupted");
        return full;
    }

    public static Stream OpenAppend(string path)
    {
        string full = Resolve(path);
        string? dir = Path.GetDirectoryName(full);
        if (string.IsNullOrEmpty(dir)) throw new InvalidOperationException("File is corrupted");
        Directory.CreateDirectory(dir);
        return new FileStream(full, FileMode.Append, System.IO.FileAccess.Write, FileShare.Read);
    }

    public static string GetFullPath(string path) => Resolve(path);

    public static void Move(string from, string to, bool overwrite = false)
    {
        string fullFrom = Resolve(from);
        string fullTo = Resolve(to);
        if (!File.Exists(fullFrom)) return;
        string? dir = Path.GetDirectoryName(fullTo);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.Move(fullFrom, fullTo, overwrite);
    }

    public static bool Exists(string path) => File.Exists(Resolve(path));

    public static byte[]? Load(string path)
    {
        string full = Resolve(path);
        if (!File.Exists(full)) return null;
        byte[] data = File.ReadAllBytes(full);
        if (!data.AsSpan().StartsWith(Magic)) return data;
        return Decrypt(data.AsSpan(Magic.Length));
    }

    public static void Save(string path, byte[] data, bool encrypt = true)
    {
        if (Core.Instance.IsDebug) encrypt = false;
        string full = Resolve(path);
        string? dir = Path.GetDirectoryName(full);
        if (string.IsNullOrEmpty(dir)) throw new InvalidOperationException("File is corrupted");
        Directory.CreateDirectory(dir);
        string tmp = full + ".tmp";
        if (encrypt)
        {
            byte[] payload = new byte[Magic.Length + NonceSize + TagSize + data.Length];
            Magic.CopyTo(payload, 0);
            Encrypt(data, payload.AsSpan(Magic.Length));
            File.WriteAllBytes(tmp, payload);
        }
        else File.WriteAllBytes(tmp, data);
        File.Move(tmp, full, true);
    }

    public static void Delete(string path)
    {
        string full = Resolve(path);
        if (File.Exists(full)) File.Delete(full);
    }

    private static void Encrypt(ReadOnlySpan<byte> plain, Span<byte> destination)
    {
        if (destination.Length < NonceSize + TagSize + plain.Length) throw new ArgumentException("File is corrupted");
        Span<byte> nonce = destination[..NonceSize];
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(Key.AsSpan(), TagSize);
        aes.Encrypt(nonce, plain, destination[(NonceSize + TagSize)..], destination.Slice(NonceSize, TagSize));
    }

    private static byte[] Decrypt(ReadOnlySpan<byte> data)
    {
        if (data.Length < NonceSize + TagSize) throw new InvalidDataException("File is corrupted");
        byte[] plain = new byte[data.Length - NonceSize - TagSize];
        using var aes = new AesGcm(Key.AsSpan(), TagSize);
        aes.Decrypt(data[..NonceSize], data[(NonceSize + TagSize)..], data.Slice(NonceSize, TagSize), plain);
        return plain;
    }
}
