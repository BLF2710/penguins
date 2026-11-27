// Desktop Penguins - little cartoon penguins that drift onto your desktop
// (by umbrella, or tumbling), waddle along the taskbar and the tops of your
// windows, climb window sides, ride on each other's heads, greet each other,
// and get up to things: belly-slides, waving, naps, dances, hops, flights.
//
// Interact:  hover = they look at you, click = jump, drag/fling = throw them,
//            right-click = menu, shake a window = they fly off,
//            Ctrl+Alt+P = shoo them away for a minute (again = bring them back).
//
// Build (no extra installs needed on Windows):
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:DesktopPenguins.exe DesktopPenguins.cs
// Run:  DesktopPenguins.exe [numberOfPenguins]     (default 5)
//       DesktopPenguins.exe --preview sheet.png    (save all frames to an image)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace DesktopPenguins
{
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage,
            out IntPtr bits, IntPtr section, uint offset);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT r, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int v, int size);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);

        [StructLayout(LayoutKind.Sequential)] public struct LASTINPUTINFO { public int cbSize; public uint dwTime; }
        [DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public int cbSize, flags;
            public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
            public RECT rcCaret;
        }
        [DllImport("user32.dll")] public static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
    }

    // ---------------------------------------------------------------- artwork

    static class Art
    {
        public const int W = 44, H = 52;             // sprite window size in pixels (room for umbrella / Zs)
        public const int HeadH = 28;                  // feet-to-top-of-head height in pixels (for stacking)
        const float K = 0.24f;                        // pixels per design unit
        const float DW = W / K, DH = H / K;           // canvas in design units
        const float PX = (DW - 150f) / 2f, PY = DH - 125f; // where the 150x125 penguin box sits (bottom centre)
        const int SS = 4;                             // supersampling factor

        public const int WalkA = 0, WalkB = 1, Stand = 2, ArmsUp = 3, FlyA = 4, FlyB = 5, WaveA = 6, WaveB = 7,
                         SleepA = 8, SleepB = 9, Slide = 10, Umbrella = 11, Spin90 = 12, Spin180 = 13, Spin270 = 14,
                         ClimbA = 15, ClimbB = 16, BuildA = 17, BuildB = 18, PullA = 19, PullB = 20,
                         YawnA = 21, YawnB = 22, SwayL = 23, SwayR = 24, TypeA = 25, TypeB = 26,
                         ShakeL = 27, ShakeR = 28, DiveA = 29, DiveB = 30, PlantA = 31, PlantB = 32,
                         DazedA = 33, DazedB = 34;
        public const int Frames = 35;

        // Sleep frames contain "z" letters, which shouldn't be mirrored.
        public static bool Mirrorable(int f) { return f != SleepA && f != SleepB; }

        // Rendered facing right; mirror for left.
        public static Bitmap Render(int frame)
        {
            using (var big = new Bitmap(W * SS, H * SS, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(SS * K, SS * K);
                    switch (frame)
                    {
                        case WalkA:   At(g); Penguin(g, 8, 20, -4, -5, 3, 0, false); break;
                        case WalkB:   At(g); Penguin(g, 20, 8, 3, 0, -4, -5, false); break;
                        case Stand:   At(g); Penguin(g, 12, 12, 0, 0, 0, 0, false); break;
                        case ArmsUp:  At(g); Penguin(g, 150, 150, -4, -3, 4, -3, false); break;
                        case FlyA:    At(g); Penguin(g, 100, 100, -2, -2, 2, -2, false); break;
                        case FlyB:    At(g); Penguin(g, 65, 65, -2, -2, 2, -2, false); break;
                        case WaveA:   At(g); Penguin(g, 12, 155, 0, 0, 0, 0, false); break;
                        case WaveB:   At(g); Penguin(g, 12, 115, 0, 0, 0, 0, false); break;
                        case SleepA:  At(g); Penguin(g, 6, 6, 0, 0, 0, 0, true); Zs(g, false); break;
                        case SleepB:  At(g); Penguin(g, 6, 6, 0, 0, 0, 0, true); Zs(g, true); break;
                        case Slide:   Rotated(g, DW / 2, DH - 40, 90); Penguin(g, 6, 6, 0, 0, 0, 0, false); break;
                        case Umbrella:
                            At(g); UmbrellaShape(g, 104);
                            Penguin(g, 22, 176, -3, -2, 3, -2, false); break;
                        case Spin90:  Rotated(g, DW / 2, PY + 62.5f, 90);  Penguin(g, 150, 150, -4, -3, 4, -3, false); break;
                        case Spin180: Rotated(g, DW / 2, PY + 62.5f, 180); Penguin(g, 150, 150, -4, -3, 4, -3, false); break;
                        case Spin270: Rotated(g, DW / 2, PY + 62.5f, 270); Penguin(g, 150, 150, -4, -3, 4, -3, false); break;
                        case ClimbA:  At(g); Penguin(g, 40, 168, 3, 0, -2, -8, false); break;
                        case ClimbB:  At(g); Penguin(g, 168, 40, 3, -8, -2, 0, false); break;
                        case BuildA:  AtFeet(g, 9);   PenguinF(g, 30, 55, -2, 0, 4, 0, 0); break;    // patting snow
                        case BuildB:  AtFeet(g, 9);   PenguinF(g, 45, 88, -2, 0, 4, 0, 0); break;
                        case PullA:   AtFeet(g, -16); PenguinF(g, 70, 82, 6, 0, -2, 0, 0); break;    // leaning back on a rope
                        case PullB:   AtFeet(g, -22); PenguinF(g, 76, 88, 8, 0, -4, 0, 0); break;
                        case YawnA:   At(g); PenguinF(g, 22, 22, 0, 0, 0, 0, 2); break;
                        case YawnB:   At(g); PenguinF(g, 165, 165, -3, -2, 3, -2, 2); break;        // big stretch
                        case SwayL:   AtFeet(g, -14); PenguinF(g, 150, 150, -4, -3, 4, -3, 0); break;
                        case TypeA:   AtFeet(g, 6); PenguinF(g, 30, 80, 0, 0, 0, 0, 0); break;      // tapping along
                        case TypeB:   AtFeet(g, 6); PenguinF(g, 62, 48, 0, 0, 0, 0, 0); break;
                        case ShakeL:  At(g); PenguinF(g, 14, 14, 0, 0, 0, 0, 0, -1f); break;          // head shake "no"
                        case ShakeR:  At(g); PenguinF(g, 14, 14, 0, 0, 0, 0, 0, 1f); break;
                        // nose-dive: head first, flippers flapping
                        case DiveA:   Rotated(g, DW / 2, PY + 62.5f, 172); PenguinF(g, 150, 150, -4, -3, 4, -3, 0); break;
                        case DiveB:   Rotated(g, DW / 2, PY + 62.5f, 188); PenguinF(g, 110, 110, -2, -5, 2, -5, 0); break;
                        // faceplant: beak in the ground, bottom up, feet wiggling
                        case PlantA:  Rotated(g, DW / 2, DH - 50, 140); PenguinF(g, 35, 60, -5, -6, 4, 0, 0); break;
                        case PlantB:  Rotated(g, DW / 2, DH - 50, 140); PenguinF(g, 60, 35, 4, 0, -5, -6, 0); break;
                        // dazed: wobbling, with stars circling the head
                        case DazedA:  AtFeet(g, -5); PenguinF(g, 18, 10, 0, 0, 0, 0, 0); Stars(g, 0); break;
                        case DazedB:  AtFeet(g, 5);  PenguinF(g, 10, 18, 0, 0, 0, 0, 0); Stars(g, 60); break;
                        default:      AtFeet(g, 14);  PenguinF(g, 150, 150, -4, -3, 4, -3, 0); break; // SwayR
                    }
                }
                var small = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(small))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.DrawImage(big, new Rectangle(0, 0, W, H));
                }
                return small;
            }
        }

        static void At(Graphics g) { g.TranslateTransform(PX, PY); }

        static void Rotated(Graphics g, float px, float py, float angle)
        {
            g.TranslateTransform(px, py);
            g.RotateTransform(angle);
            g.TranslateTransform(-75f, -62.5f);
        }

        // Rotate the whole penguin about its feet (positive = lean forward/right).
        static void AtFeet(Graphics g, float angle)
        {
            g.TranslateTransform(DW / 2, DH - 2);
            g.RotateTransform(angle);
            g.TranslateTransform(-75f, -123f);
        }

        static void Penguin(Graphics g, float armL, float armR, float flx, float fly, float frx, float fry, bool asleep)
        {
            PenguinF(g, armL, armR, flx, fly, frx, fry, asleep ? 1 : 0);
        }

        // Cartoon penguin in a 150x125 box, facing right.
        // armL/armR: flipper angle outward from hanging down, in degrees.  fl*/fr*: foot offsets.
        // face: 0 eyes open, 1 asleep, 2 yawning (eyes shut, beak wide open)
        // headTurn: 0 = normal 3/4 view; otherwise the body faces front and only the face
        //           turns (-1 left .. 1 right) with a frown, for shaking the head "no".
        static void PenguinF(Graphics g, float armL, float armR, float flx, float fly, float frx, float fry, int face,
                             float headTurn = 0f)
        {
            bool asleep = face != 0;
            bool shaking = headTurn != 0;
            const float cx = 75f;
            float d = shaking ? 0f : 4f;                      // body / belly offset
            float fd = shaking ? headTurn * 10f : 4f;         // face offset
            float look = shaking ? 3.75f + headTurn * 3f : 6.5f;   // pupil position inside the eye
            using (var rim = new Pen(Color.FromArgb(95, 95, 108), 3f))
            using (var black = new SolidBrush(Color.FromArgb(18, 18, 22)))
            {
                Foot(g, cx - 29 + flx + d * 0.5f, 108 + fly);
                Foot(g, cx + 3 + frx + d * 0.5f, 108 + fry);

                Flipper(g, cx - 25, 54, armL, black, rim);
                Flipper(g, cx + 25, 54, -armR, black, rim);

                var head = new RectangleF(cx - 25, 5, 50, 50);
                var body = new RectangleF(cx - 34, 33, 68, 83);
                g.DrawEllipse(rim, head);
                g.DrawEllipse(rim, body);
                g.FillEllipse(black, head);
                g.FillEllipse(black, body);

                var belly = new RectangleF(cx - 25 + d, 53, 50, 60);
                using (var b = new LinearGradientBrush(belly, Color.White, Color.FromArgb(214, 214, 222), 90f))
                    g.FillEllipse(b, belly);

                GraphicsState faceState = null;
                if (shaking)
                {   // tilt the face a little toward the way it's turning
                    faceState = g.Save();
                    g.TranslateTransform(cx, 30);
                    g.RotateTransform(headTurn * 12f);
                    g.TranslateTransform(-cx, -30);
                }

                if (asleep)
                {
                    using (var lid = new Pen(Color.FromArgb(235, 235, 240), 2.6f))
                    {
                        g.DrawArc(lid, cx - 15 + fd, 20, 13, 10, 15, 150);
                        g.DrawArc(lid, cx + 2 + fd, 20, 13, 10, 15, 150);
                    }
                }
                else
                {
                    using (var white = new SolidBrush(Color.White))
                    {
                        g.FillEllipse(white, cx - 15 + fd, 16, 13, 18);
                        g.FillEllipse(white, cx + 2 + fd, 16, 13, 18);
                    }
                    g.FillEllipse(black, cx - 15 + fd + look, 22, 5.5f, 9);
                    g.FillEllipse(black, cx + 2 + fd + look, 22, 5.5f, 9);
                }

                if (shaking)
                {   // frowning brows: a "V" over the eyes
                    using (var brow = new Pen(Color.FromArgb(205, 205, 215), 2.8f))
                    {
                        brow.StartCap = brow.EndCap = LineCap.Round;
                        g.DrawLine(brow, cx - 16 + fd, 11, cx - 4 + fd, 15.5f);
                        g.DrawLine(brow, cx + 16 + fd, 11, cx + 4 + fd, 15.5f);
                    }
                }

                if (face == 2)
                {   // yawn: upper and lower beak apart, dark mouth between
                    float bx = cx + fd * 1.4f;
                    using (var m = new SolidBrush(Color.FromArgb(120, 30, 45)))
                        g.FillEllipse(m, bx - 11, 37, 22, 17);
                    var up = new RectangleF(bx - 16, 31, 32, 12);
                    var lo = new RectangleF(bx - 13, 49, 26, 9);
                    using (var b = new LinearGradientBrush(up, Color.FromArgb(255, 214, 70), Color.FromArgb(232, 145, 0), 90f))
                        g.FillEllipse(b, up);
                    using (var b = new LinearGradientBrush(lo, Color.FromArgb(250, 190, 40), Color.FromArgb(220, 130, 0), 90f))
                        g.FillEllipse(b, lo);
                }
                else
                {
                    var beak = new RectangleF(cx - 16 + fd * 1.4f, 34, 32, 15);
                    using (var b = new LinearGradientBrush(beak, Color.FromArgb(255, 214, 70), Color.FromArgb(232, 145, 0), 90f))
                        g.FillEllipse(b, beak);
                    using (var p = new Pen(Color.FromArgb(180, 110, 0), 1.6f))
                    {
                        if (shaking) g.DrawArc(p, cx - 10 + fd * 1.4f, 41, 20, 8, 200, 140);   // turned-down mouth
                        else g.DrawArc(p, cx - 12 + fd * 1.4f, 37, 24, 8, 20, 140);
                    }
                }
                if (faceState != null) g.Restore(faceState);
            }
        }

        static void Foot(Graphics g, float x, float y)
        {
            var r = new RectangleF(x, y, 26, 15);
            using (var b = new LinearGradientBrush(r, Color.FromArgb(255, 196, 50), Color.FromArgb(222, 125, 0), 90f))
                g.FillEllipse(b, r);
            using (var p = new Pen(Color.FromArgb(160, 90, 0), 1.5f))
                g.DrawEllipse(p, r);
        }

        static void Flipper(Graphics g, float px, float py, float angle, Brush fill, Pen rim)
        {
            var st = g.Save();
            g.TranslateTransform(px, py);
            g.RotateTransform(angle);
            var r = new RectangleF(-8, -5, 16, 46);
            g.DrawEllipse(rim, r);
            g.FillEllipse(fill, r);
            g.Restore(st);
        }

        // A little striped umbrella held up by the right flipper; sx = stick x in penguin-box units.
        static void UmbrellaShape(Graphics g, float sx)
        {
            using (var stick = new Pen(Color.FromArgb(70, 60, 55), 3.2f))
                g.DrawLine(stick, sx, -48, sx, 18);
            var canopy = new RectangleF(sx - 54, -80, 108, 60);
            Color[] panels = {
                Color.FromArgb(235, 70, 80), Color.FromArgb(250, 245, 235), Color.FromArgb(70, 150, 230),
                Color.FromArgb(250, 245, 235), Color.FromArgb(235, 70, 80), Color.FromArgb(250, 245, 235) };
            for (int i = 0; i < panels.Length; i++)
                using (var b = new SolidBrush(panels[i]))
                    g.FillPie(b, canopy.X, canopy.Y, canopy.Width, canopy.Height, 180 + i * 30, 30);
            using (var edge = new Pen(Color.FromArgb(90, 40, 50), 2.2f))
            {
                g.DrawArc(edge, canopy, 180, 180);
                g.DrawLine(edge, canopy.Left, canopy.Top + canopy.Height / 2, canopy.Right, canopy.Top + canopy.Height / 2);
            }
            using (var knob = new SolidBrush(Color.FromArgb(70, 60, 55)))
                g.FillEllipse(knob, sx - 4, -86, 8, 8);
        }

        // Three little stars on a ring above the head; `start` rotates the ring.
        static void Stars(Graphics g, float start)
        {
            for (int i = 0; i < 3; i++)
            {
                double a = (start + i * 120) * Math.PI / 180;
                float x = 79 + (float)Math.Cos(a) * 24, y = -12 + (float)Math.Sin(a) * 6;
                var pts = new PointF[10];
                for (int k = 0; k < 10; k++)
                {
                    double t = -Math.PI / 2 + k * Math.PI / 5;
                    float r = k % 2 == 0 ? 9.5f : 4f;
                    pts[k] = new PointF(x + (float)Math.Cos(t) * r, y + (float)Math.Sin(t) * r);
                }
                using (var b = new SolidBrush(Color.FromArgb(255, 222, 60))) g.FillPolygon(b, pts);
                using (var p = new Pen(Color.FromArgb(185, 130, 0), 1.6f)) g.DrawPolygon(p, pts);
            }
        }

        static void Zs(Graphics g, bool two)
        {
            Z(g, 108, -4, 12);
            if (two) Z(g, 122, -34, 17);
        }

        static void Z(Graphics g, float x, float y, float s)
        {
            var pts = new[] { new PointF(x, y), new PointF(x + s, y), new PointF(x, y + s), new PointF(x + s, y + s) };
            using (var dark = new Pen(Color.FromArgb(35, 55, 90), 6.5f))
            using (var light = new Pen(Color.FromArgb(200, 230, 255), 3f))
            {
                dark.LineJoin = light.LineJoin = LineJoin.Round;
                dark.StartCap = dark.EndCap = light.StartCap = light.EndCap = LineCap.Round;
                g.DrawLines(dark, pts);
                g.DrawLines(light, pts);
            }
        }

        // Premultiplied 32-bit DIB for UpdateLayeredWindow.
        public static IntPtr ToHBitmap(Bitmap src)
        {
            var bi = new Native.BITMAPINFOHEADER();
            bi.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bi.biWidth = src.Width; bi.biHeight = -src.Height;   // top-down
            bi.biPlanes = 1; bi.biBitCount = 32;
            IntPtr bits;
            IntPtr hbm = Native.CreateDIBSection(IntPtr.Zero, ref bi, 0, out bits, IntPtr.Zero, 0);
            var data = src.LockBits(new Rectangle(0, 0, src.Width, src.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            var row = new byte[src.Width * 4];
            for (int y = 0; y < src.Height; y++)
            {
                Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), row, 0, row.Length);
                Marshal.Copy(row, 0, new IntPtr(bits.ToInt64() + (long)y * row.Length), row.Length);
            }
            src.UnlockBits(data);
            return hbm;
        }

        // [0] facing right, [1] facing left
        public static Bitmap[,] RenderAll()
        {
            var all = new Bitmap[2, Frames];
            for (int f = 0; f < Frames; f++)
            {
                all[0, f] = Render(f);
                all[1, f] = (Bitmap)all[0, f].Clone();
                if (Mirrorable(f)) all[1, f].RotateFlip(RotateFlipType.RotateNoneFlipX);
            }
            return all;
        }
    }

    // Snowman (stages 0-4 = being built, 5-7 = melting) and the tug-of-war rope.
    static class SnowArt
    {
        public const int W = 40, H = 50, Stages = 8;
        const int SS = 4;

        public static Bitmap Render(int stage)
        {
            using (var big = new Bitmap(W * SS, H * SS, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(SS, SS);
                    switch (stage)
                    {
                        case 0: Ball(g, 20, 44, 6); break;
                        case 1: Ball(g, 20, 39, 11); break;
                        case 2: Ball(g, 20, 39, 11); Ball(g, 20, 23, 8); break;
                        case 3: Ball(g, 20, 39, 11); Ball(g, 20, 23, 8); Ball(g, 20, 10, 6); break;
                        case 4: Full(g); break;
                        case 5:   // sagging
                            g.TranslateTransform(20, 50); g.RotateTransform(7); g.ScaleTransform(1.08f, 0.7f); g.TranslateTransform(-20, -50);
                            Full(g); break;
                        case 6:   // a lump in a puddle
                            Puddle(g, 34, 8);
                            Ball(g, 20, 42, 7);
                            Nose(g, 22, 42, 0.8f); Coal(g, 17, 40); Coal(g, 21, 38);
                            Scarf(g, 9, 44, false); break;
                        default:  // just a puddle with the leftovers
                            Puddle(g, 38, 6);
                            Nose(g, 21, 46, 0.8f); Coal(g, 13, 47); Coal(g, 16, 46); Coal(g, 27, 47);
                            Scarf(g, 6, 45, false); break;
                    }
                }
                return Downsample(big, W, H);
            }
        }

        public static Bitmap RenderRope(int w)
        {
            const int h = 12;
            using (var big = new Bitmap(w * SS, h * SS, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(SS, SS);
                    using (var dark = new Pen(Color.FromArgb(110, 75, 40), 3.4f))
                    using (var light = new Pen(Color.FromArgb(200, 155, 95), 2f))
                    using (var twist = new Pen(Color.FromArgb(130, 90, 50), 1f))
                    {
                        g.DrawLine(dark, 0, 6, w, 6);
                        g.DrawLine(light, 0, 6, w, 6);
                        for (float x = 2; x < w; x += 4) g.DrawLine(twist, x, 5, x + 2, 7);
                    }
                    using (var red = new SolidBrush(Color.FromArgb(225, 45, 55)))
                        g.FillPolygon(red, new[] { new PointF(w / 2f - 3, 6), new PointF(w / 2f + 3, 6), new PointF(w / 2f + 1.5f, 11.5f), new PointF(w / 2f - 1.5f, 11.5f) });
                }
                return Downsample(big, w, h);
            }
        }

        static Bitmap Downsample(Bitmap big, int w, int h)
        {
            var small = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(small))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(big, new Rectangle(0, 0, w, h));
            }
            return small;
        }

        static void Ball(Graphics g, float cx, float cy, float r)
        {
            var rect = new RectangleF(cx - r, cy - r, r * 2, r * 2);
            using (var b = new LinearGradientBrush(rect, Color.White, Color.FromArgb(200, 218, 238), 60f))
                g.FillEllipse(b, rect);
            using (var p = new Pen(Color.FromArgb(140, 162, 190), 0.8f))
                g.DrawEllipse(p, rect);
        }

        static void Full(Graphics g)
        {
            using (var twig = new Pen(Color.FromArgb(105, 70, 40), 1.3f))
            {
                twig.StartCap = twig.EndCap = LineCap.Round;
                g.DrawLine(twig, 14, 23, 3, 15); g.DrawLine(twig, 6, 17, 4, 13);
                g.DrawLine(twig, 26, 23, 37, 14); g.DrawLine(twig, 34, 16, 37, 18);
            }
            Ball(g, 20, 39, 11);
            Ball(g, 20, 23, 8);
            Ball(g, 20, 10, 6);
            Coal(g, 20, 21); Coal(g, 20, 26);                 // buttons
            Coal(g, 17.6f, 8.6f); Coal(g, 22.4f, 8.6f);       // eyes
            Nose(g, 20, 11, 1f);
            Scarf(g, 13.5f, 15, true);
            // knit hat with a pompom
            using (var hat = new SolidBrush(Color.FromArgb(60, 120, 210)))
                g.FillPie(hat, 14, 1, 12, 9, 180, 180);
            using (var band = new SolidBrush(Color.FromArgb(40, 90, 175)))
                g.FillRectangle(band, 13.6f, 4.6f, 12.8f, 1.8f);
            using (var pom = new SolidBrush(Color.FromArgb(245, 245, 250)))
                g.FillEllipse(pom, 18.6f, -0.2f, 2.8f, 2.8f);
        }

        static void Coal(Graphics g, float x, float y)
        {
            using (var b = new SolidBrush(Color.FromArgb(30, 30, 35))) g.FillEllipse(b, x - 1, y - 1, 2, 2);
        }

        static void Nose(Graphics g, float x, float y, float s)
        {
            using (var b = new SolidBrush(Color.FromArgb(245, 130, 20)))
                g.FillPolygon(b, new[] { new PointF(x, y - 1.2f * s), new PointF(x + 7 * s, y + 0.3f * s), new PointF(x, y + 1.2f * s) });
        }

        static void Scarf(Graphics g, float x, float y, bool worn)
        {
            using (var red = new SolidBrush(Color.FromArgb(215, 45, 55)))
            using (var stripe = new SolidBrush(Color.FromArgb(250, 235, 235)))
            {
                if (worn)
                {
                    g.FillRectangle(red, x, y, 13, 3);
                    g.FillRectangle(red, x + 9, y + 1, 3, 8);
                    g.FillRectangle(stripe, x + 9, y + 6, 3, 1);
                }
                else
                {   // lying flat in the puddle
                    g.FillRectangle(red, x, y, 12, 2.4f);
                    g.FillRectangle(stripe, x + 8, y, 1, 2.4f);
                }
            }
        }

        static void Puddle(Graphics g, float w, float h)
        {
            using (var b = new SolidBrush(Color.FromArgb(170, 185, 215, 245)))
                g.FillEllipse(b, 20 - w / 2, 50 - h - 0.5f, w, h);
            using (var p = new Pen(Color.FromArgb(150, 140, 175, 220), 0.7f))
                g.DrawEllipse(p, 20 - w / 2, 50 - h - 0.5f, w, h);
        }
    }

    // Little speech bubbles with an icon, and name tags.
    static class BubbleArt
    {
        public const int W = 24, H = 22;
        public const int Heart = 0, Bang = 1, Question = 2, Note = 3, Zzz = 4, Count = 5;
        const int SS = 4;

        public static Bitmap Render(int icon)
        {
            using (var big = new Bitmap(W * SS, H * SS, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    g.ScaleTransform(SS, SS);

                    // bubble with a little tail pointing down-left toward the penguin
                    using (var path = RoundRect(new RectangleF(1, 1, 22, 15.5f), 6))
                    {
                        path.AddPolygon(new[] { new PointF(5, 15), new PointF(10, 15), new PointF(3.5f, 21) });
                        path.FillMode = FillMode.Winding;
                        using (var fill = new SolidBrush(Color.FromArgb(252, 252, 255))) g.FillPath(fill, path);
                        using (var edge = new Pen(Color.FromArgb(120, 125, 140), 0.9f)) g.DrawPath(edge, path);
                    }
                    using (var cover = new SolidBrush(Color.FromArgb(252, 252, 255)))
                        g.FillRectangle(cover, 5.4f, 14.2f, 4.2f, 1.6f);            // hide the seam under the tail

                    switch (icon)
                    {
                        case Heart:
                            using (var b = new SolidBrush(Color.FromArgb(232, 60, 85)))
                            {
                                g.FillEllipse(b, 7.2f, 4, 5.4f, 5.4f);
                                g.FillEllipse(b, 11.4f, 4, 5.4f, 5.4f);
                                g.FillPolygon(b, new[] { new PointF(7.4f, 7.6f), new PointF(16.6f, 7.6f), new PointF(12, 13.2f) });
                            }
                            break;
                        case Note:
                            using (var b = new SolidBrush(Color.FromArgb(120, 70, 200)))
                            using (var p = new Pen(Color.FromArgb(120, 70, 200), 1.3f))
                            {
                                g.FillEllipse(b, 7.5f, 9.5f, 4.6f, 3.6f);
                                g.FillEllipse(b, 13f, 8.3f, 4.6f, 3.6f);
                                g.DrawLine(p, 11.6f, 11, 11.6f, 3.8f);
                                g.DrawLine(p, 17.1f, 9.8f, 17.1f, 2.6f);
                                g.FillPolygon(b, new[] { new PointF(11, 3.2f), new PointF(17.7f, 2f), new PointF(17.7f, 4.2f), new PointF(11, 5.4f) });
                            }
                            break;
                        default:
                            string text = icon == Bang ? "!" : icon == Question ? "?" : "zzz";
                            Color col = icon == Bang ? Color.FromArgb(225, 90, 20)
                                      : icon == Question ? Color.FromArgb(40, 110, 210) : Color.FromArgb(70, 120, 190);
                            float size = icon == Zzz ? 8f : 11f;
                            using (var f = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel))
                            using (var b = new SolidBrush(col))
                            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                g.DrawString(text, f, b, new RectangleF(1, 1, 22, 15.5f), sf);
                            break;
                    }
                }
                var small = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(small))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(big, new Rectangle(0, 0, W, H));
                }
                return small;
            }
        }

        // A dark rounded tag with the penguin's name in white.
        public static Bitmap RenderTag(string name)
        {
            using (var f = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                SizeF sz;
                using (var tmp = new Bitmap(1, 1)) using (var mg = Graphics.FromImage(tmp)) sz = mg.MeasureString(name, f);
                int w = (int)Math.Ceiling(sz.Width) + 10, h = 18;
                var b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(b))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    using (var path = RoundRect(new RectangleF(0.5f, 0.5f, w - 1, h - 1), 8))
                    using (var bg = new SolidBrush(Color.FromArgb(225, 28, 32, 44)))
                    using (var edge = new Pen(Color.FromArgb(200, 255, 255, 255), 1f))
                    {
                        g.FillPath(bg, path);
                        g.DrawPath(edge, path);
                    }
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(name, f, Brushes.White, new RectangleF(0, 0, w, h + 1), sf);
                }
                return b;
            }
        }

        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ---------------------------------------------------------------- windows

    // One penguin's window: layered, per-pixel alpha. Clicks on fully transparent
    // pixels fall through to whatever is underneath, so only the penguin is clickable.
    class SpriteWindow : Form
    {
        readonly int wd, ht;
        readonly bool through;     // fully click-through (bubbles, name tags)

        public SpriteWindow() : this(Art.W, Art.H) { }

        public SpriteWindow(int w, int h, bool clickThrough = false)
        {
            wd = w; ht = h; through = clickThrough;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
            Size = new Size(w, h);
            Cursor = Cursors.Hand;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 /*LAYERED*/ | 0x80 /*TOOLWINDOW*/ | 0x08000000 /*NOACTIVATE*/ | 0x8 /*TOPMOST*/;
                if (through) cp.ExStyle |= 0x20 /*TRANSPARENT*/;
                return cp;
            }
        }
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21 /*WM_MOUSEACTIVATE*/) { m.Result = new IntPtr(3) /*MA_NOACTIVATE*/; return; }
            base.WndProc(ref m);
        }

        public void Draw(IntPtr screenDC, IntPtr memDC, IntPtr hbm, int x, int y)
        {
            var old = Native.SelectObject(memDC, hbm);
            var pos = new Native.POINT { X = x, Y = y };
            var size = new Native.SIZE { cx = wd, cy = ht };
            var src = new Native.POINT();
            var blend = new Native.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
            Native.UpdateLayeredWindow(Handle, screenDC, ref pos, ref size, memDC, ref src, 0, ref blend, 2 /*ULW_ALPHA*/);
            Native.SelectObject(memDC, old);
        }
    }

    // Invisible window that receives the global "shoo" hotkey.
    class HotkeyWindow : NativeWindow
    {
        public event EventHandler Pressed;
        public HotkeyWindow() { CreateHandle(new CreateParams()); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 /*WM_HOTKEY*/ && Pressed != null) Pressed(this, EventArgs.Empty);
            base.WndProc(ref m);
        }
    }

    // ---------------------------------------------------------------- state

    class Penguin
    {
        public float X, Y, Vx, Vy, Speed;   // X = centre, Y = feet (screen coords)
        public int Dir = 1;                 // 1 right, -1 left
        public int State;
        public bool OnFloor;                // standing on the taskbar / bottom of the work area
        public IntPtr Surf;                 // window it stands on (or climbs)
        public bool Umbrella, Moved, Spin, Near, NextSlide, Shooed;
        public float FlyBaseY, SlideV, HopV, HopOff, GrabDx, GrabDy;
        public Point Down;
        public int AfterLand, Cooldown, BumpCool, ClimbCool, NextDir;
        public int Tick, Timer;
        public Penguin Base, Rider;         // stacking
        public object Job;                  // Snowman or Tug this penguin is part of
        public float TargetX, TugOff;       // where to walk to for a job / place on the rope
        public int FaceDir, NextState, WatchX;
        public int TypeFrame, ShakeTimer;     // typing along with you
        public int NearTicks;                 // how long the cursor has hovered close by
        // speech bubble
        public int Bubble = -1, BubbleTimer, QCool;
        public SpriteWindow BubbleWin;
        public int BubbleKey = -1, BubbleX, BubbleY;
        // name tag
        public string Name;
        public bool Hovered;
        public int HoverLinger;
        public SpriteWindow TagWin;
        public IntPtr TagBmp;
        public int TagW, TagX = int.MinValue, TagY;
        public bool TagShown, BubbleShown;
        public SpriteWindow Win;
        public int LastFrame = -1, LastX = int.MinValue, LastY = int.MinValue;
    }

    class Snowman
    {
        public float X;
        public int Stage = -1, Progress, Life, MeltTimer, Idle;
        public bool Melting;
        public List<Penguin> Crew = new List<Penguin>();
        public SpriteWindow Win;
        public int LastKey = -1, LastX, LastY;
    }

    class Tug
    {
        public List<Penguin> Left = new List<Penguin>(), Right = new List<Penguin>();
        public float C, C0, V, SL = 1, SR = 1;
        public int Phase, Timer, BoostL, BoostR, RopeOff;
        public SpriteWindow Rope;
        public IntPtr RopeBmp;
        public List<Penguin> All() { var a = new List<Penguin>(Left); a.AddRange(Right); return a; }
    }

    class WinInfo { public IntPtr H; public Rectangle R; }

    // Movement history of a window that penguins are on (for riding along / shake detection).
    class Track { public Rectangle R; public int SX, SY, Rev, RevTimer, MoveTimer; }

    // ---------------------------------------------------------------- simulation + tray

    class PenguinApp : ApplicationContext
    {
        const int FALL = 0, WALK = 1, LOOK = 2, FLY = 3, WAVE = 4, SLEEP = 5, SLIDE = 6, DANCE = 7,
                  HELD = 8, THROWN = 9, CURIOUS = 10, GREET = 11, RIDE = 12, CLIMB = 13, OFF = 14,
                  GOTO = 15, BUILD = 16, ADMIRE = 17, READY = 18, TUG = 19, WATCH = 20, YAWN = 21, SWAY = 22,
                  FACEPLANT = 23, DAZED = 24;
        const int W = Art.W, H = Art.H;
        const int ShooTicks = 2000;    // ~60 s

        readonly IntPtr[,] hbm = new IntPtr[2, Art.Frames];
        readonly IntPtr screenDC, memDC;
        readonly List<Penguin> penguins = new List<Penguin>();
        List<WinInfo> wins = new List<WinInfo>();
        List<WinInfo> scratch = new List<WinInfo>();
        readonly Dictionary<IntPtr, Track> tracks = new Dictionary<IntPtr, Track>();
        readonly Random rnd = new Random();
        readonly Native.EnumWindowsProc enumCb;
        readonly Timer timer;
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        readonly HotkeyWindow hotkey;
        bool follow, hotkeyOk;
        Rectangle virt;
        Rectangle[] screenBounds, workAreas;
        int target, ticks, shooUntil;

        // speech bubbles, names, fullscreen
        readonly IntPtr[] bubbleHbm = new IntPtr[BubbleArt.Count];
        static readonly string[] NamePool = {
            "Pebble", "Waddles", "Biscuit", "Nugget", "Pip", "Sprout", "Mochi", "Dumpling", "Puddle", "Flurry",
            "Noodle", "Bean", "Socks", "Tofu", "Marble", "Button", "Crumble", "Snowy", "Pickle", "Fizz",
            "Toast", "Bramble", "Clover", "Ziggy", "Juniper", "Maple", "Sushi", "Pudding", "Tumble", "Wobble",
            "Frosty", "Peanut", "Olive", "Mittens", "Skipper", "Bubbles", "Cocoa", "Hazel", "Sleet", "Gizmo" };
        readonly List<string> unusedNames = new List<string>(NamePool);
        bool hiddenFs;

        // snowman & tug of war
        readonly IntPtr[] snowHbm = new IntPtr[SnowArt.Stages];
        Snowman snow;
        Tug tug;

        // typing / idle / cursor-wave detection
        readonly bool[] keyDown = new bool[256];
        readonly Queue<int> keyTicks = new Queue<int>();
        readonly Queue<int> revTicks = new Queue<int>();
        readonly List<int> curHist = new List<int>();
        int lastCurX, curSign, lastRevX, lastRevY, swayUntil;

        static int LastOf(Queue<int> q) { int last = 0; foreach (var v in q) last = v; return last; }
        float swayCenter, swayAmp = 40;
        uint idleMs;

        public PenguinApp(int count)
        {
            target = count;
            var frames = Art.RenderAll();
            for (int d = 0; d < 2; d++)
                for (int f = 0; f < Art.Frames; f++)
                    hbm[d, f] = Art.ToHBitmap(frames[d, f]);
            for (int s = 0; s < SnowArt.Stages; s++)
                using (var b = SnowArt.Render(s)) snowHbm[s] = Art.ToHBitmap(b);
            for (int i = 0; i < BubbleArt.Count; i++)
                using (var b = BubbleArt.Render(i)) bubbleHbm[i] = Art.ToHBitmap(b);
            screenDC = Native.GetDC(IntPtr.Zero);
            memDC = Native.CreateCompatibleDC(screenDC);

            enumCb = EnumCallback;
            UpdateScreens();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += delegate { UpdateScreens(); };

            hotkey = new HotkeyWindow();
            hotkey.Pressed += delegate { Shoo(); };
            hotkeyOk = Native.RegisterHotKey(hotkey.Handle, 1, 0x1 /*ALT*/ | 0x2 /*CTRL*/ | 0x4000 /*NOREPEAT*/, (uint)Keys.P);

            tray = new NotifyIcon();
            using (var icon = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(icon))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(frames[0, Art.Stand], new Rectangle(-6, -18, 44, 52));
                }
                tray.Icon = Icon.FromHandle(icon.GetHicon());
            }
            tray.Text = "Desktop Penguins";
            menu = new ContextMenuStrip();
            menu.Items.Add("Add a penguin", null, delegate { target = Math.Min(target + 1, 60); });
            menu.Items.Add("Remove a penguin", null, delegate { target = Math.Max(target - 1, 0); });
            var followItem = new ToolStripMenuItem("Follow the mouse");
            followItem.CheckOnClick = true;
            followItem.CheckedChanged += delegate { follow = followItem.Checked; };
            menu.Items.Add(followItem);
            menu.Items.Add("Build a snowman", null, delegate { StartSnowman(); });
            menu.Items.Add("Tug of war!", null, delegate { StartTug(); });
            menu.Items.Add(hotkeyOk ? "Shoo!  (Ctrl+Alt+P)" : "Shoo!", null, delegate { Shoo(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { Quit(); });
            tray.ContextMenuStrip = menu;
            tray.Visible = true;

            timer = new Timer();
            timer.Interval = 30;
            timer.Tick += OnTick;
            timer.Start();
        }

        void Quit()
        {
            timer.Stop();
            Native.UnregisterHotKey(hotkey.Handle, 1);
            hotkey.DestroyHandle();
            tray.Visible = false;
            foreach (var p in penguins) ClosePenguinWindows(p);
            if (snow != null) snow.Win.Close();
            if (tug != null && tug.Rope != null) tug.Rope.Close();
            ExitThread();
        }

        // ---------- screens & windows ----------

        void UpdateScreens()
        {
            virt = SystemInformation.VirtualScreen;
            var all = Screen.AllScreens;
            screenBounds = new Rectangle[all.Length];
            workAreas = new Rectangle[all.Length];
            for (int i = 0; i < all.Length; i++) { screenBounds[i] = all[i].Bounds; workAreas[i] = all[i].WorkingArea; }
        }

        int FloorAt(int x)
        {
            int best = int.MinValue;
            for (int i = 0; i < workAreas.Length; i++)
                if (x >= workAreas[i].Left && x < workAreas[i].Right && workAreas[i].Bottom > best)
                    best = workAreas[i].Bottom;
            return best;
        }

        int ScreenTopAt(int x, int y)
        {
            for (int i = 0; i < screenBounds.Length; i++)
                if (screenBounds[i].Contains(x, y)) return screenBounds[i].Top;
            return int.MaxValue;
        }

        void RefreshWindows()
        {
            scratch.Clear();
            Native.EnumWindows(enumCb, IntPtr.Zero);   // top of z-order first
            var t = wins; wins = scratch; scratch = t;
        }

        static bool Cloaked(IntPtr h)
        {
            int cloaked;
            return Native.DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0;
        }

        static Rectangle FrameRect(IntPtr h)
        {
            Native.RECT r;
            if (Native.DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(Native.RECT))) != 0)
                Native.GetWindowRect(h, out r);
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }

        // Current rectangle of a window, or false if it was closed / minimized / hidden.
        static bool GetRect(IntPtr h, out Rectangle r)
        {
            r = Rectangle.Empty;
            if (!Native.IsWindow(h) || !Native.IsWindowVisible(h) || Native.IsIconic(h) || Cloaked(h)) return false;
            r = FrameRect(h);
            return true;
        }

        bool EnumCallback(IntPtr h, IntPtr l)
        {
            if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
            if (Native.GetWindowTextLength(h) == 0) return true;
            int ex = Native.GetWindowLong(h, -20);
            if ((ex & 0x80) != 0 || (ex & 0x20) != 0) return true;           // tool / click-through overlays
            if (Cloaked(h)) return true;
            var sb = new StringBuilder(64);
            Native.GetClassName(h, sb, 64);
            string cls = sb.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return true;
            var r = FrameRect(h);
            if (r.Width < 60 || r.Height < 30) return true;
            scratch.Add(new WinInfo { H = h, R = r });
            return true;
        }

        int IndexOf(IntPtr h)
        {
            for (int i = 0; i < wins.Count; i++) if (wins[i].H == h) return i;
            return -1;
        }

        bool Covered(int i, int x, int y)
        {
            for (int j = 0; j < i; j++) if (wins[j].R.Contains(x, y)) return true;
            return false;
        }

        // Can a penguin stand on the top edge of window i at column x?
        bool Standable(int i, int x, int top)
        {
            if (top - 34 < ScreenTopAt(x, top)) return false;   // no room above (e.g. maximized)
            return !Covered(i, x, top - 1) && !Covered(i, x, top);
        }

        // Any window whose top edge at (x, y) can be stood on.
        IntPtr SupportAt(int x, int y)
        {
            for (int i = 0; i < wins.Count; i++)
            {
                var r = wins[i].R;
                if (r.Top == y && x >= r.Left && x < r.Right && Standable(i, x, y)) return wins[i].H;
            }
            return IntPtr.Zero;
        }

        // Is the penguin still standing on its window?
        bool StillSupported(Penguin p)
        {
            int x = (int)p.X, y = (int)p.Y;
            int i = IndexOf(p.Surf);
            if (i >= 0)
            {
                var r = wins[i].R;
                return r.Top == y && x >= r.Left && x < r.Right && Standable(i, x, y);
            }
            Track t;   // not in the cached list yet (e.g. just appeared): use the live rectangle
            if (tracks.TryGetValue(p.Surf, out t))
                return t.R.Top == y && x >= t.R.Left && x < t.R.Right;
            return false;
        }

        bool FindLanding(int x, int y0, int y1, out int land, out bool floor, out IntPtr surf)
        {
            land = int.MaxValue; floor = false; surf = IntPtr.Zero;
            int f = FloorAt(x);
            if (f != int.MinValue && f <= y1) { land = f; floor = true; }
            for (int i = 0; i < wins.Count; i++)
            {
                var r = wins[i].R;
                int t = r.Top;
                if (t > y0 && t <= y1 && t < land && x >= r.Left && x < r.Right && Standable(i, x, t))
                { land = t; floor = false; surf = wins[i].H; }
            }
            return land != int.MaxValue;
        }

        void EnsureTrack(IntPtr h)
        {
            Rectangle r;
            if (h != IntPtr.Zero && !tracks.ContainsKey(h) && GetRect(h, out r)) tracks[h] = new Track { R = r };
        }

        static bool StandsOnSurface(Penguin p)
        {
            switch (p.State)
            {
                case WALK: case LOOK: case WAVE: case SLEEP: case DANCE: case CURIOUS: case GREET: case SLIDE: case CLIMB:
                case GOTO: case BUILD: case ADMIRE: case READY: case WATCH: case YAWN: case SWAY:
                case FACEPLANT: case DAZED:
                    return true;
            }
            return false;
        }

        // Can another penguin stand on this one's head?
        static bool CanCarry(Penguin p)
        {
            switch (p.State)
            {
                case WALK: case LOOK: case WAVE: case SLEEP: case DANCE: case CURIOUS: case GREET: case RIDE:
                case GOTO: case BUILD: case ADMIRE: case READY: case WATCH: case YAWN: case SWAY:
                case DAZED:
                    return true;
            }
            return false;
        }

        // Follow windows that penguins stand on: ride along when moved, fly off when shaken, drop when closed.
        void TrackSurfaces()
        {
            var seen = new HashSet<IntPtr>();
            foreach (var p in penguins)
                if (p.Surf != IntPtr.Zero && StandsOnSurface(p)) seen.Add(p.Surf);

            foreach (var h in seen)
            {
                Rectangle r;
                if (!GetRect(h, out r))
                {   // closed / minimized: everybody drops
                    foreach (var p in penguins)
                        if (p.Surf == h && StandsOnSurface(p)) StartFalling(p, false);
                    tracks.Remove(h);
                    continue;
                }
                Track t;
                if (!tracks.TryGetValue(h, out t)) { tracks[h] = new Track { R = r }; continue; }

                int dx = r.X - t.R.X, dy = r.Y - t.R.Y;
                t.R = r;
                int i = IndexOf(h);
                if (i >= 0) wins[i].R = r;
                if (t.RevTimer > 0 && --t.RevTimer == 0) t.Rev = 0;
                if (dx == 0 && dy == 0) { if (t.MoveTimer > 0) t.MoveTimer--; continue; }

                t.MoveTimer = 8;
                int sx = Math.Sign(dx), sy = Math.Sign(dy);
                if (sx != 0) { if (t.SX != 0 && sx != t.SX) { t.Rev++; t.RevTimer = 40; } t.SX = sx; }
                if (sy != 0) { if (t.SY != 0 && sy != t.SY) { t.Rev++; t.RevTimer = 40; } t.SY = sy; }

                bool shaken = t.Rev >= 4;
                foreach (var p in penguins)
                {
                    if (p.Surf != h || !StandsOnSurface(p)) continue;
                    p.X += dx; p.Y += dy;
                    if (shaken) Fling(p, dx, dy);
                }
                if (shaken) t.Rev = 0;
            }

            if (ticks % 300 == 0)   // forget windows nobody stands on any more
            {
                var stale = new List<IntPtr>();
                foreach (var k in tracks.Keys) if (!seen.Contains(k)) stale.Add(k);
                foreach (var k in stale) tracks.Remove(k);
            }
        }

        bool WindowMoving(IntPtr h)
        {
            Track t;
            return h != IntPtr.Zero && tracks.TryGetValue(h, out t) && t.MoveTimer > 0;
        }

        // ---------- lifecycle ----------

        void Spawn()
        {
            var p = new Penguin();
            Reset(p);
            if (unusedNames.Count == 0) unusedNames.AddRange(NamePool);
            int ni = rnd.Next(unusedNames.Count);
            p.Name = unusedNames[ni];
            unusedNames.RemoveAt(ni);
            p.Win = new SpriteWindow();
            p.Win.MouseDown += delegate(object s, MouseEventArgs e) { Grab(p, e); };
            p.Win.MouseUp += delegate { Release(p); };
            p.Win.MouseCaptureChanged += delegate { if (!p.Win.Capture) Release(p); };
            p.Win.MouseEnter += delegate { p.Hovered = true; };
            p.Win.MouseLeave += delegate { p.Hovered = false; p.HoverLinger = 35; };
            p.Win.Show();
            if (hiddenFs) Native.ShowWindow(p.Win.Handle, 0);
            penguins.Add(p);
        }

        void ClosePenguinWindows(Penguin p)
        {
            p.Win.Close();
            if (p.BubbleWin != null) p.BubbleWin.Close();
            if (p.TagWin != null) p.TagWin.Close();
            if (p.TagBmp != IntPtr.Zero) { Native.DeleteObject(p.TagBmp); p.TagBmp = IntPtr.Zero; }
        }

        // ---------- speech bubbles ----------

        void Say(Penguin p, int icon, int duration = 60)
        {
            p.Bubble = icon; p.BubbleTimer = duration;
        }

        // "?" — with a cooldown so a confused penguin doesn't spam it
        void Puzzled(Penguin p)
        {
            if (p.QCool > 0) return;
            p.QCool = 240;
            Say(p, BubbleArt.Question, 50);
        }

        // Bubble above-right of the head; name tag centred above the head.
        void DrawExtras(Penguin p, int headX, int headTop)
        {
            bool off = p.State == OFF;

            bool showTag = !off && (p.Hovered || p.State == HELD || p.HoverLinger > 0);
            if (p.HoverLinger > 0 && !p.Hovered) p.HoverLinger--;
            if (showTag)
            {
                if (p.TagWin == null)
                {
                    using (var b = BubbleArt.RenderTag(p.Name)) { p.TagBmp = Art.ToHBitmap(b); p.TagW = b.Width; }
                    p.TagWin = new SpriteWindow(p.TagW, 18, true);
                    p.TagWin.Show();
                }
                int tx = headX - p.TagW / 2, ty = headTop - 22;
                if (!p.TagShown || tx != p.TagX || ty != p.TagY)
                {
                    p.TagWin.Draw(screenDC, memDC, p.TagBmp, tx, ty);
                    p.TagX = tx; p.TagY = ty; p.TagShown = true;
                }
            }
            else if (p.TagShown)
            {
                p.TagWin.Draw(screenDC, memDC, p.TagBmp, -32000, -32000);
                p.TagShown = false;
            }

            if (p.BubbleTimer > 0 && !off)
            {
                p.BubbleTimer--;
                if (p.BubbleWin == null) { p.BubbleWin = new SpriteWindow(BubbleArt.W, BubbleArt.H, true); p.BubbleWin.Show(); }
                int bx = headX + 6, by = headTop - BubbleArt.H - (showTag ? 20 : 2);
                if (!p.BubbleShown || p.Bubble != p.BubbleKey || bx != p.BubbleX || by != p.BubbleY)
                {
                    p.BubbleWin.Draw(screenDC, memDC, bubbleHbm[p.Bubble], bx, by);
                    p.BubbleKey = p.Bubble; p.BubbleX = bx; p.BubbleY = by; p.BubbleShown = true;
                }
            }
            else if (p.BubbleShown)
            {
                p.BubbleWin.Draw(screenDC, memDC, bubbleHbm[p.BubbleKey], -32000, -32000);
                p.BubbleShown = false; p.BubbleTimer = 0;
            }
        }

        void Reset(Penguin p)
        {
            Detach(p);
            var sb = screenBounds[rnd.Next(screenBounds.Length)];
            p.X = sb.Left + W + rnd.Next(Math.Max(1, sb.Width - 2 * W));
            p.Y = sb.Top - 2;
            p.Dir = rnd.Next(2) == 0 ? 1 : -1;
            p.Speed = 1.2f + (float)rnd.NextDouble() * 1.0f;
            p.Shooed = false;
            StartFalling(p, rnd.Next(100) < 60);   // most arrive by umbrella
        }

        // Leave whatever it was standing on and whatever game it was part of.
        void Detach(Penguin p)
        {
            if (p.Base != null) { if (p.Base.Rider == p) p.Base.Rider = null; p.Base = null; }
            p.Job = null;
        }

        void StartFalling(Penguin p, bool umbrella)
        {
            Detach(p);
            p.State = FALL; p.Vy = 0; p.Vx = 0; p.OnFloor = false; p.Surf = IntPtr.Zero;
            p.Umbrella = umbrella; p.Tick = 0; p.HopOff = 0; p.HopV = 0;
        }

        void Fling(Penguin p, int dx, int dy)
        {
            Detach(p);
            p.State = THROWN; p.Tick = 0; p.OnFloor = false; p.Surf = IntPtr.Zero;
            p.Vx = Math.Max(-25f, Math.Min(25f, dx * 1.5f)) + (float)(rnd.NextDouble() * 6 - 3);
            p.Vy = -5f - (float)rnd.NextDouble() * 4f + Math.Min(0, dy);
            p.Spin = true; p.AfterLand = FACEPLANT;
            Say(p, BubbleArt.Bang, 50);                 // shaken off a window: "!"
        }

        void Shoo()
        {
            if (ticks < shooUntil) { shooUntil = ticks; return; }   // pressed again: come back now
            shooUntil = ticks + ShooTicks;
            foreach (var p in penguins)
            {
                if (p.State == OFF) continue;
                if (p.Win.Capture) p.Win.Capture = false;
                Detach(p);
                var sb = Screen.FromPoint(new Point((int)p.X, (int)p.Y)).Bounds;
                p.Dir = p.X < sb.Left + sb.Width / 2 ? -1 : 1;     // nearest side
                p.State = FLY; p.Tick = 0; p.FlyBaseY = p.Y - 10; p.Shooed = true;
                p.OnFloor = false; p.Surf = IntPtr.Zero; p.HopOff = p.HopV = 0;
            }
        }

        // ---------- mouse interaction ----------

        void Grab(Penguin p, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { menu.Show(Cursor.Position); return; }
            if (e.Button != MouseButtons.Left || p.State == OFF) return;
            if (p.State == TUG && tug != null)
            {   // cheer this team on: a burst of pulling strength
                if (tug.Left.Contains(p)) tug.BoostL = 25; else tug.BoostR = 25;
                return;
            }
            Detach(p);
            var c = Cursor.Position;
            p.GrabDx = c.X - p.X;
            p.GrabDy = c.Y - (p.Y + p.HopOff);
            p.Down = c; p.Moved = false;
            p.State = HELD; p.Tick = 0;
            Say(p, BubbleArt.Bang, 45);                 // "!"
            p.Vx = p.Vy = 0; p.HopOff = p.HopV = 0;
            p.Umbrella = false; p.OnFloor = false; p.Surf = IntPtr.Zero;
        }

        void Release(Penguin p)
        {
            if (p.State != HELD) return;
            var c = Cursor.Position;
            if (Math.Abs(c.X - p.Down.X) + Math.Abs(c.Y - p.Down.Y) > 5) p.Moved = true;
            p.State = THROWN; p.Tick = 0;
            if (!p.Moved)
            {   // a click: jump for joy, then wave
                p.Vx = 0; p.Vy = -6.5f; p.Spin = false; p.AfterLand = WAVE;
            }
            else
            {   // let go / flung: keep the drag velocity
                p.Vx = Math.Max(-35f, Math.Min(35f, p.Vx));
                p.Vy = Math.Max(-35f, Math.Min(35f, p.Vy));
                p.Spin = Math.Abs(p.Vx) + Math.Abs(p.Vy) > 4;
                p.AfterLand = p.Spin ? FACEPLANT : LOOK;    // flung: faceplant; gently dropped: look around
            }
        }

        // ---------- simulation ----------

        // Land on a window, the floor, or another penguin's head between y and ny.
        bool TryLand(Penguin p, float ny, int afterState, int afterTimer)
        {
            int x = (int)p.X, y0 = (int)p.Y, y1 = (int)ny;
            int land; bool floor; IntPtr surf;
            bool hit = FindLanding(x, y0, y1, out land, out floor, out surf);

            Penguin head = null;
            int best = hit ? land : int.MaxValue;
            foreach (var q in penguins)
            {
                if (q == p || q.Rider != null || q.Base == p || !CanCarry(q)) continue;
                int top = (int)(q.Y + q.HopOff) - Art.HeadH;
                if (Math.Abs(q.X - p.X) < 11 && top > y0 && top <= y1 && top < best) { best = top; head = q; }
            }

            if (head != null)
            {   // pile-up: stand on its head
                p.State = RIDE; p.Base = head; head.Rider = p;
                p.X = head.X; p.Y = best; p.Vx = p.Vy = 0; p.Tick = 0;
                p.Timer = 150 + rnd.Next(250); p.Umbrella = false; p.OnFloor = false; p.Surf = IntPtr.Zero;
                return true;
            }
            if (!hit) return false;
            p.Y = land; p.OnFloor = floor; p.Surf = floor ? IntPtr.Zero : surf;
            p.Vx = p.Vy = 0; p.Tick = 0; p.Umbrella = false;
            p.State = afterState; p.Timer = afterTimer;
            EnsureTrack(p.Surf);
            return true;
        }

        void StartBehaviour(Penguin p)
        {
            p.Tick = 0;
            if (idleMs > 90000 && rnd.Next(2) == 0)
            {   // long idle: naps
                p.State = SLEEP; p.Timer = 300 + rnd.Next(300); Say(p, BubbleArt.Zzz, 70); return;
            }
            int r = rnd.Next(100);
            if (r < 20)      { p.State = LOOK;  p.Timer = 70 + rnd.Next(60); }
            else if (r < 35) { p.State = WAVE;  p.Timer = 50 + rnd.Next(40); }
            else if (r < 47) { p.State = SLEEP; p.Timer = 160 + rnd.Next(200); Say(p, BubbleArt.Zzz, 70); }
            else if (r < 65) { p.State = SLIDE; p.SlideV = 5f + (float)rnd.NextDouble() * 2f; }
            else if (r < 77) { p.State = DANCE; p.Timer = 80 + rnd.Next(60); Say(p, BubbleArt.Note); }
            else if (r < 93) { p.HopV = -3.4f; }                                  // hop while walking
            else             { p.State = FLY; p.OnFloor = false; p.Surf = IntPtr.Zero; p.FlyBaseY = p.Y; }
        }

        // Walking into the side of a window: maybe climb it.
        bool TryClimb(Penguin p, float nx)
        {
            if (p.ClimbCool > 0) return false;
            int y = (int)p.Y;
            for (int i = 0; i < wins.Count; i++)
            {
                if (wins[i].H == p.Surf) continue;
                var r = wins[i].R;
                int edge = p.Dir > 0 ? r.Left : r.Right;
                bool reaching = p.Dir > 0 ? (p.X + 8 <= edge && nx + 8 > edge) : (p.X - 8 >= edge && nx - 8 < edge);
                if (!reaching) continue;
                if (r.Top >= y - 20 || r.Bottom < y - 25) continue;           // must rise above and reach down to it
                if (Covered(i, edge + p.Dir * 2, y - 15)) continue;           // that side isn't visible here
                if (!Standable(i, edge + p.Dir * 14, r.Top)) continue;        // nowhere to stand at the top
                p.ClimbCool = 150;
                if (rnd.Next(100) >= 65) { Puzzled(p); return false; }        // bumped into it: "?" and walk past
                p.State = CLIMB; p.Tick = 0; p.OnFloor = false; p.Surf = wins[i].H;
                p.X = edge - p.Dir * 9;
                EnsureTrack(p.Surf);
                return true;
            }
            return false;
        }

        // Move along the current surface. Turns at walls / screen edges, falls off ledges.
        void Move(Penguin p, float speed)
        {
            int y = (int)p.Y;
            float nx = p.X + p.Dir * speed;
            int ix = (int)nx;
            if (ix < virt.Left + 20 || ix >= virt.Right - 20) { p.Dir = -p.Dir; Puzzled(p); return; }   // screen edge
            if (p.State == WALK && TryClimb(p, nx)) return;
            if (p.OnFloor)
            {
                int f = FloorAt(ix);
                if (f == int.MinValue || f < y) { p.Dir = -p.Dir; Puzzled(p); return; }  // gap or wall: "?" and turn
                p.X = nx;
                if (f > y) StartFalling(p, false);
            }
            else
            {
                p.X = nx;
                if (StillSupported(p)) return;
                IntPtr next = SupportAt(ix, y);                              // onto a neighbouring window?
                if (next != IntPtr.Zero) { p.Surf = next; EnsureTrack(next); }
                else StartFalling(p, false);                                 // off the edge: tumble
            }
        }

        void Step(Penguin p)
        {
            int x = (int)p.X, y = (int)p.Y;
            p.Tick++;
            if (p.QCool > 0) p.QCool--;
            if (p.BumpCool > 0) p.BumpCool--;
            if (p.ClimbCool > 0) p.ClimbCool--;

            switch (p.State)
            {
                case OFF:
                    if (ticks >= shooUntil) Reset(p);
                    return;

                case TUG:          // position is driven by the rope (UpdateTug)
                    return;

                case HELD:
                {   // dangle from the cursor; remember how fast it is moving for the throw
                    var c = Cursor.Position;
                    float nx = c.X - p.GrabDx, ny = c.Y - p.GrabDy;
                    p.Vx = p.Vx * 0.4f + (nx - p.X) * 0.6f;
                    p.Vy = p.Vy * 0.4f + (ny - p.Y) * 0.6f;
                    p.X = nx; p.Y = ny;
                    if (Math.Abs(c.X - p.Down.X) + Math.Abs(c.Y - p.Down.Y) > 5) p.Moved = true;
                    return;
                }

                case THROWN:
                {
                    p.Vy = Math.Min(p.Vy + 0.4f, 16f);
                    p.Vx *= 0.985f;
                    float nx = p.X + p.Vx;
                    if (nx < virt.Left + 20) { nx = virt.Left + 20; p.Vx = -p.Vx * 0.6f; }   // bounce off screen sides
                    if (nx > virt.Right - 20) { nx = virt.Right - 20; p.Vx = -p.Vx * 0.6f; }
                    p.X = nx;
                    float ny = p.Y + p.Vy;
                    if (!(p.Vy > 0 && TryLand(p, ny, p.AfterLand, p.AfterLand == FACEPLANT ? 60 : 50))) p.Y = ny;
                    if (p.Y > virt.Bottom + 300) Reset(p);
                    return;
                }

                case FLY:
                    p.X += p.Dir * (p.Shooed ? 9f : 4.5f);
                    if (p.Tick < 60) p.FlyBaseY -= 2.5f;   // climb after take-off
                    p.Y = p.FlyBaseY + (float)Math.Sin(p.Tick * 0.07) * 14f;
                    if (p.X < virt.Left - W || p.X > virt.Right + W)
                    {
                        if (ticks < shooUntil) p.State = OFF;    // stay away until the shoo wears off
                        else Reset(p);
                    }
                    return;

                case FALL:
                {
                    if (p.Umbrella)
                    {
                        p.Vy = Math.Min(p.Vy + 0.04f, 1.1f);                         // drift down slowly
                        p.X += (float)Math.Sin(p.Tick * 0.035) * 0.7f;               // and sway
                        p.X = Math.Max(virt.Left + 20, Math.Min(virt.Right - 20, p.X));
                    }
                    else p.Vy = Math.Min(p.Vy + 0.12f, 3.5f);
                    float ny = p.Y + p.Vy;
                    // umbrella: land gently and look around; otherwise: faceplant
                    if (!TryLand(p, ny, p.Umbrella ? LOOK : FACEPLANT, p.Umbrella ? 40 : 60)) p.Y = ny;
                    if (p.Y > virt.Bottom + 200) Reset(p);
                    return;
                }

                case RIDE:
                {   // standing on another penguin's head
                    var b = p.Base;
                    if (b == null || !penguins.Contains(b) || b.Rider != p || !CanCarry(b)) { StartFalling(p, false); return; }
                    p.X = b.X; p.Y = b.Y + b.HopOff - Art.HeadH;
                    if (--p.Timer <= 0)
                    {   // hop off sideways
                        Detach(p);
                        p.State = THROWN; p.Tick = 0; p.Spin = false; p.AfterLand = LOOK;
                        p.Vx = p.Dir * 2.5f; p.Vy = -4.5f;
                    }
                    return;
                }

                case CLIMB:
                {
                    Track t;
                    if (!tracks.TryGetValue(p.Surf, out t)) { StartFalling(p, false); return; }
                    var r = t.R;
                    int edge = p.Dir > 0 ? r.Left : r.Right;
                    p.X = edge - p.Dir * 9;
                    p.Y -= 2f;
                    if (p.Y - 30 > r.Bottom) { StartFalling(p, false); return; }   // the window moved away
                    if (p.Y <= r.Top)
                    {   // made it: step onto the top
                        p.Y = r.Top; p.X = edge + p.Dir * 14;
                        if (StillSupported(p)) { p.State = LOOK; p.Timer = 30; p.Tick = 0; p.ClimbCool = 150; }
                        else StartFalling(p, false);
                    }
                    return;
                }
            }

            // Standing on something still?
            if (p.OnFloor)
            {
                int f = FloorAt(x);
                if (f == int.MinValue || f > y) { StartFalling(p, false); return; }
                if (f < y) p.Y = y = f;
            }
            else if (!StillSupported(p)) { StartFalling(p, false); return; }

            switch (p.State)
            {
                case LOOK:
                case WAVE:
                case SLEEP:
                case DANCE:
                    if (--p.Timer <= 0) { p.State = WALK; p.Tick = 0; if (rnd.Next(3) == 0) p.Dir = -p.Dir; }
                    return;
                case FACEPLANT:    // beak in the ground, feet wiggling...
                    if (--p.Timer <= 0) { p.State = DAZED; p.Timer = 55; p.Tick = 0; }
                    return;
                case DAZED:        // ...then up again, seeing stars
                    if (--p.Timer <= 0) { p.State = WALK; p.Tick = 0; }
                    return;
                case ADMIRE:       // step back and admire the snowman, then celebrate
                    if (--p.Timer <= 0) { p.State = DANCE; p.Timer = 70; p.Tick = 0; Say(p, BubbleArt.Note); }
                    return;
                case YAWN:
                    if (--p.Timer <= 0)
                    {
                        p.Tick = 0;
                        if (rnd.Next(100) < 40) { p.State = SLEEP; p.Timer = 200 + rnd.Next(250); Say(p, BubbleArt.Zzz, 70); }
                        else p.State = WALK;
                    }
                    return;
                case WATCH:        // typing along with you
                    p.Dir = p.WatchX >= p.X ? 1 : -1;
                    if (p.HopV != 0 || p.HopOff != 0)
                    {
                        p.HopOff += p.HopV; p.HopV += 0.25f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    if (p.ShakeTimer > 0) p.ShakeTimer--;
                    if (--p.Timer <= 0 && p.HopOff == 0) { p.State = WALK; p.Tick = 0; p.Cooldown = 60; }
                    return;
                case SWAY:
                    if (--p.Timer <= 0) { p.State = WALK; p.Tick = 0; p.Cooldown = 60; }
                    return;
                case READY:        // waiting at the rope
                case BUILD:        // patting snow
                    p.Dir = p.FaceDir;
                    return;
                case GOTO:
                {   // walk to a spot for a snowman / tug of war
                    float gx = p.TargetX - p.X;
                    if (Math.Abs(gx) < 2.5f)
                    {
                        p.X = p.TargetX; p.Dir = p.FaceDir; p.State = p.NextState; p.Tick = 0;
                        return;
                    }
                    p.Dir = gx > 0 ? 1 : -1;
                    Move(p, Math.Min(p.Speed + 0.6f, Math.Abs(gx)));
                    if (p.State == GOTO && --p.Timer <= 0) { p.Job = null; p.State = WALK; }   // couldn't get there
                    return;
                }
                case GREET:
                    if (--p.Timer <= 0)
                    {
                        p.Dir = p.NextDir; p.Tick = 0; p.BumpCool = 150;
                        if (p.NextSlide) { p.State = SLIDE; p.SlideV = 5.5f; }
                        else p.State = WALK;
                    }
                    return;
                case SLIDE:
                    Move(p, p.SlideV);
                    p.SlideV -= 0.06f;
                    if (p.State == SLIDE && p.SlideV < 0.6f) { p.State = LOOK; p.Timer = 20; }
                    return;
                case CURIOUS:
                {   // watch the cursor; wave when it comes close
                    var c = Cursor.Position;
                    float dx = c.X - p.X, dy = c.Y - (p.Y - 20);
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    p.Dir = dx >= 0 ? 1 : -1;
                    p.Near = dist < 50;
                    if (dist > 160 || --p.Timer <= 0) { p.State = WALK; p.Tick = 0; p.Cooldown = 200; }
                    return;
                }
            }

            // Walking (maybe mid-hop)
            if (p.Cooldown > 0) p.Cooldown--;
            if (p.HopV != 0 || p.HopOff != 0)
            {
                p.HopOff += p.HopV; p.HopV += 0.22f;
                if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
            }
            else
            {
                var c = Cursor.Position;
                float dx = c.X - p.X, dy = c.Y - (p.Y - 20);
                if (dx * dx + dy * dy < 80 * 80) p.NearTicks++; else p.NearTicks = 0;
                if (p.Cooldown == 0 && p.NearTicks >= 16)
                {   // the cursor lingered nearby (~0.5 s): stop and look at it
                    p.NearTicks = 0;
                    p.State = CURIOUS; p.Timer = 300; p.Tick = 0; p.Near = false;
                    return;
                }
                if (follow)
                {
                    if (Math.Abs(dx) > 25) p.Dir = dx > 0 ? 1 : -1;
                    else { p.State = LOOK; p.Timer = 20; return; }      // arrived under the cursor
                }
                else if (rnd.Next(700) == 0) p.Dir = -p.Dir;
                if (idleMs > 30000 && rnd.Next(500) == 0)
                {   // you've been away a while: yawn
                    p.State = YAWN; p.Timer = 55; p.Tick = 0;
                    Say(p, BubbleArt.Zzz, 55);
                    return;
                }
                if (rnd.Next(follow ? 900 : 240) == 0) { StartBehaviour(p); return; }
            }
            Move(p, p.Speed);
        }

        // Two walking penguins that meet face each other and wave, then part ways or slide off together.
        void CheckBumps()
        {
            for (int i = 0; i < penguins.Count; i++)
            {
                var a = penguins[i];
                if (a.State != WALK || a.BumpCool > 0 || a.HopOff != 0) continue;
                for (int j = i + 1; j < penguins.Count; j++)
                {
                    var b = penguins[j];
                    if (b.State != WALK || b.BumpCool > 0 || b.HopOff != 0) continue;
                    if (a.OnFloor != b.OnFloor || a.Surf != b.Surf || Math.Abs(a.Y - b.Y) > 1) continue;
                    float gap = b.X - a.X;
                    if (Math.Abs(gap) > 26 || gap * a.Dir <= 0 || -gap * b.Dir <= 0) continue;   // not approaching

                    bool together = rnd.Next(100) < 30;
                    int slideDir = rnd.Next(2) == 0 ? 1 : -1;
                    foreach (var p in new[] { a, b })
                    {
                        var other = p == a ? b : a;
                        p.State = GREET; p.Tick = 0; p.Timer = 45 + rnd.Next(15);
                        Say(p, BubbleArt.Heart, 55);
                        p.Dir = other.X >= p.X ? 1 : -1;                          // face each other
                        p.NextSlide = together;
                        p.NextDir = together ? slideDir : -p.Dir;                // ...then turn away
                    }
                    break;
                }
            }
        }

        // Which picture to show, and which way it faces.
        int FrameFor(Penguin p, out int drawDir)
        {
            drawDir = p.Dir;
            if (StandsOnSurface(p) && p.State != CLIMB && p.State != SLIDE && p.State != FACEPLANT && WindowMoving(p.Surf))
                return Art.ArmsUp;                                   // holding on while the window moves
            switch (p.State)
            {
                case FALL:
                    if (p.Umbrella) return Art.Umbrella;
                    return (p.Tick / 5) % 2 == 0 ? Art.DiveA : Art.DiveB;     // nose-dive
                case FACEPLANT: return (p.Tick / 7) % 2 == 0 ? Art.PlantA : Art.PlantB;
                case DAZED: return (p.Tick / 9) % 2 == 0 ? Art.DazedA : Art.DazedB;
                case FLY: return (p.Tick / 8) % 2 == 0 ? Art.FlyA : Art.FlyB;
                case LOOK:
                    if ((p.Tick / 22) % 2 == 1) drawDir = -p.Dir;
                    return Art.Stand;
                case WAVE:
                case GREET: return (p.Tick / 7) % 2 == 0 ? Art.WaveA : Art.WaveB;
                case SLEEP: return (p.Tick / 35) % 2 == 0 ? Art.SleepA : Art.SleepB;
                case SLIDE: return Art.Slide;
                case DANCE:
                    if ((p.Tick / 16) % 2 == 1) drawDir = -p.Dir;
                    return (p.Tick / 8) % 2 == 0 ? Art.ArmsUp : Art.WaveA;
                case HELD: return (p.Tick / 5) % 2 == 0 ? Art.ArmsUp : Art.FlyB;    // flailing
                case THROWN: return p.Spin ? Tumble(p.Tick / 4) : Art.ArmsUp;
                case CURIOUS:
                    if (p.Near) return (p.Tick / 7) % 2 == 0 ? Art.WaveA : Art.WaveB;
                    return Art.Stand;
                case RIDE:
                    if ((p.Tick / 50) % 3 == 2) return (p.Tick / 7) % 2 == 0 ? Art.WaveA : Art.WaveB;
                    return Art.Stand;
                case CLIMB: return (p.Tick / 8) % 2 == 0 ? Art.ClimbA : Art.ClimbB;
                case OFF: return Art.Stand;
                case GOTO: return (p.Tick / 7) % 2 == 0 ? Art.WalkA : Art.WalkB;
                case READY:
                case ADMIRE: return Art.Stand;
                case WATCH:
                    if (p.ShakeTimer > 0)
                    {   // "no no no": body still, face turning left and right
                        drawDir = 1;
                        return (p.ShakeTimer / 4) % 2 == 0 ? Art.ShakeL : Art.ShakeR;
                    }
                    if (p.HopOff < 0) return Art.ArmsUp;
                    return p.TypeFrame == 0 ? Art.TypeA : Art.TypeB;
                case BUILD: return (p.Tick / 10) % 2 == 0 ? Art.BuildA : Art.BuildB;
                case TUG:
                {
                    bool straining = tug != null && (tug.Left.Contains(p) ? tug.BoostL > 0 : tug.BoostR > 0);
                    return (p.Tick / (straining ? 4 : 9)) % 2 == 0 ? Art.PullA : Art.PullB;
                }
                case YAWN: return p.Tick < 22 ? Art.YawnA : Art.YawnB;
                case SWAY:
                {   // lean the same way as the waving cursor
                    drawDir = 1;
                    float lean = (Cursor.Position.X - swayCenter) / Math.Max(20f, swayAmp);
                    if (lean < -0.35f) return Art.SwayL;
                    if (lean > 0.35f) return Art.SwayR;
                    return Art.ArmsUp;
                }
            }
            if (p.HopOff < 0) return Art.ArmsUp;
            return (p.Tick / 8) % 2 == 0 ? Art.WalkA : Art.WalkB;
        }

        // ---------- group activities ----------

        bool Available(Penguin p)
        {
            return p.OnFloor && (p.State == WALK || p.State == LOOK) && p.HopOff == 0
                && p.Job == null && p.Base == null && p.Rider == null;
        }

        // Up to `max` free penguins on the same stretch of floor, nearest to a random leader first.
        List<Penguin> PickGroup(int max)
        {
            var free = penguins.FindAll(Available);
            if (free.Count < 2) return null;
            var lead = free[rnd.Next(free.Count)];
            var group = free.FindAll(q => (int)q.Y == (int)lead.Y && Math.Abs(q.X - lead.X) < 900);
            group.Sort((a, b) => Math.Abs(a.X - lead.X).CompareTo(Math.Abs(b.X - lead.X)));
            if (group.Count > max) group.RemoveRange(max, group.Count - max);
            return group.Count >= 2 ? group : null;
        }

        Rectangle WorkAreaAt(float x)
        {
            foreach (var wa in workAreas) if (x >= wa.Left && x < wa.Right) return wa;
            return workAreas[0];
        }

        void SendTo(Penguin p, object job, float x, int face, int then)
        {
            p.Job = job; p.State = GOTO; p.Tick = 0; p.Timer = 900;
            p.TargetX = x; p.FaceDir = face; p.NextState = then;
        }

        void StartSnowman()
        {
            if (snow != null || tug != null || ticks < shooUntil) return;
            var crew = PickGroup(3);
            if (crew == null) return;
            var wa = WorkAreaAt(crew[0].X);
            float site = Math.Max(wa.Left + 80, Math.Min(wa.Right - 80, crew[0].X));
            var s = new Snowman { X = site, Crew = crew };
            s.Win = new SpriteWindow(SnowArt.W, SnowArt.H);
            s.Win.MouseDown += delegate(object o, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right) menu.Show(Cursor.Position);
                else if (!s.Melting) StartMelt(s);        // click the snowman to melt it
            };
            s.Win.Show();
            snow = s;
            float[] spots = { -24, 24, -46 };
            for (int i = 0; i < crew.Count; i++)
                SendTo(crew[i], s, site + spots[i], spots[i] < 0 ? 1 : -1, BUILD);
        }

        void StartMelt(Snowman s)
        {
            s.Melting = true; s.MeltTimer = 0;
            foreach (var p in s.Crew)
                if (p.Job == s) { p.Job = null; p.State = LOOK; p.Timer = 50; p.Tick = 0; }   // aww
            if (s.Stage < 0) RemoveSnowman();
        }

        void RemoveSnowman()
        {
            if (snow == null) return;
            snow.Win.Close();
            foreach (var p in snow.Crew) if (p.Job == snow) { p.Job = null; p.State = WALK; }
            snow = null;
        }

        void UpdateSnowman()
        {
            var s = snow;
            if (s == null) return;
            if (!s.Melting)
            {
                if (s.Stage < 4)
                {
                    int working = 0, onJob = 0;
                    foreach (var p in s.Crew)
                    {
                        if (p.Job != s) continue;
                        onJob++;
                        if (p.State == BUILD) working++;
                    }
                    if (working > 0)
                    {
                        s.Idle = 0;
                        if (s.Stage < 0) s.Stage = 0;
                        s.Progress += working;
                        if (s.Progress >= 110) { s.Progress = 0; s.Stage++; }
                        if (s.Stage == 4)
                        {   // finished!  step back and admire it
                            foreach (var p in s.Crew)
                                if (p.Job == s) { p.Job = null; p.State = ADMIRE; p.Timer = 70; p.Tick = 0; Say(p, BubbleArt.Heart, 65); }
                            s.Life = 6000;   // about 3 minutes
                        }
                    }
                    else if (onJob == 0 && ++s.Idle > 600) StartMelt(s);   // builders wandered off
                }
                else if (--s.Life <= 0) StartMelt(s);
            }
            else if (++s.MeltTimer > 260) { RemoveSnowman(); return; }

            if (s.Stage < 0) return;
            int stage = s.Melting ? 5 + Math.Min(2, s.MeltTimer / 70) : s.Stage;
            int floor = FloorAt((int)s.X);
            if (floor == int.MinValue) return;
            int x = (int)s.X - SnowArt.W / 2, y = floor - SnowArt.H;
            if (stage != s.LastKey || x != s.LastX || y != s.LastY)
            {
                s.Win.Draw(screenDC, memDC, snowHbm[stage], x, y);
                s.LastKey = stage; s.LastX = x; s.LastY = y;
            }
        }

        void StartTug()
        {
            if (snow != null || tug != null || ticks < shooUntil) return;
            var group = PickGroup(4);
            if (group == null) return;
            var wa = WorkAreaAt(group[0].X);
            float c = Math.Max(wa.Left + 140, Math.Min(wa.Right - 140, group[0].X));
            var t = new Tug { C = c, C0 = c, Timer = 900 };
            for (int i = 0; i < group.Count; i++)
            {
                var team = i % 2 == 0 ? t.Left : t.Right;
                int side = team == t.Left ? -1 : 1;
                var p = group[i];
                p.TugOff = side * (28 + team.Count * 24);
                team.Add(p);
                SendTo(p, t, c + p.TugOff, -side, READY);
            }
            tug = t;
        }

        void EndTug(List<Penguin> winners)
        {
            var t = tug;
            if (t == null) return;
            if (t.Rope != null) t.Rope.Close();
            if (t.RopeBmp != IntPtr.Zero) Native.DeleteObject(t.RopeBmp);
            foreach (var p in t.All())
            {
                if (p.Job != t) continue;
                p.Job = null; p.Tick = 0;
                if (winners == null) { p.State = LOOK; p.Timer = 40; }
                else if (winners.Contains(p)) { p.State = DANCE; p.Timer = 90; Say(p, BubbleArt.Note); }
                else
                {   // pulled over: belly-flop toward the winners
                    p.Dir = winners == t.Right ? 1 : -1;
                    p.State = SLIDE; p.SlideV = 3.5f;
                    Say(p, BubbleArt.Bang, 50);
                }
            }
            tug = null;
        }

        void UpdateTug()
        {
            var t = tug;
            if (t == null) return;
            var all = t.All();
            foreach (var p in all) if (p.Job != t) { EndTug(null); return; }   // someone was taken away

            if (t.Phase == 0)
            {   // gathering at the rope
                if (all.TrueForAll(q => q.State == READY))
                {
                    t.Phase = 1; t.Timer = 1100;
                    float minOff = 0, maxOff = 0;
                    foreach (var p in all) { p.State = TUG; p.Tick = 0; minOff = Math.Min(minOff, p.TugOff); maxOff = Math.Max(maxOff, p.TugOff); }
                    t.RopeOff = (int)minOff + 8;
                    int w = (int)(maxOff - minOff) - 16;
                    using (var b = SnowArt.RenderRope(w)) t.RopeBmp = Art.ToHBitmap(b);
                    t.Rope = new SpriteWindow(w, 12);
                    t.Rope.Cursor = Cursors.Default;
                    t.Rope.Show();
                }
                else if (--t.Timer <= 0) EndTug(null);
                return;
            }

            // pulling: each side's strength wobbles, clicks give a boost
            if (ticks % 12 == 0)
            {
                t.SL = 0; foreach (var p in t.Left) t.SL += 0.6f + (float)rnd.NextDouble() * 0.8f;
                t.SR = 0; foreach (var p in t.Right) t.SR += 0.6f + (float)rnd.NextDouble() * 0.8f;
            }
            float fl = t.SL + (t.BoostL > 0 ? 1.3f : 0), fr = t.SR + (t.BoostR > 0 ? 1.3f : 0);
            if (t.BoostL > 0) t.BoostL--;
            if (t.BoostR > 0) t.BoostR--;
            t.V = t.V * 0.9f + (fr - fl) * 0.06f;
            t.C += t.V;

            foreach (var p in all)
            {
                p.X = t.C + p.TugOff;
                int f = FloorAt((int)p.X);
                if (f != int.MinValue) p.Y = f;
            }
            t.Rope.Draw(screenDC, memDC, t.RopeBmp, (int)(t.C + t.RopeOff), (int)all[0].Y - 21);

            if (t.C - t.C0 > 45) EndTug(t.Right);
            else if (t.C - t.C0 < -45) EndTug(t.Left);
            else if (--t.Timer <= 0) EndTug(null);         // a draw
        }

        // ---------- typing, idling, cursor waving ----------

        Point CaretPoint()
        {
            var gti = new Native.GUITHREADINFO();
            gti.cbSize = Marshal.SizeOf(typeof(Native.GUITHREADINFO));
            if (Native.GetGUIThreadInfo(0, ref gti) && gti.hwndCaret != IntPtr.Zero)
            {
                var pt = new Native.POINT { X = gti.rcCaret.Left, Y = gti.rcCaret.Bottom };
                if (Native.ClientToScreen(gti.hwndCaret, ref pt)) return new Point(pt.X, pt.Y);
            }
            var fg = Native.GetForegroundWindow();     // no caret info: use the middle of the active window
            if (fg != IntPtr.Zero) { var r = FrameRect(fg); return new Point(r.Left + r.Width / 2, r.Top + r.Height / 2); }
            return Cursor.Position;
        }

        void SenseUser()
        {
            // how long since any keyboard/mouse input
            var lii = new Native.LASTINPUTINFO();
            lii.cbSize = Marshal.SizeOf(typeof(Native.LASTINPUTINFO));
            if (Native.GetLastInputInfo(ref lii)) idleMs = unchecked((uint)Environment.TickCount - lii.dwTime);

            // Count key presses. Nothing is recorded: only how many, plus whether
            // Enter or Backspace was among them (for the reactions below).
            int presses = 0;
            bool enter = false, back = false;
            for (int vk = 8; vk < 255; vk++)
            {
                if ((vk >= 0x10 && vk <= 0x12) || (vk >= 0xA0 && vk <= 0xA5) || vk == 0x5B || vk == 0x5C) continue; // modifiers
                bool down = (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
                if (down && !keyDown[vk])
                {
                    keyTicks.Enqueue(ticks); presses++;
                    if (vk == 0x0D) enter = true;
                    if (vk == 0x08) back = true;
                }
                keyDown[vk] = down;
            }
            while (keyTicks.Count > 0 && ticks - keyTicks.Peek() > 60) keyTicks.Dequeue();
            if (keyTicks.Count >= 8)
            {   // typing fast: nearby penguins come to attention
                var caret = CaretPoint();
                foreach (var p in penguins)
                {
                    bool idle = (p.State == WALK && p.HopOff == 0) || p.State == LOOK || p.State == CURIOUS || p.State == WATCH;
                    if (!idle || p.Job != null) continue;
                    if (Math.Abs(p.X - caret.X) < 450 && p.Y > caret.Y - 40 && p.Y - caret.Y < 700)
                    {
                        if (p.State != WATCH) { p.Tick = 0; p.HopOff = 0; p.HopV = 0; }
                        p.State = WATCH; p.Timer = 60; p.WatchX = caret.X;
                    }
                }
            }
            // ...and type along: each key you press is a tap of their flippers
            bool frenzy = keyTicks.Count >= 16;
            foreach (var p in penguins)
            {
                if (p.State != WATCH) continue;
                if (presses > 0) p.TypeFrame ^= 1;
                if (enter && p.HopOff == 0) p.HopV = -3.8f;                       // Enter: a happy hop
                if (back && p.ShakeTimer == 0) p.ShakeTimer = 24;                 // Backspace: shake head (3 turns)
                if (frenzy && p.HopOff == 0 && rnd.Next(45) == 0) p.HopV = -2.6f; // very fast typing: excited bounces
            }

            // A real wiggle: at least 5 quick, wide, mostly sideways swings in a row.
            // Each swing must cover 60+ px, take at most ~0.5 s, and not drift much vertically;
            // anything else (ordinary pointing, slow moves) resets the count.
            var cur = Cursor.Position;
            int dx = cur.X - lastCurX; lastCurX = cur.X;
            curHist.Add(cur.X);
            if (curHist.Count > 60) curHist.RemoveAt(0);
            if (Math.Abs(dx) >= 3)
            {
                int s = Math.Sign(dx);
                if (curSign != 0 && s != curSign)
                {
                    int swing = Math.Abs(cur.X - lastRevX);
                    int lastRev = revTicks.Count > 0 ? LastOf(revTicks) : ticks;
                    bool good = swing >= 60 && Math.Abs(cur.Y - lastRevY) < swing / 2 && ticks - lastRev <= 16;
                    if (!good) revTicks.Clear();
                    revTicks.Enqueue(ticks);
                    lastRevX = cur.X; lastRevY = cur.Y;
                }
                curSign = s;
            }
            if (revTicks.Count > 0 && ticks - LastOf(revTicks) > 16) revTicks.Clear();   // paused: not wiggling
            if (revTicks.Count >= 6)   // 6 turning points = 5 swings
            {
                int mn = int.MaxValue, mx = int.MinValue;
                foreach (var v in curHist) { mn = Math.Min(mn, v); mx = Math.Max(mx, v); }
                swayCenter = (mn + mx) / 2f; swayAmp = Math.Max(30f, (mx - mn) / 2f);
                swayUntil = ticks + 25;
            }
            if (ticks < swayUntil)
            {
                foreach (var p in penguins)
                {
                    bool free = (p.State == WALK && p.HopOff == 0) || p.State == LOOK || p.State == CURIOUS
                             || p.State == WATCH || p.State == WAVE || p.State == SWAY;
                    if (!free || p.Job != null) continue;
                    if (Math.Abs(p.X - cur.X) < 300 && p.Y > cur.Y)
                    {
                        if (p.State != SWAY) Say(p, BubbleArt.Note, 50);
                        p.State = SWAY; p.Timer = 25;
                    }
                }
            }
        }

        static int Tumble(int n)
        {
            switch (n % 4)
            {
                case 0: return Art.ArmsUp;
                case 1: return Art.Spin90;
                case 2: return Art.Spin180;
                default: return Art.Spin270;
            }
        }

        // ---------- fullscreen ----------

        // Is a game, video or slideshow running fullscreen?
        bool FullscreenActive()
        {
            int st;   // Windows' own signal: 2 busy (fullscreen app), 3 D3D fullscreen, 4 presentation mode
            if (Native.SHQueryUserNotificationState(out st) == 0 && st >= 2 && st <= 4) return true;

            var fg = Native.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            var sb = new StringBuilder(64);
            Native.GetClassName(fg, sb, 64);
            string cls = sb.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return false;
            int style = Native.GetWindowLong(fg, -16);
            if ((style & 0x00C00000) == 0x00C00000) return false;      // has a title bar: an ordinary (maybe maximized) window
            Native.RECT r;
            if (!Native.GetWindowRect(fg, out r)) return false;
            var b = Screen.FromHandle(fg).Bounds;                      // borderless and covering the whole monitor
            return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
        }

        void SetHidden(bool hide)
        {
            hiddenFs = hide;
            int cmd = hide ? 0 /*SW_HIDE*/ : 4 /*SW_SHOWNOACTIVATE*/;
            foreach (var p in penguins)
            {
                Native.ShowWindow(p.Win.Handle, cmd);
                if (p.BubbleWin != null)
                {
                    Native.ShowWindow(p.BubbleWin.Handle, cmd);
                    if (!hide) p.BubbleWin.Draw(screenDC, memDC, bubbleHbm[Math.Max(0, p.BubbleKey)], -32000, -32000);
                }
                if (p.TagWin != null)
                {
                    Native.ShowWindow(p.TagWin.Handle, cmd);
                    if (!hide) p.TagWin.Draw(screenDC, memDC, p.TagBmp, -32000, -32000);
                }
                p.LastFrame = -1; p.BubbleShown = false; p.TagShown = false; p.Hovered = false;   // redraw when back
                p.BubbleTimer = 0; p.HoverLinger = 0;
                if (hide && p.State == HELD) { p.Win.Capture = false; Release(p); }
            }
            if (snow != null) { Native.ShowWindow(snow.Win.Handle, cmd); snow.LastKey = -1; }
            if (tug != null && tug.Rope != null) Native.ShowWindow(tug.Rope.Handle, cmd);
        }

        void OnTick(object sender, EventArgs e)
        {
            ticks++;
            if (ticks % 10 == 0)
            {
                bool fs = FullscreenActive();
                if (fs != hiddenFs) SetHidden(fs);
            }
            if (hiddenFs) return;      // paused while something is fullscreen
            if (ticks % 6 == 1) RefreshWindows();
            if (penguins.Count < target && ticks % 25 == 1 && ticks >= shooUntil) Spawn();
            while (penguins.Count > target)
            {
                var last = penguins[penguins.Count - 1];
                penguins.RemoveAt(penguins.Count - 1);
                Detach(last);
                if (last.Rider != null) { last.Rider.Base = null; last.Rider = null; }
                ClosePenguinWindows(last);
                unusedNames.Add(last.Name);
            }

            TrackSurfaces();
            SenseUser();
            foreach (var p in penguins) Step(p);
            UpdateTug();
            UpdateSnowman();
            CheckBumps();

            // every couple of minutes, on average, a group activity starts by itself
            if (snow == null && tug == null && ticks % 200 == 0 && rnd.Next(20) == 0)
            {
                if (rnd.Next(2) == 0) StartSnowman(); else StartTug();
            }

            bool raise = ticks % 60 == 0;
            foreach (var p in penguins)
            {
                int dir;
                int f = FrameFor(p, out dir);
                int d = dir > 0 ? 0 : 1;
                int x = (int)p.X - W / 2, y = (int)(p.Y + p.HopOff) - H;
                if (p.State == OFF) { x = -32000; y = -32000; }
                int key = f * 2 + d;
                if (key != p.LastFrame || x != p.LastX || y != p.LastY)
                {
                    p.Win.Draw(screenDC, memDC, hbm[d, f], x, y);
                    p.LastFrame = key; p.LastX = x; p.LastY = y;
                }
                if (raise) Native.SetWindowPos(p.Win.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013); // stay topmost
                DrawExtras(p, (int)p.X, (int)(p.Y + p.HopOff) - Art.HeadH);
                if (raise)
                {
                    if (p.BubbleWin != null) Native.SetWindowPos(p.BubbleWin.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
                    if (p.TagWin != null) Native.SetWindowPos(p.TagWin.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
                }
            }
        }
    }

    static class Program
    {
        static void SavePreview(string path)
        {
            const int Z = 3, pad = 6;
            var frames = Art.RenderAll();
            int cw = Art.W * Z + pad, ch = Art.H * Z + pad;
            using (var sheet = new Bitmap(cw * Art.Frames, ch * 3 + Art.H + 20))
            using (var g = Graphics.FromImage(sheet))
            {
                g.Clear(Color.FromArgb(70, 110, 160));
                g.FillRectangle(Brushes.Black, 0, ch * 2, sheet.Width, Art.H + 20);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                for (int f = 0; f < Art.Frames; f++)
                {
                    g.DrawImage(frames[0, f], f * cw + pad / 2, pad / 2, Art.W * Z, Art.H * Z);
                    g.DrawImage(frames[1, f], f * cw + pad / 2, ch + pad / 2, Art.W * Z, Art.H * Z);
                    g.DrawImageUnscaled(frames[0, f], f * cw + 30, ch * 2 + 10);   // actual size on black
                }
                int sy = ch * 2 + Art.H + 20;
                for (int s = 0; s < SnowArt.Stages; s++)
                    using (var b = SnowArt.Render(s))
                        g.DrawImage(b, s * cw + pad / 2, sy, SnowArt.W * Z, SnowArt.H * Z);
                using (var rope = SnowArt.RenderRope(60))
                    g.DrawImage(rope, SnowArt.Stages * cw + 20, sy + 60, 60 * Z, 12 * Z);
                int bxs = SnowArt.Stages * cw + 60 * Z + 60;
                for (int i = 0; i < BubbleArt.Count; i++)
                    using (var b = BubbleArt.Render(i))
                        g.DrawImage(b, bxs + i * (BubbleArt.W * Z + 12), sy + 20, BubbleArt.W * Z, BubbleArt.H * Z);
                using (var tag = BubbleArt.RenderTag("Waddles"))
                    g.DrawImage(tag, bxs + BubbleArt.Count * (BubbleArt.W * Z + 12) + 10, sy + 30, tag.Width * Z, tag.Height * Z);
                sheet.Save(path, ImageFormat.Png);
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--preview") { SavePreview(args[1]); return; }

            bool created;
            var mutex = new System.Threading.Mutex(true, "DesktopPenguins_SingleInstance", out created);
            if (!created) return;

            Native.SetProcessDPIAware();
            int n = 5;
            if (args.Length > 0) int.TryParse(args[0], out n);

            Application.EnableVisualStyles();
            Application.Run(new PenguinApp(Math.Max(0, Math.Min(n, 60))));
            GC.KeepAlive(mutex);
        }
    }
}
