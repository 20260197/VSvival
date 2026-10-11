# M1 — 쿠키 생성 및 자동 포탑 전투

## 적용 기준과 검증 상태

Unity `6000.5.1f1`, URP 2D, Android Portrait를 유지한다. 기준 커밋은 사용자 실기기 검증 이후의 `b67649e`이며 M1 작업 브랜치는 `codex/m1-cookie-combat`이다. 사용자가 기존 제목 화면 APK의 Galaxy S22 Ultra 실행 성공을 보고했지만, **이번 M1 전투의 Play/Android 실행 성공을 의미하지는 않는다**.

기존 `CookieBreakerMain.unity`, 씬 GUID, 카메라, 렌더링 자산, Android Build Profile, ProjectSettings, Packages를 수정하지 않았다. 기존 타이틀/씬 생성 메뉴와 APK 빌드 도구도 보존했다. 씬을 덮어쓰거나 수동으로 씬 YAML/메타데이터/프리팹을 만들지 않았다. 로컬 Unity가 새 스크립트의 `.meta`를 생성하고 Editor 메뉴로 씬에 오브젝트를 추가한다.

## 변경 파일과 시스템

| 파일 | 역할 |
| --- | --- |
| `Assets/Scripts/Combat/CombatState.cs` | Unity 실행 없이 검사할 수 있는 체력/코인 상태. 최초 사망 전이, 음수 잔액 및 중복 보상 방지에 사용 |
| `Assets/Scripts/Combat/CookieHealth.cs` | 쿠키 체력, 피해 처리, 사망 시 코인 1개 지급 및 오브젝트 제거 |
| `Assets/Scripts/Combat/CookieSpawner.cs` | 화면 중앙의 viewport 영역에 일정 간격 생성, 최대 수 제한, 겹침 방지, 가장 가까운 살아 있는 쿠키 조회 |
| `Assets/Scripts/Combat/AutomaticTurret.cs` | 기본 포탑 1개, 가장 가까운 쿠키 탐색·조준·주기적 발사, 활성 투사체 상한 |
| `Assets/Scripts/Combat/CookieProjectile.cs` | Rigidbody2D 이동, collider sweep/trigger 충돌, 피해 1회 적용, 수명 종료 시 제거 |
| `Assets/Scripts/Combat/CoinWallet.cs` | 코인 잔액, 변경 이벤트. 후속 강화 시스템용 `TrySpend` 확장점 |
| `Assets/Scripts/Combat/CoinHud.cs` | 상단 `Coins: N` 표시. 기존 제목을 Play 중에만 위로 옮기고 비활성화 시 원래 레이아웃 복원 |
| `Assets/Scripts/Combat/SafeAreaPanel.cs` | 코인 UI의 Safe Area 대응 및 화면 크기 변화 반영 |
| `Assets/Scripts/Combat/PlaceholderGraphic.cs` | 외부 에셋 없이 원형 쿠키/초콜릿칩·포탑·탄환의 임시 Sprite/Texture 생성 및 파괴 시 정리 |
| `Assets/Editor/M1CombatSetup.cs` | 기존 메인 씬에 M1 오브젝트와 참조만 추가. 중복 추가 방지, Undo 지원, 자동 저장하지 않음 |
| `Tests/CombatStateChecks.cs` | 반복 피해·사망 보상·재화 경계 조건의 독립 C# 검사 |
| `Tests/verify_m1.py` | Unity를 실행하지 않고 런타임/Editor 코드 분리 컴파일 및 상태 검사 실행 |
| `TASKS.md` | 사용자 확인한 기본 세로 UI 상태와 M1 코드 구현/실행 검증 대기 상태 기록 |
| `Docs/M1_COMBAT.md` | 이 설정·테스트·호환성 보고서 |

플레이 관련 책임을 MonoBehaviour별로 나눴다. 무기를 추가할 때 포탑을 추가하거나 피해·발사 간격을 바꿀 수 있고, 강화는 `AutomaticTurret.Damage`와 `CoinWallet.TrySpend`를 사용할 수 있다. 이번에는 강화 버튼·구매 UI·저장·사운드·오브젝트 풀링을 구현하지 않았다.

## 로컬 Unity 실행 절차

1. 로컬 미커밋 변경을 보존한 뒤 M1 브랜치를 가져온다.

   ```bash
   git fetch origin
   git switch --track origin/codex/m1-cookie-combat
   ```

   이미 해당 로컬 브랜치가 있으면 그 브랜치로 전환한다.

2. Unity **6000.5.1f1**에서 프로젝트를 열고 패키지 import와 C# 컴파일 완료를 기다린다. 새 스크립트/폴더의 `.meta`는 Unity가 생성하도록 둔다. Console 오류가 있다면 진행 전에 확인한다.
3. 기존 `Assets/Scenes/CookieBreakerMain.unity`를 열고 활성 씬으로 설정한다. **기존 Create Test Main Scene 메뉴로 다시 만들지 않는다.**
4. Edit Mode에서 **Cookie Breaker > M1 > Add Combat to Open Main Scene**을 한 번 실행한다.
5. Hierarchy의 `Cookie Breaker M1` 루트와 아래 자식들을 확인한다. 메뉴는 다른 씬·Play Mode·중복 설치 시 중단한다. Inspector 참조는 메뉴에서 연결한다.

   ```text
   Cookie Breaker M1  [CoinWallet, CookieSpawner, CoinHud]
     Active Cookies
     Active Projectiles
     Inactive Templates
       Cookie Template      (inactive)
       Projectile Template  (inactive)
     Automatic Turret
       Base
       Barrel Pivot
         Barrel
         Muzzle
     Coin Canvas
       Safe Area
         Coin Label
   ```

6. **씬을 저장(Ctrl/Cmd+S)**한 뒤 Game View를 Portrait, 예를 들어 `1080 × 1920` 또는 S22 Ultra와 비슷한 비율로 설정한다.
7. Play를 누른다. 임시 그래픽은 Play 때 생성되므로 Edit Mode에서 템플릿/포탑 Sprite가 비어 있어도 예상된 상태다. 쿠키 생성, 아래쪽 포탑 조준/발사, 쿠키 제거, 상단 코인 증가를 확인한다. 기존 제목은 전투 영역을 가리지 않도록 위로 이동하며 Play 종료 후 원래 배치로 돌아온다.
8. 설정을 되돌리려면 설치 직후 Undo하거나 새 `Cookie Breaker M1` 루트만 삭제한다. 기존 카메라·조명·제목·씬은 유지한다. 재실행은 기존 M1 루트가 남아 있으면 중단한다.
9. 저장된 씬과 Unity가 생성한 `.meta`를 로컬에서 검토한다. 해당 변경이 클라우드 소스 브랜치에 자동으로 포함되는 것은 아니다.

## 임시 기본 수치

아래 수치는 초기 동작 확인용이며 최종 밸런스가 아니다. 관련 컴포넌트의 Inspector에서 조절한다.

| 항목 | 기본값 |
| --- | --- |
| 쿠키 최대 체력 | 3 |
| 쿠키 파괴 보상 | 1 coin 고정 |
| 생성 간격 | 0.9초 |
| 활성 쿠키 상한 | 24 (Inspector 1–100) |
| 생성 viewport 영역 | x=0.14–0.86, y=0.36–0.78 |
| 쿠키 간 최소 거리 | 0.72 world unit, 최대 12회 위치 선택 후 다음 생성 주기 대기 |
| 포탑 viewport 위치 | x=0.5, y=0.18 |
| 포탑 탐색 범위 | 8 world units |
| 탄환 피해량 | 1 |
| 발사 간격 | 0.4초 |
| 탄환 속도/수명 | 10 world units/sec, 3초 |
| 활성 탄환 상한 | 24 (Inspector 1–64) |
| 시작 코인 | 0, 매 Play 재시작 시 초기화. 저장 기능 없음 |

`Inactive Templates`의 쿠키/탄환은 런타임 복제 원형이다. **직접 활성화하지 않는다.** 쿠키 체력은 비활성 Cookie Template의 CookieHealth에서 조절한다. 포탑 피해/간격은 Automatic Turret에서, 생성 간격/수 상한은 루트 CookieSpawner에서 조절한다. 코인 지급은 최초 사망 때만 발생하고, 죽거나 비활성인 쿠키는 탐색에서 제외한다. 탄환은 먼저 충돌한 살아 있는 쿠키 한 개에만 피해를 준다.

## 실제 수행한 검사

클라우드에서 다음 명령을 실행했다.

```bash
python3 Tests/verify_m1.py \
  --unity-data /workspace/unity-setup/6000.5.1f1/Editor/Data
git diff --check
```

- Unity 6000.5.1f1 설치 모듈과 템플릿 캐시의 uGUI/URP 어셈블리를 참조한 **런타임 코드 컴파일 통과**. 런타임에는 UnityEditor 참조가 없다.
- 같은 설치 어셈블리를 참조한 **Editor 도구 컴파일 통과**. 최종 컴파일에서 오류/경고 없음.
- 200개 쿠키의 서로 다른 피해·과도한 피해·사망 후 반복 적중 시나리오에서 **정확히 200 coins**를 확인했다.
- 0/음수 피해 무시, 비치명타 보상 없음, int 최대 피해의 안전한 사망 처리 확인.
- 재화 부족/0/음수 비용 실패 시 잔액 유지, 잔액 전액 소비, 음수 잔액 방지, 보상 후 재사용 및 초기값/오버플로 경계 조건 검사 통과.
- 기준 커밋의 기존 메인 씬·설정·패키지·프로필·에셋 해시 보존을 확인했다.

이 검사는 Unity의 실제 프로젝트 import·Console·물리 충돌·Play·씬/화면·APK 실행 검증을 대신하지 않는다. 클라우드에서 Unity Editor나 Android 빌드를 실행하지 않았다. 별도 Python/C# 검증 코드는 `Tests/`에 있어 Unity 게임 어셈블리에 포함되지 않는다.

## 로컬에서 완료 기준 검증

| 검사 | 절차와 기대 결과 | 현재 |
| --- | --- | --- |
| 지속 생성 | Play 후 중앙에 쿠키가 생성되고 한도에 도달해도 초과 생성하지 않음 | 미검증 |
| 자동 전투 | 포탑이 가장 가까운 살아 있는 쿠키를 바라보고 주기적으로 발사함 | 미검증 |
| 피해/보상 | 기본 체력 3의 쿠키가 피해 1의 탄환 세 번에 제거되고 코인 1개 증가 | 물리/화면 미검증, 상태 로직 검사 통과 |
| 중복 보상 | 높은 발사 속도로 여러 탄환이 같은 쿠키를 공격해도 쿠키당 코인 1개 | 상태 로직 검사 통과, Unity 실행 미검증 |
| 목표 소멸 | 현재 목표가 다른 탄환에 파괴돼도 다음 쿠키를 탐색하고 MissingReference 오류가 없음 | 미검증 |
| 탄환 정리 | 빗나간 탄환이 수명 후 제거되고 Active Projectiles가 무제한 증가하지 않음 | 미검증 |
| UI/재시작 | 상단 코인 숫자가 갱신되고 정지 후 재시작하면 0부터 시작. 제목 원본 배치 복원 | 미검증 |
| Console | 로컬 import/Play에서 오류·경고 없는지 확인, 3–5분 반복 플레이 | 독립 C# 컴파일 통과, Unity Console 미검증 |
| Undo/중복 설치 | 메뉴 재실행이 중단되고 Undo로 신규 루트만 제거됨 | 미검증 |
| S22 Ultra | Safe Area, 세로 방향, 쿠키/포탑 가시성, 코인 표시, 연속 실행 확인 | M1 APK 미검증 |

자동 전투의 가장 가까운 목표를 확인하려면 Play를 일시정지하고 Active Cookies의 쿠키 두 개를 서로 다른 거리로 옮겨본 뒤 재개한다. 작은 Game View 비율에서는 쿠키가 겹치지 않는 공간이 부족해 상한 전에 생성이 잠시 대기할 수 있다.

## Android 테스트 준비와 호환성

기존 Android 프로필, Application ID, Portrait 설정, ARM64/IL2CPP, URP 설정 및 기존 메인 씬 GUID를 보존했다. M1 씬 구성과 저장을 로컬에서 수행한 뒤 기존 Android Build Profile로 APK를 빌드하거나 **Cookie Breaker > Build Android Test APK** 메뉴를 사용한다. 해당 메뉴는 기존 메인 씬만 Development APK로 빌드한다. 예상 출력은 `Builds/Android/CookieBreaker.apk`이며 실제 생성 여부는 로컬에서 확인한다.

기존 제목 화면 APK 성공은 사용자가 확인했다. M1의 첫 APK 생성·S22 Ultra 설치·물리 동작·렌더링·Safe Area·성능은 별도 확인이 필요하다. 새 Material/Texture 에셋이나 패키지 의존성을 추가하지 않았다. 임시 그래픽은 런타임에서 생성/해제하며, 게임플레이용 실시간 물리 시뮬레이션은 기존 Physics2D 기본 설정을 사용한다.
