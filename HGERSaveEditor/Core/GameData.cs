using System.Text;

namespace HGERSaveEditor.Core;

/// <summary>
/// 게임 내 데이터 테이블 (포켓몬 이름, 기술 이름, 성격 등).
/// 종족 이름 / 기술 이름은 외부 텍스트 파일에서 로드합니다:
///   data/species.txt  - 한 줄에 하나씩, 0번부터 (0번째 줄 = "없음")
///   data/moves.txt    - 한 줄에 하나씩, 0번부터 (0번째 줄 = "없음")
///   data/items.txt    - 한 줄에 하나씩
///   data/abilities.txt
/// 파일이 없으면 "종족 #N" 형태의 기본 이름을 사용합니다.
/// hg-engine 추가 포켓몬/기술의 경우 위 파일을 편집하여 추가하세요.
/// </summary>
public static class GameData
{
    // ==================== 로드된 데이터 ====================
    private static string[] _speciesNames  = [];
    private static string[] _moveNames     = [];
    private static string[] _itemNames     = [];
    private static string[] _abilityNames  = [];
    private static string[] _ballNames     = [];
    private static readonly Dictionary<int, string> _locationNames = [];

    public static string[] MoveNames    => _moveNames;
    public static string[] SpeciesNames => _speciesNames;
    public static string[] AbilityNames => _abilityNames;
    public static string[] BallNames   => _ballNames;
    public static IReadOnlyDictionary<int, string> LocationEntries => _locationNames;

    // ==================== 폼 이름 ====================

    private static readonly Dictionary<(int species, int form), string> _formNames = [];

    public static void LoadFormNames(string path)
    {
        _formNames.Clear();
        if (!File.Exists(path)) return;
        try
        {
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                var parts = line.Split('\t');
                if (parts.Length < 3) continue;
                if (!int.TryParse(parts[0], out int species)) continue;
                if (!int.TryParse(parts[1], out int form)) continue;
                _formNames[(species, form)] = parts[2];
            }
        }
        catch { }
    }

    /// <summary>폼 이름 반환. 등록되지 않은 폼이면 null.</summary>
    public static string? GetFormName(int species, int form)
    {
        if (_formNames.TryGetValue((species, form), out var name))
            return name;
        return null;
    }

    // ==================== 스프라이트 ====================

    private static string _spriteDir = "";
    private static readonly Dictionary<(int species, int form), Image?> _spriteCache = [];

    public static void InitializeSprites(string spriteDir)
    {
        _spriteDir = spriteDir;
        foreach (var img in _spriteCache.Values) img?.Dispose();
        _spriteCache.Clear();
    }

    /// <summary>알 스프라이트를 반환합니다.</summary>
    public static Image? GetEggSprite()
    {
        if (string.IsNullOrEmpty(_spriteDir)) return null;
        if (_spriteCache.TryGetValue((-1, 0), out var cached)) return cached;
        Image? img = null;
        string path = Path.Combine(_spriteDir, "egg.png");
        if (File.Exists(path))
            try { img = Image.FromFile(path); } catch { }
        _spriteCache[(-1, 0)] = img;
        return img;
    }

    /// <summary>종족·폼에 해당하는 스프라이트 이미지를 반환합니다. 폼 스프라이트가 없으면 form 0 폴백, 그것도 없으면 null.</summary>
    public static Image? GetSprite(int species, int form = 0)
    {
        if (species <= 0 || string.IsNullOrEmpty(_spriteDir)) return null;
        if (_spriteCache.TryGetValue((species, form), out var cached)) return cached;

        // form > 0이면 species_form.png 먼저, 없으면 species.png 폴백
        Image? img = null;
        if (form > 0)
        {
            string formPath = Path.Combine(_spriteDir, $"{species}_{form}.png");
            if (File.Exists(formPath))
                try { img = Image.FromFile(formPath); } catch { }
        }
        if (img == null)
        {
            string basePath = Path.Combine(_spriteDir, $"{species}.png");
            if (File.Exists(basePath))
                try { img = Image.FromFile(basePath); } catch { }
        }

        _spriteCache[(species, form)] = img;
        return img;
    }

    // ==================== 초기화 ====================

    public static void Initialize(string dataDir = "data")
    {
        StringConverter4.Initialize(Path.Combine(dataDir, "charmap.txt"));
        _speciesNames  = LoadLines(Path.Combine(dataDir, "species.txt"),  "종족");
        _moveNames     = LoadLines(Path.Combine(dataDir, "moves.txt"),    "기술");
        _itemNames     = LoadLines(Path.Combine(dataDir, "items.txt"),    "아이템");
        _abilityNames  = LoadLines(Path.Combine(dataDir, "abilities.txt"),"특성");
        _ballNames     = LoadLines(Path.Combine(dataDir, "balls.txt"),   "볼");
        LoadLocations(Path.Combine(dataDir, "locations.txt"));
        LoadGenderless(Path.Combine(dataDir, "genderless.txt"));
    }

    private static void LoadLocations(string path)
    {
        _locationNames.Clear();
        if (!File.Exists(path)) return;
        try
        {
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                var sep = line.IndexOf('\t');
                if (sep < 0) continue;
                if (!int.TryParse(line.AsSpan(0, sep), out int id)) continue;
                _locationNames[id] = line[(sep + 1)..];
            }
        }
        catch { }
    }

    private static string[] LoadLines(string path, string prefix)
    {
        if (!File.Exists(path)) return [];
        try { return File.ReadAllLines(path, System.Text.Encoding.UTF8); }
        catch { return []; }
    }

    // ==================== 이름 조회 ====================

    public static string GetSpeciesName(int id)
    {
        if (id <= 0) return "없음";
        if (id < _speciesNames.Length && !string.IsNullOrEmpty(_speciesNames[id]))
            return _speciesNames[id];
        return $"종족 #{id}";
    }

    public static string GetMoveName(int id)
    {
        if (id <= 0) return "없음";
        if (id < _moveNames.Length && !string.IsNullOrEmpty(_moveNames[id]))
            return _moveNames[id];
        return $"기술 #{id}";
    }

    public static string GetItemName(int id)
    {
        if (id <= 0) return "없음";
        if (id < _itemNames.Length && !string.IsNullOrEmpty(_itemNames[id]))
            return _itemNames[id];
        return $"아이템 #{id}";
    }

    public static string GetAbilityName(int id)
    {
        if (id <= 0) return "없음";
        if (id < _abilityNames.Length && !string.IsNullOrEmpty(_abilityNames[id]))
            return _abilityNames[id];
        return $"특성 #{id}";
    }

    public static string GetBallName(int id)
    {
        if (id <= 0) return "없음";
        if (id < _ballNames.Length && !string.IsNullOrEmpty(_ballNames[id]))
            return _ballNames[id];
        return $"볼 #{id}";
    }

    public static string GetLocationName(int id)
    {
        if (_locationNames.TryGetValue(id, out var name))
            return name;
        return $"장소 #{id}";
    }

    // ==================== 성격 ====================

    // 인덱스 = PID % 25 (Gen4 표준 순서)
    // 0:노력(Hardy) 1:외로움(Lonely) 2:용감(Brave) 3:고집(Adamant) 4:개구쟁이(Naughty)
    // 5:대담(Bold)  6:온순(Docile) 7:무사태평(Relaxed) 8:장난꾸러기(Impish) 9:촐랑(Lax)
    // 10:겁쟁이(Timid) 11:성급(Hasty) 12:성실(Serious) 13:명랑(Jolly) 14:천진난만(Naive)
    // 15:조심(Modest) 16:의젓(Mild) 17:냉정(Quiet) 18:수줍음(Bashful) 19:덜렁(Rash)
    // 20:차분(Calm) 21:얌전(Gentle) 22:건방(Sassy) 23:신중(Careful) 24:변덕(Quirky)
    public static readonly string[] NatureNames =
    [
        "노력",   "외로움",   "용감",     "고집",   "개구쟁이",
        "대담",   "온순",   "무사태평",     "장난꾸러기", "촐랑",
        "겁쟁이",   "성급",   "성실",     "명랑",     "천진난만",
        "조심",   "의젓",   "냉정",     "수줍음",     "덜렁",
        "차분",   "얌전",   "건방",     "신중",     "변덕",
    ];

    // 성격 보정 테이블 [성격][스탯]: 1.1=강화, 0.9=약화, 1.0=중립
    // 스탯 순서: Atk=0, Def=1, SpA=2, SpD=3, Spe=4
    private static readonly double[,] NatureTable = new double[25, 5];

    static GameData()
    {
        // 모든 1.0으로 초기화
        for (int i = 0; i < 25; i++)
            for (int j = 0; j < 5; j++)
                NatureTable[i, j] = 1.0;

        // 강화/약화 설정 (보정 없는 성격: i/5 == i%5)
        for (int nat = 0; nat < 25; nat++)
        {
            int boost  = nat / 5;
            int reduce = nat % 5;
            if (boost == reduce) continue;
            NatureTable[nat, boost]  = 1.1;
            NatureTable[nat, reduce] = 0.9;
        }
    }

    public static double GetNatureModifier(int nature, int statIndex) =>
        (nature >= 0 && nature < 25) ? NatureTable[nature, statIndex] : 1.0;

    // ==================== 타입 ====================

    public static readonly string[] TypeNames =
    [
        "노말", "불꽃", "물", "전기", "풀", "얼음", "격투", "독",
        "땅", "비행", "에스퍼", "벌레", "바위", "고스트", "드래곤",
        "악", "강철", "???",
    ];

    // ==================== 성장속도 ====================

    // 성장속도 (species별, 기본값 MediumFast=0)
    // 인덱스: 0=MediumFast, 1=Erratic, 2=Fluctuating, 3=MediumSlow, 4=Fast, 5=Slow
    private static readonly int[] GrowthRateTable = new int[2000]; // 기본값 MediumFast(0)

    public static int GetGrowthRate(int species)
    {
        if (species > 0 && species < GrowthRateTable.Length)
            return GrowthRateTable[species];
        return 0; // MediumFast
    }

    public static void LoadGrowthRates(string csvPath)
    {
        if (!File.Exists(csvPath)) return;
        try
        {
            foreach (var line in File.ReadAllLines(csvPath))
            {
                var sep = line.IndexOf(',');
                if (sep < 0) continue;
                if (!int.TryParse(line.AsSpan(0, sep), out int id)) continue; // 헤더 스킵
                if (id <= 0 || id >= GrowthRateTable.Length) continue;
                GrowthRateTable[id] = ParseGrowthRateName(line[(sep + 1)..].Trim());
            }
        }
        catch { }
    }

    private static int ParseGrowthRateName(string name) => name switch
    {
        "Medium Fast"  => 0,
        "Erratic"      => 1,
        "Fluctuating"  => 2,
        "Medium Slow"  => 3,
        "Fast"         => 4,
        "Slow"         => 5,
        _              => 0,
    };

    /// <summary>경험치 테이블 (growthRate, level)
    /// 0=MediumFast, 1=Erratic, 2=Fluctuating, 3=MediumSlow, 4=Fast, 5=Slow</summary>
    public static uint GetExpTable(int growthRate, int level)
    {
        if (level <= 0) return 0;
        if (level >= 100) level = 100;
        long n = level;
        return growthRate switch
        {
            0 => (uint)(n * n * n),                                                                  // MediumFast
            1 => ErraticExp(n),                                                                       // Erratic
            2 => FluctuatingExp(n),                                                                   // Fluctuating
            3 => (uint)Math.Max(0L, 6L * n * n * n / 5 - 15L * n * n + 100L * n - 140L),           // MediumSlow
            4 => (uint)(4L * n * n * n / 5),                                                         // Fast
            5 => (uint)(5L * n * n * n / 4),                                                         // Slow
            _ => (uint)(n * n * n),
        };
    }

    // Erratic (Gen4 기준)
    private static uint ErraticExp(long n)
    {
        if (n < 50) return (uint)(n * n * n * (100 - n) / 50);
        if (n < 68) return (uint)(n * n * n * (150 - n) / 100);
        if (n < 98) return (uint)(n * n * n * ((long)Math.Floor((1911.0 * n + 1637500.0) / 3.0)) / 500000);
        return (uint)(n * n * n * (160 - n) / 100);
    }

    // Fluctuating (Gen4 기준)
    private static uint FluctuatingExp(long n)
    {
        if (n < 15) return (uint)(n * n * n * ((long)Math.Floor((n + 1.0) / 3.0) + 24) / 50);
        if (n < 36) return (uint)(n * n * n * (n + 14) / 50);
        return (uint)(n * n * n * ((long)Math.Floor(n / 2.0) + 32) / 50);
    }

    /// <summary>경험치로부터 레벨 계산</summary>
    public static int CalcLevel(uint exp, int growthRate)
    {
        for (int lv = 100; lv >= 1; lv--)
            if (exp >= GetExpTable(growthRate, lv)) return lv;
        return 1;
    }

    /// <summary>레벨에 해당하는 최소 경험치 반환</summary>
    public static uint CalcExpForLevel(int level, int species)
    {
        return GetExpTable(GetGrowthRate(species), level);
    }

    // ==================== 성별 비율 ====================

    // 성별비율 테이블 (species별). 0=항상수컷, 254=항상암컷, 255=무성, 나머지=임계값
    private static byte[] GenderRatioTable = BuildGenderTable();

    private static byte[] BuildGenderTable()
    {
        // 기본값 127 (1:1). LoadGenderless()에서 무성 포켓몬 반영.
        byte[] t = new byte[2000];
        Array.Fill(t, (byte)127);
        return t;
    }

    public static void LoadGenderless(string path)
    {
        if (!File.Exists(path)) return;
        foreach (string line in File.ReadAllLines(path))
        {
            if (int.TryParse(line.Trim(), out int id) && id > 0 && id < GenderRatioTable.Length)
                GenderRatioTable[id] = 255;
        }
    }

    public static int GetGenderRatio(int species)
    {
        if (species > 0 && species < GenderRatioTable.Length)
            return GenderRatioTable[species];
        return 127;
    }

    // ==================== 기본 스탯 ====================

    public record BaseStatsRecord(int HP, int Atk, int Def, int SpA, int SpD, int Spe);

    // 기본 스탯 테이블 (species, form) → BaseStatsRecord
    // CSV 형식:
    //   7열: id,HP,Atk,Def,SpA,SpD,Spe        → form 0으로 저장 (하위 호환)
    //   8열: id,form,HP,Atk,Def,SpA,SpD,Spe   → 지정된 form으로 저장
    private static readonly Dictionary<(int species, int form), BaseStatsRecord> _baseStats = BuildDefaultBaseStats();

    private static Dictionary<(int, int), BaseStatsRecord> BuildDefaultBaseStats()
    {
        var d = new Dictionary<(int, int), BaseStatsRecord>();
        void Add(int id, int hp, int atk, int def, int spa, int spd, int spe)
            => d[(id, 0)] = new(hp, atk, def, spa, spd, spe);

        Add(  1, 45, 49, 49, 65, 65, 45);  // 이상해씨
        Add(  2, 60, 62, 63, 80, 80, 60);  // 이상해풀
        Add(  3, 80, 82, 83,100,100, 80);  // 이상해꽃
        Add(  4, 39, 52, 43, 60, 50, 65);  // 파이리
        Add(  5, 58, 64, 58, 80, 65, 80);  // 리자드
        Add(  6, 78, 84, 78,109, 85,100);  // 리자몽
        Add(  7, 44, 48, 65, 50, 64, 43);  // 꼬부기
        Add(  8, 59, 63, 80, 65, 80, 58);  // 어니부기
        Add(  9, 79, 83,100, 85,105, 78);  // 거북왕
        Add(152, 45, 49, 65, 49, 45, 45);  // 치코리타
        Add(153, 60, 62, 80, 63, 63, 60);  // 베이리프
        Add(154, 80, 82,100, 83, 83, 80);  // 메가니움
        Add(155, 39, 52, 43, 60, 50, 65);  // 브케인
        Add(156, 58, 64, 58, 80, 65, 80);  // 마그케인
        Add(157, 78, 84, 78,109, 85,100);  // 블레이범
        Add(158, 50, 65, 64, 44, 48, 43);  // 토토돌
        Add(159, 65, 80, 80, 59, 63, 58);  // 엘리게이
        Add(160, 85,105,100, 79, 83, 78);  // 장크로다일
        Add(175, 35, 20, 35, 50, 35, 50);  // 토게피
        Add(176, 55, 40, 85, 80, 80, 40);  // 토게틱
        Add(468, 85, 50, 95, 80,115, 80);  // 토게키스

        return d;
    }

    public static void LoadBaseStats(string csvPath)
    {
        if (!File.Exists(csvPath)) return;
        try
        {
            foreach (var line in File.ReadAllLines(csvPath))
            {
                var parts = line.Split(',');
                if (parts.Length < 7) continue;
                if (!int.TryParse(parts[0], out int id) || id <= 0) continue;

                int form, hp, atk, def, spa, spd, spe;
                if (parts.Length >= 8 && int.TryParse(parts[1], out int parsedForm))
                {
                    // 8열 형식: id,form,HP,Atk,Def,SpA,SpD,Spe
                    form = parsedForm;
                    hp  = int.Parse(parts[2]); atk = int.Parse(parts[3]);
                    def = int.Parse(parts[4]); spa = int.Parse(parts[5]);
                    spd = int.Parse(parts[6]); spe = int.Parse(parts[7]);
                }
                else
                {
                    // 7열 형식: id,HP,Atk,Def,SpA,SpD,Spe (하위 호환, form=0)
                    form = 0;
                    hp  = int.Parse(parts[1]); atk = int.Parse(parts[2]);
                    def = int.Parse(parts[3]); spa = int.Parse(parts[4]);
                    spd = int.Parse(parts[5]); spe = int.Parse(parts[6]);
                }

                _baseStats[(id, form)] = new(hp, atk, def, spa, spd, spe);
            }
        }
        catch { /* 오류 무시 */ }
    }

    /// <summary>해당 종족의 사용 가능한 폼 목록을 반환. basestats에 등록된 폼만 포함, 없으면 [0].</summary>
    public static List<int> GetAvailableForms(int species)
    {
        var forms = new List<int>();
        foreach (var key in _baseStats.Keys)
            if (key.species == species)
                forms.Add(key.form);
        forms.Sort();
        if (forms.Count == 0)
            forms.Add(0);
        return forms;
    }

    /// <summary>종족·폼에 해당하는 기본 스탯을 반환. 해당 폼이 없으면 form 0 폴백, 그것도 없으면 null.</summary>
    public static BaseStatsRecord? GetBaseStats(int species, int form = 0)
    {
        if (species <= 0) return null;
        if (_baseStats.TryGetValue((species, form), out var r)) return r;
        if (form != 0 && _baseStats.TryGetValue((species, 0), out r)) return r;
        return null;
    }

    // ==================== 언어 이름 ====================
    public static string GetLanguageName(int lang) => lang switch
    {
        1 => "JPN", 2 => "ENG", 3 => "FRE",
        4 => "ITA", 5 => "GER", 7 => "SPA", 8 => "KOR",
        _ => $"Lang{lang}"
    };
}
