# Safety Guardians — 세션 성과 & 클리어 UI 설계서

> **용도**: 세션 성과 집계 및 마지막 공장 클리어 UI 구현 시 기준 문서  
> **대상**: 프로젝트를 처음 보는 사람도 한 번에 이해할 수 있도록 작성  
> **최종 갱신**: 2026-06-09

### 관련 문서

| 문서 | 언제 읽나 |
|------|-----------|
| **이 문서 (Spec)** | 규칙·UI·등급·데이터 구조가 궁금할 때 |
| **[WorkPackages.md](./SessionStats-And-ClearUI-WorkPackages.md)** | **다른 AI/팀원에게 넘길 때**, 천천히 **WP 단위**로 나눠 구현할 때 |

> 다른 AI에게 작업을 맡길 때: **WorkPackages.md §0 복붙 블록** + 원하는 `WP 번호`를 지정하세요.

---

## 목차

0. [관련 문서 (WorkPackages)](#관련-문서)
1. [게임 개요](#1-게임-개요)
2. [이번에 만드는 것](#2-이번에-만드는-것)
3. [화면별 역할](#3-화면별-역할)
4. [통계 집계 개념](#4-통계-집계-개념)
5. [클리어 조건](#5-클리어-조건)
6. [집계 지표](#6-집계-지표)
7. [등급·점수·타이틀 (S~D)](#7-등급점수타이틀-sd)
8. [챕터별 스냅샷 & 그래프](#8-챕터별-스냅샷--그래프)
9. [UISessionClear UI](#9-uisessionclear-ui)
10. [코드·시스템 구조](#10-코드시스템-구조)
11. [구현 체크리스트](#11-구현-체크리스트)
12. [확정 사항 총정리](#12-확정-사항-총정리)
13. [자주 헷갈리는 것](#13-자주-헷갈리는-것)

---

## 1. 게임 개요

| 항목 | 설명 |
|------|------|
| **장르** | 공장 맵을 돌아다니며 **오염 몬스터를 정화**하는 액션 + **턴제 전투** |
| **플레이어 자원** | **산소** — 줄어들면 게임오버 |
| **전투 목표** | 몬스터 **오염도를 0**으로 만들어 정화 |
| **맵 구조** | **공장 3개** (챕터 1 → 2 → 3). 같은 씬 안에서 맵만 바뀜 |
| **진행** | 각 공장의 몬스터를 전부 정화하면 그 공장 **클리어** |

### 관련 기존 코드 (참고)

| 클래스 | 역할 |
|--------|------|
| `ChapterManager` | 챕터(공장) 전환, `CurrentChapterIndex`, `ChapterCount` |
| `MonsterSpawner` | 몬스터 스폰, `RemainingMonsterCount`, `OnAllMonstersCleared` |
| `GameManager` | `NotifyStageCleared()`, `RestartCurrentChapter()`, `LoadOpeningScene()` |
| `UIResult` | 공장 클리어 시 아이템 표시 (챕터 1·2) |
| `UIGameOver` | 산소 고갈 게임오버 |
| `UILoading` | 다음 공장으로 이동 |
| `PollutionManager` | 챕터 오염도, `OnMonsterPurified()` |

---

## 2. 이번에 만드는 것

> **플레이 내내 통계를 쌓아 두었다가, 마지막 공장(챕터 3)의 몬스터를 전부 정화했을 때만**  
> **`UISessionClear` UI로 성과를 보여준다.**

### UI 표현 (확정)

| 단계 | 내용 |
|------|------|
| **메인 화면** | **S~D 등급** 한 장 + 타이틀 + (선택) clearRun 한 줄 요약 |
| **상세보기** | 챕터별 **막대 그래프** + **좌우 2열** 상세표 + 획득 아이템 전체 |

- 게임오버 화면·공장 1·2 클리어 화면에는 **세션 성과를 넣지 않는다.**
- 클리어 **전용 씬은 만들지 않는다.** Canvas UI 패널로 처리한다 (`UIResult`와 동일한 방식).
- **F 등급은 사용하지 않는다.** 클리어 화면이 뜨는 경우 최하 등급은 **D**이다.

---

## 3. 화면별 역할

### 플로우 다이어그램

```
[새 게임 시작]
      │
      ▼
┌─ 공장 1 ─────────────────────────┐
│  몬스터 전부 정화                   │
│  → ChapterSnapshot[1] 저장         │  ← 내부만, UI 없음
│  → UIResult (기존)                 │  ← 아이템만
│  → UILoading → 공장 2              │
└───────────────────────────────────┘
      │
      ▼
┌─ 공장 2 ─────────────────────────┐
│  → ChapterSnapshot[2] 저장         │
│  → UIResult → UILoading → 공장 3   │
└───────────────────────────────────┘
      │
      ▼
┌─ 공장 3 (마지막) ──────────────────┐
│  → ChapterSnapshot[3] 저장         │
│  → UISessionClear (신규) ★         │  ← S~D + 상세보기
└───────────────────────────────────┘

[산소 0] → UIGameOver (기존)          ← 성과 통계 없음
[처음부터] → 통계 전부 리셋
[현재 공장 재시작] → sessionTotal 유지, clearRun·해당 챕터 진행 중 통계 리셋
```

### 화면 요약표

| 화면 | 언제 | 성과 통계 | 비고 |
|------|------|-----------|------|
| **UIResult** | 공장 1·2 클리어 | ❌ | 기존 유지. 아이템 + "공장 정화 완료" |
| **UISessionClear** | **공장 3** 몬스터 전멸 | ✅ | **신규**. 메인(S~D) + 상세보기 |
| **UIGameOver** | 산소 고갈 | ❌ | 기존 유지 |
| **UILoading** | 공장 1·2 → 다음 이동 | ❌ | 기존 유지 |

---

## 4. 통계 집계 개념

### 4.1 세션이란?

**「새 게임」~ 「마지막 공장 클리어」 또는 「처음부터 시작」** 까지 한 번의 플레이.

| 이벤트 | 동작 |
|--------|------|
| 세션 시작 | `BeginNewPlaySession` (오프닝 후 새 게임) |
| 세션 전체 리셋 | `LoadOpeningScene` / 처음부터 시작 |

### 4.2 이중 기록 (sessionTotal + clearRun)

통계를 **두 종류**로 동시에 쌓는다.

| 이름 | 변수(가칭) | 의미 | UI 표시 위치 |
|------|------------|------|--------------|
| **이번 여정** | `sessionTotal` | 재시도·게임오버 포함 **전체 누적** | 상세보기 **오른쪽 열** |
| **최종 클리어** | `clearRun` | **메인 S~D 등급** — 공장 3 run | 상세보기 **왼쪽 열** + **메인 등급** |

```
예시:
  공장 1~2 플레이 → sessionTotal, clearRun 둘 다 누적
  공장 3 진입     → clearRun만 0부터 다시 (옵션 B)
  공장 3에서 2번 죽음 → sessionTotal: 게임오버 +2 유지
                       clearRun: 재시도마다 리셋
  공장 3 클리어   → 메인 등급=clearRun S~D, 상세=2열+그래프
```

### 4.3 Run 리셋 규칙 (옵션 B — 확정)

| 이벤트 | sessionTotal | clearRun | chapterSnapshots (진행 중) |
|--------|--------------|----------|----------------------------|
| 새 게임 / 처음부터 시작 | **리셋** | **리셋** | **리셋** |
| 공장 1·2 클리어 → 다음 공장 | 유지 | 유지 | **해당 챕터 스냅샷 저장** |
| **공장 3 진입** | 유지 | **리셋** | — |
| 현재 공장 재시작 / 게임오버 후 재시도 | 유지 | **리셋** | **현재 챕터 진행 통계 리셋** (스냅샷은 클리어 시 갱신) |
| 공장 N 클리어 | 유지 | (3만 스냅샷) | **ChapterSnapshot[N] 갱신** |

### 4.4 챕터별 스냅샷 (ChapterSnapshot)

공장 **클리어 순간**마다 그 챕터의 성과를 **고정 저장**한다. 상세보기 **막대 그래프**의 데이터 소스.

| 저장 시점 | 동작 |
|-----------|------|
| 공장 1 클리어 | `chapterSnapshots[0]` 저장 |
| 공장 2 클리어 | `chapterSnapshots[1]` 저장 |
| 공장 3 클리어 | `chapterSnapshots[2]` 저장 (최종) |

**재시도 규칙**: 같은 공장을 재클리어하면 **해당 챕터 슬롯만** 새 값으로 **덮어쓴다**. 다른 공장 스냅샷은 유지.

---

## 5. 클리어 조건

**아래를 모두 만족할 때만** `UISessionClear`를 연다.

| # | 조건 | 코드 기준 |
|---|------|-----------|
| 1 | **마지막 공장** | `CurrentChapterIndex == ChapterCount` (현재: 챕터 3) |
| 2 | **몬스터 전부 정화** | `MonsterSpawner.RemainingMonsterCount == 0` → `NotifyStageCleared()` |
| 3 | **게임오버 상태가 아님** | 산소 0으로 끝난 경우 제외 |

공장 1·2에서 몬스터 전멸 → **`UIResult`만** + `ChapterSnapshot` 내부 저장 (조건 1 불만족).

### 클리어 UI 분기 (의사 코드)

```csharp
void OnStageCleared()
{
    PlaySessionStats.SaveChapterSnapshot(CurrentChapterIndex);

    if (CurrentChapterIndex == ChapterCount)
    {
        PlaySessionStats.TakeFinalSnapshot();
        UISessionClear.ShowMain();
    }
    else
    {
        UIResult.ShowStageClearResult(); // 기존
    }
}
```

---

## 6. 집계 지표

### 6.1 1차 구현 (A + C) — 현재 범위

#### A. 정화·진행

| 지표 | sessionTotal | clearRun | ChapterSnapshot | 규칙 |
|------|:------------:|:--------:|:---------------:|------|
| 정화 몬스터 수 | ✓ | ✓ | ✓ | **몬스터 ID당 최초 1회만** +1 (챕터 내) |
| 클리어한 공장 수 | ✓ | — | — | 공장 클리어 시 +1, 최대 3 |
| 플레이 시간 | ✓ | ✓ | ✓ | 재시도·게임오버 포함 (챕터별 구간) |
| 도달 공장 | ✓ | — | — | 진행 중 최대 (클리어 시 3) |

#### C. 생존·자원

| 지표 | sessionTotal | clearRun | ChapterSnapshot | 규칙 |
|------|:------------:|:--------:|:---------------:|------|
| 남은 산소 % | — | ✓ | ✓ | **해당 공장 클리어 순간** |
| 도망 횟수 | ✓ | ✓ | ✓ | 도망할 때마다 +1 |
| 도망 패널티 | ✓ | ✓ | — | UI에는 **횟수만** 표시 가능 |
| 획득 아이템 | ✓ | — | — | **세션 획득 로그** 별도 저장 ※ |
| 게임오버(위기) 횟수 | ✓ | — | — | 상세보기만, **등급 계산 제외** |

> ※ 챕터 전환 시 `InventoryManager.ClearInventory()`가 호출되므로,  
> 최종 화면용 아이템은 **세션 전용 획득 기록**에 누적한다.

### 6.2 2차 구현 (B. 전투) — 추후

| 지표 | sessionTotal | clearRun | ChapterSnapshot |
|------|:------------:|:--------:|:---------------:|
| 총 전투 횟수 | ✓ | ✓ | ✓ |
| 승리 / 도망 | ✓ | ✓ | — |
| 평균 전투 턴 | ✓ | ✓ | — |

상세보기 2열 **맨 아래 한 줄** + (선택) 챕터 그래프 보조 지표로 추가.

---

## 7. 등급·점수·타이틀 (S~D)

### 7.1 등급 체계 (F 미사용 — 확정)

클리어 화면이 뜨는 경우 **최하 등급은 D**. **F는 사용하지 않는다.**

등급은 **점수 + 최소 조건**을 함께 본다. **S부터 내려가며** 해당 등급 조건을 **모두** 만족하는 **가장 높은 등급**을 부여한다.

| 등급 | 점수 (0~100) | 남은 산소 | 도망 (공장 3 run) | 의미 |
|------|:------------:|:---------:|:-----------------:|------|
| **S** | **85 이상** | **50% 이상** | **3회 이하** | 마지막 공장을 매우 안정적으로 정화 |
| **A** | **70 이상** | **35% 이상** | **5회 이하** | 잘 클리어 |
| **B** | **55 이상** | **20% 이상** | **7회 이하** | 보통 — 도망을 써도 충분히 도달 가능 |
| **C** | **40 이상** | **10% 이상** | **제한 없음** | 아슬아슬하게 클리어 |
| **D** | 클리어 | — | — | C 조건 미달이지만 **공장 3 클리어** |

> **도망 0회는 S 조건이 아님.** S는 도망 **3회 이하** + 산소·점수를 함께 본다.  
> 도망만 많고 점수가 낮으면 B/C/D로 내려간다.

**판정 순서 (구현)**

```
1. clearRun(공장 3 run)으로 점수 계산 (7.2)
2. S 조건 충족? → S
   아니면 A 조건? → A
   … 반복 …
3. C까지 미달이면 → D (클리어 전제)
```

### 7.2 점수 계산식 (1차안)

내부 **0~100점**. 등급 **후보**를 정하고, **7.1 표의 산소·도망**으로 최종 등급을 확정한다.

```
점수 = Clamp(
    (정화 몬스터 × 10)
  + (남은 산소% × 0.5)
  − (도망 × 5),
  0, 100
)
```

| 항목 | 비고 |
|------|------|
| 도망 패널티 | **1회당 −5** (기존 −8에서 완화 — 도망 써도 S/A 가능) |
| 정화 1마리 | +10 (공장 3 run 기준 몬스터 수에 맞게 상한 튜닝 가능) |

| 적용 대상 | 설명 |
|-----------|------|
| **메인 S~D** | `clearRun` (공장 3 최종 run) |
| **챕터별 S~D** | 각 `ChapterSnapshot` — **동일 공식·동일 7.1 표** |
| **sessionTotal** | 등급 계산 **제외** |

> 밸런스는 `SessionGradeCalculator` 한곳에서 조정.

### 7.3 등급별 칭호 (1:1 — 확정)

메인 화면: **등급 문자(S~D) + 아래 칭호**를 항상 쌍으로 표시.

| 등급 | 칭호 |
|------|------|
| **S** | **완벽한 수호자** |
| **A** | **베테랑 수호자** |
| **B** | **현장 수호자** |
| **C** | **간신히 해낸 수호자** |
| **D** | **버틴 수호자** |

플레이 스타일 **별도 조건 없음** — 등급만 맞으면 해당 칭호.

### 7.4 등급 예시 (공장 3 run)

| 상황 | 점수(대략) | 도망 | 산소 | 등급 |
|------|-----------|------|------|------|
| 정화 8, 도망 2, 산소 65% | ~88 | 2 | 65% | **S** |
| 정화 7, 도망 4, 산소 40% | ~73 | 4 | 40% | **A** |
| 정화 6, 도망 6, 산소 25% | ~58 | 6 | 25% | **B** |
| 정화 5, 도망 8, 산소 15% | ~42 | 8 | 15% | **C** |
| 정화 4, 도망 10, 산소 8% | ~34 | 10 | 8% | **D** (산소 10% 미만) |

---

## 8. 챕터별 스냅샷 & 그래프

### 8.1 ChapterSnapshot 데이터

```csharp
struct ChapterSnapshot
{
    int chapterIndex;           // 1 ~ 3
    int purifiedMonsters;
    int escapeCount;
    float finalOxygenPercent;   // 클리어 순간
    float playTimeSeconds;      // 해당 공장 구간
    int score;                  // 0 ~ 100
    SessionGrade grade;         // S, A, B, C, D
}
```

### 8.2 챕터별 현재 진행 추적

공장 클리어 전까지 **현재 챕터용 임시 카운터**를 둔다.

- 공장 진입 / 재시작 시: `currentChapterStats` 리셋
- 정화·도망·시간: `currentChapterStats` + `sessionTotal` + `clearRun`(해당 시) 동시 누적
- 공장 클리어 시: `currentChapterStats` → `chapterSnapshots[index]` 저장

### 8.3 막대 그래프 (상세보기)

**공장 3개 × 막대 1개** — 챕터별 **종합 점수(0~100)** 또는 **등급 색상**.

```
── 공장별 활동 ──

공장 1  ████████░░  82  A
공장 2  ██████░░░░  68  B
공장 3  █████████░  95  S
```

| 항목 | 설명 |
|------|------|
| **구현** | Unity UI `Image.fillAmount` 또는 Slider × 3 (차트 라이브러리 불필요) |
| **데이터** | `chapterSnapshots[0..2].score`, `.grade` |
| **색상** | S=금, A=은, B=동/팀 팔레트, C/D=회색 계열 등 |
| **라벨** | "공장 1" ~ "공장 3" |

**한눈에**: 2공장에서 막대가 짧으면 그 구간에서 어려웠음을 표현.

### 8.4 재시도 시 그래프 동작

| 상황 | 그래프 |
|------|--------|
| 공장 2 재시도 후 재클리어 | **공장 2 막대만** 갱신 |
| 공장 3 여러 번 시도 후 클리어 | **공장 3 막대** = 최종 클리어 시 스냅샷 |
| 공장 1·2는 이미 클리어 | 해당 막대 **유지** |

---

## 9. UISessionClear UI

### 9.1 2단 구조 (확정)

```
[메인 패널]  ← 최초 표시
    ↓ [상세보기]
[상세 패널]  ← 그래프 + 2열 + 아이템 전체
    ↓ [뒤로]
[메인 패널]
```

### 9.2 메인 패널 와이어

```
┌─────────────────────────────────┐
│    ★ 모든 공장 정화 완료 ★        │
│                                 │
│           ┌─────┐               │
│           │  S  │  ← 크게 (S~D) │
│           └─────┘               │
│        완벽한 수호자              │
│   도망 2 · 산소 63%              │  ← clearRun 한 줄 (선택)
│                                 │
│      [ 상세보기 ]                │
│                                 │
│  ── 획득 아이템 (간략) ──         │  ← 1~3개 또는 생략
│  [○] [○] [○]                    │
│                                 │
│   [ 처음부터 ]    [ 확인 ]        │
└─────────────────────────────────┘
```

| 요소 | 데이터 |
|------|--------|
| 큰 등급 | `clearRun` → S~D |
| 타이틀 | `clearRun` 기준 |
| 한 줄 요약 | `clearRun` (도망, 산소) |
| 아이템 | 세션 누적 (간략) |

### 9.3 상세 패널 와이어

```
┌──────────────────────────────────────────────┐
│  [← 뒤로]              상세 결과               │
│                                              │
│  ── 공장별 활동 ──                             │
│  공장 1  ████████░░  82  A                   │
│  공장 2  ██████░░░░  68  B                   │
│  공장 3  █████████░  95  S                   │
│                                              │
│  ┌──────────────────┬──────────────────┐    │
│  │  최종 클리어       │  이번 여정         │    │
│  │  (clearRun)       │  (sessionTotal)   │    │
│  │  [S] 95점         │  공장  ● ● ●      │    │
│  │  정화  8마리      │  정화  18마리     │    │
│  │  도망  0회        │  도망  5회        │    │
│  │  산소  ████░ 63%  │  활동  58분       │    │
│  │                   │  위기  2회        │    │
│  └──────────────────┴──────────────────┘    │
│                                              │
│  ── 획득 아이템 (전체) ──                      │
│  [○] [○] [○] ...                             │
└──────────────────────────────────────────────┘
```

### 9.4 상세 2열 역할

| | 왼쪽 (최종 클리어) | 오른쪽 (이번 여정) |
|---|-------------------|-------------------|
| 데이터 | `clearRun` | `sessionTotal` |
| 등급·점수 | ✅ | ❌ |
| 산소 게이지 | ✅ | ❌ |
| 공장 ●●● | ❌ | ✅ |
| 위기(게임오버) | ❌ | ✅ |
| 플레이 시간 | ❌ | ✅ |

### 9.5 모바일

| 화면 | 좁은 화면 대응 |
|------|----------------|
| 메인 | 등급·버튼 중심, 세로 배치 |
| 상세 그래프 | 막대 3개 세로 나열 (가로 막대 유지) |
| 상세 2열 | **위·아래 2단** (왼쪽=위, 오른쪽=아래) |

### 9.6 Prefab 계층 (가칭)

```
UISessionClear
├─ MainPanel
│   ├─ TitleText
│   ├─ GradeBadgeLarge          ← S~D
│   ├─ StyleTitleText
│   ├─ SummaryLineText          ← clearRun 한 줄
│   ├─ DetailButton             → DetailPanel
│   ├─ ItemDisplayRowBrief      ← 간략 (선택)
│   └─ Buttons (Restart / Confirm)
│
└─ DetailPanel                  ← 기본 비활성
    ├─ BackButton               → MainPanel
    ├─ ChapterGraphRoot
    │   ├─ ChapterBarRow × 3    ← Image fill + Label + Grade
    │   └─ ...
    ├─ ColumnsRoot
    │   ├─ ClearRunColumn
    │   └─ JourneyColumn
    └─ ItemDisplayRowFull         ← UIResult 슬롯 재사용
```

### 9.7 UI 문구 톤 (권장)

| 데이터 | UI 표시 예 |
|--------|------------|
| 게임오버 횟수 | "위기 2회" |
| 도망 횟수 | "도망 5회" / "전략적 후퇴 5회" |
| 플레이 시간 | "활동 58분" |
| 공장별 그래프 | "공장 1" ~ "공장 3" |

---

## 10. 코드·시스템 구조

### 10.1 신규 파일

| 파일 | 역할 |
|------|------|
| `PlaySessionStats.cs` | sessionTotal, clearRun, chapterSnapshots, 리셋, 스냅샷 |
| `UISessionClear.cs` | Main/Detail 패널, 그래프·2열 바인딩 |
| `SessionGradeCalculator.cs` | 점수 0~100, S~D 매핑, 타이틀 |
| `ChapterBarGraphView.cs` | (선택) 막대 3개 UI 갱신 |

### 10.2 수정 파일

| 파일 | 변경 내용 |
|------|-----------|
| `GameManager.cs` / `UIManager.cs` | 클리어 분기, UISessionClear 호출 |
| `InventoryManager.cs` 또는 획득 훅 | 세션 아이템 획득 로그 |
| `ChapterManager.cs` | 공장 3 진입 시 `BeginClearRun()` |

### 10.3 통계 훅 연결표

| 이벤트 | 연결 위치 | 동작 |
|--------|-----------|------|
| 새 게임 / 처음부터 | `BeginNewPlaySession`, `LoadOpeningScene` | `ResetAll()` |
| 공장 진입 | `ChapterManager` | `BeginCurrentChapterStats()` |
| 공장 3 진입 | `ChapterManager` | `BeginClearRun()` |
| 공장 재시작 | `RestartCurrentChapter` | `BeginClearRun()` + `BeginCurrentChapterStats()` |
| 정화 성공 | `PollutionManager` 등 | 정화 +1 (ID 중복 방지) |
| 도망 | `BattleUIController` / `PlayerOxygen` | 도망 +1 |
| 아이템 획득 | 획득 팝업 / `InventoryManager` | 세션 획득 로그 |
| **공장 클리어** | `NotifyStageCleared` | **`SaveChapterSnapshot()`** + UI 분기 |
| 게임오버 | `PlayerOxygen` | sessionTotal 위기 +1, UI 없음 |

### 10.4 데이터 구조 (가칭)

```csharp
enum SessionGrade { S, A, B, C, D }

struct StatBlock
{
    int purifiedMonsters;
    int escapeCount;
    float escapePenaltyTotal;
    float playTimeSeconds;
    float finalOxygenPercent;
}

struct ChapterSnapshot
{
    int chapterIndex;
    int purifiedMonsters;
    int escapeCount;
    float finalOxygenPercent;
    float playTimeSeconds;
    int score;
    SessionGrade grade;
}

class PlaySessionStats
{
    StatBlock sessionTotal;
    StatBlock clearRun;
    StatBlock currentChapterStats;

    ChapterSnapshot[] chapterSnapshots;  // length 3

    HashSet<string> sessionPurifiedIds;
    HashSet<string> clearRunPurifiedIds;
    HashSet<string> currentChapterPurifiedIds;

    List<StackedInventoryItem> sessionAcquiredItems;
    int clearedFactoryCount;
    int gameOverCount;

    SessionGrade GetMainGrade() => GradeCalculator.FromStatBlock(clearRun);
}
```

---

## 11. 구현 체크리스트

### Phase 1 — 데이터

- [ ] `PlaySessionStats` (sessionTotal, clearRun, currentChapterStats)
- [ ] `ChapterSnapshot[3]` + `SaveChapterSnapshot()` on 공장 클리어
- [ ] `SessionGradeCalculator` (점수, S~D, 타이틀)
- [ ] 몬스터 ID 중복 정화 방지 (세션 / 챕터 / clearRun 각각)
- [ ] 세션 아이템 획득 로그
- [ ] [10.3 훅 연결표](#103-통계-훅-연결표) 연결

### Phase 2 — 분기

- [ ] `NotifyStageCleared` → 마지막 공장: `UISessionClear`, 아니면 `UIResult`
- [ ] 공장 1·2 클리어 시 스냅샷만 저장 (성과 UI 없음)
- [ ] `UIGameOver`에 성과 UI **추가하지 않음**

### Phase 3 — UI (메인)

- [ ] `UISessionClear` MainPanel — **S~D 큰 등급** + 타이틀
- [ ] [상세보기] / [처음부터] / [확인] 버튼
- [ ] (선택) 아이템 간략 표시

### Phase 4 — UI (상세)

- [ ] DetailPanel — **챕터별 막대 그래프 × 3**
- [ ] 좌우 2열 (clearRun / sessionTotal)
- [ ] 아이템 전체 (`UIResult` 슬롯 재사용)
- [ ] [뒤로] → MainPanel
- [ ] (선택) 좁은 화면 2단 레이아웃

### Phase 5 — 테스트

- [ ] 공장 1·2 → UIResult만, 스냅샷 내부 저장
- [ ] 공장 2 재시도 → sessionTotal 유지, 재클리어 시 공장 2 막대만 갱신
- [ ] 공장 3 진입 → clearRun 리셋
- [ ] 공장 3 클리어 → 메인 S~D + 상세보기 동작
- [ ] 게임오버 → UIGameOver만
- [ ] 처음부터 → 통계·스냅샷 전부 0
- [ ] F 등급 UI 없음, 최하 D

### Phase 6 — 2차 (추후)

- [ ] 전투(B) 지표
- [ ] 상세 2열 하단 + (선택) 그래프 보조 지표

---

## 12. 확정 사항 총정리

| # | 항목 | 결정 |
|---|------|------|
| 1 | 집계 범위 | **세션 전체** |
| 2 | 집계 방식 | **sessionTotal + clearRun** + **ChapterSnapshot[3]** |
| 3 | Run 리셋 | **옵션 B** (공장 3 진입 + 재시작) |
| 4 | 1차 지표 | **A + C** |
| 5 | 2차 지표 | **B (전투)** — 추후 |
| 6 | 성과 UI 시점 | **마지막 공장(챕터 3) 몬스터 전멸** |
| 7 | 공장 1·2 UI | **UIResult 유지** |
| 8 | 게임오버 UI | **성과 없음** |
| 9 | 클리어 표현 | **UI 패널** (별도 씬 없음) |
| 10 | **메인 UI** | **S~D 등급 한 장** + 타이틀 + [상세보기] |
| 11 | **상세 UI** | **챕터 막대 그래프** + **2열** + 아이템 |
| 12 | **등급 범위** | **S ~ D** (F 미사용) |
| 13 | 메인 등급 기준 | **clearRun** (공장 3 run) |
| 14 | 챕터 그래프 | **ChapterSnapshot** 점수/등급 |
| 15 | 재시도 | sessionTotal 유지, **해당 챕터 스냅샷만 갱신** |
| 16 | 처음부터 | **전부 리셋** |

---

## 13. 자주 헷갈리는 것

| 질문 | 답 |
|------|-----|
| F 등급은? | ❌ **사용 안 함**. 클리어 UI 최하 = **D** |
| 메인에 2열이 바로 보이나? | ❌ **메인=S~D만**, 2열은 **[상세보기]** 안 |
| 챕터 그래프 데이터는? | ✅ **ChapterSnapshot** (공장 클리어 시 저장) |
| 공장 2 재시도하면? | sessionTotal 유지, **공장 2 막대만** 재클리어 시 갱신 |
| 메인 S와 공장 3 막대 S가 다를 수 있나? | ✅ 같을 수 있음 (둘 다 clearRun / snapshot[2] 기준) |
| sessionTotal로 등급 매기나? | ❌ **clearRun**만 |
| S는 도망 0이어야 하나? | ❌ **3회 이하** + 점수 85+ + 산소 50%+ |
| 칭호(완벽한 수호자 등)는? | ✅ **등급과 1:1** (§7.3) |
| Phase 0 「3번」? | 전투(B) **언제 넣을지** (클리어 조건 아님) |

---

## 변경 이력

| 날짜 | 내용 |
|------|------|
| 2026-06-09 | 초안 — 세션 성과·클리어 UI 설계 |
| 2026-06-09 | **S~D 등급별 기준·칭호 1:1**, 도망 패널티 −5, S=도망 3회 이하 |
| 2026-06-09 | [WorkPackages.md](./SessionStats-And-ClearUI-WorkPackages.md) — AI/팀 작업 분할 문서 추가 |
