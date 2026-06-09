# 세션 성과 & 클리어 UI — 작업 분할 & AI 공유용

> **이 문서를 다른 AI / 팀원에게 먼저 보여주세요.**  
> 천천히 할 때 **한 번에 하나의 WP(Work Package)** 만 맡기면 됩니다.

| 문서 | 역할 |
|------|------|
| **[SessionStats-And-ClearUI-Spec.md](./SessionStats-And-ClearUI-Spec.md)** | 설계 **전체** (WHAT/WHY) — 규칙·UI·등급·데이터 |
| **이 문서 (WorkPackages)** | 구현 **분할** (HOW/WHEN) — 순서·범위·AI 프롬프트 |

**프로젝트**: Unity — `PreludeOpus-SafetyGuardians`  
**최종 갱신**: 2026-06-09

---

## 0. 다른 AI에게 넘길 때 (복붙용)

아래 블록 전체를 새 채팅에 붙여넣고, `[WP 번호]`만 바꿔 주세요.

```
프로젝트: PreludeOpus-SafetyGuardians (Unity C#)

아래 두 문서를 기준으로 작업해 주세요. 문서 내용과 충돌하면 문서가 우선입니다.
- Docs/SessionStats-And-ClearUI-Spec.md
- Docs/SessionStats-And-ClearUI-WorkPackages.md

이번에 할 작업: [WP-1] PlaySessionStats 코어
(원하는 WP 번호로 변경)

규칙:
- 명시된 WP 범위만 수정. 다른 WP 파일은 건드리지 마세요.
- 공장 1·2는 UIResult 유지. 게임오버(UIGameOver)에 성과 UI 추가 금지.
- F 등급 없음. S~D만. 메인 등급은 clearRun(공장 3 run) 기준.
- 완료 후: 변경 파일 목록, 테스트 방법, 다음 WP 제안을 짧게 정리.

WP 완료 시 이 문서의 해당 체크박스를 [x]로 바꿔 주세요.
```

---

## 1. 한 줄 요약 (AI가 먼저 읽을 것)

| 항목 | 내용 |
|------|------|
| **만드는 것** | 플레이 세션 통계 + 마지막 공장(챕터 3) 클리어 UI |
| **UI 이름** | `UISessionClear` (Canvas 패널, 별도 씬 없음) |
| **언제 표시** | 챕터 3 몬스터 전부 정화 시만 |
| **메인 화면** | S~D 등급 + 칭호 + [상세보기] |
| **상세 화면** | 공장별 막대 그래프 3개 + 2열 통계 + 아이템 |
| **안 보여줌** | 게임오버, 공장 1·2 클리어(UIResult) |

---

## 2. 작업 패키지 맵 (의존 관계)

```
WP-0  문서 숙지 (코드 없음)
  │
  ▼
WP-1  PlaySessionStats + SessionGradeCalculator     ← 데이터 기반 (필수 선행)
  │
  ├─► WP-2  이벤트 훅 연결 (정화·도망·게임오버·리셋)
  │
  ├─► WP-3  클리어 분기 (UIResult vs UISessionClear)
  │
  ├─► WP-4  UISessionClear — 메인 패널 (S~D)
  │
  └─► WP-5  UISessionClear — 상세 패널 (그래프 + 2열)
        │
        ▼
WP-6  통합 테스트 & 폴리시
  │
  ▼
WP-7  (선택·2차) 전투(B) 지표
```

**천천히 할 추천 순서**: WP-0 → 1 → 2 → 3 → 4 → 5 → 6  
WP-4와 WP-5는 WP-3 이후 **UI만** 나눠서 해도 됩니다.

---

## 3. WP별 상세

상태: `[ ]` 미착수 · `[~]` 진행 중 · `[x]` 완료

---

### WP-0 — 문서 숙지 (코드 없음)

| | |
|---|---|
| **목표** | Spec 이해, 범위 합의 |
| **산출물** | 없음 |
| **시간** | 15~30분 |

**체크**
- [ ] Spec §12 확정 표 읽음
- [ ] UIResult / UIGameOver / UISessionClear 역할 구분
- [ ] sessionTotal vs clearRun vs ChapterSnapshot 구분

---

### WP-1 — PlaySessionStats + SessionGradeCalculator

| | |
|---|---|
| **목표** | 통계·등급 **데이터 레이어만** (UI 없음) |
| **선행** | WP-0 |
| **신규 파일** | `Assets/CommonScript/PlaySessionStats.cs`, `Assets/CommonScript/SessionGradeCalculator.cs` |
| **수정** | 없음 (훅은 WP-2) |

**구현 범위**
- `StatBlock`, `ChapterSnapshot`, `SessionGrade` (S~D)
- `sessionTotal`, `clearRun`, `currentChapterStats`, `chapterSnapshots[3]`
- `ResetAll()`, `BeginClearRun()`, `BeginCurrentChapterStats()`
- `SaveChapterSnapshot(chapterIndex)` — 클리어 시 호출 예정
- 몬스터 ID 중복 정화 방지 (HashSet 3종)
- `SessionGradeCalculator`: 점수 공식 + §7.1 등급表 + §7.3 칭호 1:1
- 싱글톤 또는 `DontDestroyOnLoad` — 기존 Manager 패턴 따르기

**하지 말 것**
- UI Prefab 생성
- GameManager 분기
- InventoryManager 대규모 수정

**완료 기준**
- [ ] 에디터/단위 테스트 또는 임시 Debug.Log로 Reset → 누적 → 등급 계산 검증
- [ ] Spec §10.4 데이터 구조와 일치

**AI 프롬프트 한 줄**: `WP-1만: PlaySessionStats와 SessionGradeCalculator를 Spec §4,6,7,10 기준으로 추가해줘. UI/훅 연결은 하지 마.`

**상태**: [ ]

---

### WP-2 — 이벤트 훅 연결

| | |
|---|---|
| **목표** | 게임플레이 이벤트 → `PlaySessionStats` 누적 |
| **선행** | WP-1 |
| **수정 파일** | `GameManager.cs`, `ChapterManager.cs`, `PlayerOxygen.cs`, `BattleUIController.cs`, `PollutionManager.cs` 또는 `MonsterBattleTracker.cs`, `InventoryManager.cs` 또는 `UIManager.cs` |

**구현 범위** (Spec §10.3 표)

| 이벤트 | 동작 |
|--------|------|
| `BeginNewPlaySession`, `LoadOpeningScene` | `ResetAll()` |
| 공장 진입 | `BeginCurrentChapterStats()` |
| 공장 3 진입 | `BeginClearRun()` |
| `RestartCurrentChapter` | `BeginClearRun()` + `BeginCurrentChapterStats()` |
| 정화 성공 | 정화 +1 (ID 중복 방지) |
| 도망 | 도망 +1 |
| 아이템 획득 | `sessionAcquiredItems` 로그 |
| 게임오버 | `gameOverCount++` (UI 없음) |
| 공장 클리어 | `SaveChapterSnapshot()` — **UI 분기는 WP-3** |

**하지 말 것**
- UISessionClear Prefab
- UIResult 동작 변경

**완료 기준**
- [ ] 플레이 중 Console/Inspector로 sessionTotal 증가 확인
- [ ] 공장 3 진입 시 clearRun만 리셋 확인
- [ ] 재시작 시 sessionTotal 유지 확인

**AI 프롬프트 한 줄**: `WP-2만: PlaySessionStats 훅을 Spec §10.3, §4.3에 맞게 기존 클래스에 연결해줘. UI 분기는 WP-3이니 SaveChapterSnapshot까지만.`

**상태**: [ ]

---

### WP-3 — 클리어 UI 분기

| | |
|---|---|
| **목표** | 챕터 3 → `UISessionClear`, 챕터 1·2 → `UIResult` |
| **선행** | WP-1, WP-2 |
| **수정** | `GameManager.cs`, `UIManager.cs` |
| **신규** | `UISessionClear.cs` **스텁** (ShowMain/Close 빈 껍데기 OK) |

**구현 범위**
- `NotifyStageCleared` / `ConsumeStageClearPending` 흐름에 분기 (Spec §5)
- `CurrentChapterIndex == ChapterCount` → `UISessionClear.ShowMain()`
- 그 외 → 기존 `UIResult.ShowStageClearResult()`
- `UIGameOver` **수정하지 않음**

**하지 말 것**
- 메인/상세 UI 디자인 (WP-4,5)
- 등급 계산 로직 변경 (WP-1)

**완료 기준**
- [ ] 공장 1·2 클리어 → UIResult만
- [ ] 공장 3 클리어 → UISessionClear 호출 (빈 패널이라도)
- [ ] 게임오버 → 성과 UI 없음

**AI 프롬프트 한 줄**: `WP-3만: Spec §5 분기를 UIManager/GameManager에 넣고 UISessionClear 스텁을 연결해줘. UI 레이아웃은 WP-4.`

**상태**: [ ]

---

### WP-4 — UISessionClear 메인 패널

| | |
|---|---|
| **목표** | S~D 큰 등급 + 칭호 + [상세보기] + 버튼 |
| **선행** | WP-3 |
| **신규/수정** | `UISessionClear.cs`, Canvas Prefab (MainPanel) |

**구현 범위** (Spec §9.2)
- `GradeBadgeLarge`, `StyleTitleText`, `SummaryLineText` (clearRun)
- `DetailButton` → DetailPanel 활성화 (WP-5에서 채움)
- `[처음부터]` → `LoadOpeningScene` + `ResetAll()`
- `[확인]` → Spec에 맞게 (오프닝/타이틀 — 기존 패턴 따름)
- (선택) 아이템 간략 1~3개

**하지 말 것**
- 상세 그래프·2열 (WP-5)
- PlaySessionStats 로직 변경

**완료 기준**
- [ ] 공장 3 클리어 시 등급·칭호가 clearRun 기준으로 표시
- [ ] 상세보기 버튼으로 DetailPanel 전환 (내용 비어 있어도 OK)

**AI 프롬프트 한 줄**: `WP-4만: UISessionClear MainPanel을 Spec §9.2, §7.3대로 만들고 PlaySessionStats에 바인딩해줘. DetailPanel 내용은 WP-5.`

**상태**: [ ]

---

### WP-5 — UISessionClear 상세 패널

| | |
|---|---|
| **목표** | 공장별 막대 그래프 + 2열 + 아이템 전체 |
| **선행** | WP-4 |
| **신규/수정** | `UISessionClear.cs`, `ChapterBarGraphView.cs`(선택), DetailPanel Prefab |

**구현 범위** (Spec §8.3, §9.3, §9.4)
- 막대 3개: `chapterSnapshots[0..2]` score/grade
- 2열: clearRun(왼) / sessionTotal(오)
- `UIResult` ItemDisplaySlot 재사용
- `[뒤로]` → MainPanel
- (선택) 좁은 화면 위·아래 2단

**하지 말 것**
- 메인 등급 로직 변경
- 전투(B) 지표 (WP-7)

**완료 기준**
- [ ] 상세보기에서 그래프·2열·아이템 표시
- [ ] 공장 2 재클리어 시 공장 2 막대만 갱신 (WP-2+6 테스트)

**AI 프롬프트 한 줄**: `WP-5만: UISessionClear DetailPanel — 챕터 막대 그래프 3개 + 2열 + 아이템. Spec §8, §9.3 참고.`

**상태**: [ ]

---

### WP-6 — 통합 테스트 & 폴리시

| | |
|---|---|
| **목표** | Spec §11 Phase 5 테스트 전부 통과 |
| **선행** | WP-1 ~ WP-5 |

**테스트 시나리오** (Spec §11)

| # | 시나리오 | 기대 |
|---|----------|------|
| 1 | 공장 1·2 클리어 | UIResult만 |
| 2 | 공장 2 재시도 | sessionTotal 유지 |
| 3 | 공장 3 진입 | clearRun 리셋 |
| 4 | 공장 3 클리어 | 메인 S~D + 상세 동작 |
| 5 | 게임오버 | UIGameOver만 |
| 6 | 처음부터 | 통계 0 |
| 7 | 재정화 | 정화 수 +0 |

**완료 기준**
- [ ] 위 7항 수동 테스트 통과
- [ ] Spec §13 FAQ와 모순 없음

**AI 프롬프트 한 줄**: `WP-6: SessionStats 기능 통합 테스트하고 Spec §11 체크리스트 기준으로 버그 수정해줘.`

**상태**: [ ]

---

### WP-7 — (선택·2차) 전투(B) 지표

| | |
|---|---|
| **목표** | 전투 횟수, 평균 턴 등 — Spec §6.2 |
| **선행** | WP-6 |
| **범위** | PlaySessionStats 확장 + 상세 패널 1~2줄 추가 |

**하지 말 것**
- 메인 S~D 판정 변경 (1차 A+C 유지)

**상태**: [ ]

---

## 4. 파일 소유권 (충돌 방지)

| 파일 | 담당 WP |
|------|---------|
| `PlaySessionStats.cs` | WP-1, WP-2, WP-7 |
| `SessionGradeCalculator.cs` | WP-1 |
| `UISessionClear.cs` | WP-3(스텁), WP-4, WP-5 |
| `ChapterBarGraphView.cs` | WP-5 |
| `GameManager.cs`, `UIManager.cs` | WP-2, WP-3 |
| `ChapterManager.cs` | WP-2 |
| `PlayerOxygen.cs`, `BattleUIController.cs` | WP-2 |
| `PollutionManager` / `MonsterBattleTracker` | WP-2 |
| `InventoryManager` / `UIManager` (획득) | WP-2 |
| `UIResult.cs`, `UIGameOver.cs` | **건드리지 않음** (동작 유지) |

---

## 5. 진행 현황 보드 (팀/본인용)

| WP | 이름 | 상태 | 담당 | 메모 |
|----|------|------|------|------|
| 0 | 문서 숙지 | [ ] | | |
| 1 | Stats + Grade | [ ] | | |
| 2 | 훅 연결 | [ ] | | |
| 3 | 클리어 분기 | [ ] | | |
| 4 | UI 메인 | [ ] | | |
| 5 | UI 상세 | [ ] | | |
| 6 | 통합 테스트 | [ ] | | |
| 7 | 전투(B) 2차 | [ ] | | |

---

## 6. Spec 빠른 참조 (AI용)

### 등급 (clearRun / ChapterSnapshot 공통)

| 등급 | 점수 | 산소 | 도망 |
|------|------|------|------|
| S | 85+ | 50%+ | ≤3 |
| A | 70+ | 35%+ | ≤5 |
| B | 55+ | 20%+ | ≤7 |
| C | 40+ | 10%+ | 무제한 |
| D | 클리어 | — | — |

```
점수 = Clamp(정화×10 + 산소%×0.5 − 도망×5, 0, 100)
```

### 칭호

| S | A | B | C | D |
|---|---|---|---|---|
| 완벽한 수호자 | 베테랑 수호자 | 현장 수호자 | 간신히 해낸 수호자 | 버틴 수호자 |

### Run 리셋 (옵션 B)

- `clearRun` 리셋: **공장 3 진입**, **현재 공장 재시작**
- `sessionTotal` 리셋: **처음부터 시작**만

---

## 7. 변경 이력

| 날짜 | 내용 |
|------|------|
| 2026-06-09 | AI 공유·WP 분할 문서 최초 작성 |
