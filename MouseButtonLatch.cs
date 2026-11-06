namespace StickMouse;

[Flags] enum MouseHold { None=0, Left=1, Right=2, Middle=4, Back=8, Forward=16 }

// Track accepted transitions, including a rejected release until it can be retried.
sealed class MouseButtonLatch
{
    MouseHold held;
    public bool Left => (held & MouseHold.Left)!=0;
    public bool Right => (held & MouseHold.Right)!=0;
    public bool Pending => held!=MouseHold.None;
    static readonly (MouseHold button,uint down,uint up,uint data)[] Events = {
        (MouseHold.Left,2,4,0),(MouseHold.Right,8,16,0),(MouseHold.Middle,32,64,0),
        (MouseHold.Back,128,256,1),(MouseHold.Forward,128,256,2) };
    public bool Update(MouseHold desired,Func<MouseEvent,bool> send)
    {
        // Release stale owners first, before any new press.
        foreach(var e in Events) if((held&e.button)!=0 && (desired&e.button)==0)
        { if(!send(new(0,0,e.up,e.data)))return false; held &= ~e.button; }
        foreach(var e in Events) if((held&e.button)==0 && (desired&e.button)!=0)
        { if(!send(new(0,0,e.down,e.data)))return false; held |= e.button; }
        return true;
    }
    public bool ReleaseEvents(Func<MouseEvent,bool> send)
    {
        bool ok=true;
        foreach(var e in Events) if((held&e.button)!=0)
        { if(send(new(0,0,e.up,e.data)))held &= ~e.button; else ok=false; }
        return ok;
    }
    public bool Update(bool left,bool right,Func<uint,bool> send) => Update((left?MouseHold.Left:0)|(right?MouseHold.Right:0), e=>send(e.Flags));
    public bool Release(Func<uint,bool> send) => ReleaseEvents((MouseEvent e)=>send(e.Flags));
    public static void Test()
    {
        var latch = new MouseButtonLatch(); var events = new List<uint>();
        bool Accept(uint n) { events.Add(n); return true; }
        if (!latch.Update(true,true,Accept) || !events.SequenceEqual(new uint[] {2,8})) throw new Exception("Button mapping");
        latch.Update(true,true,Accept); if(events.Count != 2)throw new Exception("Duplicate mouse down");
        if(latch.Release(_ => false) || !latch.Left || !latch.Right)throw new Exception("Lost pending release");
        if(!latch.Release(Accept) || latch.Pending || !events.SequenceEqual(new uint[]{2,8,4,16}))throw new Exception("Release retry");
        if(latch.Update(true,false,_=>false)||latch.Pending)throw new Exception("Rejected press recorded");
        events.Clear(); latch.Update(true,true,n=> n==2 && Accept(n));
        if(!latch.Left||latch.Right)throw new Exception("Partial update state");
        latch.Release(Accept);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"input-test.txt"),"PASS: L3/R3 flags, held transitions, release rejection/retry, rejected press, partial input failure");
    }
}
