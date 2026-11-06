using System.Runtime.InteropServices;

namespace StickMouse;

sealed partial class MouseWindow
{
    NotifyIcon? trayIcon;
    ContextMenuStrip? trayMenu;
    readonly Icon appIcon=GamepadIcons.Create();
    ToolStripMenuItem? trayState, trayControl;
    bool exitRequested, trayHintShown;
    internal bool StartInTray => trayIcon?.Visible==true;
    void InitializeTray()
    {
        trayMenu=new ContextMenuStrip();
        trayState=new ToolStripMenuItem("No controller detected"){Enabled=false};trayMenu.Items.Add(trayState);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Open StickMouse",null,(_,_)=>ShowFromTray());
        trayControl=new ToolStripMenuItem("Start controller mouse",null,(_,_)=>{if(enabled.Enabled)enabled.Checked=!enabled.Checked;});trayMenu.Items.Add(trayControl);
        trayMenu.Items.Add("Hide window",null,(_,_)=>HideToTray());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Quit StickMouse",null,(_,_)=>QuitApplication());
        trayIcon=new NotifyIcon {Icon=appIcon,Text="StickMouse - waiting for controller",ContextMenuStrip=trayMenu,Visible=true};
        trayIcon.DoubleClick+=(_,_)=>ShowFromTray();trayIcon.BalloonTipClicked+=(_,_)=>ShowFromTray();
        Resize+=(_,_)=>{if(WindowState==FormWindowState.Minimized)HideToTray();};
        if(!previewMode)Opacity=0;
        UpdateTray();
    }
    void UpdateTray()
    {
        if(trayIcon is null)return;
        string state=controller<0?"No controller detected":enabled.Checked?"Controller mouse active":"Controller connected - paused";
        if(trayIcon.Text!="StickMouse - "+state)trayIcon.Text="StickMouse - "+state;
        trayState!.Text=state;trayControl!.Text=enabled.Checked?"Pause controller mouse":"Start controller mouse";trayControl.Enabled=enabled.Enabled;
    }
    internal void HideToTray()
    {
        if(trayIcon?.Visible!=true)return; // Never hide without a recovery icon.
        Hide();ShowInTaskbar=false;
        if(!previewMode && !trayHintShown){trayHintShown=true;trayIcon.ShowBalloonTip(3500,"StickMouse is running in the tray","Double-click the tray icon to open. Right-click it to pause or quit.",ToolTipIcon.Info);}
    }
    internal void ShowFromTray()
    {
        ShowInTaskbar=true;WindowState=FormWindowState.Normal;Show();Activate();BringToFront();
    }
    internal void QuitApplication(){exitRequested=true;Close();}
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if(e.CloseReason==CloseReason.UserClosing && !exitRequested && trayIcon?.Visible==true)
        {e.Cancel=true;HideToTray();base.OnFormClosing(e);return;}
        base.OnFormClosing(e);
        if(!e.Cancel){timer.Stop();Release();if(!previewMode)SavePreferences();if(trayIcon is not null)trayIcon.Visible=false;}
    }
    void DisposeTray()
    {
        if(trayIcon is not null){trayIcon.Visible=false;trayIcon.Dispose();trayIcon=null;}
        trayMenu?.Dispose();
    }
    internal static void TestTray()
    {
        using var app=new MouseWindow(true,null,true);
        app.Show();Application.DoEvents();
        if(app.trayIcon?.Visible!=true)throw new Exception("Tray recovery icon not created");
        app.Close();if(app.IsDisposed || app.Visible)throw new Exception("Close did not hide to tray");
        app.ShowFromTray();if(!app.Visible || !app.ShowInTaskbar)throw new Exception("Tray Open did not restore window");
        app.UpdateConnection(0);app.trayControl!.PerformClick();if(!app.enabled.Checked)throw new Exception("Tray Start failed");
        app.trayControl.PerformClick();if(app.enabled.Checked)throw new Exception("Tray Pause failed");
        app.UpdateConnection(-1);if(app.trayControl.Enabled)throw new Exception("Tray start must be disabled without controller");
        app.QuitApplication();if(!app.IsDisposed || app.trayIcon is not null)throw new Exception("Tray Quit did not dispose icon and window");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"tray-test.txt"),"PASS: recovery icon, close-to-tray, reopen, tray Start/Pause, disconnected gate, explicit Quit cleanup");
    }
}
