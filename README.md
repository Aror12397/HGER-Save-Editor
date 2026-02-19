# HGER Save Editor

hg-engine 기반 포켓몬 롬 핵(HGER)의 `.sav` 파일을 편집하는 C# WinForms 애플리케이션.

## 기능

- **파티 / 박스 편집** — 6슬롯 파티, 18박스 × 30슬롯 박스 지원
- **포켓몬 편집** — 종류, 폼, 닉네임, 레벨, 경험치, 특성, 지닌 아이템, 친밀도, 볼, 언어
- **기술 편집** — 4슬롯 기술 ID, PP, PP업
- **스탯 편집** — IV, EV, 계산 스탯 표시 (IV 최대, EV 균등, EV 초기화 버튼)
- **만남 정보** — 어버이 이름, TID/SID, 만난 레벨, 만난 장소, 만난 날짜
- **트레이너 정보** — 이름, TID/SID, 소지금, 성별, 플레이 시간
- **스프라이트 표시** — 폼별 스프라이트 지원
- **드래그 앤 드롭** — `.sav` / `.dsv` 파일을 끌어다 놓으면 바로 열기
- **PID 기반 자동 표시** — 성격, 성별, 이로치 여부를 PID에서 자동 판별

## hg-engine 대응

표준 HG/SS와 다른 hg-engine 고유 동작을 자동 처리합니다:

- 블록 셔플 순서 자동 감지 (PID%24와 불일치하는 경우 대응)
- Block B/C/D 콘텐츠 순환 이동 (표준 / hg-engine 레이아웃 자동 판별)
- IV 데이터 오프셋 동적 결정 (`0x38` / `0x78`)
- 494번 이상 종족 번호 +50 변환
- 기술 없음 마커 `0xFFFF` 처리
- 파티 배틀 스탯 PID 시드 암복호화
- 블록 크기 자동 감지 (푸터 자기참조 패턴 + CRC 검증)

## 빌드 요구사항

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10 이상

## 빌드 및 실행

```bash
cd HGERSaveEditor
dotnet build
dotnet run
```

또는 Visual Studio에서 `HGERSaveEditor.sln` 열기 → 실행 (F5)

### 배포용 빌드 (단일 exe)

```bash
cd HGERSaveEditor
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

결과물: `bin/Release/net10.0-windows/win-x64/publish/HGERSaveEditor.exe`

실행 시 `data/` 폴더를 exe와 같은 위치에 두어야 합니다.

## 프로젝트 구조

```
HGERSaveEditor/
├── Core/
│   ├── PokeCrypto.cs          Gen4 암/복호화 (LCRNG + 블록 셔플)
│   ├── PK4.cs                 포켓몬 데이터 구조 (236/136바이트)
│   ├── SAV4HGSS.cs            세이브 파일 파서 (A/B 블록 자동 감지)
│   └── GameData.cs            이름 테이블, 기본 스탯, 성장속도, 스프라이트
├── Forms/
│   ├── MainForm.cs            파티/박스 뷰어 (메인 윈도우)
│   └── PokemonEditorForm.cs   포켓몬 편집 다이얼로그
└── data/
    ├── charmap.txt            Gen4 문자 인코딩 테이블
    ├── species.txt            포켓몬 이름
    ├── moves.txt              기술 이름
    ├── items.txt              아이템 이름
    ├── abilities.txt          특성 이름
    ├── locations.txt          장소 이름
    ├── basestats.csv          기본 스탯
    ├── growth_rates.csv       성장속도
    ├── forms.txt              폼 이름
    └── sprites/               포켓몬 스프라이트 (PNG)
```

## 데이터 파일 커스터마이즈

`data/` 폴더의 텍스트 파일을 직접 수정하여 hg-engine 추가 포켓몬/기술에 대응할 수 있습니다.
각 파일은 줄 번호 = ID 방식이며, 493번 이후 줄에 추가 포켓몬을 이어 붙이면 됩니다.
