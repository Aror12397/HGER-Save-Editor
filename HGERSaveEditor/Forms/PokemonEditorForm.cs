using HGERSaveEditor.Core;

namespace HGERSaveEditor.Forms;

/// <summary>
/// 포켓몬 편집 다이얼로그 (PKHeX Gen4 UI 참조).
/// 탭: 기본 / 기술 / 스탯(EV·IV) / 만남·OT
/// </summary>
public sealed class PokemonEditorForm : Form
{
    // ==================== 결과 ====================
    public PK4 Result { get; private set; }

    private readonly PK4 _pk;
    private readonly bool _isNewSlot;
    private readonly string? _defaultOTName;
    private readonly ushort _defaultTID;
    private readonly ushort _defaultSID;
    private readonly byte _defaultGender;

    // ==================== 컨트롤 ====================
    private TabControl _tabs = null!;
    private bool _loading;
    private bool _syncingExpLevel;

    // --- 기본 탭 ---
    private PictureBox _picSprite      = null!;
    private ComboBox       _cmbSpecies      = null!;
    private SearchComboBox _speciesSearch   = null!;
    private ComboBox _cmbForm        = null!;
    private Label    _lblFormTag     = null!;
    private List<int> _formValues    = new();
    private TextBox  _txtNickname    = null!;
    private CheckBox _chkNicknamed   = null!;
    private TextBox  _numLevel       = null!;
    private TextBox  _numExp         = null!;
    private ComboBox _cmbNature      = null!;
    private Label    _lblNatureMod   = null!;
    private ComboBox _cmbMintNature  = null!;
    private Label    _lblMintMod     = null!;
    private Label    _lblGender      = null!;
    private Label    _lblShiny       = null!;
    private ComboBox       _cmbAbility      = null!;
    private SearchComboBox _abilitySearch   = null!;
    private ComboBox       _cmbHeldItem       = null!;
    private SearchComboBox _heldItemSearch    = null!;
    private TextBox  _numFriendship  = null!;
    private ComboBox _cmbLanguage    = null!;
    private ComboBox _cmbBall        = null!;
    private TextBox  _txtPID         = null!;

    // --- 기술 탭 ---
    private MoveRow[] _moveRows = null!;

    // --- 스탯 탭 ---
    private StatRow[] _statRows      = null!;
    private Label     _lblEvTotal    = null!;
    private Label     _lblHiddenPower = null!;

    // --- 만남 탭 ---
    private TextBox     _txtOTName      = null!;
    private TextBox     _numTID         = null!;
    private TextBox     _numSID         = null!;
    private TextBox     _numMetLevel    = null!;
    private ComboBox       _cmbMetLocation    = null!;
    private SearchComboBox _metLocationSearch = null!;
    private TextBox     _txtMetDate     = null!;
    // ==================== 생성자 ====================

    public PokemonEditorForm(PK4 pk, string? defaultOTName = null, ushort defaultTID = 0, ushort defaultSID = 0, byte defaultGender = 0)
    {
        _isNewSlot = pk.IsEmpty;
        _defaultOTName = defaultOTName;
        _defaultTID = defaultTID;
        _defaultSID = defaultSID;
        _defaultGender = defaultGender;

        if (_isNewSlot)
        {
            // 새 포켓몬: 암복호화 왕복 없이 빈 데이터로 생성.
            int size = pk.IsPartyForm ? PokeCrypto.SIZE_4PARTY : PokeCrypto.SIZE_4STORED;
            _pk = PK4.CreateBlank(size);
        }
        else
        {
            _pk = pk.Clone();
        }
        Result = pk;

        InitializeComponent();
        LoadFromPK4();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text            = "포켓몬 편집";
        Size            = new Size(590, 630);
        MinimumSize     = new Size(550, 580);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition   = FormStartPosition.CenterParent;
        Font            = new Font("Segoe UI", 9f);
        BackColor       = Color.FromArgb(30, 30, 30);
        ForeColor       = Color.White;

        _tabs = new TabControl { Dock = DockStyle.Fill };
        _tabs.TabPages.Add(BuildMainTab());
        _tabs.TabPages.Add(BuildMovesTab());
        _tabs.TabPages.Add(BuildStatsTab());
        _tabs.TabPages.Add(BuildMetTab());

        var btnPanel = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height        = 40,
            Padding       = new Padding(4),
            BackColor     = Color.FromArgb(45, 45, 48),
        };

        var btnOK     = MakeButton("확인",  DialogResult.OK);
        var btnCancel = MakeButton("취소",  DialogResult.Cancel);
        btnOK.Click   += OnOK;

        btnPanel.Controls.AddRange([btnOK, btnCancel]);
        Controls.Add(_tabs);
        Controls.Add(btnPanel);
        AcceptButton = btnOK;
        CancelButton = btnCancel;

        ResumeLayout(false);
        PerformLayout();
    }

    private static Button MakeButton(string text, DialogResult dr) => new()
    {
        Text         = text,
        DialogResult = dr,
        Width        = 70,
        Height       = 28,
        FlatStyle    = FlatStyle.Flat,
        BackColor    = dr == DialogResult.OK ? Color.FromArgb(0, 122, 204) : Color.FromArgb(60, 60, 60),
        ForeColor    = Color.White,
    };

    // ==================== 탭 1: 기본 정보 ====================

    private void UpdateSpriteDisplay(int species, int form = 0)
    {
        var sprite = GameData.GetSprite(species, form);
        _picSprite.Image = sprite;
        _picSprite.Visible = sprite != null;
    }

    private TabPage BuildMainTab()
    {
        var page  = new TabPage("기본") { BackColor = Color.FromArgb(40, 40, 40) };

        // 스프라이트 패널 (오른쪽 상단 고정)
        _picSprite = new PictureBox
        {
            Size      = new Size(80, 80),
            SizeMode  = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(50, 50, 55),
            Visible   = false,
            Anchor    = AnchorStyles.Top | AnchorStyles.Right,
        };
        page.Controls.Add(_picSprite);
        page.Layout += (_, _) =>
        {
            _picSprite.Location = new Point(page.ClientSize.Width - _picSprite.Width - 8, 8);
        };

        var table = MakeTable2Col(120);

        // PID (hex) — 읽기 전용 표시
        _txtPID = new TextBox { Width = 120, MaxLength = 8, CharacterCasing = CharacterCasing.Upper, Enabled = false };

        _lblShiny = new Label
        {
            Text      = "☆",
            Dock      = DockStyle.Left,
            Width     = 22,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.FromArgb(120, 120, 120),
            Font      = new Font("Segoe UI", 8f),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding   = Padding.Empty,
        };

        // 레이블 패널: 별 라벨(왼쪽) + "PID (hex)" 텍스트(오른쪽)
        var pidLabelPanel = new Panel { Dock = DockStyle.Fill };
        var pidTextLabel = new Label
        {
            Text      = "PID (hex)",
            Dock      = DockStyle.Fill,
            ForeColor = Color.LightGray,
            TextAlign = ContentAlignment.MiddleRight,
        };
        pidLabelPanel.Controls.Add(pidTextLabel);  // Fill — 먼저 추가
        pidLabelPanel.Controls.Add(_lblShiny);     // Left — 나중에 추가 (역순 처리로 왼쪽 배치)

        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.Controls.Add(pidLabelPanel);
        table.Controls.Add(_txtPID);

        // 종류 + 폼
        _cmbSpecies = new ComboBox { Width = 200, MaxDropDownItems = 12 };
        int maxSpecies = Math.Min(Math.Max(GameData.SpeciesNames.Length, 800), 1026);
        string[] speciesNames = new string[maxSpecies];
        for (int i = 0; i < maxSpecies; i++) speciesNames[i] = GameData.GetSpeciesName(i);
        _speciesSearch = new SearchComboBox(_cmbSpecies, speciesNames);
        _cmbForm = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Enabled = false, Visible = false };
        _lblFormTag = MakeInfoLabel("폼", Color.DarkGray, 24);
        _lblFormTag.Visible = false;
        _speciesSearch.SelectionChanged += sp =>
        {
            UpdateFormDropdown(sp);
            UpdateSpriteDisplay(sp, SelectedForm);
            if (!_loading)
            {
                RecalcStatValues();
                if (!_chkNicknamed.Checked)
                    _txtNickname.Text = GameData.GetSpeciesName(sp);
            }
        };
        _cmbForm.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading)
            {
                UpdateSpriteDisplay(_speciesSearch.SelectedId, SelectedForm);
                RecalcStatValues();
            }
        };
        AddRow(table, "종류", MakeFlow(_cmbSpecies, _lblFormTag, _cmbForm));

        // 닉네임 + IsNicknamed 체크박스
        _txtNickname  = new TextBox { Width = 130, MaxLength = 10 };
        _chkNicknamed = new CheckBox { Text = "닉네임 지정", ForeColor = Color.LightGray, AutoSize = true, Margin = new Padding(8, 3, 3, 3) };
        _chkNicknamed.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            _txtNickname.ReadOnly = !_chkNicknamed.Checked;
            if (!_chkNicknamed.Checked)
                _txtNickname.Text = GameData.GetSpeciesName(_speciesSearch.SelectedId);
        };
        AddRow(table, "닉네임", MakeFlow(_txtNickname, _chkNicknamed));

        // 레벨 / 경험치
        _numLevel = MakeDigitBox(55, 3);
        _numExp   = MakeDigitBox(110, 9);
        _numLevel.TextChanged += OnLevelChanged;
        _numExp.TextChanged   += OnExpChanged;
        AddRow(table, "레벨 / EXP", MakeFlow(_numLevel, MakeInfoLabel("EXP", Color.DarkGray, 30), _numExp));

        // 성격 (PID 기반, 읽기 전용)
        _cmbNature    = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, Enabled = false };
        _cmbNature.Items.AddRange(GameData.NatureNames);
        _lblNatureMod = MakeInfoLabel("", Color.Plum, 150);
        _cmbNature.SelectedIndexChanged += (_, _) => { UpdateNatureModifierLabel(); UpdateStatColors(); RecalcStatValues(); };
        AddRow(table, "성격", MakeFlow(_cmbNature, _lblNatureMod));

        // 민트 성격 (스탯 보정 오버라이드)
        _cmbMintNature = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        _cmbMintNature.Items.Add("없음 (PID 성격)");
        _cmbMintNature.Items.AddRange(GameData.NatureNames);
        _lblMintMod    = MakeInfoLabel("", Color.FromArgb(130, 220, 130), 150);
        _cmbMintNature.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            UpdateNatureModifierLabel();
            UpdateStatColors();
            RecalcStatValues();
        };
        AddRow(table, "민트", MakeFlow(_cmbMintNature, _lblMintMod));

        // 성별 표시 (PID에서 자동 결정, 읽기 전용)
        _lblGender = new Label
        {
            Width     = 80,
            Height    = 24,
            BackColor = Color.FromArgb(50, 50, 55),
            ForeColor = Color.LightCyan,
            Font      = new Font(Font.FontFamily, 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin    = new Padding(3, 3, 3, 3),
        };
        AddRow(table, "성별", _lblGender);

        // 특성
        _cmbAbility = new ComboBox { Width = 200, MaxDropDownItems = 12 };
        int maxAbility = Math.Max(GameData.AbilityNames.Length, 300);
        string[] abilityNames = new string[maxAbility];
        for (int i = 0; i < maxAbility; i++) abilityNames[i] = GameData.GetAbilityName(i);
        _abilitySearch = new SearchComboBox(_cmbAbility, abilityNames);
        AddRow(table, "특성", _cmbAbility);

        // 지닌 아이템
        _cmbHeldItem = new ComboBox { Width = 200, MaxDropDownItems = 12 };
        string[] heldItemNames = new string[800];
        for (int i = 0; i < 800; i++) heldItemNames[i] = GameData.GetItemName(i);
        _heldItemSearch = new SearchComboBox(_cmbHeldItem, heldItemNames);
        AddRow(table, "지닌 아이템", _cmbHeldItem);

        // 친밀도
        _numFriendship = MakeDigitBox(70, 3);
        AddRow(table, "친밀도", _numFriendship);

        // 언어
        _cmbLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        foreach (string lang in new[] { "JPN (1)", "ENG (2)", "FRE (3)", "ITA (4)", "GER (5)", "SPA (7)", "KOR (8)" })
            _cmbLanguage.Items.Add(lang);
        AddRow(table, "언어", _cmbLanguage);

        // 볼
        _cmbBall = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        for (int i = 0; i < 256; i++) _cmbBall.Items.Add(GameData.GetItemName(i));
        AddRow(table, "볼", _cmbBall);

        // 남은 공간 흡수용 filler 행
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var fillerMain = new Label();
        table.Controls.Add(fillerMain);
        table.SetColumnSpan(fillerMain, table.ColumnCount);

        page.Controls.Add(table);
        return page;
    }

    // ==================== 탭 2: 기술 ====================

    private TabPage BuildMovesTab()
    {
        var page  = new TabPage("기술") { BackColor = Color.FromArgb(40, 40, 40) };
        var table = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 4,
            Padding     = new Padding(10),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  60));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55));

        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        foreach (string h in new[] { "슬롯", "기술", "PP", "PP업" })
            table.Controls.Add(new Label { Text = h, AutoSize = false, ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });

        _moveRows = new MoveRow[4];
        for (int i = 0; i < 4; i++)
        {
            _moveRows[i] = new MoveRow(i + 1);
            _moveRows[i].AddToTable(table);
        }

        // 남은 공간 흡수용 filler 행
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var fillerMoves = new Label();
        table.Controls.Add(fillerMoves);
        table.SetColumnSpan(fillerMoves, table.ColumnCount);

        page.Controls.Add(table);
        return page;
    }

    // ==================== 탭 3: 스탯 ====================

    private TabPage BuildStatsTab()
    {
        var page  = new TabPage("스탯 / EV·IV") { BackColor = Color.FromArgb(40, 40, 40) };
        var outer = new Panel { Dock = DockStyle.Fill };

        var table = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 6,
            Padding     = new Padding(10, 10, 10, 0),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));  // 스탯명
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));  // 기본
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));  // IV
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));  // EV
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));  // 계산값
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100)); // 여백

        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        foreach (string h in new[] { "스탯", "기본", "IV", "EV", "계산", "" })
            table.Controls.Add(new Label { Text = h, AutoSize = false, ForeColor = Color.LightGray, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });

        string[] statNames = ["HP", "공격", "방어", "특공", "특방", "스피드"];
        _statRows = new StatRow[6];
        for (int i = 0; i < 6; i++)
        {
            _statRows[i] = new StatRow(statNames[i]);
            _statRows[i].AddToTable(table);
            int idx = i;
            _statRows[i].OnChanged += () => { RecalcStats(idx); UpdateEvTotal(); RecalcStatValues(); UpdateHiddenPower(); };
        }

        // 남은 공간 흡수용 filler 행
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var fillerStats = new Label();
        table.Controls.Add(fillerStats);
        table.SetColumnSpan(fillerStats, table.ColumnCount);

        // 하단: 잠재파워 + EV 합계 + 빠른 버튼
        _lblHiddenPower = new Label
        {
            Text      = "잠재파워: -",
            AutoSize  = true,
            ForeColor = Color.LightGray,
            Margin    = new Padding(0, 4, 14, 0),
        };

        var bottomPanel = new FlowLayoutPanel
        {
            Dock         = DockStyle.Bottom,
            Height       = 38,
            Padding      = new Padding(10, 4, 4, 4),
            BackColor    = Color.FromArgb(40, 40, 40),
            WrapContents = false,
        };

        _lblEvTotal = new Label
        {
            Text      = "EV: 0 / 510",
            AutoSize  = true,
            ForeColor = Color.LightGray,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin    = new Padding(0, 4, 14, 0),
        };

        var btnMaxIV = MakeSmallButton("IV 최대");
        btnMaxIV.Click += (_, _) => { foreach (var r in _statRows) r.SetIV(31); RecalcStatValues(); UpdateHiddenPower(); };

        var btnClearIV = MakeSmallButton("IV 초기화");
        btnClearIV.Width = 72;
        btnClearIV.Click += (_, _) => { foreach (var r in _statRows) r.SetIV(0); RecalcStatValues(); UpdateHiddenPower(); };

        var btnClearEV = MakeSmallButton("EV 초기화");
        btnClearEV.Width = 72;
        btnClearEV.Click += (_, _) => { foreach (var r in _statRows) r.SetEV(0); UpdateEvTotal(); RecalcStatValues(); };

        bottomPanel.Controls.AddRange([_lblHiddenPower, _lblEvTotal, btnMaxIV, btnClearIV, btnClearEV]);
        outer.Controls.Add(table);
        outer.Controls.Add(bottomPanel);
        page.Controls.Add(outer);
        return page;
    }

    private static Button MakeSmallButton(string text) => new()
    {
        Text      = text,
        Width     = 62,
        Height    = 24,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(60, 60, 60),
        ForeColor = Color.White,
        Margin    = new Padding(3, 0, 3, 0),
    };

    // ==================== 탭 4: 만남 / OT ====================

    private TabPage BuildMetTab()
    {
        var page  = new TabPage("만남 / OT") { BackColor = Color.FromArgb(40, 40, 40) };
        var table = MakeTable2Col(120);

        // OT 이름
        _txtOTName = new TextBox { Width = 110, MaxLength = 7 };
        AddRow(table, "어버이 이름", _txtOTName);

        // TID / SID
        _numTID = MakeDigitBox(75, 5);
        _numSID = MakeDigitBox(75, 5);
        _numTID.TextChanged += (_, _) => { if (!_loading) UpdateShinyLabel(); };
        _numSID.TextChanged += (_, _) => { if (!_loading) UpdateShinyLabel(); };
        AddRow(table, "TID / SID", MakeFlow(_numTID, MakeInfoLabel("SID", Color.DarkGray, 30), _numSID));

        // 만난 레벨
        _numMetLevel = MakeDigitBox(65, 3);
        AddRow(table, "만난 레벨", _numMetLevel);

        // 만난 장소
        _cmbMetLocation = new ComboBox { Width = 240, MaxDropDownItems = 12 };
        int maxLocId = GameData.LocationEntries.Count > 0 ? GameData.LocationEntries.Keys.Max() : 0;
        maxLocId = Math.Max(maxLocId, 300);
        string[] locNames = new string[maxLocId + 1];
        for (int i = 0; i <= maxLocId; i++) locNames[i] = GameData.GetLocationName(i);
        _metLocationSearch = new SearchComboBox(_cmbMetLocation, locNames);
        AddRow(table, "만난 장소", _cmbMetLocation);

        // 만난 날짜 — 달력 버튼을 TextBox 내부 오른쪽에 배치 (PKHeX 스타일)
        _txtMetDate = new TextBox { Width = 130, MaxLength = 10 };
        var btnCalendar = new Button
        {
            Text      = "\U0001f4c5",
            Width     = 22,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            TabStop   = false,
        };
        btnCalendar.FlatAppearance.BorderSize = 0;
        // TextBox 내부에 버튼 삽입 — TextBox 배경색과 동일하게 맞춤
        _txtMetDate.Controls.Add(btnCalendar);
        _txtMetDate.Layout += (_, _) =>
        {
            btnCalendar.Height = _txtMetDate.ClientSize.Height;
            btnCalendar.Location = new Point(_txtMetDate.ClientSize.Width - btnCalendar.Width, 0);
            btnCalendar.BackColor = _txtMetDate.BackColor;
            btnCalendar.ForeColor = _txtMetDate.ForeColor;
        };
        btnCalendar.Click += (_, _) =>
        {
            using var dlg = new Form
            {
                Text            = "만난 날짜 선택",
                Size            = new Size(260, 260),
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                StartPosition   = FormStartPosition.CenterParent,
                BackColor       = Color.FromArgb(40, 40, 40),
            };
            var cal = new MonthCalendar { MaxSelectionCount = 1 };
            if (DateTime.TryParse(_txtMetDate.Text, out var cur))
                cal.SelectionStart = cur;
            cal.DateSelected += (_, args) =>
            {
                _txtMetDate.Text = args.Start.ToString("yyyy-MM-dd");
                dlg.Close();
            };
            dlg.Controls.Add(cal);
            dlg.ShowDialog(this);
        };
        AddRow(table, "만난 날짜", _txtMetDate);

        // 남은 공간 흡수용 filler 행
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var fillerMet = new Label();
        table.Controls.Add(fillerMet);
        table.SetColumnSpan(fillerMet, table.ColumnCount);

        page.Controls.Add(table);
        return page;
    }

    // ==================== 데이터 로드 ====================

    private void LoadFromPK4()
    {
        _loading = true;

        // 만남 탭 먼저 (TID/SID가 빛나기 판정에 필요)
        _txtOTName.Text       = _pk.OTName;
        _numTID.Text          = _pk.TID.ToString();
        _numSID.Text          = _pk.SID.ToString();
        _numMetLevel.Text     = _pk.MetLevel.ToString();
        _metLocationSearch.SetSelected(_pk.MetLocation);
        var met = _pk.MetDate;
        _txtMetDate.Text = met.Year > 0 ? $"20{met.Year:D2}-{met.Month:D2}-{met.Day:D2}" : "";
        // 기본 탭
        _speciesSearch.SetSelected(_pk.Species);
        UpdateFormDropdown(_pk.Species, _pk.Form);
        UpdateSpriteDisplay(_pk.Species, _pk.Form);
        _chkNicknamed.Checked = _pk.IsNicknamed;
        _txtNickname.ReadOnly = !_pk.IsNicknamed;
        _txtNickname.Text     = _pk.IsNicknamed ? _pk.Nickname : GameData.GetSpeciesName(_pk.Species);
        _numLevel.Text        = Math.Clamp(_pk.Level, 1, 100).ToString();
        _numExp.Text          = _pk.Exp.ToString();
        _cmbNature.SelectedIndex = Math.Clamp(_pk.Nature, 0, 24);
        // 민트: stored = (nature_index + 1) * 2, 0=없음. ComboBox index = stored / 2.
        int mintIdx = (_pk.StatNature >= 2 && _pk.StatNature % 2 == 0) ? _pk.StatNature / 2 : 0;
        _cmbMintNature.SelectedIndex = Math.Clamp(mintIdx, 0, 25);
        _abilitySearch.SetSelected(_pk.Ability);
        _heldItemSearch.SetSelected(Math.Clamp((int)_pk.HeldItem, 0, 799));
        _numFriendship.Text   = _pk.Friendship.ToString();
        _txtPID.Text          = _pk.PID.ToString("X8");

        int[] langIds = [1, 2, 3, 4, 5, 7, 8];
        int langIdx = Array.IndexOf(langIds, _pk.Language);
        _cmbLanguage.SelectedIndex = langIdx >= 0 ? langIdx : 1;

        int ballId = Math.Clamp(_pk.Ball, 0, _cmbBall.Items.Count - 1);
        _cmbBall.SelectedIndex = ballId;

        // 기술 탭
        ushort[] moves = [_pk.Move1, _pk.Move2, _pk.Move3, _pk.Move4];
        byte[]   pps   = [_pk.PP1,   _pk.PP2,   _pk.PP3,   _pk.PP4];
        byte[]   ppups = [_pk.PPUp1, _pk.PPUp2, _pk.PPUp3, _pk.PPUp4];
        for (int i = 0; i < 4; i++)
            _moveRows[i].Load(moves[i], pps[i], ppups[i]);

        // 스탯 탭
        int[] ivs   = [_pk.IV_HP, _pk.IV_Atk, _pk.IV_Def, _pk.IV_SpA, _pk.IV_SpD, _pk.IV_Spe];
        int[] evs   = [_pk.EV_HP, _pk.EV_Atk, _pk.EV_Def, _pk.EV_SpA, _pk.EV_SpD, _pk.EV_Spe];
        var   bs    = GameData.GetBaseStats(_pk.Species, _pk.Form);
        int[] bases = bs != null ? [bs.HP, bs.Atk, bs.Def, bs.SpA, bs.SpD, bs.Spe] : [0, 0, 0, 0, 0, 0];
        for (int i = 0; i < 6; i++)
            _statRows[i].Load(bases[i], ivs[i], evs[i]);

        _loading = false;

        // 빈 슬롯(새 포켓몬)이면 기본값 세팅
        if (_isNewSlot)
        {
            _cmbBall.SelectedIndex = 4; // 몬스터볼
            if (_defaultOTName != null)
                _txtOTName.Text = _defaultOTName;
            _numTID.Text = _defaultTID.ToString();
            _numSID.Text = _defaultSID.ToString();
            var now = DateTime.Now;
            _txtMetDate.Text = now.ToString("yyyy-MM-dd");
        }

        // 로드 완료 후 UI 갱신
        UpdateNatureModifierLabel();
        UpdateStatColors();
        RecalcStatValues();
        UpdateEvTotal();
        UpdateHiddenPower();
        UpdateGenderLabel();
        UpdateShinyLabel();
    }

    // ==================== 데이터 저장 ====================

    private void SaveToPK4()
    {
        // 기본 탭
        _pk.Species    = (ushort)_speciesSearch.SelectedId;
        _pk.Form       = (byte)SelectedForm;
        _pk.Nickname   = _txtNickname.Text;
        _pk.IsNicknamed = _chkNicknamed.Checked;
        _pk.Exp        = (uint)GetInt(_numExp);
        _pk.Ability    = (byte)_abilitySearch.SelectedId;
        _pk.HeldItem   = (ushort)_heldItemSearch.SelectedId;
        _pk.Friendship = (byte)GetInt(_numFriendship);

        int[] langIds = [1, 2, 3, 4, 5, 7, 8];
        int langSel = _cmbLanguage.SelectedIndex;
        _pk.Language = (byte)(langSel >= 0 && langSel < langIds.Length ? langIds[langSel] : 2);

        _pk.Ball = (byte)Math.Clamp(_cmbBall.SelectedIndex, 0, 255);

        // 성격(PID%25)·성별(PID&0xFF)은 PID에서 자동 결정되므로 별도 조정 없음.
        uint pid = uint.TryParse(_txtPID.Text, System.Globalization.NumberStyles.HexNumber, null, out uint pidParsed) ? pidParsed : 0;
        _pk.PID = pid;

        // 민트 성격: ComboBox index → stored = index * 2 (0=없음, 2=Hardy, 4=Lonely, ...)
        _pk.StatNature = (byte)(_cmbMintNature.SelectedIndex * 2);

        // 기술 탭
        _pk.Move1 = (ushort)_moveRows[0].MoveID; _pk.PP1 = (byte)_moveRows[0].PP; _pk.PPUp1 = (byte)_moveRows[0].PPUp;
        _pk.Move2 = (ushort)_moveRows[1].MoveID; _pk.PP2 = (byte)_moveRows[1].PP; _pk.PPUp2 = (byte)_moveRows[1].PPUp;
        _pk.Move3 = (ushort)_moveRows[2].MoveID; _pk.PP3 = (byte)_moveRows[2].PP; _pk.PPUp3 = (byte)_moveRows[2].PPUp;
        _pk.Move4 = (ushort)_moveRows[3].MoveID; _pk.PP4 = (byte)_moveRows[3].PP; _pk.PPUp4 = (byte)_moveRows[3].PPUp;

        // 스탯 탭 IV/EV (순서: HP, Atk, Def, SpA, SpD, Spe)
        _pk.IV_HP  = _statRows[0].IV; _pk.EV_HP  = (byte)_statRows[0].EV;
        _pk.IV_Atk = _statRows[1].IV; _pk.EV_Atk = (byte)_statRows[1].EV;
        _pk.IV_Def = _statRows[2].IV; _pk.EV_Def = (byte)_statRows[2].EV;
        _pk.IV_SpA = _statRows[3].IV; _pk.EV_SpA = (byte)_statRows[3].EV;
        _pk.IV_SpD = _statRows[4].IV; _pk.EV_SpD = (byte)_statRows[4].EV;
        _pk.IV_Spe = _statRows[5].IV; _pk.EV_Spe = (byte)_statRows[5].EV;

        // 만남 탭
        _pk.OTName      = _txtOTName.Text;
        _pk.TID         = (ushort)GetInt(_numTID);
        _pk.SID         = (ushort)GetInt(_numSID);
        _pk.MetLevel    = GetInt(_numMetLevel);
        _pk.MetLocation = (ushort)_metLocationSearch.SelectedId;
        if (DateTime.TryParse(_txtMetDate.Text, out var metDate))
            _pk.MetDate = ((byte)(metDate.Year - 2000), (byte)metDate.Month, (byte)metDate.Day);

        // 새 포켓몬: OT 성별 + 출신 게임 설정 (게임이 자기 포켓몬으로 인식하도록)
        if (_isNewSlot)
        {
            _pk.OTIsFemale = _defaultGender == 1;
            _pk.OriginGame = 7; // HG
        }

        // 파티 포켓몬이면 배틀 스탯 갱신
        _pk.RefreshStats();
    }

    // ==================== 이벤트 ====================

    private void OnOK(object? sender, EventArgs e)
    {
        SaveToPK4();

        var warnings = _pk.GetValidationWarnings();
        if (warnings.Count > 0)
        {
            string reasons = string.Join("\n", warnings.ConvertAll(w => "• " + w));
            string msg = $"이 포켓몬에 다음 문제가 있습니다:\n\n{reasons}\n\n이대로 저장하시겠습니까?";
            if (MessageBox.Show(msg, "포켓몬 경고", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                DialogResult = DialogResult.None;
                return;
            }
        }

        Result       = _pk;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void OnLevelChanged(object? sender, EventArgs e)
    {
        if (_loading || _syncingExpLevel) return;
        _syncingExpLevel = true;
        _numExp.Text = GameData.CalcExpForLevel(GetInt(_numLevel), _speciesSearch.SelectedId).ToString();
        _syncingExpLevel = false;
        RecalcStatValues();
    }

    private void OnExpChanged(object? sender, EventArgs e)
    {
        if (_loading || _syncingExpLevel) return;
        if (!uint.TryParse(_numExp.Text, out uint exp)) return;
        int lv = GameData.CalcLevel(exp, GameData.GetGrowthRate(_speciesSearch.SelectedId));
        _syncingExpLevel = true;
        _numLevel.Text = lv.ToString();
        _syncingExpLevel = false;
        RecalcStatValues();
    }

    // ==================== 스탯 연산 ====================

    private void RecalcStats(int changedIndex)
    {
        int total = _statRows.Sum(r => r.EV);
        if (total > 510)
            _statRows[changedIndex].SetEV(Math.Max(0, _statRows[changedIndex].EV - (total - 510)));
    }

    private void UpdateEvTotal()
    {
        int total = _statRows.Sum(r => r.EV);
        _lblEvTotal.Text      = $"EV: {total} / 510";
        _lblEvTotal.ForeColor = total > 510 ? Color.OrangeRed : total == 510 ? Color.LightGreen : Color.LightGray;
    }

    // 잠재파워 타입 인덱스 → TypeNames 인덱스 매핑 (노말·???·페어리 제외한 16타입)
    private static readonly int[] HiddenPowerTypeMap =
        [6, 9, 7, 8, 12, 11, 13, 16, 1, 2, 4, 3, 10, 5, 14, 15];

    private void UpdateHiddenPower()
    {
        // stat row 순서: HP(0), Atk(1), Def(2), SpA(3), SpD(4), Spe(5)
        int hp  = _statRows[0].IV & 1;
        int atk = _statRows[1].IV & 1;
        int def = _statRows[2].IV & 1;
        int spa = _statRows[3].IV & 1;
        int spd = _statRows[4].IV & 1;
        int spe = _statRows[5].IV & 1;
        int idx = (hp + 2 * atk + 4 * def + 8 * spe + 16 * spa + 32 * spd) * 15 / 63;
        int typeIdx = HiddenPowerTypeMap[Math.Clamp(idx, 0, 15)];
        string typeName = typeIdx < GameData.TypeNames.Length ? GameData.TypeNames[typeIdx] : "???";
        _lblHiddenPower.Text = $"잠재파워: {typeName}";
    }

    private void RecalcStatValues()
    {
        var bs  = GameData.GetBaseStats(_speciesSearch.SelectedId, SelectedForm);
        int lv  = GetInt(_numLevel);
        int nat = GetEffectiveNature();
        if (bs == null || lv <= 0)
        {
            for (int i = 0; i < 6; i++) { _statRows[i].SetBase(0); _statRows[i].SetCalc(-1); }
            return;
        }

        int[] bases  = [bs.HP, bs.Atk, bs.Def, bs.SpA, bs.SpD, bs.Spe];
        for (int i = 0; i < 6; i++) _statRows[i].SetBase(bases[i]);
        // stat row 순서 → nature stat index (Gen4 기준: Atk=0, Def=1, Spe=2, SpA=3, SpD=4)
        int[] natMap = [-1, 0, 1, 3, 4, 2];
        for (int i = 0; i < 6; i++)
        {
            int    iv   = _statRows[i].IV;
            int    ev   = _statRows[i].EV;
            double mod  = (i == 0 || nat < 0) ? 1.0 : GameData.GetNatureModifier(nat, natMap[i]);
            int    calc = i == 0
                ? (2 * bases[i] + iv + ev / 4) * lv / 100 + lv + 10
                : (int)(((2 * bases[i] + iv + ev / 4) * lv / 100 + 5) * mod);
            _statRows[i].SetCalc(calc);
        }
    }

    // ==================== UI 갱신 ====================

    /// <summary>민트가 적용된 경우 민트 성격 인덱스(0~24), 아니면 PID 성격 인덱스를 반환.</summary>
    private int GetEffectiveNature()
    {
        int mintCombo = _cmbMintNature.SelectedIndex; // 0=없음, 1~25=성격
        return mintCombo > 0 ? mintCombo - 1 : _cmbNature.SelectedIndex;
    }

    private void UpdateNatureModifierLabel()
    {
        // PID 성격 레이블
        int nat = _cmbNature.SelectedIndex;
        if (nat < 0) { _lblNatureMod.Text = ""; return; }
        int b = nat / 5, r = nat % 5;
        _lblNatureMod.Text = b == r ? "(무보정)" : $"({StatLabels[b]}↑  {StatLabels[r]}↓)";

        // 민트 성격 레이블
        int mint = _cmbMintNature.SelectedIndex;
        if (mint <= 0) { _lblMintMod.Text = ""; return; }
        int mb = mint / 5, mr = mint % 5;
        _lblMintMod.Text = mb == mr ? "(무보정)" : $"({StatLabels[mb]}↑  {StatLabels[mr]}↓)";
    }

    private void UpdateStatColors()
    {
        // stat row 순서: HP(0), 공격(1), 방어(2), 특공(3), 특방(4), 스피드(5)
        // nature stat 순서 (Gen4 기준): Atk=0, Def=1, Spe=2, SpA=3, SpD=4
        int nat       = GetEffectiveNature();
        int[] natMap  = [-1, 0, 1, 3, 4, 2];
        for (int i = 0; i < 6; i++)
        {
            Color c = Color.White;
            if (nat >= 0 && natMap[i] >= 0)
            {
                int b = nat / 5, r = nat % 5;
                if (b != r)
                {
                    if (natMap[i] == b) c = Color.FromArgb(255, 140, 140); // 강화: 빨간색
                    if (natMap[i] == r) c = Color.FromArgb(140, 160, 255); // 약화: 파란색
                }
            }
            _statRows[i].SetNameColor(c);
        }
    }

    private void UpdateGenderLabel()
    {
        uint   pid       = uint.TryParse(_txtPID.Text, System.Globalization.NumberStyles.HexNumber, null, out uint pidParsed) ? pidParsed : 0;
        int    species   = _speciesSearch.SelectedId;
        int    threshold = GameData.GetGenderRatio(species);
        bool   fixedGender = threshold is 0 or 254 or 255;
        string text = threshold switch
        {
            255 => "무성",
            254 => "♀ 암컷",
            0   => "♂ 수컷",
            _   => (pid & 0xFF) < (uint)threshold ? "♀ 암컷" : "♂ 수컷",
        };
        _lblGender.Text      = text;
        _lblGender.ForeColor = text.StartsWith('♀') ? Color.LightPink
                             : text == "무성"        ? Color.LightGray
                             : Color.LightCyan;
    }

    private void UpdateShinyLabel()
    {
        uint   pid     = uint.TryParse(_txtPID.Text, System.Globalization.NumberStyles.HexNumber, null, out uint pidParsed) ? pidParsed : 0;
        ushort tid     = (ushort)GetInt(_numTID);
        ushort sid     = (ushort)GetInt(_numSID);
        bool   isShiny = (tid ^ sid ^ (pid >> 16) ^ (pid & 0xFFFF)) < 8;
        _lblShiny.BackColor = isShiny ? Color.FromArgb(120, 100, 10) : Color.FromArgb(60, 60, 60);
        _lblShiny.ForeColor = isShiny ? Color.Gold : Color.FromArgb(120, 120, 120);
        _lblShiny.Text      = isShiny ? "★" : "☆";
    }

    // ==================== 헬퍼 ====================

    private int SelectedForm =>
        _cmbForm.SelectedIndex >= 0 && _cmbForm.SelectedIndex < _formValues.Count
            ? _formValues[_cmbForm.SelectedIndex] : 0;

    private void UpdateFormDropdown(int species, int selectForm = 0)
    {
        var forms = GameData.GetAvailableForms(species);
        _formValues.Clear();
        _cmbForm.Items.Clear();
        foreach (int f in forms)
        {
            string name = GameData.GetFormName(species, f) ?? $"폼 {f}";
            _cmbForm.Items.Add(name);
            _formValues.Add(f);
        }
        bool hasMultiple = forms.Count > 1;
        _cmbForm.Enabled = hasMultiple;
        _cmbForm.Visible = hasMultiple;
        _lblFormTag.Visible = hasMultiple;
        int idx = _formValues.IndexOf(selectForm);
        _cmbForm.SelectedIndex = idx >= 0 ? idx : 0;
    }

    // Gen4 성격 인덱스: Atk=0, Def=1, Spe=2, SpA=3, SpD=4
    private static readonly string[] StatLabels = ["공격", "방어", "스피드", "특공", "특방"];

    private static int GetInt(TextBox tb) =>
        int.TryParse(tb.Text, out int v) ? v : 0;

    private static TextBox MakeDigitBox(int width, int maxLen)
    {
        var tb = new TextBox { Width = width, MaxLength = maxLen, TextAlign = HorizontalAlignment.Right };
        tb.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
        return tb;
    }

    private static TableLayoutPanel MakeTable2Col(int labelWidth)
    {
        var t = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            Padding     = new Padding(10),
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    private static Label MakeInfoLabel(string text, Color color, int width, bool bold = false)
    {
        var lbl = new Label
        {
            Text      = text,
            AutoSize  = false,
            Width     = width,
            Height    = 23,
            ForeColor = color,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin    = new Padding(4, 3, 3, 3),
        };
        if (bold) lbl.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        return lbl;
    }

    private static FlowLayoutPanel MakeFlow(params Control[] controls)
    {
        var f = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = Padding.Empty };
        f.Controls.AddRange(controls);
        return f;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control ctrl)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.Controls.Add(new Label
        {
            Text      = label,
            AutoSize  = false,
            ForeColor = Color.LightGray,
            TextAlign = ContentAlignment.MiddleRight,
            Dock      = DockStyle.Fill,
        });
        table.Controls.Add(ctrl);
    }
}

// ==================== 기술 행 ====================

internal sealed class MoveRow
{
    private readonly ComboBox    _cmbMove;
    private readonly SearchComboBox _moveSearch;
    public TextBox NumPP   = new() { Width = 55, MaxLength = 2, TextAlign = HorizontalAlignment.Right };
    public TextBox NumPPUp = new() { Width = 55, MaxLength = 1, TextAlign = HorizontalAlignment.Right };
    private readonly Label _lblSlot;

    public int MoveID => _moveSearch.SelectedId;
    public int PP     => int.TryParse(NumPP.Text,   out int v) ? v : 0;
    public int PPUp   => int.TryParse(NumPPUp.Text, out int v) ? v : 0;

    public MoveRow(int slotNum)
    {
        _lblSlot = new Label
        {
            Text      = $"기술 {slotNum}",
            AutoSize  = false,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock      = DockStyle.Fill,
        };

        _cmbMove = new ComboBox
        {
            BackColor        = Color.FromArgb(45, 45, 48),
            ForeColor        = Color.White,
            FlatStyle        = FlatStyle.Flat,
            MaxDropDownItems = 12,
            Dock             = DockStyle.Fill,
        };
        _moveSearch = new SearchComboBox(_cmbMove, GameData.MoveNames);

        foreach (var tb in new[] { NumPP, NumPPUp })
        {
            var t = tb;
            t.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
        }
    }

    public void Load(ushort moveId, byte pp, byte ppUp)
    {
        // 0xFFFF = hg-engine의 "기술 없음" 마커 → 0으로 표시
        ushort displayId = moveId == 0xFFFF ? (ushort)0 : moveId;
        _moveSearch.SetSelected(displayId);
        NumPP.Text   = pp.ToString();
        NumPPUp.Text = ppUp.ToString();
    }

    public void AddToTable(TableLayoutPanel table)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.Controls.Add(_lblSlot);
        table.Controls.Add(_cmbMove);
        table.Controls.Add(NumPP);
        table.Controls.Add(NumPPUp);
    }
}

// ==================== 스탯 행 ====================

internal sealed class StatRow
{
    public event Action? OnChanged;

    private readonly Label   _lblName;
    private readonly Label   _lblBase = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.LightGray, Dock = DockStyle.Fill };
    private readonly TextBox _numIV   = new() { Width = 55, MaxLength = 2, TextAlign = HorizontalAlignment.Right };
    private readonly TextBox _numEV   = new() { Width = 58, MaxLength = 3, TextAlign = HorizontalAlignment.Right };
    private readonly Label   _lblCalc = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.LightGreen, Dock = DockStyle.Fill };

    public int IV => int.TryParse(_numIV.Text, out int v) ? v : 0;
    public int EV => int.TryParse(_numEV.Text, out int v) ? v : 0;

    public StatRow(string name)
    {
        _lblName = new Label
        {
            Text      = name,
            AutoSize  = false,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock      = DockStyle.Fill,
        };

        foreach (var tb in new[] { _numIV, _numEV })
        {
            var t = tb;
            t.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
        }

        _numIV.TextChanged += (_, _) => OnChanged?.Invoke();
        _numEV.TextChanged += (_, _) => OnChanged?.Invoke();
    }

    public void Load(int baseStat, int iv, int ev)
    {
        _lblBase.Text = baseStat > 0 ? baseStat.ToString() : "-";
        _numIV.Text   = iv.ToString();
        _numEV.Text   = ev.ToString();
        _lblCalc.Text = "-";
    }

    public void SetBase(int v)        => _lblBase.Text = v > 0 ? v.ToString() : "-";
    public void SetIV(int v)          => _numIV.Text = Math.Clamp(v, 0, 31).ToString();
    public void SetEV(int v)          => _numEV.Text = Math.Clamp(v, 0, 252).ToString();
    public void SetCalc(int v)        => _lblCalc.Text = v >= 0 ? v.ToString() : "-";
    public void SetNameColor(Color c) => _lblName.ForeColor = c;

    public void AddToTable(TableLayoutPanel table)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.Controls.Add(_lblName);
        table.Controls.Add(_lblBase);
        table.Controls.Add(_numIV);
        table.Controls.Add(_numEV);
        table.Controls.Add(_lblCalc);
        table.Controls.Add(new Label()); // 6번째 열 여백
    }
}

// ==================== 검색 콤보박스 헬퍼 ====================

/// <summary>
/// ComboBox에 텍스트 검색(부분 일치) 기능을 추가한다.
/// DropDown 스타일로 전환 후 TextChanged 이벤트로 항목을 필터링하며,
/// 포커스를 잃으면 전체 목록을 복원하고 마지막 선택을 표시한다.
/// </summary>
internal sealed class SearchComboBox
{
    // IME P/Invoke — 조합 중 여부 감지 및 조합 취소에 사용
    [System.Runtime.InteropServices.DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("imm32.dll")]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);
    [System.Runtime.InteropServices.DllImport("imm32.dll")]
    private static extern int ImmGetCompositionString(IntPtr hIMC, uint dwIndex, IntPtr lpBuf, uint dwBufLen);
    [System.Runtime.InteropServices.DllImport("imm32.dll")]
    private static extern bool ImmNotifyIME(IntPtr hIMC, uint dwAction, uint dwIndex, uint dwValue);
    private const uint GCS_COMPSTR       = 0x0008;
    private const uint NI_COMPOSITIONSTR = 0x0015;
    private const uint CPS_CANCEL        = 0x0004;

    private bool IsImeComposing()
    {
        try
        {
            IntPtr hIMC = ImmGetContext(_cmb.Handle);
            if (hIMC == IntPtr.Zero) return false;
            try   { return ImmGetCompositionString(hIMC, GCS_COMPSTR, IntPtr.Zero, 0) > 0; }
            finally { ImmReleaseContext(_cmb.Handle, hIMC); }
        }
        catch { return false; }
    }

    // 항목 선택 시 IME 조합 중인 문자를 삽입하지 않고 취소한다.
    // (예: '전기' 검색 후 클릭 → IME가 '기'를 추가 삽입하여 '전기쇼크기'가 되는 버그 방지)
    private void CancelImeComposition()
    {
        try
        {
            IntPtr hIMC = ImmGetContext(_cmb.Handle);
            if (hIMC == IntPtr.Zero) return;
            try   { ImmNotifyIME(hIMC, NI_COMPOSITIONSTR, CPS_CANCEL, 0); }
            finally { ImmReleaseContext(_cmb.Handle, hIMC); }
        }
        catch { }
    }

    public event Action<int>? SelectionChanged;

    private readonly ComboBox  _cmb;
    private readonly string[]  _allItems;
    private readonly List<int> _filteredIds = new();
    private int  _selectedId;
    private bool _busy;
    private bool _committed;

    public int SelectedId => _selectedId;

    public SearchComboBox(ComboBox cmb, string[] allItems)
    {
        _cmb      = cmb;
        _allItems = allItems;

        cmb.DropDownStyle    = ComboBoxStyle.DropDown;
        cmb.AutoCompleteMode = AutoCompleteMode.None;

        RestoreAll();
        if (_cmb.Items.Count > 0)
            _cmb.SelectedIndex = 0;

        cmb.SelectionChangeCommitted += OnCommitted;
        cmb.TextChanged              += OnTextChanged;
        cmb.SelectedIndexChanged     += OnSelected;
        cmb.DropDownClosed           += OnDropDownClosed;
        cmb.Leave                    += OnLeave;
    }

    public void SetSelected(int id)
    {
        int newId = Math.Clamp(id, 0, Math.Max(0, _allItems.Length - 1));
        bool changed = _selectedId != newId;
        _selectedId = newId;
        _busy = true;
        RestoreAll();
        if (_selectedId < _cmb.Items.Count)
            _cmb.SelectedIndex = _selectedId;
        _busy = false;
        if (changed) SelectionChanged?.Invoke(_selectedId);
    }

    // SelectionChangeCommitted: 사용자가 드롭다운에서 항목을 클릭/Enter로 확정했을 때 발생.
    // TextChanged보다 먼저 발생하므로, _filteredIds가 변경되기 전에 _selectedId를 안전하게 갱신한다.
    private void OnCommitted(object? s, EventArgs e)
    {
        if (_busy) return;
        int idx = _cmb.SelectedIndex;
        if (idx >= 0 && idx < _filteredIds.Count)
        {
            int newId = _filteredIds[idx];
            _busy = true;
            CancelImeComposition();
            _busy = false;
            bool changed = _selectedId != newId;
            _selectedId = newId;
            _committed = true;
            if (changed) SelectionChanged?.Invoke(_selectedId);
        }
    }

    private void OnTextChanged(object? s, EventArgs e)
    {
        if (_busy) return;

        // 드롭다운 선택 직후: OnCommitted가 _selectedId를 이미 갱신했으므로
        // Items.Clear() 없이 전체 목록을 복원하고 선택을 표시한다.
        // Items.Clear()가 텍스트를 지워 공백으로 표시되는 버그를 방지.
        if (_committed)
        {
            _committed = false;
            _busy = true;
            RestoreAll();
            if (_selectedId < _cmb.Items.Count)
                _cmb.SelectedIndex = _selectedId;
            _busy = false;
            return;
        }

        // IME 조합 중(예: ㅁ → 마 조합 과정)에는 건너뜀.
        // Items.Clear() + Text 재설정이 조합 버퍼를 깨뜨려 'ㅁ마' 등으로 중복 입력되는 버그 방지.
        if (IsImeComposing()) return;
        _busy = true;

        string search = _cmb.Text;
        int    cursor = _cmb.SelectionStart;

        _cmb.BeginUpdate();
        _cmb.Items.Clear();
        _filteredIds.Clear();
        for (int i = 0; i < _allItems.Length; i++)
        {
            if (search.Length == 0 || _allItems[i].Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                _cmb.Items.Add(_allItems[i]);
                _filteredIds.Add(i);
            }
        }
        _cmb.EndUpdate();

        // CB_RESETCONTENT(Items.Clear)가 편집 컨트롤 텍스트를 지우는 경우 복원.
        // IsImeComposing() == false 일 때만 여기 도달하므로 Text 재설정이 IME 조합을 방해하지 않음.
        if (_cmb.Text != search)
            _cmb.Text = search;
        _cmb.SelectionStart  = Math.Min(cursor, _cmb.Text.Length);
        _cmb.SelectionLength = 0;

        _busy = false;
    }

    private void OnSelected(object? s, EventArgs e)
    {
        if (_busy) return;
        int idx = _cmb.SelectedIndex;
        if (idx >= 0 && idx < _filteredIds.Count)
        {
            int newId = _filteredIds[idx];
            _busy = true;
            CancelImeComposition(); // 조합 중인 문자가 선택 후 삽입되지 않도록 즉시 취소
            _busy = false;
            bool changed = _selectedId != newId;
            _selectedId = newId;
            if (changed) SelectionChanged?.Invoke(_selectedId);
        }
    }

    private void OnDropDownClosed(object? s, EventArgs e)
    {
        // 드롭다운이 닫히는 도중 RestoreAll/SelectedIndex 조작은 ComboBox 내부 상태와 충돌해
        // 선택이 무시되는 버그를 유발한다. IME 취소만 수행하고 목록 복원은 OnLeave에 위임한다.
        // _busy로 감싸서 CancelImeComposition이 TextChanged를 유발해도 _filteredIds가 변경되지 않게 한다.
        _busy = true;
        CancelImeComposition();
        _busy = false;
    }

    private void OnLeave(object? s, EventArgs e)
    {
        _busy = true;
        RestoreAll();
        if (_selectedId < _cmb.Items.Count)
            _cmb.SelectedIndex = _selectedId;
        else if (_cmb.Items.Count > 0)
            _cmb.SelectedIndex = 0;
        _busy = false;
    }

    private void RestoreAll()
    {
        _cmb.BeginUpdate();
        _cmb.Items.Clear();
        _filteredIds.Clear();
        for (int i = 0; i < _allItems.Length; i++)
        {
            _cmb.Items.Add(_allItems[i]);
            _filteredIds.Add(i);
        }
        _cmb.EndUpdate();
    }
}
