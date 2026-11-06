namespace StickMouse;

sealed class MappingWindow : Form
{
    readonly Preferences original;
    readonly Dictionary<PadControl,ComboBox> buttons = new();
    readonly ComboBox leftStick = Choice(), rightStick = Choice();
    readonly NumericUpDown scrollSpeed = new() { Minimum=1,Maximum=30,Value=8,BackColor=MouseWindow.Surface,ForeColor=MouseWindow.Ink,BorderStyle=BorderStyle.FixedSingle };
    internal Preferences Result { get; private set; }
    readonly List<Font> fonts = new();
    internal MappingWindow(Preferences current)
    {
        original=Preferences.Normalize(current); Result=Preferences.Normalize(current);
        Text="StickMouse · Button mapping"; Icon=GamepadIcons.Create();
        BackColor=MouseWindow.Canvas;ForeColor=MouseWindow.Ink;AutoScaleMode=AutoScaleMode.None;
        StartPosition=FormStartPosition.CenterParent;MinimizeBox=false;ShowInTaskbar=false;
        float d=DeviceDpi/96f;int U(int n)=>(int)Math.Round(n*d);
        Font=OwnFont(12*d);
        var header=new Panel{Dock=DockStyle.Top,Height=U(94),BackColor=MouseWindow.Canvas};
        var title=TextLabel("Make it yours",22*d,true);title.SetBounds(U(28),U(15),U(650),U(34));
        var hint=TextLabel("Choose what every button does. Mouse control stays paused while you edit.",12*d);hint.SetBounds(U(28),U(56),U(750),U(24));hint.ForeColor=MouseWindow.Muted;
        header.Controls.AddRange(new Control[]{title,hint});
        var footer=new Panel{Dock=DockStyle.Bottom,Height=U(76),BackColor=MouseWindow.Surface};
        var reset=Button("Reset mappings",MouseWindow.Muted);var cancel=Button("Cancel",MouseWindow.Ink);var apply=Button("Apply && save",MouseWindow.Canvas);apply.BackColor=MouseWindow.Mint;
        apply.Click+=(_,_)=>{Result=Draft();DialogResult=DialogResult.OK;Close();};
        cancel.Click+=(_,_)=>{DialogResult=DialogResult.Cancel;Close();};
        reset.Click+=(_,_)=>Fill(new Preferences());
        footer.Controls.AddRange(new Control[]{reset,cancel,apply});AcceptButton=apply;CancelButton=cancel;
        var body=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=MouseWindow.Canvas};
        var sheet=new Panel{BackColor=MouseWindow.Canvas};body.Controls.Add(sheet);
        Controls.Add(body);Controls.Add(header);Controls.Add(footer);
        var stickHeading=TextLabel("STICKS & SCROLL",11*d,true);stickHeading.ForeColor=MouseWindow.Muted;sheet.Controls.Add(stickHeading);
        var leftLabel=TextLabel("Left stick",12*d,true);var rightLabel=TextLabel("Right stick",12*d,true);
        var scrollLabel=TextLabel("Scroll speed",12*d,true);var scrollHint=TextLabel("Wheel steps / second · Precision mode uses 25% cursor speed",11*d);scrollHint.ForeColor=MouseWindow.Muted;
        foreach(var role in Enum.GetValues<StickRole>()){leftStick.Items.Add(Bindings.Label(role));rightStick.Items.Add(Bindings.Label(role));}
        sheet.Controls.AddRange(new Control[]{leftLabel,rightLabel,leftStick,rightStick,scrollLabel,scrollSpeed,scrollHint});
        var buttonHeading=TextLabel("BUTTON ACTIONS",11*d,true);buttonHeading.ForeColor=MouseWindow.Muted;sheet.Controls.Add(buttonHeading);
        var labels=new Dictionary<PadControl,Label>();
        foreach(var control in Bindings.Controls)
        {
            var label=TextLabel(Bindings.Label(control),11*d,true);var choice=Choice();choice.Font=Font;
            choice.AccessibleName=Bindings.Label(control)+" mouse action";
            foreach(var action in Bindings.Actions)choice.Items.Add(Bindings.Label(action));
            labels.Add(control,label);buttons.Add(control,choice);sheet.Controls.AddRange(new Control[]{label,choice});
        }
        leftStick.AccessibleName="Left stick behavior";rightStick.AccessibleName="Right stick behavior";scrollSpeed.AccessibleName="Scroll speed in wheel steps per second";
        var note=TextLabel("Click actions stay held until release, so any button can drag. Scroll and cursor actions repeat while held.\nDouble-click and drag toggle fire once per press. Close this editor to use F8 pause / resume. Apply keeps control paused.",11*d);note.ForeColor=MouseWindow.Muted;sheet.Controls.Add(note);
        void Arrange()
        {
            int width=Math.Max(body.ClientSize.Width-(body.VerticalScroll.Visible?SystemInformation.VerticalScrollBarWidth:0),U(740));
            sheet.Size=new Size(width,U(804));int col=(width-U(76))/2;
            stickHeading.SetBounds(U(28),U(8),width-U(56),U(20));
            leftLabel.SetBounds(U(28),U(36),col,U(20));rightLabel.SetBounds(U(48)+col,U(36),col,U(20));
            leftStick.SetBounds(U(28),U(60),col,U(30));rightStick.SetBounds(U(48)+col,U(60),col,U(30));
            scrollLabel.SetBounds(U(28),U(110),U(110),U(25));scrollSpeed.SetBounds(U(144),U(106),U(76),U(30));
            scrollHint.SetBounds(U(232),U(111),width-U(260),U(36));buttonHeading.SetBounds(U(28),U(159),width-U(56),U(20));
            for(int i=0;i<Bindings.Controls.Length;i++)
            {
                var control=Bindings.Controls[i];int x=U(28)+(i/8)*(col+U(20)),y=U(188)+(i%8)*U(66);
                labels[control].SetBounds(x,y,col,U(20));buttons[control].SetBounds(x,y+U(23),col,U(30));
            }
            note.SetBounds(U(28),U(724),width-U(56),U(72));
            reset.SetBounds(U(28),U(22),U(136),U(34));cancel.SetBounds(footer.Width-U(280),U(22),U(104),U(34));apply.SetBounds(footer.Width-U(164),U(22),U(136),U(34));
        }
        body.Resize+=(_,_)=>Arrange();footer.Resize+=(_,_)=>Arrange();
        MinimumSize=new Size(U(640),U(480));ClientSize=new Size(U(880),U(700));
        var area=Screen.FromControl(this).WorkingArea;Size=new Size(Math.Min(Width,area.Width-32),Math.Min(Height,area.Height-32));
        Fill(original);Arrange();
        // ComboBox uses its native keyboard and screen-reader behavior; only the menu colors are owner drawn.
    }
    Font OwnFont(float size,bool bold=false){var font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);fonts.Add(font);return font;}
    Label TextLabel(string text,float size,bool bold=false)=>new(){Text=text,ForeColor=MouseWindow.Ink,BackColor=Color.Transparent,Font=OwnFont(size,bold),AutoSize=false,UseMnemonic=false};
    static Button Button(string text,Color color)
    {var b=new Button{Text=text,FlatStyle=FlatStyle.Flat,BackColor=MouseWindow.Surface,ForeColor=color};b.FlatAppearance.BorderColor=PaintKit.Line;b.FlatAppearance.MouseOverBackColor=Color.FromArgb(57,70,66);return b;}
    static ComboBox Choice()
    {
        var box=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=MouseWindow.Surface,ForeColor=MouseWindow.Ink,DrawMode=DrawMode.OwnerDrawFixed,IntegralHeight=false,MaxDropDownItems=10,DropDownHeight=280};
        box.DrawItem+=(_,e)=>{
            bool selected=(e.State&DrawItemState.Selected)!=0;
            using var brush=new SolidBrush(selected?Color.FromArgb(47,68,58):MouseWindow.Surface);e.Graphics.FillRectangle(brush,e.Bounds);
            if(e.Index>=0)TextRenderer.DrawText(e.Graphics,box.Items[e.Index]?.ToString() ?? "",box.Font,e.Bounds,MouseWindow.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        };
        box.FontChanged+=(_,_)=>box.ItemHeight=(int)Math.Ceiling(box.Font.GetHeight()+10);
        return box;
    }
    void Fill(Preferences p)
    {
        foreach(var c in Bindings.Controls)buttons[c].SelectedIndex=(int)Bindings.Action(p,c);
        leftStick.SelectedIndex=(int)p.LeftStickRole;rightStick.SelectedIndex=(int)p.RightStickRole;scrollSpeed.Value=p.ScrollSpeed;
    }
    internal Preferences Draft()
    {
        var p=Preferences.Normalize(original);
        p.Buttons=buttons.ToDictionary(pair=>pair.Key.ToString(),pair=>(MouseAction)pair.Value.SelectedIndex);
        p.LeftStickRole=(StickRole)leftStick.SelectedIndex;p.RightStickRole=(StickRole)rightStick.SelectedIndex;p.ScrollSpeed=(int)scrollSpeed.Value;
        return Preferences.Normalize(p);
    }
    internal void SetBinding(PadControl c,MouseAction a)=>buttons[c].SelectedIndex=(int)a;
    internal void ResetDraft()=>Fill(new Preferences());
    internal void ScrollToBottom(){var body=Controls.OfType<Panel>().Single(panel=>panel.AutoScroll);body.AutoScrollPosition=new Point(0,2000);Application.DoEvents();}
    protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing){Icon?.Dispose();foreach(var font in fonts)font.Dispose();}}
}

sealed partial class MouseWindow
{
    internal static void TestMappingInterface()
    {
        var original=new Preferences{Speed=1750};
        using(var editor=new MappingWindow(original))
        {
            editor.SetBinding(PadControl.A,MouseAction.MiddleClick);var draft=editor.Draft();
            if(Bindings.Action(draft,PadControl.A)!=MouseAction.MiddleClick || draft.Speed!=1750 || Bindings.Action(original,PadControl.A)!=MouseAction.None)throw new Exception("Mapping draft mutated live settings");
            editor.ResetDraft();if(Bindings.Action(editor.Draft(),PadControl.LeftStick)!=MouseAction.LeftClick || editor.Draft().Speed!=1750)throw new Exception("Mapping reset lost cursor preferences");
        }
        string path=Path.Combine(AppContext.BaseDirectory,".verify","mapping-settings.json");File.Delete(path);
        using(var app=new MouseWindow(true,path))
        {
            app.mappingEditorOpen=true;app.UpdateConnection(0);
            if(app.enabled.Enabled || app.trayControl?.Enabled==true)throw new Exception("Editor allows starting input");
            app.mappingEditorOpen=false;app.UpdateConnection(0);
            var p=new Preferences();p.Buttons[nameof(PadControl.A)]=MouseAction.MiddleClick;
            p.LeftStickRole=StickRole.Scroll;p.RightStickRole=StickRole.Cursor;p.ScrollSpeed=15;
            app.UpdateConnection(0);app.enabled.Checked=true;app.ApplyPreferences(p);
            if(app.enabled.Checked)throw new Exception("Remap did not pause input");app.SavePreferences();
        }
        using(var app=new MouseWindow(true,path))
            if(Bindings.Action(app.CurrentPreferences(),PadControl.A)!=MouseAction.MiddleClick || app.CurrentPreferences().RightStickRole!=StickRole.Cursor || app.CurrentPreferences().ScrollSpeed!=15)throw new Exception("Mapping persistence");
        File.Delete(path);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"mapping-ui-test.txt"),"PASS: draft/cancel isolation, mapping reset, apply pauses input, bindings/stick roles/scroll speed persistence");
    }
}
