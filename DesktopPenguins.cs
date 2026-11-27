// Desktop Penguins - little cartoon penguins that drift onto your desktop
// (by umbrella, or tumbling), waddle along the taskbar and the tops of your
// windows, climb window sides, ride on each other's heads, greet each other,
// and get up to things: belly-slides, waving, naps, dances, hops, flights.
//
// Interact:  hover = they look at you, click = jump, drag/fling = throw them,
//            right-click = menu, shake a window = they fly off,
//            Ctrl+Alt+P = shoo them away for a minute (again = bring them back).
//
// Build (no extra installs needed on Windows 10/11), all on one line:
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:DesktopPenguins.exe /r:C:\Windows\System32\WinMetadata\Windows.Foundation.winmd /r:C:\Windows\System32\WinMetadata\Windows.Media.winmd /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.dll /r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.InteropServices.WindowsRuntime.dll DesktopPenguins.cs
// Run:  DesktopPenguins.exe [numberOfPenguins]     (default and minimum 6: the core six)
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
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
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

    // How loud the speakers are right now (0..1), from Windows' own audio meter.
    // Only the level is read: nothing is recorded or listened to.
    static class AudioMeter
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCom { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        }

        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioMeterInformation
        {
            [PreserveSig] int GetPeakValue(out float peak);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out int count);
            [PreserveSig] int SetMasterVolumeLevel(float level, IntPtr ctx);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, IntPtr ctx);
            [PreserveSig] int GetMasterVolumeLevel(out float level);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(int ch, float level, IntPtr ctx);
            [PreserveSig] int SetChannelVolumeLevelScalar(int ch, float level, IntPtr ctx);
            [PreserveSig] int GetChannelVolumeLevel(int ch, out float level);
            [PreserveSig] int GetChannelVolumeLevelScalar(int ch, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, IntPtr ctx);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        static IAudioMeterInformation meter;
        static IAudioEndpointVolume volume;
        static int reopenAt;

        // Loudness of whatever is playing (0 when muted, silent or there's no sound device).
        public static float Level(int tick)
        {
            if (tick >= reopenAt)
            {   // every ~10 s pick up the current default device (headphones plugged in, etc.)
                reopenAt = tick + 330;
                Close();
                Open();
            }
            if (meter == null) return 0;
            try
            {
                float peak, vol;
                bool mute;
                if (meter.GetPeakValue(out peak) != 0) { Close(); return 0; }
                if (volume != null)
                {
                    if (volume.GetMute(out mute) == 0 && mute) return 0;
                    if (volume.GetMasterVolumeLevelScalar(out vol) == 0 && vol < 0.03f) return 0;
                }
                return peak;
            }
            catch { Close(); return 0; }
        }

        static void Open()
        {
            try
            {
                var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                IMMDevice dev;
                if (en.GetDefaultAudioEndpoint(0 /*render*/, 1 /*multimedia*/, out dev) != 0 || dev == null) return;
                object o;
                var iid = typeof(IAudioMeterInformation).GUID;
                if (dev.Activate(ref iid, 23, IntPtr.Zero, out o) != 0) return;
                meter = (IAudioMeterInformation)o;
                iid = typeof(IAudioEndpointVolume).GUID;
                if (dev.Activate(ref iid, 23, IntPtr.Zero, out o) == 0) volume = (IAudioEndpointVolume)o;
            }
            catch { Close(); }
        }

        static void Close()
        {
            try { if (meter != null) Marshal.ReleaseComObject(meter); } catch { }
            try { if (volume != null) Marshal.ReleaseComObject(volume); } catch { }
            meter = null; volume = null;
        }
    }

    // Is a video / song playing? Windows' media controls know, even when you're muted.
    // Only each app's id and its play / pause state are read.
    static class MediaSessions
    {
        static object opObj, mgrObj;        // typed as object so this class still loads on Windows without the API
        static bool failed;

        // Is something from this app (by its process name) playing right now?
        public static bool Playing(string proc)
        {
            if (failed || string.IsNullOrEmpty(proc)) return false;
            try { return Query(proc); }
            catch { failed = true; return false; }   // older Windows without these controls: just use the sound
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static bool Query(string proc)
        {
            var mgr = mgrObj as Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
            if (mgr == null)
            {
                if (opObj == null) opObj = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                var op = (Windows.Foundation.IAsyncOperation<Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager>)opObj;
                if (op.Status == Windows.Foundation.AsyncStatus.Started) return false;     // still starting up
                if (op.Status != Windows.Foundation.AsyncStatus.Completed) { failed = true; return false; }
                mgr = op.GetResults();
                mgrObj = mgr;
            }
            foreach (var ses in mgr.GetSessions())
            {
                if (ses.GetPlaybackInfo().PlaybackStatus != Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) continue;
                string id = (ses.SourceAppUserModelId ?? "").ToLowerInvariant();
                if (id.Contains(proc) || (proc == "firefox" && id.Contains("308046b0af4a39cb"))) return true;   // Firefox's app id
            }
            return false;
        }
    }

    // ---------------------------------------------------------------- artwork

    static class Art
    {
        public const float Scale = 1.2f;              // overall penguin size (1 = the original small ones)
        public const int W = 53, H = 63;             // sprite window size in pixels (room for umbrella / Zs)
        public const int HeadH = 34;                  // feet-to-top-of-head height in pixels (for stacking)
        const float K = 0.24f * Scale;                // pixels per design unit
        const float DW = W / K, DH = H / K;           // canvas in design units
        const float PX = (DW - 150f) / 2f, PY = DH - 125f; // where the 150x125 penguin box sits (bottom centre)
        const int SS = 4;                             // supersampling factor

        public const int WalkA = 0, WalkB = 1, Stand = 2, ArmsUp = 3, FlyA = 4, FlyB = 5, WaveA = 6, WaveB = 7,
                         SleepA = 8, SleepB = 9, Slide = 10, Umbrella = 11, Spin90 = 12, Spin180 = 13, Spin270 = 14,
                         ClimbA = 15, ClimbB = 16, BuildA = 17, BuildB = 18, PullA = 19, PullB = 20,
                         YawnA = 21, YawnB = 22, SwayL = 23, SwayR = 24, TypeA = 25, TypeB = 26,
                         ShakeL = 27, ShakeR = 28, DiveA = 29, DiveB = 30, PlantA = 31, PlantB = 32,
                         DazedA = 33, DazedB = 34, PetA = 35, PetB = 36, Blink = 37, Tease = 38, EatA = 39, EatB = 40, SlideB = 41, Leap = 42, PoseA = 43, PoseB = 44,
                         SleepWalkA = 45, SleepWalkB = 46, Adjust = 47, Crouch = 48, GuitarA = 49, GuitarB = 50,
                         Nod = 51, Doze = 52, HumA = 53, HumB = 54, BalanceA = 55, BalanceB = 56, Stretch = 57, TapA = 58,
                         FluffA = 59, FluffB = 60, PillowA = 61, PillowB = 62, Grump = 63, GrumpA = 64, GrumpB = 65,
                         RepairA = 66, RepairB = 67, Back = 68, Attention = 69, Salute = 70, PatA = 71, PatB = 72,
                         TiptoeA = 73, TiptoeB = 74, JuggleA = 75, JuggleB = 76,
                         Sit = 77, SitBlink = 78, SitEatA = 79, SitEatB = 80, SitSleepA = 81, SitSleepB = 82, SitNod = 83,
                         FishA = 84, FishB = 85, FishTug = 86, FishSleep = 87, FishCatch = 88,
                         HugRunA = 89, HugRunB = 90, HugA = 91, HugB = 92, HugSqueeze = 93, HugSleep = 94,
                         ShiverA = 95, ShiverB = 96, SnugA = 97, SnugM = 98, SnugB = 99,
                         KnitA = 100, KnitB = 101;
        public const int Frames = 102;
        public const float HoleX = 160;               // where the fishing line meets the ice (penguin-box units)

        // Accessories (one sprite set each): 0 = none.
        public const int NoOutfit = 0, Nightcap = 1, Scarf = 2, HardHat = 3, Goggles = 4, Headphones = 5, Propeller = 6,
                         PropB = 7, PropC = 8,          // Pip's propeller mid-spin (edge-on blur / blades swapped)
                         GogglesDown = 9,               // Puddle, goggles over the eyes for a slide
                         HatTilt = 10,                  // Wobble, hard hat knocked askew
                         PipScarf = 11, PipScarfB = 12, PipScarfC = 13,   // Pip in Waddles' scarf (each propeller phase)
                         // the Frostbites, the rival family next door (all wear the family's snowflake pin)
                         SailorCap = 14, Shawl = 15, Sweatband = 16, Beret = 17, Bow = 18, EggShell = 19,
                         Outfits = 20, BaseOutfits = 7; // the variants are drawn on first use
        public const float SmallScale = 0.8f;         // Pip, the little brother, is drawn at this size
        static int outfit;     // the accessory being drawn by the current Render call
        static bool scarf;     // ...and whether a scarf goes with it
        static bool sitting;   // sat down on the ground: body lowered, feet poking out in front
        static bool pin;       // a Frostbite: the family's snowflake pin on the chest
        static bool HasHat() { return outfit != NoOutfit && outfit != Scarf && outfit != Shawl; }
        static bool SmallOutfit(int o) { return o == Propeller || o == PropB || o == PropC || o == EggShell; }

        // Sleep frames contain "z" letters, which shouldn't be mirrored.
        public static bool Mirrorable(int f) { return f != SleepA && f != SleepB; }

        // Rendered facing right; mirror for left.
        public static Bitmap Render(int frame, int accessory = NoOutfit)
        {
            bool pipScarf = accessory >= PipScarf && accessory <= PipScarfC;
            scarf = accessory == Scarf || pipScarf;
            outfit = pipScarf ? Propeller + (accessory - PipScarf) : accessory;
            pin = accessory >= SailorCap;
            sitting = false;
            using (var big = new Bitmap(W * SS, H * SS, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(SS * K, SS * K);
                    if (SmallOutfit(outfit))
                    {   // the little ones (Pip, Pearl): same drawing, shrunk about the feet
                        g.TranslateTransform(DW / 2, DH - 2);
                        g.ScaleTransform(SmallScale, SmallScale);
                        g.TranslateTransform(-DW / 2, -(DH - 2));
                    }
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
                        // belly slide: flat on the tummy, chin up, flippers swept back
                        case Slide:   At(g); ProneF(g, 118, 88, -6, 96, 108, 0, 0); break;
                        case SlideB:  At(g); ProneF(g, 118, 87, -6, 91, 113, 4, 0); break;
                        // leapfrog: leaning into the jump, flippers out, feet kicked back
                        case Leap:    AtFeet(g, 24); PenguinF(g, 128, 128, -12, -6, -6, -10, 0); break;
                        // posing for a screenshot: facing the camera, waving / "ta-da!"
                        case PoseA:   At(g); PenguinF(g, 14, 152, 0, 0, 0, 0, 0, 0f, true); break;
                        case PoseB:   At(g); PenguinF(g, 140, 140, -3, -2, 3, -2, 3, 0f, true); break;
                        // Pebble sleepwalking: eyes shut, flippers out in front, slow shuffling steps
                        case SleepWalkA: At(g); PenguinF(g, -55, 86, -4, -5, 3, 0, 1); break;
                        case SleepWalkB: At(g); PenguinF(g, -55, 78, 3, 0, -4, -5, 1); break;
                        // a flipper up to the head: goggles down / hat back on
                        case Adjust:  At(g); PenguinF(g, 12, 172, 0, 0, 0, 0, 0); break;
                        // crouched, ready to launch into a slide
                        case Crouch:  AtFeet(g, 20); PenguinF(g, 48, -10, -3, 0, 4, 0, 0); break;
                        // Ziggy's guitar solo: leaning back, eyes shut, strumming
                        case GuitarA: AtFeet(g, -7);  PenguinF(g, 115, 0, -2, 0, 3, 0, 3); Guitar(g, 12); break;
                        case GuitarB: AtFeet(g, -12); PenguinF(g, 120, 0, -2, 0, 3, -4, 3); Guitar(g, 34); break;
                        // nodding along (to the music in Ziggy's headphones)
                        case Nod:     AtFeet(g, 7); PenguinF(g, 14, 14, 0, 0, 0, 0, 3); break;
                        // Pebble nodding off on his feet
                        case Doze:    AtFeet(g, 10); PenguinF(g, 4, 4, 0, 0, 0, 0, 1); break;
                        // Waddles humming to herself, swaying gently
                        case HumA:    AtFeet(g, -4); PenguinF(g, 26, 18, 0, 0, 0, 0, 3); break;
                        case HumB:    AtFeet(g, 4);  PenguinF(g, 18, 26, 0, 0, 0, 0, 3); break;
                        // Wobble trying to keep his balance on one foot
                        case BalanceA: AtFeet(g, -11); PenguinF(g, 100, 62, 0, -4, 0, 0, 0); break;
                        case BalanceB: AtFeet(g, 11);  PenguinF(g, 62, 100, 0, 0, 0, -4, 0); break;
                        // Puddle's big stretch
                        case Stretch: AtFeet(g, -6); PenguinF(g, 172, 172, -2, 0, 2, 0, 3); break;
                        // Ziggy tapping his foot
                        case TapA:    At(g); PenguinF(g, 14, 14, 0, 0, 6, -8, 0); break;
                        // Pebble fluffing his pillow... and fast asleep on it
                        case FluffA:  AtFeet(g, 5); PenguinF(g, 20, 0, 0, 0, 0, 0, 3); Pillow(g, 117, 72, 42, 26, -8); NearFlipper(g, 72); break;
                        case FluffB:  AtFeet(g, 9); PenguinF(g, 20, 0, 0, 0, 0, 0, 3); Pillow(g, 120, 70, 46, 21, 6); NearFlipper(g, 96); break;
                        case PillowA: At(g); Pillow(g, 138, 110, 48, 22, 0); ProneF(g, 116, 89, 14, 100, 112, 0, 1); break;
                        case PillowB: At(g); Pillow(g, 138, 110, 48, 22, 0); ProneF(g, 116, 90, 16, 97, 115, 1, 1); break;
                        // Pebble, not a morning penguin: heavy lids, a frown, a slouch
                        case Grump:   At(g); PenguinF(g, 4, 4, 0, 0, 0, 0, 5); break;
                        case GrumpA:  AtFeet(g, 5); PenguinF(g, 6, 16, -4, -5, 3, 0, 5); break;
                        case GrumpB:  AtFeet(g, 5); PenguinF(g, 16, 6, 3, 0, -4, -5, 5); break;
                        // Wobble and his wrench: up... clank!
                        case RepairA: AtFeet(g, 12); PenguinF(g, 22, 0, -2, 0, 4, 0, 0); Tool(g, 150, 230, false); break;
                        case RepairB: AtFeet(g, 18); PenguinF(g, 22, 0, -2, 0, 4, 0, 0); Tool(g, 62, 10, true); break;
                        // seen from behind (turning round, twirling)
                        case Back:    At(g); BackF(g); break;
                        // standing to attention / saluting, facing front
                        case Attention: At(g); PenguinF(g, 3, 3, 1, 0, -1, 0, 0, 0f, true); break;
                        case Salute:  At(g); PenguinF(g, 3, 0, 1, 0, -1, 0, 0, 0f, true); NearFlipper(g, 205); break;
                        // Ziggy patting Pip on the head
                        case PatA:    At(g); PenguinF(g, 12, 122, 0, 0, 0, 0, 3); break;
                        case PatB:    At(g); PenguinF(g, 12, 136, 0, 0, 0, 0, 3); break;
                        // tiptoeing: up on the toes, flippers raised, very quiet
                        case TiptoeA: At(g); g.TranslateTransform(0, -5); PenguinF(g, 112, 128, -3, 5, 2, 2, 0); break;
                        case TiptoeB: At(g); g.TranslateTransform(0, -5); PenguinF(g, 112, 128, 2, 2, -3, 5, 0); break;
                        // Wobble fumbling the fish he was thrown: whoa... whoa...
                        case JuggleA: At(g); PenguinF(g, 150, 125, -4, -3, 4, -3, 0); Fish(g, 96, -12, -60, 40); break;
                        // sitting down: watching a video, at the dinner table, listening to music
                        case Sit:       SitAt(g, 0); PenguinF(g, 10, 10, 0, 0, 0, 0, 0); break;
                        case SitBlink:  SitAt(g, 0); PenguinF(g, 10, 10, 0, 0, 0, 0, 1); break;
                        case SitEatA:   SitAt(g, 0); PenguinF(g, 14, 95, 0, 0, 0, 0, 0); Fish(g, 104, 50, -145, 44); break;
                        case SitEatB:   SitAt(g, -8); PenguinF(g, 26, 40, 0, 0, 0, 0, 3); Fish(g, 86, 30, 105, 34); break;
                        case SitSleepA: SitAt(g, 7); PenguinF(g, 6, 6, 0, 0, 0, 0, 1); break;
                        case SitSleepB: SitAt(g, 9); PenguinF(g, 6, 6, 0, 0, 0, 0, 1); break;
                        case SitNod:    SitAt(g, 7); PenguinF(g, 14, 14, 0, 0, 0, 0, 3); break;
                        // ice fishing: sat by the hole, rod out... a bite! ...got one!
                        case FishA:     SitAt(g, 0); PenguinF(g, 10, 0, 0, 0, 0, 0, 0); Rod(g, 112, 161, 12, 0); break;
                        case FishB:     SitAt(g, 0); PenguinF(g, 10, 0, 0, 0, 0, 0, 0); Rod(g, 108, 161, 17, 4); break;
                        case FishTug:   SitAt(g, -6); PenguinF(g, 30, 0, 0, 0, 0, 0, 0); Rod(g, 128, 150, 30, 16); break;
                        case FishSleep: SitAt(g, 6); PenguinF(g, 6, 0, 0, 0, 0, 0, 1); Rod(g, 96, 160, 44, 3); break;
                        case FishCatch: At(g); PenguinF(g, 140, 0, -4, -3, 4, -3, 3); Fish(g, 116, -10, -95, 50); NearFlipper(g, 162); break;
                        // group hug: running in with flippers wide open... cuddled in, swaying... squeeeeze!
                        case HugRunA:    AtFeet(g, 9);  PenguinF(g, 118, 128, -4, -5, 3, 0, 0); break;
                        case HugRunB:    AtFeet(g, 9);  PenguinF(g, 108, 120, 3, 0, -4, -5, 0); break;
                        case HugA:       AtFeet(g, 7);  PenguinF(g, 20, 0, 0, 0, 0, 0, 3); NearFlipper(g, 82); break;
                        case HugB:       AtFeet(g, 11); PenguinF(g, 20, 0, 0, 0, 0, 0, 3); NearFlipper(g, 90); break;
                        case HugSqueeze: AtFeet(g, 16); PenguinF(g, 24, 0, 0, 0, 0, 0, 3); NearFlipper(g, 72); BubbleArt.HeartAt(g, 96, -8, 2.4f, Color.FromArgb(240, 70, 100)); break;
                        case HugSleep:   AtFeet(g, 9);  PenguinF(g, 20, 0, 0, 0, 0, 0, 1); NearFlipper(g, 80); break;
                        // family huddle: brrr, hugging myself on the cold outside... snug in the middle, swaying together
                        case ShiverA:    At(g); g.TranslateTransform(-1.8f, 0); Shiver(g); break;
                        case ShiverB:    At(g); g.TranslateTransform(1.8f, 0);  Shiver(g); break;
                        case SnugA:      AtFeet(g, -4); PenguinF(g, 8, 4, 0, 0, 0, 0, 3); break;
                        case SnugM:      AtFeet(g, 2);  PenguinF(g, 6, 4, 0, 0, 0, 0, 3); break;
                        case SnugB:      AtFeet(g, 8);  PenguinF(g, 4, 6, 0, 0, 0, 0, 3); break;
                        // Juniper knitting: click, click...
                        case KnitA:      At(g); PenguinF(g, 12, 44, 0, 0, 0, 0, 3); Knitting(g, 0); break;
                        case KnitB:      At(g); PenguinF(g, 12, 50, 0, 0, 0, 0, 3); Knitting(g, 1); break;
                        case JuggleB: At(g); PenguinF(g, 125, 155, -3, 0, 3, -4, 0); Fish(g, 58, -22, 170, 40); break;
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
                        case DiveA:   At(g); Tilt(g, 75, 42, 62, 68, 92); ProneF(g, 118, 86, -10, 150, 60, 7, 0); break;
                        case DiveB:   At(g); Tilt(g, 75, 42, 62, 68, 92); ProneF(g, 118, 86, -10, 112, 98, -7, 0); break;
                        // faceplant: a belly-flop, face in the ground, feet wiggling
                        case PlantA:  At(g); ProneF(g, 120, 101, 38, 150, 200, 16, 1); break;   // splat: flat on the tummy, face in the snow
                        case PlantB:  At(g); ProneF(g, 120, 101, 38, 118, 218, 2, 1); break;    // ...feet and flippers flailing
                        // dazed: wobbling, with stars circling the head
                        case DazedA:  AtFeet(g, -5); PenguinF(g, 18, 10, 0, 0, 0, 0, 0); Stars(g, 0); break;
                        case DazedB:  AtFeet(g, 5);  PenguinF(g, 10, 18, 0, 0, 0, 0, 0); Stars(g, 60); break;
                        // being petted: eyes shut, nuzzling forward into the cursor
                        case PetA:    AtFeet(g, 7);  PenguinF(g, 28, 40, 0, 0, 0, 0, 3); break;
                        case PetB:    AtFeet(g, 12); PenguinF(g, 34, 52, 0, 0, 0, 0, 3); break;
                        // lost the staring contest: blinked
                        case Blink:   At(g); PenguinF(g, 12, 12, 0, 0, 0, 0, 1); break;
                        // teasing a sibling: squinty eyes, tongue out
                        case Tease:   AtFeet(g, 4); PenguinF(g, 24, 24, 0, 0, 0, 0, 4); break;
                        // dinner: holding a fish up to the beak... then gulping it down, tail first
                        case EatA:    At(g); PenguinF(g, 14, 95, 0, 0, 0, 0, 0); Fish(g, 104, 50, -145, 44); break;
                        case EatB:    AtFeet(g, -10); PenguinF(g, 26, 40, 0, 0, 0, 0, 3); Fish(g, 86, 30, 105, 34); break;
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

        // Sitting: the whole penguin lowered onto its bottom (leaning by `angle`); PenguinF puts the feet out front.
        static void SitAt(Graphics g, float angle)
        {
            AtFeet(g, angle);
            g.TranslateTransform(0, 13);
            sitting = true;
        }

        // Rotate by `angle` about (cx, cy), and move that point to (tx, ty).
        static void Tilt(Graphics g, float tx, float ty, float angle, float cx, float cy)
        {
            g.TranslateTransform(tx, ty);
            g.RotateTransform(angle);
            g.TranslateTransform(-cx, -cy);
        }

        // A red acoustic guitar held across the tummy, neck up and back, with the strumming flipper on top.
        static void Guitar(Graphics g, float strum)
        {
            var st = g.Save();
            g.TranslateTransform(84, 94);
            g.RotateTransform(28);
            using (var neck = new SolidBrush(Color.FromArgb(125, 80, 40))) g.FillRectangle(neck, -64, -3, 44, 6);
            using (var head = new SolidBrush(Color.FromArgb(55, 38, 26))) g.FillRectangle(head, -76, -5, 13, 10);
            using (var peg = new SolidBrush(Color.FromArgb(230, 220, 190)))
                for (int i = 0; i < 3; i++) { g.FillEllipse(peg, -74 + i * 4, -8, 3, 3); g.FillEllipse(peg, -74 + i * 4, 5, 3, 3); }
            using (var b = new LinearGradientBrush(new RectangleF(-30, -16, 50, 32), Color.FromArgb(240, 70, 60), Color.FromArgb(150, 20, 30), 90f))
            {
                g.FillEllipse(b, -14, -15, 33, 30);
                g.FillEllipse(b, -29, -11, 22, 22);
            }
            using (var edge = new Pen(Color.FromArgb(90, 15, 20), 1.6f))
            {
                g.DrawArc(edge, -14, -15, 33, 30, -70, 140);
                g.DrawArc(edge, -29, -11, 22, 22, 110, 140);
            }
            using (var hole = new SolidBrush(Color.FromArgb(45, 22, 15))) g.FillEllipse(hole, -13, -5, 10, 10);
            using (var bridge = new SolidBrush(Color.FromArgb(60, 35, 20))) g.FillRectangle(bridge, 7, -5, 3, 10);
            using (var str = new Pen(Color.FromArgb(235, 235, 215), 0.9f))
            {
                g.DrawLine(str, -64, -1.6f, 8, -1.6f);
                g.DrawLine(str, -64, 1.6f, 8, 1.6f);
            }
            g.Restore(st);
            using (var rim = new Pen(Color.FromArgb(95, 95, 108), 3f))
            using (var black = new SolidBrush(Color.FromArgb(18, 18, 22)))
                Flipper(g, 99, 56, strum, black, rim);
        }

        // A flipper drawn on top of everything else (holding something, saluting).
        // Two needles clicking away over a bit of teal knitting, the ball of yarn at her feet.
        static void Knitting(Graphics g, int k)
        {
            var piece = new RectangleF(108, 66, 28, 20);
            using (var wool = new SolidBrush(Color.FromArgb(70, 170, 165)))
            using (var row = new Pen(Color.FromArgb(150, 230, 225), 1.2f))
            {
                g.FillRectangle(wool, piece);
                for (int i = 0; i < 4; i++) g.DrawLine(row, piece.X + 1, piece.Y + 3 + i * 5, piece.Right - 1, piece.Y + 3 + i * 5);
            }
            using (var yarn = new Pen(Color.FromArgb(70, 170, 165), 2f))
                g.DrawBezier(yarn, 124, 86, 126, 100, 134, 94, 136, 102);
            var ball = new RectangleF(126, 98, 22, 20);
            using (var wool = new SolidBrush(Color.FromArgb(80, 185, 180))) g.FillEllipse(wool, ball);
            using (var line = new Pen(Color.FromArgb(40, 120, 118), 1.2f))
            {
                g.DrawArc(line, ball.X + 3, ball.Y + 3, 16, 14, 200, 140);
                g.DrawArc(line, ball.X + 4, ball.Y + 6, 13, 12, 20, 140);
            }
            float o = k == 0 ? 0 : 3;
            using (var needle = new Pen(Color.FromArgb(240, 200, 110), 3.2f))
            {
                needle.StartCap = needle.EndCap = LineCap.Round;
                g.DrawLine(needle, 104 + o, 54, 140 - o, 80);
                g.DrawLine(needle, 142 - o, 54, 106 + o, 80);
            }
            NearFlipper(g, k == 0 ? 52 : 46);
        }

        // The Frostbites' family pin: a little snowflake badge.
        static void Pin(Graphics g, float x, float y)
        {
            using (var disc = new SolidBrush(Color.FromArgb(45, 125, 215)))
            using (var rim = new Pen(Color.FromArgb(25, 70, 140), 1.6f))
            {
                g.FillEllipse(disc, x - 8, y - 8, 16, 16);
                g.DrawEllipse(rim, x - 8, y - 8, 16, 16);
            }
            using (var arm = new Pen(Color.White, 2f))
            {
                arm.StartCap = arm.EndCap = LineCap.Round;
                for (int k = 0; k < 3; k++)
                {
                    double a = k * Math.PI / 3 + Math.PI / 2;
                    float dx = (float)Math.Cos(a) * 5.2f, dy = (float)Math.Sin(a) * 5.2f;
                    g.DrawLine(arm, x - dx, y - dy, x + dx, y + dy);
                }
            }
        }

        // Juniper's knitted shawl over the shoulders, with a little fringe.
        static void ShawlWear(Graphics g, float cx, float fd)
        {
            var drape = new[] { new PointF(cx - 33, 47), new PointF(cx + 33, 47), new PointF(cx + 31, 60),
                                new PointF(cx + 4 + fd, 82), new PointF(cx - 31, 60) };
            using (var b = new LinearGradientBrush(new RectangleF(cx - 34, 44, 68, 40), Color.FromArgb(90, 190, 185), Color.FromArgb(40, 125, 125), 90f))
                g.FillPolygon(b, drape);
            using (var knit = new Pen(Color.FromArgb(170, 235, 230), 1.3f))
            {
                g.DrawLine(knit, cx - 26, 54, cx + 26, 54);
                g.DrawLine(knit, cx - 18, 62, cx + 20, 62);
                g.DrawLine(knit, cx - 8, 70, cx + 12, 70);
            }
            using (var edge = new Pen(Color.FromArgb(30, 95, 95), 1.4f)) g.DrawPolygon(edge, drape);
            using (var fringe = new Pen(Color.FromArgb(60, 150, 148), 1.5f))
                for (int i = 0; i < 4; i++)
                    g.DrawLine(fringe, cx - 2 + fd + i * 3, 80 - i * 2, cx - 2 + fd + i * 3, 86 - i * 2);
            using (var pinB = new SolidBrush(Color.FromArgb(240, 200, 90))) g.FillEllipse(pinB, cx + 8 + fd, 47, 6, 6);   // a little brooch
        }

        static void Neck(Graphics g, float cx, float fd)
        {
            if (scarf) NeckWear(g, cx, fd);
            if (outfit == Shawl) ShawlWear(g, cx, fd);
        }

        // The Frostbites' headgear. Returns false for anything else.
        static bool FrostWear(Graphics g, float cx, float fd)
        {
            switch (outfit)
            {
                case SailorCap:
                {   // Captain Bubbles: white cap, navy band, black peak, gold anchor badge
                    var crown = new RectangleF(cx - 25, -7, 52, 18);
                    using (var w = new LinearGradientBrush(crown, Color.White, Color.FromArgb(210, 215, 225), 90f)) g.FillEllipse(w, crown);
                    using (var edge = new Pen(Color.FromArgb(120, 125, 140), 1.4f)) g.DrawEllipse(edge, crown);
                    using (var navy = new SolidBrush(Color.FromArgb(35, 45, 95))) g.FillRectangle(navy, cx - 22, 3, 44, 9);
                    using (var peak = new SolidBrush(Color.FromArgb(25, 25, 30))) g.FillEllipse(peak, cx + 2 + fd, 8, 28, 8);
                    using (var gold = new SolidBrush(Color.FromArgb(245, 200, 60))) g.FillEllipse(gold, cx - 3 + fd * 0.5f, -3, 8, 8);
                    using (var anchor = new Pen(Color.FromArgb(35, 45, 95), 1.2f))
                    {
                        float ax = cx + 1 + fd * 0.5f;
                        g.DrawLine(anchor, ax, -2, ax, 4);
                        g.DrawArc(anchor, ax - 2.5f, 0, 5, 4, 0, 180);
                    }
                    return true;
                }
                case Sweatband:
                {   // Bolt: red sweatband with a white stripe, ends flicking out behind
                    using (var red = new SolidBrush(Color.FromArgb(225, 45, 50)))
                    {
                        g.FillPolygon(red, new[] { new PointF(cx - 24, 14), new PointF(cx - 38, 8), new PointF(cx - 36, 16) });
                        g.FillPolygon(red, new[] { new PointF(cx - 24, 18), new PointF(cx - 36, 22), new PointF(cx - 33, 27) });
                        g.FillRectangle(red, cx - 25, 11, 50, 10);
                    }
                    using (var white = new SolidBrush(Color.FromArgb(250, 250, 250))) g.FillRectangle(white, cx - 25, 14.5f, 50, 3);
                    using (var edge = new Pen(Color.FromArgb(140, 20, 25), 1.2f)) g.DrawRectangle(edge, cx - 25, 11, 50, 10);
                    return true;
                }
                case Beret:
                {   // Doodle: a purple beret worn at an arty angle, with its little stalk
                    var st = g.Save();
                    g.TranslateTransform(cx - 4, 4);
                    g.RotateTransform(-12);
                    var cap = new RectangleF(-27, -12, 52, 20);
                    using (var b = new LinearGradientBrush(cap, Color.FromArgb(160, 85, 200), Color.FromArgb(95, 40, 135), 90f)) g.FillEllipse(b, cap);
                    using (var edge = new Pen(Color.FromArgb(60, 25, 90), 1.4f)) g.DrawEllipse(edge, cap);
                    using (var stalk = new Pen(Color.FromArgb(70, 30, 100), 2.4f)) g.DrawLine(stalk, -2, -12, 0, -18);
                    g.Restore(st);
                    return true;
                }
                case Bow:
                {   // Mochi: a big pink bow on the side of her head
                    float bx = cx - 6, by = 6;
                    using (var pink = new SolidBrush(Color.FromArgb(255, 125, 170)))
                    using (var edge = new Pen(Color.FromArgb(190, 60, 110), 1.3f))
                    {
                        var l = new[] { new PointF(bx, by), new PointF(bx - 15, by - 9), new PointF(bx - 14, by + 9) };
                        var r = new[] { new PointF(bx, by), new PointF(bx + 15, by - 9), new PointF(bx + 14, by + 9) };
                        g.FillPolygon(pink, l); g.DrawPolygon(edge, l);
                        g.FillPolygon(pink, r); g.DrawPolygon(edge, r);
                        g.FillEllipse(pink, bx - 4, by - 4, 8, 8); g.DrawEllipse(edge, bx - 4, by - 4, 8, 8);
                    }
                    return true;
                }
                case EggShell:
                {   // Pearl: the top of her eggshell, still on her head, cracked zigzag rim
                    var pts = new List<PointF>();
                    pts.Add(new PointF(cx - 26, 17));
                    for (int i = 0; i <= 8; i++) pts.Add(new PointF(cx - 26 + i * 6.5f, i % 2 == 0 ? 17 : 11));
                    pts.Add(new PointF(cx + 26, 17));
                    using (var path = new GraphicsPath())
                    {
                        path.AddArc(cx - 26, -12, 52, 54, 180, 180);
                        path.AddLines(pts.ToArray());
                        path.CloseFigure();
                        using (var b = new LinearGradientBrush(new RectangleF(cx - 26, -12, 52, 30), Color.FromArgb(255, 252, 240), Color.FromArgb(225, 215, 190), 90f))
                            g.FillPath(b, path);
                        using (var edge = new Pen(Color.FromArgb(160, 145, 115), 1.4f)) g.DrawPath(edge, path);
                    }
                    using (var spot = new SolidBrush(Color.FromArgb(150, 190, 175, 140)))
                    {
                        g.FillEllipse(spot, cx - 12, -4, 5, 4);
                        g.FillEllipse(spot, cx + 8, 0, 4, 3);
                    }
                    return true;
                }
            }
            return false;
        }

        // Facing us, flippers wrapped round the belly, with little shiver marks either side.
        static void Shiver(Graphics g)
        {
            PenguinF(g, -30, -30, 0, 0, 0, 0, 0, 0f, true);
            using (var rim = new Pen(Color.FromArgb(95, 95, 108), 3f))
            using (var black = new SolidBrush(Color.FromArgb(18, 18, 22)))
            {
                Flipper(g, 53, 60, -60, black, rim);
                Flipper(g, 97, 60, 60, black, rim);
            }
            using (var brr = new Pen(Color.FromArgb(200, 120, 175, 230), 2.4f))
            {
                brr.StartCap = brr.EndCap = LineCap.Round;
                for (int k = 0; k < 2; k++)
                {
                    float o = k * 7;
                    g.DrawArc(brr, 24 - o, 48 - o, 12 + o, 26 + o * 2, 110, 140);    // ( (
                    g.DrawArc(brr, 114, 48 - o, 12 + o, 26 + o * 2, -70, 140);       //   ) )
                }
            }
        }

        static void NearFlipper(Graphics g, float armR)
        {
            using (var rim = new Pen(Color.FromArgb(95, 95, 108), 3f))
            using (var black = new SolidBrush(Color.FromArgb(18, 18, 22)))
                Flipper(g, 100, 54, -armR, black, rim);
        }

        static GraphicsPath Rounded(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Pebble's puffy pale-blue pillow, centred on (x, y).
        static void Pillow(Graphics g, float x, float y, float w, float h, float angle)
        {
            var st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(angle);
            var r = new RectangleF(-w / 2, -h / 2, w, h);
            using (var path = Rounded(r, Math.Min(w, h) * 0.45f))
            {
                using (var b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Color.FromArgb(246, 248, 255), Color.FromArgb(170, 186, 236), 90f))
                    g.FillPath(b, path);
                var cs = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                using (var stripe = new Pen(Color.FromArgb(120, 255, 255, 255), 3f))
                    for (float sx = r.X + 8; sx < r.Right; sx += 11) g.DrawLine(stripe, sx, r.Y, sx - 4, r.Bottom);
                g.Restore(cs);
                using (var edge = new Pen(Color.FromArgb(105, 118, 180), 2f)) g.DrawPath(edge, path);
            }
            using (var dent = new SolidBrush(Color.FromArgb(120, 136, 205))) g.FillEllipse(dent, -2.2f, -2.2f, 4.4f, 4.4f);
            g.Restore(st);
        }

        // A fishing rod in the near flipper (at armR), its tip at (tipX, tipY), the line dropping into the
        // hole at HoleX on the ground. Drawn while sitting, so the ground is at y = 110. `bend` bows the rod.
        static void Rod(Graphics g, float armR, float tipX, float tipY, float bend)
        {
            double a = armR * Math.PI / 180;
            float hx = 100 + 33 * (float)Math.Sin(a), hy = 54 + 33 * (float)Math.Cos(a);   // where the flipper holds it
            float dx = tipX - hx, dy = tipY - hy, len = (float)Math.Sqrt(dx * dx + dy * dy);
            float ux = dx / len, uy = dy / len;
            float bx = hx - ux * 12, by = hy - uy * 12;                                     // the butt, behind the grip
            float mx = hx + dx * 0.55f - uy * -bend, my = hy + dy * 0.55f + ux * -bend;    // bowed toward the line
            using (var line = new Pen(Color.FromArgb(225, 236, 240, 248), 2.2f))
                g.DrawLine(line, tipX, tipY, HoleX, 110);
            using (var rod = new Pen(Color.FromArgb(125, 80, 40), 3.6f))
            {
                rod.StartCap = rod.EndCap = LineCap.Round;
                g.DrawBezier(rod, bx, by, hx, hy, mx, my, tipX, tipY);
            }
            using (var reel = new SolidBrush(Color.FromArgb(170, 175, 188)))
            using (var edge = new Pen(Color.FromArgb(70, 72, 85), 1.4f))
            {
                g.FillEllipse(reel, bx + ux * 6 - 5, by + uy * 6 + 1, 10, 10);
                g.DrawEllipse(edge, bx + ux * 6 - 5, by + uy * 6 + 1, 10, 10);
            }
            NearFlipper(g, armR);
        }

        // Wobble's wrench gripped at the near flipper's tip, pointing along `dir` (0 down, 90 forward, 180 up).
        static void Tool(Graphics g, float armR, float dir, bool spark)
        {
            double a = armR * Math.PI / 180;
            float tx = 100 + 36 * (float)Math.Sin(a), ty = 54 + 36 * (float)Math.Cos(a);
            var st = g.Save();
            g.TranslateTransform(tx, ty);
            g.RotateTransform(-dir);                  // local +y now points along dir
            var shape = new[] {
                new PointF(-3.5f, -6), new PointF(3.5f, -6), new PointF(3.5f, 28), new PointF(10, 28), new PointF(10, 43),
                new PointF(4, 43), new PointF(4, 34), new PointF(-4, 34), new PointF(-4, 43), new PointF(-10, 43), new PointF(-10, 28), new PointF(-3.5f, 28) };
            using (var b = new LinearGradientBrush(new RectangleF(-11, -8, 22, 54), Color.FromArgb(232, 236, 244), Color.FromArgb(120, 128, 146), 0f))
                g.FillPolygon(b, shape);
            using (var edge = new Pen(Color.FromArgb(60, 65, 80), 1.8f)) g.DrawPolygon(edge, shape);
            g.Restore(st);
            if (spark)
            {   // clank!
                double d = dir * Math.PI / 180;
                float sx = tx + 46 * (float)Math.Sin(d), sy = ty + 46 * (float)Math.Cos(d);
                using (var p = new Pen(Color.FromArgb(255, 205, 30), 2.8f))
                {
                    p.StartCap = p.EndCap = LineCap.Round;
                    for (int i = 0; i < 5; i++)
                    {
                        double sa = (200 + i * 35) * Math.PI / 180;
                        g.DrawLine(p, sx + 6 * (float)Math.Cos(sa), sy + 6 * (float)Math.Sin(sa), sx + 14 * (float)Math.Cos(sa), sy + 14 * (float)Math.Sin(sa));
                    }
                }
            }
            NearFlipper(g, armR);
        }

        // The penguin seen from behind: all black, a little tail, no face.
        static void BackF(Graphics g)
        {
            const float cx = 75f;
            using (var rim = new Pen(Color.FromArgb(95, 95, 108), 3f))
            using (var black = new SolidBrush(Color.FromArgb(18, 18, 22)))
            {
                Foot(g, cx - 27, 109);
                Foot(g, cx + 1, 109);
                Flipper(g, cx - 27, 54, 16, black, rim);
                Flipper(g, cx + 27, 54, -16, black, rim);
                var head = new RectangleF(cx - 25, 5, 50, 50);
                var body = new RectangleF(cx - 34, 33, 68, 83);
                g.DrawEllipse(rim, head);
                g.DrawEllipse(rim, body);
                g.FillEllipse(black, head);
                g.FillEllipse(black, body);
                using (var tail = new SolidBrush(Color.FromArgb(48, 48, 58)))
                    g.FillPolygon(tail, new[] { new PointF(cx - 9, 105), new PointF(cx + 9, 105), new PointF(cx, 121) });
                using (var sheen = new Pen(Color.FromArgb(80, 255, 255, 255), 2.6f))
                {
                    sheen.StartCap = sheen.EndCap = LineCap.Round;
                    g.DrawArc(sheen, cx - 19, 10, 38, 38, 205, 60);
                    g.DrawArc(sheen, cx - 28, 40, 56, 70, 200, 45);
                }
                Neck(g, cx, 0);
                if (outfit == Goggles || outfit == GogglesDown)
                    using (var strap = new Pen(Color.FromArgb(40, 150, 160), 3.6f)) g.DrawLine(strap, cx - 25, 13, cx + 25, 13);
                else if (HasHat()) HeadWear(g, cx, -3);
            }
        }

        // Little puffs of snow: x, y, radius triples.
        static void Puffs(Graphics g, params float[] xyr)
        {
            using (var snow = new SolidBrush(Color.FromArgb(240, 248, 255)))
            using (var edge = new Pen(Color.FromArgb(165, 190, 220), 1.4f))
                for (int i = 0; i + 2 < xyr.Length; i += 3)
                {
                    var r = new RectangleF(xyr[i] - xyr[i + 2], xyr[i + 1] - xyr[i + 2], xyr[i + 2] * 2, xyr[i + 2] * 2);
                    g.FillEllipse(snow, r);
                    g.DrawEllipse(edge, r);
                }
        }

        // Lying on its tummy, facing right, side view, in the 150x125 box (ground at y = 123).
        // hx, hy: head centre; ha: head tilt (positive = chin down); flipNear / flipFar: flipper
        // angles (90 = swept straight back, 180 = straight up); kick: feet up / down.
        static void ProneF(Graphics g, float hx, float hy, float ha, float flipNear, float flipFar, float kick, int face)
        {
            using (var rim = new Pen(Color.FromArgb(95, 95, 108), 3f))
            using (var black = new SolidBrush(Color.FromArgb(18, 18, 22)))
            {
                Foot(g, -6, 97 - kick);                         // feet trailing behind
                Foot(g, -1, 106 + kick * 0.5f);
                Flipper(g, 90, 88, flipFar, black, rim);        // far flipper, behind the body
                var body = new RectangleF(14, 80, 100, 42);
                var head = new RectangleF(hx - 25, hy - 25, 50, 50);
                g.DrawEllipse(rim, body);
                g.DrawEllipse(rim, head);
                g.FillEllipse(black, body);
                g.FillEllipse(black, head);
                using (var path = new GraphicsPath())
                {   // the white tummy, underneath
                    path.AddEllipse(body);
                    var st = g.Save();
                    g.SetClip(path, CombineMode.Intersect);
                    var belly = new RectangleF(20, 102, 96, 28);
                    using (var b = new LinearGradientBrush(belly, Color.White, Color.FromArgb(214, 214, 222), 90f))
                        g.FillEllipse(b, belly);
                    g.Restore(st);
                }
                Flipper(g, 84, 98, flipNear, black, rim);       // near flipper, along the side
                if (scarf || outfit == Shawl)
                {   // scarf round the neck, tail streaming out behind
                    var st = g.Save();
                    g.TranslateTransform(hx - 21, hy + 8);
                    g.RotateTransform(90 + ha * 0.5f);
                    g.TranslateTransform(-75, -52.5f);
                    Neck(g, 75, 0);
                    g.Restore(st);
                }
                var hs = g.Save();
                g.TranslateTransform(hx, hy);
                g.RotateTransform(ha);
                g.TranslateTransform(-75, -30);
                Face(g, 75, 10, 6.5f, face, false, black);
                if (HasHat()) HeadWear(g, 75, 10);
                g.Restore(hs);
            }
        }

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
        // face: 0 eyes open, 1 asleep, 2 yawning (eyes shut, beak wide open), 3 content (happy shut eyes, blush), 4 teasing (tongue out)
        // headTurn: 0 = normal 3/4 view; otherwise the body faces front and only the face
        //           turns (-1 left .. 1 right) with a frown, for shaking the head "no".
        static void PenguinF(Graphics g, float armL, float armR, float flx, float fly, float frx, float fry, int face,
                             float headTurn = 0f, bool front = false)
        {
            bool asleep = face != 0;
            bool shaking = headTurn != 0;
            const float cx = 75f;
            bool frontal = shaking || front;                  // body squarely facing us
            float d = frontal ? 0f : 4f;                      // body / belly offset
            float fd = shaking ? headTurn * 10f : front ? 0f : 4f;          // face offset
            float look = shaking ? 3.75f + headTurn * 3f : front ? 3.75f : 6.5f;   // pupil position inside the eye
            using (var rim = new Pen(Color.FromArgb(95, 95, 108), 3f))
            using (var black = new SolidBrush(Color.FromArgb(18, 18, 22)))
            {
                if (!sitting)
                {
                    Foot(g, cx - 29 + flx + d * 0.5f, 108 + fly);
                    Foot(g, cx + 3 + frx + d * 0.5f, 108 + fry);
                }

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
                if (sitting)
                {   // feet sticking out in front, toes up
                    Foot(g, cx - 19 + d, 93, true);         // the far one, a shade darker
                    Foot(g, cx + 10 + d, 96);
                }

                GraphicsState faceState = null;
                if (shaking)
                {   // tilt the face a little toward the way it's turning
                    faceState = g.Save();
                    g.TranslateTransform(cx, 30);
                    g.RotateTransform(headTurn * 12f);
                    g.TranslateTransform(-cx, -30);
                }

                Neck(g, cx, fd);
                if (pin) Pin(g, cx - 9 + d, outfit == Shawl ? 64 : 66);

                Face(g, cx, fd, look, face, shaking, black);
                if (faceState != null) g.Restore(faceState);
                if (HasHat()) HeadWear(g, cx, fd);
            }
        }

        // Eyes, brows and beak for a head centred on (cx, 30); fd shifts the face toward the front.
        static void Face(Graphics g, float cx, float fd, float look, int face, bool shaking, Brush black)
        {
            bool asleep = face != 0;
            if (face == 3 || face == 4)
            {   // content (3) / teasing (4): upturned "^ ^" eyes and rosy cheeks
                using (var lid = new Pen(Color.FromArgb(235, 235, 240), 2.6f))
                {
                    lid.StartCap = lid.EndCap = LineCap.Round;
                    g.DrawArc(lid, cx - 15 + fd, 21, 13, 10, 200, 140);
                    g.DrawArc(lid, cx + 2 + fd, 21, 13, 10, 200, 140);
                }
                if (face == 3)
                using (var blush = new SolidBrush(Color.FromArgb(150, 255, 120, 150)))
                {
                    g.FillEllipse(blush, cx - 21 + fd, 31, 9, 5);
                    g.FillEllipse(blush, cx + 14 + fd, 31, 9, 5);
                }
            }
            else if (face == 5)
            {   // grumpy: lids heavy and half shut, slanting down to the middle
                using (var white = new SolidBrush(Color.White))
                {
                    g.FillEllipse(white, cx - 15 + fd, 16, 13, 18);
                    g.FillEllipse(white, cx + 2 + fd, 16, 13, 18);
                }
                g.FillEllipse(black, cx - 15 + fd + look, 25, 5.5f, 8);
                g.FillEllipse(black, cx + 2 + fd + look, 25, 5.5f, 8);
                g.FillPolygon(black, new[] { new PointF(cx - 17 + fd, 13), new PointF(cx + 17 + fd, 13), new PointF(cx + 17 + fd, 22),
                    new PointF(cx + 3 + fd, 25.5f), new PointF(cx - 3 + fd, 25.5f), new PointF(cx - 17 + fd, 22) });
                using (var lid = new Pen(Color.FromArgb(205, 205, 215), 2.4f))
                {
                    lid.StartCap = lid.EndCap = LineCap.Round;
                    g.DrawLine(lid, cx - 15 + fd, 22, cx - 3 + fd, 25.5f);
                    g.DrawLine(lid, cx + 15 + fd, 22, cx + 3 + fd, 25.5f);
                }
            }
            else if (asleep)
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
                if (face == 4)   // tongue poking out under the beak
                    using (var tongue = new SolidBrush(Color.FromArgb(240, 90, 120))) g.FillEllipse(tongue, cx - 5 + fd * 1.4f, 42, 14, 19);
                var beak = new RectangleF(cx - 16 + fd * 1.4f, 34, 32, 15);
                using (var b = new LinearGradientBrush(beak, Color.FromArgb(255, 214, 70), Color.FromArgb(232, 145, 0), 90f))
                    g.FillEllipse(b, beak);
                using (var p = new Pen(Color.FromArgb(180, 110, 0), 1.6f))
                {
                    if (shaking || face == 5) g.DrawArc(p, cx - 10 + fd * 1.4f, 41, 20, 8, 200, 140);   // turned-down mouth
                    else g.DrawArc(p, cx - 12 + fd * 1.4f, 37, 24, 8, 20, 140);
                }
            }
        }

        // Red knitted scarf with a tail hanging down the back, drawn under the beak.
        static void NeckWear(Graphics g, float cx, float fd)
        {
            var wrap = new RectangleF(cx - 27, 47, 54, 11);
            using (var b = new LinearGradientBrush(wrap, Color.FromArgb(232, 60, 60), Color.FromArgb(170, 25, 35), 90f))
            {
                var tail = new[] { new PointF(cx - 22, 52), new PointF(cx - 12, 52), new PointF(cx - 15, 80), new PointF(cx - 27, 78) };
                g.FillPolygon(b, tail);
                g.FillEllipse(b, wrap);
            }
            using (var stripe = new Pen(Color.FromArgb(250, 235, 225), 2.2f))
            {
                g.DrawLine(stripe, cx - 25, 71, cx - 14, 72);
                g.DrawLine(stripe, cx - 24, 65, cx - 13, 66);
            }
            using (var edge = new Pen(Color.FromArgb(120, 15, 25), 1.4f)) g.DrawEllipse(edge, wrap);
        }

        // Nightcap, hard hat, swim goggles or headphones, drawn over the head.
        static void HeadWear(Graphics g, float cx, float fd)
        {
            if (FrostWear(g, cx, fd)) return;
            if (outfit == HatTilt)
            {   // the hard hat knocked askew, down over the eyes
                var st = g.Save();
                g.TranslateTransform(cx + 7, 15);
                g.RotateTransform(26);
                g.TranslateTransform(-cx, -8);
                outfit = HardHat;
                HeadWear(g, cx, fd);
                outfit = HatTilt;
                g.Restore(st);
                return;
            }
            if (outfit == GogglesDown)
            {   // swim goggles pulled down over the eyes: ready to slide
                using (var strap = new Pen(Color.FromArgb(40, 150, 160), 3.6f)) g.DrawLine(strap, cx - 25, 24, cx + 24, 22);
                using (var lens = new SolidBrush(Color.FromArgb(150, 120, 215, 250)))
                using (var rim = new Pen(Color.FromArgb(20, 120, 140), 2.8f))
                {
                    g.FillEllipse(lens, cx - 17 + fd, 15, 16, 19); g.DrawEllipse(rim, cx - 17 + fd, 15, 16, 19);
                    g.FillEllipse(lens, cx + 1 + fd, 15, 16, 19);  g.DrawEllipse(rim, cx + 1 + fd, 15, 16, 19);
                    g.DrawLine(rim, cx - 1 + fd, 23, cx + 1 + fd, 23);
                }
                using (var shine = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                {
                    g.FillEllipse(shine, cx - 13 + fd, 18, 5, 4);
                    g.FillEllipse(shine, cx + 5 + fd, 18, 5, 4);
                }
                return;
            }
            if (outfit == Propeller || outfit == PropB || outfit == PropC)
            {   // rainbow propeller beanie
                var dome = new RectangleF(cx - 26, -6, 52, 40);
                Color[] seg = { Color.FromArgb(235, 60, 60), Color.FromArgb(255, 205, 40), Color.FromArgb(60, 170, 90), Color.FromArgb(60, 130, 235) };
                for (int i = 0; i < 4; i++)
                    using (var b = new SolidBrush(seg[i])) g.FillPie(b, dome.X, dome.Y, dome.Width, dome.Height, 180 + i * 45, 45);
                using (var edge = new Pen(Color.FromArgb(40, 40, 60), 1.6f)) g.DrawArc(edge, dome, 180, 180);
                using (var band = new SolidBrush(Color.FromArgb(40, 50, 110))) g.FillRectangle(band, cx - 26, 11, 52, 4);
                using (var stem = new Pen(Color.FromArgb(60, 60, 70), 2.4f)) g.DrawLine(stem, cx, -6, cx, -15);
                using (var red = new SolidBrush(Color.FromArgb(235, 60, 60)))
                using (var blue = new SolidBrush(Color.FromArgb(60, 130, 235)))
                using (var edge = new Pen(Color.FromArgb(40, 40, 60), 1.2f))
                using (var blur = new SolidBrush(Color.FromArgb(95, 205, 215, 235)))
                {
                    if (outfit == PropB)
                    {   // spinning: a blur, with the blades edge-on
                        g.FillEllipse(blur, cx - 22, -20, 44, 7);
                        g.FillEllipse(red, cx - 10, -19, 9, 5);
                        g.FillEllipse(blue, cx + 1, -19, 9, 5);
                    }
                    else
                    {
                        if (outfit == PropC) g.FillEllipse(blur, cx - 22, -20, 44, 7);
                        var left = outfit == PropC ? blue : red;
                        var right = outfit == PropC ? red : blue;
                        g.FillEllipse(left, cx - 19, -20, 17, 7); g.DrawEllipse(edge, cx - 19, -20, 17, 7);
                        g.FillEllipse(right, cx + 2, -20, 17, 7); g.DrawEllipse(edge, cx + 2, -20, 17, 7);
                    }
                }
                using (var hub = new SolidBrush(Color.FromArgb(255, 205, 40))) g.FillEllipse(hub, cx - 3.5f, -20, 7, 7);
            }
            else if (outfit == HardHat)
            {   // bright yellow safety helmet with a ridge and a brim: for the clumsy one
                var dome = new RectangleF(cx - 27, -10, 54, 44);
                using (var b = new LinearGradientBrush(dome, Color.FromArgb(255, 236, 90), Color.FromArgb(235, 170, 0), 90f))
                    g.FillPie(b, dome.X, dome.Y, dome.Width, dome.Height, 180, 180);
                using (var edge = new Pen(Color.FromArgb(150, 95, 0), 2f))
                {
                    g.DrawArc(edge, dome, 180, 180);
                    using (var ridge = new SolidBrush(Color.FromArgb(255, 250, 190)))
                        g.FillRectangle(ridge, cx - 3 + fd * 0.5f, -9, 6, 20);
                    var brim = new RectangleF(cx - 33 + fd * 0.6f, 9, 66, 9);
                    using (var bb = new SolidBrush(Color.FromArgb(245, 190, 20))) g.FillEllipse(bb, brim);
                    g.DrawEllipse(edge, brim);
                }
            }
            else if (outfit == Nightcap)
            {   // floppy blue nightcap drooping backwards, with a pom-pom
                var cone = new[] { new PointF(cx - 24, 13), new PointF(cx + 23, 11), new PointF(cx + 4, -14), new PointF(cx - 22, -22), new PointF(cx - 40, -6) };
                using (var b = new LinearGradientBrush(new RectangleF(cx - 42, -24, 66, 38), Color.FromArgb(110, 150, 240), Color.FromArgb(45, 70, 170), 90f))
                    g.FillPolygon(b, cone);
                using (var edge = new Pen(Color.FromArgb(30, 45, 110), 1.6f)) g.DrawPolygon(edge, cone);
                using (var band = new SolidBrush(Color.FromArgb(245, 245, 250)))
                {
                    g.FillEllipse(band, cx - 27, 6, 54, 12);
                    g.FillEllipse(band, cx - 48, -12, 13, 13);     // pom-pom
                }
            }
            else if (outfit == Goggles)
            {   // swim goggles pushed up on the forehead
                using (var strap = new Pen(Color.FromArgb(40, 150, 160), 3.2f)) g.DrawLine(strap, cx - 25, 11, cx + 24, 8);
                using (var lens = new SolidBrush(Color.FromArgb(200, 140, 225, 255)))
                using (var rim = new Pen(Color.FromArgb(20, 120, 140), 2.4f))
                {
                    g.FillEllipse(lens, cx - 14 + fd, 3, 13, 10); g.DrawEllipse(rim, cx - 14 + fd, 3, 13, 10);
                    g.FillEllipse(lens, cx + 2 + fd, 2, 13, 10);  g.DrawEllipse(rim, cx + 2 + fd, 2, 13, 10);
                }
                using (var shine = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
                {
                    g.FillEllipse(shine, cx - 11 + fd, 5, 4, 3);
                    g.FillEllipse(shine, cx + 5 + fd, 4, 4, 3);
                }
            }
            else
            {   // headphones: band over the top, ear cup on the back of the head
                using (var band = new Pen(Color.FromArgb(60, 60, 70), 4f))
                    g.DrawArc(band, cx - 27, 1, 52, 44, 190, 160);
                var cup = new RectangleF(cx - 31, 18, 15, 21);
                using (var b = new LinearGradientBrush(cup, Color.FromArgb(255, 110, 180), Color.FromArgb(200, 40, 120), 0f))
                    g.FillEllipse(b, cup);
                using (var edge = new Pen(Color.FromArgb(60, 60, 70), 2f)) g.DrawEllipse(edge, cup);
            }
        }

        // A blue-grey fish centred on (x, y), head pointing along `angle`.
        public static void Fish(Graphics g, float x, float y, float angle, float len)
        {
            var st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(angle);
            float h = len * 0.42f;
            var body = new RectangleF(-len * 0.32f, -h / 2, len * 0.82f, h);
            using (var b = new LinearGradientBrush(new RectangleF(-len / 2, -h / 2 - 1, len, h + 2), Color.FromArgb(160, 200, 230), Color.FromArgb(70, 110, 165), 90f))
            {
                g.FillPolygon(b, new[] { new PointF(-len * 0.24f, 0), new PointF(-len * 0.5f, -h * 0.6f), new PointF(-len * 0.5f, h * 0.6f) });
                g.FillEllipse(b, body);
            }
            using (var edge = new Pen(Color.FromArgb(45, 75, 120), Math.Max(0.6f, len * 0.035f))) g.DrawEllipse(edge, body);
            using (var belly = new SolidBrush(Color.FromArgb(200, 235, 245, 252))) g.FillEllipse(belly, -len * 0.12f, h * 0.02f, len * 0.5f, h * 0.32f);
            using (var eye = new SolidBrush(Color.FromArgb(20, 20, 30))) g.FillEllipse(eye, len * 0.3f, -h * 0.24f, len * 0.08f, len * 0.08f);
            g.Restore(st);
        }

        static void Foot(Graphics g, float x, float y, bool far = false)
        {
            var r = new RectangleF(x, y, 26, 15);
            using (var b = far ? new LinearGradientBrush(r, Color.FromArgb(226, 160, 30), Color.FromArgb(190, 100, 0), 90f)
                               : new LinearGradientBrush(r, Color.FromArgb(255, 196, 50), Color.FromArgb(222, 125, 0), 90f))
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
        public static Bitmap[,] RenderAll(int accessory = NoOutfit)
        {
            var all = new Bitmap[2, Frames];
            for (int f = 0; f < Frames; f++)
            {
                all[0, f] = Render(f, accessory);
                all[1, f] = (Bitmap)all[0, f].Clone();
                if (Mirrorable(f)) all[1, f].RotateFlip(RotateFlipType.RotateNoneFlipX);
            }
            return all;
        }
    }

    // Snowman (stages 0-4 = being built, 5-7 = melting) and the tug-of-war rope.
    static class SnowArt
    {
        public const int W = 48, H = 60, Stages = 8;   // 40x50 drawing, scaled up with the penguins
        const int SS = 4;

        public static Bitmap Render(int stage)
        {
            using (var big = new Bitmap(W * SS, H * SS, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(SS * Art.Scale, SS * Art.Scale);
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

        public const int FishW = 22, FishH = 12, BucketW = 34, BucketH = 30, HoleW = 26, HoleH = 11;

        // A small heart with a soft white outline (floating up from a group hug).
        public static Bitmap RenderHeart(int w, int h, Color c)
        {
            using (var big = new Bitmap(w * SS, h * SS, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(SS, SS);
                    BubbleArt.HeartAt(g, w / 2f, h / 2f, 1.32f, Color.FromArgb(235, 255, 255, 255));
                    BubbleArt.HeartAt(g, w / 2f, h / 2f + 0.2f, 1.05f, c);
                    using (var shine = new SolidBrush(Color.FromArgb(170, 255, 255, 255))) g.FillEllipse(shine, w / 2f - 3.6f, h / 2f - 3.6f, 2.2f, 1.6f);
                }
                return Downsample(big, w, h);
            }
        }

        // A tiny snowflake drifting down onto the huddle.
        public static Bitmap RenderFlake(int w, int h)
        {
            using (var big = new Bitmap(w * SS, h * SS, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(SS, SS);
                    float cx = w / 2f, cy = h / 2f, r = 4.6f;
                    using (var edge = new Pen(Color.FromArgb(150, 110, 150, 200), 2.4f))
                    using (var arm = new Pen(Color.White, 1.3f))
                    {
                        edge.StartCap = edge.EndCap = arm.StartCap = arm.EndCap = LineCap.Round;
                        foreach (var pen in new[] { edge, arm })
                            for (int k = 0; k < 3; k++)
                            {
                                double a = k * Math.PI / 3 + Math.PI / 2;
                                float dx = (float)Math.Cos(a) * r, dy = (float)Math.Sin(a) * r;
                                g.DrawLine(pen, cx - dx, cy - dy, cx + dx, cy + dy);
                            }
                    }
                }
                return Downsample(big, w, h);
            }
        }

        // A soft little cloud of warm breath.
        public static Bitmap RenderPuff(int w, int h)
        {
            using (var big = new Bitmap(w * SS, h * SS, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(SS, SS);
                    using (var b = new SolidBrush(Color.FromArgb(200, 245, 248, 255)))
                    {
                        g.FillEllipse(b, 1.5f, 4.5f, 6.5f, 6f);
                        g.FillEllipse(b, 4.5f, 2f, 7f, 7f);
                        g.FillEllipse(b, 7.5f, 5f, 5.5f, 5.5f);
                    }
                }
                return Downsample(big, w, h);
            }
        }

        // A little ice-fishing hole: 0 = just the hole, 1 = with the bobber, 2 = bobber dipping (ripples).
        public static Bitmap RenderHole(int kind)
        {
            using (var big = new Bitmap(HoleW * SS, HoleH * SS, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(SS, SS);
                    var rim = new RectangleF(0.6f, 1.2f, HoleW - 1.2f, HoleH - 2.4f);
                    using (var b = new LinearGradientBrush(rim, Color.FromArgb(250, 253, 255), Color.FromArgb(190, 215, 240), 90f)) g.FillEllipse(b, rim);
                    using (var edge = new Pen(Color.FromArgb(150, 185, 220), 0.7f)) g.DrawEllipse(edge, rim);
                    var water = new RectangleF(5, 3.4f, HoleW - 10, HoleH - 6.4f);
                    using (var b = new LinearGradientBrush(water, Color.FromArgb(18, 45, 95), Color.FromArgb(45, 100, 165), 90f)) g.FillEllipse(b, water);
                    using (var shine = new Pen(Color.FromArgb(150, 160, 210, 255), 0.6f)) g.DrawArc(shine, water.X + 2, water.Y + 0.8f, water.Width - 4, water.Height - 1.4f, 20, 140);
                    float cx = HoleW / 2f, cy = HoleH / 2f;
                    if (kind == 2)
                        using (var ring = new Pen(Color.FromArgb(200, 210, 235, 255), 0.6f))
                        {
                            g.DrawEllipse(ring, cx - 3.5f, cy - 1.3f, 7, 2.6f);
                            g.DrawEllipse(ring, cx - 6, cy - 2, 12, 4);
                        }
                    if (kind >= 1)
                    {   // red-and-white bobber (just its top showing when a fish nibbles)
                        float top = kind == 2 ? cy - 0.6f : cy - 2.6f, h = kind == 2 ? 1.6f : 3.4f;
                        using (var red = new SolidBrush(Color.FromArgb(230, 50, 50))) g.FillEllipse(red, cx - 1.7f, top, 3.4f, h + 0.6f);
                        if (kind == 1) using (var white = new SolidBrush(Color.White)) g.FillPie(white, cx - 1.7f, top, 3.4f, h + 0.6f, 0, 180);
                    }
                }
                return Downsample(big, HoleW, HoleH);
            }
        }

        // A fish flying through the air (thrown to someone at dinner).
        public static Bitmap RenderFish(bool right)
        {
            using (var big = new Bitmap(FishW * SS, FishH * SS, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(SS, SS);
                    if (!right) { g.TranslateTransform(FishW, 0); g.ScaleTransform(-1, 1); }
                    Art.Fish(g, FishW / 2f, FishH / 2f, 0, 20);
                }
                return Downsample(big, FishW, FishH);
            }
        }

        // Waddles' bucket, full of fish.
        public static Bitmap RenderBucket()
        {
            using (var big = new Bitmap(BucketW * SS, BucketH * SS, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(SS, SS);
                    Art.Fish(g, 11, 9, -115, 16);
                    Art.Fish(g, 23, 8, -65, 16);
                    Art.Fish(g, 17, 6, -92, 15);
                    var body = new[] { new PointF(4, 12), new PointF(30, 12), new PointF(27, 29), new PointF(7, 29) };
                    using (var b = new LinearGradientBrush(new RectangleF(4, 12, 26, 17), Color.FromArgb(130, 180, 228), Color.FromArgb(55, 95, 155), 0f))
                        g.FillPolygon(b, body);
                    using (var edge = new Pen(Color.FromArgb(35, 60, 100), 1.2f)) g.DrawPolygon(edge, body);
                    using (var hoop = new Pen(Color.FromArgb(225, 225, 235), 1.4f)) g.DrawLine(hoop, 5.6f, 21, 28.4f, 21);
                    using (var rim = new SolidBrush(Color.FromArgb(175, 210, 242))) g.FillRectangle(rim, 3, 11, 28, 3);
                    using (var edge = new Pen(Color.FromArgb(35, 60, 100), 0.9f)) g.DrawRectangle(edge, 3, 11, 28, 3);
                }
                return Downsample(big, BucketW, BucketH);
            }
        }

        public const int BallSize = 12;

        // A snowball for the snowball fight.
        public static Bitmap RenderBall()
        {
            using (var big = new Bitmap(BallSize * SS, BallSize * SS, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.ScaleTransform(SS, SS);
                    var r = new RectangleF(1, 1, BallSize - 2, BallSize - 2);
                    using (var b = new LinearGradientBrush(r, Color.White, Color.FromArgb(185, 205, 230), 60f)) g.FillEllipse(b, r);
                    using (var edge = new Pen(Color.FromArgb(120, 140, 170), 0.8f)) g.DrawEllipse(edge, r);
                }
                return Downsample(big, BallSize, BallSize);
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
        public const int Heart = 0, Bang = 1, Question = 2, Note = 3, Zzz = 4, Grr = 5,
                         Wrench = 6, Pillow = 7, Phones = 8, Star = 9, Hearts = 10, Cloud = 11, Fish = 12, Count = 13;

        public static void HeartAt(Graphics g, float cx, float cy, float s, Color c)
        {
            using (var b = new SolidBrush(c))
            {
                g.FillEllipse(b, cx - 4.8f * s, cy - 4.5f * s, 5.4f * s, 5.4f * s);
                g.FillEllipse(b, cx - 0.6f * s, cy - 4.5f * s, 5.4f * s, 5.4f * s);
                g.FillPolygon(b, new[] { new PointF(cx - 4.6f * s, cy - 0.9f * s), new PointF(cx + 4.6f * s, cy - 0.9f * s), new PointF(cx, cy + 4.7f * s) });
            }
        }
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
                        case Wrench:       // Wobble's fixing something
                        {
                            var st = g.Save();
                            g.TranslateTransform(12, 8.6f);
                            g.RotateTransform(40);
                            using (var b = new SolidBrush(Color.FromArgb(110, 120, 138)))
                            {
                                g.FillRectangle(b, -1.3f, -5.5f, 2.6f, 8.5f);
                                g.FillRectangle(b, -3.8f, 2.4f, 7.6f, 2.3f);
                                g.FillRectangle(b, -3.8f, 2.4f, 2.3f, 4.6f);
                                g.FillRectangle(b, 1.5f, 2.4f, 2.3f, 4.6f);
                                g.FillEllipse(b, -2.3f, -7.6f, 4.6f, 4.6f);
                            }
                            g.Restore(st);
                            break;
                        }
                        case Pillow:       // Pebble's bedtime
                            using (var path = RoundRect(new RectangleF(4.5f, 4.4f, 15, 9), 3.6f))
                            using (var b = new LinearGradientBrush(new RectangleF(4, 4, 16, 10), Color.FromArgb(240, 244, 255), Color.FromArgb(160, 178, 232), 90f))
                            using (var edge = new Pen(Color.FromArgb(95, 110, 175), 1f))
                            { g.FillPath(b, path); g.DrawPath(edge, path); }
                            using (var dot = new SolidBrush(Color.FromArgb(115, 130, 200))) g.FillEllipse(dot, 11.2f, 8.1f, 1.6f, 1.6f);
                            break;
                        case Phones:       // Ziggy's own tunes
                            using (var band = new Pen(Color.FromArgb(70, 70, 82), 1.7f)) g.DrawArc(band, 6.3f, 3.4f, 11.4f, 11, 185, 170);
                            using (var b = new SolidBrush(Color.FromArgb(235, 70, 150)))
                            { g.FillEllipse(b, 4.6f, 7.4f, 4.4f, 6.2f); g.FillEllipse(b, 15f, 7.4f, 4.4f, 6.2f); }
                            break;
                        case Star:         // Puddle: ta-da!
                        {
                            var pts = new PointF[10];
                            for (int k = 0; k < 10; k++)
                            {
                                double t = -Math.PI / 2 + k * Math.PI / 5;
                                float r = k % 2 == 0 ? 6.4f : 2.7f;
                                pts[k] = new PointF(12 + (float)Math.Cos(t) * r, 9 + (float)Math.Sin(t) * r);
                            }
                            using (var b = new SolidBrush(Color.FromArgb(255, 200, 30))) g.FillPolygon(b, pts);
                            using (var edge = new Pen(Color.FromArgb(200, 130, 0), 0.8f)) g.DrawPolygon(edge, pts);
                            break;
                        }
                        case Hearts:       // Waddles, full of love
                            HeartAt(g, 8.6f, 6.6f, 0.62f, Color.FromArgb(255, 130, 165));
                            HeartAt(g, 14.4f, 8.8f, 0.8f, Color.FromArgb(232, 60, 85));
                            break;
                        case Fish:         // gone fishing
                            Art.Fish(g, 12, 8.8f, 0, 15);
                            break;
                        case Cloud:        // grumpy
                            using (var b = new SolidBrush(Color.FromArgb(120, 128, 145)))
                            {
                                g.FillEllipse(b, 5, 6, 7, 6);
                                g.FillEllipse(b, 9, 3.4f, 7.5f, 7.5f);
                                g.FillEllipse(b, 13.5f, 6, 6, 5.5f);
                                g.FillRectangle(b, 7.5f, 8.5f, 10, 3.4f);
                            }
                            using (var rain = new Pen(Color.FromArgb(70, 130, 220), 1.1f))
                            {
                                g.DrawLine(rain, 9, 12.8f, 8.2f, 15);
                                g.DrawLine(rain, 12.5f, 12.8f, 11.7f, 15);
                                g.DrawLine(rain, 16, 12.8f, 15.2f, 15);
                            }
                            break;
                        default:
                            string text = icon == Bang ? "!" : icon == Question ? "?" : icon == Grr ? "grr" : "zzz";
                            Color col = icon == Bang ? Color.FromArgb(225, 90, 20)
                                      : icon == Question ? Color.FromArgb(40, 110, 210)
                                      : icon == Grr ? Color.FromArgb(205, 40, 40) : Color.FromArgb(70, 120, 190);
                            float size = icon == Zzz || icon == Grr ? 8.5f : 11f;
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

        // A dark rounded tag with the penguin's name in white; the leader's is gold, with a star.
        // friend / rival, if given, go on a small second line: "♥ Pebble  grr Ziggy".
        public static Bitmap RenderTag(string name, bool leader = false, string friend = null, string rival = null)
        {
            if (leader) name = "★ " + name;
            var ink = leader ? Color.FromArgb(255, 214, 70) : Color.White;
            string fs = friend != null ? "♥ " + friend : null, rs = rival != null ? "grr " + rival : null;
            using (var f = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var small = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                SizeF sz, fsz = SizeF.Empty, rsz = SizeF.Empty;
                using (var tmp = new Bitmap(1, 1)) using (var mg = Graphics.FromImage(tmp))
                {
                    sz = mg.MeasureString(name, f);
                    if (fs != null) fsz = mg.MeasureString(fs, small);
                    if (rs != null) rsz = mg.MeasureString(rs, small);
                }
                bool sub = fs != null || rs != null;
                float subW = fsz.Width + rsz.Width + (fs != null && rs != null ? 4 : 0);
                int w = (int)Math.Ceiling(Math.Max(sz.Width, subW)) + 10, h = sub ? 30 : 18;
                var b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(b))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    using (var path = RoundRect(new RectangleF(0.5f, 0.5f, w - 1, h - 1), 8))
                    using (var bg = new SolidBrush(Color.FromArgb(225, 28, 32, 44)))
                    using (var edge = new Pen(leader ? Color.FromArgb(230, 255, 200, 60) : Color.FromArgb(200, 255, 255, 255), leader ? 1.5f : 1f))
                    {
                        g.FillPath(bg, path);
                        g.DrawPath(edge, path);
                    }
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        using (var inkB = new SolidBrush(ink)) g.DrawString(name, f, inkB, new RectangleF(0, 0, w, 19), sf);
                    if (sub)
                    {
                        float x = (w - subW) / 2f;
                        if (fs != null)
                        {
                            using (var pink = new SolidBrush(Color.FromArgb(255, 140, 170))) g.DrawString(fs, small, pink, x, 15);
                            x += fsz.Width + 4;
                        }
                        if (rs != null)
                            using (var red = new SolidBrush(Color.FromArgb(255, 120, 90))) g.DrawString(rs, small, red, x, 15);
                    }
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

        public void Draw(IntPtr screenDC, IntPtr memDC, IntPtr hbm, int x, int y, byte alpha = 255)
        {
            var old = Native.SelectObject(memDC, hbm);
            var pos = new Native.POINT { X = x, Y = y };
            var size = new Native.SIZE { cx = wd, cy = ht };
            var src = new Native.POINT();
            var blend = new Native.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
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

    // ---------------------------------------------------------------- family

    // Who gets on with whom among the core six. Fixed: these don't change.
    static class Family
    {
        // first listed = shown on the name tag
        static readonly string[][] FriendPairs = {
            new[] { "Waddles", "Pebble" },     // best friends
            new[] { "Ziggy", "Pip" },          // big brother, little brother
            new[] { "Wobble", "Puddle" },      // Wobble admires Puddle (and copies her slides)
            new[] { "Waddles", "Pip" },        // Waddles looks out for the little one
            new[] { "Juniper", "Pearl" },      // next door: Juniper and the littlest Frostbite
            new[] { "Bubbles", "Bolt" },       // the captain and his first mate
            new[] { "Doodle", "Mochi" },       // the artist and her favourite (well-fed) model
        };
        static readonly string[][] RivalPairs = {
            new[] { "Puddle", "Ziggy" },       // the two fast ones
            new[] { "Pebble", "Ziggy" },       // Ziggy keeps waking Pebble up
        };

        static bool In(string[][] pairs, string a, string b)
        {
            foreach (var pr in pairs) if ((pr[0] == a && pr[1] == b) || (pr[0] == b && pr[1] == a)) return true;
            return false;
        }
        static List<string> Of(string[][] pairs, string name)
        {
            var list = new List<string>();
            foreach (var pr in pairs) { if (pr[0] == name) list.Add(pr[1]); else if (pr[1] == name) list.Add(pr[0]); }
            return list;
        }

        public static bool AreFriends(string a, string b) { return In(FriendPairs, a, b); }
        public static bool AreRivals(string a, string b) { return In(RivalPairs, a, b); }
        public static bool Admires(string fan, string star) { return fan == "Wobble" && star == "Puddle"; }
        public static List<string> FriendsOf(string name) { return Of(FriendPairs, name); }
        public static List<string> RivalsOf(string name) { return Of(RivalPairs, name); }
        public static string BestFriend(string name) { var l = FriendsOf(name); return l.Count > 0 ? l[0] : null; }
        public static string Rival(string name) { var l = RivalsOf(name); return l.Count > 0 ? l[0] : null; }

        public static string Label(string name, string other)
        {
            if ((name == "Waddles" && other == "Pebble") || (name == "Pebble" && other == "Waddles")) return "best friend";
            if (name == "Ziggy" && other == "Pip") return "little brother";
            if (name == "Pip" && other == "Ziggy") return "big brother";
            if (name == "Wobble" && other == "Puddle") return "hero";
            if (name == "Puddle" && other == "Wobble") return "biggest fan";
            return null;
        }
    }

    // A little window listing who's friends with whom.
    class ChartForm : Form
    {
        readonly string[] names;

        public ChartForm(string[] coreNames)
        {
            names = coreNames;
            Text = "Penguin friendships";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(430, 36 + names.Length * 34);
            BackColor = Color.FromArgb(28, 32, 44);
            DoubleBuffered = true;
            TopMost = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var head = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (var nameF = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var body = new Font("Segoe UI", 9.5f))
            using (var white = new SolidBrush(Color.White))
            using (var gold = new SolidBrush(Color.FromArgb(255, 214, 70)))
            using (var pink = new SolidBrush(Color.FromArgb(255, 140, 170)))
            using (var red = new SolidBrush(Color.FromArgb(255, 120, 90)))
            using (var dim = new SolidBrush(Color.FromArgb(150, 160, 180)))
            {
                g.DrawString("Who gets on with whom", head, white, 14, 8);
                float y = 38;
                foreach (var n in names)
                {
                    g.DrawString(n == "Ziggy" ? "★ Ziggy" : n, nameF, n == "Ziggy" ? gold : white, 14, y);
                    float x = 104;
                    foreach (var f in Family.FriendsOf(n))
                    {
                        string lab = Family.Label(n, f);
                        string t = "♥ " + f + (lab != null ? " (" + lab + ")" : "");
                        g.DrawString(t, body, pink, x, y + 1);
                        x += g.MeasureString(t, body).Width + 6;
                    }
                    foreach (var r in Family.RivalsOf(n))
                    {
                        string t = "grr " + r;
                        g.DrawString(t, body, red, x, y + 1);
                        x += g.MeasureString(t, body).Width + 6;
                    }
                    if (x == 104) g.DrawString("-", body, dim, x, y + 1);
                    y += 34;
                }
            }
        }
    }

    // ---------------------------------------------------------------- state

    // How a penguin tends to behave. The core five each have their own; everyone else is Ordinary.
    class Personality
    {
        public string Trait;
        // chance weights for: look, wave, sleep, slide, dance, hop, fly
        public int[] Weights = { 20, 15, 12, 18, 12, 16, 7 };
        public float SpeedMin = 1.2f, SpeedRange = 1.0f;
        public int Restless = 240;          // 1-in-N chance per walking tick to start something
        public float SlideMin = 5f;         // belly-slide launch speed
        public int ClimbPct = 65;           // chance to climb a window it walks into
        public int TripOdds;                // 1-in-N chance per walking tick to trip over (0 = never)
        public int CuriousTicks = 16;       // how long the cursor must linger before it stops to look
        public int GreetRange = 31;         // how close another penguin must be to say hi
        public int PetHearts = 60;          // ticks between hearts while petted
        public int PetLimit;                // after this long being petted: PetEnd (0 = enjoys it forever)
        public int PetEnd = -1;
        public int Accessory = Art.NoOutfit;
        public bool Leader;                 // leads parades, snowmen and tug of war
        public bool Small;                  // drawn smaller (shorter head for stacking)
        public bool TagAlong;               // follows the big ones around and copies them

        public static readonly Personality Ordinary = new Personality { Trait = "ordinary" };

        public static Personality For(string name)
        {
            switch (name)
            {
                case "Pebble": return new Personality {   // sleepy: naps a lot, ambles, dozes off when petted
                    Trait = "sleepy", Weights = new[] { 20, 8, 42, 6, 4, 10, 2 }, SpeedMin = 0.8f, SpeedRange = 0.5f,
                    Restless = 300, ClimbPct = 35, PetLimit = 220, PetEnd = PenguinApp.SLEEP, Accessory = Art.Nightcap };
                case "Waddles": return new Personality {  // friendly: waves at everyone, notices you fast, loves pets
                    Trait = "friendly", Weights = new[] { 20, 38, 8, 10, 12, 10, 2 }, SpeedMin = 1.0f, SpeedRange = 0.6f,
                    CuriousTicks = 6, GreetRange = 70, PetHearts = 35, Accessory = Art.Scarf };
                case "Wobble": return new Personality {   // clumsy: trips over nothing, fumbles climbs (hence the hard hat)
                    Trait = "clumsy", Weights = new[] { 28, 14, 10, 18, 14, 14, 2 }, SpeedMin = 1.0f, SpeedRange = 0.8f,
                    ClimbPct = 30, TripOdds = 900, Accessory = Art.HardHat };
                case "Puddle": return new Personality {   // slider: belly-slides everywhere, fast
                    Trait = "slippery", Weights = new[] { 12, 10, 8, 52, 6, 10, 2 }, SlideMin = 7f,
                    Restless = 170, Accessory = Art.Goggles };
                case "Pip": return new Personality {      // the little brother: small, quick little steps, tags along and copies the big ones
                    Trait = "little brother", Weights = new[] { 18, 22, 8, 14, 14, 22, 2 }, SpeedMin = 1.6f, SpeedRange = 0.6f,
                    CuriousTicks = 8, GreetRange = 48, PetHearts = 40, TripOdds = 2500, ClimbPct = 50,
                    Small = true, TagAlong = true, Accessory = Art.Propeller };
                // ---- the Frostbites, next door
                case "Bubbles": return new Personality {  // the captain: brisk march, keeps watch from up high, no time for cuddles
                    Trait = "captain", Weights = new[] { 34, 8, 5, 8, 4, 12, 9 }, SpeedMin = 1.5f, SpeedRange = 0.4f,
                    Restless = 190, ClimbPct = 90, CuriousTicks = 24, PetLimit = 90, PetEnd = PenguinApp.WALK, Accessory = Art.SailorCap };
                case "Juniper": return new Personality {  // the gentle one: knits, waves, slow and steady, loves a cuddle
                    Trait = "motherly", Weights = new[] { 26, 30, 14, 3, 8, 6, 2 }, SpeedMin = 0.8f, SpeedRange = 0.4f,
                    ClimbPct = 25, CuriousTicks = 8, GreetRange = 65, PetHearts = 35, Accessory = Art.Shawl };
                case "Bolt": return new Personality {     // sporty show-off: fast, slides and hops, always warming up
                    Trait = "sporty", Weights = new[] { 10, 10, 3, 34, 8, 28, 7 }, SpeedMin = 2.0f, SpeedRange = 0.8f,
                    Restless = 140, SlideMin = 6.5f, ClimbPct = 80, Accessory = Art.Sweatband };
                case "Doodle": return new Personality {   // the artist: watches everything, aloof... "hmph" if you pet her too long
                    Trait = "artsy", Weights = new[] { 38, 6, 10, 8, 14, 6, 3 }, SpeedMin = 1.0f, SpeedRange = 0.5f,
                    CuriousTicks = 30, PetLimit = 130, PetEnd = PenguinApp.LOOK, Accessory = Art.Beret };
                case "Mochi": return new Personality {    // the foodie: snacks, naps, in no hurry
                    Trait = "foodie", Weights = new[] { 22, 14, 28, 8, 10, 8, 2 }, SpeedMin = 0.9f, SpeedRange = 0.5f,
                    Restless = 260, ClimbPct = 40, PetHearts = 40, Accessory = Art.Bow };
                case "Pearl": return new Personality {    // the youngest: small, bouncy, tags along after her family
                    Trait = "little sister", Weights = new[] { 18, 24, 8, 14, 16, 20, 2 }, SpeedMin = 1.5f, SpeedRange = 0.5f,
                    CuriousTicks = 8, GreetRange = 48, PetHearts = 40, TripOdds = 2500, ClimbPct = 50,
                    Small = true, TagAlong = true, Accessory = Art.EggShell };
                case "Ziggy": return new Personality {    // the leader, and hyper: dances, hops, flies, hurries; too busy for long pets
                    Trait = "leader", Leader = true, Weights = new[] { 8, 10, 2, 12, 32, 26, 10 }, SpeedMin = 2.0f, SpeedRange = 0.9f,
                    Restless = 130, ClimbPct = 90, PetLimit = 110, PetEnd = PenguinApp.DANCE, Accessory = Art.Headphones };
            }
            return Ordinary;
        }
    }

    class Penguin
    {
        public Personality Me = Personality.Ordinary;
        public int PetTicks;                  // how long the cursor has rested on this penguin
        public int ThrowCool, ThrowAnim, HitTimer;   // snowball fight
        public float HopFromX, HopFromY;      // tower: where the hop up started
        public Penguin HopOnto;               // tower: whose head it is hopping onto
        public float SummonY;                 // called to an activity: the floor it flies down to
        public int RideFor;                   // piggyback: how long to ride once up (0 = until told to get down)
        public Penguin HelpedBy;              // Pip: the big sibling coming to help it up
        public Penguin Buddy;                 // walking together: the friend it keeps step with
        public Penguin CatchWho, Catcher;     // the catch: who it is running to catch / who is coming to catch it
        public bool PendingSlide, SlideFail;  // copying a slide (after a short look), and botching it
        public int SlideDir, NapUntil;        // which way to slide; until when someone is already napping beside it
        public Penguin Target;                // ambush: the rival it's throwing at
        public bool Pest;                     // hopped onto a rival's head uninvited
        public int GlareUntil;                // you flung its friend: it glares at your cursor until then
        public int PetTotal;                  // how much you've petted it this session
        public Penguin MakeUpWith;            // after sulking: the sibling who comes to make up
        public bool LaughNext;                // hit by a snowball: laughs it off once the stars clear
        public bool Returning;                // just dropped back in: the family says hello when it lands
        public bool WaveAfter;                // waking from bedtime: wave at you after the yawn
        public int Fish, EatT;                // dinner: fish still to eat, and how far through this one
        public bool Dozing;                   // fell asleep at the dinner table
        public int PileAt;                    // parade pile-up: when it crashed into the one in front (0 = still up)
        public float PrevX;                   // where it was at the start of this tick (did it walk?)
        public int PoseKind;                  // screenshot: 0 wave, 1 ta-da
        public int GogglesUntil;              // Puddle: goggles down over the eyes until then
        public bool HatAskew;                 // Wobble: hard hat knocked crooked
        public bool Pillow;                   // Pebble: asleep on his pillow
        public string CopyName;               // Pip: whose idle habit he's copying
        public bool Tiptoe;                   // Pip: tiptoeing past someone asleep
        public int Juggle;                    // Wobble: fumbling the fish he was thrown
        public bool FixWall;                  // Wobble: walked into a wall (might fix it)
        public List<Penguin> Visits;          // Waddles: who's still to say hi to
        public bool HoldPet;                  // waiting, and being patted / given a scarf
        public int Attn;                      // inspection pose: 0 attention, 1 salute, 2 dozing, 3 posing, 4 waving
        public FishHole Hole;                 // ice fishing: its hole in the ice
        public int FishPhase, FishT, Catches; // 0 chipping the hole, 1 waiting, 2 a bite!, 3 caught one
        public bool FishNap;                  // nodded off over the rod
        public bool HugNap, Wobbly;           // group hug: fell asleep in it / losing balance in a squeeze
        public int Shiver;                    // family huddle: brrr, still cold
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
        public int TagW, TagH = 18, TagX = int.MinValue, TagY;
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

    class Snowball
    {
        public float X, Y, Vx, Vy;
        public Penguin Target, From;        // an ambush: who it's meant for, and who threw it
        public bool FromLeft;
        public int Life, Flight;            // dinner fish: ticks flown / ticks to arrive
        public bool Right;
        public SpriteWindow Win;
    }

    // Two teams lobbing snowballs at each other; the team with more hits wins.
    class Fight
    {
        public List<Penguin> Left = new List<Penguin>(), Right = new List<Penguin>();
        public List<Snowball> Balls = new List<Snowball>();
        public int Timer, HitsL, HitsR;
        public List<Penguin> All() { var a = new List<Penguin>(Left); a.AddRange(Right); return a; }
    }

    // Penguins hop onto each other's heads, Ziggy on top, until it wobbles and topples.
    class Tower
    {
        public List<Penguin> Crew;          // [0] is the base, the rest climb in order
        public float Site;
        public int Built, Phase, Timer;     // Built = penguins in the stack so far
    }

    // Everyone dances in time; Ziggy DJs if he's there.
    class Party
    {
        public List<Penguin> Crew;
        public Penguin Dj;
        public int Timer;
    }

    // Two rivals race to the edge of the screen; their friends cheer.
    class Race
    {
        public Penguin A, B;
        public int Dir, Phase, Timer;
        public float Finish;
        public List<Penguin> Fans = new List<Penguin>();
    }

    // After a team game: everyone lines up, high-fives down the line, and cheers together.
    class GoodGame { public List<Penguin> Crew; public int Phase, Timer, T; }

    // Keeping warm like real emperor penguins: a tight cluster that slowly shuffles, Pip in the middle.
    class Huddle { public List<Penguin> Order; public float Center; public int Timer, T, ShakeOff; public bool FromLeft; public Penguin Napper; }

    // Everyone lines up for a family photo. Flash!
    class Photo
    {
        public List<Penguin> Crew;
        public int Phase, Timer;
        public bool PebbleNaps;
        public SpriteWindow Flash;
        public IntPtr FlashBmp;
        public int FX, FY;
    }

    // A hole in the ice for fishing (drawn in its own little window on the floor).
    class FishHole
    {
        public SpriteWindow Win;
        public int Key = -1, X, Y, A = -1;
    }

    // Ziggy inspects the troops: everyone lines up at attention and he walks down the line.
    class Inspection
    {
        public Penguin Chief;
        public List<Penguin> Line = new List<Penguin>(), Todo;
        public int Dir, Phase, T, Age;
        public bool Walking;
    }

    // Waddles calls everyone to dinner and hands out fish. Ziggy gives his to Pip; Pebble nods off mid-meal.
    class Dinner
    {
        public List<Penguin> Crew;           // seated left to right, facing the cook
        public Penguin Cook;
        public int Phase, Timer, Serve, T, Age;
        public bool PebbleNaps, Shared;
        public List<Snowball> Fish = new List<Snowball>();
        public SpriteWindow Bucket;
        public float BX;
        public int BY;
    }

    // A media player is open: everyone sits in a row and watches, munching fish.
    class Movie
    {
        public List<Penguin> Crew;
        public IntPtr Win;
        public int Age, LastSeen;
        public bool Browser;                // a video site in the browser (rather than a media player)
    }

    // Lights out: everyone snuggles up in a row. Waddles tucks Pip in, Ziggy keeps watch for a bit.
    class Bedtime { public List<Penguin> Crew; public bool Idle; public int Start; }

    // Pet one penguin long enough and everyone comes over for a cuddle.
    class Hug
    {
        public Penguin Center;
        public List<Penguin> Crew;
        public int T, Squeeze;              // how long it's gone on; a squeeze in progress
        public bool Auto;                   // started by itself (not by petting): the middle one hugs too, and it ends on its own
    }

    // A little heart floating up from a group hug.
    class Floaty
    {
        public SpriteWindow Win;
        public float X0, Y, Phase, Vx, Vy, Grav;
        public int Life, Max, Kind, Key = -1;       // Kind: 0/1 heart, 2 snowflake, 3 warm breath
    }

    // Follow the leader: everyone marches in a line behind Ziggy.
    class Parade
    {
        public Penguin Leader;
        public List<Penguin> Line = new List<Penguin>();
        public int Timer;
        public bool Kid;                    // Pip trying to lead (only Waddles plays along)
        public int Stunt, StuntT, Stunts, Done, Leaps;   // something exciting on the way: 1 slide train, 2 leapfrog, 3 pile-up
        public Penguin Jumper;              // leapfrog: whose turn it is
        public int LeapT, LeapWait;
        public int StuntCool = 160 + new Random().Next(120);
    }

    class WinInfo { public IntPtr H; public Rectangle R; }

    // Movement history of a window that penguins are on (for riding along / shake detection).
    class Track { public Rectangle R; public int SX, SY, Rev, RevTimer, MoveTimer; }

    // ---------------------------------------------------------------- simulation + tray

    class PenguinApp : ApplicationContext
    {
        public const int FALL = 0, WALK = 1, LOOK = 2, FLY = 3, WAVE = 4, SLEEP = 5, SLIDE = 6, DANCE = 7,
                  HELD = 8, THROWN = 9, CURIOUS = 10, GREET = 11, RIDE = 12, CLIMB = 13, OFF = 14,
                  GOTO = 15, BUILD = 16, ADMIRE = 17, READY = 18, TUG = 19, WATCH = 20, YAWN = 21, SWAY = 22,
                  FACEPLANT = 23, DAZED = 24, PET = 25, FOLLOW = 26, FIGHT = 27, HOPUP = 28, PARTY = 29, SUMMON = 30, HELP = 31, HUG = 32, HIFIVE = 33, TOGETHER = 34, NAPWITH = 35, CATCH = 36,
                  RACE = 37, CHEER = 38, STARE = 39, BLINK = 40, SULK = 41, AMBUSH = 42, SHAKEOFF = 43, SHAKEHANDS = 44, GUARD = 45, JEALOUS = 46,
                  TEASE = 47, MAKEUP = 48, GG = 49, HUDDLE = 50, PHOTO = 51, BED = 52, TUCK = 53, BEDGUARD = 54, DINE = 55, COOK = 56, GROOVE = 57, MOVIE = 58, PEEK = 59, POSE = 60,
                  GOGGLE = 61, HATFIX = 62, SLEEPWALK = 63, LISTEN = 64, IDLE = 65,
                  PILLOW = 66, GRUMP = 67, SCARF = 68, VISIT = 69, REPAIR = 70, SHOWOFF = 71, CLAP = 72,
                  ATTN = 74, INSPECT = 75, CHECKPIP = 76, SNEAK = 77, STARTLE = 78, HOLD = 79, ICEFISH = 80, FISHWATCH = 81;
        const int W = Art.W, H = Art.H;
        const int ShooTicks = 2000;    // ~60 s

        readonly IntPtr[,,] hbm = new IntPtr[Art.Outfits, 2, Art.Frames];
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
        // The core six: always on screen, always these names (in this order).
        static readonly string[] CoreNames = { "Pebble", "Waddles", "Wobble", "Puddle", "Ziggy", "Pip" };
        // The Frostbites: the rival family next door, living on the right-hand side (tray toggle).
        static readonly string[] FrostNames = { "Bubbles", "Juniper", "Bolt", "Doodle", "Mochi", "Pearl" };
        public const int MinPenguins = 6;
        static readonly string[] RandomPool = Array.FindAll(NamePool, n => Array.IndexOf(CoreNames, n) < 0 && Array.IndexOf(FrostNames, n) < 0);
        bool frostOn = true;
        const string SettingsKey = @"Software\DesktopPenguins";
        readonly List<string> unusedNames = new List<string>(RandomPool);
        bool hiddenFs;

        // snowman & tug of war
        readonly IntPtr[] snowHbm = new IntPtr[SnowArt.Stages];
        Snowman snow;
        Tug tug;
        Parade parade;
        Fight fight;
        Tower tower;
        Party party;
        Hug hug;
        Race race;
        GoodGame gg;
        Huddle huddle;
        Photo photo;
        Bedtime bedtime;
        Dinner dinner;
        Movie movie;
        int movieCool;
        bool music;                         // something's playing: dance!
        Penguin dj;
        readonly bool[] loud = new bool[90];
        int loudI, lastBeat, grooveBeat, lastPose = -1000, lastPeek;
        float slowLevel;
        int loudCount, lastLoudTick, lastPlayingTick;
        bool videoFg;                       // the active window is a video: watch it rather than dance
        readonly HashSet<IntPtr> knownWins = new HashSet<IntPtr>();
        readonly Dictionary<uint, string> procNames = new Dictionary<uint, string>();
        readonly object peekJob = new object();
        readonly uint myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        int lastDinner = -1000000;
        readonly IntPtr[] fishHbm = new IntPtr[2];
        readonly IntPtr[] holeHbm = new IntPtr[3];
        readonly IntPtr[] floatHbm = new IntPtr[4];
        readonly List<Floaty> floaties = new List<Floaty>();
        const int FloatW = 14, FloatH = 13;
        IntPtr bucketHbm;
        List<Penguin> ggPending;            // a team game just ended: cheer once the dust settles
        int ggAt, lastWelcome, lastNightBed = -1000000;
        readonly List<int> activityBag = new List<int>();   // shuffled turns for the automatic activities
        int lastActivity = -1;
        int lastBusy;                       // the last tick a group activity was going on
        const int FreeTime = 2000;          // ~60 s of free time between group activities
        bool scarfOnPip;                    // Waddles has lent Pip her scarf
        int scarfSince = -100000;
        Inspection inspect;
        bool awayLong, idleBedDone;
        Penguin favourite;                  // the one you've petted most this session
        readonly List<Snowball> strays = new List<Snowball>();   // ambush snowballs, outside any fight
        ChartForm chart;
        IntPtr ballHbm;

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
            frostOn = LoadFrostOn();
            Bitmap[,] frames = null;
            for (int o = 0; o < Art.BaseOutfits; o++)
            {
                var set = Art.RenderAll(o);
                for (int d = 0; d < 2; d++)
                    for (int f = 0; f < Art.Frames; f++)
                        hbm[o, d, f] = Art.ToHBitmap(set[d, f]);
                if (o == Art.NoOutfit) frames = set;
                else foreach (var b in set) b.Dispose();
            }
            for (int s = 0; s < SnowArt.Stages; s++)
                using (var b = SnowArt.Render(s)) snowHbm[s] = Art.ToHBitmap(b);
            using (var b = SnowArt.RenderBall()) ballHbm = Art.ToHBitmap(b);
            for (int i = 0; i < 2; i++)
                using (var b = SnowArt.RenderFish(i == 1)) fishHbm[i] = Art.ToHBitmap(b);
            for (int i = 0; i < holeHbm.Length; i++)
                using (var b = SnowArt.RenderHole(i)) holeHbm[i] = Art.ToHBitmap(b);
            for (int i = 0; i < floatHbm.Length; i++)
                using (var b = i == 2 ? SnowArt.RenderFlake(FloatW, FloatH) : i == 3 ? SnowArt.RenderPuff(FloatW, FloatH)
                             : SnowArt.RenderHeart(FloatW, FloatH, i == 0 ? Color.FromArgb(240, 60, 90) : Color.FromArgb(255, 135, 170)))
                    floatHbm[i] = Art.ToHBitmap(b);
            using (var b = SnowArt.RenderBucket()) bucketHbm = Art.ToHBitmap(b);
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
            menu.Items.Add("Remove a penguin", null, delegate { target = Math.Max(target - 1, MinPenguins); });
            var followItem = new ToolStripMenuItem("Follow the mouse");
            followItem.CheckOnClick = true;
            followItem.CheckedChanged += delegate { follow = followItem.Checked; };
            menu.Items.Add(followItem);
            var startItem = new ToolStripMenuItem("Start with Windows");
            startItem.Checked = StartsWithWindows();
            startItem.CheckOnClick = true;
            startItem.CheckedChanged += delegate
            {
                try { SetStartWithWindows(startItem.Checked); }
                catch { startItem.Checked = StartsWithWindows(); }   // couldn't write the setting: show what's really there
            };
            menu.Items.Add(startItem);
            var frostItem = new ToolStripMenuItem("The Frostbites (neighbours)");
            frostItem.Checked = frostOn;
            frostItem.CheckOnClick = true;
            frostItem.CheckedChanged += delegate { frostOn = frostItem.Checked; SaveFrostOn(frostOn); };
            menu.Items.Add(frostItem);
            menu.Items.Add("Build a snowman", null, delegate { StartSnowman(); });
            menu.Items.Add("Tug of war!", null, delegate { StartTug(); });
            menu.Items.Add("Snowball fight!", null, delegate { StartFight(); });
            menu.Items.Add("Penguin tower!", null, delegate { StartTower(); });
            menu.Items.Add("Dance party!", null, delegate { StartParty(); });
            menu.Items.Add("Follow Ziggy!", null, delegate { StartParade(); });
            menu.Items.Add("Family huddle", null, delegate { StartHuddle(); });
            menu.Items.Add("Group hug!", null, delegate { StartGroupHug(); });
            menu.Items.Add("Family photo", null, delegate { StartPhoto(); });
            menu.Items.Add("Family dinner", null, delegate { StartDinner(); });
            menu.Items.Add("Bedtime", null, delegate { StartBedtime(false); });
            menu.Items.Add("Friendship chart", null, delegate
            {
                if (chart == null || chart.IsDisposed) chart = new ChartForm(CoreNames);
                chart.Show(); chart.Activate();
            });
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

        // A value under HKCU\...\Run starts the app when you sign in to Windows.
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunName = "DesktopPenguins";

        static bool StartsWithWindows()
        {
            using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunName) != null;
        }

        static void SetStartWithWindows(bool on)
        {
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(RunName, false);
            }
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
            EndFight(null);
            ClearStrays();
            EndPhoto();
            EndDinner();
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
                case FACEPLANT: case DAZED: case PET: case FOLLOW: case FIGHT: case PARTY: case HELP: case HUG:
                case HIFIVE: case TOGETHER: case NAPWITH: case CATCH:
                case RACE: case CHEER: case STARE: case BLINK: case SULK: case AMBUSH: case SHAKEOFF: case SHAKEHANDS:
                case GUARD: case JEALOUS:
                case TEASE: case MAKEUP: case GG: case HUDDLE: case PHOTO: case BED: case TUCK: case BEDGUARD:
                case DINE: case COOK: case GROOVE: case MOVIE: case PEEK: case POSE:
                case GOGGLE: case HATFIX: case SLEEPWALK: case LISTEN: case IDLE:
                case PILLOW: case GRUMP: case SCARF: case VISIT: case REPAIR: case SHOWOFF: case CLAP:
                case ATTN: case INSPECT: case CHECKPIP: case SNEAK: case STARTLE: case HOLD: case ICEFISH: case FISHWATCH:
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
                case DAZED: case PET: case FOLLOW: case FIGHT: case PARTY: case HELP: case HUG:
                case HIFIVE: case TOGETHER: case CATCH:
                case RACE: case CHEER: case STARE: case BLINK: case SULK: case AMBUSH: case SHAKEOFF: case SHAKEHANDS:
                case GUARD: case JEALOUS:
                case TEASE: case MAKEUP: case GG: case HUDDLE: case PHOTO: case BED: case TUCK: case BEDGUARD:
                case DINE: case COOK: case GROOVE: case MOVIE: case PEEK: case POSE:
                case GOGGLE: case HATFIX: case SLEEPWALK: case LISTEN: case IDLE:
                case PILLOW: case GRUMP: case SCARF: case VISIT: case REPAIR: case SHOWOFF: case CLAP:
                case ATTN: case INSPECT: case CHECKPIP: case SNEAK: case STARTLE: case HOLD: case ICEFISH: case FISHWATCH:
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

        void Spawn(string name = null)
        {
            var p = new Penguin();
            string core = Array.Find(CoreNames, n => Core(n) == null);
            if (name != null) p.Name = name;
            else if (core != null) p.Name = core;
            else
            {
                if (unusedNames.Count == 0) unusedNames.AddRange(RandomPool);
                int ni = rnd.Next(unusedNames.Count);
                p.Name = unusedNames[ni];
                unusedNames.RemoveAt(ni);
            }
            p.Me = Personality.For(p.Name);
            Reset(p);
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

        void RemovePenguin(Penguin last)
        {
            penguins.Remove(last);
            Detach(last);
            if (last.Rider != null) { last.Rider.Base = null; last.Rider = null; }
            foreach (var q in penguins)
            {   // let go of anyone who was busy with it
                if (q.Base == last) q.Base = null;
                if (q.Job == last) { q.Job = null; q.State = WALK; q.Tick = 0; }
            }
            ClosePenguinWindows(last);
            if (Array.IndexOf(CoreNames, last.Name) < 0 && Array.IndexOf(FrostNames, last.Name) < 0) unusedNames.Add(last.Name);
        }

        void ClosePenguinWindows(Penguin p)
        {
            CloseHole(p);
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
                    using (var b = BubbleArt.RenderTag(p.Name, p.Me.Leader, Family.BestFriend(p.Name), Family.Rival(p.Name)))
                    { p.TagBmp = Art.ToHBitmap(b); p.TagW = b.Width; p.TagH = b.Height; }
                    p.TagWin = new SpriteWindow(p.TagW, p.TagH, true);
                    p.TagWin.Show();
                }
                int tx = headX - p.TagW / 2, ty = headTop - p.TagH - 4;
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
                int bx = headX + 6, by = headTop - BubbleArt.H - (showTag ? p.TagH + 2 : 2);
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
            if (IsCore(p) || IsFrost(p))
            {   // drop in on your own family's side
                float lo, hi; Span(out lo, out hi);
                float third = (hi - lo) / 3, a = IsFrost(p) ? hi - third : lo, b = IsFrost(p) ? hi : lo + third;
                float x = a + W + (float)rnd.NextDouble() * Math.Max(1, b - a - 2 * W);
                foreach (var s2 in screenBounds) if (x >= s2.Left && x < s2.Right) { sb = s2; p.X = x; break; }
            }
            p.Y = sb.Top - 2;
            p.Dir = rnd.Next(2) == 0 ? 1 : -1;
            p.Speed = p.Me.SpeedMin + (float)rnd.NextDouble() * p.Me.SpeedRange;
            p.PetTicks = 0;
            p.Shooed = false;
            p.Returning = true;
            StartFalling(p, rnd.Next(100) < 60);   // most arrive by umbrella
        }

        // Leave whatever it was standing on and whatever game it was part of.
        void Detach(Penguin p)
        {
            p.Pest = false;
            p.Visits = null;
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
            p.Spin = true; p.AfterLand = FACEPLANT; p.Catcher = null;
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
            p.State = THROWN; p.Tick = 0; p.Catcher = null;
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
                if (p.Spin) OnFlung(p);
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
                int top = (int)(q.Y + q.HopOff) - HeadOf(q);
                if (Math.Abs(q.X - p.X) < 13 && top > y0 && top <= y1 && top < best) { best = top; head = q; }
            }

            if (head != null)
            {   // pile-up: stand on its head
                p.State = RIDE; p.Base = head; head.Rider = p;
                p.X = head.X; p.Y = best; p.Vx = p.Vy = 0; p.Tick = 0;
                p.Timer = 150 + rnd.Next(250); p.Umbrella = false; p.OnFloor = false; p.Surf = IntPtr.Zero;
                if (p.Returning) { p.Returning = false; Welcome(p); }
                return true;
            }
            if (!hit) return false;
            p.Y = land; p.OnFloor = floor; p.Surf = floor ? IntPtr.Zero : surf;
            p.Vx = p.Vy = 0; p.Tick = 0; p.Umbrella = false;
            p.State = afterState; p.Timer = afterTimer;
            EnsureTrack(p.Surf);
            if (p.Returning) { p.Returning = false; Welcome(p); }
            return true;
        }

        void StartBehaviour(Penguin p)
        {
            p.Tick = 0;
            if (idleMs > 90000 && rnd.Next(2) == 0)
            {   // long idle: naps
                p.State = SLEEP; p.Timer = 300 + rnd.Next(300); Say(p, BubbleArt.Zzz, 70); return;
            }
            // pick by this penguin's weights: look, wave, sleep, slide, dance, hop, fly
            var w = p.Me.Weights;
            int total = 0; foreach (int v in w) total += v;
            int r = rnd.Next(total), pick = 0;
            while (pick < w.Length - 1 && r >= w[pick]) r -= w[pick++];
            if (p.Rider != null && (pick == 3 || pick == 6)) pick = 0;          // carrying someone: no slides or flights
            switch (pick)
            {
                case 0:
                    if (p.Me.Accessory == Art.HardHat && p.Rider == null && rnd.Next(100) < 22) { p.State = REPAIR; p.FaceDir = p.Dir; }   // Wobble tinkers
                    else if ((IsCore(p) || IsFrost(p)) && rnd.Next(100) < 45) { p.State = IDLE; p.Timer = 100 + rnd.Next(60); }   // a signature idle
                    else { p.State = LOOK; p.Timer = 70 + rnd.Next(60); }
                    break;
                case 1: p.State = WAVE;  p.Timer = 50 + rnd.Next(40); break;
                case 2:
                    if (p.Name == "Pebble" && rnd.Next(3) == 0) { p.State = SLEEPWALK; p.Timer = 260 + rnd.Next(200); }   // off he wanders, fast asleep
                    else if (p.Name == "Pebble" && p.OnFloor && p.Rider == null && rnd.Next(100) < 60) { PillowTime(p); return; }   // the pillow ritual
                    else { p.State = SLEEP; p.Timer = 160 + rnd.Next(200); }
                    Say(p, BubbleArt.Zzz, 70);
                    break;
                case 3:
                    p.SlideV = p.Me.SlideMin + (float)rnd.NextDouble() * 2f;
                    if (p.Me.Accessory == Art.Goggles) { p.State = GOGGLE; p.Timer = 34; }             // Puddle: goggles down first
                    else { p.State = SLIDE; OnSlide(p); }
                    break;
                case 4:
                    if (p.Me.Leader && !music && rnd.Next(100) < 40) { p.State = LISTEN; p.Timer = 90 + rnd.Next(70); }   // Ziggy's own tunes
                    else p.State = DANCE;
                    if (p.State == DANCE) p.Timer = 80 + rnd.Next(60);
                    Say(p, p.State == LISTEN ? BubbleArt.Phones : BubbleArt.Note);
                    break;
                case 5: p.HopV = -3.4f; break;                                    // hop while walking
                default: p.State = FLY; p.OnFloor = false; p.Surf = IntPtr.Zero; p.FlyBaseY = p.Y; break;
            }
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
                bool reaching = p.Dir > 0 ? (p.X + 10 <= edge && nx + 10 > edge) : (p.X - 10 >= edge && nx - 10 < edge);
                if (!reaching) continue;
                if (r.Top >= y - 24 || r.Bottom < y - 30) continue;           // must rise above and reach down to it
                if (Covered(i, edge + p.Dir * 2, y - 15)) continue;           // that side isn't visible here
                if (!Standable(i, edge + p.Dir * 17, r.Top)) continue;        // nowhere to stand at the top
                p.ClimbCool = 150;
                if (rnd.Next(100) >= p.Me.ClimbPct) { Puzzled(p); return false; }   // bumped into it: "?" and walk past
                p.State = CLIMB; p.Tick = 0; p.OnFloor = false; p.Surf = wins[i].H;
                p.X = edge - p.Dir * 11;
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
            if (ix < virt.Left + 20 || ix >= virt.Right - 20) { p.Dir = -p.Dir; if (!Bonk(p)) Puzzled(p); return; }   // screen edge
            if (p.State == WALK && p.Job == null && TryClimb(p, nx)) return;
            if (p.OnFloor)
            {
                int f = FloorAt(ix);
                if (f == int.MinValue || f < y) { p.Dir = -p.Dir; if (!Bonk(p)) Puzzled(p); return; }  // gap or wall: "?" and turn
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
            p.PrevX = p.X;
            if (p.QCool > 0) p.QCool--;
            if (p.BumpCool > 0) p.BumpCool--;
            if (p.ClimbCool > 0) p.ClimbCool--;
            p.Tiptoe = false;
            if (p.Pillow && p.State != SLEEP) p.Pillow = false;
            if (p.CopyName != null && p.State != IDLE) p.CopyName = null;
            if (p.Hole != null && p.State != ICEFISH) CloseHole(p);              // called away: the hole freezes over

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
                    if (p.Spin && p.Catcher == null && p.Vy > -1 && p.Tick % 5 == 0) FindCatcher(p);
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
                    p.X = b.X; p.Y = b.Y + b.HopOff - HeadOf(b);
                    if (p.Pest && p.Timer == 50 && b.State != SHAKEOFF)
                    {   // the one underneath has had enough
                        b.State = SHAKEOFF; b.Timer = 50; b.Tick = 0; b.HopOff = 0; b.HopV = 0;
                        Say(b, BubbleArt.Grr, 50);
                        p.Timer = 1000;
                    }
                    if (--p.Timer <= 0)
                    {   // hop off sideways
                        Detach(p);
                        p.State = THROWN; p.Tick = 0; p.Spin = false; p.AfterLand = LOOK;
                        p.Vx = p.Dir * 2.5f; p.Vy = -4.5f;
                    }
                    return;
                }

                case SUMMON:
                {   // called to an activity: flap over in an arc and land on the spot
                    float k = Math.Min(1f, p.Tick / (float)Math.Max(1, p.Timer));
                    float arc = Math.Min(110f, 30f + Math.Abs(p.TargetX - p.HopFromX) * 0.15f);
                    p.X = p.HopFromX + (p.TargetX - p.HopFromX) * k;
                    p.Y = p.HopFromY + (p.SummonY - p.HopFromY) * k - (float)Math.Sin(k * Math.PI) * arc;
                    p.Dir = p.TargetX >= p.HopFromX ? 1 : -1;
                    if (k >= 1f)
                    {
                        int f = FloorAt((int)p.X);
                        p.Y = f != int.MinValue ? f : p.SummonY;
                        p.OnFloor = true; p.Surf = IntPtr.Zero;
                        p.Dir = p.FaceDir; p.State = p.NextState; p.Tick = 0;
                    }
                    return;
                }

                case HOPUP:
                {   // tower: arc up onto the head of whoever is on top
                    var onto = p.HopOnto;
                    if (onto == null || !penguins.Contains(onto) || onto.Rider != null || !CanCarry(onto))
                    { p.HopOnto = null; StartFalling(p, false); return; }
                    float k = Math.Min(1f, p.Tick / 28f);
                    float tx = onto.X, ty = onto.Y + onto.HopOff - HeadOf(onto);
                    p.X = p.HopFromX + (tx - p.HopFromX) * k;
                    p.Y = p.HopFromY + (ty - p.HopFromY) * k - (float)Math.Sin(k * Math.PI) * 30f;
                    if (k >= 1f)
                    {
                        p.State = RIDE; p.Base = onto; onto.Rider = p; p.HopOnto = null;
                        p.Timer = p.RideFor > 0 ? p.RideFor : 1000000;   // piggyback ride, or until the tower comes down
                        p.RideFor = 0; p.Tick = 0;
                    }
                    return;
                }

                case CLIMB:
                {
                    Track t;
                    if (!tracks.TryGetValue(p.Surf, out t)) { StartFalling(p, false); return; }
                    var r = t.R;
                    int edge = p.Dir > 0 ? r.Left : r.Right;
                    p.X = edge - p.Dir * 11;
                    p.Y -= 2f;
                    if (p.Y - 36 > r.Bottom) { StartFalling(p, false); return; }   // the window moved away
                    if (p.Y <= r.Top)
                    {   // made it: step onto the top
                        p.Y = r.Top; p.X = edge + p.Dir * 17;
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

            // Petting: rest the cursor on a calm penguin for ~1.5 s and it leans into it.
            bool calm = (p.State == WALK && p.HopOff == 0) || p.State == LOOK || p.State == CURIOUS;
            if (p.Hovered && calm && p.Cooldown == 0 && p.Job == null && p.Rider == null)
            {
                if (++p.PetTicks >= 45) { p.State = PET; p.Tick = 0; p.Timer = 25; p.GlareUntil = 0; Say(p, BubbleArt.Heart, 50); return; }
            }
            else if (p.State != PET) p.PetTicks = 0;

            switch (p.State)
            {
                case RACE:         // driven by UpdateRace
                    return;
                case TEASE:        // tongue out... then both burst out laughing
                    if (--p.Timer <= 0) { p.State = DANCE; p.Timer = 35; p.Tick = 0; Say(p, BubbleArt.Note, 40); }
                    return;
                case GG:
                case PHOTO:
                    p.Dir = p.FaceDir;
                    if (p.HopV != 0 || p.HopOff != 0)
                    {
                        p.HopOff += p.HopV; p.HopV += 0.25f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    return;
                case HUDDLE:
                {   // shuffle to my spot in the cluster
                    if (p.Tick == 1) p.Shiver = 40 + rnd.Next(40);                  // just arrived out of the cold: brrr
                    float gx = p.TargetX - p.X;
                    if (Math.Abs(gx) > 1.5f) { p.Dir = gx > 0 ? 1 : -1; Move(p, Math.Min(Math.Abs(gx), 0.7f)); }
                    else { p.Dir = p.FaceDir; if (p.Shiver > 0) p.Shiver--; }
                    if (p.HopV != 0 || p.HopOff != 0)
                    {   // Pip popping up for a look / shaking off the snow
                        p.HopOff += p.HopV; p.HopV += 0.3f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    if (huddle != null && huddle.Napper == p && p.Tick % 120 == 60) Say(p, BubbleArt.Zzz, 60);
                    return;
                }
                case BED:
                    p.Dir = p.FaceDir;
                    if (p.Tick % 260 == 130 && rnd.Next(3) == 0) Say(p, BubbleArt.Zzz, 70);
                    return;
                case TUCK:         // Waddles tucks Pip in, then lies down too
                    p.Dir = p.FaceDir;
                    if (p.Tick == 1) { p.Timer = 90; Say(p, BubbleArt.Heart, 60); }
                    if (--p.Timer <= 0) { p.State = BED; p.Tick = 0; Say(p, BubbleArt.Zzz, 60); }
                    return;
                case GOGGLE:       // Puddle: goggles down... crouch... go!
                    if (p.Tick == 14) p.GogglesUntil = ticks + 400;
                    if (--p.Timer <= 0) { p.State = SLIDE; p.Tick = 0; OnSlide(p); }
                    return;
                case HATFIX:       // Wobble: hat's gone wonky... push it back on
                    if (p.Tick == 1) Say(p, BubbleArt.Question, 40);
                    if (p.Tick == 40) p.HatAskew = false;
                    if (p.Tick == 54) Say(p, BubbleArt.Note, 40);                       // there we go
                    if (p.Tick >= 72)
                    {   // ...and maybe fix the wall that did it
                        p.Tick = 0;
                        if (p.FixWall && p.Job == null && rnd.Next(100) < 60) { p.State = REPAIR; p.Dir = p.FaceDir; }
                        else p.State = WALK;
                        p.FixWall = false;
                    }
                    return;
                case SLEEPWALK:    // Pebble, fast asleep, shuffling along with his flippers out
                {
                    if (p.Tick % 110 == 30) Say(p, BubbleArt.Zzz, 60);
                    int dir0 = p.Dir;
                    Move(p, 0.55f);
                    if (p.State != SLEEPWALK) return;                                     // wandered off an edge
                    bool bumped = p.Dir != dir0;                                          // walked into a wall
                    if (!bumped)
                        foreach (var q in penguins)
                            if (q != p && Math.Abs(q.Y - p.Y) <= 1 && (q.X - p.X) * p.Dir > 0 && Math.Abs(q.X - p.X) < 22) { bumped = true; break; }
                    if (bumped) { p.State = LOOK; p.Timer = 60; p.Tick = 0; Say(p, BubbleArt.Bang, 45); return; }   // huh?! where am I?
                    if (--p.Timer <= 0) { p.State = SLEEP; p.Timer = 200; p.Tick = 0; }  // settles down for a proper nap
                    return;
                }
                case LISTEN:       // Ziggy, nodding along to his headphones
                    if (p.Tick % 70 == 35) Say(p, (p.Tick / 70) % 2 == 0 ? BubbleArt.Phones : BubbleArt.Note, 40);
                    if (--p.Timer <= 0) { p.State = WALK; p.Tick = 0; }
                    return;
                case IDLE:         // each one's own way of passing the time
                    if (p.Me.Small && p.CopyName == null && p.Tick % 20 == 1 && p.HopOff == 0) p.HopV = -2.6f;
                    if (p.HopV != 0 || p.HopOff != 0)
                    {
                        p.HopOff += p.HopV; p.HopV += 0.3f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    if (IdleName(p) == "Waddles" && p.Tick % 60 == 10) Say(p, BubbleArt.Note, 40);   // humming
                    if (IdleName(p) == "Pebble" && p.Tick % 70 == 55) Say(p, BubbleArt.Bang, 22);    // ...jerks awake
                    if (IdleName(p) == "Bolt" && p.Tick % 18 == 1 && p.HopOff == 0) p.HopV = -2.4f;  // star jumps
                    if (IdleName(p) == "Mochi" && p.Tick == 2) Say(p, BubbleArt.Fish, 45);           // snack time
                    if (IdleName(p) == "Juniper" && p.Tick % 80 == 40) Say(p, BubbleArt.Heart, 35);
                    if (--p.Timer <= 0 && p.HopOff == 0)
                    {
                        p.Tick = 0;
                        if (p.Me.Accessory == Art.HardHat && rnd.Next(2) == 0)
                        { p.State = FACEPLANT; p.Timer = 45; Say(p, BubbleArt.Bang, 40); }       // wobbled right over
                        else p.State = WALK;
                    }
                    return;
                case PILLOW:       // Pebble: fluff, fluff... turn round, turn round again... and down he goes
                    p.Job = null;
                    if (p.Tick == 1)
                    {
                        p.Dir = p.FaceDir;
                        Say(p, BubbleArt.Pillow, 60);
                        var wd = Core("Waddles");
                        if (wd != null && wd != p && SameFloor(wd, p) && Math.Abs(wd.X - p.X) < 60 && wd.State != SLEEP) Say(wd, BubbleArt.Heart, 45);
                    }
                    if (p.Tick == 58 || p.Tick == 74) p.Dir = -p.Dir;
                    if (p.Tick >= 92) { p.State = SLEEP; p.Pillow = true; p.Timer = 300 + rnd.Next(250); p.Tick = 0; Say(p, BubbleArt.Zzz, 70); }
                    return;
                case GRUMP:        // Pebble, not a morning penguin: a big yawn, then a slow, grumpy shuffle
                    if (p.Tick > 40) { Move(p, 0.5f); if (p.State != GRUMP) return; }
                    if (p.Tick % 100 == 70) Say(p, BubbleArt.Cloud, 45);
                    if (--p.Timer <= 0) { p.State = WALK; p.Tick = 0; }
                    return;
                case SCARF:        // Waddles wraps her scarf round Pip (or Pip hands it back)
                {
                    var o = p.Job as Penguin;
                    p.Dir = p.FaceDir;
                    if (p.Tick == 1)
                    {
                        if (o == null || Math.Abs(o.X - p.X) > 60 || Math.Abs(o.Y - p.Y) > 1 || o.Job != null || !(o.State == HOLD || o.State == LOOK || o.State == WALK))
                        { p.Job = null; p.State = LOOK; p.Timer = 30; p.Tick = 0; Puzzled(p); return; }
                        o.State = HOLD; o.Tick = 0; o.Timer = 85; o.Buddy = null; o.HoldPet = true; o.FaceDir = o.Dir = p.X >= o.X ? 1 : -1;
                    }
                    if (p.Tick == 45) { scarfOnPip = !scarfOnPip; scarfSince = ticks; Say(p, BubbleArt.Heart, 50); if (o != null) Say(o, BubbleArt.Hearts, 55); }
                    if (p.Tick >= 75) { p.Job = null; p.State = WALK; p.Tick = 0; p.BumpCool = 200; }
                    return;
                }
                case HOLD:         // waiting for someone who's on the way over
                    p.Dir = p.FaceDir;
                    if (p.Buddy != null && p.Tick % 20 == 0) p.FaceDir = p.Buddy.X >= p.X ? 1 : -1;
                    if (--p.Timer <= 0)
                    {
                        p.Buddy = null; p.HoldPet = false; p.Tick = 0;
                        if (p.Visits != null) { NextVisit(p); return; }
                        p.State = WALK;
                    }
                    return;
                case VISIT:        // Waddles arrives: a high five, a hug for her best friend, a fond look at a sleeper
                {
                    var q = p.Job as Penguin;
                    p.Job = null; p.Tick = 0;
                    if (q == null || Math.Abs(q.X - p.X) > 70 || Math.Abs(q.Y - p.Y) > 1 || q.Job != null) { NextVisit(p); return; }
                    if (q.State == SLEEP)
                    {   // shh
                        p.State = HOLD; p.FaceDir = p.Dir = q.X >= p.X ? 1 : -1; p.Timer = 45; p.HoldPet = false; p.Buddy = null;
                        Say(p, BubbleArt.Heart, 40);
                        return;
                    }
                    if (!(q.State == HOLD || q.State == WALK || q.State == LOOK || q.State == IDLE)) { NextVisit(p); return; }
                    q.Buddy = null; q.HoldPet = false;
                    if (Friends(p, q) && !q.Me.Small)
                    {   // best friends: a flipper-bump and hearts
                        foreach (var r in new[] { p, q })
                        {
                            var other = r == p ? q : p;
                            r.State = SHAKEHANDS; r.Tick = 0; r.Timer = 60; r.Dir = other.X >= r.X ? 1 : -1;
                        }
                    }
                    else HighFive(p, q);
                    return;
                }
                case REPAIR:       // Wobble and his wrench: clank, clank... (and now and then, ow)
                    p.Dir = p.FaceDir; p.Job = null;
                    if (p.Tick == 1) { p.Timer = 110 + rnd.Next(60); Say(p, BubbleArt.Wrench, 50); }
                    if (p.Tick == 60 && rnd.Next(4) == 0)
                    { p.State = DAZED; p.Timer = 50; p.Tick = 0; if (p.Me.Accessory == Art.HardHat) p.HatAskew = true; Say(p, BubbleArt.Bang, 40); return; }   // whacked himself
                    if (--p.Timer <= 0) { p.State = WALK; p.Tick = 0; p.Dir = -p.Dir; Say(p, BubbleArt.Note, 40); }   // good as new
                    return;
                case SHOWOFF:      // Puddle: a twirl... and ta-da!
                    if (p.Tick == 34) Say(p, BubbleArt.Star, 50);
                    if (--p.Timer <= 0) { p.State = WALK; p.Tick = 0; p.Dir = p.FaceDir; }
                    return;
                case CLAP:         // Wobble cheering on his hero
                    p.Dir = p.FaceDir;
                    if (p.Tick % 22 == 5 && p.HopOff == 0) p.HopV = -2.8f;
                    if (p.HopV != 0 || p.HopOff != 0)
                    {
                        p.HopOff += p.HopV; p.HopV += 0.3f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    if (p.Tick == 36) Say(p, BubbleArt.Heart, 45);
                    if (--p.Timer <= 0 && p.HopOff == 0) { p.State = WALK; p.Tick = 0; }
                    return;
                case ATTN:         // standing to attention for Ziggy
                    if (inspect == null || p.Job != inspect) { p.Job = null; p.Attn = 0; p.State = WALK; p.Tick = 0; return; }
                    p.Dir = p.FaceDir;
                    if (p.HopV != 0 || p.HopOff != 0)
                    {
                        p.HopOff += p.HopV; p.HopV += 0.3f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    return;
                case INSPECT:      // Ziggy, moved along the line by UpdateInspect
                    if (inspect == null || p.Job != inspect) { p.Job = null; p.Attn = 0; p.State = WALK; p.Tick = 0; }
                    return;
                case CHECKPIP:     // Ziggy checks on his little brother: a pat on the head (or he keeps watch, if Pip's asleep)
                {
                    var pip = p.Job as Penguin;
                    p.Dir = p.FaceDir;
                    if (p.Tick == 1)
                    {
                        if (pip == null || Math.Abs(pip.X - p.X) > 60 || Math.Abs(pip.Y - p.Y) > 1 || pip.Job != null)
                        { p.Job = null; p.State = LOOK; p.Timer = 30; p.Tick = 0; Puzzled(p); return; }
                        if (pip.State == SLEEP) { p.State = GUARD; p.Tick = 0; Say(p, BubbleArt.Heart, 50); return; }   // fast asleep: keep watch
                        pip.State = HOLD; pip.Tick = 0; pip.Timer = 80; pip.Buddy = null; pip.HoldPet = true; pip.FaceDir = pip.Dir = p.X >= pip.X ? 1 : -1;
                    }
                    if (p.Tick == 28 && pip != null) Say(pip, BubbleArt.Heart, 50);
                    if (p.Tick >= 64)
                    {
                        p.Job = null; p.State = WALK; p.Tick = 0;
                        Say(p, BubbleArt.Note, 40);
                        if (pip != null && pip.State == HOLD) { pip.State = LOOK; pip.Timer = 20; pip.HoldPet = false; }
                        if (rnd.Next(100) < 40) TryPiggyback();                  // hop on!
                    }
                    return;
                }
                case SNEAK:        // Pip tiptoes up behind Ziggy... BOO!
                {
                    var z = p.Job as Penguin;
                    if (z == null || z.Job != null || Math.Abs(z.Y - p.Y) > 1 || z.State != LISTEN)
                    { p.Job = null; p.Attn = 0; p.Timer = 30; p.State = LOOK; p.Tick = 0; Puzzled(p); return; }
                    int side = p.X < z.X ? -1 : 1;
                    if (p.Timer > 0)
                    {   // frozen mid-step, all innocent... and he goes back to his music
                        if (--p.Timer == 0) z.Dir = -side;
                        return;
                    }
                    float gx = z.X + side * 18 - p.X;
                    if (Math.Abs(gx) > 2)
                    {
                        if (p.Attn < 1 && Math.Abs(gx) < 220 && Math.Abs(gx) > 60 && rnd.Next(80) == 0)
                        {   // Ziggy glances round: freeze!
                            p.Attn++; p.Timer = 45; z.Dir = side;
                            Say(z, BubbleArt.Question, 35); Say(p, BubbleArt.Note, 35);
                            return;
                        }
                        p.Dir = gx > 0 ? 1 : -1;
                        Move(p, 0.8f);
                        return;
                    }
                    p.Job = null; p.Attn = 0; p.Tick = 0;
                    p.State = DANCE; p.Timer = 70; p.Dir = -side; Say(p, BubbleArt.Bang, 40);   // BOO!
                    z.State = STARTLE; z.Tick = 0; z.Timer = 75; z.HopV = -5.5f; z.Dir = side; Say(z, BubbleArt.Bang, 45);
                    return;
                }
                case HUG:          // cuddled in: swaying together, squeezing tight now and then
                {
                    p.Dir = p.FaceDir;
                    var h = hug;
                    if (h == null || p.Job != h) { p.Job = null; p.State = LOOK; p.Timer = 30; p.Tick = 0; p.HopOff = 0; p.HopV = 0; return; }
                    if (p.Tick == 1) p.HopV = -2.6f;                                          // a hop into the hug
                    if (p.Me.Small && p.Tick % 55 == 30 && p.HopOff == 0) p.HopV = -3.4f;      // Pip, bouncing to see over everyone
                    if (p.HopV != 0 || p.HopOff != 0)
                    {
                        p.HopOff += p.HopV; p.HopV += 0.3f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    if (p.HopOff == 0) p.X = p.TargetX + p.FaceDir * (h.Squeeze > 0 ? 4f : 0f);  // squeeze in tight
                    if (p.HugNap && p.Tick % 120 == 60) Say(p, BubbleArt.Zzz, 55);
                    return;
                }
                case ICEFISH:      // chip a hole, sit, wait... a bite!
                    p.Dir = p.FaceDir;
                    p.FishT++;
                    switch (p.FishPhase)
                    {
                        case 0:    // chipping the hole
                            if (p.FishT == 1) Say(p, BubbleArt.Fish, 50);
                            if (p.FishT >= 45) { p.FishPhase = 1; p.FishT = 0; }
                            break;
                        case 1:    // waiting...
                        {
                            if (p.Name == "Pebble" && !p.FishNap && p.FishT == 100 && rnd.Next(100) < 60) { p.FishNap = true; Say(p, BubbleArt.Zzz, 60); }
                            if (p.FishNap && p.FishT % 160 == 0) Say(p, BubbleArt.Zzz, 60);
                            if (p.Me.Leader && p.FishT == 150) Say(p, BubbleArt.Grr, 40);            // come ON
                            int odds = p.Me.SlideMin >= 7f ? 110 : 240;                               // Puddle's a natural
                            if (p.FishT > 80 && rnd.Next(odds) == 0)
                            {
                                p.FishPhase = 2; p.FishT = 0;
                                Say(p, BubbleArt.Bang, 35);
                                if (p.FishNap) { p.FishNap = false; }                                   // jolted awake
                            }
                            else if (--p.Timer <= 0) { EndFishing(p, p.Catches > 0); return; }
                            break;
                        }
                        case 2:    // a bite: tug, tug!
                            if (p.FishT == 20 && p.Me.Accessory == Art.HardHat && rnd.Next(100) < 35)
                            {   // it's a big one... and it pulls Wobble flat on his face
                                EndFishing(p, false);
                                p.State = FACEPLANT; p.Timer = 50; p.Tick = 0;
                                Say(p, BubbleArt.Bang, 40);
                                return;
                            }
                            if (p.FishT >= 36)
                            {
                                p.FishT = 0;
                                if (rnd.Next(100) < 62)
                                {   // got one!
                                    p.FishPhase = 3; p.Catches++;
                                    Say(p, p.Name == "Waddles" ? BubbleArt.Hearts : BubbleArt.Heart, 50);
                                    foreach (var q in penguins)
                                        if (q.State == FISHWATCH && q.Job == p) { q.HopV = -3.6f; Say(q, BubbleArt.Bang, 35); }   // yay!
                                }
                                else if (p.Me.Leader) { Say(p, BubbleArt.Grr, 45); EndFishing(p, false); return; }   // Ziggy gives up
                                else { p.FishPhase = 1; Say(p, BubbleArt.Question, 40); }                        // it got away
                            }
                            break;
                        case 3:    // hold it up... then eat it
                            if (p.FishT >= 115)
                            {
                                p.FishPhase = 1; p.FishT = 0;
                                if (p.Catches >= 2) { EndFishing(p, true); return; }
                            }
                            break;
                    }
                    return;
                case FISHWATCH:    // sat beside the one fishing, watching the hole
                {
                    var f = p.Job as Penguin;
                    if (f == null || f.State != ICEFISH) { p.Job = null; p.State = WALK; p.Tick = 0; p.HopOff = 0; p.HopV = 0; return; }
                    p.Dir = p.FaceDir;
                    if (p.HopV != 0 || p.HopOff != 0)
                    {
                        p.HopOff += p.HopV; p.HopV += 0.3f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    return;
                }
                case STARTLE:      // Ziggy jumps out of his skin... then laughs it off
                    if (p.HopV != 0 || p.HopOff != 0)
                    {
                        p.HopOff += p.HopV; p.HopV += 0.35f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    if (p.Tick == 40) Say(p, BubbleArt.Note, 40);
                    if (--p.Timer <= 0 && p.HopOff == 0) { p.State = DANCE; p.Timer = 40; p.Tick = 0; }
                    return;
                case GROOVE:       // dancing to your music (hops come from the beat)
                    if (!music) { p.State = LOOK; p.Timer = 30; p.Tick = 0; p.HopOff = 0; p.HopV = 0; return; }
                    if (p.HopV != 0 || p.HopOff != 0)
                    {
                        p.HopOff += p.HopV; p.HopV += 0.45f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    return;
                case POSE:         // say cheese!
                    if (p.Me.Small && p.Tick == 4) p.HopV = -4f;
                    if (p.HopV != 0 || p.HopOff != 0)
                    {
                        p.HopOff += p.HopV; p.HopV += 0.3f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    if (--p.Timer <= 0 && p.HopOff == 0) { p.State = LOOK; p.Timer = 20; p.Tick = 0; }
                    return;
                case PEEK:         // what's this new window?
                    p.Dir = p.FaceDir;
                    if (p.Tick == 1) { p.Timer = 85; Say(p, BubbleArt.Question, 45); }
                    if (p.Tick == 50) Say(p, rnd.Next(2) == 0 ? BubbleArt.Bang : BubbleArt.Heart, 40);
                    if (--p.Timer <= 0) { p.Job = null; p.State = LOOK; p.Timer = 30; p.Tick = 0; }
                    return;
                case MOVIE:
                    p.Dir = p.FaceDir;
                    if (p.HopV != 0 || p.HopOff != 0)
                    {
                        p.HopOff += p.HopV; p.HopV += 0.3f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    return;
                case DINE:         // driven by UpdateDinner
                case COOK:
                    p.Dir = p.FaceDir;
                    return;
                case BEDGUARD:     // Ziggy keeps watch for a while... then dozes off too
                    if (p.Tick == 1) p.Timer = 330;
                    if (p.Tick % 60 == 0) p.Dir = -p.Dir;
                    if (--p.Timer <= 0) { p.State = BED; p.Tick = 0; Say(p, BubbleArt.Zzz, 60); }
                    return;
                case GUARD:
                {   // stand by sleeping Pip, keeping a lookout
                    var pip = p.Job as Penguin;
                    if (pip == null || pip.State != SLEEP) { p.Job = null; p.State = LOOK; p.Timer = 30; p.Tick = 0; return; }
                    if (p.Tick % 60 == 0) p.Dir = -p.Dir;
                    return;
                }
                case JEALOUS:
                {   // glare at the one getting all the attention
                    var c = p.Job as Penguin;
                    if (c == null || c.State != PET) { p.Job = null; Sulk(p, c != null && c.X >= p.X ? -1 : 1, c); return; }
                    p.Dir = c.X >= p.X ? 1 : -1;
                    if (p.Tick % 70 == 0) Say(p, BubbleArt.Grr, 45);
                    if (p.Tick % 90 == 45 && Math.Abs(c.X - p.X) > 16) p.X += p.Dir * 8;   // squeeze in closer
                    return;
                }
                case CHEER:
                {   // hop and wave for a friend in the race
                    var rc = race;
                    if (rc == null || p.Job != rc) { p.Job = null; p.State = LOOK; p.Timer = 30; p.HopOff = 0; p.HopV = 0; return; }
                    var who = Math.Abs(rc.A.X - p.X) < Math.Abs(rc.B.X - p.X) ? rc.A : rc.B;
                    p.Dir = who.X >= p.X ? 1 : -1;
                    if (p.HopV == 0 && p.HopOff == 0 && rnd.Next(25) == 0) p.HopV = -3f;
                    if (p.HopV != 0 || p.HopOff != 0)
                    {
                        p.HopOff += p.HopV; p.HopV += 0.25f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    return;
                }
                case STARE:
                {   // eyes locked... whoever blinks first loses
                    var o = p.Buddy;
                    if (o == null || (o.State != STARE && o.State != BLINK && o.State != SULK))
                    { p.Buddy = null; p.State = WALK; p.Tick = 0; p.BumpCool = 200; return; }
                    p.Dir = o.X >= p.X ? 1 : -1;
                    if (--p.Timer <= 0)
                    {
                        p.Buddy = null; p.Tick = 0;
                        if (o.State == STARE) { p.State = BLINK; p.Timer = 18; p.MakeUpWith = o; }     // blinked!
                        else { p.State = DANCE; p.Timer = 50; Say(p, BubbleArt.Note); p.BumpCool = 200; }
                    }
                    return;
                }
                case BLINK:
                    if (--p.Timer <= 0) { Sulk(p, -p.Dir, p.MakeUpWith); p.BumpCool = 200; }
                    return;
                case SULK:
                    Move(p, 0.55f);
                    if (p.State == SULK && --p.Timer <= 0)
                    {
                        p.Tick = 0;
                        var w = p.MakeUpWith;
                        p.MakeUpWith = null;
                        if (w != null && penguins.Contains(w) && Free(w) && Math.Abs(w.Y - p.Y) <= 1 && Math.Abs(w.X - p.X) < 700)
                        {   // the other one comes over to make up; wait for them
                            int side = w.X < p.X ? -1 : 1;
                            SendTo(w, p, p.X + side * 28, -side, MAKEUP);
                            p.State = LOOK; p.Timer = 260; p.Dir = side;
                        }
                        else p.State = WALK;
                    }
                    return;
                case MAKEUP:
                {   // arrived next to the sulker: flipper-bump, all forgiven
                    var o = p.Job as Penguin;
                    p.Job = null; p.Tick = 0;
                    if (o != null && Math.Abs(o.X - p.X) < 70 && (o.State == LOOK || o.State == WALK || o.State == SULK))
                    {
                        foreach (var q in new[] { p, o })
                        {
                            var other = q == p ? o : p;
                            q.State = SHAKEHANDS; q.Tick = 0; q.Timer = 60; q.Dir = other.X >= q.X ? 1 : -1;
                        }
                        MakeTruce(p, o, 1500);
                    }
                    else { p.State = LOOK; p.Timer = 30; }
                    return;
                }
                case AMBUSH:
                {   // wind up... and lob it
                    var t = p.Target;
                    if (t != null) p.Dir = t.X >= p.X ? 1 : -1;
                    if (--p.ThrowAnim == 8 && t != null && penguins.Contains(t)) ThrowAt(p, t);
                    if (p.ThrowAnim <= 0) { p.Target = null; p.State = WALK; p.Tick = 0; }
                    return;
                }
                case SHAKEOFF:
                    if (--p.Timer <= 0)
                    {   // shake the pest off
                        var r = p.Rider;
                        if (r != null)
                        {
                            Detach(r);
                            r.State = THROWN; r.Tick = 0; r.Spin = false; r.AfterLand = LOOK; r.Catcher = null;
                            r.Vx = (rnd.Next(2) == 0 ? 1 : -1) * 3.5f; r.Vy = -4.5f;
                            r.OnFloor = false; r.Surf = IntPtr.Zero;
                            Say(r, BubbleArt.Note, 40);                        // wheee, giggling
                        }
                        p.State = WALK; p.Tick = 0; p.BumpCool = 200;
                    }
                    return;
                case SHAKEHANDS:
                    if (p.Tick == 30) Say(p, BubbleArt.Heart, 40);
                    if (--p.Timer <= 0)
                    {
                        if (p.Visits != null) { NextVisit(p); return; }
                        p.State = WALK; p.Tick = 0; p.Dir = -p.Dir; p.BumpCool = 300;
                    }
                    return;
                case HIFIVE:
                {   // face each other, jump and slap flippers, then go separate ways
                    p.Dir = p.FaceDir;
                    if (p.Tick == 12) p.HopV = -3.4f;
                    if (p.HopV != 0 || p.HopOff != 0)
                    {
                        p.HopOff += p.HopV; p.HopV += 0.25f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    if (--p.Timer <= 0 && p.HopOff == 0)
                    {
                        if (p.Visits != null) { NextVisit(p); return; }              // Waddles: on to the next one
                        p.State = WALK; p.Tick = 0; p.Dir = p.NextDir; p.BumpCool = 200;
                    }
                    return;
                }
                case TOGETHER:
                {   // keep step just behind a friend
                    var bud = p.Buddy;
                    if (bud == null || --p.Timer <= 0 || !penguins.Contains(bud) || bud.Job != null || bud.OnFloor != p.OnFloor
                        || bud.Surf != p.Surf || Math.Abs(bud.Y - p.Y) > 1 || !StandsOnSurface(bud))
                    { p.Buddy = null; p.State = WALK; p.Tick = 0; p.BumpCool = 200; return; }
                    p.TargetX = bud.X - bud.Dir * 24;
                    float gx = p.TargetX - p.X;
                    if (Math.Abs(gx) < 2.5f) { p.Dir = bud.Dir; return; }
                    p.Dir = gx > 0 ? 1 : -1;
                    Move(p, Math.Min(Math.Abs(gx), bud.Speed + 1f));
                    return;
                }
                case NAPWITH:
                {   // arrived beside a sleeping friend: lie down too
                    var q = p.Job as Penguin;
                    p.Job = null; p.Tick = 0;
                    if (q != null && q.State == SLEEP)
                    {
                        p.State = SLEEP; p.Timer = Math.Max(q.Timer, 120) + 60;
                        p.Dir = q.X >= p.X ? 1 : -1;
                        Say(p, BubbleArt.Zzz, 70);
                    }
                    else { p.State = LOOK; p.Timer = 30; }
                    return;
                }
                case CATCH:
                {   // run under a flung friend
                    var w = p.CatchWho;
                    if (w == null || w.State != THROWN)
                    {
                        if (w != null && w.Base == p)
                        {   // caught!
                            Say(p, BubbleArt.Heart, 55); Say(w, BubbleArt.Heart, 55);
                            w.Timer = 45; w.Spin = false;
                        }
                        if (w != null) w.Catcher = null;
                        p.CatchWho = null; p.State = LOOK; p.Timer = 40; p.Tick = 0;
                        return;
                    }
                    float gx = (w.X + w.Vx * 6) - p.X;
                    if (Math.Abs(gx) > 2) { p.Dir = gx > 0 ? 1 : -1; Move(p, Math.Min(Math.Abs(gx), 4.5f)); }
                    return;
                }
                case HELP:
                {   // arrived next to Pip: help it up
                    var pip = p.Job as Penguin;
                    p.Job = null; p.Tick = 0;
                    if (pip != null && (pip.State == FACEPLANT || pip.State == DAZED))
                    {
                        pip.State = LOOK; pip.Timer = 45; pip.Tick = 0; pip.Dir = p.X > pip.X ? 1 : -1;
                        Say(pip, BubbleArt.Heart, 55); Say(p, BubbleArt.Heart, 55);
                        p.State = WAVE; p.Timer = 45;
                    }
                    else if (pip != null && Math.Abs(pip.X - p.X) < 70 && (pip.State == WALK || pip.State == LOOK))
                    {   // already up: a comforting hug anyway
                        pip.State = LOOK; pip.Timer = 40; pip.Tick = 0; pip.Dir = p.X > pip.X ? 1 : -1;
                        Say(pip, BubbleArt.Heart, 55); Say(p, BubbleArt.Heart, 55);
                        p.State = WAVE; p.Timer = 40;
                    }
                    else { p.State = LOOK; p.Timer = 30; }
                    if (pip != null) pip.HelpedBy = null;
                    return;
                }
                case FOLLOW:
                {   // keep my place in Ziggy's line
                    var pr = parade;
                    if (pr == null || p.Job != pr) { p.Job = null; p.State = WALK; p.Tick = 0; return; }
                    int place = pr.Line.IndexOf(p) + 1;
                    var lead = pr.Leader;
                    bool airborne = p.HopV != 0 || p.HopOff != 0;
                    if (airborne)
                    {   // leapfrogging
                        p.HopOff += p.HopV; p.HopV += 0.22f;
                        if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
                    }
                    float gap = 31, speed = 2.8f;
                    if (pr.Stunt == 1) speed = 4.5f;                                    // keep up with the slide
                    if (pr.Stunt == 2 && pr.Jumper != null && pr.LeapT < RoomLen)
                    {   // making room at the front (and the jumper gets ready)
                        p.Dir = lead.Dir;
                        return;
                    }
                    if (pr.Stunt == 3)
                    {   // hurrying along... then piling into the one in front
                        if (p.PileAt > 0) return;
                        speed = 3.6f;
                        if (pr.StuntT >= RushLen)
                        {
                            gap = 15;
                            float tx = lead.X - lead.Dir * gap * place;
                            if (Math.Abs(tx - p.X) < 3 || pr.StuntT > RushLen + 25 + place * 8)
                            {
                                p.PileAt = pr.StuntT;
                                Say(p, BubbleArt.Bang, 30);
                                return;
                            }
                        }
                    }
                    if (airborne) speed = 5.5f;
                    p.TargetX = lead.X - lead.Dir * gap * place;
                    float gx = p.TargetX - p.X;
                    if (pr.Stunt == 2 && !airborne && (gx > 0 ? 1 : -1) != lead.Dir && Math.Abs(gx) >= 2.5f)
                    { p.Dir = lead.Dir; return; }                                       // let the line catch up; never walk backwards
                    if (Math.Abs(gx) < 0.5f) { p.Dir = lead.Dir; return; }                // keep pace smoothly, no stop-start
                    p.Dir = gx > 0 ? 1 : -1;
                    Move(p, Math.Min(Math.Abs(gx), speed));
                    return;
                }
                case PET:
                {
                    p.Dir = Cursor.Position.X >= p.X ? 1 : -1;               // lean toward the hand
                    if (p.Hovered) p.Timer = 25;                              // keep going while the cursor stays
                    else if (--p.Timer <= 0) { p.State = WALK; p.Tick = 0; p.Cooldown = 120; p.PetTicks = 0; return; }
                    if (p.Tick % p.Me.PetHearts == 0) Say(p, BubbleArt.Heart, 45);
                    p.PetTotal++;
                    if (p.Tick == 45 && hug == null) Jealous(p);
                    if (p.Tick == 90 && p.OnFloor && !GroupBusy() && ticks >= shooUntil) StartHug(p);   // everyone wants in
                    if (p.Me.PetLimit > 0 && p.Tick >= p.Me.PetLimit && (hug == null || hug.Center != p))
                    {   // Pebble dozes off; Ziggy gets fidgety and dances away
                        p.PetTicks = 0; p.Tick = 0; p.Cooldown = 200;
                        if (p.Me.PetEnd == SLEEP) { p.State = SLEEP; p.Timer = 250 + rnd.Next(200); Say(p, BubbleArt.Zzz, 70); }
                        else if (p.Me.PetEnd == LOOK) { p.State = LOOK; p.Timer = 70; Say(p, BubbleArt.Grr, 45); }   // Doodle: hmph (she liked it)
                        else if (p.Me.PetEnd == WALK) { p.State = WALK; Say(p, BubbleArt.Bang, 40); }               // Bubbles: back to duty
                        else { p.State = DANCE; p.Timer = 70; Say(p, BubbleArt.Note); }
                    }
                    return;
                }
                case LOOK:
                case WAVE:
                case SLEEP:
                case DANCE:
                    if (--p.Timer <= 0)
                    {
                        p.Tick = 0;
                        if (p.PendingSlide) { p.PendingSlide = false; p.State = SLIDE; p.Dir = p.SlideDir; p.SlideV = 5.5f; return; }
                        if (p.State == SLEEP && p.Name == "Pebble" && rnd.Next(100) < 55)
                        { p.State = GRUMP; p.Timer = 200 + rnd.Next(80); Say(p, BubbleArt.Cloud, 60); return; }   // not a morning penguin
                        p.State = WALK; if (rnd.Next(3) == 0) p.Dir = -p.Dir;
                    }
                    return;
                case FACEPLANT:    // beak in the ground, feet wiggling...
                    if (p.Tick == 1 && IsCore(p)) CallForHelp(p);
                    if (p.Tick == 1 && p.Me.Accessory == Art.HardHat) p.HatAskew = true;
                    if (p.Me.Small && p.HelpedBy != null && p.HelpedBy.Job == p && p.Tick < 450) p.Timer = Math.Max(p.Timer, 2);   // Pip stays down until help comes
                    if (--p.Timer <= 0) { p.State = DAZED; p.Timer = 55; p.Tick = 0; p.HelpedBy = null; }
                    return;
                case DAZED:        // ...then up again, seeing stars
                    if (--p.Timer <= 0)
                    {
                        p.Tick = 0;
                        if (p.LaughNext) { p.LaughNext = false; p.State = DANCE; p.Timer = 40; Say(p, BubbleArt.Note, 40); }   // laughs it off
                        else if (p.HatAskew && p.Job == null) p.State = HATFIX;                  // Wobble sorts his hat out
                        else p.State = WALK;
                    }
                    return;
                case ADMIRE:       // step back and admire the snowman, then celebrate
                    if (--p.Timer <= 0) { p.State = DANCE; p.Timer = 70; p.Tick = 0; Say(p, BubbleArt.Note); }
                    return;
                case YAWN:
                    if (--p.Timer <= 0)
                    {
                        p.Tick = 0;
                        if (p.WaveAfter) { p.WaveAfter = false; p.State = WAVE; p.Timer = 60; p.Dir = Cursor.Position.X >= p.X ? 1 : -1; Say(p, BubbleArt.Heart, 50); }
                        else if (rnd.Next(100) < 40) { p.State = SLEEP; p.Timer = 200 + rnd.Next(250); Say(p, BubbleArt.Zzz, 70); }
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
                case FIGHT:        // snowball fight (driven by UpdateFight)
                case PARTY:        // dancing (driven by UpdateParty)
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
                    Move(p, Math.Min(p.Speed + (p.NextState == HUG ? 2.2f : 0.6f), Math.Abs(gx)));   // hurrying to a hug
                    if (p.State == GOTO && --p.Timer <= 0)
                    {   // couldn't get there: the core five fly the rest of the way, others give up
                        if (IsCore(p)) Summon(p);
                        else { p.Job = null; p.State = WALK; }
                    }
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
                    if (p.State == SLIDE && p.SlideV < 0.6f)
                    {
                        if (p.SlideFail) { p.SlideFail = false; p.State = FACEPLANT; p.Timer = 50; p.Tick = 0; Say(p, BubbleArt.Bang, 40); }   // whoops
                        else if (p.Me.SlideMin >= 7f && p.Job == null && p.OnFloor && rnd.Next(100) < 40) ShowOff(p);   // ta-da!
                        else { p.State = LOOK; p.Timer = 20; }
                        if (p.GogglesUntil > ticks + 60) p.GogglesUntil = ticks + 60;          // goggles back up in a moment
                    }
                    return;
                case CURIOUS:
                {   // watch the cursor; wave when it comes close
                    var c = Cursor.Position;
                    float dx = c.X - p.X, dy = c.Y - (p.Y - 24);
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    p.Dir = dx >= 0 ? 1 : -1;
                    p.Near = dist < 50;
                    if (p.Name == "Waddles" && p.Job == null && p.Rider == null && !p.Near && p.Tick > 15 && p.Tick < 300 && Math.Abs(dx) > 32)
                    {   // Waddles toddles after your cursor for a few steps
                        if (p.Tick == 16) Say(p, BubbleArt.Hearts, 45);
                        Move(p, 1.5f);
                        if (p.State != CURIOUS) return;
                    }
                    if (dist > 160 || --p.Timer <= 0) { p.State = WALK; p.Tick = 0; p.Cooldown = 200; }
                    return;
                }
            }

            // Walking (maybe mid-hop)
            if (p.Cooldown > 0) p.Cooldown--;
            if (parade != null && p.Job == parade)
            {   // leading the parade: a steady march, no distractions
                var pr = parade;
                if (pr.Stunt == 3) { if (pr.StuntT < RushLen) Move(p, 3.2f); return; }   // faster, faster... stop!
                Move(p, pr.Stunt == 1 && pr.StuntT < SlideLen ? 3.6f : 1.7f);
                return;
            }
            if (p.HopV != 0 || p.HopOff != 0)
            {
                p.HopOff += p.HopV; p.HopV += 0.22f;
                if (p.HopOff >= 0) { p.HopOff = 0; p.HopV = 0; }
            }
            else
            {
                var c = Cursor.Position;
                float dx = c.X - p.X, dy = c.Y - (p.Y - 24);
                if (dx * dx + dy * dy < 80 * 80) p.NearTicks++; else p.NearTicks = 0;
                if (p.Cooldown == 0 && p.NearTicks >= (p == favourite ? Math.Min(8, p.Me.CuriousTicks) : p.Me.CuriousTicks))
                {   // the cursor lingered nearby (~0.5 s): stop and look at it
                    p.NearTicks = 0;
                    p.State = CURIOUS; p.Timer = 300; p.Tick = 0; p.Near = false;
                    if (p.GlareUntil > ticks) Say(p, BubbleArt.Grr, 60);
                    return;
                }
                if (follow)
                {
                    if (Math.Abs(dx) > 25) p.Dir = dx > 0 ? 1 : -1;
                    else { p.State = LOOK; p.Timer = 20; return; }      // arrived under the cursor
                }
                else if (rnd.Next(700) == 0) p.Dir = -p.Dir;
                if (!follow) HomeSide(p);
                if (!follow && p.Tick % 45 == 20 && rnd.Next(3) == 0 && TryNapWith(p)) return;
                if (p.Tick % 30 == 5) WakeRival(p);
                if (!follow && p == favourite && p.Tick % 90 == 45)
                {   // your favourite likes to stay near you
                    float fx = Cursor.Position.X - p.X;
                    if (Math.Abs(fx) > 40 && Math.Abs(fx) < 900) p.Dir = fx > 0 ? 1 : -1;
                }
                if (!follow && p.Me.TagAlong && p.Tick % 30 == 0)
                {   // little brother: hurry after the nearest big sibling, and sometimes copy them
                    var big = NearestSibling(p);
                    if (big != null)
                    {
                        float gap = big.X - p.X;
                        if (Math.Abs(gap) > 90) p.Dir = gap > 0 ? 1 : -1;
                        else if (Math.Abs(gap) < 60 && rnd.Next(3) == 0 && CopyOf(p, big)) return;
                    }
                }
                if (idleMs > 30000 && rnd.Next(500) == 0)
                {   // you've been away a while: yawn
                    p.State = YAWN; p.Timer = 55; p.Tick = 0;
                    Say(p, BubbleArt.Zzz, 55);
                    return;
                }
                if (p.Me.TripOdds > 0 && rnd.Next(p.Me.TripOdds) == 0)
                {   // tripped over nothing at all
                    p.State = FACEPLANT; p.Timer = 50; p.Tick = 0;
                    Say(p, BubbleArt.Bang, 45);
                    return;
                }
                if (rnd.Next(follow ? 900 : p.Me.Restless) == 0) { StartBehaviour(p); return; }
            }
            float sp = p.Speed;
            if (p.Me.Small && p.Job == null && SleeperNear(p)) { p.Tiptoe = true; sp = 0.8f; }   // shh: tiptoe past whoever's asleep
            Move(p, sp);
        }

        // The closest other core penguin on the same surface (for Pip to tag along after).
        Penguin NearestSibling(Penguin p)
        {
            Penguin best = null;
            float bestD = 700;
            foreach (var q in penguins)
            {
                if (q == p || !(IsFrost(p) ? IsFrost(q) : IsCore(q)) || q.Job != null || q.OnFloor != p.OnFloor || q.Surf != p.Surf || Math.Abs(q.Y - p.Y) > 1) continue;
                float d = Math.Abs(q.X - p.X);
                if (d < bestD) { bestD = d; best = q; }
            }
            return best;
        }

        // Do whatever the big sibling is doing.
        bool CopyOf(Penguin p, Penguin big)
        {
            p.Tick = 0;
            switch (big.State)
            {
                case SLEEP: p.State = SLEEP; p.Timer = 150 + rnd.Next(100); Say(p, BubbleArt.Zzz, 60); return true;
                case DANCE: p.State = DANCE; p.Timer = 80; Say(p, BubbleArt.Note); return true;
                case WAVE: case GREET: p.State = WAVE; p.Timer = 50; return true;
                case LOOK: case CURIOUS: p.State = LOOK; p.Timer = 50; p.Dir = big.Dir; return true;
                case SLIDE: p.State = SLIDE; p.Dir = big.Dir; p.SlideV = Math.Max(4f, big.SlideV); return true;
                case IDLE: p.State = IDLE; p.Timer = 90 + rnd.Next(40); p.CopyName = big.CopyName ?? big.Name; return true;   // the same little habit
                case LISTEN: p.State = IDLE; p.Timer = 90; p.CopyName = big.Name; return true;                                // nodding along too
                case SLEEPWALK: p.State = SLEEPWALK; p.Timer = 160; Say(p, BubbleArt.Zzz, 60); return true;
                case GRUMP: p.State = GRUMP; p.Timer = 150; Say(p, BubbleArt.Cloud, 50); return true;
                case REPAIR: p.State = REPAIR; p.FaceDir = p.Dir = big.Dir; return true;
                case SHOWOFF: p.State = SHOWOFF; p.Timer = 95; p.FaceDir = p.Dir; return true;
                case HATFIX: case GOGGLE: p.State = HATFIX; return true;                                                    // straightens his propeller
                case PILLOW: p.State = PILLOW; p.FaceDir = p.Dir; return true;
            }
            return false;
        }

        // ---- rivals

        void RivalMeet(Penguin a, Penguin b)
        {
            if (!a.OnFloor && a.Surf != IntPtr.Zero && rnd.Next(2) == 0) { LedgeShove(a, b); return; }   // up on a window: shove!
            int r = rnd.Next(100);
            if (r < 30 && StartRace(a, b)) return;
            if (r < 55) { Stare(a, b); return; }
            if (r < 70) { HeadHop(a, b); return; }
            if (r < 75) { MakeUp(a, b); return; }
            Tease(a, b);
        }

        // Sticking their tongues out at each other... then both crack up.
        void Tease(Penguin a, Penguin b)
        {
            foreach (var p in new[] { a, b })
            {
                var o = p == a ? b : a;
                p.State = TEASE; p.Tick = 0; p.Timer = 45; p.BumpCool = 250;
                p.Dir = o.X >= p.X ? 1 : -1;
            }
        }

        // Walk off slowly in a huff... until the other one comes over to make up.
        void Sulk(Penguin p, int dir, Penguin makeUpWith = null)
        {
            p.State = SULK; p.Tick = 0; p.Timer = 140; p.Dir = dir; p.Buddy = null;
            p.MakeUpWith = makeUpWith;
            Say(p, BubbleArt.Grr, 60);
        }

        bool StartRace(Penguin a, Penguin b)
        {
            if (GroupBusy() || !a.OnFloor || !b.OnFloor || ticks < shooUntil) return false;
            float mid = (a.X + b.X) / 2;
            var wa = WorkAreaAt(mid);
            int dir = (wa.Right - mid) > (mid - wa.Left) ? 1 : -1;               // toward the farther edge
            float room = dir > 0 ? wa.Right - 60 - mid : mid - (wa.Left + 60);
            if (room < 160) return false;
            var rc = new Race { A = a, B = b, Dir = dir, Timer = 45, Finish = mid + dir * Math.Min(420f, room) };
            a.X = mid - 9; b.X = mid + 9;                                          // side by side on the start line
            foreach (var p in new[] { a, b })
            {
                p.Job = rc; p.State = RACE; p.Tick = 0; p.Dir = dir; p.HopOff = 0; p.HopV = 0;
                Say(p, BubbleArt.Bang, 45);
            }
            foreach (var q in penguins)
            {   // friends of either racer come to cheer
                if (q == a || q == b || !Available(q) || Math.Abs(q.Y - a.Y) > 1 || Math.Abs(q.X - mid) > 600) continue;
                if (!Friends(q, a) && !Friends(q, b)) continue;
                q.Job = rc; q.State = CHEER; q.Tick = 0;
                rc.Fans.Add(q);
            }
            race = rc;
            return true;
        }

        void UpdateRace()
        {
            var rc = race;
            if (rc == null) return;
            if (rc.A.Job != rc || rc.B.Job != rc) { EndRace(null); return; }
            rc.Fans.RemoveAll(q => q.Job != rc);
            if (ticks % 40 == 0 && rc.Fans.Count > 0) Say(rc.Fans[rnd.Next(rc.Fans.Count)], BubbleArt.Note, 35);
            if (rc.Phase == 0) { if (--rc.Timer <= 0) { rc.Phase = 1; rc.Timer = 600; } return; }   // ready... set...
            foreach (var p in new[] { rc.A, rc.B })
            {
                if (p.State != RACE) { EndRace(null); return; }
                float v = (p.Me.SlideMin >= 7f ? 4.4f : 3.0f + p.Speed * 0.45f) + (float)rnd.NextDouble() * 1.4f;   // Puddle slides
                Move(p, v);
                if (p.State != RACE || p.Dir != rc.Dir) { EndRace(null); return; }       // ran into something
                if ((p.X - rc.Finish) * rc.Dir >= 0) { EndRace(p); return; }
            }
            if (--rc.Timer <= 0) EndRace(null);
        }

        void EndRace(Penguin winner)
        {
            var rc = race;
            if (rc == null) return;
            race = null;
            var all = new List<Penguin>(rc.Fans) { rc.A, rc.B };
            foreach (var p in all)
            {
                if (p.Job != rc) continue;
                p.Job = null; p.Tick = 0; p.HopOff = 0; p.HopV = 0;
                p.State = LOOK; p.Timer = 40;
            }
            if (winner == null) return;
            var loser = winner == rc.A ? rc.B : rc.A;
            if (winner.State == LOOK) { winner.State = DANCE; winner.Timer = 80; Say(winner, BubbleArt.Note); }
            if (loser.State == LOOK) Sulk(loser, -rc.Dir, winner);
            foreach (var q in rc.Fans)
                if (q.State == LOOK && Friends(q, winner)) { q.State = DANCE; q.Timer = 50; Say(q, BubbleArt.Note, 45); }
        }

        void Stare(Penguin a, Penguin b)
        {
            int t = 70 + rnd.Next(110);
            var loser = rnd.Next(2) == 0 ? a : b;
            foreach (var p in new[] { a, b })
            {
                var o = p == a ? b : a;
                p.State = STARE; p.Tick = 0; p.Buddy = o;
                p.Timer = p == loser ? t : t + 18;
                p.Dir = o.X >= p.X ? 1 : -1;
            }
            Say(a, BubbleArt.Bang, 40);
        }

        void HeadHop(Penguin a, Penguin b)
        {
            var hopper = rnd.Next(2) == 0 ? a : b;
            var under = hopper == a ? b : a;
            if (under.Rider != null || hopper.Rider != null) { Tease(a, b); return; }
            under.State = LOOK; under.Timer = 200; under.Tick = 0;
            Say(under, BubbleArt.Bang, 40);
            hopper.State = HOPUP; hopper.Tick = 0; hopper.HopOnto = under;
            hopper.HopFromX = hopper.X; hopper.HopFromY = hopper.Y;
            hopper.OnFloor = false; hopper.Surf = IntPtr.Zero;
            hopper.RideFor = 90; hopper.Pest = true;
        }

        void LedgeShove(Penguin a, Penguin b)
        {
            var shover = rnd.Next(2) == 0 ? a : b;
            var victim = shover == a ? b : a;
            int dir = victim.X >= shover.X ? 1 : -1;
            float vx = 3.2f;
            Track tr;
            if (tracks.TryGetValue(victim.Surf, out tr))
            {   // enough of a push to go over the edge
                float edge = dir > 0 ? tr.R.Right : tr.R.Left;
                vx = Math.Min(12f, Math.Max(3.2f, (Math.Abs(edge - victim.X) + 24) / 18f));
            }
            Detach(victim);
            victim.State = THROWN; victim.Tick = 0; victim.Spin = true; victim.AfterLand = FACEPLANT;
            victim.Vx = dir * vx; victim.Vy = -3.5f; victim.OnFloor = false; victim.Surf = IntPtr.Zero; victim.Catcher = null;
            Say(victim, BubbleArt.Bang, 45);
            shover.State = LOOK; shover.Timer = 45; shover.Tick = 0; shover.Dir = dir;
            Say(shover, BubbleArt.Bang, 45);                                     // oops!
        }

        // A reluctant flipper-shake: they leave each other alone for a while.
        void MakeUp(Penguin a, Penguin b)
        {
            foreach (var p in new[] { a, b })
            {
                var o = p == a ? b : a;
                p.State = SHAKEHANDS; p.Tick = 0; p.Timer = 60;
                p.Dir = o.X >= p.X ? 1 : -1;
            }
            Say(a, BubbleArt.Question, 30);
            MakeTruce(a, b, 3000);
        }

        // Out of nowhere, a rival lobs a snowball.
        void TryAmbush()
        {
            foreach (var a in penguins)
            {
                if (!IsCore(a) || !Available(a)) continue;
                foreach (var b in penguins)
                {
                    if (b == a || !Rivals(a, b) || !b.OnFloor || b.Job != null || b.Base != null || Math.Abs(b.Y - a.Y) > 1 || !StandsOnSurface(b)) continue;
                    float d = Math.Abs(b.X - a.X);
                    if (d < 120 || d > 420) continue;
                    a.State = AMBUSH; a.Tick = 0; a.ThrowAnim = 22; a.Target = b;
                    a.Dir = b.X >= a.X ? 1 : -1;
                    return;
                }
            }
        }

        void ThrowAt(Penguin p, Penguin t)
        {
            const float T = 38f, grav = 0.25f;
            var b = new Snowball { X = p.X + p.Dir * 12, Y = p.Y - 38, Target = t, From = p };
            float tx = t.X + rnd.Next(-16, 17), ty = t.Y - 22;
            b.Vx = (tx - b.X) / T;
            b.Vy = (ty - b.Y) / T - 0.5f * grav * T;
            b.Win = new SpriteWindow(SnowArt.BallSize, SnowArt.BallSize, true);
            b.Win.Show();
            strays.Add(b);
        }

        void UpdateStrays()
        {
            for (int i = strays.Count - 1; i >= 0; i--)
            {
                var b = strays[i];
                b.Vy += 0.25f; b.X += b.Vx; b.Y += b.Vy;
                var t = b.Target;
                bool gone = false;
                if (t != null && penguins.Contains(t) && StandsOnSurface(t) && Math.Abs(b.X - t.X) <= 13 && Math.Abs(b.Y - (t.Y - 22)) <= 18)
                {   // splat!
                    if (t.Job == null && t.Base == null && t.Rider == null) { t.State = DAZED; t.Timer = 45; t.Tick = 0; t.LaughNext = true; }
                    Say(t, BubbleArt.Bang, 45);
                    var thrower = b.From;
                    if (thrower != null && (thrower.State == WALK || thrower.State == LOOK) && thrower.Job == null)
                    { thrower.State = DANCE; thrower.Timer = 40; thrower.Tick = 0; Say(thrower, BubbleArt.Note, 40); }   // got you!
                    gone = true;
                    if (!GroupBusy() && rnd.Next(4) == 0) StartFight();                 // it's on!
                }
                int floor = FloorAt((int)b.X);
                if (!gone && ((floor != int.MinValue && b.Y > floor) || b.Y > virt.Bottom || b.X < virt.Left || b.X > virt.Right))
                {
                    gone = true;
                    if (t != null && penguins.Contains(t)) Say(t, BubbleArt.Question, 40);   // missed: "huh?"
                }
                if (gone) { b.Win.Close(); strays.RemoveAt(i); continue; }
                b.Win.Draw(screenDC, memDC, ballHbm, (int)b.X - SnowArt.BallSize / 2, (int)b.Y - SnowArt.BallSize / 2);
            }
        }

        void ClearStrays()
        {
            foreach (var b in strays) b.Win.Close();
            strays.Clear();
        }

        // Walking right past a sleeping rival might wake it - grumpily.
        void WakeRival(Penguin p)
        {
            foreach (var q in penguins)
            {
                if (q.State != SLEEP || !Rivals(p, q) || q.OnFloor != p.OnFloor || q.Surf != p.Surf || Math.Abs(q.Y - p.Y) > 1) continue;
                if (Math.Abs(q.X - p.X) > 36 || rnd.Next(3) != 0) continue;
                Sulk(q, q.X >= p.X ? 1 : -1, p);
                return;
            }
        }

        // ---- friends

        void HighFive(Penguin a, Penguin b)
        {
            foreach (var p in new[] { a, b })
            {
                var other = p == a ? b : a;
                p.State = HIFIVE; p.Tick = 0; p.Timer = 42; p.HopOff = 0; p.HopV = 0;
                p.Dir = p.FaceDir = other.X >= p.X ? 1 : -1;
                p.NextDir = -p.Dir;                                              // then off on their way
            }
            Say(a, BubbleArt.Bang, 40);
        }

        void WalkTogether(Penguin a, Penguin b)
        {
            b.State = TOGETHER; b.Tick = 0; b.Timer = 300 + rnd.Next(300); b.Buddy = a;
            a.BumpCool = b.BumpCool = 400;
            Say(a, BubbleArt.Heart, 50); Say(b, BubbleArt.Heart, 50);
        }

        // A friend dozing nearby on the same floor? Go and nap beside it.
        bool TryNapWith(Penguin p)
        {
            if (!p.OnFloor || !IsCore(p)) return false;
            foreach (var q in penguins)
            {
                if (q == p || q.State != SLEEP || !q.OnFloor || Math.Abs(q.Y - p.Y) > 1 || ticks < q.NapUntil) continue;
                if (!Friends(p, q) || Math.Abs(q.X - p.X) > 300) continue;
                int side = p.X < q.X ? -1 : 1;
                q.NapUntil = ticks + 600;
                SendTo(p, q, q.X + side * 26, -side, NAPWITH);
                Say(p, BubbleArt.Heart, 45);
                return true;
            }
            return false;
        }

        // Someone started a belly-slide: their biggest fan tries it too (and usually botches it).
        void OnSlide(Penguin p)
        {
            foreach (var q in penguins)
            {
                if (!Family.Admires(q.Name, p.Name) || !Available(q) || Math.Abs(q.Y - p.Y) > 1 || Math.Abs(q.X - p.X) > 300) continue;
                q.State = LOOK; q.Timer = 25; q.Tick = 0; q.Dir = p.X >= q.X ? 1 : -1;
                q.PendingSlide = true; q.SlideDir = p.Dir; q.SlideFail = rnd.Next(10) < 6;
            }
        }

        // A flung penguin is coming down: a friend standing below runs to catch it.
        void FindCatcher(Penguin w)
        {
            foreach (var q in penguins)
            {
                if (q == w || !Friends(q, w) || !Available(q) || q.Y < w.Y + 40 || Math.Abs(q.X - w.X) > 260) continue;
                q.State = CATCH; q.Tick = 0; q.CatchWho = w; w.Catcher = q;
                Say(q, BubbleArt.Bang, 35);
                return;
            }
        }

        // Two walking penguins that meet face each other and wave, then part ways or slide off together.
        void CheckBumps()
        {
            for (int i = 0; i < penguins.Count; i++)
            {
                var a = penguins[i];
                if (a.State != WALK || a.BumpCool > 0 || a.HopOff != 0 || a.Job != null) continue;
                for (int j = i + 1; j < penguins.Count; j++)
                {
                    var b = penguins[j];
                    if (b.State != WALK || b.BumpCool > 0 || b.HopOff != 0 || b.Job != null) continue;
                    if (a.OnFloor != b.OnFloor || a.Surf != b.Surf || Math.Abs(a.Y - b.Y) > 1) continue;
                    float gap = b.X - a.X;
                    int range = Math.Max(a.Me.GreetRange, b.Me.GreetRange);           // the friendly ones say hi from further off
                    if (Math.Abs(gap) > range || gap * a.Dir <= 0 || -gap * b.Dir <= 0) continue;   // not approaching

                    if (Friends(a, b))
                    {
                        int r = rnd.Next(100);
                        if (r < 30) { HighFive(a, b); break; }
                        if (r < 60) { WalkTogether(a, b); break; }
                    }
                    if (Rivals(a, b)) { RivalMeet(a, b); break; }
                    bool together = Friends(a, b) || rnd.Next(100) < 30;      // friends always slide off as a pair
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
            var pz = parade;
            if (pz != null && pz.Stunt != 0 && p.Job == pz && (p.State == FOLLOW || (p.State == WALK && p == pz.Leader)))
            {
                int t = pz.StuntT, place = p == pz.Leader ? 0 : pz.Line.IndexOf(p) + 1;
                switch (pz.Stunt)
                {
                    case 1:
                        if (t < place * 6) return (p.Tick / 4) % 2 == 0 ? Art.WalkA : Art.WalkB;       // hurrying...
                        if (t < SlideLen + place * 6) return (ticks / 5) % 2 == 0 ? Art.Slide : Art.SlideB;   // ...wheee
                        return Art.ArmsUp;                                                              // pop back up
                    case 2:
                        if (p.HopOff < 0) return Art.Leap;
                        if (pz.Jumper != null && pz.LeapT < RoomLen)
                        {
                            if (p == pz.Jumper && pz.LeapT >= RoomLen - 8) return Art.ArmsUp;              // ready... set...
                            return Art.Stand;                                                              // making room
                        }
                        break;
                    default:
                        if (place == 0)
                        {
                            if (t < RushLen) return (p.Tick / 4) % 2 == 0 ? Art.WalkA : Art.WalkB;       // hurrying
                            if (t >= RushLen + 25 && t < PileLen - 20)
                            {   // Ziggy turns round to look... and laughs
                                drawDir = -p.Dir;
                                return t < RushLen + 45 ? Art.Stand : (t / 8) % 2 == 0 ? Art.PetA : Art.Stand;
                            }
                            return Art.Stand;
                        }
                        if (p.PileAt > 0)
                        {
                            if (t >= PileLen - 35) return (t / 9) % 2 == 0 ? Art.DazedA : Art.DazedB; // up again, seeing stars
                            return (t / 7) % 2 == 0 ? Art.PlantA : Art.PlantB;                       // splat
                        }
                        if (Math.Abs(p.X - p.PrevX) >= 0.3f) return (p.Tick / 4) % 2 == 0 ? Art.WalkA : Art.WalkB;
                        break;
                }
            }
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
                case SLEEP:
                    if (p.Pillow) return (p.Tick / 35) % 2 == 0 ? Art.PillowA : Art.PillowB;   // curled up on the pillow
                    return (p.Tick / 35) % 2 == 0 ? Art.SleepA : Art.SleepB;
                case SLIDE: return (p.Tick / 5) % 2 == 0 ? Art.Slide : Art.SlideB;
                case DANCE:
                    if ((p.Tick / 16) % 2 == 1) drawDir = -p.Dir;
                    return (p.Tick / 8) % 2 == 0 ? Art.ArmsUp : Art.WaveA;
                case HELD: return (p.Tick / 5) % 2 == 0 ? Art.ArmsUp : Art.FlyB;    // flailing
                case THROWN: return p.Spin ? Tumble(p.Tick / 4) : Art.ArmsUp;
                case CURIOUS:
                    if (Math.Abs(p.X - p.PrevX) > 0.3f) return (p.Tick / 7) % 2 == 0 ? Art.WalkA : Art.WalkB;   // Waddles, following you
                    if (p.GlareUntil > ticks)
                    {   // you flung my friend: "no, no, no"
                        if (p.Near) { drawDir = 1; return (p.Tick / 4) % 2 == 0 ? Art.ShakeL : Art.ShakeR; }
                        return Art.Stand;
                    }
                    if (p.Near) return (p.Tick / 7) % 2 == 0 ? Art.WaveA : Art.WaveB;
                    return Art.Stand;
                case RIDE:
                    if ((p.Tick / 50) % 3 == 2) return (p.Tick / 7) % 2 == 0 ? Art.WaveA : Art.WaveB;
                    return Art.Stand;
                case CLIMB: return (p.Tick / 8) % 2 == 0 ? Art.ClimbA : Art.ClimbB;
                case OFF: return Art.Stand;
                case PET: return (p.Tick / 14) % 2 == 0 ? Art.PetA : Art.PetB;    // nuzzling the cursor
                case HOPUP: return Art.ArmsUp;
                case HUG:
                {
                    var h = hug;
                    if (p.HopOff < 0) return Art.ArmsUp;
                    if (h == null) return Art.HugA;
                    if (h.Squeeze > 0)
                        return p.Wobbly ? ((p.Tick / 4) % 2 == 0 ? Art.BalanceA : Art.BalanceB) : Art.HugSqueeze;   // whoa / squeeeeze
                    if (p.HugNap) return Art.HugSleep;
                    if (p.Me.Leader && h.T % 300 >= 150 && h.T % 300 < 180)
                    { drawDir = 1; return (p.Tick / 4) % 2 == 0 ? Art.ShakeL : Art.ShakeR; }               // ok, ok... (he stays)
                    return (ticks / 20) % 2 == 0 ? Art.HugA : Art.HugB;                                     // swaying together
                }
                case HIFIVE: return p.HopOff < 0 ? Art.WaveA : Art.Stand;              // flipper up for the slap
                case TOGETHER:
                    if (Math.Abs(p.TargetX - p.X) < 2.5f) return Art.Stand;
                    return (p.Tick / 8) % 2 == 0 ? Art.WalkA : Art.WalkB;
                case NAPWITH: return Art.Stand;
                case CATCH: return (p.Tick / 4) % 2 == 0 ? Art.WalkA : Art.ArmsUp;     // running, arms out
                case RACE:
                    if (race == null || race.Phase == 0) return Art.Stand;            // on your marks...
                    if (p.Me.SlideMin >= 7f) return (p.Tick / 5) % 2 == 0 ? Art.Slide : Art.SlideB;   // Puddle races on her belly
                    return (p.Tick / 3) % 2 == 0 ? Art.WalkA : Art.WalkB;               // flat out
                case CHEER: return p.HopOff < 0 ? Art.ArmsUp : (p.Tick / 7) % 2 == 0 ? Art.WaveA : Art.WaveB;
                case STARE: return Art.Stand;
                case BLINK: return Art.Blink;
                case SULK: return (p.Tick / 14) % 2 == 0 ? Art.WalkA : Art.WalkB;
                case AMBUSH:
                    if (p.ThrowAnim > 8) return (p.ThrowAnim > 15) ? Art.BuildA : Art.WaveB;   // scoop, wind up...
                    return Art.FlyA;                                                    // ...throw
                case SHAKEOFF:
                    drawDir = 1;
                    return (p.Tick / 4) % 2 == 0 ? Art.ShakeL : Art.ShakeR;
                case SHAKEHANDS: return p.Tick < 30 ? Art.Stand : Art.WaveB;
                case GUARD: return Art.Stand;
                case TEASE: return Art.Tease;
                case MAKEUP: return Art.Stand;
                case GG:
                    if (p.HopOff < 0) return gg != null && gg.Phase == 2 ? Art.ArmsUp : Art.WaveA;   // high five down the line / all cheer
                    return Art.Stand;
                case HUDDLE:
                {
                    var hd = huddle;
                    if (Math.Abs(p.TargetX - p.X) > 1.5f) return (p.Tick / 12) % 2 == 0 ? Art.WalkA : Art.WalkB;   // shuffling
                    if (hd != null && hd.ShakeOff > 0)
                        return p.HopOff < 0 ? Art.ArmsUp : (hd.ShakeOff / 4) % 2 == 0 ? Art.ShakeL : Art.ShakeR;   // shaking off the snow
                    if (p.HopOff < 0) return Art.ArmsUp;                                // Pip popping up in the middle
                    if (p.Shiver > 0) return (p.Tick / 3) % 2 == 0 ? Art.ShiverA : Art.ShiverB;
                    if (hd != null && hd.Napper == p) return Art.Doze;
                    double sway = Math.Sin(ticks * 0.06);                              // everyone sways together
                    if (Math.Abs(sway) < 0.35) return Art.SnugM;
                    return (sway > 0) == (p.Dir == 1) ? Art.SnugB : Art.SnugA;
                }
                case PHOTO:
                {
                    var ph = photo;
                    if (ph == null || ph.Phase < 2) return Art.Stand;
                    if (ph.Phase == 2 && ph.PebbleNaps && p.Name == "Pebble") return Art.SleepA;   // asleep in the photo
                    return ph.Phase == 2 ? Art.WaveA : ((p.Tick / 7) % 2 == 0 ? Art.WaveA : Art.WaveB);
                }
                case BED: return (p.Tick / 35) % 2 == 0 ? Art.SleepA : Art.SleepB;
                case TUCK: return (p.Tick / 14) % 2 == 0 ? Art.PetA : Art.PetB;
                case BEDGUARD: return Art.Stand;
                case GOGGLE: return p.Tick < 16 ? Art.Adjust : Art.Crouch;
                case HATFIX: return p.Tick >= 25 && p.Tick < 48 ? Art.Adjust : Art.Stand;
                case SLEEPWALK: return (p.Tick / 12) % 2 == 0 ? Art.SleepWalkA : Art.SleepWalkB;
                case LISTEN:
                {
                    int k = (p.Tick / 9) % 4;
                    return k == 1 || k == 3 ? Art.SitNod : Art.Sit;               // sat down, nodding along
                }
                case IDLE: return IdleFrame(p);
                case PILLOW:
                    if (p.Tick < 56) return (p.Tick / 9) % 2 == 0 ? Art.FluffA : Art.FluffB;
                    if ((p.Tick >= 58 && p.Tick < 66) || (p.Tick >= 74 && p.Tick < 82)) return Art.Back;   // turning round
                    return Art.Stand;
                case GRUMP:
                    if (p.Tick <= 40) return p.Tick < 18 ? Art.YawnA : Art.YawnB;
                    if (Math.Abs(p.X - p.PrevX) < 0.2f) return Art.Grump;
                    return (p.Tick / 12) % 2 == 0 ? Art.GrumpA : Art.GrumpB;
                case SCARF: return p.Tick < 45 ? ((p.Tick / 7) % 2 == 0 ? Art.TypeA : Art.TypeB) : Art.Stand;
                case VISIT: return Art.Stand;
                case HOLD:
                    if (p.HoldPet) return (p.Tick / 14) % 2 == 0 ? Art.PetA : Art.PetB;
                    if (p.Buddy != null && Math.Abs(p.Buddy.X - p.X) < 80) return (p.Tick / 7) % 2 == 0 ? Art.WaveA : Art.WaveB;   // here they come!
                    return Art.Stand;
                case REPAIR: return (p.Tick / 6) % 2 == 0 ? Art.RepairA : Art.RepairB;
                case SHOWOFF:
                    if (p.Tick < 32)
                    {   // twirl: side, front, other side, back...
                        int k = (p.Tick / 4) % 4;
                        if (k == 1) return Art.Attention;
                        if (k == 3) return Art.Back;
                        if (k == 2) drawDir = -p.Dir;
                        return Art.Stand;
                    }
                    return Art.PoseB;                                                  // ta-da!
                case CLAP: return p.HopOff < 0 ? Art.ArmsUp : (p.Tick / 6) % 2 == 0 ? Art.WaveA : Art.WaveB;
                case ATTN:
                    switch (p.Attn)
                    {
                        case 1: return Art.Salute;
                        case 2: return Art.Doze;
                        case 3: return Art.PoseB;
                        case 4: return (p.Tick / 7) % 2 == 0 ? Art.WaveA : Art.WaveB;
                    }
                    return Art.Attention;
                case INSPECT:
                    if (Math.Abs(p.X - p.PrevX) > 0.3f) return (p.Tick / 7) % 2 == 0 ? Art.WalkA : Art.WalkB;
                    return p.Attn == 1 ? Art.Salute : Art.Stand;
                case CHECKPIP: return p.Tick < 64 ? ((p.Tick / 10) % 2 == 0 ? Art.PatA : Art.PatB) : Art.Stand;
                case SNEAK:
                    if (p.Timer > 0) return Art.Attention;                             // who, me?
                    return (p.Tick / 9) % 2 == 0 ? Art.TiptoeA : Art.TiptoeB;
                case ICEFISH:
                    switch (p.FishPhase)
                    {
                        case 0: return (p.FishT / 9) % 2 == 0 ? Art.BuildA : Art.BuildB;          // chipping at the ice
                        case 2: return (p.FishT / 4) % 2 == 0 ? Art.FishTug : Art.FishA;          // tug, tug!
                        case 3: if (p.FishT < 45) return Art.FishCatch;                            // look at this one!
                                return (p.FishT % 32) < 22 ? Art.SitEatA : Art.SitEatB;            // nom
                    }
                    if (p.FishNap) return Art.FishSleep;
                    return (p.FishT / 40) % 3 == 2 ? Art.FishB : Art.FishA;
                case FISHWATCH:
                {
                    if (p.HopOff < 0) return Art.ArmsUp;
                    var f = p.Job as Penguin;
                    if (f != null && f.FishNap) return (p.Tick / 35) % 2 == 0 ? Art.SitSleepA : Art.SitSleepB;   // dozing off too
                    if (f != null && f.FishPhase == 2) return Art.Sit;                                          // ooh!
                    return (p.Tick / 9) % 4 == 1 ? Art.SitNod : (p.Tick % 150 < 8 ? Art.SitBlink : Art.Sit);
                }
                case STARTLE:
                    if (p.HopOff < 0) return Art.ArmsUp;
                    return p.Tick < 40 ? Art.Stand : (p.Tick / 8) % 2 == 0 ? Art.PetA : Art.Stand;   // ...and laughs
                case GROOVE:
                    if (p == dj)
                    {   // Ziggy: on the decks... then a guitar solo
                        if ((grooveBeat / 16) % 2 == 1) return grooveBeat % 2 == 0 ? Art.GuitarA : Art.GuitarB;
                        return grooveBeat % 2 == 0 ? Art.TypeA : Art.TypeB;
                    }
                    if (p.Name == "Pebble") return grooveBeat % 2 == 0 ? Art.SwayL : Art.SwayR;     // a sleepy sway
                    if ((grooveBeat / 4) % 2 == 1) drawDir = -p.Dir;
                    switch ((grooveBeat + p.Name.Length) % 4) { case 0: case 2: return Art.ArmsUp; case 1: return Art.WaveA; default: return Art.WaveB; }
                case POSE: return p.PoseKind == 0 ? Art.PoseA : Art.PoseB;
                case PEEK: return p.Tick < 50 ? Art.Stand : (p.Tick / 7) % 2 == 0 ? Art.WaveA : Art.WaveB;
                case MOVIE:
                {
                    if (p.Dozing) return (p.Tick / 35) % 2 == 0 ? Art.SitSleepA : Art.SitSleepB;
                    if (p.HopOff < 0) return Art.ArmsUp;                                            // jumps up at the good bits
                    int ph = (p.Tick + p.Name.Length * 37) % 260;                                   // a fish now and then
                    if (ph < 70) return (ph % 32) < 22 ? Art.SitEatA : Art.SitEatB;
                    return ph % 120 < 8 ? Art.SitBlink : Art.Sit;
                }
                case DINE:
                case COOK:
                    if (p.Juggle > 0) return (p.Juggle / 6) % 2 == 0 ? Art.JuggleA : Art.JuggleB;   // whoa, whoa... got it
                    if (p.ThrowAnim > 0) return p.ThrowAnim > 8 ? Art.BuildA : Art.FlyA;    // scoop from the bucket... toss
                    if (p.State == DINE)
                    {   // sat at the table
                        if (p.Dozing) return (p.Tick / 35) % 2 == 0 ? Art.SitSleepA : Art.SitSleepB;
                        if (p.Fish > 0) return (p.EatT % 32) < 22 ? Art.SitEatA : Art.SitEatB;
                        if (dinner != null && dinner.Phase >= 2) return (p.Tick / 80) % 3 == 0 ? Art.Sit : Art.SitBlink;
                        return Art.Sit;
                    }
                    if (p.Dozing) return (p.Tick / 35) % 2 == 0 ? Art.SleepA : Art.SleepB;  // nodded off at the table
                    if (p.Fish > 0) return (p.EatT % 32) < 22 ? Art.EatA : Art.EatB;        // nom... gulp
                    if (dinner != null && dinner.Phase >= 2) return (p.Tick / 80) % 3 == 0 ? Art.Stand : Art.Blink;   // full and happy
                    return Art.Stand;
                case JEALOUS:
                    if ((p.Tick / 35) % 3 == 2) { drawDir = 1; return (p.Tick / 4) % 2 == 0 ? Art.ShakeL : Art.ShakeR; }   // stomp-stomp
                    return Art.Stand;
                case HELP: return Art.Stand;
                case SUMMON: return (p.Tick / 5) % 2 == 0 ? Art.FlyA : Art.FlyB;
                case FIGHT:
                    if (p.HitTimer > 0) return (p.HitTimer / 9) % 2 == 0 ? Art.DazedA : Art.DazedB;   // got one in the face
                    if (p.ThrowAnim > 8) return Art.WaveB;                                         // wind up...
                    if (p.ThrowAnim > 0) return Art.FlyA;                                          // ...and throw
                    if (p.ThrowCool < 30) return (p.Tick / 8) % 2 == 0 ? Art.BuildA : Art.BuildB;  // scooping up snow
                    return Art.Stand;
                case PARTY:
                {
                    if (party != null && p == party.Dj) return (ticks / 5) % 2 == 0 ? Art.TypeA : Art.TypeB;   // on the decks
                    int beat = ticks / 10;                                   // everyone on the same beat
                    if ((beat / 4) % 2 == 1) drawDir = -p.Dir;
                    switch (beat % 4) { case 0: case 2: return Art.ArmsUp; case 1: return Art.WaveA; default: return Art.WaveB; }
                }
                case FOLLOW:
                    if (Math.Abs(p.X - p.PrevX) < 0.3f) return Art.Stand;        // not moving: wait in line
                    return (p.Tick / 7) % 2 == 0 ? Art.WalkA : Art.WalkB;
                case GOTO:
                    if (p.NextState == HUG) return (p.Tick / 5) % 2 == 0 ? Art.HugRunA : Art.HugRunB;   // flippers wide open!
                    return (p.Tick / 7) % 2 == 0 ? Art.WalkA : Art.WalkB;
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
            if (p.Tiptoe) return (p.Tick / 9) % 2 == 0 ? Art.TiptoeA : Art.TiptoeB;
            return (p.Tick / 8) % 2 == 0 ? Art.WalkA : Art.WalkB;
        }

        // ---------- group activities ----------

        bool Available(Penguin p)
        {
            return p.OnFloor && (p.State == WALK || p.State == LOOK) && p.HopOff == 0
                && p.Job == null && p.Base == null && p.Rider == null;
        }

        // Up to `max` free penguins on the same stretch of floor, nearest to a random leader first.
        static bool Friends(Penguin a, Penguin b) { return Family.AreFriends(a.Name, b.Name); }
        bool Rivals(Penguin a, Penguin b) { return Family.AreRivals(a.Name, b.Name) && !Truce(a, b); }

        // making up: a rival pair that has made up leaves each other alone for a while
        readonly Dictionary<string, int> truceUntil = new Dictionary<string, int>();
        static string PairKey(Penguin a, Penguin b) { return string.CompareOrdinal(a.Name, b.Name) < 0 ? a.Name + "|" + b.Name : b.Name + "|" + a.Name; }
        bool Truce(Penguin a, Penguin b) { int t; return truceUntil.TryGetValue(PairKey(a, b), out t) && ticks < t; }
        void MakeTruce(Penguin a, Penguin b, int len) { truceUntil[PairKey(a, b)] = ticks + len; }

        // Split a group into two balanced teams: friends together, rivals apart.
        static void SplitTeams(List<Penguin> group, List<Penguin> left, List<Penguin> right)
        {
            int cap = (group.Count + 1) / 2;
            foreach (var p in group)
            {
                int sl = 0, sr = 0;
                foreach (var q in left) sl += Pull(p, q);
                foreach (var q in right) sr += Pull(p, q);
                bool toLeft;
                if (left.Count >= cap) toLeft = false;
                else if (right.Count >= cap) toLeft = true;
                else if (sl != sr) toLeft = sl > sr;
                else toLeft = left.Count <= right.Count;
                (toLeft ? left : right).Add(p);
            }
        }
        static int Pull(Penguin a, Penguin b)
        {
            if (Family.AreFriends(a.Name, b.Name)) return 2;
            if (Family.AreRivals(a.Name, b.Name)) return -3;
            return 0;
        }

        // Somewhere to stand that isn't busy (for helpers and comforters).
        static bool Free(Penguin p)
        {
            return p.OnFloor && p.Job == null && p.Base == null && p.Rider == null && p.HopOff == 0
                && (p.State == WALK || p.State == LOOK || p.State == WAVE || p.State == DANCE);
        }

        static int HeadOf(Penguin p) { return p.Me.Small ? (int)Math.Round(Art.HeadH * Art.SmallScale) : Art.HeadH; }

        static bool IsCore(Penguin p) { return Array.IndexOf(CoreNames, p.Name) >= 0; }
        static bool IsFrost(Penguin p) { return Array.IndexOf(FrostNames, p.Name) >= 0; }
        int OwnCount() { int n = 0; foreach (var q in penguins) if (!IsFrost(q)) n++; return n; }

        // The whole desktop, left to right: the family lives in the left third, the Frostbites in the right,
        // and the middle is shared ground.
        void Span(out float lo, out float hi)
        {
            lo = float.MaxValue; hi = float.MinValue;
            foreach (var wa in workAreas) { lo = Math.Min(lo, wa.Left); hi = Math.Max(hi, wa.Right); }
        }

        // Wandering about: don't stray too far into the other family's side.
        void HomeSide(Penguin p)
        {
            bool frost = IsFrost(p);
            if (p.Job != null || (!frost && !IsCore(p))) return;
            float lo, hi; Span(out lo, out hi);
            float third = (hi - lo) / 3;
            if (!frost && p.Dir == 1 && p.X > hi - third) p.Dir = -1;
            else if (frost && p.Dir == -1 && p.X < lo + third) p.Dir = 1;
        }

        static bool LoadFrostOn()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SettingsKey))
                    return k == null || !(k.GetValue("Frostbites") is int) || (int)k.GetValue("Frostbites") != 0;
            }
            catch { return true; }
        }

        static void SaveFrostOn(bool on)
        {
            try { using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(SettingsKey)) k.SetValue("Frostbites", on ? 1 : 0); }
            catch { }
        }

        // A core penguin can be called away from anything except being held or shooed.
        static bool Recruitable(Penguin p) { return IsCore(p) && p.State != HELD && p.State != OFF && (p.Job == null || p.Job is Penguin || p.Job is Inspection); }   // errands (helping, guarding...) give way to activities

        // The group for an activity: all of the core five, wherever they are, plus free extras
        // standing nearby on the same floor, up to `max`. group[0] is the one in charge.
        List<Penguin> PickGroup(int max, Penguin lead = null)
        {
            var core = penguins.FindAll(Recruitable);
            var free = penguins.FindAll(q => Available(q) && !IsCore(q) && !IsFrost(q));
            if (lead == null)
                lead = core.Find(q => q.Me.Leader && Available(q)) ?? core.Find(Available)        // Ziggy takes charge when free
                    ?? core.Find(q => q.Me.Leader) ?? (core.Count > 0 ? core[0] : null)
                    ?? (free.Count > 0 ? free[rnd.Next(free.Count)] : null);
            if (lead == null) return null;
            int fy = FloorAt((int)lead.X);
            if (fy == int.MinValue) fy = (int)lead.Y;
            var group = new List<Penguin>(core);
            group.Remove(lead);
            var extras = free.FindAll(q => q != lead && (int)q.Y == fy && Math.Abs(q.X - lead.X) < 900);
            extras.Sort((a, b) => Math.Abs(a.X - lead.X).CompareTo(Math.Abs(b.X - lead.X)));
            int room = Math.Max(0, max - group.Count - 1);
            if (extras.Count > room) extras.RemoveRange(room, extras.Count - room);
            group.AddRange(extras);
            group.Sort((a, b) => Math.Abs(a.X - lead.X).CompareTo(Math.Abs(b.X - lead.X)));
            group.Insert(0, lead);
            return group.Count >= 2 ? group : null;
        }

        Rectangle WorkAreaAt(float x)
        {
            foreach (var wa in workAreas) if (x >= wa.Left && x < wa.Right) return wa;
            return workAreas[0];
        }

        // Send a penguin to its spot for an activity: walk there along the floor if it can,
        // otherwise (on a window, in the air, another floor, busy riding...) fly straight over.
        void SendTo(Penguin p, object job, float x, int face, int then)
        {
            if (p.Base != null) { if (p.Base.Rider == p) p.Base.Rider = null; p.Base = null; }
            if (!(job is Penguin)) p.Visits = null;
            p.Job = job; p.Tick = 0; p.HopOff = 0; p.HopV = 0;
            p.TargetX = x; p.FaceDir = face; p.NextState = then;
            int fy = FloorAt((int)x);
            bool walk = p.OnFloor && StandsOnSurface(p) && p.State != CLIMB && fy != int.MinValue && Math.Abs(fy - (int)p.Y) <= 1;
            if (walk) { p.State = GOTO; p.Timer = IsCore(p) ? 300 : 900; }
            else Summon(p);
        }

        void Summon(Penguin p)
        {
            if (p.Base != null) { if (p.Base.Rider == p) p.Base.Rider = null; p.Base = null; }
            int fy = FloorAt((int)p.TargetX);
            p.SummonY = fy != int.MinValue ? fy : p.Y;
            p.HopFromX = p.X; p.HopFromY = p.Y;
            float dist = Math.Abs(p.TargetX - p.X) + Math.Abs(p.SummonY - p.Y);
            p.Timer = (int)Clamp(dist / 9f, 30, 130);
            p.State = SUMMON; p.Tick = 0;
            p.OnFloor = false; p.Surf = IntPtr.Zero; p.Umbrella = false; p.Spin = false; p.HopOff = 0; p.HopV = 0;
            Say(p, BubbleArt.Bang, 40);                                       // "coming!"
        }

        bool GroupBusy()
        {
            return (snow != null && snow.Stage < 4 && !snow.Melting)    // a finished snowman is just decoration
                || tug != null || parade != null || fight != null || tower != null || party != null || hug != null || race != null
                || gg != null || huddle != null || photo != null || bedtime != null || dinner != null || movie != null;
        }

        static float Clamp(float v, float lo, float hi) { return Math.Max(lo, Math.Min(hi, v)); }

        // ---- snowball fight

        void StartFight()
        {
            if (GroupBusy() || ticks < shooUntil) return;
            var group = PickGroup(8);
            if (group == null) return;
            var wa = WorkAreaAt(group[0].X);
            float c = Clamp(group[0].X, wa.Left + 230, wa.Right - 230);
            var f = new Fight { Timer = 900 };
            SplitTeams(group, f.Left, f.Right);                                  // friends together, rivals face off
            foreach (var team in new[] { f.Left, f.Right })
            {
                int side = team == f.Left ? -1 : 1;
                for (int i = 0; i < team.Count; i++)
                {
                    var p = team[i];
                    p.ThrowCool = 30 + rnd.Next(60); p.ThrowAnim = 0; p.HitTimer = 0;
                    SendTo(p, f, c + side * (90 + i * 36), -side, FIGHT);
                }
            }
            fight = f;
        }

        void Throw(Fight f, Penguin p, bool fromLeft)
        {
            if (p.Me.Accessory == Art.HardHat && rnd.Next(4) == 0) { p.HitTimer = 36; Say(p, BubbleArt.Bang, 40); return; }   // Wobble fumbles it: right on his own head
            var targets = (fromLeft ? f.Right : f.Left).FindAll(q => q.State == FIGHT || q.State == GOTO);
            if (targets.Count == 0) return;
            var grudges = targets.FindAll(q => Rivals(p, q));
            var t = grudges.Count > 0 && rnd.Next(10) < 7 ? grudges[rnd.Next(grudges.Count)] : targets[rnd.Next(targets.Count)];   // rivals first
            const float T = 38f, grav = 0.25f;
            var b = new Snowball { X = p.X + p.Dir * 12, Y = p.Y - 38, FromLeft = fromLeft };
            float tx = t.X + rnd.Next(-14, 15), ty = t.Y - 22;              // not everyone's a good shot
            b.Vx = (tx - b.X) / T;
            b.Vy = (ty - b.Y) / T - 0.5f * grav * T;
            b.Win = new SpriteWindow(SnowArt.BallSize, SnowArt.BallSize, true);
            b.Win.Show();
            f.Balls.Add(b);
        }

        void UpdateFight()
        {
            var f = fight;
            if (f == null) return;
            f.Left.RemoveAll(q => q.Job != f);
            f.Right.RemoveAll(q => q.Job != f);
            if (f.Left.Count == 0 || f.Right.Count == 0) { EndFight(null); return; }

            bool started = false;
            foreach (var team in new[] { f.Left, f.Right })
                foreach (var p in team)
                {
                    if (p.State != FIGHT) continue;
                    started = true;
                    if (p.HitTimer > 0) { p.HitTimer--; continue; }
                    if (p.ThrowAnim > 0) { if (--p.ThrowAnim == 8) Throw(f, p, team == f.Left); continue; }
                    if (--p.ThrowCool <= 0) { p.ThrowAnim = 16; p.ThrowCool = 45 + rnd.Next(70); }
                }

            for (int i = f.Balls.Count - 1; i >= 0; i--)
            {
                var b = f.Balls[i];
                b.Vy += 0.25f; b.X += b.Vx; b.Y += b.Vy;
                bool gone = false;
                foreach (var t in b.FromLeft ? f.Right : f.Left)
                {
                    if (t.State != FIGHT || Math.Abs(b.X - t.X) > 13 || Math.Abs(b.Y - (t.Y - 22)) > 18) continue;
                    t.HitTimer = 36; t.ThrowAnim = 0;                         // splat!
                    Say(t, BubbleArt.Bang, 40);
                    if (b.FromLeft) f.HitsL++; else f.HitsR++;
                    gone = true;
                    break;
                }
                int floor = FloorAt((int)b.X);
                if (!gone && ((floor != int.MinValue && b.Y > floor) || b.Y > virt.Bottom || b.X < virt.Left || b.X > virt.Right)) gone = true;
                if (gone) { b.Win.Close(); f.Balls.RemoveAt(i); continue; }
                b.Win.Draw(screenDC, memDC, ballHbm, (int)b.X - SnowArt.BallSize / 2, (int)b.Y - SnowArt.BallSize / 2);
            }

            if (started && --f.Timer <= 0)
            {
                QueueGoodGame(f.All());
                EndFight(f.HitsL > f.HitsR ? f.Left : f.HitsR > f.HitsL ? f.Right : null);
            }
        }

        void EndFight(List<Penguin> winners)
        {
            var f = fight;
            if (f == null) return;
            fight = null;
            foreach (var b in f.Balls) b.Win.Close();
            foreach (var p in f.All())
            {
                if (p.Job != f) continue;
                p.Job = null; p.Tick = 0; p.HitTimer = 0; p.ThrowAnim = 0;
                if (winners == null) { p.State = LOOK; p.Timer = 40; }
                else if (winners.Contains(p)) { p.State = DANCE; p.Timer = 90; Say(p, BubbleArt.Note); }
                else { p.State = DAZED; p.Timer = 60; }                       // snowed under
            }
            if (winners != null) foreach (var p in f.All()) if (!winners.Contains(p) && IsCore(p)) CallForHelp(p);
        }

        // ---- penguin tower

        void StartTower()
        {
            if (GroupBusy() || ticks < shooUntil) return;
            var crew = PickGroup(6);
            if (crew == null || crew.Count < 3) return;
            var lead = crew.Find(q => q.Me.Leader);
            var small = crew.Find(q => q.Me.Small);
            if (lead != null) { crew.Remove(lead); crew.Add(lead); }        // the leader lifts...
            if (small != null) { crew.Remove(small); crew.Add(small); }     // ...little brother up to the very top
            var wa = WorkAreaAt(crew[0].X);
            float site = Clamp(crew[0].X, wa.Left + 170, wa.Right - 100);
            var t = new Tower { Crew = crew, Site = site };
            for (int i = 0; i < crew.Count; i++)
                SendTo(crew[i], t, i == 0 ? site : site - 43 - (i - 1) * 31, 1, READY);   // the rest queue up behind
            tower = t;
        }

        void UpdateTower()
        {
            var t = tower;
            if (t == null) return;
            foreach (var p in t.Crew) if (p.Job != t) { EndTower(false); return; }   // someone was taken away
            var bottom = t.Crew[0];
            switch (t.Phase)
            {
                case 0:     // stacking
                    if (t.Built == 0) { if (bottom.State == READY) t.Built = 1; return; }
                    var next = t.Crew[t.Built];
                    if (next.State == READY)
                    {   // hop on!
                        next.State = HOPUP; next.Tick = 0; next.HopOnto = t.Crew[t.Built - 1];
                        next.HopFromX = next.X; next.HopFromY = next.Y;
                        next.OnFloor = false; next.Surf = IntPtr.Zero;
                    }
                    else if (next.State == RIDE)
                    {
                        if (++t.Built == t.Crew.Count)
                        {   // done: everyone cheers
                            t.Phase = 1; t.Timer = 200;
                            foreach (var p in t.Crew) Say(p, BubbleArt.Heart, 60);
                        }
                    }
                    return;
                case 1:     // admire it for a bit
                    if (--t.Timer <= 0) { t.Phase = 2; t.Timer = 80; Say(t.Crew[t.Crew.Count - 1], BubbleArt.Bang, 60); }
                    return;
                default:    // uh oh: it wobbles more and more, then over it goes
                    bottom.X = t.Site + (float)Math.Sin(t.Timer * 0.45) * (2 + (80 - t.Timer) * 0.09f);
                    if (--t.Timer <= 0) EndTower(true);
                    return;
            }
        }

        void EndTower(bool topple)
        {
            var t = tower;
            if (t == null) return;
            tower = null;
            if (topple) QueueGoodGame(t.Crew);
            int dir = rnd.Next(2) == 0 ? 1 : -1;
            for (int i = t.Crew.Count - 1; i >= 0; i--)
            {
                var p = t.Crew[i];
                if (p.Job != t) continue;
                p.Job = null; p.Tick = 0;
                if (!topple)
                {   // called off: riders hop down soon, the rest wander away
                    if (p.State == RIDE) p.Timer = 20 + rnd.Next(40);
                    else if (p.State == HOPUP) { p.HopOnto = null; StartFalling(p, false); }
                    else { p.State = LOOK; p.Timer = 40; }
                    continue;
                }
                if (i == 0) { p.X = t.Site; p.State = FACEPLANT; p.Timer = 60; continue; }
                Detach(p);
                p.State = THROWN; p.Spin = true; p.AfterLand = FACEPLANT;
                p.Vx = dir * (1.5f + i * 1.2f); p.Vy = -3f - i * 0.5f;
                p.OnFloor = false; p.Surf = IntPtr.Zero;
                Say(p, BubbleArt.Bang, 40);
            }
        }

        // ---- dance party

        void StartParty()
        {
            if (GroupBusy() || ticks < shooUntil) return;
            var crew = PickGroup(10);
            if (crew == null) return;
            var dj = crew.Find(q => q.Me.Leader);
            var wa = WorkAreaAt(crew[0].X);
            float c = Clamp(crew[0].X, wa.Left + 190, wa.Right - 190);
            var pa = new Party { Crew = crew, Dj = dj, Timer = 650 };
            if (dj != null) SendTo(dj, pa, c, 1, PARTY);
            var dancers = crew.FindAll(q => q != dj);
            var lft = new List<Penguin>(); var rgt = new List<Penguin>();
            SplitTeams(dancers, lft, rgt);                                       // friends dance side by side
            foreach (var row in new[] { lft, rgt })
            {
                int side = row == lft ? -1 : 1;
                for (int i = 0; i < row.Count; i++)
                    SendTo(row[i], pa, c + side * (dj != null ? 6 + (i + 1) * 36 : (i + 1) * 36 - 18), -side, PARTY);
            }
            Say(crew[0], BubbleArt.Note);
            party = pa;
        }

        void UpdateParty()
        {
            var pa = party;
            if (pa == null) return;
            pa.Crew.RemoveAll(q => q.Job != pa);
            if (pa.Crew.Count < 2) { EndParty(); return; }
            if (!pa.Crew.Exists(q => q.State == PARTY)) return;                     // still gathering
            foreach (var p in pa.Crew)
                if (p.State == PARTY && p != pa.Dj) p.HopOff = (ticks / 10) % 2 == 0 ? -3 : 0;   // bounce on the beat
            if (ticks % 45 == 0)
            {
                var q = pa.Crew[rnd.Next(pa.Crew.Count)];
                if (q.State == PARTY) Say(q, BubbleArt.Note, 40);
            }
            if (--pa.Timer <= 0) EndParty();
        }

        void EndParty()
        {
            var pa = party;
            if (pa == null) return;
            party = null;
            foreach (var p in pa.Crew)
            {
                if (p.Job != pa) continue;
                p.Job = null; p.HopOff = 0; p.Tick = 0;
                p.State = LOOK; p.Timer = 40;
                Say(p, BubbleArt.Heart, 50);
            }
        }

        // ---- family moments

        // Order a crew left to right by where they are, with Pip in the middle.
        static List<Penguin> PipInMiddle(List<Penguin> crew)
        {
            var order = new List<Penguin>(crew);
            order.Sort((a, b) => a.X.CompareTo(b.X));
            var pip = order.Find(q => q.Me.Small);
            if (pip != null) { order.Remove(pip); order.Insert(order.Count / 2, pip); }
            return order;
        }

        // ---- good game!

        void QueueGoodGame(List<Penguin> who) { ggPending = new List<Penguin>(who); ggAt = ticks + 120; }

        void StartGoodGame(List<Penguin> who)
        {
            var crew = who.FindAll(q => penguins.Contains(q) && q.State != HELD && q.State != OFF && (q.Job == null || q.Job is Penguin));
            if (crew.Count < 2) return;
            crew.Sort((a, b) => a.X.CompareTo(b.X));
            float avg = 0; foreach (var q in crew) avg += q.X; avg /= crew.Count;
            var wa = WorkAreaAt(avg);
            float half = (crew.Count - 1) * 15f;
            float c = Clamp(avg, wa.Left + half + 40, wa.Right - half - 40);
            var g = new GoodGame { Crew = crew, Timer = 400 };
            for (int i = 0; i < crew.Count; i++) SendTo(crew[i], g, c - half + i * 30, 1, GG);
            gg = g;
        }

        void UpdateGoodGame()
        {
            if (ggPending != null && ticks >= ggAt)
            {
                var list = ggPending; ggPending = null;
                if (!GroupBusy()) StartGoodGame(list);
            }
            var g = gg;
            if (g == null) return;
            g.Crew.RemoveAll(q => q.Job != g);
            if (g.Crew.Count < 2) { EndGoodGame(); return; }
            switch (g.Phase)
            {
                case 0:     // lining up
                    if (g.Crew.TrueForAll(q => q.State == GG) || --g.Timer <= 0) { g.Phase = 1; g.T = 0; }
                    return;
                case 1:     // high fives ripple down the line
                    g.T++;
                    for (int i = 0; i < g.Crew.Count; i++)
                        if (g.T == i * 9 + 1 && g.Crew[i].State == GG)
                        {
                            g.Crew[i].HopV = -3.4f;
                            Say(g.Crew[i], i % 2 == 0 ? BubbleArt.Heart : BubbleArt.Note, 40);
                        }
                    if (g.T > g.Crew.Count * 9 + 20) { g.Phase = 2; g.T = 0; }
                    return;
                default:    // and everyone cheers together
                    if (g.T++ == 0) foreach (var q in g.Crew) if (q.State == GG) { q.HopV = -4f; Say(q, BubbleArt.Heart, 50); }
                    if (g.T > 45) EndGoodGame();
                    return;
            }
        }

        void EndGoodGame()
        {
            var g = gg;
            if (g == null) return;
            gg = null;
            foreach (var q in g.Crew)
                if (q.Job == g) { q.Job = null; q.HopOff = 0; q.HopV = 0; q.Tick = 0; q.State = LOOK; q.Timer = 30; }
        }

        // ---- family huddle

        void StartHuddle()
        {
            if (GroupBusy() || ticks < shooUntil) return;
            var crew = PickGroup(12);
            if (crew == null) return;
            var wa = WorkAreaAt(crew[0].X);
            float c = Clamp(crew[0].X, wa.Left + 140, wa.Right - 140);
            var h = new Huddle { Order = PipInMiddle(crew), Center = c, Timer = 1100 };
            for (int i = 0; i < h.Order.Count; i++)
            {
                float x = HuddleSlot(h, i);
                SendTo(h.Order[i], h, x, x <= c ? 1 : -1, HUDDLE);
            }
            Say(crew[0], BubbleArt.Heart, 50);
            huddle = h;
        }

        static float HuddleSlot(Huddle h, int i) { return h.Center + (i - (h.Order.Count - 1) / 2f) * 15f; }

        void UpdateHuddle()
        {
            var h = huddle;
            if (h == null) return;
            h.Order.RemoveAll(q => q.Job != h);
            if (h.Napper != null && h.Napper.Job != h) h.Napper = null;
            if (h.Order.Count < 2) { EndHuddle(); return; }
            bool settled = h.Order.TrueForAll(q => q.State == HUDDLE && Math.Abs(q.TargetX - q.X) <= 1.5f);
            if (settled) h.T++;
            if (h.ShakeOff > 0)
            {   // all done: shake the snow off, a little hop, and off we go
                h.ShakeOff--;
                if (h.ShakeOff == 18)
                    foreach (var q in h.Order)
                    {
                        q.HopV = -2.8f;
                        for (int k = 0; k < 3; k++)
                            SpawnFloaty(2, q.X, q.Y - HeadOf(q) + 4, (float)(rnd.NextDouble() * 3 - 1.5), -1.6f - (float)rnd.NextDouble(), 0.08f, 45, 40);
                        if (rnd.Next(2) == 0) Say(q, BubbleArt.Heart, 45);
                    }
                if (h.ShakeOff == 0) EndHuddle();
                return;
            }
            if (--h.Timer <= 0)
            {
                if (h.T < 120) { EndHuddle(); return; }
                h.ShakeOff = 40;
                foreach (var q in h.Order) q.Shiver = 0;
                if (h.Napper != null) { Say(h.Napper, BubbleArt.Bang, 40); h.Napper = null; }   // huh? oh, we're done
                return;
            }
            if (settled && h.T > 30)
            {
                float floor = h.Order[0].Y, span = h.Order.Count * 9 + 70;
                if (ticks % 11 == 0)                     // snow drifting down around the huddle
                    SpawnFloaty(2, h.Center + (float)(rnd.NextDouble() * 2 - 1) * span, floor - 125 - rnd.Next(40),
                                0, 0.5f, 0, 240, 40);
                if (ticks % 45 == 0)
                {   // warm breath in the cold air
                    var q = h.Order[rnd.Next(h.Order.Count)];
                    if (q != h.Napper)
                        SpawnFloaty(3, q.X + q.Dir * HeadOf(q) * 0.3f, q.Y - HeadOf(q) * 0.72f, q.Dir * 0.25f, -0.35f, 0, 45, 40);
                }
                var pip = h.Order.Find(q => q.Me.Small);
                if (pip != null && h.T % 260 == 130 && pip.HopOff == 0)
                {   // Pip pops up in the middle for a look round
                    pip.HopV = -3.4f;
                    Say(pip, BubbleArt.Heart, 45);
                    SpawnHeart(pip.X, pip.Y - HeadOf(pip) - 6);
                }
                if (h.Napper == null && h.T == 320)
                {
                    var peb = h.Order.Find(q => q.Name == "Pebble");
                    if (peb != null) { h.Napper = peb; peb.Shiver = 0; Say(peb, BubbleArt.Zzz, 60); }   // so warm in here...
                }
            }
            if (h.Timer % 160 == 50 && settled)
            {   // whoever is on the cold outside starts to shiver... their turn to go in next
                var outside = h.Order[h.FromLeft ? 0 : h.Order.Count - 1];
                if (!outside.Me.Small && outside != h.Napper) outside.Shiver = 50;
            }
            if (h.Timer % 160 == 0 && settled)
            {   // the one on the outside shuffles in next to the middle
                int end = h.FromLeft ? 0 : h.Order.Count - 1;
                var outside = h.Order[end];
                if (!outside.Me.Small && outside != h.Napper)
                {
                    h.Order.RemoveAt(end);
                    h.Order.Insert(h.Order.Count / 2 + (h.FromLeft ? 1 : 0), outside);
                }
                h.FromLeft = !h.FromLeft;
                var pip = h.Order.Find(q => q.Me.Small);
                if (pip != null) { h.Order.Remove(pip); h.Order.Insert(h.Order.Count / 2, pip); }   // Pip stays in the warm middle
                for (int i = 0; i < h.Order.Count; i++)
                {
                    var q = h.Order[i];
                    q.TargetX = HuddleSlot(h, i);
                    q.FaceDir = q.TargetX <= h.Center ? 1 : -1;
                }
            }
            if (settled && ticks % 110 == 0)
            {
                var q = h.Order[rnd.Next(h.Order.Count)];
                if (q != h.Napper && q.Shiver == 0) Say(q, BubbleArt.Heart, 40);
            }
        }

        void EndHuddle()
        {
            var h = huddle;
            if (h == null) return;
            huddle = null;
            foreach (var q in h.Order)
                if (q.Job == h) { q.Job = null; q.Tick = 0; q.Shiver = 0; q.HopOff = 0; q.HopV = 0; q.State = LOOK; q.Timer = 30 + rnd.Next(30); }
        }

        // ---- family photo

        void StartPhoto()
        {
            if (GroupBusy() || ticks < shooUntil) return;
            var crew = PickGroup(12);
            if (crew == null) return;
            var wa = WorkAreaAt(crew[0].X);
            float c = Clamp(crew[0].X, wa.Left + 220, wa.Right - 220);
            var ph = new Photo { Crew = crew, Timer = 450, PebbleNaps = rnd.Next(2) == 0 };
            var z = crew.Find(q => q.Me.Leader);
            var pip = crew.Find(q => q.Me.Small);
            var rest = crew.FindAll(q => q != z && q != pip);
            rest.Sort((a, b) => (IsCore(b) ? 1 : 0).CompareTo(IsCore(a) ? 1 : 0));   // family nearest the middle
            float mid = c;
            if (z != null) SendTo(z, ph, c, -1, PHOTO);                              // Ziggy in the middle...
            if (pip != null) SendTo(pip, ph, c - 24, 1, PHOTO);                       // ...little brother by his side
            for (int i = 0; i < rest.Count; i++)
            {
                int side = i % 2 == 0 ? 1 : -1, rank = i / 2 + 1;
                float x = mid + side * rank * 32 + (side < 0 && pip != null ? -24 : 0);
                SendTo(rest[i], ph, x, -side, PHOTO);
            }
            photo = ph;
        }

        void UpdatePhoto()
        {
            var ph = photo;
            if (ph == null) return;
            ph.Crew.RemoveAll(q => q.Job != ph);
            if (ph.Crew.Count < 2) { EndPhoto(); return; }
            switch (ph.Phase)
            {
                case 0:     // gathering
                    if (ph.Crew.TrueForAll(q => q.State == PHOTO) || --ph.Timer <= 0)
                    {
                        ph.Phase = 1; ph.Timer = 70;
                        var z = ph.Crew.Find(q => q.Me.Leader) ?? ph.Crew[0];
                        Say(z, BubbleArt.Bang, 60);                                    // "everyone, say cheese!"
                    }
                    return;
                case 1:     // holding still...
                    if (--ph.Timer <= 0) { ph.Phase = 2; ph.Timer = 18; ShowFlash(ph); }
                    return;
                case 2:     // FLASH
                    if (ph.Flash != null)
                        ph.Flash.Draw(screenDC, memDC, ph.FlashBmp, ph.FX, ph.FY, (byte)(230 * ph.Timer / 18));
                    if (--ph.Timer <= 0)
                    {
                        HideFlash(ph);
                        ph.Phase = 3; ph.Timer = 70;
                        foreach (var q in ph.Crew)
                            if (q.Name == "Pebble" && ph.PebbleNaps) Say(q, BubbleArt.Bang, 50);   // huh? was that the photo?
                            else { Say(q, BubbleArt.Heart, 55); if (rnd.Next(2) == 0) q.HopV = -3f; }
                    }
                    return;
                default:    // everyone happy
                    if (--ph.Timer <= 0) EndPhoto();
                    return;
            }
        }

        void ShowFlash(Photo ph)
        {
            float lo = float.MaxValue, hi = float.MinValue, floorY = 0;
            foreach (var q in ph.Crew) { lo = Math.Min(lo, q.X); hi = Math.Max(hi, q.X); floorY = q.Y; }
            int w = (int)(hi - lo) + 160, h = 150;
            using (var b = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(b)) g.Clear(Color.White);
                ph.FlashBmp = Art.ToHBitmap(b);
            }
            ph.Flash = new SpriteWindow(w, h, true);
            ph.Flash.Show();
            ph.FX = (int)lo - 80; ph.FY = (int)floorY - h + 10;
        }

        void HideFlash(Photo ph)
        {
            if (ph.Flash != null) { ph.Flash.Close(); ph.Flash = null; }
            if (ph.FlashBmp != IntPtr.Zero) { Native.DeleteObject(ph.FlashBmp); ph.FlashBmp = IntPtr.Zero; }
        }

        void EndPhoto()
        {
            var ph = photo;
            if (ph == null) return;
            photo = null;
            HideFlash(ph);
            foreach (var q in ph.Crew)
                if (q.Job == ph) { q.Job = null; q.HopOff = 0; q.HopV = 0; q.Tick = 0; q.State = LOOK; q.Timer = 30; }
        }

        // ---- family dinner

        void StartDinner()
        {
            if (GroupBusy() || ticks < shooUntil) return;
            var crew = PickGroup(12);
            if (crew == null) return;
            var cook = crew.Find(q => q.Name == "Waddles") ?? crew[0];
            var diners = crew.FindAll(q => q != cook);
            if (diners.Count == 0) return;
            diners.Sort((a, b) => a.X.CompareTo(b.X));
            var z = diners.Find(q => q.Me.Leader);
            var pip = diners.Find(q => q.Me.Small);
            if (z != null && pip != null) { diners.Remove(z); diners.Insert(diners.IndexOf(pip) + 1, z); }   // Ziggy sits next to his little brother
            var wa = WorkAreaAt(cook.X);
            float rowW = (diners.Count - 1) * 26f;
            float left = Clamp(cook.X + 60, wa.Left + 110, wa.Right - rowW - 40);
            var d = new Dinner { Crew = diners, Cook = cook, Timer = 450, PebbleNaps = rnd.Next(100) < 70 };
            d.BX = left - 40;                                                          // bucket between the cook and the table
            foreach (var q in crew) { q.Fish = 0; q.EatT = 0; q.Dozing = false; q.ThrowAnim = 0; q.Juggle = 0; }
            SendTo(cook, d, left - 70, 1, COOK);
            for (int i = 0; i < diners.Count; i++) SendTo(diners[i], d, left + i * 26, -1, DINE);
            Say(cook, BubbleArt.Bang, 60);                                             // "dinner's ready!"
            foreach (var q in diners) if (rnd.Next(2) == 0) Say(q, BubbleArt.Heart, 45);
            dinner = d;
            lastDinner = ticks;
        }

        void TossFish(Dinner d, Penguin from, Penguin to)
        {
            const int T = 28;
            var b = new Snowball { X = from.X + from.Dir * 10, Y = from.Y - 34, Target = to, From = from, Flight = T };
            b.Vx = (to.X - b.X) / T;
            b.Vy = (to.Y - 30 - b.Y) / T - 0.5f * 0.25f * T;
            b.Right = b.Vx >= 0;
            b.Win = new SpriteWindow(SnowArt.FishW, SnowArt.FishH, true);
            b.Win.Show();
            d.Fish.Add(b);
        }

        void UpdateDinner()
        {
            var d = dinner;
            if (d == null) return;
            d.Crew.RemoveAll(q => q.Job != d);
            bool cookHere = d.Cook.Job == d;
            if (d.Crew.Count == 0 || (!cookHere && d.Phase < 2) || ++d.Age > 2600) { EndDinner(); return; }

            for (int i = d.Fish.Count - 1; i >= 0; i--)
            {   // fish in the air
                var b = d.Fish[i];
                b.Vy += 0.25f; b.X += b.Vx; b.Y += b.Vy;
                if (++b.Life >= b.Flight)
                {
                    var t = b.Target;
                    if (t != null && t.Job == d)
                    {
                        if (t.Me.Accessory == Art.HardHat && rnd.Next(100) < 60) { t.Juggle = 44; Say(t, BubbleArt.Bang, 35); }   // Wobble fumbles it...
                        else { t.Fish++; Say(t, rnd.Next(2) == 0 ? BubbleArt.Heart : BubbleArt.Note, 35); }
                    }
                    b.Win.Close(); d.Fish.RemoveAt(i); continue;
                }
                b.Win.Draw(screenDC, memDC, fishHbm[b.Right ? 1 : 0], (int)b.X - SnowArt.FishW / 2, (int)b.Y - SnowArt.FishH / 2);
            }
            if (d.Bucket != null)
                d.Bucket.Draw(screenDC, memDC, bucketHbm, (int)d.BX - SnowArt.BucketW / 2, d.BY - SnowArt.BucketH + 2);

            // eating
            bool eating = false;
            var eaters = new List<Penguin>(d.Crew);
            if (cookHere) eaters.Add(d.Cook);
            foreach (var q in eaters)
            {
                if (q.Juggle > 0) { eating = true; if (--q.Juggle == 0) { q.Fish++; Say(q, BubbleArt.Note, 35); } continue; }   // ...got it!
                if (q.Fish <= 0 || q.Dozing) continue;
                eating = true;
                q.EatT++;
                if (d.PebbleNaps && q.Name == "Pebble" && q.EatT == 60) { q.Dozing = true; Say(q, BubbleArt.Zzz, 70); continue; }   // ...zzz
                if (q.EatT >= 130)
                {
                    q.Fish--; q.EatT = 0;
                    if (q.Fish == 0) Say(q, q.Me.Small ? BubbleArt.Note : BubbleArt.Heart, 45);
                }
            }
            foreach (var q in eaters) if (q.Dozing && q.Tick % 200 == 100) Say(q, BubbleArt.Zzz, 60);

            // Ziggy takes a look at his fish... and gives it to Pip
            if (!d.Shared)
            {
                var z = d.Crew.Find(q => q.Me.Leader);
                var pip = d.Crew.Find(q => q.Me.Small);
                if (z == null || pip == null) d.Shared = true;
                else if (z.Fish > 0 && z.EatT == 25)
                {
                    d.Shared = true;
                    z.Fish--; z.EatT = 0;
                    z.Dir = pip.X >= z.X ? 1 : -1;
                    TossFish(d, z, pip);
                    Say(z, BubbleArt.Heart, 55);
                }
            }

            switch (d.Phase)
            {
                case 0:     // everyone to the table
                    if ((d.Cook.State == COOK && d.Crew.TrueForAll(q => q.State == DINE)) || --d.Timer <= 0)
                    {
                        if (d.Cook.State != COOK) { EndDinner(); return; }
                        d.Phase = 1; d.T = 0; d.Serve = 0;
                        d.BY = (int)d.Cook.Y;
                        d.Bucket = new SpriteWindow(SnowArt.BucketW, SnowArt.BucketH, true);
                        d.Bucket.Show();
                    }
                    return;
                case 1:     // the cook hands out fish, one each, down the row
                {
                    var c = d.Cook;
                    d.T++;
                    if (c.ThrowAnim > 0)
                    {
                        if (--c.ThrowAnim == 8 && d.Serve < d.Crew.Count) TossFish(d, c, d.Crew[d.Serve++]);
                    }
                    else if (d.Serve < d.Crew.Count && d.T % 30 == 0) c.ThrowAnim = 16;
                    if (d.Serve >= d.Crew.Count && c.ThrowAnim == 0 && d.Fish.Count == 0)
                    { c.Fish = 1; c.EatT = 0; d.Phase = 2; d.T = 0; }                         // and one for the cook
                    return;
                }
                case 2:     // eating
                    d.T++;
                    if (!eating && d.Fish.Count == 0 && d.Shared && d.T > 30)
                    {
                        d.Phase = 3; d.T = 0;
                        foreach (var q in eaters)
                            if (q.Dozing) { q.Dozing = false; q.Fish = 0; Say(q, BubbleArt.Bang, 50); }   // huh? dinner's over?
                            else Say(q, BubbleArt.Heart, 50);
                    }
                    return;
                default:    // full tummies
                    if (++d.T > 70) EndDinner();
                    return;
            }
        }

        void EndDinner()
        {
            var d = dinner;
            if (d == null) return;
            dinner = null;
            foreach (var b in d.Fish) b.Win.Close();
            d.Fish.Clear();
            if (d.Bucket != null) { d.Bucket.Close(); d.Bucket = null; }
            var all = new List<Penguin>(d.Crew);
            if (!all.Contains(d.Cook)) all.Add(d.Cook);
            foreach (var q in all)
            {
                bool dozed = q.Dozing;
                q.Fish = 0; q.EatT = 0; q.Dozing = false; q.ThrowAnim = 0; q.Juggle = 0;
                if (q.Job != d) continue;
                q.Job = null; q.Tick = 0;
                if (dozed) { q.State = YAWN; q.Timer = 60; }
                else { q.State = LOOK; q.Timer = 30 + rnd.Next(20); }
            }
        }

        // ---- little personal habits

        // What's on this penguin's head right now: Pip's propeller spins, Puddle's goggles come down, Wobble's hat goes crooked.
        int AccessoryNow(Penguin p)
        {
            int a = p.Me.Accessory;
            bool lent = scarfOnPip && IsCore(p);
            if (a == Art.Propeller)
            {
                bool fast = p.State == FLY || p.State == SUMMON || p.State == FALL || p.State == RACE || p.State == THROWN;
                bool spin = fast || (Math.Abs(p.X - p.PrevX) > 0.3f && !p.Tiptoe && p.State != SNEAK) || p.HopOff < 0
                    || p.State == DANCE || p.State == GROOVE || p.State == CHEER || p.State == PARTY || p.State == GG
                    || p.State == POSE || p.State == WAVE || p.State == SLIDE || p.State == IDLE || p.State == SHOWOFF;
                int v = a;
                if (spin)
                {
                    int ph = (fast ? ticks : ticks / 2) % 4;
                    v = ph == 0 ? Art.Propeller : ph == 2 ? Art.PropC : Art.PropB;
                }
                return lent ? v - Art.Propeller + Art.PipScarf : v;                // in Waddles' scarf
            }
            if (a == Art.Scarf && lent) return Art.NoOutfit;                       // lent it to Pip
            if (a == Art.Goggles && (ticks < p.GogglesUntil || (p.State == RACE && p.Me.SlideMin >= 7f))) return Art.GogglesDown;
            if (a == Art.HardHat && p.HatAskew) return Art.HatTilt;
            return a;
        }

        // A sprite, drawing the accessory variants the first time they're needed.
        IntPtr Sprite(int o, int d, int f)
        {
            if (hbm[o, d, f] == IntPtr.Zero)
                using (var b = Art.Render(f, o))
                {
                    hbm[o, 0, f] = Art.ToHBitmap(b);
                    using (var m = (Bitmap)b.Clone())
                    {
                        if (Art.Mirrorable(f)) m.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        hbm[o, 1, f] = Art.ToHBitmap(m);
                    }
                }
            return hbm[o, d, f];
        }

        int IdleFrame(Penguin p)
        {
            int t = p.Tick;
            switch (IdleName(p))
            {
                case "Pebble":  return t % 70 < 55 ? Art.Doze : Art.Stand;                       // nodding off... jerk awake
                case "Waddles": return (t / 16) % 2 == 0 ? Art.HumA : Art.HumB;                  // humming, swaying
                case "Wobble":  return (t / 10) % 2 == 0 ? Art.BalanceA : Art.BalanceB;          // whoa, whoa
                case "Puddle":  return t > 12 && t < 70 ? Art.Stretch : Art.Stand;               // a big stretch
                case "Ziggy":   { int k = (t / 8) % 4; return k == 1 ? Art.TapA : k == 3 ? Art.Nod : Art.Stand; }   // foot tapping
                case "Pip":     return p.HopOff < 0 ? Art.ArmsUp : Art.Stand;                    // bouncing
                case "Bubbles": return t % 90 < 60 ? Art.Salute : Art.Attention;                 // keeping a lookout
                case "Juniper": return (t / 7) % 2 == 0 ? Art.KnitA : Art.KnitB;                 // knitting
                case "Bolt":    return p.HopOff < 0 ? Art.ArmsUp : Art.Stand;                    // star jumps, warming up
                case "Doodle":  return t < 26 ? Art.Adjust : Art.Tease;                          // beret just so... hmph
                case "Mochi":   return (t / 12) % 2 == 0 ? Art.EatA : Art.EatB;                  // a little snack
                case "Pearl":   return p.HopOff < 0 ? Art.WaveA : Art.Stand;                     // bouncing
            }
            return Art.Stand;
        }

        // Wobble walks smack into the wall. Hat goes crooked.
        bool Bonk(Penguin p)
        {
            if (p.Me.Accessory != Art.HardHat || p.State != WALK || p.Job != null || rnd.Next(3) != 0) return false;
            p.HatAskew = true;
            p.FixWall = true; p.FaceDir = -p.Dir;              // the wall's that way
            p.State = DAZED; p.Timer = 30; p.Tick = 0;
            Say(p, BubbleArt.Bang, 40);
            return true;
        }

        // ---- more little habits

        static string IdleName(Penguin p) { return p.CopyName ?? p.Name; }

        Penguin Core(string name) { return penguins.Find(q => q.Name == name); }
        Penguin Leader() { return penguins.Find(q => q.Me.Leader && IsCore(q)); }
        Penguin Little() { return penguins.Find(q => q.Me.Small && IsCore(q)); }

        static bool SameFloor(Penguin a, Penguin b) { return a.OnFloor && b.OnFloor && Math.Abs(a.Y - b.Y) <= 1; }

        // Someone's in the middle of a moment of their own: don't start a group activity on top of it.
        bool PersonalMoment()
        {
            if (inspect != null) return true;
            foreach (var p in penguins)
            {
                if (!IsCore(p)) continue;
                if (p.Visits != null) return true;
                switch (p.State)
                {
                    case PET: case HIFIVE: case TOGETHER: case NAPWITH: case STARE: case BLINK: case SULK: case TEASE: case MAKEUP:
                    case SHAKEHANDS: case CATCH: case HELP: case HOPUP: case AMBUSH: case JEALOUS: case GOGGLE: case HATFIX: case SLEEPWALK:
                    case PILLOW: case GRUMP: case SCARF: case VISIT: case REPAIR: case SHOWOFF: case CLAP:
                    case CHECKPIP: case SNEAK: case STARTLE: case HOLD: case ICEFISH: case FISHWATCH:
                        return true;
                    case RIDE: if (p.Timer < 100000) return true; break;               // a piggyback ride
                    case GOTO: if (p.Job is Penguin) return true; break;              // on the way to a friend
                }
            }
            return false;
        }

        // Stand still and wait for someone who's on the way over.
        void Wait(Penguin p, Penguin visitor, float dist)
        {
            p.State = HOLD; p.Tick = 0; p.Buddy = visitor; p.HoldPet = false;
            p.FaceDir = p.Dir = visitor.X >= p.X ? 1 : -1;
            p.Timer = (int)Clamp(dist / Math.Max(1f, visitor.Speed + 0.6f), 0, 400) + 90;
        }

        // Pebble's bedtime ritual: find a spot (next to Waddles, if she's about), fluff the pillow, turn round twice... zzz
        void PillowTime(Penguin p)
        {
            var w = Core("Waddles");
            if (w != null && w != p && SameFloor(w, p) && w.Job == null && StandsOnSurface(w) && Math.Abs(w.X - p.X) < 500 && Math.Abs(w.X - p.X) > 40)
            {
                int side = p.X < w.X ? -1 : 1;
                SendTo(p, w, w.X + side * 30, -side, PILLOW);
                return;
            }
            p.State = PILLOW; p.Tick = 0; p.FaceDir = p.Dir;
        }

        // Waddles lends Pip her scarf for a while...
        void TryScarf()
        {
            if (scarfOnPip || ticks - scarfSince < 4000) return;
            var w = Core("Waddles");
            var pip = Little();
            if (w == null || pip == null || !Available(w) || !Available(pip) || !SameFloor(w, pip)) return;
            float dx = Math.Abs(w.X - pip.X);
            if (dx > 600) return;
            int side = w.X < pip.X ? -1 : 1;
            SendTo(w, pip, pip.X + side * 24, -side, SCARF);
            Wait(pip, w, dx);
            Say(w, BubbleArt.Hearts, 50);
        }

        // ...and Pip brings it back.
        void ScarfBack()
        {
            if (!scarfOnPip) return;
            var w = Core("Waddles");
            var pip = Little();
            if (w == null || pip == null || ticks - scarfSince > 7000) { scarfOnPip = false; scarfSince = ticks; return; }   // it found its way home
            if (ticks - scarfSince < 1800 || GroupBusy() || !Available(pip) || !SameFloor(w, pip) || w.Job != null || w.Rider != null) return;
            if (!(w.State == WALK || w.State == LOOK || w.State == IDLE)) return;
            float dx = Math.Abs(w.X - pip.X);
            if (dx > 700) return;
            int side = pip.X < w.X ? -1 : 1;
            SendTo(pip, w, w.X + side * 24, -side, SCARF);
            Wait(w, pip, dx);
        }

        // Waddles does the rounds: a high five (or a hug, for Pebble) for everyone nearby.
        void TryRounds()
        {
            var w = Core("Waddles");
            if (w == null || !Available(w)) return;
            var list = penguins.FindAll(q => q != w && IsCore(q) && SameFloor(q, w) && q.Job == null && q.Rider == null && q.Base == null
                && Math.Abs(q.X - w.X) < 800 && (q.State == WALK || q.State == LOOK || q.State == IDLE || q.State == SLEEP));
            if (list.Count < 2) return;
            var route = new List<Penguin>();
            float x = w.X;
            while (list.Count > 0)
            {   // the nearest one, then the nearest to that one...
                Penguin best = null;
                foreach (var q in list) if (best == null || Math.Abs(q.X - x) < Math.Abs(best.X - x)) best = q;
                route.Add(best); list.Remove(best); x = best.X;
            }
            w.Visits = route;
            Say(w, BubbleArt.Hearts, 60);
            NextVisit(w);
        }

        void NextVisit(Penguin w)
        {
            while (w.Visits != null && w.Visits.Count > 0)
            {
                var q = w.Visits[0];
                w.Visits.RemoveAt(0);
                if (!penguins.Contains(q) || q.Job != null || !SameFloor(q, w) || Math.Abs(q.X - w.X) > 900) continue;
                if (!(q.State == WALK || q.State == LOOK || q.State == IDLE || q.State == SLEEP || q.State == HOLD)) continue;
                int side = w.X < q.X ? -1 : 1;
                float dx = Math.Abs(q.X - w.X);
                var rest = w.Visits;
                SendTo(w, q, q.X + side * 26, -side, VISIT);
                w.Visits = rest;
                if (q.State != SLEEP) Wait(q, w, dx);
                return;
            }
            w.Visits = null;
            w.State = WAVE; w.Timer = 45; w.Tick = 0;                            // that's everyone
            Say(w, BubbleArt.Hearts, 50);
        }

        // Wobble goes to fix up the snowman.
        void TryFixSnowman()
        {
            var s = snow;
            if (s == null || s.Stage != 4 || s.Melting) return;
            var w = penguins.Find(q => q.Me.Accessory == Art.HardHat && IsCore(q));
            if (w == null || !Available(w)) return;
            int fy = FloorAt((int)s.X);
            if (fy == int.MinValue || Math.Abs(fy - (int)w.Y) > 1 || Math.Abs(s.X - w.X) > 800) return;
            int side = w.X < s.X ? -1 : 1;
            SendTo(w, w, s.X + side * 30, -side, REPAIR);
            Say(w, BubbleArt.Wrench, 45);
        }

        // Puddle, after a good slide: a twirl... and ta-da! Her biggest fan applauds.
        void ShowOff(Penguin p)
        {
            p.State = SHOWOFF; p.Tick = 0; p.Timer = 95; p.FaceDir = p.Dir;
            foreach (var q in penguins)
            {
                if (!Family.Admires(q.Name, p.Name) || !Available(q) || !SameFloor(q, p) || Math.Abs(q.X - p.X) > 350) continue;
                q.State = CLAP; q.Tick = 0; q.Timer = 85; q.FaceDir = q.Dir = p.X >= q.X ? 1 : -1;
            }
        }

        bool SleeperNear(Penguin p)
        {
            foreach (var q in penguins)
                if (q != p && (q.State == SLEEP || q.State == BED || q.State == NAPWITH) && SameFloor(q, p) && Math.Abs(q.X - p.X) < 75) return true;
            return false;
        }

        // ---- ice fishing

        const int HoleOff = 24;     // pixels from the penguin's middle to the hole's (matches Art.HoleX)

        // Someone free on the floor sits down to fish right where they are. Pebble and Waddles like it most.
        void TryFishing()
        {
            var pool = new List<Penguin>();
            foreach (var q in penguins)
            {
                if (!IsCore(q) || !Available(q) || q.Rider != null) continue;
                pool.Add(q);
                if (q.Name == "Pebble" || q.Name == "Waddles") pool.Add(q);
            }
            if (pool.Count == 0) return;
            var p = pool[rnd.Next(pool.Count)];
            int y = (int)p.Y;
            for (int k = HoleOff - 14; k <= HoleOff + 14; k += 7)
                if (FloorAt((int)p.X + p.Dir * k) != y) { p.Dir = -p.Dir; break; }    // no flat ice that way: face the other way
            for (int k = HoleOff - 14; k <= HoleOff + 14; k += 7)
                if (FloorAt((int)p.X + p.Dir * k) != y) return;
            p.State = ICEFISH; p.Tick = 0; p.FaceDir = p.Dir;
            p.FishPhase = 0; p.FishT = 0; p.Catches = 0; p.FishNap = false;
            p.Timer = p.Me.Leader ? 260 : 500 + rnd.Next(350);
            p.Hole = new FishHole { Win = new SpriteWindow(SnowArt.HoleW, SnowArt.HoleH, true) };
            p.Hole.Win.Show();

            // the little brother comes to watch (or Waddles does, if it's Pip fishing)
            var fan = p.Me.Small ? Core("Waddles") : Little();
            if (fan != null && fan != p && Available(fan) && SameFloor(fan, p) && Math.Abs(fan.X - p.X) < 500)
            {
                float sx = p.X - p.Dir * 26;
                int fy = FloorAt((int)sx);
                if (fy == y) SendTo(fan, p, sx, p.Dir, FISHWATCH);
            }
        }

        void EndFishing(Penguin p, bool happy)
        {
            CloseHole(p);
            p.FishPhase = 0; p.FishT = 0; p.FishNap = false; p.Tick = 0;
            if (happy) { p.State = DANCE; p.Timer = 45; Say(p, BubbleArt.Note, 40); }    // a good day's fishing
            else p.State = WALK;
        }

        void CloseHole(Penguin p)
        {
            if (p.Hole == null) return;
            p.Hole.Win.Close();
            p.Hole = null;
        }

        void DrawHole(Penguin p)
        {
            var h = p.Hole;
            if (h == null) return;
            int kind = 1, a = 255;
            switch (p.FishPhase)
            {
                case 0: kind = 0; a = Math.Min(255, 40 + p.FishT * 5); break;            // the hole opening up
                case 2: kind = (p.FishT / 4) % 2 == 0 ? 2 : 1; break;                    // splashing
                case 3: kind = 0; break;                                                  // fish out, line in
                default: if (!p.FishNap && (p.FishT / 40) % 3 == 2) kind = 2; break;      // a nibble
            }
            int x = (int)p.X + p.FaceDir * HoleOff - SnowArt.HoleW / 2, y = (int)p.Y - SnowArt.HoleH / 2;
            if (kind != h.Key || x != h.X || y != h.Y || a != h.A)
            {
                h.Win.Draw(screenDC, memDC, holeHbm[kind], x, y, (byte)a);
                h.Key = kind; h.X = x; h.Y = y; h.A = a;
            }
        }

        // Ziggy wanders over to see how his little brother's doing.
        void TryCheckPip()
        {
            var z = Leader();
            var pip = Little();
            if (z == null || pip == null || !Available(z) || !SameFloor(z, pip) || pip.Job != null || pip.Rider != null || pip.Base != null) return;
            if (!(pip.State == WALK || pip.State == LOOK || pip.State == IDLE || pip.State == SLEEP)) return;
            float dx = Math.Abs(z.X - pip.X);
            if (dx < 200 || dx > 1000) return;                                  // close by already / too far to bother
            int side = z.X < pip.X ? -1 : 1;
            SendTo(z, pip, pip.X + side * 16, -side, CHECKPIP);
            if (pip.State != SLEEP) Wait(pip, z, dx);
            Say(z, BubbleArt.Question, 45);                                      // where's Pip got to?
        }

        // Ziggy has his headphones on... Pip sneaks up on him.
        void TrySneak()
        {
            var z = Leader();
            var pip = Little();
            if (z == null || pip == null || !Available(pip) || !SameFloor(z, pip) || z.Job != null || z.Rider != null || z.Base != null) return;
            if (!(z.State == LISTEN || z.State == IDLE || z.State == LOOK)) return;
            float dx = Math.Abs(z.X - pip.X);
            if (dx < 50 || dx > 450) return;
            z.State = LISTEN; z.Tick = 0;
            z.Timer = Math.Max(z.Timer, Math.Min(700, (int)(dx / 0.8f) + 160));
            z.Dir = pip.X < z.X ? 1 : -1;                                        // looking the other way
            Say(z, BubbleArt.Phones, 50);
            pip.State = SNEAK; pip.Tick = 0; pip.Job = z; pip.Timer = 0; pip.Attn = 0;
        }

        // Ziggy calls the troops to attention and walks down the line.
        void TryInspect()
        {
            var z = Leader();
            if (z == null || !Available(z)) return;
            var troops = penguins.FindAll(q => q != z && IsCore(q) && Available(q) && SameFloor(q, z) && Math.Abs(q.X - z.X) < 700);
            if (troops.Count < 3) return;
            troops.Sort((a, b) => Math.Abs(a.X - z.X).CompareTo(Math.Abs(b.X - z.X)));
            var wa = WorkAreaAt(z.X);
            float len = 46 + 40 * troops.Count;
            int dir = z.Dir;
            if (z.X + dir * len > wa.Right - 30 || z.X + dir * len < wa.Left + 30) dir = -dir;
            if (z.X + dir * len > wa.Right - 30 || z.X + dir * len < wa.Left + 30) return;   // no room for a line here
            for (int i = 0; i < troops.Count; i++)
            {
                int fy = FloorAt((int)(z.X + dir * (46 + 40 * i)));
                if (fy == int.MinValue || Math.Abs(fy - (int)z.Y) > 1) return;
            }
            var ins = new Inspection { Chief = z, Dir = dir };
            for (int i = 0; i < troops.Count; i++)
            {
                var q = troops[i];
                q.Attn = 0;
                SendTo(q, ins, z.X + dir * (46 + 40 * i), -dir, ATTN);
                ins.Line.Add(q);
            }
            z.Job = ins; z.State = INSPECT; z.Tick = 0; z.Dir = dir; z.Attn = 0;
            Say(z, BubbleArt.Bang, 50);                                          // fall in!
            inspect = ins;
        }

        void UpdateInspect()
        {
            var ins = inspect;
            if (ins == null) return;
            var z = ins.Chief;
            ins.Line.RemoveAll(q => q.Job != ins);
            if (z.Job != ins || z.State != INSPECT || ins.Line.Count == 0 || GroupBusy() || ++ins.Age > 2400) { EndInspect(); return; }
            ins.T++;
            switch (ins.Phase)
            {
                case 0:   // falling in
                    if (ins.Line.TrueForAll(q => q.State == ATTN) || ins.T > 420)
                    {
                        foreach (var q in ins.Line) if (q.State != ATTN) { q.Job = null; q.State = WALK; q.Tick = 0; }
                        ins.Line.RemoveAll(q => q.Job != ins);
                        if (ins.Line.Count == 0) { EndInspect(); return; }
                        ins.Line.Sort((a, b) => ((a.X - z.X) * ins.Dir).CompareTo((b.X - z.X) * ins.Dir));
                        ins.Todo = new List<Penguin>(ins.Line);
                        ins.Phase = 1; ins.T = 0; ins.Walking = true;
                    }
                    break;
                case 1:   // down the line, one at a time
                {
                    while (ins.Todo.Count > 0 && ins.Todo[0].Job != ins && ins.Walking) ins.Todo.RemoveAt(0);
                    if (ins.Todo.Count == 0) { ins.Phase = 2; ins.T = 0; z.Attn = 0; z.Dir = -ins.Dir; Say(z, BubbleArt.Bang, 45); break; }   // dismissed!
                    var q = ins.Todo[0];
                    if (ins.Walking)
                    {
                        float gx = q.X - ins.Dir * 22 - z.X;
                        if (Math.Abs(gx) > 1.5f) { z.Dir = gx > 0 ? 1 : -1; Move(z, Math.Min(Math.Abs(gx), 1.3f)); ins.T = 0; break; }
                        ins.Walking = false; ins.T = 0; z.Dir = ins.Dir;
                    }
                    InspectOne(ins, z, q);
                    if (ins.T >= 60) { if (q.Job == ins) q.Attn = 0; z.Attn = 0; ins.Todo.RemoveAt(0); ins.Walking = true; ins.T = 0; }
                    break;
                }
                case 2:   // dismissed: a happy hop, and off they go
                    if (ins.T == 18) foreach (var q in ins.Line) q.HopV = -3.5f;
                    if (ins.T >= 50) EndInspect();
                    break;
            }
        }

        // How each of them stands up to inspection.
        void InspectOne(Inspection ins, Penguin z, Penguin q)
        {
            int t = ins.T;
            if (t == 1) z.Attn = 1;                                              // a salute
            if (q.Job != ins) return;
            if (q.Name == "Pebble")
            {   // asleep on his feet... !! awake, sir
                if (t == 1) { q.Attn = 2; Say(q, BubbleArt.Zzz, 40); }
                if (t == 28) { z.Attn = 0; Say(z, BubbleArt.Bang, 30); }
                if (t == 34) { q.Attn = 1; q.HopV = -3f; Say(q, BubbleArt.Bang, 30); }
            }
            else if (q.Me.Accessory == Art.HardHat)
            {   // a smart salute... and over he goes
                if (t == 8) q.Attn = 1;
                if (t == 26 && rnd.Next(100) < 60)
                {
                    q.Job = null; q.Attn = 0; q.State = FACEPLANT; q.Timer = 45; q.Tick = 0;
                    Say(q, BubbleArt.Bang, 40); Say(z, BubbleArt.Question, 35);
                }
                else if (t == 44) Say(z, BubbleArt.Note, 30);
            }
            else if (q.Me.Small)
            {   // the littlest soldier
                if (t == 6) { q.Attn = 1; q.HopV = -3.2f; }
                if (t == 40) Say(z, BubbleArt.Heart, 40);
            }
            else if (q.Me.SlideMin >= 7f)
            {   // strikes a pose instead
                if (t == 10) { q.Attn = 3; Say(q, BubbleArt.Star, 40); }
                if (t == 40) Say(z, BubbleArt.Question, 35);
            }
            else if (q.Name == "Waddles")
            {   // can't help waving
                if (t == 10) { q.Attn = 4; Say(q, BubbleArt.Hearts, 40); }
                if (t == 44) Say(z, BubbleArt.Note, 30);
            }
            else
            {
                if (t == 8) q.Attn = 1;
                if (t == 44) Say(z, BubbleArt.Note, 30);
            }
        }

        void EndInspect()
        {
            var ins = inspect;
            if (ins == null) return;
            inspect = null;
            bool done = ins.Phase == 2;
            foreach (var q in ins.Line)
            {
                if (q.Job != ins) continue;
                q.Job = null; q.Attn = 0; q.HopOff = 0; q.HopV = 0; q.Tick = 0;
                if (done) { q.State = DANCE; q.Timer = 35; Say(q, BubbleArt.Note, 35); }
                else q.State = WALK;
            }
            var z = ins.Chief;
            if (z.Job == ins)
            {
                z.Job = null; z.Attn = 0; z.Tick = 0;
                if (done) { z.State = DANCE; z.Timer = 50; Say(z, BubbleArt.Note, 40); }
                else z.State = WALK;
            }
        }

        // ---- dancing to your music

        bool CanGroove(Penguin p)
        {
            return p.Job == null && p.Base == null && p.Rider == null && p.HopOff == 0 && StandsOnSurface(p)
                && (p.State == WALK || p.State == LOOK || p.State == WAVE || p.State == CURIOUS);
        }

        void SenseMusic()
        {
            float v = AudioMeter.Level(ticks);
            loud[loudI] = v > 0.02f; loudI = (loudI + 1) % loud.Length;
            int n = 0;
            foreach (bool b in loud) if (b) n++;
            loudCount = n;
            if (v > 0.02f) lastLoudTick = ticks;
            if (!music && n >= 70 && movie == null && !videoFg && !GroupBusy() && ticks >= shooUntil) StartMusic();   // sound for most of ~3 s
            else if (music && (n <= 10 || movie != null)) StopMusic();                               // ~2.5 s of quiet

            bool beat = v > 0.04f && v > slowLevel * 1.3f && ticks - lastBeat >= 8;
            slowLevel = slowLevel * 0.96f + v * 0.04f;
            if (music && !beat && ticks - lastBeat > 22) beat = true;                                 // keep the groove going in quiet bits
            if (beat) { lastBeat = ticks; if (music) OnBeat(v); }

            if (music && ticks % 15 == 0)
            {   // anyone free joins in; Ziggy takes the decks
                foreach (var q in penguins) if (CanGroove(q)) { q.State = GROOVE; q.Tick = 0; }
                if (dj == null || dj.State != GROOVE) dj = penguins.Find(q => q.Me.Leader && q.State == GROOVE);
            }
            if (music && ticks % 90 == 45)
            {
                var g = penguins.FindAll(q => q.State == GROOVE);
                if (g.Count > 0) Say(g[rnd.Next(g.Count)], BubbleArt.Note, 45);
            }
        }

        void StartMusic()
        {
            music = true; dj = null;
            foreach (var q in penguins) if (CanGroove(q)) { q.State = GROOVE; q.Tick = 0; Say(q, BubbleArt.Note, 40); }
            dj = penguins.Find(q => q.Me.Leader && q.State == GROOVE);
        }

        void StopMusic()
        {
            music = false; dj = null;
            var g = penguins.FindAll(q => q.State == GROOVE);
            foreach (var q in g) { q.State = LOOK; q.Timer = 30 + rnd.Next(30); q.Tick = 0; q.HopOff = 0; q.HopV = 0; }
            if (g.Count > 0) Say(g[rnd.Next(g.Count)], BubbleArt.Heart, 50);
        }

        void OnBeat(float v)
        {
            grooveBeat++;
            float lift = 1f + Math.Min(1f, v * 1.6f) * 2.2f;                                          // louder = bigger bounces
            foreach (var p in penguins)
            {
                if (p.State != GROOVE || p == dj || p.HopOff != 0 || p.Name == "Pebble") continue;
                if (p.Name == "Wobble" && rnd.Next(90) == 0)
                {   // danced a bit too hard
                    p.State = FACEPLANT; p.Timer = 50; p.Tick = 0;
                    Say(p, BubbleArt.Bang, 40);
                    continue;
                }
                p.HopV = -lift * (p.Me.Small ? 1.25f : 1f);
            }
        }

        // ---- movie time

        static readonly string[] Players = {
            "vlc", "mpc-hc", "mpc-hc64", "mpc-be", "mpc-be64", "potplayer", "potplayer64", "potplayermini", "potplayermini64",
            "wmplayer", "mpv", "smplayer", "kmplayer", "gom", "video.ui", "microsoft.media.player", "plex", "kodi", "stremio" };

        static readonly string[] Browsers = {
            "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "arc", "zen", "librewolf", "waterfox", "floorp", "chromium" };
        static readonly string[] VideoSites = {
            "YouTube", "Netflix", "Twitch", "Disney+", "Prime Video", "Crunchyroll", "Hulu", "Vimeo", "bilibili", "Dailymotion" };

        static string TitleOf(IntPtr h)
        {
            var sb = new StringBuilder(256);
            Native.GetWindowText(h, sb, 256);
            return sb.ToString();
        }

        // A browser showing a video site. Only the active tab's title is checked, for these site names.
        bool IsVideoSite(IntPtr h)
        {
            if (Array.IndexOf(Browsers, ProcessOf(h)) < 0) return false;
            string t = TitleOf(h);
            if (t.IndexOf("YouTube Music", StringComparison.OrdinalIgnoreCase) >= 0) return false;   // that's music: dance!
            foreach (var site in VideoSites)
                if (t.IndexOf(site, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        string ProcessOf(IntPtr h)
        {
            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            string n;
            if (procNames.TryGetValue(pid, out n)) return n;
            try { n = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant(); }
            catch { n = ""; }
            if (procNames.Count > 200) procNames.Clear();
            procNames[pid] = n;
            return n;
        }

        // Only the app's name (and, for Store apps, the window title) is looked at.
        bool IsMediaPlayer(IntPtr h)
        {
            string n = ProcessOf(h);
            if (Array.IndexOf(Players, n) >= 0) return true;
            if (n == "applicationframehost")
            {
                string t = TitleOf(h);
                return t.Contains("Media Player") || t.Contains("Movies & TV") || t.Contains("Films & TV");
            }
            return false;
        }

        void SenseMedia()
        {
            var fg = Native.GetForegroundWindow();
            Rectangle r;
            if (fg == IntPtr.Zero || !GetRect(fg, out r) || r.Width < 200 || r.Height < 150) { videoFg = false; return; }
            bool player = IsMediaPlayer(fg), site = !player && IsVideoSite(fg);
            videoFg = player || site;
            var m = movie;
            if (m != null)
            {   // still watching? (same window, and for a browser still on the video site)
                if (fg == m.Win && (!m.Browser || site)) m.LastSeen = ticks;
                if (m.Browser && MediaSessions.Playing(ProcessOf(m.Win))) lastPlayingTick = ticks;
                return;
            }
            if (GroupBusy() || ticks < movieCool || ticks < shooUntil) return;
            if (player) StartMovie(fg, r, false);
            else if (site && (MediaSessions.Playing(ProcessOf(fg)) || loudCount >= 45))
            {   // a video's actually playing (muted or not)
                lastPlayingTick = ticks;
                StartMovie(fg, r, true);
            }
        }

        void StartMovie(IntPtr h, Rectangle r, bool browser)
        {
            var crew = PickGroup(12);
            if (crew == null) return;
            if (music) StopMusic();
            var order = PipInMiddle(crew);
            var z = order.Find(q => q.Me.Leader);
            var pip = order.Find(q => q.Me.Small);
            if (z != null && pip != null) { order.Remove(z); order.Insert(order.IndexOf(pip) + 1, z); }   // big brother next to Pip
            float c = r.Left + r.Width / 2f;
            var wa = WorkAreaAt(c);
            float half = (order.Count - 1) * 12f;
            c = Clamp(c, wa.Left + half + 40, wa.Right - half - 40);
            var m = new Movie { Crew = order, Win = h, LastSeen = ticks, Browser = browser };
            for (int i = 0; i < order.Count; i++)
            {
                order[i].Dozing = false;
                float x = c - half + i * 24;
                SendTo(order[i], m, x, x <= c ? 1 : -1, MOVIE);
            }
            Say(order[0], BubbleArt.Bang, 50);                                                       // ooh, a movie!
            movie = m;
        }

        void UpdateMovie()
        {
            var m = movie;
            if (m == null) return;
            m.Crew.RemoveAll(q => q.Job != m);
            m.Age++;
            Rectangle r;
            bool paused = m.Browser && ticks - lastLoudTick > 900 && ticks - lastPlayingTick > 900;   // paused / finished for ~30 s
            if (m.Crew.Count == 0 || !GetRect(m.Win, out r) || ticks - m.LastSeen > 300 || m.Age > 7200 || paused) { EndMovie(); return; }
            if (m.Age % 170 == 0)
            {   // reacting to the film
                var q = m.Crew[rnd.Next(m.Crew.Count)];
                if (q.State == MOVIE && !q.Dozing)
                    switch (rnd.Next(4))
                    {
                        case 0: Say(q, BubbleArt.Bang, 45); q.HopV = -3.5f; break;                 // whoa!
                        case 1: Say(q, BubbleArt.Heart, 45); break;
                        case 2: Say(q, BubbleArt.Question, 40); break;
                        default: Say(q, BubbleArt.Note, 40); break;
                    }
            }
            if (m.Age == 1500)
            {   // Pebble never makes it to the end
                var pb = m.Crew.Find(q => q.Name == "Pebble" && q.State == MOVIE);
                if (pb != null) { pb.Dozing = true; Say(pb, BubbleArt.Zzz, 70); }
            }
        }

        void EndMovie()
        {
            var m = movie;
            if (m == null) return;
            movie = null;
            movieCool = ticks + 3600;                                                                  // ~2 minutes before the next showing
            foreach (var q in m.Crew)
            {
                bool dozed = q.Dozing;
                q.Dozing = false; q.HopOff = 0; q.HopV = 0;
                if (q.Job != m) continue;
                q.Job = null; q.Tick = 0;
                if (dozed) { q.State = YAWN; q.Timer = 60; }
                else { q.State = LOOK; q.Timer = 30 + rnd.Next(30); if (rnd.Next(2) == 0) Say(q, BubbleArt.Heart, 40); }
            }
        }

        // ---- a new window: someone wanders over to have a look

        void CheckNewWindows()
        {
            var fresh = new List<WinInfo>();
            foreach (var w in wins) if (!knownWins.Contains(w.H)) fresh.Add(w);
            knownWins.Clear();
            foreach (var w in wins) knownWins.Add(w.H);
            if (ticks < 60 || fresh.Count == 0 || fresh.Count > 2) return;                           // first look round / switching desktops
            if (GroupBusy() || music || ticks - lastPeek < 450 || ticks < shooUntil) return;
            var nw = fresh[0];
            uint pid;
            Native.GetWindowThreadProcessId(nw.H, out pid);
            if (pid == myPid) return;
            float cx = nw.R.Left + nw.R.Width / 2f;
            Penguin best = null;
            float bd = 900;
            foreach (var p in penguins)
            {
                if (!Available(p)) continue;
                float d = Math.Abs(p.X - cx);
                if (d < bd) { bd = d; best = p; }
            }
            if (best == null) return;
            var wa = WorkAreaAt(cx);
            bool fromLeft = best.X < cx;
            float x = fromLeft ? nw.R.Left - 20 : nw.R.Right + 20;
            if (x < wa.Left + 30 || x > wa.Right - 30) x = fromLeft ? nw.R.Left + 40 : nw.R.Right - 40;
            x = Clamp(x, wa.Left + 30, wa.Right - 30);
            int fy = FloorAt((int)x);
            if (fy == int.MinValue || Math.Abs(fy - (int)best.Y) > 1) return;                        // only strolls over along the floor
            lastPeek = ticks;
            SendTo(best, peekJob, x, x < cx ? 1 : -1, PEEK);
        }

        // ---- screenshot: everyone poses

        void Pose()
        {
            if (ticks - lastPose < 60) return;
            lastPose = ticks;
            foreach (var p in penguins)
            {
                bool free = p.Job == null && p.Base == null && p.Rider == null && p.HopOff == 0 && StandsOnSurface(p)
                    && (p.State == WALK || p.State == LOOK || p.State == CURIOUS || p.State == WATCH || p.State == SWAY
                        || p.State == WAVE || p.State == DANCE || p.State == GROOVE);
                if (!free) continue;
                p.State = POSE; p.Tick = 0; p.Timer = 75; p.HopV = 0;
                p.PoseKind = p.Me.Small ? 1 : rnd.Next(2);
                if (rnd.Next(3) == 0) Say(p, BubbleArt.Heart, 45);
            }
        }

        // ---- bedtime

        void StartBedtime(bool idle)
        {
            if (GroupBusy() || ticks < shooUntil) return;
            var crew = PickGroup(12);
            if (crew == null) return;
            var order = PipInMiddle(crew);
            var z = order.Find(q => q.Me.Leader);
            var kind = order.Find(q => q.Name == "Waddles");
            var pip = order.Find(q => q.Me.Small);
            if (z != null) { order.Remove(z); order.Add(z); }                           // Ziggy on the end, keeping watch
            if (kind != null && pip != null) { order.Remove(kind); order.Insert(order.IndexOf(pip) + 1, kind); }   // Waddles next to Pip
            var wa = WorkAreaAt(crew[0].X);
            float half = (order.Count - 1) * 11f;
            float c = Clamp(crew[0].X, wa.Left + half + 60, wa.Right - half - 60);
            var bt = new Bedtime { Crew = order, Idle = idle, Start = ticks };
            for (int i = 0; i < order.Count; i++)
            {
                var q = order[i];
                float x = c - half + i * 22;
                int next = q == z ? BEDGUARD : (q == kind && pip != null) ? TUCK : BED;
                int face = q == kind && pip != null ? (pip.X <= x ? -1 : 1) : (x <= c ? 1 : -1);
                SendTo(q, bt, x, q == kind && pip != null ? -1 : face, next);
            }
            Say(order[0], BubbleArt.Zzz, 60);
            bedtime = bt;
            if (!idle) lastNightBed = ticks;
        }

        void UpdateBedtime()
        {
            var bt = bedtime;
            if (bt == null) return;
            bt.Crew.RemoveAll(q => q.Job != bt);
            if (bt.Crew.Count == 0) { bedtime = null; return; }
            if (bt.Idle ? idleMs < 1500 : ticks - bt.Start > 5400) EndBedtime(true);    // you're back / three minutes is enough
        }

        void EndBedtime(bool waveAfter)
        {
            var bt = bedtime;
            if (bt == null) return;
            bedtime = null;
            foreach (var q in bt.Crew)
                if (q.Job == bt) { q.Job = null; q.Tick = 0; q.State = YAWN; q.Timer = 55 + rnd.Next(30); q.WaveAfter = waveAfter; }
        }

        // ---- welcome back

        // Someone just dropped back in: the family waves, and the nearest one runs over to say hi.
        void Welcome(Penguin p)
        {
            if (GroupBusy() || ticks < lastWelcome + 90 || !IsCore(p)) return;
            lastWelcome = ticks;
            Penguin runner = null;
            float best = 600;
            foreach (var q in penguins)
            {
                if (q == p || !IsCore(q) || !Free(q) || Math.Abs(q.Y - p.Y) > 1 || Math.Abs(q.X - p.X) > 600) continue;
                q.State = WAVE; q.Timer = 45; q.Tick = 0; q.Dir = p.X >= q.X ? 1 : -1;
                Say(q, rnd.Next(2) == 0 ? BubbleArt.Heart : BubbleArt.Note, 45);
                float d = Math.Abs(q.X - p.X) - (Friends(q, p) ? 200 : 0);           // a friend, if one's around
                if (d < best) { best = d; runner = q; }
            }
            if (runner != null)
            {
                int side = runner.X < p.X ? -1 : 1;
                SendTo(runner, p, p.X + side * 28, -side, HELP);                      // HELP on arrival = a hello hug
            }
        }

        // You're back at your computer after a while: everyone turns and waves.
        void WelcomeUser()
        {
            if (bedtime != null) { EndBedtime(true); return; }                       // they wake up and wave after a yawn
            if (GroupBusy()) return;
            int cx = Cursor.Position.X;
            foreach (var q in penguins)
            {
                if (!IsCore(q) || !Free(q)) continue;
                q.State = WAVE; q.Timer = 50 + rnd.Next(25); q.Tick = 0; q.Dir = cx >= q.X ? 1 : -1;
                Say(q, rnd.Next(2) == 0 ? BubbleArt.Heart : BubbleArt.Note, 50);
            }
        }

        // Pip tries to lead a parade like Ziggy. Only Waddles, out of kindness, falls in.
        void TryPipParade()
        {
            var pip = Little();
            if (pip == null || !Available(pip) || GroupBusy() || ticks < shooUntil) return;
            var pr = new Parade { Leader = pip, Kid = true, Timer = 260 + rnd.Next(120) };
            var kind = penguins.Find(q => q.Name == "Waddles" && Available(q) && Math.Abs(q.Y - pip.Y) <= 1 && Math.Abs(q.X - pip.X) < 600);
            if (kind != null) pr.Line.Add(kind);
            pip.Job = pr; pip.State = WALK; pip.Tick = 0;
            Say(pip, BubbleArt.Bang, 60);
            if (kind != null) { kind.Job = pr; kind.State = FOLLOW; kind.Tick = 0; Say(kind, BubbleArt.Heart, 50); }
            foreach (var q in penguins)
                if (q != pip && q != kind && IsCore(q) && Available(q) && Math.Abs(q.X - pip.X) < 400) Say(q, BubbleArt.Question, 45);   // "...huh?"
            parade = pr;
        }

        // Ziggy shows Pip how to belly-slide. Pip copies... and faceplants. Ziggy helps it up.
        void TrySlideLesson()
        {
            var big = penguins.Find(q => q.Me.Leader);
            var pip = Little();
            if (big == null || pip == null || !Available(big) || !Available(pip)) return;
            if (Math.Abs(big.Y - pip.Y) > 1 || Math.Abs(big.X - pip.X) > 150) return;
            big.State = SLIDE; big.Tick = 0; big.SlideV = 6f;
            Say(big, BubbleArt.Note, 45);
            pip.State = LOOK; pip.Timer = 35; pip.Tick = 0; pip.Dir = big.X >= pip.X ? 1 : -1;
            pip.PendingSlide = true; pip.SlideDir = big.Dir; pip.SlideFail = true;
        }

        // Pip is asleep: Ziggy comes and stands guard beside it.
        void TryGuard()
        {
            var big = penguins.Find(q => q.Me.Leader);
            var pip = Little();
            if (big == null || pip == null || pip.State != SLEEP || !pip.OnFloor || !Available(big)) return;
            if (Math.Abs(big.Y - pip.Y) > 1 || Math.Abs(big.X - pip.X) > 500 || penguins.Exists(q => q.Job == pip)) return;
            int side = big.X < pip.X ? -1 : 1;
            SendTo(big, pip, pip.X + side * 30, -side, GUARD);
        }

        // Petting someone for a while: its rival comes over, jealous.
        void Jealous(Penguin pet)
        {
            if (!pet.OnFloor) return;
            foreach (var r in penguins)
            {
                if (!Rivals(r, pet) || !Available(r) || Math.Abs(r.Y - pet.Y) > 1 || Math.Abs(r.X - pet.X) > 500) continue;
                int side = r.X < pet.X ? -1 : 1;
                SendTo(r, pet, pet.X + side * 34, -side, JEALOUS);
                Say(r, BubbleArt.Grr, 50);
                return;
            }
        }

        // You flung someone: its friends won't forget it in a hurry. Its rivals think it's hilarious.
        void OnFlung(Penguin p)
        {
            foreach (var q in penguins)
            {
                if (q == p || !IsCore(q) || Math.Abs(q.X - p.X) > 700) continue;
                if (Friends(q, p)) { q.GlareUntil = ticks + 900; Say(q, BubbleArt.Grr, 50); }
                else if (Family.AreRivals(q.Name, p.Name)) Say(q, BubbleArt.Bang, 45);   // still family: "!"
            }
        }

        // Ziggy gives Pip a piggyback ride when they're close together.
        void TryPiggyback()
        {
            var big = penguins.Find(q => q.Me.Leader);
            var pip = Little();
            if (big == null || pip == null || !Available(big) || !Available(pip) || big.Rider != null) return;
            if (Math.Abs(big.Y - pip.Y) > 1 || Math.Abs(big.X - pip.X) > 160) return;
            pip.State = HOPUP; pip.Tick = 0; pip.HopOnto = big;
            pip.HopFromX = pip.X; pip.HopFromY = pip.Y;
            pip.OnFloor = false; pip.Surf = IntPtr.Zero;
            pip.RideFor = 350 + rnd.Next(300);
            Say(pip, BubbleArt.Heart, 50);
            Say(big, BubbleArt.Note, 50);
        }

        // Someone fell flat: for Pip, the nearest free big sibling (Ziggy first) comes to help it up;
        // for the others, a nearby friend comes over to comfort them.
        void CallForHelp(Penguin pip)
        {
            Penguin helper = null;
            float best = 350;
            foreach (var q in penguins)
            {
                if (q == pip || !IsCore(q) || !Free(q) || Math.Abs(q.Y - pip.Y) > 1) continue;
                if (!pip.Me.Small && !Friends(q, pip)) continue;              // grown-ups get comforted by their friends
                float d = Math.Abs(q.X - pip.X) - (q.Me.Leader ? 120 : 0);
                if (d < best) { best = d; helper = q; }
            }
            if (helper == null) return;
            int side = helper.X < pip.X ? -1 : 1;
            SendTo(helper, pip, pip.X + side * 26, -side, HELP);
            pip.HelpedBy = helper;
            Say(helper, BubbleArt.Bang, 40);
        }

        // A group hug just because: everyone piles in around Waddles.
        void StartGroupHug()
        {
            if (GroupBusy() || ticks < shooUntil) return;
            var center = penguins.Find(q => q.Name == "Waddles" && Recruitable(q)) ?? penguins.Find(Recruitable);
            if (center != null) StartHug(center, true);
        }

        void StartHug(Penguin center, bool auto = false)
        {
            if (IsFrost(center)) return;      // the family doesn't (yet) pile onto the neighbours
            foreach (var q in penguins)   // the jealous one is welcome too
                if (q.Job == center && (q.State == JEALOUS || (q.State == GOTO && q.NextState == JEALOUS))) { q.Job = null; q.State = WALK; q.Tick = 0; }
            var group = PickGroup(8, center);
            if (group == null) return;
            var h = new Hug { Center = center, Crew = group.GetRange(1, group.Count - 1) };
            h.Crew.Sort((a, b) => (Friends(b, center) ? 1 : 0).CompareTo(Friends(a, center) ? 1 : 0));   // friends cuddle closest
            center.Job = h;
            float cx = center.X;
            if (auto) { var wa = WorkAreaAt(center.X); cx = Clamp(center.X, wa.Left + 120, wa.Right - 120); }   // room on both sides
            int slot = 0;
            foreach (var p in h.Crew)
            {   // huddle in close on both sides, facing the middle
                slot++;
                int side = slot % 2 == 1 ? -1 : 1, rank = (slot + 1) / 2;
                p.HugNap = false; p.Wobbly = false;
                SendTo(p, h, cx + side * (20 + (rank - 1) * 17), -side, HUG);
                Say(p, BubbleArt.Heart, 50);
            }
            if (auto)
            {   // the one in the middle stays put (or flies down to the floor) and hugs too
                h.Auto = true;
                SendTo(center, h, cx, center.Dir, HUG);
                Say(center, BubbleArt.Hearts, 60);
            }
            hug = h;
        }

        void UpdateHug()
        {
            var h = hug;
            if (h == null) return;
            bool going = h.Auto ? h.Center.Job == h && h.T < 700 : h.Center.State == PET && h.Center.Job == h;
            if (!going) { EndHug(); return; }                                    // you stopped petting / it's been a good long hug
            h.Crew.RemoveAll(q => q.Job != h);
            h.T++;
            if (h.Squeeze > 0) h.Squeeze--;
            var inHug = h.Crew.FindAll(q => q.State == HUG);
            if (h.Auto && h.Center.State == HUG) inHug.Add(h.Center);
            if (h.T % 45 == 0 && inHug.Count > 0)
            {
                var q = inHug[rnd.Next(inHug.Count)];
                if (!q.HugNap) Say(q, q.Name == "Waddles" ? BubbleArt.Hearts : BubbleArt.Heart, 40);
            }
            if (h.T % 100 == 60 && inHug.Count >= 2)
            {   // squeeeeze! and little hearts float up
                h.Squeeze = 16;
                foreach (var q in inHug) q.Wobbly = q.Me.Accessory == Art.HardHat && rnd.Next(100) < 35;
                for (int i = 0; i < 3; i++)
                {
                    var q = inHug[rnd.Next(inHug.Count)];
                    SpawnHeart(q.X + rnd.Next(-8, 9), q.Y - HeadOf(q) - 6);
                }
                Say(h.Center, BubbleArt.Hearts, 45);
            }
            if (h.T == 230)
                foreach (var q in inHug)
                    if (q.Name == "Pebble") { q.HugNap = true; Say(q, BubbleArt.Zzz, 60); }   // so cosy... zzz
            if (h.T % 300 == 150)
            {
                var z = inHug.Find(q => q.Me.Leader);
                if (z != null) Say(z, BubbleArt.Note, 35);                                  // "ok, ok!"
            }
        }

        void SpawnHeart(float x, float y) { SpawnFloaty(rnd.Next(2), x, y, 0, -0.75f, 0, 85, 18); }

        void SpawnFloaty(int kind, float x, float y, float vx, float vy, float grav, int life, int cap)
        {
            if (floaties.Count >= cap) return;
            var f = new Floaty { X0 = x, Y = y, Vx = vx, Vy = vy, Grav = grav, Max = life, Kind = kind,
                                 Phase = (float)(rnd.NextDouble() * Math.PI * 2) };
            f.Win = new SpriteWindow(FloatW, FloatH, true);
            f.Win.Show();
            floaties.Add(f);
        }

        void UpdateFloaties()
        {
            for (int i = floaties.Count - 1; i >= 0; i--)
            {
                var f = floaties[i];
                if (++f.Life >= f.Max) { f.Win.Close(); floaties.RemoveAt(i); continue; }
                f.X0 += f.Vx; f.Y += f.Vy; f.Vy += f.Grav;
                bool flake = f.Kind == 2;
                double wob = f.Kind == 3 ? 0 : Math.Sin(f.Life * (flake ? 0.05f : 0.12f) + f.Phase) * (flake ? 7 : 5);
                int x = (int)(f.X0 + wob) - FloatW / 2;
                int left = f.Max - f.Life;
                int a = f.Kind == 3 ? left * 170 / f.Max                                 // breath just melts away
                      : left < 25 ? left * 255 / 25 : flake && f.Life < 10 ? f.Life * 25 : 255;
                f.Win.Draw(screenDC, memDC, floatHbm[f.Kind], x, (int)f.Y - FloatH / 2, (byte)a);
            }
        }

        void ClearFloaties()
        {
            foreach (var f in floaties) f.Win.Close();
            floaties.Clear();
        }

        void EndHug()
        {
            var h = hug;
            if (h == null) return;
            hug = null;
            bool centerIn = h.Auto && h.Center.Job == h;
            if (h.Center.Job == h) h.Center.Job = null;
            var huggers = new List<Penguin>(h.Crew) { h.Center };
            for (int i = 0; i < huggers.Count; i++)
                for (int j = i + 1; j < huggers.Count; j++)
                    if (Family.AreRivals(huggers[i].Name, huggers[j].Name)) MakeTruce(huggers[i], huggers[j], 3000);   // hugged it out
            bool real = h.T > 40;                                   // it got going: finish with a cheer and a burst of hearts
            var all = new List<Penguin>(h.Crew);
            if (centerIn) { all.Add(h.Center); h.Center.Job = h; }
            foreach (var p in all)
            {
                if (p.Job != h) continue;
                p.Job = null; p.Tick = 0; p.Wobbly = false; p.HopOff = 0; p.HopV = 0;
                if (p.HugNap) { p.HugNap = false; p.State = SLEEP; p.Timer = 200 + rnd.Next(150); continue; }   // Pebble sleeps right through it
                if (real && p.State == HUG)
                {
                    p.State = CLAP; p.Timer = 55; p.FaceDir = p.Dir = h.Center.X >= p.X ? 1 : -1;     // yay!
                    if (rnd.Next(2) == 0) SpawnHeart(p.X, p.Y - HeadOf(p) - 6);
                }
                else { p.State = LOOK; p.Timer = 30 + rnd.Next(30); }
                Say(p, BubbleArt.Heart, 45);
            }
            if (real) SpawnHeart(h.Center.X, h.Center.Y - HeadOf(h.Center) - 8);
        }

        // ---- parade

        // Ziggy shouts "!" and everyone free on the same stretch of floor falls in line behind.
        void StartParade()
        {
            if (GroupBusy() || ticks < shooUntil) return;
            var lead = penguins.Find(q => q.Me.Leader && q.State != HELD && q.State != OFF);
            if (lead == null) return;
            var group = PickGroup(12, lead);
            if (group == null) return;
            var line = group.GetRange(1, group.Count - 1);                     // nearest at the front
            var pip = line.Find(q => q.Me.TagAlong);
            if (pip != null) { line.Remove(pip); line.Add(pip); }              // little brother trails at the back
            var pr = new Parade { Leader = lead, Line = line, Timer = 550 + rnd.Next(250) };
            parade = pr;
            if (Available(lead)) { lead.Job = pr; lead.State = WALK; lead.Tick = 0; }
            else
            {   // Ziggy comes down to the floor first
                var wa = WorkAreaAt(lead.X);
                SendTo(lead, pr, Clamp(lead.X, wa.Left + 60, wa.Right - 60), lead.Dir, WALK);
            }
            Say(lead, BubbleArt.Bang, 60);
            for (int i = 0; i < line.Count; i++)
            {
                var p = line[i];
                if (Available(p)) { p.Job = pr; p.State = FOLLOW; p.Tick = 0; }
                else SendTo(p, pr, lead.X - lead.Dir * 31 * (i + 1), lead.Dir, FOLLOW);
                Say(p, BubbleArt.Note, 50);
            }
        }

        void EndParade(bool finished)
        {
            var pr = parade;
            if (pr == null) return;
            parade = null;
            var all = new List<Penguin>(pr.Line) { pr.Leader };
            foreach (var p in all)
            {
                if (p.Job != pr) continue;
                p.Job = null; p.Tick = 0; p.Cooldown = 60;
                if (finished) { p.State = DANCE; p.Timer = 70; Say(p, BubbleArt.Note); }   // a little celebration
                else { p.State = LOOK; p.Timer = 40; Puzzled(p); }                           // where did Ziggy go?
            }
            if (pr.Kid && pr.Line.Count == 0 && pr.Leader.Job == null && pr.Leader.State != HELD) Sulk(pr.Leader, -pr.Leader.Dir, penguins.Find(q => q.Name == "Waddles"));   // nobody came... Waddles cheers it up
        }

        void UpdateParade()
        {
            var pr = parade;
            if (pr == null) return;
            var ls = pr.Leader.State;
            if (pr.Leader.Job != pr || (ls != WALK && ls != GOTO && ls != SUMMON)) { EndParade(false); return; }   // leader got picked up / fell
            pr.Line.RemoveAll(q => q.Job != pr);                                                // stragglers drop out
            if (pr.Line.Count == 0 && !pr.Kid) { EndParade(false); return; }
            if (pr.Stunt != 0) { UpdateStunt(pr); return; }
            if (ls == WALK && --pr.Timer <= 0) { EndParade(true); return; }
            if (!pr.Kid && ls == WALK && pr.Line.Count >= 2 && pr.Stunts < 2 && --pr.StuntCool <= 0
                && pr.Line.TrueForAll(q => q.State == FOLLOW))
                StartStunt(pr);
        }

        // ---- something exciting on the march

        const int SlideLen = 150, PileLen = 200;
        const int RushLen = 50;             // pile-up: everyone speeds up this long before Ziggy stops dead
        const int RoomLen = 22;             // leapfrog: the line drops back this long before the jump

        void StartStunt(Parade pr)
        {
            int pick;
            do pick = 1 + rnd.Next(3); while ((pr.Done & (1 << pick)) != 0);
            pr.Done |= 1 << pick;
            pr.Stunt = pick; pr.StuntT = 0; pr.Stunts++; pr.Leaps = 0; pr.Jumper = null; pr.LeapWait = 0;
            pr.StuntCool = 200 + rnd.Next(150);
            foreach (var q in pr.Line) q.PileAt = 0;
            if (pick == 1) Say(pr.Leader, BubbleArt.Bang, 50);                     // "slide!"
            else Say(pick == 2 ? pr.Line[pr.Line.Count - 1] : pr.Leader, BubbleArt.Note, 45);   // leapfrog! / "faster!"
        }

        void UpdateStunt(Parade pr)
        {
            int t = ++pr.StuntT;
            switch (pr.Stunt)
            {
                case 1:     // slide train: Ziggy drops onto his tummy, and one by one everyone follows
                    for (int i = 0; i < pr.Line.Count; i++)
                        if (t == (i + 1) * 6) Say(pr.Line[i], i % 2 == 0 ? BubbleArt.Note : BubbleArt.Heart, 35);   // wheee
                    if (t >= SlideLen + 6 * pr.Line.Count + 10) EndStunt(pr);
                    return;
                case 2:     // leapfrog: the line drops back to make room, then whoever's at the back hops over everyone
                    if (pr.Jumper != null && !pr.Line.Contains(pr.Jumper)) { pr.Jumper = null; pr.LeapWait = 12; }
                    if (pr.Jumper == null)
                    {
                        if (pr.Leaps >= pr.Line.Count) { EndStunt(pr); return; }
                        if (--pr.LeapWait > 0) return;
                        var j = pr.Line[pr.Line.Count - 1];
                        pr.Line.RemoveAt(pr.Line.Count - 1);
                        pr.Line.Insert(0, j);                                             // their spot is now right behind Ziggy
                        pr.Jumper = j; pr.LeapT = 0; pr.Leaps++;
                        return;
                    }
                    if (++pr.LeapT == RoomLen)
                    {   // room made: jump!
                        pr.Jumper.HopV = -6f;
                        if (rnd.Next(2) == 0) Say(pr.Jumper, BubbleArt.Note, 35);
                    }
                    else if (pr.LeapT > RoomLen + 2 && pr.Jumper.HopV == 0 && pr.Jumper.HopOff == 0)
                    { pr.Jumper = null; pr.LeapWait = 12; }                               // landed; next one
                    return;
                default:    // pile-up: everyone speeds up... Ziggy stops dead, and they crash into each other
                    if (t == RushLen) Say(pr.Leader, BubbleArt.Bang, 40);              // stop!
                    if (t == RushLen + 45) Say(pr.Leader, BubbleArt.Note, 50);         // Ziggy can't stop laughing
                    if (t == PileLen - 30) foreach (var q in pr.Line) if (rnd.Next(3) == 0) Say(q, BubbleArt.Note, 40);
                    if (t >= PileLen) EndStunt(pr);
                    return;
            }
        }

        void EndStunt(Parade pr)
        {
            foreach (var q in pr.Line) { q.HopV = 0; q.HopOff = 0; q.PileAt = 0; }
            pr.Stunt = 0; pr.StuntT = 0; pr.Jumper = null;
        }

        void StartSnowman()
        {
            if (GroupBusy() || snow != null || ticks < shooUntil) return;     // one snowman at a time
            var crew = PickGroup(6);
            if (crew == null) return;
            var wa = WorkAreaAt(crew[0].X);
            float site = Math.Max(wa.Left + 100, Math.Min(wa.Right - 100, crew[0].X));
            var s = new Snowman { X = site, Crew = crew };
            s.Win = new SpriteWindow(SnowArt.W, SnowArt.H);
            s.Win.MouseDown += delegate(object o, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right) menu.Show(Cursor.Position);
                else if (!s.Melting) StartMelt(s);        // click the snowman to melt it
            };
            s.Win.Show();
            snow = s;
            for (int i = 0; i < crew.Count; i++)
            {   // alternate sides, further out each pair
                float off = (i % 2 == 0 ? -1 : 1) * (29 + (i / 2) * 26);
                SendTo(crew[i], s, site + off, off < 0 ? 1 : -1, BUILD);
            }
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
            if (GroupBusy() || ticks < shooUntil) return;
            var group = PickGroup(8);
            if (group == null) return;
            var wa = WorkAreaAt(group[0].X);
            float c = Math.Max(wa.Left + 160, Math.Min(wa.Right - 160, group[0].X));
            var t = new Tug { C = c, C0 = c, Timer = 900 };
            SplitTeams(group, t.Left, t.Right);                                  // friends together, rivals face off
            foreach (var team in new[] { t.Left, t.Right })
            {
                int side = team == t.Left ? -1 : 1;
                for (int i = 0; i < team.Count; i++)
                {
                    var p = team[i];
                    p.TugOff = side * (34 + i * 29);
                    SendTo(p, t, c + p.TugOff, -side, READY);
                }
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
            if (winners != null) QueueGoodGame(t.All());
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
            t.Rope.Draw(screenDC, memDC, t.RopeBmp, (int)(t.C + t.RopeOff), (int)all[0].Y - 25);

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
            bool enter = false, back = false, shot = false;
            bool winKey = ((Native.GetAsyncKeyState(0x5B) | Native.GetAsyncKeyState(0x5C)) & 0x8000) != 0;
            bool shift = (Native.GetAsyncKeyState(0x10) & 0x8000) != 0;
            for (int vk = 8; vk < 255; vk++)
            {
                if ((vk >= 0x10 && vk <= 0x12) || (vk >= 0xA0 && vk <= 0xA5) || vk == 0x5B || vk == 0x5C) continue; // modifiers
                bool down = (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
                if (down && !keyDown[vk])
                {
                    keyTicks.Enqueue(ticks); presses++;
                    if (vk == 0x0D) enter = true;
                    if (vk == 0x08) back = true;
                    if (vk == 0x2C || (vk == 0x53 && winKey && shift)) shot = true;     // Print Screen / Win+Shift+S
                }
                keyDown[vk] = down;
            }
            if (shot) Pose();
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
            if (hide) { EndFight(null); EndTower(false); EndParty(); EndHug(); EndRace(null); ClearStrays(); EndGoodGame(); EndHuddle(); EndPhoto(); EndBedtime(false); EndDinner(); EndMovie(); movieCool = 0; EndInspect(); ClearFloaties(); }   // pause = game over
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
                if (hide && p.State == ICEFISH) { p.State = WALK; p.Tick = 0; }
                if (hide) CloseHole(p);
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
            if (ticks % 6 == 1) { RefreshWindows(); CheckNewWindows(); }
            if (OwnCount() < target && ticks % 25 == 1 && ticks >= shooUntil) Spawn();
            else if (frostOn && ticks % 25 == 13 && ticks >= shooUntil && Core("Pip") != null)
            {   // the neighbours drop in, one at a time
                string missing = Array.Find(FrostNames, n => Core(n) == null);
                if (missing != null) Spawn(missing);
            }
            while (OwnCount() > target) RemovePenguin(penguins.FindLast(q => !IsFrost(q)));
            if (!frostOn) { var fb = penguins.Find(IsFrost); if (fb != null) RemovePenguin(fb); }

            TrackSurfaces();
            SenseUser();
            SenseMusic();
            if (ticks % 30 == 20) SenseMedia();
            foreach (var p in penguins) Step(p);
            UpdateTug();
            UpdateSnowman();
            UpdateParade();
            UpdateFight();
            UpdateTower();
            UpdateParty();
            UpdateHug();
            UpdateFloaties();
            UpdateRace();
            UpdateStrays();
            UpdateGoodGame();
            UpdateHuddle();
            UpdatePhoto();
            UpdateBedtime();
            UpdateDinner();
            UpdateMovie();
            UpdateInspect();
            CheckBumps();

            // now and then, Ziggy gives Pip a piggyback ride
            if (!GroupBusy() && ticks % 200 == 50 && rnd.Next(6) == 0) TryPiggyback();
            // and now and then, a rival lobs a snowball out of nowhere
            if (!GroupBusy() && ticks % 200 == 150 && rnd.Next(16) == 0) TryAmbush();
            // the brothers
            if (!GroupBusy() && !music && ticks % 200 == 75 && rnd.Next(30) == 0) TryPipParade();
            if (!GroupBusy() && ticks % 200 == 125 && rnd.Next(9) == 0) TrySlideLesson();
            if (ticks % 30 == 15) TryGuard();

            // little moments of their own
            if (!GroupBusy() && inspect == null && ticks >= shooUntil)
            {
                if (ticks % 200 == 25 && rnd.Next(8) == 0) TryCheckPip();
                if (ticks % 200 == 40 && rnd.Next(12) == 0) TryInspect();
                if (ticks % 200 == 100 && rnd.Next(9) == 0) TryScarf();
                if (ticks % 200 == 110 && rnd.Next(6) == 0) TryFixSnowman();
                if (ticks % 200 == 140 && rnd.Next(7) == 0) TrySneak();
                if (ticks % 200 == 160 && rnd.Next(10) == 0) TryRounds();
                if (ticks % 200 == 60 && rnd.Next(7) == 0) TryFishing();
            }
            if (ticks % 30 == 5) ScarfBack();

            // you've been away a while: huddle up; away longer: bedtime. Late at night: bedtime too.
            if (!GroupBusy() && ticks % 200 == 175 && idleMs > 60000 && idleMs < 300000 && rnd.Next(4) == 0) StartHuddle();
            if (ticks % 100 == 0 && !GroupBusy())
            {
                if (idleMs > 300000 && !idleBedDone) { idleBedDone = true; StartBedtime(true); }
                int hour = DateTime.Now.Hour;
                if ((hour >= 23 || hour < 5) && ticks - lastNightBed > 108000) StartBedtime(false);   // at most once an hour
                else if (hour == 18 && ticks - lastDinner > 216000 && rnd.Next(30) == 0) StartDinner();   // dinner time!
            }
            if (idleMs > 120000) awayLong = true;
            else if (awayLong && idleMs < 1000) { awayLong = false; idleBedDone = false; WelcomeUser(); }
            if (ticks % 30 == 0)
            {   // who's the favourite? (most petted, once it's been a fair bit)
                Penguin best = null;
                foreach (var q in penguins) if (IsCore(q) && q.PetTotal >= 150 && (best == null || q.PetTotal > best.PetTotal)) best = q;
                favourite = best;
            }

            // Group activities start by themselves, but only after a minute or so of free time since the
            // last one (so there's time to see them being themselves), and never on top of a personal
            // moment. They come out of a shuffled bag, so every activity gets a turn before any comes round again.
            if (GroupBusy() || music || inspect != null) lastBusy = ticks;
            if (!GroupBusy() && !music && ticks - lastBusy >= FreeTime && ticks % 200 == 0 && rnd.Next(5) == 0 && !PersonalMoment())
            {
                if (activityBag.Count == 0)
                {
                    for (int i = 0; i < 10; i++) activityBag.Add(i);
                    for (int i = activityBag.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); int tmp = activityBag[i]; activityBag[i] = activityBag[j]; activityBag[j] = tmp; }
                    if (activityBag[activityBag.Count - 1] == lastActivity) { activityBag.Reverse(); }   // never the same one twice in a row
                }
                int pick = activityBag[activityBag.Count - 1];
                if (pick == 0 && snow != null && activityBag.Count > 1)
                {   // a snowman's still standing: save its turn for later
                    activityBag.RemoveAt(activityBag.Count - 1);
                    activityBag.Insert(0, 0);
                    pick = activityBag[activityBag.Count - 1];
                }
                activityBag.RemoveAt(activityBag.Count - 1);
                lastActivity = pick;
                switch (pick)
                {
                    case 8: StartParade(); break;
                    case 0: StartSnowman(); break;
                    case 1: StartTug(); break;
                    case 2: StartFight(); break;
                    case 3: StartTower(); break;
                    case 4: StartHuddle(); break;
                    case 5: StartPhoto(); break;
                    case 6: StartDinner(); break;
                    case 9: StartGroupHug(); break;
                    default: StartParty(); break;
                }
            }

            bool raise = ticks % 60 == 0;
            foreach (var p in penguins)
            {
                int dir;
                int f = FrameFor(p, out dir);
                int d = dir > 0 ? 0 : 1;
                int x = (int)p.X - W / 2, y = (int)(p.Y + p.HopOff) - H;
                if (p.State == OFF) { x = -32000; y = -32000; }
                int acc = AccessoryNow(p);
                int key = (acc * Art.Frames + f) * 2 + d;
                if (key != p.LastFrame || x != p.LastX || y != p.LastY)
                {
                    p.Win.Draw(screenDC, memDC, Sprite(acc, d, f), x, y);
                    p.LastFrame = key; p.LastX = x; p.LastY = y;
                }
                if (raise) Native.SetWindowPos(p.Win.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013); // stay topmost
                DrawExtras(p, (int)p.X, (int)(p.Y + p.HopOff) - HeadOf(p));
                DrawHole(p);
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
            int[] outfitFrames = { Art.Stand, Art.WalkA, Art.WaveA, Art.PetB, Art.SleepA, Art.Slide, Art.Umbrella, Art.PlantA };
            const int perRow = 40;           // frames wrap into blocks of 40 columns
            int blocks = (Art.Frames + perRow - 1) / perRow, bh = ch * 2 + Art.H + 20;
            int sy = blocks * bh;            // snowman / bubbles row
            int oy = sy + ch;                // outfit rows start here
            using (var sheet = new Bitmap(cw * perRow, oy + ch * (Art.Outfits - 1)))
            using (var g = Graphics.FromImage(sheet))
            {
                g.Clear(Color.FromArgb(70, 110, 160));
                for (int k = 0; k < blocks; k++) g.FillRectangle(Brushes.Black, 0, k * bh + ch * 2, sheet.Width, Art.H + 20);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                for (int f = 0; f < Art.Frames; f++)
                {
                    int bx = (f % perRow) * cw, by = (f / perRow) * bh;
                    g.DrawImage(frames[0, f], bx + pad / 2, by + pad / 2, Art.W * Z, Art.H * Z);
                    g.DrawImage(frames[1, f], bx + pad / 2, by + ch + pad / 2, Art.W * Z, Art.H * Z);
                    g.DrawImageUnscaled(frames[0, f], bx + 30, by + ch * 2 + 10);   // actual size on black
                }
                for (int s = 0; s < SnowArt.Stages; s++)
                    using (var b = SnowArt.Render(s))
                        g.DrawImage(b, s * cw + pad / 2, sy, SnowArt.W * Z, SnowArt.H * Z);
                using (var rope = SnowArt.RenderRope(60))
                    g.DrawImage(rope, SnowArt.Stages * cw + 20, sy + 60, 60 * Z, 12 * Z);
                int bxs = SnowArt.Stages * cw + 60 * Z + 60;
                for (int i = 0; i < BubbleArt.Count; i++)
                    using (var b = BubbleArt.Render(i))
                        g.DrawImage(b, bxs + i * (BubbleArt.W * Z + 12), sy + 20, BubbleArt.W * Z, BubbleArt.H * Z);
                using (var tag = BubbleArt.RenderTag("Ziggy", true, Family.BestFriend("Ziggy"), Family.Rival("Ziggy")))
                    g.DrawImage(tag, bxs + BubbleArt.Count * (BubbleArt.W * Z + 12) + 10, sy + 30, tag.Width * Z, tag.Height * Z);
                for (int o = 1; o < Art.Outfits; o++)          // each accessory on a few poses
                    for (int i = 0; i < outfitFrames.Length; i++)
                        using (var b = Art.Render(outfitFrames[i], o))
                            g.DrawImage(b, i * cw + pad / 2, oy + (o - 1) * ch + pad / 2, Art.W * Z, Art.H * Z);
                using (var b = SnowArt.RenderBucket())
                    g.DrawImage(b, outfitFrames.Length * cw + 40, oy + 20, SnowArt.BucketW * Z, SnowArt.BucketH * Z);
                using (var b = SnowArt.RenderFish(true))
                    g.DrawImage(b, outfitFrames.Length * cw + 160, oy + 40, SnowArt.FishW * Z, SnowArt.FishH * Z);
                using (var b = SnowArt.RenderFish(false))
                    g.DrawImage(b, outfitFrames.Length * cw + 240, oy + 40, SnowArt.FishW * Z, SnowArt.FishH * Z);
                for (int k = 0; k < 3; k++)
                    using (var h = SnowArt.RenderHole(k))
                        g.DrawImage(h, outfitFrames.Length * cw + 320 + k * 110, oy + 40, SnowArt.HoleW * Z, SnowArt.HoleH * Z);
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
            int n = PenguinApp.MinPenguins;
            if (args.Length > 0 && !int.TryParse(args[0], out n)) n = PenguinApp.MinPenguins;

            Application.EnableVisualStyles();
            Application.Run(new PenguinApp(Math.Max(PenguinApp.MinPenguins, Math.Min(n, 60))));
            GC.KeepAlive(mutex);
        }
    }
}
