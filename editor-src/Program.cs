using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace EntropyEditor;

static class Program { [STAThread] static void Main() => Application.Run(new MainForm()); }

class MainForm : Form
{
    record ParamDef(string Key, int Min, int Max, int Default, string LabelZh, string LabelEn);

    static readonly string[] PosNames = ["Top", "Jungle", "Mid", "Bottom", "Support"];
    static readonly string[] PosLabelsZh = ["上单", "打野", "中单", "ADC", "辅助"];

    static readonly ParamDef[] ChaosRecall = [
        new("recall_threshold_min",   1, 50, 22, "回城血量下限", "Recall HP min"),
        new("recall_threshold_max",   1, 50, 33, "回城血量上限", "Recall HP max"),
        new("overstay_min",           0, 50, 10, "赖线概率下限", "Overstay min"),
        new("overstay_max",           0, 50, 18, "赖线概率上限", "Overstay max"),
    ];
    static readonly ParamDef[] Comeback = [
        new("danger_hp",               1, 40, 15, "残血判定线", "Danger HP"),
        new("danger_floor",            1, 10,  3, "最低危险阈值", "Danger floor"),
        new("danger_ring_window",     16,256, 64, "滚动窗口大小", "Ring window"),
        new("threshold_reduce",        0, 20,  8, "劣势回城降低", "Loser recall"),
        new("overstay_increase",       0, 30, 15, "劣势赖线加成", "Loser stay"),
        new("safe_threshold_increase", 0, 20,  5, "优势回城提高", "Winner recall"),
    ];
    static readonly ParamDef[] Safety = [
        new("absolute_hp_panic", 50, 500, 150, "绝对血量保底", "Panic HP"),
    ];
    static readonly int[] PosRecallDefaults = [-8, 8, -2, 15, -3];
    static readonly int[] PosOverstayDefaults = [15, -8, 3, -10, 10];
    static readonly int[] PosDangerDefaults  = [-6, 5, 0, 10, -3];

    static readonly Color Accent = Color.FromArgb(52, 73, 94);
    static readonly Color Bg = Color.FromArgb(245, 247, 250);
    static readonly Color CardBg = Color.White;
    static readonly Color TextMain = Color.FromArgb(44, 62, 80);
    static readonly Color TextMuted = Color.FromArgb(127, 140, 141);
    static readonly Color BorderColor = Color.FromArgb(220, 225, 230);

    Dictionary<string, TrackBar> _sliders = new();
    Dictionary<string, NumericUpDown> _inputs = new();
    Dictionary<string, int> _values = new();
    List<(Label label, string zh, string en)> _langLabels = new();
    bool _isChinese = true;
    string _modDir;
    string _loadedFile = "";

    Label _titleLabel = null!;
    Label _configPath = null!;
    Label _statusLabel = null!;
    Button _saveBtn = null!;
    Button _loadDefaultBtn = null!;
    Button _loadUserBtn = null!;
    Button _browseBtn = null!;
    Button _reloadBtn = null!;
    Panel _scroll = null!;

    void LBtn(Button b, string zh, string en) { b.Text = _isChinese ? zh : en; }
    void RefreshBtnTexts()
    {
        LBtn(_browseBtn, "浏览并加载...", "Browse & Load...");
        LBtn(_loadDefaultBtn, "默认配置", "Default");
        LBtn(_loadUserBtn, "用户配置", "User");
        LBtn(_reloadBtn, "重新加载", "Reload");
        LBtn(_saveBtn, "保存到 config.user.toml", "Save to config.user.toml");
        foreach (Control c in _scroll.Controls)
            if (c is Panel bar && bar.Tag is string tag && tag == "presetBar")
                foreach (Control bc in bar.Controls)
                    if (bc is Button pb && pb.Tag is (string pzh, string pen))
                        pb.Text = _isChinese ? pzh : pen;
    }

    Label MakeLang(Control parent, string zh, string en, Point loc, Font font = null, Color? color = null, bool bold = false, int? w = null)
    {
        var l = new Label { Location = loc, Text = _isChinese ? zh : en, AutoSize = w == null,
            Font = font ?? Font, ForeColor = color ?? TextMain };
        if (bold) l.Font = new Font(l.Font, FontStyle.Bold);
        if (w.HasValue) l.Size = new Size(w.Value, l.Height);
        _langLabels.Add((l, zh, en));
        parent.Controls.Add(l);
        return l;
    }

    public MainForm()
    {
        _modDir = FindModDir();
        Application.EnableVisualStyles();
        Text = "Entropy Engine Config Editor";
        Size = new Size(740, 740); MinimumSize = new Size(740, 740);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        BackColor = Bg; Font = new Font("Microsoft YaHei UI", 9f);

        BuildUI();
        ResetDefaultsToMemory();
        RefreshUI();
    }

    void BuildUI()
    {
        _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Bg };
        Controls.Add(_scroll);

        var header = new Panel { Location = new Point(0, 0), Size = new Size(720, 100), BackColor = Accent };
        _scroll.Controls.Add(header);

        _titleLabel = new Label { Location = new Point(24, 16), AutoSize = true, ForeColor = Color.White, Font = new Font("Microsoft YaHei UI", 16, FontStyle.Bold) };
        header.Controls.Add(_titleLabel);

        new Label { Location = new Point(24, 48), AutoSize = true, Text = "TM2 Match Rhythm Overhaul", ForeColor = Color.FromArgb(189, 210, 235) }.Parent = header;

        var zhBtn = new Button { Text = "ZH", Location = new Point(530, 18), Size = new Size(40, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(100, 130, 170), ForeColor = Color.White, Font = new Font(Font, FontStyle.Bold), Cursor = Cursors.Hand };
        zhBtn.Click += (_, _) => { _isChinese = true; RefreshUI(); };
        header.Controls.Add(zhBtn);

        var enBtn = new Button { Text = "EN", Location = new Point(575, 18), Size = new Size(40, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 80, 110), ForeColor = Color.White, Font = new Font(Font, FontStyle.Bold), Cursor = Cursors.Hand };
        enBtn.Click += (_, _) => { _isChinese = false; RefreshUI(); };
        header.Controls.Add(enBtn);

        var y = 110;

        var cfgPanel = new Panel { Location = new Point(16, y), Size = new Size(688, 80), BackColor = CardBg };
        cfgPanel.Paint += (_, e) => { e.Graphics.DrawRectangle(new Pen(BorderColor, 1), 0, 0, cfgPanel.Width - 1, cfgPanel.Height - 1); };
        _scroll.Controls.Add(cfgPanel);

        MakeLang(cfgPanel, "加载配置", "Load Config", new Point(16, 10), bold: true);
        _configPath = MakeLang(cfgPanel, "尚未加载配置文件", "No config file loaded", new Point(16, 32), color: TextMuted);

        _browseBtn = new Button { Location = new Point(16, 52), Size = new Size(160, 26), FlatStyle = FlatStyle.Flat, BackColor = Accent, ForeColor = Color.White, Font = new Font(Font, FontStyle.Bold), Cursor = Cursors.Hand };
        _browseBtn.Click += (_, _) => {
            var ofd = new OpenFileDialog { Filter = "TOML files (*.toml)|*.toml|All files (*.*)|*.*", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) };
            if (ofd.ShowDialog() == DialogResult.OK) { LoadFileEx(ofd.FileName); RefreshUI(); }
        };
        cfgPanel.Controls.Add(_browseBtn);

        MakeLang(cfgPanel, "快捷：", "Quick:", new Point(190, 56), color: TextMuted);

        _loadDefaultBtn = new Button { Location = new Point(240, 52), Size = new Size(80, 26), FlatStyle = FlatStyle.Flat, BackColor = CardBg, Cursor = Cursors.Hand };
        _loadDefaultBtn.Click += (_, _) => { LoadFileEx(Path.Combine(_modDir, "config.toml")); RefreshUI(); };
        cfgPanel.Controls.Add(_loadDefaultBtn);

        _loadUserBtn = new Button { Location = new Point(326, 52), Size = new Size(80, 26), FlatStyle = FlatStyle.Flat, BackColor = CardBg, Cursor = Cursors.Hand };
        _loadUserBtn.Click += (_, _) => {
            var userPath = Path.Combine(_modDir, "config.user.toml");
            if (!File.Exists(userPath))
            {
                if (MessageBox.Show(this,
                    _isChinese ? "config.user.toml 不存在。\n\n要基于默认配置新建一个吗？" : "config.user.toml not found.\n\nCreate one from defaults?",
                    _isChinese ? "新建配置文件" : "Create config file",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                File.Copy(Path.Combine(_modDir, "config.toml"), userPath);
            }
            LoadFileEx(userPath); RefreshUI();
        };
        cfgPanel.Controls.Add(_loadUserBtn);

        _reloadBtn = new Button { Location = new Point(610, 52), Size = new Size(70, 26), FlatStyle = FlatStyle.Flat, BackColor = CardBg, Cursor = Cursors.Hand };
        _reloadBtn.Click += (_, _) => { if (!string.IsNullOrEmpty(_loadedFile)) { LoadFileEx(_loadedFile); RefreshUI(); } };
        cfgPanel.Controls.Add(_reloadBtn);

        y += 92;

        y = BuildSection("混沌回城 / Chaos Recall", "Chaos Recall", "越小越早回城，越大越容易赖线不回。", "Lower = earlier recall. Higher = more likely to overstay.", ChaosRecall, y);
        y = BuildSection("败方反扑 / Comeback", "Comeback", "滚动窗口追踪双方危险度。窗口越小越敏感，降低越多反扑越猛。", "Rolling danger window. Smaller = more sensitive. Higher reduce = stronger comeback.", Comeback, y);
        y = BuildSection("安全网 / Safety Net", "Safety Net", "HP低于此值时无条件逃命。", "Unconditional escape below this HP.", Safety, y);
        y = BuildPosSection("位置偏移 — 回城", "Recall Offsets", "负值=贪线晚回  正值=保守早回", "- = greedier  + = safer", "recall", PosRecallDefaults, y);
        y = BuildPosSection("位置偏移 — 赖线", "Overstay Offsets", "正值=倾向赖线  负值=倾向纪律回城", "+ = stay more  - = recall sooner", "overstay", PosOverstayDefaults, y);
        y = BuildPosSection("位置偏移 — 危险感知", "Danger Offsets", "负值=残血不慌  正值=残血更敏感", "- = fearless  + = cautious", "danger", PosDangerDefaults, y);

        var presetBar = new Panel { Location = new Point(16, y), Size = new Size(688, 46), BackColor = CardBg, Tag = "presetBar" };
        presetBar.Paint += (_, e) => { e.Graphics.DrawRectangle(new Pen(BorderColor, 1), 0, 0, presetBar.Width - 1, presetBar.Height - 1); };
        _scroll.Controls.Add(presetBar);

        MakeLang(presetBar, "预设方案:", "Presets:", new Point(16, 12), color: TextMuted);
        MakePresetBtn(presetBar, "默认", "Default", "default", new Point(90, 9), ResetDefaults);
        MakePresetBtn(presetBar, "激进", "Aggressive", "aggro", new Point(160, 9), ApplyAggressive);
        MakePresetBtn(presetBar, "保守", "Conservative", "cons", new Point(230, 9), ApplyConservative);
        MakePresetBtn(presetBar, "关闭反扑", "No Comeback", "nocb", new Point(300, 9), ApplyNoComeback);
        MakePresetBtn(presetBar, "还原原版", "Vanilla", "vanilla", new Point(385, 9), ApplyVanilla);
        y += 56;

        _saveBtn = new Button { Location = new Point(16, y), Size = new Size(688, 44), FlatStyle = FlatStyle.Flat, BackColor = Accent, ForeColor = Color.White, Font = new Font(Font, FontStyle.Bold), Cursor = Cursors.Hand };
        _saveBtn.Click += (_, _) => Save();
        _scroll.Controls.Add(_saveBtn);
        y += 54;

        _statusLabel = new Label { Location = new Point(24, y), AutoSize = true, ForeColor = TextMuted };
        _scroll.Controls.Add(_statusLabel);
        y += 30;
        _scroll.Controls.Add(new Panel { Location = new Point(0, y), Size = new Size(1, 40), BackColor = Bg });
    }

    void MakePresetBtn(Panel bar, string zh, string en, string tag, Point loc, Action action)
    {
        var b = new Button { Location = loc, Size = new Size(68, 28), FlatStyle = FlatStyle.Flat, BackColor = CardBg, Cursor = Cursors.Hand, Tag = (zh, en) };
        b.Text = _isChinese ? zh : en;
        b.Click += (_, _) => action();
        bar.Controls.Add(b);
    }

    int BuildSection(string titleZh, string titleEn, string descZh, string descEn, ParamDef[] defs, int y)
    {
        var card = BuildCard(defs.Length * 44 + 70, y); y += 10;
        MakeLang(card, titleZh, titleEn, new Point(16, 10), font: new Font(Font.Name, 11), bold: true);
        MakeLang(card, descZh, descEn, new Point(16, 34), font: new Font(Font.Name, 8), color: TextMuted);
        var yi = 58;
        foreach (var p in defs) yi = AddRow(card, p, yi);
        return y + card.Height + 14;
    }

    int BuildPosSection(string titleZh, string titleEn, string descZh, string descEn, string suffix, int[] defaults, int y)
    {
        var card = BuildCard(5 * 44 + 70, y); y += 10;
        MakeLang(card, titleZh, titleEn, new Point(16, 10), font: new Font(Font.Name, 11), bold: true);
        MakeLang(card, descZh, descEn, new Point(16, 34), font: new Font(Font.Name, 8), color: TextMuted);
        var yi = 58;
        for (int i = 0; i < 5; i++)
            yi = AddRow(card, new ParamDef($"pos_{PosNames[i].ToLower()}_{suffix}",
                suffix == "danger" ? -15 : (suffix == "overstay" ? -30 : -20),
                suffix == "danger" ? 20 : 30, defaults[i], PosLabelsZh[i], PosNames[i]), yi);
        return y + card.Height + 14;
    }

    Panel BuildCard(int height, int y)
    {
        var card = new Panel { Location = new Point(16, y), Size = new Size(688, height), BackColor = CardBg };
        card.Paint += (_, e) => { e.Graphics.DrawRectangle(new Pen(BorderColor, 1), 0, 0, card.Width - 1, card.Height - 1); };
        _scroll.Controls.Add(card);
        return card;
    }

    int AddRow(Panel card, ParamDef p, int y)
    {
        var label = new Label { Location = new Point(16, y + 8), Size = new Size(120, 22), ForeColor = TextMain, TextAlign = ContentAlignment.MiddleLeft };
        card.Controls.Add(label);
        _langLabels.Add((label, p.LabelZh, p.LabelEn));

        var input = new NumericUpDown { Location = new Point(140, y + 5), Size = new Size(56, 24), Minimum = p.Min, Maximum = p.Max, Value = p.Default, BorderStyle = BorderStyle.FixedSingle };
        input.ValueChanged += (_, _) => SyncFromInput(p.Key);
        card.Controls.Add(input);
        _inputs[p.Key] = input;

        var track = new TrackBar { Location = new Point(204, y + 2), Size = new Size(460, 28), Minimum = p.Min, Maximum = p.Max, Value = p.Default, TickStyle = TickStyle.None, BackColor = CardBg };
        track.ValueChanged += (_, _) => SyncFromSlider(p.Key);
        card.Controls.Add(track);
        _sliders[p.Key] = track;
        _values[p.Key] = p.Default;
        return y + 38;
    }

    void SyncFromSlider(string key) { if (_sliders.TryGetValue(key, out var t)) { _values[key] = t.Value; if (_inputs.TryGetValue(key, out var n) && n.Value != t.Value) n.Value = t.Value; } }
    void SyncFromInput(string key)  { if (_inputs.TryGetValue(key, out var n)) { _values[key] = (int)n.Value; if (_sliders.TryGetValue(key, out var t) && t.Value != (int)n.Value) t.Value = (int)n.Value; } }

    void RefreshUI()
    {
        _titleLabel.Text = _isChinese ? "熵引擎配置编辑器 v2.5" : "Entropy Engine Config Editor v2.5";
        foreach (var (label, zh, en) in _langLabels) label.Text = _isChinese ? zh : en;
        RefreshBtnTexts();
        if (!string.IsNullOrEmpty(_loadedFile))
            _configPath.Text = _isChinese ? $"当前: {_loadedFile}" : $"Loaded: {_loadedFile}";
    }

    void LoadFileEx(string path)
    {
        if (!File.Exists(path)) return;
        ResetDefaultsToMemory();
        LoadFile(path);
        _loadedFile = path;
        foreach (var (key, val) in _values)
        {
            if (_sliders.TryGetValue(key, out var t)) t.Value = Math.Clamp(val, t.Minimum, t.Maximum);
            if (_inputs.TryGetValue(key, out var n)) n.Value = Math.Clamp(val, (int)n.Minimum, (int)n.Maximum);
        }
    }

    void LoadFile(string path) { foreach (var line in File.ReadAllLines(path)) { var t = line.Trim(); if (t.Length == 0 || t[0] == '#' || t[0] == '[') continue; var eq = t.IndexOf('='); if (eq < 0) continue; if (int.TryParse(t[(eq + 1)..].Trim(), out var val)) _values[t[..eq].Trim().Trim('"')] = val; } }

    void Save()
    {
        var path = Path.Combine(_modDir, "config.user.toml");
        if (!File.Exists(path) && MessageBox.Show(this, _isChinese ? "将新建 config.user.toml。\n\n以后 mod 更新不会覆盖此文件。" : "Will create config.user.toml.\n\nIt won't be overwritten by updates.", _isChinese ? "确认保存" : "Confirm", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
        var lines = new List<string> { "# Entropy Engine V2.5 User Config", "# Priority: config.user.toml > config.toml > built-in defaults", "" };
        WriteSection(lines, "混沌回城", ChaosRecall); WriteSection(lines, "败方反扑", Comeback); WriteSection(lines, "安全网", Safety);
        WritePos(lines, "位置偏移-回城", "recall"); WritePos(lines, "位置偏移-赖线", "overstay"); WritePos(lines, "位置偏移-危险感知", "danger");
        File.WriteAllLines(path, lines);
        _configPath.Text = _isChinese ? $"当前: {path}  ← 已保存" : $"Loaded: {path}  ← saved";
        _configPath.ForeColor = Color.FromArgb(39, 174, 96);
        _loadedFile = path;
    }

    void WriteSection(List<string> lines, string title, ParamDef[] defs) { lines.Add($"# {title}"); foreach (var p in defs) lines.Add($"{p.Key} = {_values[p.Key]}"); lines.Add(""); }
    void WritePos(List<string> lines, string title, string suffix) { lines.Add($"# {title}"); for (int i = 0; i < 5; i++) lines.Add($"pos_{PosNames[i].ToLower()}_{suffix} = {_values[$"pos_{PosNames[i].ToLower()}_{suffix}"]}"); lines.Add(""); }

    void ApplyAggressive() { ResetDefaultsToMemory(); Set("recall_threshold_min",10); Set("recall_threshold_max",18); Set("overstay_min",25); Set("overstay_max",35); Set("danger_floor",6); Set("danger_ring_window",40); Set("threshold_reduce",2); Set("overstay_increase",28); Set("safe_threshold_increase",2); for (int i=0;i<5;i++){Set($"pos_{PosNames[i].ToLower()}_recall",PosRecallDefaults[i]-8);Set($"pos_{PosNames[i].ToLower()}_overstay",PosOverstayDefaults[i]+8);Set($"pos_{PosNames[i].ToLower()}_danger",PosDangerDefaults[i]-4);} }
    void ApplyConservative() { ResetDefaultsToMemory(); Set("recall_threshold_min",35); Set("recall_threshold_max",45); Set("overstay_min",2); Set("overstay_max",6); Set("danger_floor",1); Set("danger_ring_window",96); Set("threshold_reduce",18); Set("overstay_increase",3); Set("safe_threshold_increase",12); for (int i=0;i<5;i++){Set($"pos_{PosNames[i].ToLower()}_recall",PosRecallDefaults[i]+8);Set($"pos_{PosNames[i].ToLower()}_overstay",PosOverstayDefaults[i]-8);Set($"pos_{PosNames[i].ToLower()}_danger",PosDangerDefaults[i]+5);} }
    void ApplyNoComeback() { Set("threshold_reduce",0); Set("overstay_increase",0); Set("safe_threshold_increase",0); }
    void ApplyVanilla() { Set("recall_threshold_min",50); Set("recall_threshold_max",50); Set("overstay_min",0); Set("overstay_max",0); }

    void ResetDefaults() { ResetDefaultsToMemory(); foreach (var (k,v) in _values) { if (_sliders.TryGetValue(k, out var t)) t.Value=v; if (_inputs.TryGetValue(k, out var n)) n.Value=v; } }
    void ResetDefaultsToMemory() { _values["recall_threshold_min"]=22;_values["recall_threshold_max"]=33;_values["overstay_min"]=10;_values["overstay_max"]=18;_values["danger_hp"]=15;_values["danger_floor"]=3;_values["danger_ring_window"]=64;_values["threshold_reduce"]=8;_values["overstay_increase"]=15;_values["safe_threshold_increase"]=5;_values["absolute_hp_panic"]=150;for(int i=0;i<5;i++){_values[$"pos_{PosNames[i].ToLower()}_recall"]=PosRecallDefaults[i];_values[$"pos_{PosNames[i].ToLower()}_overstay"]=PosOverstayDefaults[i];_values[$"pos_{PosNames[i].ToLower()}_danger"]=PosDangerDefaults[i];} }
    void Set(string key, int v) { if (_sliders.TryGetValue(key, out var s)) s.Value = v; if (_inputs.TryGetValue(key, out var n)) n.Value = v; _values[key] = v; }

    static string FindModDir() { var d = Path.GetDirectoryName(Application.ExecutablePath); if (d != null && Path.GetFileName(d) == "editor") d = Path.GetDirectoryName(d); return d ?? "."; }
}