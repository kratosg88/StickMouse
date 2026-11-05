using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StickMouse;

static class UpdateTests
{
    internal static void Run()
    {
        using var key=RSA.Create(2048);
        var trust=new UpdateTrust("kratosg88/StickMouse",key.ExportSubjectPublicKeyInfoPem());
        var current=new Version(1,1,0,0);
        string dir=Path.Combine(AppContext.BaseDirectory,".verify","update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        string zip=Path.Combine(dir,"package.zip");
        using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create))foreach(string file in UpdateProtocol.Files){using var w=new StreamWriter(archive.CreateEntry(file).Open());w.Write("new-content");}
        var m=new UpdateManifest("1.1.1","https://github.com/kratosg88/StickMouse/releases/download/v1.1.1/StickMouse-Portable.zip",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip))),new FileInfo(zip).Length,"Test release");
        string Sign(UpdateManifest manifest){byte[] data=JsonSerializer.SerializeToUtf8Bytes(manifest);return JsonSerializer.Serialize(new UpdateEnvelope(Convert.ToBase64String(data),Convert.ToBase64String(key.SignData(data,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))));}
        void Reject(Action action){try{action();}catch(Exception e)when(e is InvalidDataException or CryptographicException or FormatException){return;}throw new Exception("Updater accepted invalid input");}
        var verified=UpdateProtocol.Verify(Sign(m),trust,current);UpdateProtocol.VerifyPackage(zip,verified);
        Reject(()=>UpdateProtocol.Verify(Sign(m with {Version="1.0.0"}),trust,current));
        Reject(()=>UpdateProtocol.Verify(Sign(m with {PackageUrl="https://example.com/payload.zip"}),trust,current));
        var env=JsonSerializer.Deserialize<UpdateEnvelope>(Sign(m))!;byte[] sig=Convert.FromBase64String(env.Signature);sig[0]^=1;
        Reject(()=>UpdateProtocol.Verify(JsonSerializer.Serialize(env with {Signature=Convert.ToBase64String(sig)}),trust,current));
        Reject(()=>UpdateProtocol.VerifyPackage(zip,m with {Sha256=new string('0',64)}));
        string stage=Path.Combine(dir,"stage");UpdateProtocol.Extract(zip,stage);
        string unsafeZip=Path.Combine(dir,"unsafe.zip");
        using(var a=ZipFile.Open(unsafeZip,ZipArchiveMode.Create)){using var w=new StreamWriter(a.CreateEntry("../escape.txt").Open());w.Write("no");}
        Reject(()=>UpdateProtocol.Extract(unsafeZip,Path.Combine(dir,"unsafe-stage")));
        if(File.Exists(Path.Combine(dir,"escape.txt")))throw new Exception("Archive escaped staging");
        string target=Path.Combine(dir,"installation");Directory.CreateDirectory(target);
        foreach(string name in UpdateProtocol.Files){string p=Path.Combine(target,name);Directory.CreateDirectory(Path.GetDirectoryName(p)!);File.WriteAllText(p,"old-content");}
        File.WriteAllText(Path.Combine(target,"user-data.txt"),"keep");
        try{UpdateProtocol.ReplaceWithRollback(stage,target,Path.Combine(dir,"rollback"),n=>{if(n==3)throw new IOException("Injected write failure");});throw new Exception("Failure injection ignored");}catch(IOException){}
        foreach(string name in UpdateProtocol.Files)if(File.ReadAllText(Path.Combine(target,name))!="old-content")throw new Exception("Rollback did not restore old file");
        UpdateProtocol.ReplaceWithRollback(stage,target,Path.Combine(dir,"backup"));
        foreach(string name in UpdateProtocol.Files)if(File.ReadAllText(Path.Combine(target,name))!="new-content")throw new Exception("Update installation failed");
        if(File.ReadAllText(Path.Combine(target,"user-data.txt"))!="keep")throw new Exception("Unrelated data changed");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"update-test.txt"),"PASS: signatures, downgrade/foreign URL/hash rejection, safe extraction, injected-failure rollback, successful replacement, user-data preservation");
    }
}
