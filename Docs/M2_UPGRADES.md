# M2 — 성장 시스템 설치와 검증

> 이 문서는 M2 당시 설치 기록이다. M3 구성에서는 코인/강화가 자동 복원되며, 일반 개발 검증은 Unity Play를 기준으로 한다. 아래 APK 검증/병합 조건은 당시 기록이며 매 기능의 필수 조건으로 적용하지 않는다. 최신 절차: `Docs/M3_AUTO_SAVE.md`.

## 기준과 Git 운영

- 기준: `codex/m1-cookie-combat`의 `1a7282d0f69684f584cbee098f5d7f026f094c4d` (`dev`). 사용자 로컬에서 저장한 M1 씬, 스크립트 `.meta`, Android Portrait 설정이 원격에 포함되어 있음을 확인했다.
- 해당 커밋에서 `develop`을 생성했다. M2 구현 브랜치는 `develop`에서 만든 `codex/m2-upgrade-system`이다.
- `main`은 안정 버전, `develop`은 검증 완료 기능 통합, `codex/*`는 개별 작업용이다. M2는 로컬 검증과 사용자 승인 후 PR로 통합한다. 자동 병합하지 않는다.
- Unity 6000.5.1f1, URP 2D, 패키지, Android 설정, 기존 씬과 기존 `.meta`는 수정하지 않았다.

## 구현 규칙

| 강화 | 최초 비용 | 구매 효과 | 상한 |
| --- | ---: | --- | --- |
| 공격력 | 10 | 피해량 +1 | `int.MaxValue` 또는 표현 가능한 비용 한계 |
| 공격 속도 | 15 | 직전 발사 속도 ×1.10 | 기본 최소 간격 0.1초, 최대 초당 10회 |
| 쿠키 생산 | 20 | 직전 생성 시도 속도 ×1.15 | 기본 최소 간격 0.2초, 최대 초당 5회 |

다음 비용은 `ceil(최초 비용 × 1.15^해당 강화 구매 횟수)`다. 강화별 레벨과 비용은 독립적이다. `int.MaxValue`를 초과하는 비용은 `COST LIMIT`으로 표시하고 구매를 막는다. 값이 음수로 넘치거나 싼 값으로 잘리는 방식은 사용하지 않는다.

속도는 매 구매마다 곱셈으로 증가하며, 마지막 구매는 상한까지만 증가한다. 상한 도달 후에는 `MAX`와 비활성 버튼을 표시하며 코인을 차감하지 않는다. 기존 M1 기본값(피해량 1, 발사 간격 0.4초, 생성 간격 0.9초)에서는 공격 속도 15회, 생산 11회 구매로 상한에 도달한다. 상한은 `UpgradeController` Inspector에서 조절할 수 있는 임시 밸런스 값이다. 기존에 더 빠르게 설정한 M1 수치는 강제로 느리게 만들지 않는다.

생산 속도는 **생성 시도 빈도**다. M1의 활성 쿠키 최대 24개, 간격 검사와 12회 위치 탐색 제한을 유지하므로 실제 생성량은 공간 및 개수 제한에 따라 적을 수 있다. 투사체 최대 24개 제한도 유지한다.

구매 성공 후 피해량과 간격을 적용하고, 잔액/강화 UI를 이벤트로 갱신한다. 진행 중인 발사·생성 대기 시간도 새 간격에 비례해 줄인다. 이미 발사된 투사체는 발사 당시 피해량을 유지하고 다음 투사체부터 새 피해량을 사용한다. 저장/오프라인 성장은 이번 범위에 없으며 기본 설정에서는 Play를 다시 시작하면 코인과 강화 레벨이 초기화된다.

## 컴포넌트와 변경 파일

- `Assets/Scripts/Upgrades/UpgradeState.cs`: Unity에 의존하지 않는 비용·레벨·구매·상한 규칙. 기존 `CoinBalance`를 공유한다.
- `Assets/Scripts/Upgrades/UpgradeController.cs`: 기존 `CoinWallet`, `AutomaticTurret`, `CookieSpawner`와 규칙을 연결한다.
- `Assets/Scripts/Upgrades/UpgradePanel.cs`: 세 버튼, 레벨/현재 성능/다음 비용, 활성 상태와 이벤트 구독을 관리한다.
- `Assets/Editor/M2UpgradeSetup.cs`: 기존 씬에 M2 오브젝트와 참조를 추가하는 메뉴. 중복 설치 및 잘못된 기존 EventSystem은 변경 전에 거부한다.
- `Assets/Scripts/Combat/AutomaticTurret.cs`, `CookieSpawner.cs`: 기존 직렬화 필드를 유지하면서 간격 변경 API와 대기 시간 조정을 추가했다.
- `Assets/Scripts/Combat/CoinWallet.cs`: 구매 효과를 적용한 뒤 잔액 변경 이벤트를 보낼 수 있게 내부 연결 지점을 추가했다.
- `Tests/UpgradeStateChecks.cs`, `Tests/verify_m1.py`: M1 회귀 검사와 M2 구매/가격/상한/오버플로 검사 및 C# 독립 컴파일.
- `GAME_DESIGN.md`, `TASKS.md`, 이 문서: 설계, 검증 상태와 로컬 작업 절차.

## 로컬 Unity에서 수행할 작업

1. 현재 로컬 작업이 있다면 먼저 별도 커밋/백업으로 보호한다. 작업 트리가 깨끗한 상태에서 `git fetch origin` 후 `codex/m2-upgrade-system`을 체크아웃한다. 다른 브랜치의 수정 씬을 덮어쓰거나 강제 리셋하지 않는다.
2. Unity Hub에서 **6000.5.1f1**로 프로젝트를 열고 스크립트/패키지 import가 끝날 때까지 기다린다. 새 C# 파일과 폴더의 `.meta`는 이때 Unity가 생성한다. 클라우드에서는 임의 GUID나 `.meta`를 만들지 않았다.
3. Console 컴파일 오류가 없는지 확인한다. `Assets/Scenes/CookieBreakerMain.unity`를 열고 활성 씬으로 지정한다. Edit Mode에서 기존 `Cookie Breaker M1`, 포탑, 상단 코인 UI가 있는지 확인한다.
4. **`Cookie Breaker > M2 > Add Upgrades to Open Main Scene`**을 한 번 실행한다. M1 설치 및 메인 씬 생성 메뉴는 다시 실행하지 않는다.
5. 추가된 `Cookie Breaker M2` 아래 `UpgradeController`에 기존 Wallet/Turret/Spawner가 연결되어 있고, `Upgrade Canvas > Safe Area`의 `UpgradePanel`에 세 Button/Text가 연결되어 있는지 확인한다. 기존 오브젝트는 수정하지 않고 별도 루트만 추가한다. 실행 직후 Undo로 추가 내용을 취소할 수 있으며 자동 저장하지 않는다.
6. EventSystem이 없다면 도구가 `InputSystemUIInputModule`을 가진 EventSystem을 추가한다. 기존 `Assets/InputSystem_Actions.inputactions`의 **import된 UI 액션 참조**를 사용하며 새 입력 에셋을 만들지 않는다. 이 에셋에는 마우스 클릭과 `<Touchscreen>/touch*/press`, 위치 입력이 있다. 기존 EventSystem이 있다면 활성 모듈 한 개가 InputSystem 방식인지, Point/Left Click이 올바르게 연결되어 있는지 확인한다. 충돌을 보고한 경우 기존 입력 구성을 직접 확인하고 다시 실행한다.
7. 씬을 저장한다. **저장 후 씬을 닫았다 다시 열어** 참조와 버튼이 유지되는지 확인하고 아래 PC 검증을 수행한다. Safe Area를 적용한 하단 3개 버튼이 기존 전투 영역과 겹치지 않는지 확인한다.
8. Android Build Support/SDK/NDK/OpenJDK가 설치된 로컬에서 Android Build Profile을 활성화한다. 기존 Product Name `Cookie Breaker`, ID `com.mystudio.cookiebreaker`, Portrait, IL2CPP/ARM64를 확인한다.
9. 저장된 메인 씬이 빌드 대상에 포함된 상태에서 기존 **`Cookie Breaker > Build Android Test APK`** 메뉴로 APK를 만든다. 출력은 `Builds/Android/CookieBreaker.apk`다. 기존 빌드 도구와 Android Build Profile은 그대로 사용한다.
10. Galaxy S22 Ultra에 설치해 터치·Safe Area·구매 반영을 검증한다. 로컬에서 생성한 새 `.meta`와 저장한 씬을 **M2 작업 브랜치**에 커밋/푸시하고 결과를 PR에 남긴다. APK, Library, Temp는 커밋하지 않는다. 사용자 승인 전 `develop`/`main`으로 병합하지 않는다.

## PC Play 및 Android 검증표

### 설치 중 입력 참조 중복 오류

`RequireAction`에서 `Sequence contains more than one matching element`가 발생했다면 수정된 M2 브랜치를 먼저 받아 스크립트 컴파일을 기다린 뒤 같은 설치 메뉴를 다시 실행한다. Input System 1.19.0은 액션마다 일반 참조와 이전 파일 ID 호환용 숨겨진 참조를 함께 import한다. 설치 도구는 Unity의 로더와 동일하게 `HideFlags.HideInHierarchy` 참조를 제외한다. 숨겨지지 않은 참조가 실제로 누락되거나 여러 개인 경우에는 해당 `UI/액션명`과 개수를 명시하고 중단한다.

보고된 오류는 씬 오브젝트 생성 전 사전 검사에서 발생하므로 이 오류만으로 M2 루트가 부분 생성되지는 않는다. 입력 에셋, 기존 씬, `.meta` 또는 Library를 삭제할 필요가 없다. 수정 후 메뉴 실행과 씬 저장·재열기 검증은 로컬 Unity에서 수행한다.

### 실행 확인

- [ ] 기존 쿠키 생성/자동 탐색/공격/파괴/1코인 지급이 지속된다.
- [ ] 초기 세 버튼은 10/15/20 비용, Lv 0을 표시하고 코인이 부족하면 비활성이다.
- [ ] 코인이 비용과 정확히 같으면 구매되고 0코인이 된다. 연타해도 잔액이 음수가 되지 않는다.
- [ ] 공격력 구매로 DMG가 +1, 비용은 10 → 12 → 14로 증가한다. 새 투사체의 적중 횟수가 감소한다.
- [ ] 공격 속도 구매로 현재 발사 속도가 ×1.10, 생산 구매로 시도 속도가 ×1.15가 된다. 세 강화의 레벨/비용은 독립적이다.
- [ ] 코인 획득 및 소비 직후 상단 잔액, 구매 가능 상태, 레벨/성능/비용이 갱신된다.
- [ ] 공격 속도/생산 상한 후 MAX로 바뀌고 더 구매할 수 없다. 쿠키가 개수/공간 제한을 초과하지 않는다.
- [ ] 상한 검사를 빠르게 하려면 Edit Mode에서 M1 Wallet의 Starting Coins를 임시로 10000으로 설정한 뒤 Play한다. 검사 후 **0으로 되돌리고 저장**한다.
- [ ] 기본 9:16 및 S22 Ultra의 긴 세로 화면에서 버튼이 잘리지 않고 타이틀/코인/전투가 보인다. 디스플레이 컷아웃과 제스처 탐색 영역도 확인한다.
- [ ] 씬 재열기, Play 종료/재시작, APK 새 실행에서도 버튼 입력과 참조가 정상이다.
- [ ] 3~5분 실행, 빠른 반복 탭, 세 강화 번갈아 구매 중 Console/Android 로그에 오류가 없다.

## 클라우드 검증과 한계

실행 명령:

```bash
python3 Tests/verify_m1.py --unity-data /workspace/unity-setup/6000.5.1f1/Editor/Data
```

이 스크립트는 Unity를 실행하지 않는다. 설치된 Unity 및 패키지 DLL을 참조해 런타임/Editor C#를 컴파일하고, 순수 C# 상태 검사를 .NET에서 실행한다. 패키지 import나 Unity 자체 스크립트 컴파일 파이프라인의 성공과 같지는 않다.

- 통과: 런타임/Editor C# 독립 컴파일(오류·경고 없음), 정확한 비용 차감과 부족한 코인 거부, 반복 구매, 독립 레벨/비용, 수치 증가, 속도 상한, 비용/피해량 경계와 잘못된 입력, 기존 M1 체력/보상 규칙. `git diff --check`도 통과했다.
- 파일 검증: 기준 커밋의 모든 추적 파일 해시를 비교해 기존 씬/`.meta`/에셋/ProjectSettings/Packages가 변경되지 않았음을 확인했다.
- 로컬 확인 필요: Unity import와 Console, Editor 메뉴 실행 및 Undo, 생성된 씬/`.meta`, 액션 참조의 저장/재열기, 물리 전투와 실제 간격, 화면/Safe Area, 터치, APK 빌드와 실기기 실행.

M1의 PC/Android 성공은 사용자가 확인한 기준이다. 클라우드에서 M2 Unity Editor 실행, 씬 실행 또는 APK 빌드에 성공했다고 주장하지 않는다.

## develop 병합 조건

위 PC/Android 검증 완료, 새 Unity `.meta` 및 수정 씬의 작업 브랜치 반영, 임시 Starting Coins 원복, PR 확인, 사용자 명시적 승인 후 병합한다. 해당 변경을 포함한 최종 커밋으로 테스트했는지 확인한다.
