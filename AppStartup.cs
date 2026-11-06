using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace StickMouse;

static class AppStartup
{
    internal static readonly uint RestoreMessage=RegisterWindowMessage("StickMouse.RestoreWindow");
    internal static readonly uint SmokeQuitMessage=RegisterWindowMessage("StickMouse.StartupSmoke.Quit");
    internal static string? LaunchToken;
    internal static string? DataOverride;
    internal static string Data => DataOverride ?? Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StickMouse");
    internal static void Log(string code,string detail)
    {
        try{Directory.CreateDirectory(Data);string path=Path.Combine(Data,"runtime.log");if(File.Exists(path)&&new FileInfo(path).Length>262144)File.Move(path,path+".old",true);File.AppendAllText(path,$"{DateTimeOffset.UtcNow:o} {code} pid={Environment.ProcessId} version={UpdateClient.Current.ToString(3)} {detail}\n");}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){Debug.WriteLine(code+" logging unavailable");}
    }
    internal static void Ready(string status,IntPtr handle,bool trayVisible=false)
    {
        bool visible=IsWindowVisible(handle);Log("SM_READY",$"status={status} nativeVisible={visible}");
        if(LaunchToken is null || !Guid.TryParseExact(LaunchToken,"N",out _) || (!visible && !(status=="tray" && trayVisible)))return;
        GetWindowThreadProcessId(handle,out uint pid);
        try{
            string folder=Path.Combine(Data,"startup");Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,LaunchToken+".json");
            File.WriteAllText(path+".tmp",JsonSerializer.Serialize(new{Status=status,NativeVisible=visible,TrayIconVisible=trayVisible,ProcessId=pid,WindowHandle=handle.ToInt64(),Version=UpdateClient.Current.ToString(3)}));
            File.Move(path+".tmp",path,true);
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException){Log("SM114",e.GetType().Name);}
    }
    internal static void Attach(MouseWindow window)
    {
        window.Shown+=(_,_)=>window.BeginInvoke((Action)(()=>{
            if(window.StartInTray){window.HideToTray();window.Opacity=1;Ready("tray",window.Handle,true);return;}
            // Override an inherited SW_HIDE only after Windows consumed its first-show flag.
            ShowWindow(window.Handle,IsIconic(window.Handle)?9:5);
            window.Activate();SetForegroundWindow(window.Handle);Ready("visible",window.Handle);
        }));
    }
    internal static bool RestoreExisting(bool smoke=false)
    {
        for(int attempt=0;attempt<24;attempt++)
        {
            IntPtr found=IntPtr.Zero;
            EnumWindows((handle,_)=>{
                GetWindowThreadProcessId(handle,out uint pid);if(pid==Environment.ProcessId)return true;
                var title=new StringBuilder(128);var klass=new StringBuilder(128);
                GetWindowText(handle,title,title.Capacity);GetClassName(handle,klass,klass.Capacity);
                if(title.ToString()==(smoke?"StickMouse [startup test]":"StickMouse") && klass.ToString().StartsWith("WindowsForms10.Window.",StringComparison.Ordinal)){found=handle;return false;}return true;
            },IntPtr.Zero);
            if(found!=IntPtr.Zero)
            {
                if(SendMessageTimeout(found,RestoreMessage,IntPtr.Zero,IntPtr.Zero,2,1000,out var handled)==IntPtr.Zero || handled==IntPtr.Zero)ShowWindowAsync(found,9);
                SetForegroundWindow(found);
                for(int i=0;i<20;i++){if(IsWindowVisible(found)){Ready("restored",found);return true;}Thread.Sleep(25);}
            }
            Thread.Sleep(50);
        }
        Log("SM013","existing_window_not_responsive");return false;
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessageTimeout(IntPtr handle,uint message,IntPtr wParam,IntPtr lParam,uint flags,uint timeout,out IntPtr result);
    delegate bool EnumCallback(IntPtr handle,IntPtr parameter);
    [DllImport("user32.dll")]static extern bool EnumWindows(EnumCallback callback,IntPtr parameter);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr handle,StringBuilder text,int count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr handle,StringBuilder text,int count);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr handle,out uint process);
    [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr handle,int state);
    [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr handle,int state);
    [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")]static extern bool IsIconic(IntPtr handle);
}
