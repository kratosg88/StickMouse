using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace StickMouse;

record UpdateTrust(string Repository, string PublicKey);
record UpdateEnvelope(string Payload, string Signature);
record UpdateManifest(string Version, string PackageUrl, string Sha256, long Size, string Notes);

static class UpdateProtocol
{
    internal static readonly string[] Files = { "Launch.cmd", "Setup.ps1", "README.md", "bin/Release/net8.0-windows/StickMouse.dll", "bin/Release/net8.0-windows/StickMouse.deps.json", "bin/Release/net8.0-windows/StickMouse.runtimeconfig.json" };
    internal const long MaxPackage = 128 * 1024 * 1024;
    internal static UpdateManifest Verify(string json, UpdateTrust trust, Version current)
    {
        if (json.Length > 65536) throw new InvalidDataException("SM201: Manifest too large.");
        var envelope = JsonSerializer.Deserialize<UpdateEnvelope>(json) ?? throw new InvalidDataException("SM201: Empty manifest.");
        byte[] data = Convert.FromBase64String(envelope.Payload);
        using var rsa = RSA.Create(); if(trust.PublicKey.StartsWith("<RSAKeyValue>"))rsa.FromXmlString(trust.PublicKey);else rsa.ImportFromPem(trust.PublicKey);
        if (!rsa.VerifyData(data, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) throw new CryptographicException("SM202: Signature rejected.");
        var m = JsonSerializer.Deserialize<UpdateManifest>(data) ?? throw new InvalidDataException("SM201: Invalid manifest.");
        if (!Version.TryParse(m.Version, out var version) || version <= current) throw new InvalidDataException("SM203: No newer update.");
        if (m.Size <= 0 || m.Size > MaxPackage || m.Sha256 is null || !System.Text.RegularExpressions.Regex.IsMatch(m.Sha256,"^[a-fA-F0-9]{64}$") || m.Notes is null || m.Notes.Length > 8000) throw new InvalidDataException("SM204: Invalid package metadata.");
        if (m.PackageUrl != $"https://github.com/{trust.Repository}/releases/download/v{m.Version}/StickMouse-Portable.zip") throw new InvalidDataException("SM205: Package URL rejected.");
        return m;
    }
    internal static void VerifyPackage(string path, UpdateManifest m)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length != m.Size || !Convert.ToHexString(SHA256.HashData(stream)).Equals(m.Sha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SM206: Package integrity check failed.");
    }
    internal static void Extract(string path, string destination)
    {
        using var archive = ZipFile.OpenRead(path);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
        foreach(var entry in archive.Entries)
        {
            if(entry.FullName.EndsWith('/')) continue;
            string name = entry.FullName.Replace('\\','/');
            if(name.StartsWith("StickMouse/",StringComparison.Ordinal)) name = name[11..];
            if(!Files.Contains(name,StringComparer.Ordinal) || !seen.Add(name) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (total += entry.Length) > 256 * 1024 * 1024) throw new InvalidDataException("SM207: Unsafe or unexpected archive content.");
            string target = Path.Combine(destination,name.Replace('/',Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); entry.ExtractToFile(target,false);
        }
        if(seen.Count != Files.Length) throw new InvalidDataException("SM207: Incomplete package.");
    }
    internal static void ReplaceWithRollback(string stage,string target,string backup,Action<int>? afterCopy=null)
    {
        void NoLinks(string path)
        {
            for(var dir=new DirectoryInfo(Path.GetFullPath(path));dir is not null;dir=dir.Parent)
                if(dir.Exists && dir.Attributes.HasFlag(FileAttributes.ReparsePoint))throw new IOException("SM208: Linked installation path rejected.");
        }
        NoLinks(target);
        foreach(string name in Files)
        {
            string file=Path.Combine(target,name);NoLinks(Path.GetDirectoryName(file)!);
            if(File.Exists(file) && File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint))throw new IOException("SM208: Linked application file rejected.");
            string saved=Path.Combine(backup,name);Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
            if(File.Exists(file))File.Copy(file,saved,false);
        }
        var touched=new List<string>();
        try
        {
            foreach(string name in Files)
            {
                string file=Path.Combine(target,name);Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                touched.Add(name);File.Copy(Path.Combine(stage,name),file,true);afterCopy?.Invoke(touched.Count);
            }
        }
        catch
        {
            foreach(string name in touched.AsEnumerable().Reverse())
            {string saved=Path.Combine(backup,name),file=Path.Combine(target,name);if(File.Exists(saved))File.Copy(saved,file,true);else File.Delete(file);}
            throw;
        }
    }
}
