using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace PingTurntable
{
    public sealed class AssetRecord { public string file, url, sha256; public int bytes; }
    public sealed class AssetManifest { public string gameVersion; public List<AssetRecord> records, derived; }
    internal static class Verification
    {
        sealed class FakeHotkeys : IHotkeyPlatform
        {
            public readonly Dictionary<int, string> Owned = new Dictionary<int, string>();
            public readonly HashSet<string> External = new HashSet<string>();
            public static string Signature(int modifiers, int key) { return modifiers + ":" + key; }
            public bool Register(int id, int modifiers, int key)
            {
                string signature = Signature(modifiers, key);
                if (External.Contains(signature) || Owned.ContainsKey(id) || Owned.ContainsValue(signature)) return false;
                Owned[id] = signature; return true;
            }
            public void Unregister(int id) { Owned.Remove(id); }
        }
        static int checks;
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); ++checks; }
        static void VerifyBindings()
        {
            var defaults = KeyBindings.Defaults(); string error;
            Check(KeyBindings.Validate(defaults) == null, "Default binding set is valid");
            Check(defaults["ping"].ToString() == "Alt + 左键", "Mouse chord display");
            var changed = KeyBindings.Copy(defaults); changed["emote"] = InputBinding.Button(Keys.XButton1, 4);
            Check(KeyBindings.Find(changed, 5, 4, true, false).Id == "emote", "Shift + Mouse 4 trigger");
            Check(KeyBindings.Find(changed, 5, 0, true, false) == null, "Modifiers must match exactly");
            Check(KeyBindings.Find(defaults, (int)Keys.T, 1, false, true) == null, "Paused emote key is released");
            Check(KeyBindings.Find(defaults, (int)Keys.P, 3, false, true).Id == "pause", "Pause shortcut remains available");
            changed["emote"] = defaults["ping"].Copy(); Check(KeyBindings.Validate(changed) != null, "Duplicate chord is rejected");
            changed = KeyBindings.Copy(defaults); changed["emote"] = InputBinding.Button(Keys.LButton, 0);
            Check(KeyBindings.Validate(changed) != null, "Unmodified global primary mouse button is rejected");
            changed["emote"] = InputBinding.Keyboard(Keys.Escape, 0); Check(KeyBindings.Validate(changed) != null, "Esc remains cancellation");
            changed["emote"] = InputBinding.Keyboard(Keys.ShiftKey, 0); Check(KeyBindings.Validate(changed) != null, "Modifier-only binding is rejected");
            changed["emote"] = InputBinding.Off(); Check(KeyBindings.Validate(changed) == null, "Individual action may be disabled");
            var migration = new JavaScriptSerializer().Deserialize<Preferences>("{\"TKey\":true,\"Scale\":96,\"Emotes\":[1459,1030,1457,1505,1459]}");
            KeyBindings.Normalize(migration);
            Check(migration.Bindings["emote"].Key == (int)Keys.T && migration.Bindings["emote"].Modifiers == 0, "Legacy single T key is migrated");
            Check(migration.Scale == 96 && migration.Emotes[0] == 1459, "User scale and emotes are preserved");
            var roundtrip = new JavaScriptSerializer().Deserialize<Preferences>(new JavaScriptSerializer().Serialize(migration));
            Check(roundtrip.Bindings["emote"].Signature == migration.Bindings["emote"].Signature, "Binding persistence roundtrip");
            var platform = new FakeHotkeys();
            using (var registry = new HotkeyRegistry(platform))
            {
                Check(registry.TryApply(defaults, false, out error), "Initial global key registration");
                int count = platform.Owned.Count;
                changed = KeyBindings.Copy(defaults); changed["mute"] = InputBinding.Keyboard(Keys.F6, 0);
                platform.External.Add(FakeHotkeys.Signature(0, (int)Keys.F6));
                Check(!registry.TryApply(changed, false, out error), "External ownership conflict is rejected");
                Check(registry.FailedAction == "mute", "Conflicting action is identified");
                Check(platform.Owned.Count == count && platform.Owned[6] == FakeHotkeys.Signature(3, (int)Keys.M), "All previous keys are restored on failure");
                changed["mute"] = InputBinding.Keyboard(Keys.F7, 0);
                Check(registry.TryApply(changed, false, out error), "Free replacement key registers");
                Check(!platform.Owned.ContainsValue(FakeHotkeys.Signature(3, (int)Keys.M)), "Old binding is released immediately");
                Check(registry.TryApply(changed, true, out error) && platform.Owned.Count == 2, "Pause releases all signal keys and keeps recovery keys");
                Check(registry.TryApply(changed, false, out error) && platform.Owned.Count == count, "Resume restores active bindings");
            }
            Check(platform.Owned.Count == 0, "Shutdown releases every registered key");
            bool down, up;
            Check(Native.MouseButton(0x20B, 1u << 16, out down, out up) == 5 && down && !up, "Mouse 4 press decoding");
            Check(Native.MouseButton(0x20C, 2u << 16, out down, out up) == 6 && up && !down, "Mouse 5 release decoding");
        }
        public static int Run()
        {
            string output = Files.PathOf("verification"); Directory.CreateDirectory(output);
            try
            {
                checks = 0;
                VerifyBindings();
                Point origin = new Point(400, 300);
                Check(WheelMath.Select(new Point(400, 200), origin, 1, false) == 0, "Danger is north");
                Check(WheelMath.Select(new Point(500, 300), origin, 1, false) == 1, "On My Way is east");
                Check(WheelMath.Select(new Point(400, 400), origin, 1, false) == 2, "Assist Me is south");
                Check(WheelMath.Select(new Point(300, 300), origin, 1, false) == 3, "Enemy Missing is west");
                Check(WheelMath.Select(origin, origin, 1, false) == -1, "Center cancels ping");
                Check(WheelMath.Select(origin, origin, 1, true) == 4, "Center is fifth emote");
                Check(WheelMath.Select(new Point(400, 350), origin, 1.8, false) == -1, "Dead zone scales");
                Check(WheelMath.Select(new Point(-1800, 100), new Point(-1900, 100), 1, false) == 1, "Negative monitor coordinates");
                Check(WheelMath.ClampCenter(new Point(-2000, -500), new Rectangle(-1920, 0, 1920, 1080), 143) == new Point(-1777, 143), "Clamp against left-hand monitor");
                Check(WheelMath.ClampCenter(new Point(9999, 9999), new Rectangle(0, 0, 1920, 1080), 143) == new Point(1777, 937), "Clamp at screen edges");
                AssetManifest manifest = Files.Read<AssetManifest>("assets/manifest.json");
                Check(manifest.gameVersion == "12.21", "Historical version pinned");
                using (SHA256 hash = SHA256.Create()) foreach (AssetRecord record in manifest.records.Concat(manifest.derived ?? new List<AssetRecord>()))
                {
                    byte[] content = File.ReadAllBytes(Files.PathOf(record.file));
                    Check(content.Length == record.bytes, "Asset size: " + record.file);
                    string actual = BitConverter.ToString(hash.ComputeHash(content)).Replace("-", "").ToLowerInvariant();
                    Check(actual == record.sha256, "Asset hash: " + record.file);
                }
                Preferences prefs = new Preferences { Hextech = false, ShowLabels = false };
                using (Artwork art = new Artwork())
                {
                    Check(art.Emotes.Count == 15, "15 pre-12.22 emotes");
                    using (Bitmap sheet = new Bitmap(3 * 326, 2 * 326, PixelFormat.Format32bppArgb))
                    using (Graphics g = Graphics.FromImage(sheet))
                    {
                        g.Clear(Color.FromArgb(15, 26, 36));
                        for (int i = 0; i < 5; i++) using (Bitmap image = art.Wheel(false, i - 1, prefs, 1))
                        {
                            Check(image.Width == 286 && image.Height == 286, "Original 286px geometry");
                            Check(image.GetPixel(0, 0).A == 0, "Transparent corners");
                            g.DrawImageUnscaled(image, 20 + (i % 3) * 326, 20 + (i / 3) * 326);
                            image.Save(Path.Combine(output, "ping-" + i + ".png"), ImageFormat.Png);
                        }
                        using (Bitmap image = art.Wheel(true, 4, prefs, 1)) g.DrawImageUnscaled(image, 20 + 2 * 326, 20 + 326);
                        sheet.Save(Path.Combine(output, "wheel-preview.png"), ImageFormat.Png);
                    }
                    using (Bitmap neutral = art.Wheel(false, -1, prefs, 1))
                    using (Bitmap selected = art.Wheel(false, 0, prefs, 1))
                    {
                        string baselinePath = Path.Combine(output, "classic-baseline.png");
                        if (File.Exists(baselinePath)) using (Bitmap baseline = new Bitmap(baselinePath))
                        {
                            bool equal = baseline.Size == neutral.Size;
                            for (int y = 0; equal && y < neutral.Height; y++) for (int x = 0; x < neutral.Width; x++) if (neutral.GetPixel(x, y) != baseline.GetPixel(x, y)) { equal = false; break; }
                            Check(equal, "Classic appearance remains pixel-identical to the previous version");
                        }
                        int differences = 0;
                        for (int y = 0; y < 286; y++) for (int x = 0; x < 286; x++) if (neutral.GetPixel(x, y) != selected.GetPixel(x, y)) differences++;
                        Check(differences > 5000, "Selected wedge uses atlas highlight");
                    }
                    using (Bitmap scaled = art.Wheel(false, 0, prefs, 1.8)) Check(scaled.Width == 515, "High-DPI scale raster");
                    prefs.English = true;
                    using (Bitmap english = art.Wheel(false, 0, prefs, 1)) { english.Save(Path.Combine(output, "english-wheel.png"), ImageFormat.Png); Check(english.Width == 286, "Original English font render"); }
                    prefs.English = false;
                    using (Bitmap sheet = new Bitmap(900, 270)) using (Graphics g = Graphics.FromImage(sheet))
                    {
                        g.Clear(Color.FromArgb(22, 30, 41));
                        for (int i = 0; i < 4; i++) using (Bitmap effect = art.Effect(i, false, .3, 1)) g.DrawImageUnscaled(effect, i * 220 - 20, 0);
                        sheet.Save(Path.Combine(output, "ping-preview.png"), ImageFormat.Png);
                    }
                    foreach (Emote emote in art.Emotes) Check(art.Image("assets/emotes/" + emote.id + ".png").Width == 256, "Original emote size");
                    prefs.Hextech = true; prefs.ShowLabels = true;
                    using (Bitmap hex = art.Wheel(false, 0, prefs, 1))
                    {
                        Check(hex.Width == 326 && hex.GetPixel(0, 0).A == 0, "Hextech ornament expands canvas with transparent corners");
                        hex.Save(Path.Combine(output, "hextech-wheel.png"), ImageFormat.Png);
                    }
                    using (Bitmap fx = art.Effect(0, false, 2, 1, prefs))
                    using (Bitmap labelFree = art.Effect(0, false, 2, 1))
                    {
                        Check(fx.GetPixel(130, 213).A > 0, "Demonstration caption remains visible for the configured lifetime");
                        Check(labelFree.GetPixel(130, 213).A == 0, "Classic effect has no added caption");
                        fx.Save(Path.Combine(output, "hextech-marker.png"), ImageFormat.Png);
                    }
                }
                var report = new { success = true, checks = checks, version = "2.0 / assets 12.21", completedAt = DateTimeOffset.Now.ToString("o"), scope = "Shortcut migration, duplicates, external conflicts, transaction rollback, pause/resume key release, mouse side buttons, asset hashes, classic pixel regression, transparent render, hextech style and captions. Live native input is tested separately." };
                Files.Write("verification/report.json", report); Files.Log("SELF TEST PASSED: " + checks + " checks");
                return 0;
            }
            catch (Exception e)
            {
                Files.Write("verification/report.json", new { success = false, checks = checks, error = e.ToString() });
                Files.Log("SELF TEST FAILED: " + e); return 1;
            }
        }
    }
}
