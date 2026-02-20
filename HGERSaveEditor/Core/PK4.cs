using System.Text;

namespace HGERSaveEditor.Core;

/// <summary>
/// Gen 4 포켓몬 데이터 구조체 (PK4).
/// 복호화된 236바이트(파티) 또는 136바이트(박스) 데이터를 래핑.
///
/// 메모리 레이아웃 (복호화 상태 기준):
///   0x00-0x03 : PID (uint32)
///   0x04-0x05 : Sanity (uint16, 보통 0)
///   0x06-0x07 : Checksum (uint16)
///   0x08-0x27 : Block A — 종족·아이템·TID·SID·경험치·친밀도·특성·EV
///   0x28-0x47 : Block B — 기술·PP·PPUp·IV·폼
///   0x48-0x67 : Block C — 닉네임·출신 게임
///   0x68-0x87 : Block D — OT이름·날짜·장소·볼·만난 레벨
///   0x88-0xEB : Battle Stats (파티 전용, 100바이트)
/// </summary>
public class PK4
{
    // 복호화된 원시 데이터
    private readonly byte[] _data;
    public bool IsPartyForm => _data.Length == PokeCrypto.SIZE_4PARTY;

    public PK4(byte[] rawEncrypted)
    {
        _data = PokeCrypto.DecryptPK4(rawEncrypted);
    }

    // 복호화된 데이터로 직접 생성 (내부용)
    // skipChecksumValidation=true: Empty/CreateBlank에서 체크섬 검사 생략
    private PK4(byte[] decryptedData, bool skipChecksumValidation = false)
    {
        _data = decryptedData;
        _skipChecksumValidation = skipChecksumValidation;
    }

    // Empty 인스턴스는 체크섬 검사 없이 항상 빈 슬롯으로 취급
    private readonly bool _skipChecksumValidation;

    /// <summary>복호화된 상태에서 PK4를 복제</summary>
    public PK4 Clone() => new((byte[])_data.Clone(), _skipChecksumValidation);

    /// <summary>빈 포켓몬 슬롯을 나타내는 더미 PK4 생성</summary>
    public static PK4 Empty => new(new byte[PokeCrypto.SIZE_4STORED], true);

    /// <summary>
    /// 지정된 크기의 빈 PK4를 생성 (새 포켓몬용).
    /// 표준 Gen4 레이아웃 사용 (닉네임 0x48, OT 0x68, 기술 0x28).
    /// </summary>
    public static PK4 CreateBlank(int dataSize) => new(new byte[dataSize], true);

    /// <summary>이 슬롯이 비어있는지 (종족번호 0 또는 체크섬 불일치)</summary>
    public bool IsEmpty => Species == 0 || !IsChecksumValid;

    /// <summary>복호화된 데이터의 체크섬이 유효한지 확인</summary>
    private bool IsChecksumValid
    {
        get
        {
            if (_skipChecksumValidation) return true;
            ushort stored = BitConverter.ToUInt16(_data, 6);
            return PokeCrypto.CalcChecksum(_data) == stored;
        }
    }

    // ==================== Block A (0x08-0x27) ====================

    // hg-engine: 494번 이상 포켓몬은 세이브에 +50 된 값으로 저장됨
    // 표시 ID 494 → 저장 값 544, 표시 ID 495 → 저장 값 545, ...
    private const int SpeciesDisplayThreshold = 494;
    private const int SpeciesStoredThreshold  = 544;
    private const int SpeciesStoredOffset     = 50;

    /// <summary>전국도감 번호 (0=없음). 494 이상은 내부 저장 값에서 자동 변환.</summary>
    public ushort Species
    {
        get
        {
            ushort raw = ReadUInt16(0x08);
            return raw >= SpeciesStoredThreshold ? (ushort)(raw - SpeciesStoredOffset) : raw;
        }
        set
        {
            ushort stored = value >= SpeciesDisplayThreshold ? (ushort)(value + SpeciesStoredOffset) : value;
            WriteUInt16(0x08, stored);
        }
    }

    /// <summary>지닌 아이템 ID</summary>
    public ushort HeldItem
    {
        get => ReadUInt16(0x0A);
        set => WriteUInt16(0x0A, value);
    }

    /// <summary>트레이너 공개 ID</summary>
    public ushort TID
    {
        get => ReadUInt16(0x0C);
        set => WriteUInt16(0x0C, value);
    }

    /// <summary>트레이너 비밀 ID</summary>
    public ushort SID
    {
        get => ReadUInt16(0x0E);
        set => WriteUInt16(0x0E, value);
    }

    /// <summary>경험치</summary>
    public uint Exp
    {
        get => ReadUInt32(0x10);
        set => WriteUInt32(0x10, value);
    }

    /// <summary>친밀도 / 부화 남은 걸음 수</summary>
    public byte Friendship
    {
        get => _data[0x14];
        set => _data[0x14] = value;
    }

    /// <summary>특성 ID</summary>
    public byte Ability
    {
        get => _data[0x15];
        set => _data[0x15] = value;
    }

    /// <summary>마킹 비트필드</summary>
    public byte Markings
    {
        get => _data[0x16];
        set => _data[0x16] = value;
    }

    /// <summary>언어 (1=JPN, 2=ENG, 3=FRE, 4=ITA, 5=GER, 7=SPA, 8=KOR)</summary>
    public byte Language
    {
        get => _data[0x17];
        set => _data[0x17] = value;
    }

    // EVs: 저장 순서 = HP, Atk, Def, Spe, SpA, SpD
    public byte EV_HP  { get => _data[0x18]; set => _data[0x18] = value; }
    public byte EV_Atk { get => _data[0x19]; set => _data[0x19] = value; }
    public byte EV_Def { get => _data[0x1A]; set => _data[0x1A] = value; }
    public byte EV_Spe { get => _data[0x1B]; set => _data[0x1B] = value; }
    public byte EV_SpA { get => _data[0x1C]; set => _data[0x1C] = value; }
    public byte EV_SpD { get => _data[0x1D]; set => _data[0x1D] = value; }

    // ==================== Block B (0x28-0x47) ====================
    // 기술·PP·PPUp·IV·폼·만난 장소(Pt/HGSS)

    /// <summary>폼 번호 (Block B 0x40의 bit 3-7)</summary>
    public byte Form
    {
        get => (byte)(_data[0x40] >> 3);
        set => _data[0x40] = (byte)((_data[0x40] & 0x07) | ((value & 0x1F) << 3));
    }

    public ushort Move1 { get => ReadUInt16(0x28); set => WriteUInt16(0x28, value); }
    public ushort Move2 { get => ReadUInt16(0x2A); set => WriteUInt16(0x2A, value); }
    public ushort Move3 { get => ReadUInt16(0x2C); set => WriteUInt16(0x2C, value); }
    public ushort Move4 { get => ReadUInt16(0x2E); set => WriteUInt16(0x2E, value); }

    public byte PP1 { get => _data[0x30]; set => _data[0x30] = value; }
    public byte PP2 { get => _data[0x31]; set => _data[0x31] = value; }
    public byte PP3 { get => _data[0x32]; set => _data[0x32] = value; }
    public byte PP4 { get => _data[0x33]; set => _data[0x33] = value; }

    public byte PPUp1 { get => _data[0x34]; set => _data[0x34] = value; }
    public byte PPUp2 { get => _data[0x35]; set => _data[0x35] = value; }
    public byte PPUp3 { get => _data[0x36]; set => _data[0x36] = value; }
    public byte PPUp4 { get => _data[0x37]; set => _data[0x37] = value; }

    // IVs: bit-packed uint32 @ 0x38
    // bits 0-4:HP, 5-9:Atk, 10-14:Def, 15-19:Spe, 20-24:SpA, 25-29:SpD, 30:isEgg, 31:isNicknamed
    private uint IVData
    {
        get => ReadUInt32(0x38);
        set => WriteUInt32(0x38, value);
    }

    public int IV_HP  { get => (int)((IVData >>  0) & 0x1F); set => IVData = (IVData & ~(0x1Fu <<  0)) | ((uint)(value & 0x1F) <<  0); }
    public int IV_Atk { get => (int)((IVData >>  5) & 0x1F); set => IVData = (IVData & ~(0x1Fu <<  5)) | ((uint)(value & 0x1F) <<  5); }
    public int IV_Def { get => (int)((IVData >> 10) & 0x1F); set => IVData = (IVData & ~(0x1Fu << 10)) | ((uint)(value & 0x1F) << 10); }
    public int IV_Spe { get => (int)((IVData >> 15) & 0x1F); set => IVData = (IVData & ~(0x1Fu << 15)) | ((uint)(value & 0x1F) << 15); }
    public int IV_SpA { get => (int)((IVData >> 20) & 0x1F); set => IVData = (IVData & ~(0x1Fu << 20)) | ((uint)(value & 0x1F) << 20); }
    public int IV_SpD { get => (int)((IVData >> 25) & 0x1F); set => IVData = (IVData & ~(0x1Fu << 25)) | ((uint)(value & 0x1F) << 25); }

    public bool IsEgg
    {
        get => (IVData & (1u << 30)) != 0;
        set => IVData = value ? (IVData | (1u << 30)) : (IVData & ~(1u << 30));
    }

    public bool IsNicknamed
    {
        get => (IVData & (1u << 31)) != 0;
        set => IVData = value ? (IVData | (1u << 31)) : (IVData & ~(1u << 31));
    }

    /// <summary>
    /// 스탯 성격 오버라이드 raw 바이트 (hg-engine 민트 기능, Block B 0x42).
    /// 0 = 오버라이드 없음. 짝수 값 = (nature_index + 1) * 2.
    /// 표준 Gen4에서는 "Unused" 필드.
    /// </summary>
    public byte StatNature
    {
        get => _data[0x42];
        set => _data[0x42] = value;
    }

    /// <summary>스탯 보정에 사용되는 실효 성격 인덱스 (0~24). 민트 적용 시 디코딩, 아니면 PID 성격.</summary>
    public int EffectiveNature
    {
        get
        {
            byte raw = StatNature;
            if (raw >= 2 && raw % 2 == 0)
            {
                int idx = raw / 2 - 1;
                if (idx <= 24) return idx;
            }
            return Nature;
        }
    }

    /// <summary>만난 장소 ID (Pt/HGSS 필드, Block B 0x46)</summary>
    public ushort MetLocation
    {
        get => ReadUInt16(0x46);
        set => WriteUInt16(0x46, value);
    }

    // ==================== Block C (0x48-0x67) ====================
    // 닉네임, 출신 게임

    public string Nickname
    {
        get => StringConverter4.DecodeString(_data, 0x48, 11);
        set => StringConverter4.EncodeString(value, _data, 0x48, 11);
    }

    /// <summary>출신 게임 ID (Block C 0x5F)</summary>
    public byte OriginGame
    {
        get => _data[0x5F];
        set => _data[0x5F] = value;
    }

    // ==================== Block D (0x68-0x87) ====================
    // OT이름, 날짜, 볼, 만난 레벨

    public string OTName
    {
        get => StringConverter4.DecodeString(_data, 0x68, 8);
        set => StringConverter4.EncodeString(value, _data, 0x68, 8);
    }

    /// <summary>부화 날짜 (Block D 0x78)</summary>
    public (byte Year, byte Month, byte Day) EggDate
    {
        get => (_data[0x78], _data[0x79], _data[0x7A]);
        set { _data[0x78] = value.Year; _data[0x79] = value.Month; _data[0x7A] = value.Day; }
    }

    /// <summary>만난 날짜 (Block D 0x7B)</summary>
    public (byte Year, byte Month, byte Day) MetDate
    {
        get => (_data[0x7B], _data[0x7C], _data[0x7D]);
        set { _data[0x7B] = value.Year; _data[0x7C] = value.Month; _data[0x7D] = value.Day; }
    }

    /// <summary>잡은 볼 ID. HGSS는 0x86 필드를 사용하며, DP 호환용 0x83에도 동기화.</summary>
    public byte Ball
    {
        get => _data[0x86];
        set { _data[0x86] = value; _data[0x83] = value; }
    }

    /// <summary>만난 레벨 (하위 7비트) + OT 성별 (최상위 비트) (Block D 0x84)</summary>
    private byte MetLevelGender
    {
        get => _data[0x84];
        set => _data[0x84] = value;
    }

    public int MetLevel
    {
        get => MetLevelGender & 0x7F;
        set => MetLevelGender = (byte)((MetLevelGender & 0x80) | (value & 0x7F));
    }

    public bool OTIsFemale
    {
        get => (MetLevelGender & 0x80) != 0;
        set => MetLevelGender = (byte)((MetLevelGender & 0x7F) | (value ? 0x80 : 0));
    }

    // ==================== 파생 속성 ====================

    /// <summary>PID (개성값)</summary>
    public uint PID
    {
        get => ReadUInt32(0x00);
        set => WriteUInt32(0x00, value);
    }

    /// <summary>성별 (PID 기반): 0=수컷, 1=암컷, 2=무성</summary>
    public int Gender => DetermineGender();

    /// <summary>성격 (PID % 25)</summary>
    public int Nature => (int)(PID % 25);

    /// <summary>
    /// 빛나는 여부: (TID XOR SID XOR PID상위16 XOR PID하위16) < 8
    /// </summary>
    public bool IsShiny
    {
        get
        {
            uint pid = PID;
            return (TID ^ SID ^ (pid >> 16) ^ (pid & 0xFFFF)) < 8;
        }
    }

    /// <summary>
    /// 현재 레벨을 경험치 표로부터 계산.
    /// </summary>
    public int Level
    {
        get => CalcLevel();
        set => Exp = GameData.CalcExpForLevel(value, Species);
    }

    // ==================== 배틀 스탯 (파티 전용, 0x88~) ====================
    // PKHeX Gen4 기준 레이아웃:
    //   0x88: Status (u32)
    //   0x8C: Level (u8)
    //   0x8D: Unknown (u8)
    //   0x8E: CurrentHP (u16)
    //   0x90: MaxHP (u16)
    //   0x92: Atk (u16)
    //   0x94: Def (u16)
    //   0x96: Spe (u16)
    //   0x98: SpA (u16)
    //   0x9A: SpD (u16)

    public uint Status { get => IsPartyForm ? ReadUInt32(0x88) : 0; set { if (IsPartyForm) WriteUInt32(0x88, value); } }
    public byte CurrentLevel { get => IsPartyForm ? _data[0x8C] : (byte)0; set { if (IsPartyForm) _data[0x8C] = value; } }
    public ushort CurrentHP { get => IsPartyForm ? ReadUInt16(0x8E) : (ushort)0; set { if (IsPartyForm) WriteUInt16(0x8E, value); } }
    public ushort MaxHP      { get => IsPartyForm ? ReadUInt16(0x90) : (ushort)0; set { if (IsPartyForm) WriteUInt16(0x90, value); } }
    public ushort Stat_Atk   { get => IsPartyForm ? ReadUInt16(0x92) : (ushort)0; set { if (IsPartyForm) WriteUInt16(0x92, value); } }
    public ushort Stat_Def   { get => IsPartyForm ? ReadUInt16(0x94) : (ushort)0; set { if (IsPartyForm) WriteUInt16(0x94, value); } }
    public ushort Stat_Spe   { get => IsPartyForm ? ReadUInt16(0x96) : (ushort)0; set { if (IsPartyForm) WriteUInt16(0x96, value); } }
    public ushort Stat_SpA   { get => IsPartyForm ? ReadUInt16(0x98) : (ushort)0; set { if (IsPartyForm) WriteUInt16(0x98, value); } }
    public ushort Stat_SpD   { get => IsPartyForm ? ReadUInt16(0x9A) : (ushort)0; set { if (IsPartyForm) WriteUInt16(0x9A, value); } }

    // ==================== 직렬화 ====================

    /// <summary>
    /// 복호화된 데이터를 암호화하여 세이브 파일에 쓸 수 있는 형태로 반환.
    /// </summary>
    public byte[] WriteToRaw()
    {
        byte[] copy = (byte[])_data.Clone();
        return PokeCrypto.EncryptPK4(copy);
    }

    /// <summary>
    /// 배틀 스탯을 현재 스펙에 맞게 재계산 (파티 전용).
    /// </summary>
    public void RefreshStats()
    {
        if (!IsPartyForm) return;
        int lv = Level;
        CurrentLevel = (byte)lv;
        var bs = GameData.GetBaseStats(Species, Form);
        if (bs == null) return;

        MaxHP    = (ushort)CalcStat(bs.HP,  IV_HP,  EV_HP,  lv, natureModifier: 1.0, isHP: true);
        Stat_Atk = (ushort)CalcStat(bs.Atk, IV_Atk, EV_Atk, lv, NatureModifier(0));
        Stat_Def = (ushort)CalcStat(bs.Def, IV_Def, EV_Def, lv, NatureModifier(1));
        Stat_SpA = (ushort)CalcStat(bs.SpA, IV_SpA, EV_SpA, lv, NatureModifier(3));
        Stat_SpD = (ushort)CalcStat(bs.SpD, IV_SpD, EV_SpD, lv, NatureModifier(4));
        Stat_Spe = (ushort)CalcStat(bs.Spe, IV_Spe, EV_Spe, lv, NatureModifier(2));
        CurrentHP = MaxHP;
    }

    // ==================== 유효성 검사 ====================

    /// <summary>포켓몬 데이터의 유효성 문제 목록을 반환</summary>
    public List<string> GetValidationWarnings()
    {
        var warnings = new List<string>();
        if (Species == 0)
            warnings.Add("종류가 '없음'입니다.");
        if (string.IsNullOrWhiteSpace(Nickname))
            warnings.Add("닉네임이 없습니다.");
        if (Ability == 0)
            warnings.Add("특성이 없습니다.");
        bool hasMove = (Move1 != 0 && Move1 != 0xFFFF) ||
                       (Move2 != 0 && Move2 != 0xFFFF) ||
                       (Move3 != 0 && Move3 != 0xFFFF) ||
                       (Move4 != 0 && Move4 != 0xFFFF);
        if (!hasMove)
            warnings.Add("배운 기술이 하나도 없습니다.");
        if (MetLevel == 0)
            warnings.Add("만난 레벨이 0입니다.");
        return warnings;
    }

    /// <summary>비어있지 않은 포켓몬에 유효성 경고가 있는지</summary>
    public bool HasWarnings => !IsEmpty && GetValidationWarnings().Count > 0;

    // ==================== 내부 헬퍼 ====================

    private ushort ReadUInt16(int offset) => BitConverter.ToUInt16(_data, offset);
    private uint   ReadUInt32(int offset) => BitConverter.ToUInt32(_data, offset);
    private void WriteUInt16(int offset, ushort val) => BitConverter.GetBytes(val).CopyTo(_data, offset);
    private void WriteUInt32(int offset, uint val)   => BitConverter.GetBytes(val).CopyTo(_data, offset);

    private int DetermineGender()
    {
        int threshold = GameData.GetGenderRatio(Species);
        return threshold switch
        {
            255 => 2, // 무성
            254 => 1, // 항상 암컷
            0   => 0, // 항상 수컷
            _ => (PID & 0xFF) < (uint)threshold ? 1 : 0
        };
    }

    private int CalcLevel()
    {
        uint exp = Exp;
        int growthRate = GameData.GetGrowthRate(Species);
        for (int lv = 100; lv >= 1; lv--)
        {
            if (exp >= GameData.GetExpTable(growthRate, lv))
                return lv;
        }
        return 1;
    }

    // 성격 보정값 계산 (Gen4 기준: 0=Atk, 1=Def, 2=Spe, 3=SpA, 4=SpD)
    // 민트가 적용된 경우 EffectiveNature를 사용
    private double NatureModifier(int statIndex)
    {
        int nat = EffectiveNature;
        int boosts = nat / 5;
        int reduces = nat % 5;
        if (boosts == reduces) return 1.0; // 무보정 성격
        if (boosts == statIndex) return 1.1;
        if (reduces == statIndex) return 0.9;
        return 1.0;
    }

    private static int CalcStat(int baseStat, int iv, int ev, int level, double natureModifier = 1.0, bool isHP = false)
    {
        int val = (2 * baseStat + iv + ev / 4) * level / 100;
        if (isHP)
            return val + level + 10;
        else
            return (int)((val + 5) * natureModifier);
    }
}

/// <summary>
/// Gen 4 문자열 인코더/디코더.
/// data/charmap.txt에서 문자 테이블을 로드합니다 (HGER 게임 전용 매핑).
/// Initialize()를 먼저 호출해야 합니다.
/// </summary>
public static class StringConverter4
{
    private const ushort TerminatorChar = 0xFFFF;

    private static readonly Dictionary<ushort, char> DecodeTable = new();
    private static readonly Dictionary<char, ushort> EncodeTable = new();

    /// <summary>
    /// charmap.txt 파일에서 문자 테이블을 로드합니다.
    /// GameData.Initialize()에서 자동으로 호출됩니다.
    /// </summary>
    public static void Initialize(string charmapPath)
    {
        DecodeTable.Clear();
        EncodeTable.Clear();

        if (!File.Exists(charmapPath)) return;

        try
        {
            foreach (var rawLine in File.ReadLines(charmapPath, Encoding.UTF8))
            {
                // 주석 제거 (//)
                var commentIdx = rawLine.IndexOf("//", StringComparison.Ordinal);
                var line = commentIdx >= 0 ? rawLine.Substring(0, commentIdx) : rawLine;

                // 앞쪽 공백/탭만 제거 (뒤쪽은 문자 값의 일부일 수 있음)
                line = line.TrimStart(' ', '\t');
                if (line.Length == 0) continue;

                var eqIdx = line.IndexOf('=');
                if (eqIdx < 0) continue;

                var codeStr = line.Substring(0, eqIdx);
                var val = line.Substring(eqIdx + 1);

                if (!ushort.TryParse(codeStr, System.Globalization.NumberStyles.HexNumber, null, out ushort code))
                    continue;

                // 컨트롤/명령 코드 건너뜀 ({STRVAR_4}, {PAUSE} 등)
                if (val.StartsWith("{")) continue;

                // null 터미네이터 이스케이프 (\x0000) 건너뜀
                if (val == "\\x0000") continue;

                // 단일 문자만 처리 (이스케이프 시퀀스 \n, \r 등은 2자이므로 자동으로 제외)
                if (val.Length != 1) continue;

                var c = val[0];
                if (c == '\0') continue;

                DecodeTable[code] = c;
                // 같은 문자가 여러 코드에 매핑된 경우 첫 번째 코드만 인코딩에 사용
                EncodeTable.TryAdd(c, code);
            }
        }
        catch { /* 파일 읽기 실패 시 빈 테이블 유지 */ }
    }

    public static string DecodeString(byte[] data, int offset, int maxChars)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < maxChars; i++)
        {
            ushort code = BitConverter.ToUInt16(data, offset + i * 2);
            if (code == TerminatorChar || code == 0x0000) break;

            if (DecodeTable.TryGetValue(code, out char c))
                sb.Append(c);
            else
                break; // 알 수 없는 코드 → 중단 (박스 이름 쓰레기 데이터 방지)
        }
        return sb.ToString();
    }

    public static void EncodeString(string text, byte[] data, int offset, int maxChars)
    {
        // 전체 영역을 null terminator로 초기화
        for (int i = 0; i < maxChars; i++)
            BitConverter.GetBytes(TerminatorChar).CopyTo(data, offset + i * 2);

        int len = Math.Min(text.Length, maxChars - 1);
        for (int i = 0; i < len; i++)
        {
            ushort code = EncodeTable.TryGetValue(text[i], out ushort c) ? c : (ushort)0x0000;
            BitConverter.GetBytes(code).CopyTo(data, offset + i * 2);
        }
        // null terminator
        BitConverter.GetBytes(TerminatorChar).CopyTo(data, offset + len * 2);
    }
}
