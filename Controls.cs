using System.Drawing.Drawing2D;

namespace StickMouse;

static class PaintKit
{
    internal static readonly Color Line = Color.FromArgb(46, 53, 58), Secondary = Color.FromArgb(145, 158, 165), Accent = Color.FromArgb(157, 218, 190);
    internal static void Text(Graphics g, string text, float x, float y, float size, Color color, bool bold = false, float width = 800, float height = 40)
    {
        using var font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
        g.DrawString(text, font, brush, new RectangleF(x, y, width, height), format);
    }
    internal static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath(); float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right-d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right-d, r.Bottom-d, d, d, 0, 90); p.AddArc(r.X, r.Bottom-d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    internal static void Box(Graphics g, RectangleF r, float radius, Color fill, Color? border = null)
    {
        using var p = Rounded(r, radius); using var b = new SolidBrush(fill); g.FillPath(b, p);
        if (border.HasValue) { using var pen = new Pen(border.Value); g.DrawPath(pen, p); }
    }
}

sealed class SettingsCanvas : Panel
{
    bool connected, active;
    public bool Connected { get => connected; set { if (connected != value) { connected=value; Invalidate(); } } }
    public bool Active { get => active; set { if(active!=value){active=value;Invalidate();} } }
    public SettingsCanvas() { DoubleBuffered = true; BackColor = MouseWindow.Canvas; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        float scale = DeviceDpi / 96f; g.ScaleTransform(scale, scale); float w = Width / scale;
        var ink = MouseWindow.Ink; var muted = PaintKit.Secondary; var accent = PaintKit.Accent;
        PaintKit.Box(g, new RectangleF(32, 29, 28, 28), 8, accent);
        using (var p = new Pen(MouseWindow.Canvas, 1.8f)) { g.DrawLine(p, 40, 43, 52, 43); g.DrawLine(p, 46, 37, 46, 49); }
        PaintKit.Text(g, "StickMouse", 70, 25, 18, ink, true);
        PaintKit.Text(g, "by CoderKnight", 70, 50, 10, muted);
        PaintKit.Text(g, "DESKTOP COMPANION", w-180, 37, 10, muted);
        using var line = new Pen(PaintKit.Line); g.DrawLine(line, 32, 78, w-32, 78);
        PaintKit.Text(g, "YOUR SETUP", 32, 103, 10, muted, true);
        PaintKit.Text(g, "Desktop profile", 32, 125, 19, ink, true);
        PaintKit.Text(g, "A little more control.", 32, 154, 12, muted);
        PaintKit.Box(g, new RectangleF(32, 193, 210, 158), 14, Color.FromArgb(28, 33, 38), PaintKit.Line);
        DrawController(g, 49, 216);
        PaintKit.Box(g, new RectangleF(108, 322, 58, 18), 5, Color.FromArgb(39, 47, 49));
        PaintKit.Text(g, "XINPUT", 117, 324, 9, accent, true);
        using (var b = new SolidBrush(connected ? accent : muted)) g.FillEllipse(b, 34, 380, 6, 6);
        PaintKit.Text(g, "QUICK REFERENCE", 32, 444, 10, muted, true);
        PaintKit.Text(g, "Left stick", 32, 470, 12, ink); PaintKit.Text(g, "Move cursor", 125, 470, 12, muted);
        PaintKit.Text(g, "L3", 32, 499, 12, ink, true); PaintKit.Text(g, "Left click / drag", 125, 499, 12, muted);
        PaintKit.Text(g, "F8   Pause / resume", 32, 561, 10, accent);
        PaintKit.Text(g, "R3", 32, 528, 12, ink, true); PaintKit.Text(g, "Right click", 125, 528, 12, muted);
        float x = 282, rw = w-x-32;
        PaintKit.Text(g, "Cursor control", x, 101, 22, ink, true);
        PaintKit.Text(g, "Fine-tune the feel of your left stick.", x, 135, 12, muted, width:rw-190);
        g.DrawLine(line, x, 165, w-32, 165);
        g.DrawLine(line, x, 462, w-32, 462);
        PaintKit.Text(g, "Invert vertical axis", x, 474, 13, ink, true);
        PaintKit.Text(g, "Push up to move down", x, 495, 11, muted);
        g.DrawLine(line, x, 526, w-32, 526);
    }
    static void DrawController(Graphics g, float x, float y)
    {
        var state = g.Save(); g.TranslateTransform(x, y);
        using var path = new GraphicsPath();
        path.AddBezier(30, 17, 18, 16, 13, 33, 8, 50); path.AddBezier(8,50,-9,102,14,116,40,78);
        path.AddBezier(40,78,65,82,101,82,126,78); path.AddBezier(126,78,152,116,175,102,158,50);
        path.AddBezier(158,50,153,33,148,16,136,17); path.AddBezier(136,17,106,10,60,10,30,17); path.CloseFigure();
        using var fill = new SolidBrush(Color.FromArgb(43, 51, 57)); using var outline = new Pen(Color.FromArgb(91, 108, 115), 1.4f);
        g.FillPath(fill,path); g.DrawPath(outline,path);
        PaintKit.Box(g,new RectangleF(62,24,42,25),5,Color.FromArgb(32,39,44),Color.FromArgb(73,87,94));
        using var dark = new SolidBrush(Color.FromArgb(23,29,33)); using var ring = new Pen(Color.FromArgb(120,139,142),1.6f);
        foreach (float sx in new[] { 47f, 100f }) { g.FillEllipse(dark,sx,57,22,22); g.DrawEllipse(ring,sx,57,22,22); g.DrawEllipse(outline,sx+5,62,12,12); }
        using var cross = new Pen(Color.FromArgb(173,188,193),4); g.DrawLine(cross,32,32,32,50); g.DrawLine(cross,23,41,41,41);
        using var button = new SolidBrush(PaintKit.Accent);
        foreach(var p in new[]{new Point(132,31),new Point(142,41),new Point(122,41),new Point(132,51)}) g.FillEllipse(button,p.X-2.6f,p.Y-2.6f,5.2f,5.2f);
        g.Restore(state);
    }
}

sealed class ToggleSwitch : CheckBox
{
    public ToggleSwitch()
    {
        AutoSize = false; Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; float d = DeviceDpi/96f; g.ScaleTransform(d,d);
        PaintKit.Box(g,new RectangleF(3,4,40,22),11,Checked ? PaintKit.Accent : Color.FromArgb(65,76,82));
        using var b = new SolidBrush(Checked ? Color.FromArgb(24,49,38) : Color.FromArgb(205,216,219));
        g.FillEllipse(b,Checked ? 25 : 6,7,16,16);
        if(Focused) { using var p = new Pen(PaintKit.Accent); using var r=PaintKit.Rounded(new RectangleF(0,1,46,28),13); g.DrawPath(p,r); }
    }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}

sealed class PreferenceSlider : Control
{
    readonly int minimum, maximum, divisor; readonly string title, hint, unit;
    int current; bool hover;
    public event EventHandler? ValueChanged;
    public int Value { get => current; set { int next=Math.Clamp(value,minimum,maximum); if(next==current)return; current=next; Invalidate(); if(IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.ValueChange,-1); ValueChanged?.Invoke(this,EventArgs.Empty); } }
    public PreferenceSlider(string title,string hint,int minimum,int maximum,int value,string unit,int divisor=1)
    {
        this.title=title; this.hint=hint; this.minimum=minimum; this.maximum=maximum; this.unit=unit; this.divisor=divisor; current=value;
        TabStop=true; AccessibleName=title; AccessibleDescription=hint; AccessibleRole=AccessibleRole.Slider; Cursor=Cursors.Hand;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new SliderAccessibility(this);
    sealed class SliderAccessibility(PreferenceSlider owner) : ControlAccessibleObject(owner)
    {
        public override string? Value { get => owner.Value.ToString(); set { if(int.TryParse(value,out int n))owner.Value=n; } }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        float d=DeviceDpi/96f; g.ScaleTransform(d,d); float w=Width/d;
        PaintKit.Text(g,title,0,7,13,MouseWindow.Ink,true);
        string label=divisor==1 ? $"{current:N0}{(unit=="%"?"":" ")}{unit}" : $"{current/(double)divisor:0.0}";
        PaintKit.Box(g,new RectangleF(w-84,3,84,26),6,Color.FromArgb(34,42,45));
        PaintKit.Text(g,label,w-76,8,12,PaintKit.Accent,width:80);
        PaintKit.Text(g,hint,0,31,11,PaintKit.Secondary,width:w);
        float x=8+(w-16)*(current-minimum)/(maximum-minimum);
        using var rail=new Pen(PaintKit.Line,3) { StartCap=LineCap.Round,EndCap=LineCap.Round };
        using var active=new Pen(PaintKit.Accent,3) { StartCap=LineCap.Round,EndCap=LineCap.Round };
        g.DrawLine(rail,8,65,w-8,65); g.DrawLine(active,8,65,x,65);
        using var thumb=new SolidBrush(PaintKit.Accent); g.FillEllipse(thumb,x-6,59,12,12);
        if(hover||Focused) { using var p=new Pen(PaintKit.Accent,1); g.DrawEllipse(p,x-9,56,18,18); }
        PaintKit.Text(g,divisor==1?$"{minimum}{(unit=="%"?"%":"")}":"Linear",0,77,9,PaintKit.Secondary);
        PaintKit.Text(g,divisor==1?$"{maximum}{(unit=="%"?"%":"")}":"Precision",w-56,77,9,PaintKit.Secondary,width:56);
    }
    void SetFromMouse(int x) { float d=DeviceDpi/96f; Value=minimum+(int)Math.Round(Math.Clamp((x/d-8)/Math.Max(1,Width/d-16),0,1)*(maximum-minimum)); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); float y=e.Y*96f/DeviceDpi; if(e.Button==MouseButtons.Left&&y>=48&&y<=78){Focus();Capture=true;SetFromMouse(e.X);} }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(Capture&&e.Button==MouseButtons.Left)SetFromMouse(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture=false; }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e);hover=true;Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e);hover=false;Invalidate(); }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); int step=divisor==1&&maximum>100?25:1;
        if(e.KeyCode is Keys.Left or Keys.Down)Value-=step;
        else if(e.KeyCode is Keys.Right or Keys.Up)Value+=step;
        else if(e.KeyCode==Keys.Home)Value=minimum;
        else if(e.KeyCode==Keys.End)Value=maximum;
        else return;
        e.Handled=true;
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e);Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e);Invalidate(); }
    internal void TestKeyboard()
    {
        int original=Value; OnKeyDown(new KeyEventArgs(Keys.Home)); if(Value!=minimum)throw new Exception("Slider Home");
        OnKeyDown(new KeyEventArgs(Keys.Right)); if(Value<=minimum)throw new Exception("Slider arrow");
        OnKeyDown(new KeyEventArgs(Keys.End)); if(Value!=maximum)throw new Exception("Slider End");
        OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,0,5,0)); if(Value!=maximum)throw new Exception("Slider label click changed value"); Value=original;
    }
}

sealed class StartControl : CheckBox
{
    public StartControl()
    {
        AutoSize=false; Appearance=Appearance.Button; Cursor=Cursors.Hand;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        float d=DeviceDpi/96f;g.ScaleTransform(d,d);
        var fill=Enabled ? (Checked?Color.FromArgb(46,68,58):PaintKit.Accent) : Color.FromArgb(44,51,57);
        var ink=Enabled ? (Checked?PaintKit.Accent:Color.FromArgb(23,45,34)) : Color.FromArgb(124,137,145);
        PaintKit.Box(g,new RectangleF(1,1,Width/d-2,Height/d-2),7,fill,Focused?PaintKit.Accent:null);
        PaintKit.Text(g,Checked?"Stop controller mouse":"Start controller mouse",12,10,12,ink,true,width:Width/d-20);
    }
    protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
    protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);Invalidate();}
    protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
}
