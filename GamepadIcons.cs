using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace StickMouse;

static class GamepadIcons
{
    internal static void Draw(Graphics graphics,RectangleF bounds)
    {
        var state=graphics.Save();graphics.TranslateTransform(bounds.X,bounds.Y);graphics.ScaleTransform(bounds.Width/64,bounds.Height/64);
        graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var body=new GraphicsPath();
        body.AddBezier(18,15,10,13,8,27,5,39);body.AddBezier(5,39,1,55,12,56,22,43);
        body.AddLine(22,43,42,43);body.AddBezier(42,43,52,56,63,55,59,39);
        body.AddBezier(59,39,56,27,54,13,46,15);body.AddBezier(46,15,36,12,28,12,18,15);body.CloseFigure();
        using var fill=new SolidBrush(Color.FromArgb(157,218,190));using var border=new Pen(Color.FromArgb(38,78,62),2);
        graphics.FillPath(fill,body);graphics.DrawPath(border,body);
        using var controls=new Pen(Color.FromArgb(24,48,39),4){StartCap=LineCap.Round,EndCap=LineCap.Round};
        graphics.DrawLine(controls,14,28,26,28);graphics.DrawLine(controls,20,22,20,34);
        using var dark=new SolidBrush(Color.FromArgb(24,48,39));
        graphics.FillEllipse(dark,42,21,5,5);graphics.FillEllipse(dark,49,28,5,5);graphics.FillEllipse(dark,35,28,5,5);graphics.FillEllipse(dark,42,35,5,5);
        graphics.Restore(state);
    }
    internal static Icon Create()
    {
        using var bitmap=new Bitmap(64,64);using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);Draw(g,new RectangleF(0,0,64,64));}
        IntPtr handle=bitmap.GetHicon();
        try{using var temporary=Icon.FromHandle(handle);return (Icon)temporary.Clone();}finally{DestroyIcon(handle);}
    }
    internal static void SaveIco(string path)
    {
        int[] sizes={16,24,32,48,64,256};var images=new List<byte[]>();
        foreach(int size in sizes){using var bitmap=new Bitmap(size,size);using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);Draw(g,new RectangleF(0,0,size,size));}using var ms=new MemoryStream();bitmap.Save(ms,ImageFormat.Png);images.Add(ms.ToArray());}
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var writer=new BinaryWriter(File.Create(path));writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
        for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}
        foreach(var bytes in images)writer.Write(bytes);
    }
    [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr handle);
}
