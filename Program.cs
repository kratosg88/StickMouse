using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StickMouse;

static class Program
{
    [STAThread] static int Main(string[] args)
    {
        try { Run(args); return 0; }
        catch (Exception error)
        {
            try { var path = args.Contains("--self-test") || args.Contains("--preview") ? Path.Combine(AppContext.BaseDirectory, ".verify") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StickMouse"); Directory.CreateDirectory(path); File.AppendAllText(Path.Combine(path, "runtime.log"), $"{DateTimeOffset.UtcNow:o} SM900 {error}\n"); } catch (Exception) { /* No writable diagnostics location remains. */ }
            if (!args.Contains("--self-test") && !args.Contains("--preview")) MessageBox.Show("StickMouse stopped unexpectedly. Diagnostic code: SM900. See runtime.log in your local StickMouse app data folder.", "StickMouse");
            return 1;
        }
    }
    static void Run(string[] args)
    {
        if(args.Length==2 && args[0]=="--export-icon"){GamepadIcons.SaveIco(args[1]);return;}
        if (args.Length == 2 && args[0] == "--verify-release")
        {
            var manifest=UpdateProtocol.Verify(File.ReadAllText(Path.Combine(args[1],"update.json")),UpdateClient.Trust,new Version(0,0,0,0));
            UpdateProtocol.VerifyPackage(Path.Combine(args[1],"StickMouse-Portable.zip"),manifest);
            UpdateProtocol.Extract(Path.Combine(args[1],"StickMouse-Portable.zip"),Path.Combine(args[1],"verified-"+Guid.NewGuid().ToString("N")));
            return;
        }
        if (args.Length == 2 && args[0] == "--apply-update") { UpdateClient.Apply(args[1]); return; }
        if (args.Contains("--self-test")) { Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); ApplicationConfiguration.Initialize(); Motion.Test(); UpdateTests.Run(); MouseButtonLatch.Test(); MouseWindow.TestInterface(); MouseWindow.TestTray(); return; }
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); ApplicationConfiguration.Initialize();
        if (args.Contains("--preview"))
        {
            using var window = new MouseWindow(true);
            if (args.Contains("--connected-preview")) window.UpdateConnection(0);
            window.Show(); Application.DoEvents();
            using var bitmap = new Bitmap(window.Width, window.Height);
            window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(args[Array.IndexOf(args, "--preview") + 1]);
            window.Close(); return;
        }
        int tokenIndex=Array.IndexOf(args,"--launch-token");
        if(tokenIndex>=0 && tokenIndex+1<args.Length && Guid.TryParseExact(args[tokenIndex+1],"N",out _))AppStartup.LaunchToken=args[tokenIndex+1];
        if(args.Contains("--startup-smoke"))
        {
            int dataIndex=Array.IndexOf(args,"--diagnostics-dir");
            if(dataIndex>=0 && dataIndex+1<args.Length)AppStartup.DataOverride=Path.GetFullPath(args[dataIndex+1]);
        }
        AppStartup.Log("SM_START","normal_start");
        using var instance = new Mutex(true, args.Contains("--startup-smoke")?"Local\\StickMouse.StartupSmoke":"Local\\StickMouse.Desktop", out bool first);
        if (!first) { if(!AppStartup.RestoreExisting(args.Contains("--startup-smoke")))MessageBox.Show("The existing StickMouse window did not respond. Check its taskbar button or close StickMouse in Task Manager, then retry. Code: SM013.","StickMouse"); return; }
        using var app = new MouseWindow(args.Contains("--startup-smoke"),null,args.Contains("--tray-smoke"));
        if(args.Contains("--startup-smoke"))app.Text="StickMouse [startup test]";
        AppStartup.Attach(app);
        using var smokeTimer=new System.Windows.Forms.Timer{Interval=15000};
        if(args.Contains("--startup-smoke")){smokeTimer.Tick+=(_,_)=>app.QuitApplication();smokeTimer.Start();}
        try { Application.Run(app); } finally { app.StopInput(); }
    }
}

static class Motion
{
    public static (double x, double y) Velocity(short x, short y, double speed, double deadZone = .20, double curve = 1.6)
    {
        double nx = x / 32768.0, ny = y / 32768.0;
        double length = Math.Sqrt(nx * nx + ny * ny);
        if (length <= deadZone) return (0, 0);
        double magnitude = Math.Pow((Math.Min(length, 1) - deadZone) / (1 - deadZone), curve) * speed;
        return (nx / length * magnitude, -ny / length * magnitude);
    }
    public static void Test()
    {
        void Check(bool condition) { if (!condition) throw new Exception("Motion check failed"); }
        Check(Velocity(1000, -1000, 1000) == (0, 0));
        var right = Velocity(32767, 0, 1000); Check(right.x > 999 && right.y == 0);
        var up = Velocity(0, 32767, 1000); Check(up.y < -999);
        var diagonal = Velocity(32767, 32767, 1000);
        Check(Math.Abs(Math.Sqrt(diagonal.x * diagonal.x + diagonal.y * diagonal.y) - 1000) < .01);
        Check(Velocity(16000, 0, 1000).x < right.x);
        Check(Velocity(8000, 0, 1000, .30) == (0, 0));
        Check(Velocity(16000, 0, 1000, .10, 3).x < Velocity(16000, 0, 1000, .10, 1).x);
        Check(Preferences.Normalize(new Preferences { Speed = 9999, DeadZone = -5, Response = 0 }).Speed == 2400);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.txt"), "PASS: dead zone, direction, speed cap, acceleration curve");
    }
}

sealed partial class MouseWindow : Form
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 8 };
    readonly Label status = new() { AutoSize = true };
    readonly StartControl enabled = new() { AccessibleName = "Start controller mouse", Enabled = false };
    readonly PreferenceSlider speed = new("Sensitivity", "Maximum cursor speed", 200, 2400, 1100, "px/s");
    readonly PreferenceSlider deadZone = new("Stick dead zone", "Ignore small movements to prevent drift.", 5, 40, 20, "%");
    readonly PreferenceSlider response = new("Response curve", "Higher values give finer control near the center.", 10, 30, 16, "", 10);
    readonly ToggleSwitch invertY = new() { Text = "Invert vertical axis", AccessibleName = "Invert vertical axis" };
    string? inputError;
    readonly bool previewMode;
    readonly Stopwatch clock = Stopwatch.StartNew();
    double remainderX, remainderY;
    bool armed;
    readonly MouseButtonLatch buttons = new();
    int lastInputError;
    int controller = -1;
    readonly string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StickMouse", "runtime.log");
    public MouseWindow(bool preview = false, string? settingsPath = null, bool trayForTests = false)
    {
        previewMode = preview; preferencesOverride = settingsPath;
        InitializeUpdater();
        BuildInterface();
        LoadPreferences();
        if(!previewMode || trayForTests)InitializeTray();
        enabled.CheckedChanged += (_, _) => { Release(); armed = false; if (enabled.Checked) inputError = null; UpdateStatus(); };
        UpdateConnection(-1);
        timer.Tick += (_, _) => TickInput();

        if (!previewMode)
        {
            if (!Native.RegisterHotKey(Handle, 1, 0x4000, 0x77))
                preferenceNote.Text = "F8 is in use by another app. Use the Start button here.";
            timer.Start();
        }
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1;
        Native.DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==AppStartup.RestoreMessage){ShowFromTray();m.Result=(IntPtr)1;return;}
        if(m.Msg==AppStartup.SmokeQuitMessage && previewMode && Text=="StickMouse [startup test]"){QuitApplication();return;}
        if (m.Msg == 0x0312 && enabled.Enabled) enabled.Checked = !enabled.Checked;
        base.WndProc(ref m);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Release(); DisposeTray(); timer.Dispose(); saveTimer.Dispose(); updateCancellation.Cancel(); updateTimer.Dispose(); foreach(var font in layoutFonts.Values) font.Dispose(); layoutFonts.Clear(); if (IsHandleCreated) Native.UnregisterHotKey(Handle, 1); }
        base.Dispose(disposing);
        if(disposing)appIcon.Dispose();
    }
    void TickInput()
    {
        double dt = Math.Min(clock.Elapsed.TotalSeconds, .05); clock.Restart();
        Native.State state = default;
        int found = -1;
        if (controller >= 0 && Native.XInputGetState((uint)controller, out state) == 0) found = controller;
        else for (uint i = 0; i < 4; i++) if (Native.XInputGetState(i, out state) == 0) { found = (int)i; break; }
        UpdateConnection(found);
        timer.Interval = found < 0 ? 250 : 8;
        UpdateStatus();
        if (found < 0 || !enabled.Checked) { Release(); return; }
        bool left = (state.pad.buttons & 0x0040) != 0, right = (state.pad.buttons & 0x0080) != 0;
        if (!armed) { if (buttons.Pending && !buttons.Release(flags => Send(0, 0, flags))) return; if (!left && !right) armed = true; return; }
        var (vx, vy) = Motion.Velocity(state.pad.lx, state.pad.ly, speed.Value, deadZone.Value / 100.0, response.Value / 10.0);
        if (invertY.Checked) vy = -vy;
        remainderX += vx * dt; remainderY += vy * dt;
        int dx = (int)remainderX, dy = (int)remainderY; remainderX -= dx; remainderY -= dy;
        if ((dx != 0 || dy != 0) && !Send(dx, dy, 0x0001)) { InputFailed(); return; }
        if (!buttons.Update(left, right, flags => Send(0, 0, flags))) InputFailed();
    }
    internal void UpdateConnection(int found)
    {
        if (found != controller) { Release(); armed = false; enabled.Checked = false; inputError = null; controller = found; }
        enabled.Enabled = found >= 0;
        UpdateStatus();
    }
    void UpdateStatus()
    {
        status.Text = inputError ?? (controller < 0 ? "No controller detected\nConnect via USB or Bluetooth" : $"Controller {controller + 1} connected\n{(enabled.Checked ? "Mouse control active" : "Ready. Press Start to begin.")}");
        canvas.Connected = controller >= 0;
        canvas.Active = enabled.Checked;
        UpdateTray();
    }
    internal void StopInput() { timer.Stop(); Release(); }
    void Release()
    {
        if (!buttons.Release(flags => Send(0, 0, flags)) && inputError is null)
        {
            inputError = "SM001: Button release pending.\nReturn to the desktop to retry.";
            SettingsFailure("SM001", "Windows rejected a button release; retrying.", new System.ComponentModel.Win32Exception(lastInputError));
        }
        remainderX = remainderY = 0;
    }
    bool Send(int x, int y, uint flags)
    {
        var inputs = new[] { new Native.Input { type = 0, data = new Native.InputUnion { mouse = new Native.MouseInput { dx = x, dy = y, flags = flags } } } };
        bool accepted = Native.SendInput(1, inputs, Marshal.SizeOf<Native.Input>()) == 1;
        if (!accepted) lastInputError = Marshal.GetLastWin32Error();
        return accepted;
    }
    void InputFailed()
    {
        inputError = "SM001: Input blocked.\nReturn to desktop and enable again.";
        enabled.Checked = false;
        status.Text = inputError;
        SettingsFailure("SM001", "Mouse input blocked. Control paused.", new System.ComponentModel.Win32Exception(lastInputError));
    }

}

static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct Gamepad { public ushort buttons; public byte lt, rt; public short lx, ly, rx, ry; }
    [StructLayout(LayoutKind.Sequential)] public struct State { public uint packet; public Gamepad pad; }
    [StructLayout(LayoutKind.Sequential)] public struct MouseInput { public int dx, dy; public uint data, flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public MouseInput mouse; }
    [StructLayout(LayoutKind.Sequential)] public struct Input { public uint type; public InputUnion data; }
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("xinput1_4.dll")] public static extern uint XInputGetState(uint index, out State state);
    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}



