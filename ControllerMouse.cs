namespace StickMouse;

readonly record struct MouseEvent(int X, int Y, uint Flags, uint Data = 0);
enum InputResult { Waiting, Active, Pause, Rejected }

// All state is independent of WinForms and SendInput so failure paths can be exercised without moving the pointer.
sealed class ControllerMouse
{
    readonly MouseButtonLatch buttons = new();
    bool armed, leftTrigger, rightTrigger, drag;
    ulong previous;
    double remainderX, remainderY, wheelX, wheelY;
    static readonly ushort[] PadBits = { 0x40, 0x80, 0x1000, 0x2000, 0x4000, 0x8000, 0x100, 0x200, 0, 0, 1, 2, 4, 8, 0x10, 0x20 };
    static readonly (MouseAction action,MouseHold button)[] DoubleClicks = { (MouseAction.DoubleLeftClick,MouseHold.Left), (MouseAction.DoubleRightClick,MouseHold.Right) };
    static readonly MouseAction[] ScrollActions = {MouseAction.ScrollUp,MouseAction.ScrollDown,MouseAction.ScrollLeft,MouseAction.ScrollRight};
    readonly double[] repeats = new double[4];
    internal bool Pending => buttons.Pending;
    internal bool Stop(Func<MouseEvent, bool> send)
    {
        armed = leftTrigger = rightTrigger = drag = false;
        previous = 0; remainderX = remainderY = wheelX = wheelY = 0; Array.Clear(repeats);
        return buttons.ReleaseEvents(send);
    }
    uint Controls(Native.Gamepad pad)
    {
        // Hysteresis prevents a trigger near its threshold from repeatedly clicking.
        leftTrigger = pad.lt >= (leftTrigger ? 90 : 140);
        rightTrigger = pad.rt >= (rightTrigger ? 90 : 140);
        uint mask = 0;
        for (int i = 0; i < PadBits.Length; i++) if ((pad.buttons & PadBits[i]) != 0) mask |= 1u << i;
        if (leftTrigger) mask |= 1u << (int)PadControl.LeftTrigger;
        if (rightTrigger) mask |= 1u << (int)PadControl.RightTrigger;
        return mask;
    }
    internal InputResult Step(Native.Gamepad pad, Preferences p, double dt, Func<MouseEvent, bool> send)
    {
        dt = Math.Clamp(dt, 0, .05);
        uint controls = Controls(pad);
        ulong actions = 0;
        foreach (var c in Bindings.Controls) if ((controls & (1u << (int)c)) != 0) actions |= 1ul << (int)Bindings.Action(p, c);
        actions &= ~1ul; // Unassigned controls do not prevent arming.
        if (!armed)
        {
            if (buttons.Pending && !buttons.ReleaseEvents(send)) return InputResult.Rejected;
            // Require mapped buttons/triggers and enabled sticks to return to neutral after start/remap.
            bool Neutral(short x, short y, StickRole role) => role == StickRole.Disabled || Motion.Velocity(x,y,1,p.DeadZone/100.0).Equals((0.0,0.0));
            if (actions == 0 && Neutral(pad.lx,pad.ly,p.LeftStickRole) && Neutral(pad.rx,pad.ry,p.RightStickRole)) armed = true;
            return InputResult.Waiting;
        }
        ulong rising = actions & ~previous; previous = actions;
        bool Held(MouseAction a) => (actions & (1ul << (int)a)) != 0;
        bool Pressed(MouseAction a) => (rising & (1ul << (int)a)) != 0;
        if (Pressed(MouseAction.Pause)) return Stop(send) ? InputResult.Pause : InputResult.Rejected;
        if (Pressed(MouseAction.ToggleDrag)) drag = !drag;
        MouseHold desired = MouseHold.None;
        if (Held(MouseAction.LeftClick) || drag) desired |= MouseHold.Left;
        if (Held(MouseAction.RightClick)) desired |= MouseHold.Right;
        if (Held(MouseAction.MiddleClick)) desired |= MouseHold.Middle;
        if (Held(MouseAction.Back)) desired |= MouseHold.Back;
        if (Held(MouseAction.Forward)) desired |= MouseHold.Forward;
        if (!buttons.Update(desired, send)) return InputResult.Rejected;
        // Never release a button owned by a simultaneous hold/drag to synthesize a double-click.
        foreach (var (action, button) in DoubleClicks)
            if (Pressed(action) && (desired & button) == 0)
                for (int i=0;i<2;i++)
                    if (!buttons.Update(desired|button,send) || !buttons.Update(desired,send)) return InputResult.Rejected;

        double vx=0, vy=0, sx=0, sy=0;
        void Stick(short x,short y,StickRole role)
        {
            if (role == StickRole.Disabled) return;
            var v=Motion.Velocity(x,y,role==StickRole.Cursor?p.Speed:p.ScrollSpeed,p.DeadZone/100.0,p.Response/10.0);
            if(role==StickRole.Cursor){vx+=v.x;vy+=p.InvertY?-v.y:v.y;}
            else{sx+=v.x;sy-=v.y;}
        }
        Stick(pad.lx,pad.ly,p.LeftStickRole); Stick(pad.rx,pad.ry,p.RightStickRole);
        vx += (Held(MouseAction.MoveRight)?p.Speed:0)-(Held(MouseAction.MoveLeft)?p.Speed:0);
        vy += (Held(MouseAction.MoveDown)?p.Speed:0)-(Held(MouseAction.MoveUp)?p.Speed:0);
        double magnitude=Math.Sqrt(vx*vx+vy*vy);
        if(magnitude>p.Speed){vx*=p.Speed/magnitude;vy*=p.Speed/magnitude;}
        if(Held(MouseAction.Precision)){vx*=.25;vy*=.25;}
        remainderX+=vx*dt;remainderY+=vy*dt;
        int dx=(int)remainderX,dy=(int)remainderY;remainderX-=dx;remainderY-=dy;
        if((dx!=0 || dy!=0) && !send(new(dx,dy,1))) return InputResult.Rejected;

        for(int i=0;i<ScrollActions.Length;i++)
        {
            if(!Held(ScrollActions[i])){repeats[i]=0;continue;}
            double period=1.0/p.ScrollSpeed;
            if(Pressed(ScrollActions[i]))repeats[i]=0;else repeats[i]-=dt;
            // Preserve fractional timing; timer jitter must not reduce the selected repeat rate.
            while(repeats[i]<=0)
            {
                repeats[i]+=period;
                if(!send(new(0,0,i<2?0x800u:0x1000u,unchecked((uint)(i is 0 or 3?120:-120)))))return InputResult.Rejected;
            }
        }
        // Full detents work with applications that do not support high-resolution wheel deltas.
        sx=Math.Clamp(sx,-p.ScrollSpeed,p.ScrollSpeed);sy=Math.Clamp(sy,-p.ScrollSpeed,p.ScrollSpeed);
        if(sx==0)wheelX=0;else wheelX+=sx*dt;
        if(sy==0)wheelY=0;else wheelY+=sy*dt;
        int wx=(int)wheelX,wy=(int)wheelY;wheelX-=wx;wheelY-=wy;
        if(wx!=0 && !send(new(0,0,0x1000,unchecked((uint)(wx*120)))))return InputResult.Rejected;
        if(wy!=0 && !send(new(0,0,0x800,unchecked((uint)(wy*120)))))return InputResult.Rejected;
        return InputResult.Active;
    }
}
