namespace HGERSaveEditor.Core;

/// <summary>
/// Gen 4 포켓몬 데이터 암/복호화 유틸리티.
/// PKHeX의 PokeCrypto 로직을 참고하여 구현.
/// </summary>
public static class PokeCrypto
{
    // 파티 포켓몬: 236바이트 (배틀 스탯 포함)
    public const int SIZE_4PARTY = 236;
    // 박스 포켓몬: 136바이트 (배틀 스탯 없음)
    public const int SIZE_4STORED = 136;
    // 각 서브구조 블록 크기
    private const int BlockSize = 32;

    // 블록 순서 테이블 (PID % 24 → ABCD 블록의 저장 순서)
    // shuffle[sv][저장위치] = ABCD 인덱스 (A=0, B=1, C=2, D=3)
    private static readonly byte[][] BlockOrderTable =
    [
        [0,1,2,3],[0,1,3,2],[0,2,1,3],[0,2,3,1],[0,3,1,2],[0,3,2,1],
        [1,0,2,3],[1,0,3,2],[1,2,0,3],[1,2,3,0],[1,3,0,2],[1,3,2,0],
        [2,0,1,3],[2,0,3,1],[2,1,0,3],[2,1,3,0],[2,3,0,1],[2,3,1,0],
        [3,0,1,2],[3,0,2,1],[3,1,0,2],[3,1,2,0],[3,2,0,1],[3,2,1,0],
    ];

    /// <summary>
    /// 저장된 Gen4 포켓몬 데이터를 복호화하여 ABCD 순서로 반환.
    /// 파티 폼(236바이트)의 경우 배틀 스탯(0x88-0xEB)도 PID 시드로 복호화.
    ///
    /// hg-engine은 표준 Gen4와 다른 블록 셔플 순서를 사용할 수 있다.
    /// XOR 복호화 후 Block A의 Language 필드(블록 내 offset 15, 값 1-8)를 검사하여
    /// Block A의 실제 위치를 감지하고, 올바른 역셔플 순서를 결정한다.
    /// </summary>
    public static byte[] DecryptPK4(byte[] data)
    {
        byte[] result = (byte[])data.Clone();
        uint seed = BitConverter.ToUInt16(data, 6); // checksum → PRNG 시드

        // 128바이트 서브구조 (offset 8~135) XOR 복호화
        CryptSubstructure(result, seed, 8, 128);

        // 블록 순서 복원 (섞인 → ABCD)
        uint pid = BitConverter.ToUInt32(data, 0);
        var (sv, _) = DetectShuffleOrder(result, (int)(pid % 24));
        UnshuffleBlocks(result, sv);

        // 파티 폼: 배틀 스탯(0x88-0xEB, 100바이트)을 PID 시드로 추가 복호화
        if (result.Length == SIZE_4PARTY)
            CryptSubstructure(result, pid, 0x88, SIZE_4PARTY - 0x88);

        return result;
    }

    /// <summary>
    /// 복호화된 Gen4 포켓몬 데이터를 다시 암호화하여 저장 형태로 반환.
    /// shuffleOrder를 지정하면 해당 순서로 셔플; 미지정(-1)이면 PID%24 사용.
    /// </summary>
    public static byte[] EncryptPK4(byte[] decrypted, int shuffleOrder = -1)
    {
        byte[] result = (byte[])decrypted.Clone();

        uint pid = BitConverter.ToUInt32(decrypted, 0);
        int sv = shuffleOrder >= 0 ? shuffleOrder : (int)(pid % 24);

        // 체크섬 재계산
        ushort checksum = CalcChecksum(decrypted);
        BitConverter.GetBytes(checksum).CopyTo(result, 6);

        // 블록 셔플 (ABCD → 저장 순서)
        ShuffleBlocks(result, sv);

        // XOR 암호화 (서브구조)
        CryptSubstructure(result, checksum, 8, 128);

        // 파티 폼: 배틀 스탯(0x88-0xEB, 100바이트)을 PID 시드로 추가 암호화
        if (result.Length == SIZE_4PARTY)
            CryptSubstructure(result, pid, 0x88, SIZE_4PARTY - 0x88);

        return result;
    }

    /// <summary>
    /// 박스 포켓몬 (136바이트)을 파티 형태 (236바이트)로 확장.
    /// 배틀 스탯(100바이트)을 0으로 초기화.
    /// </summary>
    public static byte[] ConvertToParty(byte[] stored)
    {
        byte[] party = new byte[SIZE_4PARTY];
        Array.Copy(stored, party, SIZE_4STORED);
        return party;
    }

    /// <summary>
    /// 파티 포켓몬 (236바이트)을 박스 형태 (136바이트)로 축소.
    /// </summary>
    public static byte[] ConvertToStored(byte[] party)
    {
        byte[] stored = new byte[SIZE_4STORED];
        Array.Copy(party, stored, SIZE_4STORED);
        return stored;
    }

    /// <summary>
    /// 128바이트 서브구조에 대한 체크섬 계산.
    /// 모든 uint16 합의 하위 16비트.
    /// </summary>
    public static ushort CalcChecksum(byte[] decrypted)
    {
        uint sum = 0;
        for (int i = 8; i < 136; i += 2)
            sum += BitConverter.ToUInt16(decrypted, i);
        return (ushort)sum;
    }

    /// <summary>
    /// DecryptPK4와 동일하나, 감지된 셔플 순서(sv)와 레이아웃(ivAtStdLoc)을 함께 반환.
    /// sv와 레이아웃은 쌍으로 결정됨 — 독립 감지 시 불일치 발생 가능.
    /// </summary>
    public static (byte[] Data, int ShuffleOrder, bool IvAtStdLoc) DecryptPK4WithOrder(byte[] data)
    {
        byte[] result = (byte[])data.Clone();
        uint seed = BitConverter.ToUInt16(data, 6);
        CryptSubstructure(result, seed, 8, 128);

        uint pid = BitConverter.ToUInt32(data, 0);
        var (sv, ivAtStdLoc) = DetectShuffleOrder(result, (int)(pid % 24));
        UnshuffleBlocks(result, sv);

        if (result.Length == SIZE_4PARTY)
            CryptSubstructure(result, pid, 0x88, SIZE_4PARTY - 0x88);

        return (result, sv, ivAtStdLoc);
    }

    // --- 내부 메서드 ---

    /// <summary>
    /// 올바른 셔플 순서(sv)와 블록 레이아웃(ivAtStdLoc)을 함께 반환.
    /// PID%24를 먼저 시도하여 유효하면 즉시 신뢰 (표준/hg-engine 모두).
    /// PID%24 실패 시에만 전체 sv 탐색하여 hg-engine 우선으로 결정.
    /// </summary>
    private static (int sv, bool ivAtStdLoc) DetectShuffleOrder(byte[] xorDecrypted, int standardSv)
    {
        // PID%24 먼저 시도 — 유효하면 즉시 반환 (표준/hg-engine 모두)
        // 에디터가 PID%24로 암호화한 포켓몬은 반드시 여기서 올바르게 감지됨.
        // 게임이 PID%24와 다른 셔플을 사용한 포켓몬은 여기서 null → 아래 전체 검색.
        var result = VerifyUnshuffle(xorDecrypted, standardSv);
        if (result.HasValue)
            return (standardSv, result.Value);

        // PID%24 실패 → 전체 검색: hg-engine 우선
        int stdSvFound = -1;
        for (int sv = 0; sv < 24; sv++)
        {
            if (sv == standardSv) continue;
            result = VerifyUnshuffle(xorDecrypted, sv);
            if (result.HasValue)
            {
                if (!result.Value) return (sv, false); // hg-engine → 즉시 반환
                if (stdSvFound < 0) stdSvFound = sv;   // 표준 → 보류
            }
        }

        if (stdSvFound >= 0) return (stdSvFound, true);

        return (standardSv, false); // 폴백
    }

    /// <summary>
    /// 주어진 sv로 역셔플한 결과가 유효한 ABCD 블록 배치인지 검증.
    /// 반환: null=실패, true=표준 레이아웃, false=hg-engine 레이아웃.
    /// Block A 검증: Species>0, Language∈{1-5,7,8}.
    /// 레이아웃 결정: Ball이 0x83(표준)에 있으면 true, 0x63(hg-engine)에 있으면 false.
    /// Friendship>3 체크는 볼 위치가 양쪽 모두 유효한 경우에만 적용.
    /// 닉네임 종료자(0xFFFF) 필수 — 잘못된 블록 배치 오탐 방지.
    /// </summary>
    private static bool? VerifyUnshuffle(byte[] xorDecrypted, int sv)
    {
        byte[] temp = new byte[8 + BlockSize * 4];
        Array.Copy(xorDecrypted, temp, temp.Length);
        UnshuffleBlocks(temp, sv);

        // Block A: Species > 0
        if (BitConverter.ToUInt16(temp, 0x08) == 0) return null;

        // Block A: Language ∈ {1,2,3,4,5,7,8}
        byte lang = temp[0x17];
        if (lang < 1 || lang > 8 || lang == 6) return null;

        // Ball 위치로 레이아웃 결정
        byte b83 = temp[0x83], b63 = temp[0x63];
        bool ballStdOk = b83 >= 1 && b83 <= 26;
        bool ballHgeOk = b63 >= 1 && b63 <= 26;

        bool? layout;
        if (ballStdOk && !ballHgeOk) layout = true;
        else if (ballHgeOk && !ballStdOk) layout = false;
        else if (ballStdOk && ballHgeOk)
        {
            // 볼 위치 양쪽 모두 유효 → Friendship으로 추가 판별.
            // Block A: Friendship > 3 (PPUp1은 항상 0-3이므로 Block D 배제).
            // 볼이 한쪽만 유효한 경우(대부분)에는 이 체크를 생략:
            // 친밀도 0인 에디터 생성 포켓몬도 올바르게 감지되도록.
            if (temp[0x14] <= 3) return null;
            if (BitConverter.ToUInt32(temp, 0x38) != 0) layout = true;
            else layout = false;
        }
        else return null; // 볼 없음 → 실패

        // 기술/PP/IV 유효성 검증 — 잘못된 블록 배치에서의 오탐 방지
        // 올바른 블록 배치라면 Move1 > 0, PP/IV 중 하나 이상 유효해야 한다.
        bool isStd = layout.Value;
        int moveOfs = isStd ? 0x28 : 0x68;
        ushort move1 = BitConverter.ToUInt16(temp, moveOfs);
        if (move1 == 0 || move1 == 0xFFFF) return null;

        byte pp1 = temp[isStd ? 0x30 : 0x70];
        uint ivData = BitConverter.ToUInt32(temp, isStd ? 0x38 : 0x78);
        if (pp1 == 0 && ivData == 0) return null;

        // 닉네임 종료자(0xFFFF) 검증 — 잘못된 블록 배치에서의 오탐 방지
        // Gen4 문자열은 반드시 0xFFFF 종료자를 포함해야 한다.
        int nickOfs = isStd ? 0x48 : 0x28;
        int nickLen = isStd ? 22 : 16; // 표준 11chars, hg-engine 8chars (각 2바이트)
        bool hasTerminator = false;
        for (int i = nickOfs; i < nickOfs + nickLen; i += 2)
        {
            if (BitConverter.ToUInt16(temp, i) == 0xFFFF)
            {
                hasTerminator = true;
                break;
            }
        }
        if (!hasTerminator) return null;

        return layout;
    }

    // LCRNG 기반 XOR 스트림 암/복호화 (Gen4)
    private static void CryptSubstructure(byte[] data, uint seed, int offset, int length)
    {
        for (int i = 0; i < length; i += 2)
        {
            seed = seed * 0x41C64E6D + 0x6073;
            data[offset + i]     ^= (byte)(seed >> 16);
            data[offset + i + 1] ^= (byte)(seed >> 24);
        }
    }

    // ABCD → 저장 순서로 블록 셔플
    private static void ShuffleBlocks(byte[] data, int sv)
    {
        byte[] order = BlockOrderTable[sv];
        byte[] temp  = new byte[BlockSize * 4];
        // stored[i] = abcd[order[i]]
        for (int i = 0; i < 4; i++)
            Array.Copy(data, 8 + order[i] * BlockSize, temp, i * BlockSize, BlockSize);
        Array.Copy(temp, 0, data, 8, BlockSize * 4);
    }

    // 저장 순서 → ABCD 순서로 블록 역셔플
    private static void UnshuffleBlocks(byte[] data, int sv)
    {
        byte[] order = BlockOrderTable[sv];
        byte[] temp  = new byte[BlockSize * 4];
        // abcd[order[i]] = stored[i]
        for (int i = 0; i < 4; i++)
            Array.Copy(data, 8 + i * BlockSize, temp, order[i] * BlockSize, BlockSize);
        Array.Copy(temp, 0, data, 8, BlockSize * 4);
    }

    /// <summary>
    /// DS 세이브 블록용 CRC-16-CCITT 체크섬 계산.
    /// </summary>
    public static ushort CRC16(byte[] data, int offset, int length)
    {
        ushort crc = 0xFFFF;
        for (int i = offset; i < offset + length; i++)
        {
            crc ^= (ushort)(data[i] << 8);
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ 0x1021);
                else
                    crc <<= 1;
            }
        }
        return crc;
    }
}
