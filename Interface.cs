using System.Drawing.Drawing2D;
using System.Text.Json;

namespace StickMouse;

sealed class Preferences
{
    public int Speed { get; set; } = 1100;
    public int DeadZone { get; set; } = 20;
    public int Response { get; set; } = 16;
    public bool InvertY { get; set; }
    public static Preferences Normalize(Preferences p) => new() { Speed = Math.Clamp(p.Speed, 200, 2400), DeadZone = Math.Clamp(p.DeadZone, 5, 40), Response = Math.Clamp(p.Response, 10, 30), InvertY = p.InvertY };
}

sealed partial class MouseWindow
{
    internal static readonly Color Canvas = Color.FromArgb(20, 23, 26), Surface = Color.FromArgb(29, 33, 37), Ink = Color.FromArgb(237, 241, 239), Muted = Color.FromArgb(163, 174, 171), Mint = Color.FromArgb(155, 216, 187);
    readonly Label preferenceNote = new() { AutoSize = true };
    readonly string? preferencesOverride;
    string PreferencesPath => preferencesOverride ?? Path.Combine(Path.GetDirectoryName(logPath)!, "settings.json");
    internal static void TestInterface()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, ".verify", "settings-test");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "settings.json");
        File.Delete(path);
        using (var first = new MouseWindow(true, path))
        {
            first.ApplyPreferences(new Preferences { Speed = 1800, DeadZone = 12, Response = 23, InvertY = true });
            first.SavePreferences();
            first.UpdateConnection(0); first.enabled.Checked=true;
            foreach(Exception failure in new Exception[]{new System.Net.Http.HttpRequestException("offline"),new TaskCanceledException("timeout"),new System.Security.Cryptography.CryptographicException("bad signature"),new JsonException("invalid feed"),new IOException("unavailable")})
            {
                first.CheckUpdates(_=>Task.FromException<(UpdateManifest,string)>(failure)).GetAwaiter().GetResult();
                if(!first.enabled.Enabled || !first.enabled.Checked || !first.updateButton.Enabled || !first.updateStatus.Text.Contains("Current version is ready"))throw new Exception("Update failure blocked controller use");
            }
            first.speed.TestKeyboard();
            first.UpdateConnection(-1);
            if (first.enabled.Enabled || first.enabled.Checked || !first.status.Text.StartsWith("No controller detected")) throw new Exception("Disconnected start gate");
            first.UpdateConnection(0);
            if (!first.enabled.Enabled || first.enabled.Checked) throw new Exception("Connection should enable Start without starting");
            first.enabled.Checked = true;
            first.UpdateConnection(-1);
            if (first.enabled.Enabled || first.enabled.Checked) throw new Exception("Disconnect must stop control");
            first.UpdateConnection(0);
            if (first.enabled.Checked) throw new Exception("Reconnect auto-started");
        }
        using (var second = new MouseWindow(true, path))
        {
            if (second.speed.Value != 1800 || second.deadZone.Value != 12 || second.response.Value != 23 || !second.invertY.Checked)
                throw new Exception("Settings round-trip failed");
            second.ApplyPreferences(new());
            if (second.speed.Value != 1100 || second.invertY.Checked) throw new Exception("Reset failed");
            second.speed.Value = 1750;
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (timeout.ElapsedMilliseconds < 800) { Application.DoEvents(); Thread.Sleep(10); }
            if (JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path))!.Speed != 1750) throw new Exception("Debounced autosave failed");
        }
        var encoded = JsonSerializer.Serialize(Preferences.Normalize(new Preferences { Speed = -1, DeadZone = 999, Response = 999 }));
        var decoded = JsonSerializer.Deserialize<Preferences>(encoded)!;
        if (decoded.Speed != 200 || decoded.DeadZone != 40 || decoded.Response != 30) throw new Exception("Settings bounds failed");
        File.WriteAllText(path, "{bad json");
        using (var broken = new MouseWindow(true, path))
            if (!broken.preferenceNote.Text.StartsWith("SM002") || broken.speed.Value != 1100) throw new Exception("Corrupt settings fallback failed");
        File.Delete(path);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "interface-test.txt"), "PASS: settings persistence/autosave, reset, bounds, corrupt-file recovery, keyboard, connect/start/disconnect/reconnect gates, safe disposal");
    }
    readonly SettingsCanvas canvas = new();
    readonly System.Windows.Forms.Timer saveTimer = new() { Interval = 450 };
    bool loadingPreferences;
    readonly Dictionary<Control, Font> layoutFonts = new();
    void BuildInterface()
    {
        Text = "StickMouse"; AutoScaleMode = AutoScaleMode.None;
        BackColor = Canvas; ForeColor = Ink; StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Canvas };
        Controls.Add(scroll); scroll.Controls.Add(canvas);
        canvas.Controls.AddRange(new Control[] { enabled, speed, deadZone, response, invertY, preferenceNote, status });
        enabled.Text = "Start controller mouse"; invertY.Text = "Invert vertical axis";
        preferenceNote.Text = "Settings saved on this device";
        preferenceNote.ForeColor = Muted; preferenceNote.AutoSize = false;
        status.AutoSize = false; status.ForeColor = Ink;
        status.Text = "No controller connected";
        var reset = new Button { Text = "Reset defaults", FlatStyle = FlatStyle.Flat, BackColor = Surface, ForeColor = Muted, TabIndex = 5 };
        reset.FlatAppearance.BorderSize = 0; reset.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 52, 50);
        reset.Click += (_, _) => { ApplyPreferences(new()); QueueSave(); };
        canvas.Controls.Add(reset);
        enabled.TabIndex = 0; speed.TabIndex = 1; deadZone.TabIndex = 2; response.TabIndex = 3; invertY.TabIndex = 4;
        void LayoutCanvas()
        {
            float d = DeviceDpi / 96f;
            int U(float n) => (int)Math.Round(n * d);
            canvas.Size = new Size(Math.Max(scroll.ClientSize.Width - (scroll.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0), U(800)), U(612));
            int x = U(282), width = canvas.Width - x - U(32);
            enabled.SetBounds(x + width - U(174), U(114), U(174), U(36));
            speed.SetBounds(x, U(174), width, U(92)); deadZone.SetBounds(x, U(270), width, U(92)); response.SetBounds(x, U(366), width, U(92));
            invertY.SetBounds(x + width - U(48), U(477), U(48), U(28));
            reset.SetBounds(x, U(540), U(112), U(28));
            preferenceNote.SetBounds(x + U(126), U(544), width - U(126), U(28));
            status.SetBounds(U(50), U(377), U(188), U(56));
            foreach (Control c in new Control[] { reset, preferenceNote, status })
            {
                int size = U(c == status ? 12 : 11);
                if (!layoutFonts.TryGetValue(c, out var old) || old.Size != size)
                {
                    var font = new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Pixel);
                    c.Font = font; layoutFonts[c] = font; old?.Dispose();
                }
            }
            updateButton.SetBounds(x, U(578), U(160), U(30));
            updateStatus.SetBounds(x + U(174), U(584), width-U(174), U(26));
            canvas.Invalidate();
        }
        scroll.Resize += (_, _) => LayoutCanvas();
        DpiChanged += (_, _) => LayoutCanvas();
        ClientSize = new Size((int)(860 * DeviceDpi / 96f), (int)(612 * DeviceDpi / 96f));
        var area = Screen.FromControl(this).WorkingArea;
        Size = new Size(Math.Min(Width, area.Width - 32), Math.Min(Height, area.Height - 32));
        MinimumSize = new Size(640, 480);
        LayoutCanvas();
        speed.ValueChanged += (_, _) => QueueSave(); deadZone.ValueChanged += (_, _) => QueueSave(); response.ValueChanged += (_, _) => QueueSave(); invertY.CheckedChanged += (_, _) => QueueSave();
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); SavePreferences(); };
    }
    void QueueSave()
    {
        if (loadingPreferences || (previewMode && preferencesOverride is null)) return;
        preferenceNote.Text = "Saving settings�"; saveTimer.Stop(); saveTimer.Start();
    }
    void ApplyPreferences(Preferences p) { loadingPreferences = true; try { speed.Value = p.Speed; deadZone.Value = p.DeadZone; response.Value = p.Response; invertY.Checked = p.InvertY; } finally { loadingPreferences = false; } }
    void LoadPreferences()
    {
        if (!File.Exists(PreferencesPath)) return;
        try { ApplyPreferences(Preferences.Normalize(JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PreferencesPath)) ?? new())); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { SettingsFailure("SM002", "Could not load settings; using defaults.", e); }
    }
    void SavePreferences()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
            string temp = PreferencesPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new Preferences { Speed = speed.Value, DeadZone = deadZone.Value, Response = response.Value, InvertY = invertY.Checked }, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, PreferencesPath, true);
            preferenceNote.Text = "All changes saved";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { SettingsFailure("SM003", "Settings could not be saved.", e);  }
    }
    void SettingsFailure(string code, string message, Exception error)
    {
        preferenceNote.Text = $"{code}: {message}";
        try { string target = preferencesOverride is null ? logPath : Path.Combine(Path.GetDirectoryName(preferencesOverride)!, "test-runtime.log"); Directory.CreateDirectory(Path.GetDirectoryName(target)!); if (File.Exists(target) && new FileInfo(target).Length > 262144) File.Move(target, target + ".old", true); File.AppendAllText(target, $"{DateTimeOffset.UtcNow:o} {code} {error.GetType().Name}\n"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { System.Diagnostics.Debug.WriteLine($"{code}: diagnostic storage unavailable"); }
    }
}



