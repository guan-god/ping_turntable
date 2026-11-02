using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace PingTurntable
{
    internal static class Files
    {
        public static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
        public static string PathOf(string relative) { return Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)); }
        public static T Read<T>(string relative) { return new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(PathOf(relative))); }
        public static void Write(string relative, object value)
        {
            string target = PathOf(relative), temp = target + ".tmp";
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(value), new System.Text.UTF8Encoding(false));
            if (File.Exists(target)) File.Replace(temp, target, null); else File.Move(temp, target);
        }
        public static void Log(string message)
        {
            try { File.AppendAllText(PathOf("runtime.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine); } catch { }
        }
    }

    public sealed class Preferences
    {
        public bool Pinned = true;
        public bool Paused = false;
        public bool PinnedEmotes = false;
        public bool Sound = true;
        public bool English = false;
        public bool TKey = false;
        public Dictionary<string, InputBinding> Bindings;
        public bool Hextech = true;
        public bool ShowLabels = true;
        public bool ReducedMotion = false;
        public bool LockPosition = false;
        public int IdleOpacity = 70;
        public int EffectMilliseconds = 2400;
        public int Scale = 100;
        public int Volume = 65;
        public int X = int.MinValue;
        public int Y = int.MinValue;
        public int[] Emotes = new int[] { 1492, 1030, 1457, 1505, 1459 };

        public static Preferences Load()
        {
            Preferences p;
            try { p = Files.Read<Preferences>("settings.json"); } catch { p = new Preferences(); }
            if (p == null) p = new Preferences();
            p.Scale = Math.Max(60, Math.Min(180, p.Scale));
            p.Volume = Math.Max(0, Math.Min(100, p.Volume));
            if (p.Emotes == null || p.Emotes.Length != 5) p.Emotes = new Preferences().Emotes;
            p.IdleOpacity = Math.Max(25, Math.Min(100, p.IdleOpacity));
            p.EffectMilliseconds = Math.Max(800, Math.Min(6000, p.EffectMilliseconds));
            KeyBindings.Normalize(p);
            return p;
        }
        public void Save() { try { Files.Write("settings.json", this); } catch (Exception e) { Files.Log("Settings save: " + e.Message); } }
    }
    public sealed class Emote
    {
        public int id;
        public string name, nameZh, url;
        public override string ToString() { return nameZh + " / " + name; }
    }
    public sealed class Sprite
    {
        public string name;
        public int[] rect;
        public bool enabled;
        public int layer;
    }

    internal static class WheelMath
    {
        // The 12.21 wheel has four cardinal sectors, with a central dead zone.
        public static int Select(Point cursor, Point origin, double scale, bool emote)
        {
            double dx = (cursor.X - origin.X) / scale, dy = (cursor.Y - origin.Y) / scale;
            if (dx * dx + dy * dy < (emote ? 42 * 42 : 34 * 34)) return emote ? 4 : -1;
            if (Math.Abs(dx) > Math.Abs(dy)) return dx > 0 ? 1 : 3;
            return dy > 0 ? 2 : 0;
        }
        public static Point ClampCenter(Point center, Rectangle bounds, int radius)
        {
            int rx = Math.Min(radius, bounds.Width / 2), ry = Math.Min(radius, bounds.Height / 2);
            return new Point(Math.Max(bounds.Left + rx, Math.Min(bounds.Right - rx, center.X)), Math.Max(bounds.Top + ry, Math.Min(bounds.Bottom - ry, center.Y)));
        }
    }
}
