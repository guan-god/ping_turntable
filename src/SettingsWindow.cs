using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PingTurntable
{
    internal sealed class WheelPreview : Control
    {
        public bool Emotes;
        readonly OverlayApp app;
        int choice = -1;
        public WheelPreview(OverlayApp context)
        { app = context; DoubleBuffered = true; Size = new Size(332, 332); AccessibleName = "轮盘实时预览"; Cursor = Cursors.Hand; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Artwork.Quality(e.Graphics);
            using (Bitmap image = app.Art.Wheel(Emotes, choice, app.Settings, 1)) e.Graphics.DrawImageUnscaled(image, (Width - image.Width) / 2, (Height - image.Height) / 2);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        { base.OnMouseMove(e); int next = WheelMath.Select(e.Location, new Point(Width / 2, Height / 2), 1, Emotes); if (next != choice) { choice = next; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { choice = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int selection = WheelMath.Select(e.Location, new Point(Width / 2, Height / 2), 1, Emotes);
                if (selection >= 0) app.Emit(Emotes ? app.Settings.Emotes[selection] : selection, Emotes, PointToScreen(new Point(Width / 2, Height / 2)));
            }
            base.OnMouseUp(e);
        }
    }

    internal sealed class SettingsWindow : Form
    {
        readonly OverlayApp app;
        readonly WheelPreview preview;
        readonly StudioPanel[] pages = new StudioPanel[6];
        readonly StudioButton[] tabs = new StudioButton[6];
        readonly Dictionary<string, StudioButton> bindingButtons = new Dictionary<string, StudioButton>();
        readonly CheckBox pinned, sound, positionLock, labels, reduced;
        readonly ComboBox language, style, duration;
        readonly TrackBar scale, volume, idle;
        readonly Label scaleValue, volumeValue, idleValue, status, help, modeLabel, count, bindingNotice;
        readonly StudioButton pause, mode, mute, replay;
        readonly ComboBox[] slots = new ComboBox[5];
        readonly Panel historyRows;
        readonly ToolTip tooltips = new ToolTip { AutoPopDelay = 7000, InitialDelay = 500 };
        bool refreshing;
        int currentTab;
        int renderedHistoryCount = -1;
        public SettingsWindow(OverlayApp context)
        {
            app = context; Text = "信号轮盘 · 海克斯工作台";
            BackColor = Palette.Background; ForeColor = Palette.Text; Font = new Font("Microsoft YaHei UI", 9);
            ClientSize = new Size(1080, 720); MinimumSize = new Size(1096, 759); MaximumSize = MinimumSize;
            StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; AutoScaleMode = AutoScaleMode.None;
            Icon = SystemIcons.Application; DoubleBuffered = true;
            LabelAt(this, "CLASSIC SIGNALS  /  PERSONAL STUDIO", 28, 18, 700, 20, Palette.Gold, 8.5f);
            LabelAt(this, "把默契，放在指尖。", 25, 43, 730, 39, Palette.Text, 23);
            LabelAt(this, "经典四向原图  ·  五格表情  ·  你的键位与节奏", 28, 89, 850, 22, Palette.Muted, 9);
            count = LabelAt(this, "", 870, 54, 185, 28, Palette.Cyan, 11);
            var divider = new Panel { Location = new Point(28, 117), Size = new Size(1026, 1), BackColor = Color.FromArgb(54, 61, 56) }; Controls.Add(divider);
            var left = new StudioPanel { Location = new Point(28, 130), Size = new Size(342, 426) }; Controls.Add(left);
            LabelAt(left, "LIVE PREVIEW", 18, 16, 300, 20, Palette.Muted, 8.5f);
            preview = new WheelPreview(app) { Location = new Point(5, 44), BackColor = Palette.Surface }; left.Controls.Add(preview);
            modeLabel = LabelAt(left, "", 18, 389, 310, 22, Palette.Gold, 9);
            mode = ButtonAt(this, "", 28, 573, 342, 32); mode.Click += (s, e) => app.ToggleMode();
            replay = ButtonAt(this, "重放上次", 28, 618, 108, 30); replay.Click += (s, e) => app.ReplayLast(false);
            var clear = ButtonAt(this, "清除标记", 145, 618, 108, 30); clear.Click += (s, e) => app.ClearEffects();
            mute = ButtonAt(this, "", 262, 618, 108, 30); mute.Click += (s, e) => app.ToggleSound();
            tooltips.SetToolTip(replay, "在上次的位置重放。快捷键则在当前鼠标位置重放。");
            string[] names = { "常规", "自定按键", "表情", "外观", "最近标记", "来源" };
            for (int i = 0; i < 6; ++i)
            {
                int index = i; tabs[i] = ButtonAt(this, names[i], 396 + i * 111, 130, 103, 34); tabs[i].Click += (s, e) => SelectTab(index);
                pages[i] = new StudioPanel { Location = new Point(396, 181), Size = new Size(658, 424), Visible = false }; Controls.Add(pages[i]);
            }
            Panel general = pages[0];
            pinned = CheckAt(general, "常驻显示轮盘", 22, 21); positionLock = CheckAt(general, "锁定常驻位置", 342, 21);
            sound = CheckAt(general, "播放信号音效", 22, 66); labels = CheckAt(general, "标记下显示文字说明", 342, 66);
            pinned.CheckedChanged += (s, e) => Edit(() => app.Settings.Pinned = pinned.Checked);
            positionLock.CheckedChanged += (s, e) => Edit(() => app.Settings.LockPosition = positionLock.Checked);
            sound.CheckedChanged += (s, e) => Edit(() => app.Settings.Sound = sound.Checked);
            labels.CheckedChanged += (s, e) => Edit(() => app.Settings.ShowLabels = labels.Checked);
            LabelAt(general, "轮盘语言", 22, 122, 110, 23, Palette.Muted, 10);
            language = ComboAt(general, 148, 117, 170); language.Items.AddRange(new object[] { "简体中文", "English" });
            language.SelectedIndexChanged += (s, e) => Edit(() => app.Settings.English = language.SelectedIndex == 1);
            LabelAt(general, "标记停留", 342, 122, 100, 23, Palette.Muted, 10);
            duration = ComboAt(general, 446, 117, 184); duration.Items.AddRange(new object[] { "0.8 秒", "1.2 秒", "1.8 秒", "2.4 秒", "3.5 秒", "5 秒", "6 秒" });
            duration.SelectedIndexChanged += (s, e) => { if (duration.SelectedIndex >= 0) Edit(() => app.Settings.EffectMilliseconds = new[] { 800, 1200, 1800, 2400, 3500, 5000, 6000 }[duration.SelectedIndex]); };
            LabelAt(general, "轮盘缩放", 22, 185, 120, 24, Palette.Muted, 10);
            scaleValue = LabelAt(general, "", 558, 185, 72, 24, Palette.Gold, 10); scale = SliderAt(general, 148, 177, 390, 60, 180, 20, "轮盘缩放");
            scale.ValueChanged += (s, e) => Edit(() => app.Settings.Scale = scale.Value);
            LabelAt(general, "信号音量", 22, 246, 120, 24, Palette.Muted, 10);
            volumeValue = LabelAt(general, "", 558, 246, 72, 24, Palette.Gold, 10); volume = SliderAt(general, 148, 238, 390, 0, 100, 20, "信号音量");
            volume.ValueChanged += (s, e) => Edit(() => app.Settings.Volume = volume.Value);
            LabelAt(general, "Shift + 拖动可移动常驻轮盘；锁定后可避免误拖。\n暂停会释放信号键，恢复键和设置键仍有效。", 22, 314, 596, 49, Palette.Muted, 9);
            var resetPosition = ButtonAt(general, "将轮盘放到当前屏幕右下", 22, 376, 282, 28); resetPosition.Click += (s, e) => app.ResetPosition();

            Panel keyPage = pages[1];
            LabelAt(keyPage, "点按键框开始录制。保存成功即生效，冲突时保留原配置。", 18, 14, 620, 25, Palette.Muted, 9);
            var keyRows = new Panel { Location = new Point(12, 49), Size = new Size(634, 302), AutoScroll = true, BackColor = Palette.Surface }; keyPage.Controls.Add(keyRows);
            for (int i = 0; i < KeyBindings.Actions.Length; i++)
            {
                BindingAction action = KeyBindings.Actions[i]; int y = i * 55;
                LabelAt(keyRows, action.Name, 8, y + 6, 191, 22, Palette.Text, 9.5f);
                LabelAt(keyRows, action.Hint, 8, y + 29, 191, 18, Palette.Muted, 8);
                var record = ButtonAt(keyRows, "", 204, y + 8, 320, 34); record.AccessibleName = "录制按键：" + action.Name;
                record.Click += (s, e) => Record(action); bindingButtons[action.Id] = record;
                var disable = ButtonAt(keyRows, "停用", 535, y + 8, 61, 34); disable.AccessibleName = "停用按键：" + action.Name;
                disable.Click += (s, e) => { var candidate = KeyBindings.Copy(app.Settings.Bindings); candidate[action.Id] = InputBinding.Off(); Apply(candidate); };
            }
            keyRows.AutoScrollMinSize = new Size(0, KeyBindings.Actions.Length * 55);
            bindingNotice = LabelAt(keyPage, "", 18, 363, 466, 48, Palette.Cyan, 8.5f);
            var defaults = ButtonAt(keyPage, "恢复默认键位", 500, 372, 138, 30); defaults.Click += (s, e) => RestoreDefaults();

            Panel emotePage = pages[2];
            LabelAt(emotePage, "五个位置，自由搭配。全部使用 12.21 已有的表情原图。", 22, 18, 610, 25, Palette.Gold, 9.5f);
            string[] slotNames = { "上方", "右侧", "下方", "左侧", "中央 · 轻按" };
            for (int i = 0; i < 5; ++i)
            {
                int slot = i; LabelAt(emotePage, slotNames[i], 22, 65 + i * 49, 140, 26, Palette.Muted, 10);
                slots[i] = ComboAt(emotePage, 172, 60 + i * 49, 456); slots[i].Items.AddRange(app.Art.Emotes.Cast<object>().ToArray());
                slots[i].SelectedIndexChanged += (s, e) => { if (slots[slot].SelectedItem != null) Edit(() => app.Settings.Emotes[slot] = ((Emote)slots[slot].SelectedItem).id); };
            }
            LabelAt(emotePage, "点击原图试发", 22, 318, 200, 21, Palette.Muted, 9);
            int[] samples = { 1459, 1492, 1030, 1445, 1457, 1499, 1501, 1505, 3153, 3154 };
            for (int i = 0; i < samples.Length; ++i)
            {
                int id = samples[i]; var image = new PictureBox { Image = app.Art.Image("assets/emotes/" + id + ".png"), SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(22 + 61 * i, 351), Size = new Size(49, 49), Cursor = Cursors.Hand, AccessibleName = "试发表情 " + id };
                image.Click += (s, e) => app.Preview(id, true); emotePage.Controls.Add(image);
            }
            Panel appearance = pages[3];
            LabelAt(appearance, "克制一些，也能有自己的光。", 22, 20, 610, 28, Palette.Gold, 13);
            LabelAt(appearance, "轮盘风格", 22, 80, 120, 23, Palette.Muted, 10);
            style = ComboAt(appearance, 172, 74, 456); style.Items.AddRange(new object[] { "经典原版 · 12.21", "海克斯 · 鎏金细线与微光" });
            style.SelectedIndexChanged += (s, e) => Edit(() => app.Settings.Hextech = style.SelectedIndex == 1);
            LabelAt(appearance, "海克斯装饰只加在原轮盘外围，原图与方向布局保持原样。\n经典选项可以完整关闭这些新增装饰。", 22, 124, 607, 48, Palette.Muted, 9);
            LabelAt(appearance, "待机透明度", 22, 203, 140, 24, Palette.Muted, 10);
            idleValue = LabelAt(appearance, "", 558, 203, 72, 24, Palette.Gold, 10); idle = SliderAt(appearance, 172, 195, 366, 25, 100, 25, "常驻轮盘待机透明度");
            idle.ValueChanged += (s, e) => Edit(() => app.Settings.IdleOpacity = idle.Value);
            LabelAt(appearance, "移开鼠标时降低存在感，悬停时恢复清晰显示。", 22, 248, 607, 23, Palette.Muted, 9);
            reduced = CheckAt(appearance, "减少动态效果", 22, 304); reduced.CheckedChanged += (s, e) => Edit(() => app.Settings.ReducedMotion = reduced.Checked);
            LabelAt(appearance, "减少弹跳和扩散运动，保留淡出反馈。\n文字标注与停留时间可在「常规」中调整。", 22, 346, 607, 46, Palette.Muted, 9);

            LabelAt(pages[4], "最近 8 次标记", 22, 18, 610, 27, Palette.Gold, 12);
            LabelAt(pages[4], "点「重放」会在原位置再次显示；仅保留本次运行的记录。", 22, 54, 610, 24, Palette.Muted, 9);
            historyRows = new Panel { Location = new Point(18, 92), Size = new Size(622, 313), AutoScroll = true, BackColor = Palette.Surface }; pages[4].Controls.Add(historyRows);

            var source = new TextBox { Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical, Location = new Point(22, 20), Size = new Size(610, 382), BackColor = Palette.Surface, ForeColor = Palette.Muted, Font = new Font(Font.FontFamily, 9),
                Text = "历史基准：League of Legends 12.21\r\n\r\n四向信号、五格表情、金色边框与蓝色选区均使用历史游戏图集和 UI 坐标，资源 URL 与 SHA-256 可在 assets/manifest.json 中核对。\r\n\r\n新创作部分：外围海克斯细线与光晕、文字标注、工作台界面。它们是本工具的可选设计，不冒充游戏原版素材。\r\n\r\n英文加载原版 Beaufort 字体；中文字体与桌面动画仍存在适配差异。音效来自公开提取 WAV，未与 12.21 音频逐样本比对。\r\n\r\n按键、停留时间、音量等保存在 settings.json；最近标记只在内存中保留，重启清空。应用本身不访问网络。\r\n\r\n信号仅在本机屏幕显示，不会发送给游戏队友或聊天软件。\r\n\r\n完整来源与说明：SOURCES.md / README.md\r\n\r\n© Riot Games, Inc. 游戏素材归原权利人所有，本同人项目无官方关联。" }; pages[5].Controls.Add(source);
            help = LabelAt(this, "", 396, 623, 658, 43, Palette.Muted, 8.5f);
            status = LabelAt(this, "", 28, 684, 770, 23, Palette.Cyan, 9);
            pause = ButtonAt(this, "", 827, 674, 112, 32); pause.Click += (s, e) => app.TogglePause();
            var hide = ButtonAt(this, "收起到托盘", 949, 674, 105, 32); hide.Click += (s, e) => Hide();
            FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            app.StateChanged += RefreshValues; Disposed += (s, e) => { app.StateChanged -= RefreshValues; tooltips.Dispose(); };
            RefreshValues(); SelectTab(0);
        }
        void Edit(Action change) { if (refreshing) return; change(); app.Changed(); }
        void SelectTab(int index)
        {
            currentTab = index;
            for (int i = 0; i < 6; ++i) { pages[i].Visible = i == index; tabs[i].Selected = i == index; tabs[i].Invalidate(); }
            preview.Emotes = index == 2 || app.PinnedEmotes; preview.Invalidate();
            modeLabel.Text = (app.Settings.Hextech ? "海克斯微光" : "经典原版") + (preview.Emotes ? "  /  五格表情" : "  /  四向信号");
        }
        void Record(BindingAction action)
        {
            using (var dialog = new BindingDialog(app, action.Name))
                if (dialog.ShowDialog(this) == DialogResult.OK)
                { var candidate = KeyBindings.Copy(app.Settings.Bindings); candidate[action.Id] = dialog.Binding; Apply(candidate); }
        }
        void Apply(Dictionary<string, InputBinding> values)
        {
            string error;
            if (!app.TryBindings(values, out error)) { bindingNotice.Text = error; bindingNotice.ForeColor = Color.FromArgb(231, 154, 132); }
        }
        void RestoreDefaults()
        {
            var values = KeyBindings.Defaults();
            if (app.Settings.Bindings["pause"].Enabled && app.Settings.Bindings["pause"].Modifiers == 7 && app.Settings.Bindings["pause"].Key == (int)Keys.P)
                values["pause"] = app.Settings.Bindings["pause"].Copy();
            Apply(values);
        }
        void RefreshValues()
        {
            if (IsDisposed) return; refreshing = true;
            pinned.Checked = app.Settings.Pinned; sound.Checked = app.Settings.Sound; positionLock.Checked = app.Settings.LockPosition; labels.Checked = app.Settings.ShowLabels;
            language.SelectedIndex = app.Settings.English ? 1 : 0; style.SelectedIndex = app.Settings.Hextech ? 1 : 0; reduced.Checked = app.Settings.ReducedMotion;
            int[] times = { 800, 1200, 1800, 2400, 3500, 5000, 6000 };
            duration.SelectedIndex = Enumerable.Range(0, times.Length).OrderBy(i => Math.Abs(times[i] - app.Settings.EffectMilliseconds)).First();
            scale.Value = app.Settings.Scale; volume.Value = app.Settings.Volume; idle.Value = app.Settings.IdleOpacity;
            scaleValue.Text = app.Settings.Scale + "%"; volumeValue.Text = app.Settings.Volume + "%"; idleValue.Text = app.Settings.IdleOpacity + "%";
            for (int i = 0; i < 5; ++i) slots[i].SelectedItem = app.Art.Emotes.First(x => x.id == app.Settings.Emotes[i]);
            foreach (BindingAction action in KeyBindings.Actions)
            {
                var button = bindingButtons[action.Id]; button.Text = app.BindingText(action.Id); tooltips.SetToolTip(button, "点击录制「" + action.Name + "」的新按键");
            }
            bindingNotice.Text = string.IsNullOrEmpty(app.BindingNotice) ? "Esc 保留取消。每项可停用，托盘菜单始终可打开设置。" : app.BindingNotice;
            bindingNotice.ForeColor = Palette.Cyan;
            preview.Emotes = currentTab == 2 || app.PinnedEmotes; preview.Invalidate();
            modeLabel.Text = (app.Settings.Hextech ? "海克斯微光" : "经典原版") + (preview.Emotes ? "  /  五格表情" : "  /  四向信号");
            mode.Text = app.PinnedEmotes ? "切换为信号轮盘" : "切换为表情轮盘";
            mute.Text = app.Settings.Sound ? "一键静音" : "开启声音"; replay.Enabled = app.History.Count > 0 && !app.Paused;
            count.Text = "本次标记  " + app.SessionCount.ToString("D2");
            status.Text = (app.Paused ? "●  已暂停   ·   恢复：" : "●  已启用   ·   暂停：") + app.PauseKey + "   ·   本机桌面标记";
            pause.Text = app.Paused ? "恢复轮盘" : "暂停轮盘";
            help.Text = "信号：" + app.BindingText("ping") + "    表情：" + app.EmoteKey + "    Esc / 右键取消\n更多操作可在「自定按键」中录制，也可随时从托盘打开设置。";
            if (renderedHistoryCount != app.SessionCount) { RefreshHistory(); renderedHistoryCount = app.SessionCount; }
            refreshing = false;
        }
        void RefreshHistory()
        {
            historyRows.SuspendLayout();
            while (historyRows.Controls.Count > 0) { Control child = historyRows.Controls[0]; historyRows.Controls.RemoveAt(0); child.Dispose(); }
            if (app.History.Count == 0) LabelAt(historyRows, "还没有标记。点击左侧轮盘试一试。", 8, 60, 590, 40, Palette.Muted, 11);
            for (int i = 0; i < app.History.Count; ++i)
            {
                SignalRecord record = app.History[i]; int y = i * 62;
                var icon = new PictureBox { Location = new Point(8, y + 6), Size = new Size(44, 44), SizeMode = PictureBoxSizeMode.Zoom, Image = record.Emote ? app.Art.Image("assets/emotes/" + record.Index + ".png") : app.Art.Icon(record.Index) }; historyRows.Controls.Add(icon);
                LabelAt(historyRows, record.Label, 68, y + 5, 385, 22, Palette.Text, 9.5f);
                LabelAt(historyRows, record.At.ToString("HH:mm:ss") + "  ·  " + record.Position.X + ", " + record.Position.Y, 68, y + 29, 385, 18, Palette.Muted, 8);
                var play = ButtonAt(historyRows, "重放", 484, y + 11, 95, 30); play.Click += (s, e) => app.Replay(record, false);
            }
            historyRows.ResumeLayout();
        }
        static Label LabelAt(Control parent, string text, int x, int y, int w, int h, Color color, float size)
        {
            var label = new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h), ForeColor = color, Font = new Font("Microsoft YaHei UI", size), BackColor = Color.Transparent };
            parent.Controls.Add(label); return label;
        }
        static StudioButton ButtonAt(Control parent, string text, int x, int y, int w, int h)
        { var button = new StudioButton { Text = text, Location = new Point(x, y), Size = new Size(w, h) }; parent.Controls.Add(button); return button; }
        static CheckBox CheckAt(Control parent, string text, int x, int y)
        { var value = new CheckBox { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Palette.Text, Cursor = Cursors.Hand }; parent.Controls.Add(value); return value; }
        static ComboBox ComboAt(Control parent, int x, int y, int w)
        { var value = new ComboBox { Location = new Point(x, y), Size = new Size(w, 28), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(23, 41, 51), ForeColor = Palette.Text, DropDownHeight = 320 }; parent.Controls.Add(value); return value; }
        static TrackBar SliderAt(Control parent, int x, int y, int w, int min, int max, int tick, string name)
        { var value = new TrackBar { Location = new Point(x, y), Size = new Size(w, 42), Minimum = min, Maximum = max, TickFrequency = tick, SmallChange = 5, LargeChange = 10, BackColor = Palette.Surface, AccessibleName = name }; parent.Controls.Add(value); return value; }
    }
}
