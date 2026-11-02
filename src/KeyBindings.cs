using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PingTurntable
{
    public sealed class InputBinding
    {
        public int Key;
        public int Modifiers;
        public bool Mouse;
        public bool Enabled = true;
        public InputBinding Copy() { return new InputBinding { Key = Key, Modifiers = Modifiers, Mouse = Mouse, Enabled = Enabled }; }
        public string Signature { get { return (Mouse ? "M" : "K") + ":" + Modifiers + ":" + Key; } }
        public override string ToString()
        {
            if (!Enabled) return "未设置";
            string prefix = ((Modifiers & 2) != 0 ? "Ctrl + " : "") + ((Modifiers & 1) != 0 ? "Alt + " : "") + ((Modifiers & 4) != 0 ? "Shift + " : "");
            string name;
            if (Mouse)
            {
                switch ((Keys)Key) { case Keys.LButton: name = "左键"; break; case Keys.RButton: name = "右键"; break; case Keys.MButton: name = "中键"; break; case Keys.XButton1: name = "鼠标侧键 1"; break; default: name = "鼠标侧键 2"; break; }
            }
            else
            {
                Keys code = (Keys)Key;
                if (code >= Keys.D0 && code <= Keys.D9) name = ((int)code - (int)Keys.D0).ToString();
                else if (code >= Keys.NumPad0 && code <= Keys.NumPad9) name = "Num " + ((int)code - (int)Keys.NumPad0);
                else if (code == Keys.Back) name = "Backspace";
                else if (code == Keys.Space) name = "Space";
                else if (code == Keys.Return) name = "Enter";
                else name = code.ToString();
            }
            return prefix + name;
        }
        public static InputBinding Keyboard(Keys key, int mods) { return new InputBinding { Key = (int)key, Modifiers = mods }; }
        public static InputBinding Button(Keys key, int mods) { return new InputBinding { Key = (int)key, Modifiers = mods, Mouse = true }; }
        public static InputBinding Off() { return new InputBinding { Enabled = false }; }
    }

    internal sealed class BindingAction
    {
        public readonly string Id, Name, Hint;
        public readonly int NativeId;
        public readonly bool Hold, WhilePaused;
        public BindingAction(string id, string name, string hint, int number, bool hold = false, bool paused = false)
        { Id = id; Name = name; Hint = hint; NativeId = number; Hold = hold; WhilePaused = paused; }
    }

    internal static class KeyBindings
    {
        public static readonly BindingAction[] Actions = {
            new BindingAction("ping", "信号轮盘", "按住选择，释放发送", 100, true),
            new BindingAction("emote", "表情轮盘", "轻按发送中央表情", 101, true),
            new BindingAction("pause", "暂停 / 恢复", "暂停时仍可使用", 1, false, true),
            new BindingAction("settings", "打开设置", "暂停时仍可使用", 2, false, true),
            new BindingAction("mode", "切换信号 / 表情", "切换常驻轮盘", 3),
            new BindingAction("pin", "显示 / 隐藏轮盘", "按需呼出或常驻显示", 5),
            new BindingAction("mute", "静音 / 开声音", "只改变本程序音量", 6),
            new BindingAction("replay", "重放上次标记", "在鼠标当前位置重放", 7),
            new BindingAction("clear", "清除屏幕标记", "立即收起全部标记", 8),
            new BindingAction("danger", "直接发送：危险", "在鼠标位置发送", 10),
            new BindingAction("onmyway", "直接发送：正在路上", "在鼠标位置发送", 11),
            new BindingAction("assist", "直接发送：协助我", "在鼠标位置发送", 12),
            new BindingAction("missing", "直接发送：敌人不见了", "在鼠标位置发送", 13)
        };
        public static Dictionary<string, InputBinding> Defaults(bool legacyT = false)
        {
            return new Dictionary<string, InputBinding> {
                { "ping", InputBinding.Button(Keys.LButton, 1) }, { "emote", InputBinding.Keyboard(Keys.T, legacyT ? 0 : 1) },
                { "pause", InputBinding.Keyboard(Keys.P, 3) }, { "settings", InputBinding.Keyboard(Keys.O, 3) },
                { "mode", InputBinding.Keyboard(Keys.E, 3) }, { "pin", InputBinding.Keyboard(Keys.H, 3) },
                { "mute", InputBinding.Keyboard(Keys.M, 3) }, { "replay", InputBinding.Keyboard(Keys.R, 7) },
                { "clear", InputBinding.Keyboard(Keys.Back, 3) },
                { "danger", InputBinding.Keyboard(Keys.D1, 3) }, { "onmyway", InputBinding.Keyboard(Keys.D2, 3) },
                { "assist", InputBinding.Keyboard(Keys.D3, 3) }, { "missing", InputBinding.Keyboard(Keys.D4, 3) }
            };
        }
        public static Dictionary<string, InputBinding> Copy(Dictionary<string, InputBinding> source)
        { return source.ToDictionary(x => x.Key, x => x.Value.Copy()); }
        public static void Normalize(Preferences settings)
        {
            var defaults = Defaults(settings.TKey);
            if (settings.Bindings == null) settings.Bindings = defaults;
            foreach (var action in Actions) if (!settings.Bindings.ContainsKey(action.Id) || settings.Bindings[action.Id] == null) settings.Bindings[action.Id] = defaults[action.Id];
            foreach (string extra in settings.Bindings.Keys.Where(x => !Actions.Any(a => a.Id == x)).ToArray()) settings.Bindings.Remove(extra);
        }
        public static bool IsModifier(int key)
        { return key == 16 || key == 17 || key == 18 || (key >= 160 && key <= 165); }
        public static string Validate(Dictionary<string, InputBinding> values)
        {
            if (values == null || Actions.Any(a => !values.ContainsKey(a.Id) || values[a.Id] == null)) return "按键配置不完整。";
            var used = new Dictionary<string, string>();
            foreach (BindingAction action in Actions)
            {
                InputBinding binding = values[action.Id];
                if (!binding.Enabled) continue;
                if (binding.Modifiers < 0 || binding.Modifiers > 7) return action.Name + "：仅支持 Ctrl、Alt、Shift 修饰键。";
                if (binding.Mouse)
                {
                    if (!new[] { 1, 2, 4, 5, 6 }.Contains(binding.Key)) return action.Name + "：鼠标键无效。";
                    if ((binding.Key == 1 || binding.Key == 2) && binding.Modifiers == 0) return "左键和右键需要搭配 Ctrl、Alt 或 Shift；中键和侧键可单独使用。";
                }
                else if (binding.Key < 8 || binding.Key > 254 || binding.Key == 27 || binding.Key == 91 || binding.Key == 92 || IsModifier(binding.Key))
                    return action.Name + "：请选择字母、数字、功能键等主键；Esc 保留用于取消。";
                string existing;
                if (used.TryGetValue(binding.Signature, out existing)) return "按键重复：" + existing + " 与 " + action.Name + " 都使用 " + binding + "。";
                used[binding.Signature] = action.Name;
            }
            return null;
        }
        public static BindingAction Find(Dictionary<string, InputBinding> values, int key, int modifiers, bool mouse, bool paused)
        {
            return Actions.FirstOrDefault(a => (!paused || a.WhilePaused) && values[a.Id].Enabled && values[a.Id].Mouse == mouse && values[a.Id].Key == key && values[a.Id].Modifiers == modifiers);
        }
        public static int ModifiersDown()
        { return (Native.Down((int)Keys.Menu) ? 1 : 0) | (Native.Down((int)Keys.ControlKey) ? 2 : 0) | (Native.Down((int)Keys.ShiftKey) ? 4 : 0); }
    }

    internal interface IHotkeyPlatform
    {
        bool Register(int id, int modifiers, int key);
        void Unregister(int id);
    }
    internal sealed class WindowsHotkeys : IHotkeyPlatform
    {
        readonly IntPtr handle;
        public WindowsHotkeys(IntPtr window) { handle = window; }
        public bool Register(int id, int modifiers, int key) { return Native.RegisterHotKey(handle, id, (uint)(modifiers | 0x4000), (uint)key); }
        public void Unregister(int id) { Native.UnregisterHotKey(handle, id); }
    }
    internal sealed class HotkeyRegistry : IDisposable
    {
        readonly IHotkeyPlatform platform;
        readonly List<int> registered = new List<int>();
        Dictionary<string, InputBinding> current;
        bool currentPaused;
        public string FailedAction { get; private set; }
        public HotkeyRegistry(IHotkeyPlatform target) { platform = target; }
        void Clear() { foreach (int id in registered) platform.Unregister(id); registered.Clear(); }
        bool RegisterAll(Dictionary<string, InputBinding> values, bool paused)
        {
            foreach (BindingAction action in KeyBindings.Actions)
            {
                InputBinding binding = values[action.Id];
                if (!binding.Enabled || binding.Mouse || (paused && !action.WhilePaused)) continue;
                if (!platform.Register(action.NativeId, binding.Modifiers, binding.Key)) { FailedAction = action.Id; return false; }
                registered.Add(action.NativeId);
            }
            return true;
        }
        public bool TryApply(Dictionary<string, InputBinding> values, bool paused, out string error)
        {
            FailedAction = null;
            error = KeyBindings.Validate(values); if (error != null) return false;
            Clear();
            if (!RegisterAll(values, paused))
            {
                string failed = FailedAction;
                error = KeyBindings.Actions.First(a => a.Id == failed).Name + "：" + values[failed] + " 已被其他程序占用，请换一组按键。";
                Clear();
                if (current != null && !RegisterAll(current, currentPaused)) error += " 原按键也发生占用变化，请重新设置。";
                FailedAction = failed; return false;
            }
            current = KeyBindings.Copy(values); currentPaused = paused; return true;
        }
        public void Dispose() { Clear(); }
    }
}
