using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PingTurntable
{
    internal static class Palette
    {
        public static readonly Color Background = Color.FromArgb(8, 17, 24);
        public static readonly Color Surface = Color.FromArgb(14, 27, 36);
        public static readonly Color Gold = Color.FromArgb(205, 177, 120);
        public static readonly Color Muted = Color.FromArgb(141, 162, 173);
        public static readonly Color Text = Color.FromArgb(233, 230, 218);
        public static readonly Color Cyan = Color.FromArgb(98, 193, 194);
        public static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        public static void Callout(Graphics g, RectangleF rect, string text, float opacity)
        {
            int alpha = (int)(Math.Max(0, Math.Min(1, opacity)) * 255);
            using (GraphicsPath path = Rounded(rect, 7))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(alpha * 220 / 255, 9, 22, 31)))
            using (Pen edge = new Pen(Color.FromArgb(alpha * 150 / 255, Gold), .8f))
            using (SolidBrush ink = new SolidBrush(Color.FromArgb(alpha, Text)))
            using (Font font = new Font("Microsoft YaHei UI", 11, FontStyle.Regular, GraphicsUnit.Pixel))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            { g.FillPath(fill, path); g.DrawPath(edge, path); g.DrawString(text, font, ink, rect, format); }
        }
    }
    internal sealed class StudioPanel : Panel
    {
        public StudioPanel() { DoubleBuffered = true; BackColor = Palette.Surface; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Color.FromArgb(38, Palette.Gold))) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            using (Pen pen = new Pen(Color.FromArgb(135, Palette.Gold))) { e.Graphics.DrawLine(pen, 0, 0, 38, 0); e.Graphics.DrawLine(pen, Width - 39, Height - 1, Width - 1, Height - 1); }
        }
    }
    internal sealed class StudioButton : Button
    {
        public bool Selected;
        bool hovered;
        public StudioButton()
        {
            DoubleBuffered = true; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            BackColor = Palette.Surface; ForeColor = Palette.Gold; Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Color top = Selected ? Color.FromArgb(30, 54, 63) : hovered ? Color.FromArgb(27, 45, 55) : Color.FromArgb(20, 35, 44);
            using (var fill = new LinearGradientBrush(ClientRectangle, top, Color.FromArgb(13, 25, 34), 90)) e.Graphics.FillRectangle(fill, ClientRectangle);
            using (Pen pen = new Pen(Color.FromArgb(Selected ? 180 : 75, Palette.Gold))) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            if (Selected) using (Pen pen = new Pen(Palette.Gold, 2)) e.Graphics.DrawLine(pen, 12, Height - 2, Width - 12, Height - 2);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : Palette.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), Palette.Gold, BackColor);
        }
    }
    internal sealed class BindingDialog : Form
    {
        readonly OverlayApp app;
        readonly Label value;
        bool closing;
        public InputBinding Binding { get; private set; }
        public BindingDialog(OverlayApp context, string action)
        {
            app = context; Text = "录制按键 · " + action; BackColor = Palette.Background; ForeColor = Palette.Text;
            Font = new Font("Microsoft YaHei UI", 9); ClientSize = new Size(520, 312); FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; MaximizeBox = false; MinimizeBox = false;
            var title = new Label { Text = "为「" + action + "」录制按键", Location = new Point(22, 22), Size = new Size(480, 28), Font = new Font(Font.FontFamily, 14), ForeColor = Palette.Gold }; Controls.Add(title);
            var hint = new Label { Text = "按下组合键，或在下方区域点击鼠标键。\n支持 Ctrl / Alt / Shift、功能键、中键与鼠标侧键。", Location = new Point(22, 65), Size = new Size(476, 46), ForeColor = Palette.Muted }; Controls.Add(hint);
            value = new Label { Text = "等待输入…", TextAlign = ContentAlignment.MiddleCenter, Location = new Point(22, 122), Size = new Size(476, 108), BackColor = Palette.Surface, ForeColor = Palette.Cyan, Font = new Font(Font.FontFamily, 16), AccessibleName = "按键录制区域" }; Controls.Add(value);
            var note = new Label { Text = "单独字母会被全局占用；Esc 取消。左 / 右键需搭配修饰键。", Location = new Point(22, 246), Size = new Size(476, 20), ForeColor = Palette.Muted, Font = new Font(Font.FontFamily, 8.5f) }; Controls.Add(note);
            var cancel = new StudioButton { Text = "取消", Location = new Point(402, 274), Size = new Size(96, 26), DialogResult = DialogResult.Cancel }; Controls.Add(cancel); CancelButton = cancel;
            Shown += (s, e) => { app.Cancel(); app.Capturer = this; };
            FormClosed += (s, e) => { if (app.Capturer == this) app.Capturer = null; };
        }
        public bool IsForeground { get { return !closing && Visible && Native.GetForegroundWindow() == Handle; } }
        public bool AcceptsMouse(Point screenPoint) { return IsForeground && value.RectangleToScreen(value.ClientRectangle).Contains(screenPoint); }
        public void Record(InputBinding binding)
        {
            if (closing) return;
            if (KeyBindings.IsModifier(binding.Key)) return;
            if (!binding.Mouse && (binding.Key == 91 || binding.Key == 92)) { value.Text = "请选择 Ctrl、Alt 或 Shift 组合"; return; }
            if (binding.Mouse && (binding.Key == 1 || binding.Key == 2) && binding.Modifiers == 0) { value.Text = "请搭配 Ctrl、Alt 或 Shift"; return; }
            closing = true; Binding = binding;
            BeginInvoke(new Action(() => { DialogResult = DialogResult.OK; Close(); }));
        }
        public void CancelRecording() { if (closing) return; closing = true; BeginInvoke(new Action(() => { DialogResult = DialogResult.Cancel; Close(); })); }
    }
}
