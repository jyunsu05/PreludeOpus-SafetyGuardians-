# 사용 에셋 및 라이선스 (Safety Guardians)

> **프로젝트**: Prelude Opus — Safety Guardians (세이프티 가디언스)  
> **Unity**: 6000.4.8f1 · 2D · URP  
> **빌드**: Windows (.exe) — 강사 FTP 제출  
> **최종 갱신**: 2026-06-16  
> **용도**: 중간 팀프로젝트 제출 — 에셋 출처·라이선스 명시 (평가표 P5)

---

## 문서 배치

| 위치 | 경로 |
|------|------|
| Unity 프로젝트 | `Docs/ASSETS-CREDITS.md` (본 파일) |
| Windows 빌드 폴더 | `SafetyGuardians.exe`와 **같은 폴더**의 `ASSETS-CREDITS.md` |
| GitHub | `README.md`에서 본 문서 링크 |
| 제출 자료 | 기획서·발표 자료와 함께 동일 내용 포함 |

---

## 1. 요약

| 구분 | 출처 | 라이선스 | 비고 |
|------|------|----------|------|
| **엔진·패키지** | Unity Technologies | [Unity Terms](https://unity.com/legal/terms-of-service) | Unity 6.4, URP, Input System 등 |
| **UI 폰트 (메인)** | [Poppy Works — Silver](https://poppyworks.itch.io/silver) | **CC BY 4.0** | TMP SDF 변환 후 사용 |
| **UI 폰트 (보조)** | Unity TextMesh Pro 번들 | **SIL OFL 1.1** | Liberation Sans |
| **2D 그래픽** | OpenAI ChatGPT (DALL·E) + 팀 Photoshop 편집 | OpenAI [Terms of Use](https://openai.com/policies/terms-of-use) | 맵·캐릭터·몬스터·UI·오프닝 BG |
| **시야 제한 연출** | Prelude Opus 팀 자체 구현 | 교육용 팀 프로젝트 | 플레이어 중심 Fog of War (BlackFog) |
| **오디오** | Prelude Opus 팀 제작·편집·배치 | 교육용 팀 프로젝트 | AI 보조 생성 + 팀 믹싱·배치 |
| **코드** | Prelude Opus 팀 자체 작성 | 교육용 팀 프로젝트 | Cursor AI 보조 사용 |

**Silver (CC BY 4.0) 표기:**  
`Font: Silver by Poppy Works — https://poppyworks.itch.io/silver (CC BY 4.0)`

---

## 2. Unity 엔진 및 패키지

| 에셋 | 버전(대표) | 출처 | 라이선스 |
|------|-----------|------|----------|
| Unity Editor | 6000.4.8f1 | Unity Technologies | Unity Subscription / EULA |
| Universal RP | 17.4.0 | Unity Technologies | Unity EULA |
| Input System | 1.19.0 | Unity Technologies | Unity EULA |
| 2D Tilemap / Animation / AI Navigation | manifest.json 참조 | Unity Technologies | Unity EUGA |
| TextMesh Pro | uGUI 2.0 번들 | Unity Technologies | Unity EULA + 포함 폰트 별도 |

> Unity 프로젝트 내 패키지 전체 목록: `Packages/manifest.json`  
> Windows 빌드(.exe)에는 위 패키지가 런타임 형태로 포함됩니다.

---

## 3. 폰트

### 3-1. Silver (메인 UI 폰트)

| 항목 | 내용 |
|------|------|
| **Unity 프로젝트 경로** | `Assets/1.Yunseo/FontFile/Silver.ttf`, `Silver SDF.asset` |
| **다운로드** | [https://poppyworks.itch.io/silver](https://poppyworks.itch.io/silver) |
| **제작** | Poppy Works (Wolfgang Wozniak) |
| **라이선스** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| **조건** | 저작자 표기(Attribution) 필수 |

**사용 위치:** 전투 UI, 게임오버, 로딩, 결과/클리어, 인벤토리, 오프닝, HUD, 아이템 라벨, 토스트 등

### 3-2. Liberation Sans (보조·TMP 기본 폰트)

| 항목 | 내용 |
|------|------|
| **Unity 프로젝트 경로** | `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` |
| **라이선스 문서** | `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt` |
| **라이선스** | SIL Open Font License 1.1 |

---

## 4. 2D 그래픽 (스프라이트·UI)

| 에셋 종류 | 1차 생성 | 2차 가공 | 비고 |
|-----------|----------|----------|------|
| 공장맵 배경 (Chapter 1~3) | OpenAI ChatGPT (DALL·E) | Prelude Opus 팀 — Adobe Photoshop | 타일 분할·콜라이더 맵핑 |
| 플레이어 스프라이트·Walk/Idle | 동일 | 동일 | 4방향 이동 애니메이션 |
| 몬스터 (M-001~M-003) | 동일 | 동일 | Idle/Move 애니메이션 |
| UI 아이콘·게이지·버튼 | 동일 | 동일 | — |
| 오프닝 배경 | 동일 | 동일 | — |
| 타일맵 충돌용 Square 등 | Unity 기본 / 팀 제작 | — | — |

그래픽은 ChatGPT(DALL·E)로 초안을 생성한 뒤, 팀이 Photoshop으로 게임 해상도·색·투명도·맵 분할에 맞게 2차 편집했습니다.

**Unity 프로젝트 폴더:** `Assets/2.SLA/Sprites/`, `Assets/1.Yunseo/`, `Assets/3.ChangHEE/`

---

## 5. 시야 제한 (Fog of War)

| 항목 | 내용 |
|------|------|
| **구현** | Prelude Opus 팀 자체 제작 (외부 에셋 미사용) |
| **Unity 프로젝트** | `Assets/2.SLA/Prefabs/BlackFog.prefab`, `Assets/2.SLA/Scripts/DynamicFog.cs` |
| **동작** | 플레이어를 중심으로 주변만 밝게 보이도록 마스크 스프라이트·스케일 연출 |
| **라이선스** | 교육용 팀 프로젝트 |

공장 탐색 시 플레이어 주변 시야만 확보되는 Fog of War는 본 프로젝트의 핵심 필드 연출입니다.

---

## 6. 오디오 (BGM·SFX)

| 구분 | Unity 프로젝트 경로 | 출처 | 라이선스 |
|------|-------------------|------|----------|
| BGM (메뉴·오프닝·공장·전투) | `Assets/1.Yunseo/sound/BGM/` | Prelude Opus 팀 — AI 보조 생성 후 편집·믹싱 | 교육용 팀 프로젝트 |
| 효과음 (UI·플레이어·몬스터·공장) | `Assets/1.Yunseo/sound/Fx/` | Prelude Opus 팀 — 제작·편집·배치 | 교육용 팀 프로젝트 |

BGM·SFX는 팀이 직접 선곡·생성·편집하여 Unity에 배치했으며, Windows 빌드에 포함되어 재생됩니다.

---

## 7. 코드

| 항목 | 내용 |
|------|------|
| **작성** | Prelude Opus 팀 |
| **Unity 프로젝트 폴더** | `Assets/1.Yunseo/`, `Assets/2.SLA/`, `Assets/3.ChangHEE/`, `Assets/CommonScript/` |
| **AI 보조** | Cursor AI — 초안·리팩터·에디터 스크립트 등 |
| **저장소** | GitHub: PreludeOpus-SafetyGuardians |
| **라이선스** | 교육용 팀 프로젝트 |

---

## 8. 저작권 표기 (Attribution)

본 문서 및 README에 아래 표기를 포함하여 CC BY 4.0(Silver) 조건을 충족합니다.

```
Font: Silver by Poppy Works
https://poppyworks.itch.io/silver
Licensed under CC BY 4.0

Graphics: AI-generated (ChatGPT/DALL·E) + edited by Prelude Opus team (Photoshop)
Audio: Produced and edited by Prelude Opus team (educational project)
Code: Prelude Opus team (with Cursor AI assistance)
Fog of War: Original implementation by Prelude Opus team
```

---

## 9. 관련 링크

| 링크 | 용도 |
|------|------|
| [Poppy Works — Silver](https://poppyworks.itch.io/silver) | 폰트 다운로드·CC BY 4.0 |
| [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | Silver 라이선스 |
| [SIL Open Font License 1.1](https://scripts.sil.org/OFL) | Liberation Sans |
| [OpenAI Terms of Use](https://openai.com/policies/terms-of-use) | AI 생성 그래픽 |
| [Unity Legal](https://unity.com/legal) | 엔진·패키지 |

---

*Prelude Opus · Safety Guardians · 중간 팀프로젝트 제출용 · 2026-06-16*
