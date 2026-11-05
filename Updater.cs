using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace StickMouse;

static class UpdateClient
{
    internal static Version Current => typeof(UpdateClient).Assembly.GetName().Version!;
    internal static UpdateTrust Trust
    {
        get { using var stream=typeof(UpdateClient).Assembly.GetManifestResourceStream("StickMouse.UpdateTrust.json")!;return JsonSerializer.Deserialize<UpdateTrust>(stream)!; }
    }
    internal static string Root=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../.."));
    internal static string Cache=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StickMouse","updates");
    internal static async Task<string> Download(Uri url,string path,long limit,CancellationToken token)
    {
        using var client=new HttpClient {Timeout=TimeSpan.FromMinutes(3)};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("StickMouse/1.1");
        using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>limit)throw new IOException("SM209: Download too large.");
        await using var input=await response.Content.ReadAsStreamAsync(token);
        await using var output=new FileStream(path,FileMode.CreateNew);
        byte[] buffer=new byte[81920];long total=0;int n;
        while((n=await input.ReadAsync(buffer,token))>0){if((total+=n)>limit)throw new IOException("SM209: Download too large.");await output.WriteAsync(buffer.AsMemory(0,n),token);}
        return path;
    }
    internal static async Task<(UpdateManifest manifest,string envelope)> Check(CancellationToken token)
    {
        using var client=new HttpClient {Timeout=TimeSpan.FromSeconds(12),MaxResponseContentBufferSize=65536};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("StickMouse/1.1");
        string json=await client.GetStringAsync($"https://github.com/{Trust.Repository}/releases/latest/download/update.json",token);
        return (UpdateProtocol.Verify(json,Trust,Current),json);
    }
    internal static async Task<string> Prepare(UpdateManifest manifest,string envelope,CancellationToken token)
    {
        string dir=Path.Combine(Cache,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        string zip=await Download(new Uri(manifest.PackageUrl),Path.Combine(dir,"package.zip"),manifest.Size,token);
        UpdateProtocol.VerifyPackage(zip,manifest);UpdateProtocol.Extract(zip,Path.Combine(dir,"stage"));
        File.WriteAllText(Path.Combine(dir,"update.json"),envelope);return dir;
    }
    internal static void StartInstaller(string dir)
    {
        string helper=Path.Combine(dir,"helper");Directory.CreateDirectory(helper);
        foreach(string name in new[]{"StickMouse.dll","StickMouse.deps.json","StickMouse.runtimeconfig.json"})File.Copy(Path.Combine(AppContext.BaseDirectory,name),Path.Combine(helper,name),true);
        var job=new UpdateJob(Root,Environment.ProcessId,Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks);
        File.WriteAllText(Path.Combine(dir,"job.json"),JsonSerializer.Serialize(job));
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
        start.ArgumentList.Add(Path.Combine(helper,"StickMouse.dll"));start.ArgumentList.Add("--apply-update");start.ArgumentList.Add(dir);
        if(Process.Start(start) is null)throw new IOException("SM210: Cannot start installer.");
    }
    internal static void Apply(string dir)
    {
        var job=JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(Path.Combine(dir,"job.json")))!;
        var manifest=UpdateProtocol.Verify(File.ReadAllText(Path.Combine(dir,"update.json")),Trust,Current);
        UpdateProtocol.VerifyPackage(Path.Combine(dir,"package.zip"),manifest);
        string stage=Path.Combine(dir,"verified-"+Guid.NewGuid().ToString("N"));UpdateProtocol.Extract(Path.Combine(dir,"package.zip"),stage);
        try
        {
            using var parent=Process.GetProcessById(job.ParentPid);
            if(parent.StartTime.ToUniversalTime().Ticks==job.ParentStartUtc && !parent.WaitForExit(30000))throw new IOException("SM211: Close StickMouse before updating.");
        }
        catch(ArgumentException){ }
        using(var gate=new Mutex(true,"Local\\StickMouse.Desktop",out bool acquired))
        {
            if(!acquired)throw new IOException("SM211: Another instance is running.");
            if(!File.Exists(Path.Combine(job.Root,"Setup.ps1")))throw new IOException("SM212: Installation folder incomplete.");
            UpdateProtocol.ReplaceWithRollback(stage,job.Root,Path.Combine(dir,"backup"));
        }
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
        start.ArgumentList.Add(Path.Combine(job.Root,"bin/Release/net8.0-windows/StickMouse.dll"));Process.Start(start);
    }
    record UpdateJob(string Root,int ParentPid,long ParentStartUtc);
}

sealed partial class MouseWindow
{
    readonly Button updateButton=new(){FlatStyle=FlatStyle.Flat,Text="Check for updates",BackColor=Surface,ForeColor=Mint,TabIndex=6};
    readonly Label updateStatus=new(){ForeColor=Muted,AutoSize=false};
    readonly CancellationTokenSource updateCancellation=new();
    readonly System.Windows.Forms.Timer updateTimer=new(){Interval=6*60*60*1000};
    (UpdateManifest manifest,string envelope)? pendingUpdate;
    string? preparedUpdate;
    bool updateBusy;
    void InitializeUpdater()
    {
        updateButton.FlatAppearance.BorderSize=0;canvas.Controls.AddRange(new Control[]{updateButton,updateStatus});
        updateStatus.Text=$"v{UpdateClient.Current.ToString(3)} - Automatic checks";
        updateButton.Click+=async(_,_)=>await UpdateAction();
        updateTimer.Tick+=async(_,_)=>{if(pendingUpdate is null)await CheckUpdates();};
        if(!previewMode){Shown+=async(_,_)=>await CheckUpdates();updateTimer.Start();}
        FormClosed+=(_,_)=>{updateCancellation.Cancel();updateTimer.Stop();};
    }
    async Task CheckUpdates(Func<CancellationToken,Task<(UpdateManifest manifest,string envelope)>>? check = null)
    {
        if(updateBusy)return;
        if(string.IsNullOrWhiteSpace(UpdateClient.Trust.PublicKey)){updateStatus.Text="Update publishing not configured";return;}
        updateBusy=true;updateButton.Enabled=false;updateStatus.Text="Checking GitHub releases...";
        try
        {
            pendingUpdate=await (check ?? UpdateClient.Check)(updateCancellation.Token);
            if(!IsDisposed){updateStatus.Text=$"Version {pendingUpdate.Value.manifest.Version} available";updateButton.Text="Download update";}
        }
        catch(InvalidDataException e)when(e.Message.StartsWith("SM203")){if(!IsDisposed)updateStatus.Text="You have the latest version";}
        catch(Exception e)
        {
            // Update availability must never gate controller operation or normal startup.
            if(!IsDisposed){updateStatus.Text="Updates unavailable. Current version is ready.";LogUpdateFailure(e);}
        }
        finally{updateBusy=false;if(!IsDisposed)updateButton.Enabled=true;}
    }
    void LogUpdateFailure(Exception error)
    {
        try{string target=preferencesOverride is null?logPath:Path.Combine(Path.GetDirectoryName(preferencesOverride)!,"update-runtime.log");Directory.CreateDirectory(Path.GetDirectoryName(target)!);if(File.Exists(target)&&new FileInfo(target).Length>262144)File.Move(target,target+".old",true);File.AppendAllText(target,$"{DateTimeOffset.UtcNow:o} SM200 {error.GetType().Name}\n");}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){System.Diagnostics.Debug.WriteLine("SM200 diagnostic storage unavailable");}
    }
    async Task UpdateAction()
    {
        if(updateBusy)return;
        if(pendingUpdate is null){await CheckUpdates();return;}
        if(preparedUpdate is null)
        {
            var candidate=pendingUpdate.Value;
            if(MessageBox.Show(this,candidate.manifest.Notes+"\n\nDownload this update?","StickMouse "+candidate.manifest.Version,MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
            updateBusy=true;updateButton.Enabled=false;updateStatus.Text="Downloading and verifying...";
            try{preparedUpdate=await UpdateClient.Prepare(candidate.manifest,candidate.envelope,updateCancellation.Token);if(!IsDisposed){updateStatus.Text="Verified update ready";updateButton.Text="Restart & update";}}
            catch(Exception e){if(!IsDisposed){updateStatus.Text="Download failed. Current version is ready.";LogUpdateFailure(e);}}
            finally{updateBusy=false;if(!IsDisposed)updateButton.Enabled=true;}
            return;
        }
        if(enabled.Checked){MessageBox.Show(this,"Stop controller mouse mode before installing the update.","StickMouse");return;}
        try{SavePreferences();StopInput();UpdateClient.StartInstaller(preparedUpdate);Close();}
        catch(Exception e){LogUpdateFailure(e);updateStatus.Text="Installer could not start. Current version is ready.";timer.Start();}
    }
}
