namespace HGERSaveEditor.Core;

/// <summary>
/// HeartGold / SoulSilver (hg-engine) 세이브 파일 파서.
///
/// 세이브 파일 레이아웃 (512KB = 0x80000 바이트):
///   0x00000 : General Block A  (alloc: 0x10000)
///   0x10000 : Storage Block A  (alloc: 0x30000)
///   0x40000 : General Block B  (backup)
///   0x50000 : Storage Block B  (backup)
///
/// 각 블록 내부에 푸터(16바이트)가 포함되며, 푸터의 Block Size 필드가
/// 자기 자신의 위치를 가리키는 자기참조 패턴으로 푸터 위치를 감지한다.
///
/// 푸터 구조 (블록 끝 16바이트):
///   +0x00 : u32 Save Count
///   +0x04 : u32 Block Size (푸터 포함 전체 블록 크기)
///   +0x08 : u32 Unknown (날짜/매직?)
///   +0x0C : u16 Unknown
///   +0x0E : u16 CRC-16 (마지막 2바이트)
///
/// CRC-16-CCITT는 블록 시작부터 푸터 직전까지(blockSize - 16 바이트)를 커버.
/// </summary>
public class SAV4HGSS
{
    // ==================== 세이브 구조 상수 ====================
    public const int SaveSize = 0x80000; // 512KB

    // 각 블록의 할당 공간 시작 오프셋 (절대 주소)
    private const int GeneralA_Base = 0x00000;
    private const int StorageA_Base = 0x10000;
    private const int GeneralB_Base = 0x40000;
    private const int StorageB_Base = 0x50000;

    // 각 블록의 할당 크기
    private const int GeneralAllocSize = 0x10000;
    private const int StorageAllocSize = 0x30000;

    // 푸터 상수
    private const int FooterSize = 0x10;
    // 푸터 내 오프셋
    private const int FooterOfs_SaveCount = 0x00; // u32
    private const int FooterOfs_BlockSize = 0x04; // u32
    private const int FooterOfs_CRC       = 0x0E; // u16 (마지막 2바이트)

    // General block 내 오프셋
    private const int OFS_TrainerName = 0x64;
    private const int OFS_TID         = 0x74;
    private const int OFS_SID         = 0x76;
    private const int OFS_Money       = 0x78;
    private const int OFS_Gender      = 0x7C;
    private const int OFS_PlaytimeH   = 0x86;
    private const int OFS_PlaytimeM   = 0x88;
    private const int OFS_PlaytimeS   = 0x89;
    private const int OFS_PartyCount  = 0x94;
    private const int OFS_Party       = 0x98;

    // Storage block 내 오프셋
    // hg-engine: 박스당 0x1000 바이트 정렬 (표준 30×136=4080이 아님)
    private const int OFS_BoxData      = 0x0000;
    private const int BoxAllocSize     = 0x1000; // 박스당 할당 크기 (4096)
    private const int OFS_BoxNames     = 0x1E000;
    private const int BoxNameEntrySize = 40;     // 박스 이름 항목 크기 (12바이트 헤더 + 이름)
    private const int BoxNameHeaderSize = 12;    // 이름 항목 내 헤더 크기
    private const int OFS_BoxWallpaper = 0x1E4B8;
    private const int OFS_CurrentBox   = 0x1E4B0;

    public const int BoxCount    = 30;
    public const int BoxSlotCount = 30;

    // ==================== 필드 ====================

    private readonly byte[] _raw;
    private int _generalBase;
    private int _storageBase;
    private int _generalBlockSize; // 푸터 포함 전체 블록 크기
    private int _storageBlockSize;
    public bool IsModified { get; private set; }
    public string? FilePath { get; private set; }

    // ==================== 생성자 ====================

    private SAV4HGSS(byte[] data, string? path)
    {
        _raw = data;
        FilePath = path;
        DetectActiveBlocks();
    }

    /// <summary>파일에서 세이브 로드</summary>
    public static SAV4HGSS? LoadFromFile(string path)
    {
        byte[] data;
        try { data = File.ReadAllBytes(path); }
        catch { return null; }

        if (data.Length < SaveSize)
        {
            byte[] padded = new byte[SaveSize];
            Array.Copy(data, padded, data.Length);
            data = padded;
        }

        return new SAV4HGSS(data, path);
    }

    // ==================== 블록 감지 ====================

    /// <summary>
    /// 활성 블록 결정 및 블록 크기 감지.
    /// 푸터의 Block Size 필드가 자기참조하는 패턴을 이용하여
    /// 블록 크기와 푸터 위치를 자동 감지한다.
    /// </summary>
    private void DetectActiveBlocks()
    {
        // General A/B 블록 크기 및 Save Count 감지
        int genSizeA = DetectBlockSize(GeneralA_Base, GeneralAllocSize);
        int genSizeB = DetectBlockSize(GeneralB_Base, GeneralAllocSize);

        uint cntA = genSizeA > 0 ? GetSaveCount(GeneralA_Base, genSizeA) : 0;
        uint cntB = genSizeB > 0 ? GetSaveCount(GeneralB_Base, genSizeB) : 0;

        if (cntA >= cntB && genSizeA > 0)
        {
            _generalBase = GeneralA_Base;
            _storageBase = StorageA_Base;
            _generalBlockSize = genSizeA;
        }
        else if (genSizeB > 0)
        {
            _generalBase = GeneralB_Base;
            _storageBase = StorageB_Base;
            _generalBlockSize = genSizeB;
        }
        else
        {
            // 폴백: block A 사용
            _generalBase = GeneralA_Base;
            _storageBase = StorageA_Base;
            _generalBlockSize = genSizeA > 0 ? genSizeA : 0xF628; // PKHeX 표준값
        }

        _storageBlockSize = DetectBlockSize(_storageBase, StorageAllocSize);
        if (_storageBlockSize <= 0)
            _storageBlockSize = 0x12310; // PKHeX 표준값
    }

    /// <summary>
    /// 블록 크기를 자기참조 패턴으로 감지.
    /// 푸터의 Block Size 필드(footer+4, u32)가 블록 크기와 일치하는 위치를 탐색.
    /// CRC-16 검증으로 오탐 방지.
    /// </summary>
    private int DetectBlockSize(int blockBase, int allocSize)
    {
        // blockSize를 X라 하면, footer는 blockBase + X - 16에 위치.
        // footer+4 (= blockBase + X - 12)에 u32로 X가 저장되어 있다.
        int maxSize = Math.Min(allocSize, _raw.Length - blockBase);
        for (int bs = FooterSize + 4; bs <= maxSize; bs += 4)
        {
            int sizeFieldOfs = blockBase + bs - 12; // footer + 4
            if (sizeFieldOfs + 4 > _raw.Length) continue;
            uint storedSize = BitConverter.ToUInt32(_raw, sizeFieldOfs);
            if (storedSize != (uint)bs) continue;

            // Block Size 자기참조 매치 → CRC 검증
            int footerOfs = blockBase + bs - FooterSize;
            int crcOfs    = footerOfs + FooterOfs_CRC;
            if (crcOfs + 2 > _raw.Length) continue;

            ushort storedCRC  = BitConverter.ToUInt16(_raw, crcOfs);
            int    dataSize   = bs - FooterSize;
            ushort computedCRC = PokeCrypto.CRC16(_raw, blockBase, dataSize);
            if (computedCRC == storedCRC)
                return bs;
        }
        return -1; // 감지 실패
    }

    private uint GetSaveCount(int blockBase, int blockSize)
    {
        int footerOfs = blockBase + blockSize - FooterSize;
        if (footerOfs + 4 > _raw.Length) return 0;
        return BitConverter.ToUInt32(_raw, footerOfs + FooterOfs_SaveCount);
    }

    // ==================== 트레이너 정보 ====================

    public string TrainerName
    {
        get => StringConverter4.DecodeString(_raw, _generalBase + OFS_TrainerName, 8);
        set
        {
            StringConverter4.EncodeString(value, _raw, _generalBase + OFS_TrainerName, 8);
            IsModified = true;
        }
    }

    public ushort TID
    {
        get => BitConverter.ToUInt16(_raw, _generalBase + OFS_TID);
        set { BitConverter.GetBytes(value).CopyTo(_raw, _generalBase + OFS_TID); IsModified = true; }
    }

    public ushort SID
    {
        get => BitConverter.ToUInt16(_raw, _generalBase + OFS_SID);
        set { BitConverter.GetBytes(value).CopyTo(_raw, _generalBase + OFS_SID); IsModified = true; }
    }

    public uint Money
    {
        get => BitConverter.ToUInt32(_raw, _generalBase + OFS_Money);
        set { BitConverter.GetBytes(Math.Min(value, 999999u)).CopyTo(_raw, _generalBase + OFS_Money); IsModified = true; }
    }

    public byte Gender
    {
        get => _raw[_generalBase + OFS_Gender];
        set { _raw[_generalBase + OFS_Gender] = (byte)(value & 1); IsModified = true; }
    }

    public (ushort Hours, byte Minutes, byte Seconds) Playtime
    {
        get => (
            BitConverter.ToUInt16(_raw, _generalBase + OFS_PlaytimeH),
            _raw[_generalBase + OFS_PlaytimeM],
            _raw[_generalBase + OFS_PlaytimeS]
        );
    }

    // ==================== 파티 포켓몬 ====================

    public int PartyCount
    {
        get => _raw[_generalBase + OFS_PartyCount];
        private set => _raw[_generalBase + OFS_PartyCount] = (byte)Math.Clamp(value, 0, 6);
    }

    public PK4 GetPartySlot(int index)
    {
        if (index < 0 || index > 5) return PK4.Empty;
        int offset = _generalBase + OFS_Party + index * PokeCrypto.SIZE_4PARTY;
        byte[] raw = new byte[PokeCrypto.SIZE_4PARTY];
        Array.Copy(_raw, offset, raw, 0, PokeCrypto.SIZE_4PARTY);
        return new PK4(raw);
    }

    public void SetPartySlot(int index, PK4 pk)
    {
        if (index < 0 || index > 5) return;
        pk.RefreshStats();
        byte[] encrypted = pk.WriteToRaw();
        int offset = _generalBase + OFS_Party + index * PokeCrypto.SIZE_4PARTY;
        Array.Copy(encrypted, 0, _raw, offset, PokeCrypto.SIZE_4PARTY);

        int currentCount = PartyCount;
        if (pk.Species == 0)
        {
            if (index < currentCount)
            {
                int newCount = 0;
                for (int i = 0; i < currentCount; i++)
                {
                    if (i == index) continue;
                    int slotOfs = _generalBase + OFS_Party + i * PokeCrypto.SIZE_4PARTY;
                    byte[] dec = PokeCrypto.DecryptPK4(_raw[slotOfs..(slotOfs + PokeCrypto.SIZE_4PARTY)]);
                    if (BitConverter.ToUInt16(dec, 8) != 0) newCount = i + 1;
                }
                PartyCount = newCount;
            }
        }
        else if (index + 1 > currentCount)
        {
            PartyCount = index + 1;
        }

        IsModified = true;
    }

    // ==================== 박스 포켓몬 ====================

    public PK4 GetBoxSlot(int box, int slot)
    {
        int offset = _storageBase + OFS_BoxData
                   + box * BoxAllocSize
                   + slot * PokeCrypto.SIZE_4STORED;
        byte[] raw = new byte[PokeCrypto.SIZE_4STORED];
        Array.Copy(_raw, offset, raw, 0, PokeCrypto.SIZE_4STORED);
        return new PK4(raw);
    }

    public void SetBoxSlot(int box, int slot, PK4 pk)
    {
        byte[] stored = PokeCrypto.ConvertToStored(pk.WriteToRaw());
        int offset = _storageBase + OFS_BoxData
                   + box * BoxAllocSize
                   + slot * PokeCrypto.SIZE_4STORED;
        Array.Copy(stored, 0, _raw, offset, PokeCrypto.SIZE_4STORED);
        IsModified = true;
    }

    public string GetBoxName(int box)
    {
        int offset = _storageBase + OFS_BoxNames + box * BoxNameEntrySize + BoxNameHeaderSize;
        if (offset + (BoxNameEntrySize - BoxNameHeaderSize) > _raw.Length) return $"BOX {box + 1}";
        return StringConverter4.DecodeString(_raw, offset, 14);
    }

    public void SetBoxName(int box, string name)
    {
        int offset = _storageBase + OFS_BoxNames + box * BoxNameEntrySize + BoxNameHeaderSize;
        if (offset + (BoxNameEntrySize - BoxNameHeaderSize) > _raw.Length) return;
        StringConverter4.EncodeString(name, _raw, offset, 14);
        IsModified = true;
    }

    public int CurrentBox
    {
        get
        {
            int offset = _storageBase + OFS_CurrentBox;
            return offset < _raw.Length ? _raw[offset] : 0;
        }
    }

    // ==================== 저장 ====================

    public bool SaveToFile(string? path = null)
    {
        string savePath = path ?? FilePath ?? throw new InvalidOperationException("저장 경로 없음");

        RecalcBlockCRC(_generalBase, _generalBlockSize);
        RecalcBlockCRC(_storageBase, _storageBlockSize);

        try
        {
            File.WriteAllBytes(savePath, _raw);
            FilePath = savePath;
            IsModified = false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 블록의 CRC-16을 재계산하여 푸터에 기록.
    /// CRC는 블록 데이터(blockSize - 16 바이트)를 커버하며,
    /// 푸터의 마지막 2바이트(footer + 0x0E)에 저장된다.
    /// </summary>
    private void RecalcBlockCRC(int blockBase, int blockSize)
    {
        if (blockSize <= FooterSize) return;
        int dataSize = blockSize - FooterSize;
        int crcOfs   = blockBase + blockSize - 2; // 푸터 마지막 2바이트
        if (crcOfs + 2 > _raw.Length) return;

        ushort crc = PokeCrypto.CRC16(_raw, blockBase, dataSize);
        BitConverter.GetBytes(crc).CopyTo(_raw, crcOfs);
    }

    // ==================== 진단 ====================

    public string GetBlockInfo()
    {
        uint cnt = (_generalBlockSize > 0)
            ? GetSaveCount(_generalBase, _generalBlockSize)
            : 0;
        string active = _generalBase == GeneralA_Base ? "A" : "B";
        return $"Active={active}  General=0x{_generalBlockSize:X}  Storage=0x{_storageBlockSize:X}  SaveCount={cnt}";
    }
}
