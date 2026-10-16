# M3 — 자동 저장 및 게임 재설정

> M3 당시 버전 1 기록이다. M4는 이 버전을 읽고 버전 2에 포대 배치를 추가한다. 최신 설치 및 저장 차이: `Docs/M4_GRID_PLACEMENT.md`. PC Play 기본 검증 정책은 유지한다.

## 기준과 범위

- 기준: `origin/develop`의 `f8e74de` (M2 PR 병합), 작업 브랜치 `codex/m3-auto-save`.
- Unity 6000.5.1f1, URP 2D, Android Portrait, Product Name/Application ID, 기존 씬/`.meta`/에셋/패키지는 유지한다.
- 원격 develop에 M2 코드와 입력 참조 오류 수정이 포함된 것을 확인했다. 로컬 Unity에서 만든 M2 씬 구성과 새 M2 `.meta`는 이 기준 커밋에 없으므로, **실제 로컬 M2 씬을 보존하고 M3만 추가**한다. M2가 아직 설치되지 않은 씬에서는 기존 M2 메뉴로 먼저 설치해야 한다.
- 기존 코인 잔액과 강화 모델을 공유한다. 누적 쿠키 파괴 수는 기존에 별도 모델이 없어 추가 저장하지 않는다. 쿠키/탄환과 플레이 중 대기 시간도 저장하지 않는다. 오프라인 보상과 신규 포탑 저장은 범위 밖이다.

## 데이터와 파일

위치: `Application.persistentDataPath` (실제 경로는 Play 시작 시 Console 출력).

```json
{
  "data": {
    "version": 1,
    "coins": 123,
    "damageLevel": 3,
    "attackSpeedLevel": 2,
    "productionLevel": 1
  },
  "checksum": "필드 값에 대한 SHA-256 Base64"
}
```

| 파일 | 역할 |
| --- | --- |
| `cookie-breaker-save.json` | 주 저장 파일 |
| `cookie-breaker-save.json.bak` | 같은 저장 시점의 별도 복구용 사본 |
| `cookie-breaker-save.json.reset` | 초기화 작업이 중단되어도 이전 진행을 읽지 않게 하는 초기 상태 기록 |
| 각 파일의 `.tmp` | 동일 디렉터리 기록/flush 후 교체용; 로딩 시 임시 파일을 읽지 않음 |

`JsonUtility`는 직렬화 DTO만 처리한다. 강화 기본값/피해량/간격/비용은 저장하지 않고 기존 `UpgradeState`가 레벨로 재계산한다. `CoinBalance` 객체도 교체하지 않고 잔액을 복원하므로 강화가 같은 잔액을 계속 사용한다. 유효한 레벨은 구매 가능 비용 및 실제 효과 상한으로 검증하며, 여러 번 복원해도 강화 효과가 더해지지 않는다.

## 저장·로드 동작

- `AutoSaveService`는 기본 실행 순서 -100에서 기존 M2 규칙을 준비하고 첫 전투 Update 전에 로드한다. 코인/강화 변경 이벤트와 UI의 OnEnable 갱신을 통해 복원 결과를 표시한다.
- 30초마다 `Time.unscaledDeltaTime` 기준으로 저장한다. 구매 성공 전용 `Purchased` 이벤트에 연결하므로 성공한 강화 구매 직후 저장하지만, 코인 획득이나 복원 이벤트는 즉시 디스크 기록을 하지 않는다.
- `OnApplicationPause(true)`, `OnApplicationFocus(false)`, `OnApplicationQuit`에서도 저장한다. Editor에서 포커스를 다른 창으로 옮기면 추가 저장이 발생할 수 있다.
- 파일 없음: 기존 초기값으로 플레이한다. 첫 30초, 구매, 포커스 상실 또는 정상 종료 때 저장한다.
- 주 파일 정상: 주 파일을 복원한다. 잘린 JSON/체크섬 불일치/64KiB 초과 등 손상: 정상 백업을 시도한다.
- 지원하지 않는 버전 또는 두 파일 모두 복구 불가: 파일을 덮어쓰지 않고 이번 실행은 초기 수치로 진행한다. Console과 설정에 보존/저장 중단 상태를 표시한다. 플레이 중 얻은 새 진행도 자동 저장하지 않는다. 원본 복구 또는 명시적 게임 재설정이 필요하다. 미래 버전이 주 파일에 있으면 오래된 백업으로 조용히 되돌리지 않는다.
- 설정 변경으로 현재 M2 상한과 호환되지 않는 레벨도 원본을 보존하고 자동 저장을 막는다.
- 작은 JSON은 동기식으로 기록한다. `.tmp`를 `Flush(true)` 후 `File.Replace`/`File.Move`로 교체하며, 유효한 대상 파일을 먼저 삭제하지 않는다. 안전한 교체가 지원되지 않거나 권한/용량 문제로 실패하면 로그와 설정 상태에 표시하고 다음 저장 시 재시도한다.
- 강제 종료·프로세스 kill 때 종료 콜백 실행은 보장되지 않는다. 일반 코인 수익은 마지막 저장 이후 최대 약 30초가 유실될 수 있다. 이번 범위에는 오프라인 진행 계산이 없다.

## 재설정

1. 설정 버튼을 누르면 상단 코인/하단 강화와 별도 Canvas의 창이 열린다. 모달 배경은 뒤쪽 버튼 클릭을 차단한다. 전투는 계속된다.
2. Reset Game → 확인 창의 Cancel은 창만 닫으며 파일·게임 상태를 초기화하지 않는다. 전투가 계속되므로 일반적인 코인 증가는 가능하다.
3. Reset 확인 시 자동 저장을 잠시 차단하고 먼저 영구 초기화 기록 `.reset`을 기록한다. 이 기록에 실패하면 현재 진행을 유지하고 오류를 표시한다.
4. 기록이 성공하면 주 파일/백업을 모두 0코인/Lv 0으로 교체한다. 도중 실패해도 초기화 기록이 남아 다음 실행에서 이전 주 파일/백업보다 우선한다. 다음 저장 시 사본 정리를 재시도한다.
5. 활성 탄환과 쿠키는 즉시 비활성화 후 제거한다. 코인과 레벨은 0, 효과는 M1/M2 기본 수치로 복원한다. 이전 탄환의 잔여 적중으로 보상을 받지 않는다. 이후 새 전투는 정상 진행한다.
6. 주 파일·백업 초기 상태가 기록된 뒤 초기화 기록을 삭제한다. 일반 자동 저장을 다시 허용한다. 손상된 초기화 기록이 있으면 이전 진행을 읽지 않고 보존 상태로 중단한다.

## 로컬 설치 (Unity Play 기본)

1. 현재 로컬 씬과 `.meta`를 먼저 M2 작업/검증 브랜치에 커밋해 보호한다. M3 브랜치로 전환하면서 잃지 않도록 해당 커밋을 M3에 반영한다. 충돌 시 기존 씬을 강제 덮어쓰거나 리셋하지 않는다.
2. `git fetch origin` 후 `codex/m3-auto-save`를 체크아웃하고 Unity **6000.5.1f1**로 연다. 새 스크립트/폴더 `.meta`는 Unity가 생성한다. Console 컴파일 완료를 기다린다.
3. `Assets/Scenes/CookieBreakerMain.unity`를 열어 활성 씬으로 지정한다. 기존 M1 전투, M2 업그레이드와 EventSystem을 확인한다. M2가 없을 때만 **Cookie Breaker > M2 > Add Upgrades to Open Main Scene**을 실행한다. M1이나 제목 씬을 다시 만들지 않는다.
4. Edit Mode에서 **Cookie Breaker > M3 > Add Auto Save and Settings to Open Main Scene**을 실행한다. 이 메뉴는 기존 M1/M2를 수정하지 않고 `Cookie Breaker M3`를 추가한다. 다른 씬·Play Mode·입력 충돌·부분 설치 상태에서는 중단한다.
5. 새 루트의 AutoSaveService에 기존 CoinWallet/UpgradeController가 연결되고, Settings Canvas의 Safe Area에 설정 버튼·창·확인 창과 SettingsPanel 참조가 연결되어 있는지 확인한다.
6. 메뉴를 다시 실행해도 새 루트가 추가되지 않고 기존 루트를 선택하는지 확인한다. 설치 직후 Undo는 추가한 루트를 제거한다. 자동 저장하는 메뉴가 아니므로 **씬을 직접 저장하고 닫았다 다시 열어** 참조를 확인한다.
7. Play로 아래 검증표를 수행한다. 자동 저장 경로는 Editor/빌드 환경에 따라 다를 수 있으며 다른 Application ID/Company Name/Product Name을 사용하면 경로가 달라질 수 있다. 검증 중 기존 PlayerSettings를 변경하지 않는다.
8. Unity가 생성한 새 `.meta`와 저장한 씬을 **M3 작업 브랜치**에 커밋/푸시한다. 저장 JSON은 프로젝트 밖에 생성되며 저장 파일, APK, Library/Temp는 저장소에 넣지 않는다. 사용자 승인 없이 develop/main에 병합하지 않는다.

## PC Play 검증표

- [ ] 첫 Play에서 오류 없이 M1 전투와 M2 강화가 동작하고 설정 버튼이 상단 코인/하단 강화와 겹치지 않는다.
- [ ] 30초 기다려 Console 경로에 주 파일/백업이 생성되는지 확인한다. 파일 수정 시각을 보고 코인마다 변경되지 않는지 확인한다. 이 검사 중 포커스를 옮기면 저장되므로 별도 파일 감시 도구로 관찰하거나 이 추가 저장을 구분한다.
- [ ] 코인을 모아 세 강화를 구매한다. 구매 직후 파일의 잔액/해당 레벨이 갱신되는지 확인한다.
- [ ] Play를 정지하고 다시 시작한다. 코인과 세 레벨/피해량/발사·생성 간격/비용/UI가 복원되고, 세 번 반복해도 효과가 중복 증가하지 않는지 확인한다.
- [ ] 설정 → Reset Game → Cancel 후 기존 레벨과 진행이 유지되는지 확인한다.
- [ ] Reset 확인 후 코인/레벨이 0, 기본 효과로 돌아오고 기존 쿠키/탄환이 정리되는지 확인한다. 설정을 닫고 새 전투·구매가 가능한지 확인한다.
- [ ] 재설정 직후 정지/재시작 및 추가 30초 대기 후 다시 재시작해 이전 진행이 되살아나지 않는지 확인한다. 새 전투로 얻은 코인은 새 진행으로 저장될 수 있다.
- [ ] 별도 테스트 폴더에 파일을 백업하고 Play 종료 상태에서 주 파일만 손상시킨다. 재시작 시 경고와 함께 정상 백업이 복구되는지 확인한다.
- [ ] 테스트용 주 파일/백업을 모두 손상시키거나 주 파일 버전을 999로 바꾼다. 자동 덮어쓰기가 중단되고 설정에서 보존 상태를 표시하는지 확인한다. 버전은 체크섬 검사보다 먼저 검사한다. 테스트 후 원본 백업을 복구하거나 명시적 Reset을 사용한다.
- [ ] 쓰기 권한 없는 테스트 환경에서 저장/Reset 실패가 Console·설정에 표시되고 기존 파일이 보존되는지 확인한다. 실제 진행 파일 대신 별도 테스트 환경으로 수행한다.
- [ ] M3 설치 메뉴 재실행·Undo·씬 재열기 후 중복 AutoSaveService/EventSystem이 없는지 확인한다.

## 실제 수행한 검사와 미검증

```bash
python3 Tests/verify_m1.py --unity-data /workspace/unity-setup/6000.5.1f1/Editor/Data
git diff --check
```

- 통과: Unity/패키지 DLL 참조를 사용한 런타임·Editor 독립 C# 컴파일, 기존 M1/M2 순수 규칙 검사.
- 통과: 실제 임시 디렉터리의 첫 저장/원자 교체/JSON 왕복, 잘린 파일·체크섬·초대형 파일·백업 복구·미지원 버전 처리.
- 통과: 초기화 기록 실패 시 기존 상태 유지, 초기화 기록 후 주 파일 교체 실패를 디렉터리 충돌로 재현한 뒤 초기 상태 우선 로드, 재설정 후 백업에도 초기 상태 유지, 이후 새 진행 저장.
- 통과: 공유 잔액 복원 후 구매, 반복 레벨 복원의 중첩 방지, 불가능한 레벨 거부와 부분 변경 방지, 기본 수치 복원.
- 파일 검사는 .NET 8에서 테스트 JSON 코덱을 사용한다. 실제 게임은 JsonUtility를 사용하며 **Unity의 JsonUtility 실행·import·생명주기·물리/UI/씬은 이 검사로 검증되지 않는다.**
- 미검증: 위 PC Play 검증표, Android pause/focus/quit 콜백과 파일 교체 API/실기기 UI. **이번 단계에 Android APK 빌드나 실기기 재검증을 필수로 요구하지 않는다.**

## 변경 파일

추가: `Assets/Scripts/Persistence/{SaveData,JsonSaveCodec,SaveFileStore,AutoSaveService,SettingsPanel}.cs`, `Assets/Editor/M3AutoSaveSetup.cs`, `Tests/SaveStateChecks.cs`, 이 문서.

수정: `CombatState.cs`(잔액 복원), `AutomaticTurret.cs`/`CookieSpawner.cs`(재설정 중 활성 개체 정리), `UpgradeState.cs`(레벨 직접 복원), `UpgradeController.cs`(효과 재계산/복원·구매 이벤트), `Tests/verify_m1.py`, `GAME_DESIGN.md`, `TASKS.md`, `Docs/M2_UPGRADES.md`.

기존 미커밋 `Docs/M1_COMBAT.md` 변경은 보존하며 이번 커밋에 포함하지 않는다.
