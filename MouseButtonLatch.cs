namespace StickMouse;

// A button is considered released only after Windows accepts its release event.
sealed class MouseButtonLatch
{
    public bool Left { get; private set; }
    public bool Right { get; private set; }
    public bool Pending => Left || Right;
    public bool Update(bool left, bool right, Func<uint, bool> send)
    {
        if (left != Left) { if (!send(left ? 2u : 4u)) return false; Left = left; }
        if (right != Right) { if (!send(right ? 8u : 16u)) return false; Right = right; }
        return true;
    }
    public bool Release(Func<uint, bool> send)
    {
        bool ok = true;
        if (Left) { if (send(4)) Left = false; else ok = false; }
        if (Right) { if (send(16)) Right = false; else ok = false; }
        return ok;
    }
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
