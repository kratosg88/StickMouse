using System.Text.Json;

namespace StickMouse;

static class MappingTests
{
    internal static void Run()
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("Mapping: "+why);}
        var events=new List<MouseEvent>();bool Send(MouseEvent e){events.Add(e);return true;}
        Preferences Profile(params (PadControl control,MouseAction action)[] mappings)
        {
            var p=new Preferences{LeftStickRole=StickRole.Disabled,RightStickRole=StickRole.Disabled};
            foreach(var c in Bindings.Controls)p.Buttons[c.ToString()]=MouseAction.None;
            foreach(var m in mappings)p.Buttons[m.control.ToString()]=m.action;return p;
        }
        var engine=new ControllerMouse();var p=new Preferences();
        engine.Step(default,p,.02,Send);
        engine.Step(new(){buttons=0x40},p,.02,Send);engine.Step(new(){buttons=0x40},p,.02,Send);engine.Step(default,p,.02,Send);
        Check(events.Select(e=>e.Flags).SequenceEqual(new uint[]{2,4}),"L3 default hold/release, no duplicate downs");
        events.Clear();engine.Stop(Send);engine.Step(new(){buttons=0x80},p,.02,Send);
        engine.Step(new(){buttons=0x80},p,.02,Send);Check(events.Count==0,"held input at start must wait for neutral");
        engine.Step(default,p,.02,Send);engine.Step(new(){buttons=0x80},p,.02,Send);engine.Stop(Send);
        Check(events.Select(e=>e.Flags).SequenceEqual(new uint[]{8,16}),"R3 default and pause release");

        ushort[] bitsByControl={0x40,0x80,0x1000,0x2000,0x4000,0x8000,0x100,0x200,0,0,1,2,4,8,0x10,0x20};
        foreach(var control in Bindings.Controls)
        {
            var mapped=Profile((control,MouseAction.MiddleClick));engine.Stop(Send);events.Clear();engine.Step(default,mapped,.02,Send);
            var pad=new Native.Gamepad{buttons=bitsByControl[(int)control]};
            if(control==PadControl.LeftTrigger)pad.lt=255;if(control==PadControl.RightTrigger)pad.rt=255;
            engine.Step(pad,mapped,.02,Send);engine.Step(default,mapped,.02,Send);
            Check(events.Select(e=>e.Flags).SequenceEqual(new uint[]{32,64}),"control remap "+control);
        }
        p=Profile((PadControl.A,MouseAction.LeftClick),(PadControl.B,MouseAction.LeftClick));engine.Stop(Send);events.Clear();engine.Step(default,p,.02,Send);
        foreach(ushort bits in new ushort[]{0x1000,0x3000,0x2000,0})engine.Step(new(){buttons=bits},p,.02,Send);
        Check(events.Select(e=>e.Flags).SequenceEqual(new uint[]{2,4}),"duplicate bindings must share ownership");
        p=Profile((PadControl.A,MouseAction.MiddleClick),(PadControl.B,MouseAction.Back),(PadControl.X,MouseAction.Forward));
        engine.Stop(Send);events.Clear();engine.Step(default,p,.02,Send);engine.Step(new(){buttons=0x7000},p,.02,Send);
        Check(events.SequenceEqual(new MouseEvent[]{new(0,0,32),new(0,0,128,1),new(0,0,128,2)}),"middle and X-button data/flags");
        Check(!engine.Stop(_=>false)&&engine.Pending,"all rejected releases must remain pending");
        events.Clear();Check(engine.Stop(Send)&&!engine.Pending,"retry rejected releases");
        Check(events.SequenceEqual(new MouseEvent[]{new(0,0,64),new(0,0,256,1),new(0,0,256,2)}),"release each of the five buttons");

        p=Profile((PadControl.LeftTrigger,MouseAction.LeftClick));engine.Stop(Send);events.Clear();engine.Step(default,p,.02,Send);
        foreach(byte value in new byte[]{139,140,130,91,89})engine.Step(new(){lt=value},p,.02,Send);
        Check(events.Select(e=>e.Flags).SequenceEqual(new uint[]{2,4}),"trigger threshold and hysteresis");
        engine.Stop(Send);events.Clear();engine.Step(new(){lt=255},p,.02,Send);engine.Step(new(){lt=200},p,.02,Send);
        Check(events.Count==0,"trigger held on reconnect cannot click");engine.Step(default,p,.02,Send);

        p=Profile((PadControl.A,MouseAction.DoubleLeftClick));engine.Stop(Send);events.Clear();engine.Step(default,p,.02,Send);
        engine.Step(new(){buttons=0x1000},p,.02,Send);engine.Step(new(){buttons=0x1000},p,.02,Send);
        Check(events.Select(e=>e.Flags).SequenceEqual(new uint[]{2,4,2,4}),"double click once per press");
        engine.Stop(Send);engine.Step(default,p,.02,Send);int calls=0;
        Check(engine.Step(new(){buttons=0x1000},p,.02,e=>++calls==1)==InputResult.Rejected && engine.Pending,"double-click release rejection must be tracked");
        Check(engine.Stop(Send)&&!engine.Pending,"double-click failure cleanup");
        p=Profile((PadControl.A,MouseAction.ToggleDrag),(PadControl.B,MouseAction.LeftClick),(PadControl.X,MouseAction.DoubleLeftClick));
        engine.Step(default,p,.02,Send);events.Clear();
        foreach(ushort bits in new ushort[]{0x1000,0,0x4000,0,0x2000,0,0x1000})engine.Step(new(){buttons=bits},p,.02,Send);
        Check(events.Select(e=>e.Flags).SequenceEqual(new uint[]{2,4}),"toggle drag persists; direct hold and double click cannot release it");
        engine.Stop(Send);

        foreach(var (action,flag,data) in new[]{(MouseAction.ScrollUp,0x800u,120),(MouseAction.ScrollDown,0x800u,-120),(MouseAction.ScrollLeft,0x1000u,-120),(MouseAction.ScrollRight,0x1000u,120)})
        {
            p=Profile((PadControl.A,action));engine.Stop(Send);events.Clear();engine.Step(default,p,.02,Send);
            for(int i=0;i<10;i++)engine.Step(new(){buttons=0x1000},p,.02,Send);
            Check(events.Count==2 && events.All(e=>e.Flags==flag && e.Data==unchecked((uint)data)),"wheel direction and bounded repeat "+action);
            int count=events.Count;engine.Step(default,p,.05,Send);Check(events.Count==count,"wheel released");
        }
        p=Profile((PadControl.A,MouseAction.ScrollUp));p.ScrollSpeed=30;engine.Stop(Send);events.Clear();engine.Step(default,p,.02,Send);
        for(int i=0;i<100;i++)engine.Step(new(){buttons=0x1000},p,.01,Send);
        Check(events.Count==30,"30 Hz repeat must retain fractional timing");
        p=Profile();p.RightStickRole=StickRole.Scroll;p.ScrollSpeed=10;
        engine.Stop(Send);events.Clear();engine.Step(default,p,.05,Send);
        for(int i=0;i<3;i++)engine.Step(new(){rx=32767,ry=0},p,.05,Send);
        Check(events.Count==1 && events[0].Flags==0x1000 && events[0].Data==120,"analog horizontal scrolling");
        engine.Stop(Send);events.Clear();engine.Step(default,p,.05,Send);
        for(int i=0;i<3;i++)engine.Step(new(){ry=-32768},p,.05,Send);
        Check(events.Count==1 && events[0].Flags==0x800 && events[0].Data==unchecked((uint)-120),"analog downward scrolling");

        p=Profile((PadControl.A,MouseAction.MoveRight),(PadControl.B,MouseAction.Precision));
        engine.Stop(Send);events.Clear();engine.Step(default,p,.02,Send);engine.Step(new(){buttons=0x1000},p,.02,Send);
        Check(events.Single().X==22,"button cursor movement");
        engine.Stop(Send);events.Clear();engine.Step(default,p,.02,Send);engine.Step(new(){buttons=0x3000},p,.02,Send);
        Check(events.Single().X==5,"precision speed");
        p=Profile();p.RightStickRole=StickRole.Cursor;
        engine.Stop(Send);events.Clear();engine.Step(new(){rx=32767},p,.05,Send);Check(events.Count==0,"enabled stick neutral gate");
        engine.Step(default,p,.05,Send);engine.Step(new(){rx=32767},p,.05,Send);Check(events.Single().X>50,"right stick cursor role");

        p=Profile((PadControl.A,MouseAction.LeftClick),(PadControl.B,MouseAction.Pause));engine.Stop(Send);events.Clear();engine.Step(default,p,.02,Send);
        engine.Step(new(){buttons=0x1000},p,.02,Send);Check(engine.Step(new(){buttons=0x3000},p,.02,Send)==InputResult.Pause&&!engine.Pending,"mapped pause releases held mouse");
        var old=Preferences.Normalize(JsonSerializer.Deserialize<Preferences>("{\"Speed\":1800,\"InvertY\":true}")!);
        Check(old.Speed==1800 && old.InvertY && Bindings.Action(old,PadControl.LeftStick)==MouseAction.LeftClick,"legacy settings migration");
        old.Buttons=new(){{"A",(MouseAction)999},{"LeftStick",MouseAction.None},{"unknown",MouseAction.Pause}};old.LeftStickRole=(StickRole)999;old.ScrollSpeed=999;
        var clean=Preferences.Normalize(old);
        Check(clean.Buttons.Count==16 && Bindings.Action(clean,PadControl.A)==MouseAction.None && Bindings.Action(clean,PadControl.LeftStick)==MouseAction.None && clean.LeftStickRole==StickRole.Cursor && clean.ScrollSpeed==30,"invalid mapping normalization");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"mapping-test.txt"),"PASS: defaults/migration, all mouse buttons/X data, shared ownership, release failure/retry, trigger hysteresis/neutral gate, double-click failure, drag toggle, wheel directions/repeat, stick roles, cursor/precision, mapped pause");
    }
}
