# HGER Save Editor

hg-engine 기반 롬 핵의 `.sav` 파일을 편집하는 C# WinForms 애플리케이션.

## 빌드 요구사항

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (또는 Visual Studio 2022)
- Windows 10 이상

## 빌드 및 실행

```bash
cd HGERSaveEditor
dotnet build
dotnet run
```

또는 Visual Studio 2022에서 `HGERSaveEditor.sln` 열기 → 실행 (F5)

## 프로젝트 구조

```
HGERSaveEditor/
├── Core/
│   ├── PokeCrypto.cs     Gen4 포켓몬 데이터 암/복호화 (LCRNG 기반)
│   ├── PK4.cs            포켓몬 데이터 구조 (236/136바이트)
│   ├── SAV4HGSS.cs       HG/SS 세이브 파일 파서
│   └── GameData.cs       이름 테이블, 성장속도, 기본 스탯 등
├── Forms/
│   ├── MainForm.cs       파티/박스 뷰어
│   └── PokemonEditorForm.cs  포켓몬 편집 다이얼로그
└── data/                 이름 데이터 파일 (직접 작성 필요)
    ├── species.txt
    ├── moves.txt
    ├── items.txt
    ├── abilities.txt
    └── basestats.csv
```

## 세이브 파일 오프셋 조정

`SAV4HGSS.cs`의 상수값이 실제 세이브와 다를 경우 수정:

| 상수 | 기본값 | 설명 |
|------|--------|------|
| `GeneralBlockSize` | `0xF624` | General 블록 데이터 크기 |
| `StorageBlockSize` | `0x12310` | Storage 블록 데이터 크기 |
| `OFS_Party` | `0x98` | General 블록 내 파티 시작 오프셋 |
| `OFS_BoxData` | `0x0000` | Storage 블록 내 박스 데이터 시작 |
| `OFS_BoxNames` | `0x11EE0` | Storage 블록 내 박스 이름 시작 |

PKHeX로 실제 세이브를 분석하면 정확한 오프셋을 확인할 수 있습니다.

## 문자 인코딩

Gen 4 DS 문자 인코딩은 커스텀 16비트 테이블을 사용합니다.
현재 구현은 영문 기준 주요 문자만 지원합니다.
`PK4.cs`의 `StringConverter4.DecodeTable`에
PKHeX의 `StringConverter4.cs` (G4_EN 배열)을 적용하면 완전히 지원됩니다.

## 알려진 제한사항

- Gen 4 (HG/SS) 세이브 포맷 기준. hg-engine 추가 데이터가 있다면 오프셋 조정 필요.
- 닉네임 / OT 이름은 영문 기준으로 표시됨 (한글 미지원).
- 기본 스탯 데이터가 없으면 스탯 합계 계산 불가.
