using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Media;

namespace PingTurntable
{
    internal sealed class Artwork : IDisposable
    {
        readonly Dictionary<string, Bitmap> images = new Dictionary<string, Bitmap>();
        readonly List<Sprite> pingSprites = Files.Read<List<Sprite>>("assets/wheel/ping/elements.json");
        readonly List<Sprite> emoteSprites = Files.Read<List<Sprite>>("assets/wheel/emote/elements.json");
        readonly PrivateFontCollection gameFonts = new PrivateFontCollection();
        FontFamily englishFamily;
        public readonly List<Emote> Emotes = Files.Read<List<Emote>>("assets/emotes/catalog.json");
        public readonly string[] Names = { "危险", "正在路上", "协助我", "敌人不见了" };
        public readonly string[] EnglishNames = { "Danger", "On My Way", "Assist Me", "Enemy Missing" };
        public readonly string[] Sounds = { "Danger", "OnMyWay", "ComeHere", "MIA", "Base" };
        readonly string[] textureNames = { "ping-danger", "ping-omw_02", "ping-comehere_02", "ping-mia_02", "ping-caution_02" };
        readonly string[] ringNames = { "ping-danger-base", "ping-omw-base", "ping-comehere-base", "ping-mia-base", "ping-caution-base" };
        readonly string[] colorNames = { "ping-danger-base-color", "ping-omw-base-color", "ping-comehere-base-color", "ping-mia-base-color", "ping-caution-base-color" };
        public Artwork()
        {
            try
            {
                gameFonts.AddFontFile(Files.PathOf("assets/fonts/BeaufortForLoL-Bold.otf"));
                if (gameFonts.Families.Length > 0) englishFamily = gameFonts.Families[0];
            }
            catch (Exception e) { Files.Log("Original font unavailable, using system fallback: " + e.Message); }
            for (int i = 0; i < 5; ++i)
            {
                Bitmap original = Image("assets/pings/" + textureNames[i] + ".png");
                Rectangle bounds = AlphaBounds(original);
                images["icon" + i] = original.Clone(bounds, PixelFormat.Format32bppArgb);
                Bitmap ramp = Image("assets/pings/" + colorNames[i] + ".png");
                Color color = ramp.GetPixel(ramp.Width / 4, ramp.Height / 2);
                images["ring" + i] = AdditiveMask(Image("assets/pings/" + ringNames[i] + ".png"), color);
            }
        }
        public Bitmap Image(string path)
        {
            Bitmap bitmap;
            if (!images.TryGetValue(path, out bitmap))
            {
                using (var stream = File.OpenRead(Files.PathOf(path))) using (var source = new Bitmap(stream)) bitmap = new Bitmap(source);
                images[path] = bitmap;
            }
            return bitmap;
        }
        static Rectangle AlphaBounds(Bitmap image)
        {
            int left = image.Width, top = image.Height, right = 0, bottom = 0;
            for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
                if (image.GetPixel(x, y).A > 12) { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1); }
            return right > left ? Rectangle.FromLTRB(left, top, right, bottom) : new Rectangle(0, 0, image.Width, image.Height);
        }
        static Bitmap AdditiveMask(Bitmap source, Color tint)
        {
            Bitmap output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            for (int y = 0; y < source.Height; y++) for (int x = 0; x < source.Width; x++)
            {
                Color p = source.GetPixel(x, y);
                int a = Math.Max(p.R, Math.Max(p.G, p.B));
                output.SetPixel(x, y, Color.FromArgb(a, tint.R, tint.G, tint.B));
            }
            return output;
        }
        public Bitmap Icon(int index) { return images["icon" + index]; }
        public static void DrawAlpha(Graphics g, Bitmap bitmap, RectangleF dest, float opacity)
        {
            opacity = Math.Max(0, Math.Min(1, opacity));
            if (opacity >= .999f) { g.DrawImage(bitmap, dest); return; }
            using (ImageAttributes a = new ImageAttributes())
            {
                ColorMatrix matrix = new ColorMatrix(); matrix.Matrix33 = opacity; a.SetColorMatrix(matrix);
                g.DrawImage(bitmap, Rectangle.Round(dest), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, a);
            }
        }
        public Bitmap Wheel(bool emote, int selected, Preferences settings, double scale)
        {
            int padding = settings.Hextech ? 20 : 0;
            int size = Math.Max(1, (int)Math.Round((286 + padding * 2) * scale));
            Bitmap output = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            string kind = emote ? "emote" : "ping", prefix = emote ? "ContextWheel" : "PingWheel";
            List<Sprite> sprites = emote ? emoteSprites : pingSprites;
            using (Graphics g = Graphics.FromImage(output))
            {
                Quality(g); g.ScaleTransform((float)scale, (float)scale);
                g.TranslateTransform(padding, padding);
                if (settings.Hextech) DrawHextechFrame(g, selected);
                g.DrawImage(Image("assets/wheel/" + kind + "/base.png"), 0, 0, 286, 286);
                if (selected >= 0)
                {
                    Sprite hover = sprites.FirstOrDefault(x => x.name == prefix + "_" + selected + "_ButtonHover");
                    if (hover != null) DrawSprite(g, kind, hover);
                }
                if (!emote)
                {
                    foreach (Sprite sprite in sprites.Where(x => x.name.EndsWith("_Icon"))) DrawSprite(g, kind, sprite);
                    Sprite cross = sprites.First(x => x.name == "PingWheel_4_ButtonBase");
                    DrawSprite(g, kind, cross);
                    var positions = new RectangleF[] { new RectangleF(98, 78, 90, 16), new RectangleF(181, 156, 90, 16), new RectangleF(98, 244, 90, 16), new RectangleF(16, 156, 90, 16) };
                    using (Font font = MakeWheelFont(settings.English))
                    using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(250, 250, 205)))
                    using (SolidBrush shadow = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            string text = settings.English ? EnglishNames[i] : Names[i];
                            RectangleF r = positions[i], s = r; s.Offset(1, 1);
                            g.DrawString(text, font, shadow, s, format); g.DrawString(text, font, brush, r, format);
                        }
                    }
                }
                else
                {
                    RectangleF[] slots = { new RectangleF(95, 6, 96, 96), new RectangleF(184, 95, 96, 96), new RectangleF(95, 184, 96, 96), new RectangleF(6, 95, 96, 96), new RectangleF(103, 103, 80, 80) };
                    for (int i = 0; i < 5; i++)
                    {
                        int id = Emotes.Any(x => x.id == settings.Emotes[i]) ? settings.Emotes[i] : 1459;
                        g.DrawImage(Image("assets/emotes/" + id + ".png"), slots[i]);
                    }
                }
            }
            return output;
        }
        static void DrawHextechFrame(Graphics g, int selected)
        {
            // Optional original desktop ornament. The historical game wheel is drawn unchanged above it.
            using (GraphicsPath halo = new GraphicsPath())
            {
                halo.AddEllipse(-17, -17, 320, 320);
                GraphicsState saved = g.Save();
                using (GraphicsPath inside = new GraphicsPath())
                {
                    inside.AddEllipse(-2, -2, 290, 290);
                    using (Region outside = new Region(new RectangleF(-20, -20, 326, 326))) { outside.Exclude(inside); g.SetClip(outside, CombineMode.Intersect); }
                }
                using (PathGradientBrush glow = new PathGradientBrush(halo))
                {
                    glow.CenterColor = Color.FromArgb(18, Palette.Gold); glow.SurroundColors = new[] { Color.Transparent };
                    glow.FocusScales = new PointF(.8f, .8f); g.FillPath(glow, halo);
                }
                g.Restore(saved);
            }
            using (Pen guide = new Pen(Color.FromArgb(72, Palette.Gold), .7f))
            using (Pen fine = new Pen(Color.FromArgb(27, Palette.Gold), .7f))
            {
                for (int i = 0; i < 4; i++) { g.DrawArc(guide, -9, -9, 304, 304, -81 + 90 * i, 72); g.DrawArc(fine, -14, -14, 314, 314, -65 + 90 * i, 40); }
            }
            if (selected >= 0 && selected < 4)
            {
                using (Pen glow = new Pen(Color.FromArgb(48, Palette.Cyan), 5)) g.DrawArc(glow, -9, -9, 304, 304, -107 + selected * 90, 34);
                using (Pen highlight = new Pen(Color.FromArgb(192, Palette.Gold), 1.2f)) g.DrawArc(highlight, -9, -9, 304, 304, -107 + selected * 90, 34);
            }
            using (SolidBrush ink = new SolidBrush(Color.FromArgb(170, Palette.Gold)))
            {
                foreach (PointF p in new[] { new PointF(143, -9), new PointF(295, 143), new PointF(143, 295), new PointF(-9, 143) })
                    g.FillPolygon(ink, new[] { new PointF(p.X, p.Y - 2.8f), new PointF(p.X + 2.1f, p.Y), new PointF(p.X, p.Y + 2.8f), new PointF(p.X - 2.1f, p.Y) });
            }
        }
        void DrawSprite(Graphics g, string kind, Sprite sprite)
        {
            var r = sprite.rect;
            g.DrawImage(Image("assets/wheel/" + kind + "/" + sprite.name + ".png"), r[0], r[1], r[2] - r[0], r[3] - r[1]);
        }
        Font MakeWheelFont(bool english)
        {
            if (english && englishFamily != null)
            {
                FontStyle style = englishFamily.IsStyleAvailable(FontStyle.Bold) ? FontStyle.Bold : FontStyle.Regular;
                return new Font(englishFamily, 12f, style, GraphicsUnit.Pixel);
            }
            return new Font(english ? "Georgia" : "Microsoft YaHei UI", english ? 11.5f : 12f, english ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        }
        public Bitmap Effect(int index, bool emote, double elapsed, double scale, Preferences settings = null)
        {
            double lifetime = settings == null ? (emote ? 2.8 : 1.85) : settings.EffectMilliseconds / 1000.0;
            bool reduced = settings != null && settings.ReducedMotion;
            int size = Math.Max(1, (int)Math.Round(260 * scale));
            Bitmap output = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(output))
            {
                Quality(g); g.ScaleTransform((float)scale, (float)scale);
                if (emote)
                {
                    // Original 12.21 emote inventory artwork, with a desktop pop/fade.
                    float t = (float)Math.Min(1, elapsed / .16);
                    float pop = reduced ? 1 : .8f + .2f * (1 - (1 - t) * (1 - t));
                    float alpha = (float)Math.Min(1, Math.Max(0, (lifetime - elapsed) / .35));
                    float w = 148 * pop;
                    DrawAlpha(g, Image("assets/emotes/" + index + ".png"), new RectangleF(130 - w / 2, 105 - w / 2, w, w), alpha);
                }
                else
                {
                    float fade = (float)Math.Min(1, Math.Max(0, (lifetime - elapsed) / .45));
                    float appear = (float)Math.Min(1, elapsed / .09);
                    for (int n = 0; n < (reduced ? 1 : 2); n++)
                    {
                        double phase = reduced ? .5 : elapsed * 1.45 - n * .36;
                        if (phase < 0 || phase > 1.7) continue;
                        float radius = 42 + (float)phase * 68;
                        float a = fade * (float)Math.Max(0, 1 - phase / 1.7);
                        DrawAlpha(g, images["ring" + index], new RectangleF(130 - radius / 2, 178 - radius * .23f, radius, radius * .46f), a);
                    }
                    Bitmap icon = images["icon" + index];
                    float h = index == 3 ? 69 : 78;
                    float w = h * icon.Width / icon.Height;
                    float bounce = reduced ? 0 : (float)(Math.Sin(Math.Min(1, elapsed / .23) * Math.PI) * 10);
                    DrawAlpha(g, icon, new RectangleF(130 - w / 2, 166 - h - bounce, w, h), fade * appear);
                }
                if (settings != null && settings.ShowLabels)
                {
                    string label;
                    if (emote) { Emote entry = Emotes.First(x => x.id == index); label = settings.English ? entry.name : entry.nameZh; }
                    else label = settings.English ? EnglishNames[index] : Names[index];
                    float alpha = (float)Math.Min(1, Math.Max(0, (lifetime - elapsed) / .35));
                    Palette.Callout(g, new RectangleF(43, 201, 174, 27), label, alpha);
                }
            }
            return output;
        }
        public static void Quality(Graphics g)
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }
        public void Dispose() { foreach (Bitmap image in images.Values) image.Dispose(); gameFonts.Dispose(); }
    }

    internal sealed class AudioBank : IDisposable
    {
        readonly Dictionary<string, SoundPlayer> players = new Dictionary<string, SoundPlayer>();
        readonly List<MemoryStream> streams = new List<MemoryStream>();
        int volume = -1;
        public void Play(string name, Preferences settings)
        {
            if (!settings.Sound || settings.Volume == 0) return;
            try
            {
                if (volume != settings.Volume) { Clear(); volume = settings.Volume; }
                SoundPlayer player;
                if (!players.TryGetValue(name, out player))
                {
                    byte[] bytes = File.ReadAllBytes(Files.PathOf("assets/sounds/" + name + ".wav"));
                    // The source files are PCM16 WAV. Attenuate this app, without changing system volume.
                    int position = 12;
                    while (position + 8 <= bytes.Length)
                    {
                        string chunk = System.Text.Encoding.ASCII.GetString(bytes, position, 4);
                        int length = BitConverter.ToInt32(bytes, position + 4);
                        if (length < 0 || position + 8L + length > bytes.Length) break;
                        if (chunk == "data")
                        {
                            for (int i = position + 8; i + 1 < position + 8 + length; i += 2)
                            {
                                short value = (short)(BitConverter.ToInt16(bytes, i) * volume / 100);
                                bytes[i] = (byte)(value & 255); bytes[i + 1] = (byte)((value >> 8) & 255);
                            }
                            break;
                        }
                        position += 8 + length + (length & 1);
                    }
                    MemoryStream stream = new MemoryStream(bytes); streams.Add(stream);
                    player = new SoundPlayer(stream); player.Load(); players[name] = player;
                }
                player.Play();
            }
            catch (Exception e) { Files.Log("Audio: " + e.Message); }
        }
        void Clear() { foreach (var p in players.Values) { p.Stop(); p.Dispose(); } players.Clear(); foreach (var s in streams) s.Dispose(); streams.Clear(); }
        public void Dispose() { Clear(); }
    }
}
