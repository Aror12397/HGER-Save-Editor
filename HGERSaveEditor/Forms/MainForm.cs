using HGERSaveEditor.Core;

namespace HGERSaveEditor.Forms;

/// <summary>
/// 메인 폼: 세이브 파일 로드 / 파티 / 박스 포켓몬 표시
/// </summary>
public sealed class MainForm : Form
{
    // ==================== 컨트롤 ====================
    private MenuStrip   _menuStrip   = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private TabControl  _tabControl  = null!;

    // 파티 탭
    private TabPage   _partyTab   = null!;
    private Panel     _partyPanel = null!;
    private SlotButton[] _partySlots = null!;

    // 박스 탭
    private TabPage         _boxTab       = null!;
    private ComboBox        _boxSelector  = null!;
    private TableLayoutPanel _boxGrid     = null!;
    private SlotButton[]    _boxSlots     = null!;
    private Label           _trainerLabel = null!;

    // ==================== 상태 ====================
    private SAV4HGSS? _save;
    private bool _updatingBox = false;
    private byte[]? _clipboardRaw;
    private string? _clipboardName;

    // ==================== 생성자 ====================

    public MainForm(string? initialFile = null)
    {
        InitializeComponent();
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        GameData.Initialize(dataDir);
        GameData.LoadBaseStats(Path.Combine(dataDir, "basestats.csv"));
        GameData.LoadGrowthRates(Path.Combine(dataDir, "growth_rates.csv"));
        GameData.InitializeSprites(Path.Combine(dataDir, "sprites"));
        GameData.LoadFormNames(Path.Combine(dataDir, "forms.txt"));

        if (initialFile != null)
            Load += (_, _) => LoadFile(initialFile);
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        Text            = "HGER Save Editor v0.4.1";
        Size            = new Size(1010, 620);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9f);
        BackColor       = Color.FromArgb(30, 30, 30);
        ForeColor       = Color.White;
        AllowDrop       = true;
        Icon            = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        DragEnter       += OnDragEnter;
        DragDrop        += OnDragDrop;

        BuildMenu();
        BuildStatusBar();
        BuildTabControl();

        MainMenuStrip = _menuStrip;
        Controls.Add(_tabControl);
        Controls.Add(_menuStrip);
        Controls.Add(_statusStrip);

        ResumeLayout(false);
        PerformLayout();
    }

    // ==================== 메뉴 ====================

    private void BuildMenu()
    {
        _menuStrip = new MenuStrip { BackColor = Color.FromArgb(45, 45, 48), ForeColor = Color.White };

        var fileMenu = new ToolStripMenuItem("파일(&F)") { ForeColor = Color.White };
        var openItem = new ToolStripMenuItem("열기(&O)...") { ShortcutKeys = Keys.Control | Keys.O };
        var saveItem = new ToolStripMenuItem("저장(&S)") { ShortcutKeys = Keys.Control | Keys.S };
        var saveAsItem = new ToolStripMenuItem("다른 이름으로 저장(&A)...");
        var exitItem  = new ToolStripMenuItem("종료(&X)");

        openItem.Click  += OnOpen;
        saveItem.Click  += OnSave;
        saveAsItem.Click += OnSaveAs;
        exitItem.Click  += (_, _) => Application.Exit();

        fileMenu.DropDownItems.AddRange([openItem, saveItem, saveAsItem, new ToolStripSeparator(), exitItem]);
        _menuStrip.Items.Add(fileMenu);

        var helpMenu = new ToolStripMenuItem("도움말(&H)") { ForeColor = Color.White };
        var aboutItem = new ToolStripMenuItem("정보(&A)");
        aboutItem.Click += (_, _) => MessageBox.Show(
            "HGER Save Editor\nhg-engine 기반 ROM 핵 세이브 편집기\n\n" +
            "지원 버전: HGER 0.9.7",
            "정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
        helpMenu.DropDownItems.Add(aboutItem);
        _menuStrip.Items.Add(helpMenu);
    }

    // ==================== 상태바 ====================

    private void BuildStatusBar()
    {
        _statusStrip = new StatusStrip { BackColor = Color.FromArgb(0, 122, 204) };
        _statusLabel = new ToolStripStatusLabel("세이브 파일을 열어주세요.")
        {
            ForeColor = Color.White,
            AutoSize  = true
        };
        _statusStrip.Items.Add(_statusLabel);
    }

    // ==================== 탭 컨트롤 ====================

    private void BuildTabControl()
    {
        _tabControl = new TabControl
        {
            Dock      = DockStyle.Fill,
            Font      = new Font("Segoe UI", 9.5f),
            Padding   = new Point(12, 4),
        };

        BuildPartyTab();
        BuildBoxTab();

        _tabControl.TabPages.Add(_partyTab);
        _tabControl.TabPages.Add(_boxTab);
    }

    // ==================== 파티 탭 ====================

    private void BuildPartyTab()
    {
        _partyTab = new TabPage("파티") { BackColor = Color.FromArgb(40, 40, 40) };

        _trainerLabel = new Label
        {
            Text      = "트레이너: -",
            Dock      = DockStyle.Top,
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.LightGray,
            Padding   = new Padding(8, 6, 8, 6),
            AutoSize  = true,
        };

        _partyPanel = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            Padding       = new Padding(10, 10, 10, 10),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            AutoScroll    = true,
        };

        _partySlots = new SlotButton[6];
        for (int i = 0; i < 6; i++)
        {
            int idx = i;
            _partySlots[i] = new SlotButton { Tag = i };
            _partySlots[i].Click += (_, _) => OnSlotClick(SlotSource.Party, idx, 0);
            _partySlots[i].MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) ShowSlotMenu(SlotSource.Party, idx, 0); };
            _partyPanel.Controls.Add(_partySlots[i]);
        }

        _partyTab.Controls.Add(_partyPanel);
        _partyTab.Controls.Add(_trainerLabel);
    }

    // ==================== 박스 탭 ====================

    private void BuildBoxTab()
    {
        _boxTab = new TabPage("박스") { BackColor = Color.FromArgb(40, 40, 40) };

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(50, 50, 50) };

        var boxLabel = new Label
        {
            Text     = "박스:",
            ForeColor = Color.White,
            Location = new Point(10, 12),
            AutoSize = true,
        };

        _boxSelector = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(55, 8),
            Width    = 160,
        };
        for (int i = 0; i < SAV4HGSS.BoxCount; i++)
            _boxSelector.Items.Add($"박스 {i + 1}");
        _boxSelector.SelectedIndex = 0;
        _boxSelector.SelectedIndexChanged += (_, _) => RefreshBoxGrid();

        topPanel.Controls.Add(boxLabel);
        topPanel.Controls.Add(_boxSelector);

        const int cols = 6;
        int rows = SAV4HGSS.BoxSlotCount / cols;

        _boxGrid = new TableLayoutPanel
        {
            ColumnCount  = cols,
            RowCount     = rows,
            AutoSize     = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location     = new Point(10, 10),
            BackColor    = Color.FromArgb(40, 40, 40),
        };
        for (int c = 0; c < cols; c++)
            _boxGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int r = 0; r < rows; r++)
            _boxGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _boxSlots = new SlotButton[SAV4HGSS.BoxSlotCount];
        for (int i = 0; i < SAV4HGSS.BoxSlotCount; i++)
        {
            int idx = i;
            _boxSlots[i] = new SlotButton();
            _boxSlots[i].Click += (_, _) =>
            {
                int box = _boxSelector.SelectedIndex;
                OnSlotClick(SlotSource.Box, idx, box);
            };
            _boxSlots[i].MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Right)
                    ShowSlotMenu(SlotSource.Box, idx, _boxSelector.SelectedIndex);
            };
            _boxGrid.Controls.Add(_boxSlots[i], i % cols, i / cols);
        }

        var scrollPanel = new Panel
        {
            Dock      = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(40, 40, 40),
        };
        scrollPanel.Controls.Add(_boxGrid);

        _boxTab.Controls.Add(scrollPanel);
        _boxTab.Controls.Add(topPanel);
    }

    // ==================== 메뉴 이벤트 ====================

    private void OnOpen(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "세이브 파일 열기",
            Filter = "세이브 파일 (*.sav;*.dsv)|*.sav;*.dsv|모든 파일|*.*",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        LoadFile(dlg.FileName);
    }

    private void LoadFile(string path)
    {
        var save = SAV4HGSS.LoadFromFile(path);
        if (save == null)
        {
            MessageBox.Show("파일을 읽을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        _save = save;
        RefreshAll();
        _statusLabel.Text = $"로드됨: {Path.GetFileName(path)}  |  {save.GetBlockInfo()}";
        Text = $"HGER Save Editor - {Path.GetFileName(path)}";
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true) { e.Effect = DragDropEffects.None; return; }
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        string ext = Path.GetExtension(files[0]).ToLowerInvariant();
        e.Effect = (ext == ".sav" || ext == ".dsv") ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true) return;
        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        LoadFile(files[0]);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (_save == null) { MessageBox.Show("열린 파일이 없습니다."); return; }
        if (!CheckWarningsBeforeSave()) return;
        if (_save.SaveToFile())
            _statusLabel.Text = $"저장 완료: {Path.GetFileName(_save.FilePath ?? "")}";
        else
            MessageBox.Show("저장 실패.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void OnSaveAs(object? sender, EventArgs e)
    {
        if (_save == null) { MessageBox.Show("열린 파일이 없습니다."); return; }
        if (!CheckWarningsBeforeSave()) return;
        using var dlg = new SaveFileDialog
        {
            Title  = "다른 이름으로 저장",
            Filter = "세이브 파일 (*.sav)|*.sav|모든 파일|*.*",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        if (_save.SaveToFile(dlg.FileName))
            _statusLabel.Text = $"저장 완료: {Path.GetFileName(dlg.FileName)}";
        else
            MessageBox.Show("저장 실패.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    /// <summary>저장 전 모든 포켓몬의 유효성 검사. 문제가 있으면 false 반환.</summary>
    private bool CheckWarningsBeforeSave()
    {
        if (_save == null) return true;

        var locations = new List<string>();

        int count = _save.PartyCount;
        for (int i = 0; i < count; i++)
        {
            PK4 pk = _save.GetPartySlot(i);
            if (!pk.IsEmpty && !pk.IsEgg && pk.HasWarnings)
                locations.Add($"파티 {i + 1}번 - {GameData.GetSpeciesName(pk.Species)}");
        }

        for (int box = 0; box < SAV4HGSS.BoxCount; box++)
        {
            for (int slot = 0; slot < SAV4HGSS.BoxSlotCount; slot++)
            {
                PK4 pk = _save.GetBoxSlot(box, slot);
                if (!pk.IsEmpty && !pk.IsEgg && pk.HasWarnings)
                {
                    string boxName = _save.GetBoxName(box).Trim();
                    if (string.IsNullOrEmpty(boxName) || int.TryParse(boxName, out _)) boxName = $"박스 {box + 1}";
                    locations.Add($"{boxName} {slot + 1}번 - {GameData.GetSpeciesName(pk.Species)}");
                }
            }
        }

        if (locations.Count == 0) return true;

        int show = Math.Min(locations.Count, 10);
        string list = string.Join("\n", locations.GetRange(0, show));
        if (locations.Count > 10)
            list += $"\n... 외 {locations.Count - 10}건";

        MessageBox.Show(
            $"다음 포켓몬에 문제가 있어 저장할 수 없습니다:\n\n{list}\n\n해당 포켓몬을 수정한 후 다시 저장해 주세요.",
            "저장 불가", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    // ==================== 슬롯 클릭 ====================

    private enum SlotSource { Party, Box }

    private void OnSlotClick(SlotSource source, int slotIndex, int boxIndex)
    {
        if (_save == null) return;

        PK4 pk = source == SlotSource.Party
            ? _save.GetPartySlot(slotIndex)
            : _save.GetBoxSlot(boxIndex, slotIndex);

        if (pk.IsEmpty) return;

        if (pk.IsEgg)
        {
            MessageBox.Show("알은 편집할 수 없습니다.", "알", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var editor = new PokemonEditorForm(pk, _save.TrainerName, _save.TID, _save.SID, _save.Gender);
        if (editor.ShowDialog() != DialogResult.OK) return;

        PK4 edited = editor.Result;
        if (source == SlotSource.Party)
            _save.SetPartySlot(slotIndex, edited);
        else
            _save.SetBoxSlot(boxIndex, slotIndex, edited);

        RefreshAll();
    }

    // ==================== 슬롯 우클릭 메뉴 ====================

    private void ShowSlotMenu(SlotSource source, int slotIndex, int boxIndex)
    {
        if (_save == null) return;

        PK4 pk = source == SlotSource.Party
            ? _save.GetPartySlot(slotIndex)
            : _save.GetBoxSlot(boxIndex, slotIndex);

        bool hasData = !pk.IsEmpty;
        bool hasClip = _clipboardRaw != null;
        if (!hasData && !hasClip) return;

        var menu = new ContextMenuStrip
        {
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.White,
        };

        if (hasData)
        {
            string name = GameData.GetSpeciesName(pk.Species);

            menu.Items.Add(new ToolStripMenuItem("복사", null, (_, _) =>
            {
                _clipboardRaw  = pk.WriteToRaw();
                _clipboardName = name;
                _statusLabel.Text = $"복사됨: {name}";
            }));

            // 파티 1마리일 때 삭제 금지
            bool canDelete = source != SlotSource.Party || _save.PartyCount > 1;
            if (canDelete)
            {
                menu.Items.Add(new ToolStripMenuItem("삭제", null, (_, _) =>
                {
                    if (MessageBox.Show(
                            $"{name}을(를) 삭제하시겠습니까?",
                            "삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;

                    if (source == SlotSource.Party)
                        DeletePartySlot(slotIndex);
                    else
                        _save.SetBoxSlot(boxIndex, slotIndex, PK4.Empty);

                    RefreshAll();
                    _statusLabel.Text = $"삭제됨: {name}";
                }));
            }
        }

        if (hasClip)
        {
            menu.Items.Add(new ToolStripMenuItem($"붙여넣기 ({_clipboardName})", null, (_, _) =>
            {
                if (hasData)
                {
                    string existing = GameData.GetSpeciesName(pk.Species);
                    if (MessageBox.Show(
                            $"이 슬롯에 {existing}이(가) 있습니다.\n{_clipboardName}(으)로 덮어쓰시겠습니까?",
                            "붙여넣기 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;
                }

                PasteToSlot(source, slotIndex, boxIndex);
                RefreshAll();
                _statusLabel.Text = $"붙여넣기 완료: {_clipboardName}";
            }));
        }

        var btn = source == SlotSource.Party ? _partySlots[slotIndex] : _boxSlots[slotIndex];
        menu.Show(btn, btn.PointToClient(Cursor.Position));
    }

    private void DeletePartySlot(int slotIndex)
    {
        if (_save == null) return;
        int count = _save.PartyCount;
        // 파티 압축: 삭제된 슬롯 이후의 포켓몬을 앞으로 당김
        for (int i = slotIndex; i < count - 1; i++)
        {
            PK4 next = _save.GetPartySlot(i + 1);
            _save.SetPartySlot(i, next);
        }
        // 마지막 슬롯 비우기
        _save.SetPartySlot(count - 1, PK4.CreateBlank(PokeCrypto.SIZE_4PARTY));
    }

    private void PasteToSlot(SlotSource source, int slotIndex, int boxIndex)
    {
        if (_save == null || _clipboardRaw == null) return;
        byte[] raw = (byte[])_clipboardRaw.Clone();

        if (source == SlotSource.Party)
        {
            // 박스 크기(136) → 파티 크기(236) 변환
            if (raw.Length < PokeCrypto.SIZE_4PARTY)
                raw = PokeCrypto.ConvertToParty(raw);
            var pk = new PK4(raw);
            _save.SetPartySlot(slotIndex, pk);
        }
        else
        {
            var pk = new PK4(raw);
            _save.SetBoxSlot(boxIndex, slotIndex, pk);
        }
    }

    // ==================== 화면 갱신 ====================

    private void RefreshAll()
    {
        if (_save == null) return;
        RefreshTrainerInfo();
        RefreshParty();
        RefreshBoxGrid();
    }

    private void RefreshTrainerInfo()
    {
        if (_save == null) return;
        var (h, m, s) = _save.Playtime;
        string gender = _save.Gender == 0 ? "♂" : "♀";
        _trainerLabel.Text =
            $"트레이너: {_save.TrainerName} {gender}  " +
            $"ID: {_save.TID:D5}  " +
            $"소지금: {_save.Money:N0}₩  " +
            $"플레이: {h:D2}:{m:D2}:{s:D2}";
    }

    private void RefreshParty()
    {
        if (_save == null) return;
        int count = _save.PartyCount;
        for (int i = 0; i < 6; i++)
        {
            PK4 pk = (i < count) ? _save.GetPartySlot(i) : PK4.Empty;
            _partySlots[i].SetPokemon(pk);
        }
    }

    private void RefreshBoxGrid()
    {
        if (_save == null || _updatingBox) return;
        _updatingBox = true;
        try
        {
            int box = _boxSelector.SelectedIndex;

            // 박스 이름 업데이트
            // 기본 이름은 0x01DE(공백) + 숫자로 저장되어 있으므로,
            // 트림 후 숫자만 남으면 기본 이름("박스 N")으로 대체
            string boxName = _save.GetBoxName(box).Trim();
            bool isDefault = string.IsNullOrEmpty(boxName) || int.TryParse(boxName, out _);
            _boxSelector.Items[box] = isDefault ? $"박스 {box + 1}" : boxName;

            for (int i = 0; i < SAV4HGSS.BoxSlotCount; i++)
            {
                PK4 pk = _save.GetBoxSlot(box, i);
                _boxSlots[i].SetPokemon(pk);
            }
        }
        finally
        {
            _updatingBox = false;
        }
    }
}

// ==================== 슬롯 버튼 컨트롤 ====================

/// <summary>
/// 파티/박스 슬롯 하나를 표시하는 버튼 컨트롤.
/// 스프라이트 이미지가 있으면 왼쪽에 표시하고 텍스트는 오른쪽에 배치.
/// </summary>
internal sealed class SlotButton : Button
{
    private static readonly Color EmptyColor  = Color.FromArgb(55, 55, 58);
    private static readonly Color FilledColor = Color.FromArgb(40, 70, 110);
    private static readonly Color ShinyColor  = Color.FromArgb(100, 85, 20);
    private static readonly Color EggColor    = Color.FromArgb(50, 90, 60);

    private static readonly int SpriteSize = 48;

    private PK4?   _pk;
    private Image? _sprite;   // GameData 캐시에서 가져온 원본 (dispose 불필요)
    private bool   _hasWarnings;

    public SlotButton()
    {
        Size      = new Size(152, 80);
        Margin    = new Padding(4);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        FlatAppearance.BorderSize  = 1;
        Font      = new Font("Segoe UI", 8.5f);
        ForeColor = Color.White;
        Cursor    = Cursors.Hand;
        DoubleBuffered = true;
        SetEmpty();
    }

    public void SetPokemon(PK4 pk)
    {
        _pk     = pk;
        _sprite = pk.IsEmpty ? null : pk.IsEgg ? GameData.GetEggSprite() : GameData.GetSprite(pk.Species, pk.Form);
        _hasWarnings = !pk.IsEmpty && !pk.IsEgg && pk.HasWarnings;

        if (pk.IsEmpty)
        {
            SetEmpty();
            return;
        }

        if (pk.IsEgg)
        {
            string eggName = GameData.GetSpeciesName(pk.Species);
            ForeColor = Color.White;
            BackColor = EggColor;
            Text = _sprite != null
                ? $"알\n({eggName})"
                : $"알\n({eggName}) | #{pk.Species}";
            Padding = _sprite != null ? new Padding(SpriteSize + 6, 0, 0, 0) : Padding.Empty;
            ToolTipText = "알은 편집할 수 없습니다.";
            Invalidate();
            return;
        }

        string name   = GameData.GetSpeciesName(pk.Species);
        int    level  = pk.Level;
        string gender = pk.Gender switch { 0 => " ♂", 1 => " ♀", _ => "" };
        string shiny  = pk.IsShiny ? " ★" : "";

        ForeColor = Color.White;
        BackColor = pk.IsShiny ? ShinyColor : FilledColor;

        // 스프라이트 없으면 종류 번호 포함, 있으면 생략
        Text = _sprite != null
            ? $"{name}{shiny}\nLv.{level}{gender}"
            : $"{name}{shiny}\nLv.{level}{gender} | #{pk.Species}";

        // 스프라이트 영역만큼 왼쪽 Padding 확보 → base.OnPaint가 나머지 영역에 텍스트 배치
        Padding = _sprite != null ? new Padding(SpriteSize + 6, 0, 0, 0) : Padding.Empty;

        ToolTipText = $"기술1: {GameData.GetMoveName(pk.Move1)}\n" +
                      $"기술2: {GameData.GetMoveName(pk.Move2)}\n" +
                      $"기술3: {GameData.GetMoveName(pk.Move3)}\n" +
                      $"기술4: {GameData.GetMoveName(pk.Move4)}";

        Invalidate();
    }

    private void SetEmpty()
    {
        BackColor = EmptyColor;
        Text      = "(비어있음)";
        ForeColor = Color.Gray;
        _sprite   = null;
        _hasWarnings = false;
        Padding   = Padding.Empty;
        Invalidate();
    }

    // ==================== 커스텀 페인트 ====================

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);   // 배경·테두리·텍스트 (Padding 이후 영역에 그려짐)

        var g = e.Graphics;

        if (_sprite != null && _pk != null && !_pk.IsEmpty)
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            int y = (Height - SpriteSize) / 2;
            g.DrawImage(_sprite, 4, y, SpriteSize, SpriteSize);
        }

        if (_hasWarnings)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var circleBrush = new SolidBrush(Color.FromArgb(220, 50, 50));
            g.FillEllipse(circleBrush, 2, 2, 20, 20);
            using var font = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            var size = g.MeasureString("!", font);
            g.DrawString("!", font, textBrush, 2 + (20 - size.Width) / 2, 2 + (20 - size.Height) / 2);
        }
    }

    // ToolTip 속성 (ToolTip 컴포넌트 없이 간단히 표시)
    public string? ToolTipText { get; private set; }
}
