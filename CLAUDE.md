# HGER Save Editor — Claude Context Guide

**C# WinForms** app for editing `.sav` files of an hg-engine based Pokémon ROM hack (HGER).

---

## Table of Contents

1. [Build & Run](#build--run)
2. [Project Structure](#project-structure)
3. [Core Classes](#core-classes)
   - [SAV4HGSS](#sav4hgss)
   - [PK4](#pk4)
   - [PokeCrypto](#pokecrypto)
   - [StringConverter4](#stringconverter4)
   - [GameData](#gamedata)
   - [MainForm](#mainform)
4. [Data File Formats](#data-file-formats)
5. [hg-engine 특이사항](#hg-engine-특이사항)
6. [Offset Adjustment Notes](#offset-adjustment-notes)
7. [Coding Conventions](#coding-conventions)
8. [Known Limitations / TODO](#known-limitations--todo)

---

## Build & Run

```bash
cd HGERSaveEditor
dotnet build
dotnet run
```

- Target framework: `net10.0-windows`
- Solution: `HGERSaveEditor.sln`
- Output: `HGERSaveEditor/bin/Debug/net10.0-windows/`

---

## Project Structure

```
HGER/
├── CLAUDE.md                        ← this file
├── README.md
├── HGERSaveEditor.sln
├── HGER_HARD_0.9.7.sav              ← test save file
└── HGERSaveEditor/
    ├── Program.cs                   ← entry point
    ├── HGERSaveEditor.csproj
    ├── app.manifest
    ├── Core/
    │   ├── PokeCrypto.cs            ← Gen4 encrypt/decrypt
    │   ├── PK4.cs                   ← Pokémon data struct + StringConverter4
    │   ├── SAV4HGSS.cs              ← save file parser
    │   └── GameData.cs              ← name tables, growth rates, base stats, sprites
    ├── Forms/
    │   ├── MainForm.cs              ← main window (party tab, box tab)
    │   └── PokemonEditorForm.cs     ← Pokémon edit dialog
    └── data/
        ├── README.txt               ← data 디렉토리 설명
        ├── charmap.txt              ← Gen4 character encoding (HGER-specific)
        ├── species.txt              ← Pokémon names (line 0 = "없음")
        ├── moves.txt                ← move names
        ├── items.txt                ← item names
        ├── abilities.txt            ← ability names
        ├── locations.txt            ← location names (tab-separated: id\tname)
        ├── basestats.csv            ← base stats (7열 or 8열, 아래 참조)
        ├── growth_rates.csv         ← species별 성장속도 (id,GrowthRate)
        ├── forms.txt                ← 폼 이름 (tab-separated: species\tform\tname)
        └── sprites/                 ← 포켓몬 스프라이트 ({species}.png, {species}_{form}.png)
```

---

## Core Classes

### SAV4HGSS

**File:** `Core/SAV4HGSS.cs`

512KB 세이브 전체를 `byte[] _raw`로 들고 있는 파서.
A/B 두 블록이 항상 쌍으로 존재하며, 푸터의 Save Count가 높은 쪽이 "활성 블록"이다.
수정 후 저장할 때 반드시 CRC를 재계산해야 게임이 세이브를 유효로 인식한다.

**Save file layout** (512 KB = `0x80000`):

| Abs. offset | Block | Alloc size |
|-------------|-------|-----------|
| `0x00000` | General A | `0x10000` |
| `0x10000` | Storage A | `0x30000` |
| `0x40000` | General B (backup) | `0x10000` |
| `0x50000` | Storage B (backup) | `0x30000` |

**General Block offsets** (relative to block base):

| Offset | Field | Note |
|--------|-------|------|
| `0x64` | Trainer name | 16 bytes, max 8 chars |
| `0x74` | TID | u16 |
| `0x76` | SID | u16 |
| `0x78` | Money | u32, capped at 999999 |
| `0x7C` | Gender | u8 — `0`=남, `1`=여 |
| `0x86` | Playtime hours | u16 |
| `0x88` | Playtime minutes | u8 |
| `0x89` | Playtime seconds | u8 |
| `0x94` | Party count | u8 |
| `0x98` | Party data | 6 × 236 bytes |

**Storage Block offsets** (relative to block base):
> hg-engine에서는 이 값들이 표준 HG/SS와 다를 수 있으므로 PKHeX로 먼저 검증할 것.

| Offset | Field | Note |
|--------|-------|------|
| `0x0000` | Box data | 18 boxes × 30 slots × 136 bytes |
| `0x11EE0` | Box names | 18 × 40 bytes |
| `0x11FE8` | Box wallpaper | 18 bytes |
| `0x11FF8` | Current box index | u8 |

**Footer layout** (블록 끝 16바이트, 자기참조 패턴으로 위치 자동 감지):

```
+0x00  u32  Save Count  ← 이 값으로 A/B 중 최신 블록 판별
+0x04  u32  Block Size  ← 푸터 포함 전체 블록 크기 (자기참조)
+0x08  u32  Unknown (날짜/매직?)
+0x0C  u16  Unknown
+0x0E  u16  CRC-16      ← 마지막 2바이트
```

> 블록 크기와 푸터 위치는 `DetectBlockSize()`가 Block Size 필드의 자기참조 패턴 + CRC 검증으로 자동 감지한다.
> 감지 실패 시 폴백: General `0xF628`, Storage `0x12310`.

---

### PK4

**File:** `Core/PK4.cs`

복호화된 Gen4 포켓몬 데이터를 감싸는 클래스.
생성자에 암호화된 raw bytes를 넘기면 내부에서 즉시 복호화한다.
파티/박스 형태를 `IsPartyForm` 플래그로 구분하며, Battle Stats 접근 시 이를 반드시 확인해야 한다.

**Size:**

| Form | Size | Note |
|------|------|------|
| Party (`SIZE_4PARTY`) | 236 bytes | Battle Stats 포함 |
| Stored (`SIZE_4STORED`) | 136 bytes | Battle Stats 없음 |

**Memory layout** (decrypted):

| Range | Content |
|-------|---------|
| `0x00–0x03` | PID (uint32) |
| `0x04–0x05` | Sanity (uint16) |
| `0x06–0x07` | Checksum (uint16) |
| `0x08–0x27` | **Block A** — 종족·아이템·TID·SID·경험치·친밀도·특성·EV |
| `0x28–0x47` | **Block B** — 기술·PP·PP업·IV bitfield |
| `0x48–0x67` | **Block C** — 닉네임·출신 게임 |
| `0x68–0x87` | **Block D** — OT이름·날짜·장소·볼·만난 레벨 |
| `0x88–0xEB` | **Battle Stats** — 파티 전용 (상태이상·레벨·HP·6스탯) |

**IV bit-packing** (`uint32` @ `0x38`):
```
bits  0– 4 : HP
bits  5– 9 : Atk
bits 10–14 : Def
bits 15–19 : Spe
bits 20–24 : SpA
bits 25–29 : SpD
bit      30 : isEgg
bit      31 : isNicknamed
```

**Shiny check:** `(TID ^ SID ^ (PID >> 16) ^ (PID & 0xFFFF)) < 8`

**Gender:** `PID & 0xFF` vs `GenderRatioTable[species]`
- `0` = 항상 수컷, `254` = 항상 암컷, `255` = 무성, 나머지 = 임계값 비교

**Nature:** `PID % 25` → `NatureNames[]` 인덱스

---

### PokeCrypto

**File:** `Core/PokeCrypto.cs`

Gen4의 암복호화는 두 단계로 구성된다: LCRNG 기반 XOR 스트림 + ABCD 블록 순서 셔플.
순서가 중요하다 — 복호화는 XOR 먼저, 역셔플 나중이고, 암호화는 셔플 먼저, XOR 나중이다.

**Decrypt flow:**
1. Checksum (offset `0x06`, u16) → PRNG seed
2. XOR decrypt bytes `0x08–0x87` (LCRNG: `seed = seed * 0x41C64E6D + 0x6073`)
3. Unshuffle blocks: `DetectShuffleOrder()` → 자동 감지된 sv 사용 (아래 참조)
4. **파티 폼 전용**: XOR decrypt bytes `0x88–0xEB` using **PID** as seed (100 bytes)

**Encrypt flow:**
1. Recalculate checksum (sum of u16s over bytes `0x08–0x87`, ABCD 순서 상태에서 계산)
2. Shuffle blocks: 복호화 시 감지된 동일 sv 사용 (`PK4._shuffleOrder`)
3. XOR encrypt bytes `0x08–0x87` using checksum as seed
4. **파티 폼 전용**: XOR encrypt bytes `0x88–0xEB` using **PID** as seed (100 bytes)

**CRC-16-CCITT** (for save block footer):
- Poly `0x1021`, initial value `0xFFFF`
- 적용 범위: 블록 데이터 전체 (푸터 제외)

---

### StringConverter4

**File:** `Core/PK4.cs` (bottom of file)

charmap.txt 파일을 파싱해서 `ushort ↔ char` 매핑 테이블을 구성한다.
알 수 없는 코드를 만나면 디코딩을 그 자리에서 중단한다 — 박스 이름의 쓰레기 데이터를 막기 위한 의도적 설계다.

- Null terminator: `0xFFFF`
- `{STRVAR_4}`, `{PAUSE}` 등 중괄호 제어 코드는 로드 시 건너뜀
- 같은 `char`가 여러 코드에 매핑된 경우 인코딩에는 첫 번째 코드만 사용 (`TryAdd`)
- `GameData.Initialize()` 호출 시 자동 초기화됨 — 별도 호출 불필요

---

### GameData

**File:** `Core/GameData.cs`

이름 파일은 런타임에 로드된다 — 파일이 없어도 앱은 실행되고, `"종족 #N"` 식의 폴백 이름을 반환한다.
hg-engine에서 추가된 포켓몬/기술은 `data/` 파일을 직접 수정해서 대응한다.

**Initialization:**

```csharp
GameData.Initialize("data");                              // charmap + 4 name files + locations.txt
GameData.LoadBaseStats("data/basestats.csv");              // optional, for stat calc
GameData.LoadGrowthRates("data/growth_rates.csv");         // optional, for level calc
GameData.InitializeSprites("data/sprites");                // optional, for sprite display
GameData.LoadFormNames("data/forms.txt");                  // optional, for form names
```

**Built-in base stats** (하드코딩된 예시):
- 1–9 (Bulbasaur–Blastoise), 152–160 (Chikorita–Feraligatr)
- 175–176 (Togepi/Togetic), 468 (Togekiss)
- 나머지는 `data/basestats.csv`에서 로드해야 스탯 계산이 정확해짐

**Nature modifier** (stat index: `Atk=0, Def=1, Spe=2, SpA=3, SpD=4`):
```
boost  = nature / 5   → 해당 스탯 ×1.1
reduce = nature % 5   → 해당 스탯 ×0.9
boost == reduce       → 무보정 성격 (6의 배수: 노력, 온순, 성실, 수줍음, 변덕)
```
※ Gen4 실제 순서: Spe=2, SpA=3, SpD=4. (GameData.cs 주석의 SpA=2, SpD=3, Spe=4는 오기)

**Growth rate system:**
- `LoadGrowthRates(csv)`: species별 성장속도 로드 (`data/growth_rates.csv`)
- `GetGrowthRate(species)`: 성장속도 인덱스 반환 (0=MediumFast, 1=Erratic, 2=Fluctuating, 3=MediumSlow, 4=Fast, 5=Slow)
- `GetExpTable(growthRate, level)`: 레벨 도달에 필요한 누적 경험치 반환
- `CalcLevel(exp, growthRate)`: 경험치로부터 레벨 역산
- `CalcExpForLevel(level, species)`: 레벨에 해당하는 최소 경험치 반환

**Form system:**
- `LoadFormNames(path)`: 폼 이름 테이블 로드 (`data/forms.txt`, 탭 구분: `species\tform\tname`)
- `GetFormName(species, form)`: 폼 이름 반환 (등록되지 않은 폼이면 `null`)
- `GetAvailableForms(species)`: basestats에 등록된 폼 목록 반환 (없으면 `[0]`)

**Sprite system:**
- `InitializeSprites(spriteDir)`: 스프라이트 디렉토리 설정 및 캐시 초기화
- `GetSprite(species, form)`: 스프라이트 `Image` 반환 (form > 0이면 `{species}_{form}.png` 먼저 시도, 없으면 `{species}.png` 폴백)
- 캐시: `Dictionary<(int species, int form), Image?>` — 동일 요청 반복 시 파일 I/O 없음

**기타 데이터:**
- `TypeNames[]`: 타입 이름 배열 (노말, 불꽃, 물, …, ???)
- `GetLanguageName(lang)`: 언어 코드 → 이름 (1→JPN, 2→ENG, …, 8→KOR)

---

### MainForm

**File:** `Forms/MainForm.cs`

- Tab layout: **파티** (6 slots) / **박스** (18 boxes × 30 slots)
- `SlotButton` color scheme:
  - Empty: `RGB(55, 55, 58)` 회색
  - Filled: `RGB(40, 70, 110)` 파란색
  - Shiny: `RGB(100, 85, 20)` 금색
- `.sav` / `.dsv` 파일을 드래그 앤 드롭으로 바로 열 수 있음
- 슬롯 클릭 → `PokemonEditorForm` 모달 → `Result` PK4 → `SetPartySlot` / `SetBoxSlot`
- 박스 콤보박스 변경 시 `_updatingBox` 플래그로 재진입(re-entry) 방지

### PokemonEditorForm

**File:** `Forms/PokemonEditorForm.cs`

포켓몬 편집 다이얼로그. 탭 4개 구성:
- **기본**: 종류·폼·닉네임·레벨/EXP·성격·성별·PID·빛나기·특성·지닌 아이템·친밀도·언어·볼
- **기술**: 4슬롯 — 기술 ID(검색 콤보박스)·PP·PP업
- **스탯 / EV·IV**: 6스탯 기본·IV·EV·계산값 + EV 합계 표시 + 빠른 버튼(IV 최대, EV 균등, EV 초기화)
- **만남 / OT**: 어버이 이름·TID/SID·만난 레벨·만난 장소·만난 날짜

- `SearchComboBox`: 기술/아이템 콤보박스에 텍스트 검색(부분 일치) 기능 추가. IME 조합 중 상태를 P/Invoke로 감지하여 한글 입력 버그 방지.
- PID 저장 시 선택한 성격(`nature`)에 맞게 `PID % 25 == desiredNature`가 되도록 자동 조정

---

## Data File Formats

### `data/charmap.txt`

```
// 주석은 // 뒤로
0180=A
0181=B
01FF={STRVAR_4}   ← 중괄호 제어 코드는 무시됨
```

### `data/species.txt`, `data/moves.txt`, `data/items.txt`, `data/abilities.txt`

```
없음        ← index 0 (반드시 있어야 함)
이상해씨    ← index 1
이상해풀    ← index 2
```

hg-engine 추가 포켓몬은 493번 이후 줄에 이어 붙이면 된다.

### `data/basestats.csv`

7열 형식 (form 0 기본):
```
id,HP,Atk,Def,SpA,SpD,Spe
1,45,49,49,65,65,45
```

8열 형식 (폼 지정):
```
id,form,HP,Atk,Def,SpA,SpD,Spe
6,1,78,130,111,130,85,100
6,2,78,104,78,159,115,100
```

두 형식 혼용 가능. 헤더 행 없이 숫자만 있어도 파싱된다 (`int.TryParse` 첫 번째 열 실패 시 자동 스킵).
해당 폼 스탯이 없으면 form 0으로 폴백.

### `data/growth_rates.csv`

```
id,GrowthRate
1,Medium Slow
4,Medium Slow
25,Medium Fast
143,Slow
```

유효 값: `Medium Fast`, `Erratic`, `Fluctuating`, `Medium Slow`, `Fast`, `Slow`.
헤더 행은 자동 스킵 (`int.TryParse` 실패). 미등록 species는 `Medium Fast`(0) 기본값.

### `data/forms.txt`

```
6	1	메가 리자몽 X
6	2	메가 리자몽 Y
9	1	메가 거북왕
```

탭 구분 (`species\tform\tname`). `GameData.LoadFormNames()`에서 로드.
등록되지 않은 폼은 `"폼 {form}"` 이름으로 표시.

### `data/sprites/`

```
sprites/
├── 1.png      ← species 1, form 0
├── 3.png      ← species 3, form 0
├── 3_1.png    ← species 3, form 1 (메가진화 등)
├── 6_1.png    ← species 6, form 1
└── 6_2.png    ← species 6, form 2
```

파일명 규칙: `{species}.png` (기본 폼), `{species}_{form}.png` (폼 변형).
`GetSprite(species, form)`이 form > 0일 때 `{species}_{form}.png` 먼저 시도, 없으면 `{species}.png` 폴백.

### `data/locations.txt`

```
0	수수께끼의 장소
1	떡잎마을
126	연두마을
```

탭 구분 (`id\t이름`). `GameData.Initialize()`에서 자동 로드.

---

## hg-engine 특이사항

표준 HG/SS와 다른 hg-engine 고유 동작. 새 버그를 발견하면 여기에 추가할 것.

### 블록 셔플 순서: PID%24와 불일치

hg-engine이 생성한 포켓몬은 표준 Gen4의 `PID % 24` 블록 셔플 순서와 **다른** 순서로 암호화될 수 있다.
원인은 불명확하나 (hg-engine의 암호화 코드가 다를 가능성), 실제 세이브 데이터에서 확인됨.

**감지 방법 (`DetectShuffleOrder` + `VerifyUnshuffle`):**

XOR 복호화 후, 24개 sv를 순차 시도하여 유효한 ABCD 배치를 탐색한다.
PID%24 (표준 sv)를 먼저 시도하고, hg-engine 해석이 발견되면 즉시 반환한다.

`VerifyUnshuffle(sv)` 검증 단계:
1. 주어진 sv로 역셔플하여 Block A 검증: **Species > 0**, **Language ∈ {1-5,7,8}**, **Friendship > 3**
2. **Ball 위치**로 레이아웃 결정: 표준(`0x83`) vs hg-engine(`0x63`), 값이 1~26 범위인 쪽 채택
3. 양쪽 모두 유효하면 `0x38 != 0`이면 표준, 아니면 hg-engine
4. **Move1 유효성**: 레이아웃에 맞는 기술 오프셋에서 Move1 > 0 ∧ ≠ 0xFFFF
5. **PP/IV 유효성**: PP1 == 0 ∧ IVData == 0이면 거부 (잘못된 블록 배치 오탐 방지)

반환값: `null`(실패), `true`(표준 레이아웃), `false`(hg-engine 레이아웃).
hg-engine 해석을 우선하되, 없으면 표준 해석을 사용한다.

**확인된 세이브 데이터 (HGER_HARD_0.9.7.sav):**

| 슬롯 | PID%24 (표준) | 감지 sv | Species | 비고 |
|------|-------------|---------|---------|------|
| 0 | 7 (BADC) | 7 (BADC) | 158 | 표준과 일치 |
| 1 | 23 (DCBA) | 13 (CADB) | 709 | 불일치 |
| 2 | 0 (ABCD) | 17 (CDBA) | 26 | 불일치 |
| 3 | 17 (CDBA) | 1 (ABDC) | 26 | 불일치 |

- `PK4._shuffleOrder` 필드에 감지된 sv를 저장하여 재암호화 시 동일 순서 사용
- `PokeCrypto.EncryptPK4(data, shuffleOrder)` — 지정된 sv로 셔플

### 기술 없음 마커: `0xFFFF`

hg-engine은 기술 슬롯이 비어있을 때 표준 Gen4의 `0x0000` 대신 **`0xFFFF`** 를 사용한다.

- `PK4.Move1`~`Move4` (`ushort`) 값이 `0xFFFF`이면 "기술 없음"을 의미
- `PokemonEditorForm.MoveRow.Load()` 에서 `0xFFFF`는 `0`으로 변환해 "없음"으로 표시
- `MoveRow.NumMoveID.Maximum = 65535` (ushort 최대값) — `9999`로 줄이면 로드 시 `ArgumentOutOfRangeException` 발생

**확인된 세이브 데이터 (HGER_HARD_0.9.7.sav, 파티 슬롯 0):**
```
Moves: 1790, 2378, 2962, 0xFFFF
```
→ HGER는 커스텀 기술 ID가 1000번대 이상까지 존재한다. `moves.txt`에 해당 ID까지 항목을 채워야 이름이 표시된다.

### 파티 배틀 스탯 추가 암호화 (PID 시드)

Gen4 파티 폼의 배틀 스탯(`0x88–0xEB`, 100바이트)은 서브구조와 **별도로** PID를 PRNG 시드로 XOR 암호화된다. 표준 PKHeX 코드도 이 두 번째 암호화 레이어를 처리한다.

- 서브구조(`0x08–0x87`): Checksum 시드로 암호화
- 배틀 스탯(`0x88–0xEB`): **PID 시드**로 암호화 (파티 전용)
- `PokeCrypto.DecryptPK4` / `EncryptPK4`에 `if (result.Length == SIZE_4PARTY)` 조건 분기 추가

이 레이어를 복호화하지 않으면 Level, HP, Atk, Def, Spe, SpA, SpD가 모두 쓰레기값으로 표시된다.

**배틀 스탯 레이아웃 (복호화 후, PKHeX Gen4 기준):**
```
0x88: Status (u32)
0x8C: Level (u8)       ← 이전 코드가 0x8E로 잘못 읽고 있었음
0x8D: Unknown (u8)
0x8E: CurrentHP (u16)
0x90: MaxHP (u16)
0x92: Atk (u16)
0x94: Def (u16)
0x96: Spe (u16)
0x98: SpA (u16)
0x9A: SpD (u16)
```

**확인된 세이브 데이터 (HGER_HARD_0.9.7.sav, 파티 슬롯 0, PID 복호화 후):**
```
Lv6, Status=0, CurrentHP=22, MaxHP=22, Atk=15, Def=13, Spe=11, SpA=9, SpD=12
```

### Block B/C/D 콘텐츠 순환 이동 (hg-engine 포맷)

hg-engine이 생성한 포켓몬은 복호화·역셔플 후 Block B/C/D의 **콘텐츠** 배치가 표준 Gen4와 다르다.

| 블록 | 표준 Gen4 | hg-engine |
|------|-----------|-----------|
| B (0x28-0x47) | 기술·PP·PPUp·IVs | 닉네임 |
| C (0x48-0x67) | 닉네임·출신게임 | OT이름·날짜·볼·만난레벨 |
| D (0x68-0x87) | OT이름·날짜·볼·만난레벨 | 기술·PP·PPUp·IVs |

**오프셋 변환 규칙:**
- 표준 Block D 필드: `std_offset - 0x20` = hg-engine Block C 오프셋
- 표준 Block B 필드 (폼): `0x68 + (std_offset - 0x28)` = hg-engine Block D 오프셋
- 표준 Block C 필드 (출신게임): `0x28 + (std_offset - 0x48)` = hg-engine Block B 오프셋

**확인된 필드 오프셋 (슬롯 0, hg-engine 형식):**

| 필드 | 표준 offset | hg-engine offset | 슬롯 0 값 |
|------|-------------|------------------|-----------|
| Ball | 0x83 | 0x63 | 4 (Poké Ball) |
| MetLevel | 0x84 (bits 0-6) | 0x64 | 5 |
| OTFemale | 0x84 (bit 7) | 0x64 | 1 (여) |
| MetLocation | 0x46 (Block B) | 0x86 (Block D) | 126 |
| Pokerus | 0x82 | 0x62 | 0 |
| Form | 0x40 (Block B) | 0x80 (Block D) | 0 |
| OriginGame | 0x5F (Block C) | 0x3F (Block B) | 7 (HG) |
| EggDate | 0x78 | 0x58 | — |
| MetDate | 0x7B | 0x5B | — |

- `PK4._ivAtStdLoc` 플래그 하나로 모든 포맷 분기를 처리
- 표준 Gen4 포켓몬은 `_ivAtStdLoc=true` → 기존 오프셋 그대로

### Met Location: Block B의 Pt/HGSS 필드 사용

HGSS 포켓몬의 Met Location은 Block D의 DP 필드(`0x80`)가 아니라 **Block B의 Pt/HGSS 필드**에 저장된다.
hg-engine은 Egg Location을 u16→u32로 확장하여, Met Location이 표준 `0x44`에서 **`0x46`**으로 2바이트 밀린다.

| 필드 | 표준 (Block B) | hg-engine (Block D) | 비고 |
|------|---------------|---------------------|------|
| Egg Location | 0x42 (u32) | 0x82 (u32) | u16→u32 확장 |
| Met Location | 0x46 (u16) | 0x86 (u16) | +2 밀림 |

- DP Met Location 필드(`0x80`/`0x60`)에는 쓰레기 값(3002 등)이 들어있을 수 있음
- `data/locations.txt` (탭 구분, `id\t이름`) 파일로 장소 이름 표시

### IV 데이터 오프셋: 포켓몬별 동적 결정

hg-engine 포켓몬은 두 가지 IV 저장 위치를 혼용한다. 생성 시 `0x38` 값으로 위치를 판별한다.

| 슬롯 | 0x38 값 | 0x78 값 | 사용 위치 |
|------|---------|---------|----------|
| 슬롯 0 (hg-engine) | `00 00 00 00` | `82 B6 CC 3F` | **0x78** |
| 슬롯 1 (표준 Gen4) | `BF 18 9A 31` | Met Date 데이터 | **0x38** |

- `PK4._ivAtStdLoc`은 `PokeCrypto.DecryptPK4WithOrder` → `VerifyUnshuffle`의 Ball 위치 판별 결과로 결정됨
- Ball 위치가 양쪽 모두 유효할 때만 `ReadUInt32(0x38) != 0`을 2차 판별에 사용
- 읽기/쓰기 모두 판별된 위치 사용 → 표준 포켓몬의 MetDate(`0x7B`) 훼손 방지

**확인된 슬롯 0 (hg-engine 형식):**
```
0x38: 00 00 00 00  ← 비어있음
0x78: 82 B6 CC 3F  ← IVData = 0x3FCCB682
  IV_HP=2, IV_Atk=20, IV_Def=13, IV_Spe=25, IV_SpA=28, IV_SpD=31
```
**확인된 슬롯 1 (표준 Gen4 형식):**
```
0x38: BF 18 9A 31  ← IVData = 0x319A18BF (HP:31, Atk:5, Def:6, Spe:20, SpA:25, SpD:24)
0x78: 00 00 1A 02  ← Met Date (2026-02-18), IVData 아님
```

### 종족 번호 저장 오프셋: 494번 이상 +50

hg-engine은 494번 이상 포켓몬의 종족 번호를 세이브에 **+50** 더한 값으로 저장한다.

| 표시 ID | 저장 값 |
|---------|---------|
| 493 이하 | 그대로 |
| 494 | 544 |
| 495 | 545 |
| … | … |

- `PK4.Species` getter에서 저장 값 ≥ 544이면 −50 변환하여 반환
- `PK4.Species` setter에서 표시 값 ≥ 494이면 +50 변환하여 저장
- 변환 상수: `SpeciesDisplayThreshold=494`, `SpeciesStoredThreshold=544`, `SpeciesStoredOffset=50`

---

## Offset Adjustment Notes

hg-engine ROM 핵은 표준 HG/SS 오프셋과 다를 수 있다.
`SAV4HGSS.cs` 상단 `private const int OFS_*` 상수를 수정하는 것이 유일한 해결책이다.
PKHeX에서 대상 세이브를 열어 Hex Viewer로 각 오프셋을 확인한 뒤 수정하면 된다.

> 블록 크기(`GeneralBlockSize`, `StorageBlockSize`)는 상수가 아니라 `DetectBlockSize()`가 런타임에 자동 감지한다.
> 감지 실패 시 폴백: General `0xF628`, Storage `0x12310`.

바꿔야 할 상수 목록:

| Constant | Default | Description |
|----------|---------|-------------|
| `OFS_Party` | `0x98` | Party start in General block |
| `OFS_BoxData` | `0x0000` | Box data start in Storage block |
| `OFS_BoxNames` | `0x11EE0` | Box names start in Storage block |
| `OFS_BoxWallpaper` | `0x11FE8` | Box wallpaper in Storage block |
| `OFS_CurrentBox` | `0x11FF8` | Current box index in Storage block |

---

## Coding Conventions

- **Language:** C# 12, file-scoped namespaces (`namespace HGERSaveEditor.Core;`)
- **Section separators:** `// ==================== 섹션 ====================`
- **Offsets:** always hex literals (`0x98`, not `152`)
- **Modification flag:** set `IsModified = true` on every write to `_raw`
- **UI theme:** dark — base `RGB(30, 30, 30)`, panels `RGB(40–50, 40–50, 40–50)`
- **Null init pattern:** `= null!` at field declaration, assigned in `InitializeComponent()`
- **No extra validation:** 내부 데이터에는 방어 코드 최소화 — 경계값만 `Math.Clamp` / `Math.Min`으로 처리
- **No abstractions for one-off ops:** 단일 사용 헬퍼 클래스/메서드 추가 금지

---

## Known Limitations / TODO

### PID 관련 기능 (PokemonEditorForm)

Gen4에서 PID는 성격(`PID % 25`), 성별(`PID & 0xFF` vs threshold), 이로치(`TID ^ SID ^ PID` 연산), 셔플 순서(`PID % 24`) 등 여러 속성과 엮여 있다.

**구현 상태:**

| 기능 | 컨트롤 | 보존 | 알고리즘 |
|------|--------|------|----------|
| PID 랜덤 생성 | `btnRandPID` | 성격 | 랜덤 u32, `pid - (pid%25) + nature` |
| 이로치 토글 | `_btnShiny` (☆ 버튼) | 성격, 성별 | 설정: 하위 바이트(성별) 고정, 상위 바이트(0~255)×r(0~7) 탐색으로 `PID%25==nature` 후보 선택. 해제: 랜덤 상위 비트 + `AdjustPIDForNature` |
| 성별 토글 | `_btnGender` | 성격 | 하위 바이트 교체 + `AdjustPIDForNature` (CRT: 256 mod 25 = 6, 6⁻¹ mod 25 = 21) |

**`AdjustPIDForNature(pid, nature)`:** 하위 바이트(성별)를 보존하면서 `k*256`을 더해 `PID%25 == nature`로 조정.

**`PK4.PID` setter:** 값 변경 시 `_shuffleOrder = -1`로 리셋. `EncryptPK4`가 새 `PID%24` 기반 셔플을 사용하여 게임(원본 ROM의 PID%24 로직)과 정합성 유지. save.c에 별도 포켓몬 암호화 코드가 없으므로 게임은 표준 PID%24 셔플을 사용한다고 판단.
