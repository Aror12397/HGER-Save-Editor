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

    // 블록 순서 테이블 (sv → ABCD 블록의 저장 순서)
    // sv = ((PID & 0x3E000) >> 0xD) % 24
    // shuffle[sv][저장위치] = ABCD 인덱스 (A=0, B=1, C=2, D=3)
    private static readonly byte[][] BlockOrderTable =
    [
        [0,1,2,3],[0,1,3,2],[0,2,1,3],[0,2,3,1],[0,3,1,2],[0,3,2,1],
        [1,0,2,3],[1,0,3,2],[1,2,0,3],[1,2,3,0],[1,3,0,2],[1,3,2,0],
        [2,0,1,3],[2,0,3,1],[2,1,0,3],[2,1,3,0],[2,3,0,1],[2,3,1,0],
        [3,0,1,2],[3,0,2,1],[3,1,0,2],[3,1,2,0],[3,2,0,1],[3,2,1,0],
    ];

    /// <summary>
    /// Gen4 블록 셔플 값 계산: ((PID & 0x3E000) >> 0xD) % 24.
    /// Bulbapedia Gen4 표준 공식.
    /// </summary>
    private static int GetShuffleValue(uint pid) => (int)(((pid & 0x3E000) >> 0xD) % 24);

    /// <summary>
    /// 저장된 Gen4 포켓몬 데이터를 복호화하여 ABCD 순서로 반환.
    /// 파티 폼(236바이트)의 경우 배틀 스탯(0x88-0xEB)도 PID 시드로 복호화.
    /// </summary>
    public static byte[] DecryptPK4(byte[] data)
    {
        byte[] result = (byte[])data.Clone();
        uint seed = BitConverter.ToUInt16(data, 6); // checksum → PRNG 시드

        // 128바이트 서브구조 (offset 8~135) XOR 복호화
        CryptSubstructure(result, seed, 8, 128);

        // 블록 순서 복원 (섞인 → ABCD)
        uint pid = BitConverter.ToUInt32(data, 0);
        int sv = GetShuffleValue(pid);
        UnshuffleBlocks(result, sv);

        // 파티 폼: 배틀 스탯(0x88-0xEB, 100바이트)을 PID 시드로 추가 복호화
        if (result.Length == SIZE_4PARTY)
            CryptSubstructure(result, pid, 0x88, SIZE_4PARTY - 0x88);

        return result;
    }

    /// <summary>
    /// 복호화된 Gen4 포켓몬 데이터를 다시 암호화하여 저장 형태로 반환.
    /// </summary>
    public static byte[] EncryptPK4(byte[] decrypted)
    {
        byte[] result = (byte[])decrypted.Clone();

        uint pid = BitConverter.ToUInt32(decrypted, 0);
        int sv = GetShuffleValue(pid);

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

    // --- 내부 메서드 ---

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
