using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PingTurntable
{
    internal sealed class EffectInstance : IDisposable
    {
        public readonly LayerWindow Window = new LayerWindow();
        public readonly Stopwatch Watch = Stopwatch.StartNew();
        public int Index;
        public bool Emote;
        public Point Position;
        public double LastFrame = -1;
        public void Dispose() { Window.Dispose(); }
    }
    internal sealed class SignalRecord
    {
        public int Index;
        public bool Emote;
        public Point Position;
        public DateTime At;
        public string Label;
    }
    internal sealed class MessageHost : Form
    {
        readonly Action<int> command;
        public MessageHost(Action<int> callback) { command = callback; Text = "PingTurntable.MessageHost"; ShowInTaskbar = false; var unused = Handle; }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_APP_SHOW) { command(2); return; }
            if (m.Msg == Native.WM_APP_QUIT) { command(4); return; }
            if (m.Msg == Native.WM_HOTKEY) { command(m.WParam.ToInt32()); return; }
            base.WndProc(ref m);
        }
    }

    internal sealed class OverlayApp : ApplicationContext
    {
        public readonly Preferences Settings;
        public readonly Artwork Art;
        readonly AudioBank audio = new AudioBank();
        readonly LayerWindow wheel = new LayerWindow();
        readonly List<EffectInstance> effects = new List<EffectInstance>();
        readonly List<SignalRecord> history = new List<SignalRecord>();
        readonly HashSet<int> swallowedKeys = new HashSet<int>(), swallowedMouse = new HashSet<int>();
        readonly MessageHost host;
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        readonly HotkeyRegistry hotkeys;
        readonly System.Windows.Forms.Timer timer;
        readonly Native.HookProc mouseCallback, keyCallback;
        IntPtr mouseHook, keyHook;
        SettingsWindow settingsWindow;
        public BindingDialog Capturer;
        public bool Paused { get { return Settings.Paused; } }
        public bool PinnedEmotes { get { return Settings.PinnedEmotes; } }
        public string PauseKey { get { return BindingText("pause"); } }
        public string EmoteKey { get { return BindingText("emote"); } }
        public string BindingNotice { get; private set; }
        public int SessionCount { get; private set; }
        public IList<SignalRecord> History { get { return history.AsReadOnly(); } }
        bool active, emote, dragging, disposed, wheelShown;
        InputBinding heldTrigger;
        Point anchor, center, pinnedCenter, dragOffset;
        int drawnSelected = int.MinValue, drawnOpacity = 255;
        double suppressUntil;
        readonly Stopwatch clock = Stopwatch.StartNew(), gestureClock = new Stopwatch();
        Icon trayIcon;
        public event Action StateChanged;
        public double Scale { get { return Settings.Scale / 100.0; } }
        double Lifetime { get { return Settings.EffectMilliseconds / 1000.0; } }
        public string BindingText(string id) { InputBinding value; return Settings.Bindings.TryGetValue(id, out value) ? value.ToString() : "未设置"; }

        public OverlayApp(bool showSettings)
        {
            Settings = Preferences.Load(); Art = new Artwork(); BindingNotice = "";
            for (int i = 0; i < 5; ++i) if (!Art.Emotes.Any(x => x.id == Settings.Emotes[i])) Settings.Emotes[i] = 1459;
            host = new MessageHost(Command); hotkeys = new HotkeyRegistry(new WindowsHotkeys(host.Handle));
            InitializeBindings();
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            pinnedCenter = Settings.X == int.MinValue ? new Point(area.Right - 180, area.Bottom - 180) : new Point(Settings.X, Settings.Y);
            ClampPinned();
            menu = new ContextMenuStrip { BackColor = Palette.Surface, ForeColor = Palette.Text };
            menu.Items.Add("", null, (s, e) => OpenSettings()); menu.Items.Add("", null, (s, e) => ToggleMode());
            menu.Items.Add("", null, (s, e) => TogglePinned()); menu.Items.Add("", null, (s, e) => TogglePause());
            menu.Items.Add("", null, (s, e) => ToggleSound()); menu.Items.Add("清除屏幕标记", null, (s, e) => ClearEffects());
            menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("退出", null, (s, e) => ExitThread());
            using (Bitmap icon = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(icon)) g.DrawImage(Art.Icon(3), 7, 1, 18, 30);
                IntPtr handle = icon.GetHicon(); trayIcon = (Icon)Icon.FromHandle(handle).Clone(); DestroyIcon(handle);
            }
            tray = new NotifyIcon { Icon = trayIcon, Visible = true, ContextMenuStrip = menu };
            tray.DoubleClick += (s, e) => OpenSettings(); UpdateTray();
            mouseCallback = MouseHook; keyCallback = KeyHook;
            mouseHook = Native.SetWindowsHookEx(14, mouseCallback, Native.GetModuleHandle(null), 0);
            keyHook = Native.SetWindowsHookEx(13, keyCallback, Native.GetModuleHandle(null), 0);
            if (mouseHook == IntPtr.Zero || keyHook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "无法注册全局输入，请重新启动程序。");
            timer = new System.Windows.Forms.Timer { Interval = 16 }; timer.Tick += Tick; timer.Start();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
            Settings.Save(); Files.Log("Started v2; shortcuts=" + BindingText("ping") + " / " + EmoteKey + "; pause=" + PauseKey);
            if (showSettings) host.BeginInvoke(new Action(OpenSettings));
        }
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        void InitializeBindings()
        {
            if (KeyBindings.Validate(Settings.Bindings) != null) { Settings.Bindings = KeyBindings.Defaults(Settings.TKey); BindingNotice = "读取到无效按键配置，已恢复默认。"; }
            string error;
            for (int attempt = 0; attempt < KeyBindings.Actions.Length + 2; ++attempt)
            {
                if (hotkeys.TryApply(Settings.Bindings, Paused, out error)) return;
                string failed = hotkeys.FailedAction;
                if (failed == null) throw new InvalidOperationException(error);
                InputBinding old = Settings.Bindings[failed];
                if (failed == "pause" && old.Key == (int)Keys.P && old.Modifiers == 3 && !old.Mouse)
                {
                    var fallback = InputBinding.Keyboard(Keys.P, 7);
                    if (!Settings.Bindings.Any(x => x.Key != "pause" && x.Value.Enabled && x.Value.Signature == fallback.Signature))
                    { Settings.Bindings[failed] = fallback; BindingNotice = "暂停键已自动改为 Ctrl + Alt + Shift + P，原组合被占用。"; continue; }
                }
                Settings.Bindings[failed] = InputBinding.Off(); BindingNotice = error + " 该项暂时停用，可在自定按键中重设。";
                Files.Log("Shortcut disabled at startup: " + failed);
            }
            throw new InvalidOperationException("无法建立可用的按键配置。");
        }
        public bool TryBindings(Dictionary<string, InputBinding> candidate, out string error)
        {
            Cancel();
            if (!hotkeys.TryApply(candidate, false, out error)) { BindingNotice = error; Notify(); return false; }
            if (Paused && !hotkeys.TryApply(candidate, true, out error))
            {
                string ignored; hotkeys.TryApply(Settings.Bindings, true, out ignored); BindingNotice = error; Notify(); return false;
            }
            Settings.Bindings = KeyBindings.Copy(candidate); Settings.Save(); BindingNotice = "按键已保存，立即生效。";
            Files.Log("Shortcut configuration updated"); Notify(); return true;
        }
        void Command(int id)
        {
            if (id == 4) { ExitThread(); return; }
            if (Capturer != null) return;
            BindingAction action = KeyBindings.Actions.FirstOrDefault(a => a.NativeId == id);
            if (action == null || (Paused && !action.WhilePaused) || action.Hold) return;
            switch (id)
            {
                case 1: TogglePause(); break; case 2: OpenSettings(); break; case 3: ToggleMode(); break;
                case 5: TogglePinned(); break; case 6: ToggleSound(); break; case 7: ReplayLast(true); break; case 8: ClearEffects(); break;
                default: if (id >= 10 && id <= 13) Emit(id - 10, false, Native.CursorPoint); break;
            }
        }
        public void TogglePause()
        {
            bool next = !Paused; string error;
            if (!hotkeys.TryApply(Settings.Bindings, next, out error)) { BindingNotice = error; Notify(); return; }
            Settings.Paused = next; ClearEffects(false); Settings.Save(); Files.Log(Paused ? "Paused" : "Resumed"); Notify();
        }
        public void ToggleMode() { Settings.PinnedEmotes = !PinnedEmotes; Changed(); }
        public void TogglePinned() { Settings.Pinned = !Settings.Pinned; Changed(); }
        public void ToggleSound() { Settings.Sound = !Settings.Sound; Changed(); }
        public void ResetPosition() { Rectangle a = Screen.FromPoint(Native.CursorPoint).WorkingArea; pinnedCenter = new Point(a.Right - 180, a.Bottom - 180); Changed(); }
        public void Changed() { Cancel(); ClampPinned(); Settings.Save(); drawnSelected = int.MinValue; Notify(); }
        void Notify() { UpdateTray(); if (StateChanged != null) StateChanged(); }
        void UpdateTray()
        {
            if (tray == null || menu == null) return;
            tray.Text = Paused ? "信号轮盘 · 已暂停" : "信号轮盘 · 自定义按键 / 海克斯";
            menu.Items[0].Text = "打开设置    " + BindingText("settings");
            menu.Items[1].Text = "切换信号 / 表情    " + BindingText("mode");
            menu.Items[2].Text = (Settings.Pinned ? "隐藏常驻轮盘    " : "显示常驻轮盘    ") + BindingText("pin");
            menu.Items[3].Text = (Paused ? "恢复    " : "暂停    ") + PauseKey;
            menu.Items[4].Text = (Settings.Sound ? "静音    " : "开启声音    ") + BindingText("mute");
        }
        void ClampPinned()
        {
            pinnedCenter = WheelMath.ClampCenter(pinnedCenter, Screen.FromPoint(pinnedCenter).WorkingArea, (int)Math.Ceiling((Settings.Hextech ? 164 : 144) * Scale));
            Settings.X = pinnedCenter.X; Settings.Y = pinnedCenter.Y;
        }
        void DisplayChanged(object sender, EventArgs e) { if (!disposed) host.BeginInvoke(new Action(() => { ClampPinned(); Changed(); })); }
        bool HitPinned(Point p)
        {
            if (!Settings.Pinned || Paused || active || !wheelShown || clock.Elapsed.TotalSeconds < suppressUntil) return false;
            double dx = p.X - pinnedCenter.X, dy = p.Y - pinnedCenter.Y;
            return dx * dx + dy * dy < 143 * 143 * Scale * Scale;
        }
        void BeginGesture(Point p, bool useEmotes, bool fromPinned, InputBinding trigger)
        {
            active = true; emote = useEmotes; anchor = fromPinned ? pinnedCenter : p;
            center = fromPinned ? pinnedCenter : WheelMath.ClampCenter(p, Screen.FromPoint(p).Bounds, (int)Math.Ceiling((Settings.Hextech ? 164 : 144) * Scale));
            heldTrigger = trigger.Copy(); drawnSelected = int.MinValue; gestureClock.Restart(); suppressUntil = 0;
        }
        void FinishGesture(Point p)
        {
            if (!active) { heldTrigger = null; return; }
            int id = WheelMath.Select(p, anchor, Scale, emote); bool isEmote = emote; Point at = anchor;
            Cancel(); heldTrigger = null; if (id >= 0) Emit(isEmote ? Settings.Emotes[id] : id, isEmote, at);
        }
        IntPtr MouseHook(int code, IntPtr msg, IntPtr data)
        {
            if (code < 0) return Native.CallNextHookEx(mouseHook, code, msg, data);
            var info = (Native.MouseData)Marshal.PtrToStructure(data, typeof(Native.MouseData));
            Point p = new Point(info.pt.X, info.pt.Y); bool down, up;
            int button = Native.MouseButton(msg.ToInt32(), info.mouseData, out down, out up);
            if (up && swallowedMouse.Remove(button))
            {
                if (dragging && button == (int)Keys.LButton) { dragging = false; ClampPinned(); Settings.Save(); drawnSelected = int.MinValue; }
                else if (heldTrigger != null && heldTrigger.Mouse && heldTrigger.Key == button) FinishGesture(p);
                return new IntPtr(1);
            }
            if (Capturer != null)
            {
                if (down && Capturer.AcceptsMouse(p)) { swallowedMouse.Add(button); Capturer.Record(InputBinding.Button((Keys)button, KeyBindings.ModifiersDown())); return new IntPtr(1); }
                return Native.CallNextHookEx(mouseHook, code, msg, data);
            }
            if (!down) return Native.CallNextHookEx(mouseHook, code, msg, data);
            if (active && button == (int)Keys.RButton) { Cancel(); swallowedMouse.Add(button); return new IntPtr(1); }
            BindingAction action = KeyBindings.Find(Settings.Bindings, button, KeyBindings.ModifiersDown(), true, Paused);
            if (action != null)
            {
                swallowedMouse.Add(button);
                if (action.Hold) { if (!active) BeginGesture(p, action.Id == "emote", false, Settings.Bindings[action.Id]); }
                else Command(action.NativeId);
                return new IntPtr(1);
            }
            if (!Paused && !active && HitPinned(p))
            {
                if (button == (int)Keys.RButton) { swallowedMouse.Add(button); host.BeginInvoke(new Action(() => menu.Show(p))); return new IntPtr(1); }
                if (button == (int)Keys.LButton)
                {
                    swallowedMouse.Add(button);
                    if (Native.Down((int)Keys.ShiftKey) && !Settings.LockPosition) { dragging = true; dragOffset = new Point(pinnedCenter.X - p.X, pinnedCenter.Y - p.Y); }
                    else BeginGesture(p, PinnedEmotes, true, InputBinding.Button(Keys.LButton, 0));
                    return new IntPtr(1);
                }
            }
            return Native.CallNextHookEx(mouseHook, code, msg, data);
        }
        IntPtr KeyHook(int code, IntPtr msg, IntPtr data)
        {
            if (code < 0) return Native.CallNextHookEx(keyHook, code, msg, data);
            int message = msg.ToInt32(); bool down = message == Native.WM_KEYDOWN || message == Native.WM_SYSKEYDOWN;
            bool up = message == Native.WM_KEYUP || message == Native.WM_SYSKEYUP;
            var info = (Native.KeyData)Marshal.PtrToStructure(data, typeof(Native.KeyData)); int key = (int)info.vk;
            if (up && swallowedKeys.Remove(key))
            {
                if (heldTrigger != null && !heldTrigger.Mouse && heldTrigger.Key == key) FinishGesture(Native.CursorPoint);
                return new IntPtr(1);
            }
            if (down && swallowedKeys.Contains(key)) return new IntPtr(1);
            if (Capturer != null && Capturer.IsForeground && down && !KeyBindings.IsModifier(key))
            {
                swallowedKeys.Add(key);
                if (key == (int)Keys.Escape) Capturer.CancelRecording(); else Capturer.Record(InputBinding.Keyboard((Keys)key, KeyBindings.ModifiersDown()));
                return new IntPtr(1);
            }
            if (Capturer == null && down && key == (int)Keys.Escape && (active || dragging)) { Cancel(); swallowedKeys.Add(key); return new IntPtr(1); }
            if (Capturer == null && down && !Paused)
            {
                BindingAction action = KeyBindings.Find(Settings.Bindings, key, KeyBindings.ModifiersDown(), false, Paused);
                if (action != null && action.Hold)
                {
                    swallowedKeys.Add(key); if (!active) BeginGesture(Native.CursorPoint, action.Id == "emote", false, Settings.Bindings[action.Id]); return new IntPtr(1);
                }
            }
            return Native.CallNextHookEx(keyHook, code, msg, data);
        }
        public void Cancel() { active = false; dragging = false; drawnSelected = int.MinValue; gestureClock.Stop(); }
        public void Emit(int id, bool isEmote, Point position)
        {
            if (Paused) return;
            if (effects.Count >= 10) { effects[0].Dispose(); effects.RemoveAt(0); }
            effects.Add(new EffectInstance { Index = id, Emote = isEmote, Position = position });
            if (!isEmote) audio.Play(Art.Sounds[id], Settings);
            history.Insert(0, new SignalRecord { Index = id, Emote = isEmote, Position = position, At = DateTime.Now, Label = LabelFor(id, isEmote) });
            if (history.Count > 8) history.RemoveAt(8); SessionCount++;
            suppressUntil = clock.Elapsed.TotalSeconds + Lifetime; wheel.Hide(); wheelShown = false; drawnSelected = int.MinValue;
            Files.Log((isEmote ? "Emote " : "Ping ") + id + " at " + position.X + "," + position.Y); Notify();
        }
        public string LabelFor(int id, bool isEmote)
        {
            if (!isEmote) return Settings.English ? Art.EnglishNames[id] : Art.Names[id];
            Emote entry = Art.Emotes.First(x => x.id == id); return Settings.English ? entry.name : entry.nameZh;
        }
        public void Preview(int id, bool isEmote) { Point at = settingsWindow == null ? pinnedCenter : new Point(settingsWindow.Left + 203, settingsWindow.Top + 356); Emit(id, isEmote, at); }
        public void ReplayLast(bool atCursor) { if (history.Count != 0) Replay(history[0], atCursor); }
        public void Replay(SignalRecord record, bool atCursor)
        { Point at = atCursor ? Native.CursorPoint : record.Position; at = WheelMath.ClampCenter(at, Screen.FromPoint(at).Bounds, 8); Emit(record.Index, record.Emote, at); }
        public void ClearEffects(bool notify = true)
        { Cancel(); foreach (EffectInstance fx in effects) fx.Dispose(); effects.Clear(); suppressUntil = 0; if (notify) Notify(); }
        void Tick(object sender, EventArgs e)
        {
            Point pointer = Native.CursorPoint;
            if (dragging) { pinnedCenter = new Point(pointer.X + dragOffset.X, pointer.Y + dragOffset.Y); ClampPinned(); drawnSelected = int.MinValue; }
            if (active && gestureClock.Elapsed.TotalSeconds > 30) Cancel();
            bool show = Capturer == null && !Paused && (active || (Settings.Pinned && clock.Elapsed.TotalSeconds >= suppressUntil));
            if (show)
            {
                bool mode = active ? emote : PinnedEmotes; Point drawCenter = active ? center : pinnedCenter; bool hover = HitPinned(pointer);
                int choice = active ? WheelMath.Select(pointer, anchor, Scale, mode) : (hover ? WheelMath.Select(pointer, pinnedCenter, Scale, mode) : -1);
                if (dragging) choice = -1;
                int drawKey = choice + (mode ? 20 : 0); bool intro = active && !Settings.ReducedMotion && gestureClock.Elapsed.TotalSeconds < .13;
                int targetAlpha = active || hover || dragging ? 255 : Settings.IdleOpacity * 255 / 100;
                int nextAlpha = Settings.ReducedMotion || !wheelShown ? targetAlpha : drawnOpacity + (int)Math.Ceiling((targetAlpha - drawnOpacity) * .3);
                if (Math.Abs(targetAlpha - nextAlpha) <= 3) nextAlpha = targetAlpha;
                if (drawnSelected != drawKey || !wheelShown || intro || nextAlpha != drawnOpacity)
                {
                    byte alpha = intro ? (byte)Math.Max(1, nextAlpha * gestureClock.Elapsed.TotalSeconds / .13) : (byte)nextAlpha;
                    using (Bitmap bitmap = Art.Wheel(mode, choice, Settings, Scale)) wheel.Render(bitmap, new Point(drawCenter.X - bitmap.Width / 2, drawCenter.Y - bitmap.Height / 2), alpha);
                    drawnSelected = intro ? int.MinValue : drawKey; drawnOpacity = nextAlpha; wheelShown = true;
                }
            }
            else if (wheelShown) { wheel.Hide(); wheelShown = false; }
            for (int i = effects.Count - 1; i >= 0; --i)
            {
                EffectInstance fx = effects[i]; double elapsed = fx.Watch.Elapsed.TotalSeconds;
                if (elapsed >= Lifetime || Paused) { fx.Dispose(); effects.RemoveAt(i); continue; }
                if (elapsed - fx.LastFrame < .032) continue;
                using (Bitmap bitmap = Art.Effect(fx.Index, fx.Emote, elapsed, Scale, Settings)) fx.Window.Render(bitmap, new Point(fx.Position.X - bitmap.Width / 2, fx.Position.Y - (int)(178 * Scale)), 255);
                fx.LastFrame = elapsed;
            }
        }
        public void OpenSettings()
        {
            if (settingsWindow == null || settingsWindow.IsDisposed) settingsWindow = new SettingsWindow(this);
            settingsWindow.Show(); settingsWindow.WindowState = FormWindowState.Normal; settingsWindow.Activate();
        }
        protected override void ExitThreadCore()
        {
            if (disposed) return; disposed = true;
            if (timer != null) { timer.Stop(); timer.Dispose(); }
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
            if (mouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(mouseHook);
            if (keyHook != IntPtr.Zero) Native.UnhookWindowsHookEx(keyHook);
            hotkeys.Dispose(); Settings.Save(); tray.Visible = false; tray.Dispose(); trayIcon.Dispose(); menu.Dispose();
            foreach (EffectInstance fx in effects) fx.Dispose();
            wheel.Dispose(); if (settingsWindow != null) settingsWindow.Dispose(); host.Dispose(); audio.Dispose(); Art.Dispose();
            Files.Log("Stopped; all shortcuts and hooks removed"); base.ExitThreadCore();
        }
    }

    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            Native.DpiAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (Array.IndexOf(args, "--self-test") >= 0) return Verification.Run();
            if (Array.IndexOf(args, "--quit") >= 0)
            {
                IntPtr target = Native.FindWindow(null, "PingTurntable.MessageHost");
                if (target != IntPtr.Zero) Native.PostMessage(target, Native.WM_APP_QUIT, IntPtr.Zero, IntPtr.Zero); return 0;
            }
            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\PingTurntable.Classic12_21", out created))
            {
                if (!created)
                {
                    IntPtr target = Native.FindWindow(null, "PingTurntable.MessageHost");
                    if (target != IntPtr.Zero) Native.PostMessage(target, Native.WM_APP_SHOW, IntPtr.Zero, IntPtr.Zero); return 0;
                }
                Application.ThreadException += (s, e) => Files.Log("UI ERROR " + e.Exception);
                try
                {
                    bool first = !File.Exists(Files.PathOf("settings.json"));
                    using (OverlayApp app = new OverlayApp(first || Array.IndexOf(args, "--settings") >= 0)) Application.Run(app); return 0;
                }
                catch (Exception e)
                {
                    Files.Log("FATAL " + e); MessageBox.Show("启动失败：" + e.Message + "\n详情已写入 runtime.log。", "信号轮盘", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1;
                }
            }
        }
    }
}
