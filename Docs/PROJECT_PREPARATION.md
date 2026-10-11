# Cookie Breaker 프로젝트 준비 보고서

작성일: 2026-10-12 (Asia/Seoul)

## 기존 프로젝트 분석

- 프로젝트 루트 `/workspace/VSvival`, GitHub `20260197/VSvival`, Unity `6000.5.1f1`을 유지했다.
- 시작 Git 상태는 `work` 브랜치, `9952cf3` 초기 체크인, 추적 파일 49개, 미커밋 변경 없음이었다. 초기 준비 단계에서는 커밋·푸시하지 않았으며, 이후 사용자의 명시적 요청에 따라 `codex/cookie-breaker-android-prep` 브랜치로 게시한다. `main`에는 직접 푸시하지 않는다.
- 기존 프로젝트는 Universal 2D 템플릿이다. 기존 사용자 게임 스크립트·게임 프리팹·이미지·오디오는 없었다.
- `SampleScene.unity`에는 직교 Main Camera와 Global Light 2D가 있다. 원래 유일한 활성 빌드 씬이며 현재도 이 설정을 보존했다.
- 별도 `URP2DSceneTemplate.unity`와 `Lit2DSceneTemplate.scenetemplate`도 보존했다.
- 렌더링은 URP `17.5.0`이다. QualitySettings의 모든 품질 단계가 `UniversalRP.asset`을 참조하며 `Renderer2D.asset`을 사용한다. Android 기본 품질은 Medium이다.
- 입력은 Input System `1.19.0`, `activeInputHandler: 1`이다. 기존 입력 에셋의 Player/UI 액션 맵과 EditorBuildSettings의 참조를 보존했다. 실제 게임 동작이나 터치 입력 연결은 없다.
- uGUI `2.5.0`, Test Framework `1.7.0` 및 2D Animation/Aseprite/PSD Importer/SpriteShape/Tilemap, Timeline, Visual Scripting, IDE 연동, Collab Proxy, Multiplayer Center 등을 선언하고 있다. manifest는 직접 의존성 52개를 선언한다.
- `Packages/manifest.json`과 `packages-lock.json`을 JSON으로 검사했다. 기존 파일과 버전을 변경하지 않았다. 실제 패키지 복원 성공 여부는 Unity 실행 후 확인해야 한다.
- Force Text 직렬화, Unity gitignore/gitattributes, Git LFS 설정이 마련되어 있다.

URP 2D·입력 액션·uGUI는 새 게임의 기반으로 재사용할 수 있다. 샘플/템플릿 씬은 참조와 복구용으로 보존한다. Multiplayer Center, Collab Proxy, Visual Scripting, Timeline 및 고급 2D 제작 패키지는 이번 제목 화면에는 필요하지 않지만, 최종 게임 사양을 정한 뒤 정리 여부를 결정한다. 이번 작업에서 기존 씬·에셋·패키지를 삭제하지 않았다.

## 실제 수정한 설정과 소스

| 파일 | 현재 변경 내용 |
| --- | --- |
| `ProjectSettings/ProjectSettings.asset` | Product Name `Cookie Breaker`, Android Application ID `com.mystudio.cookiebreaker`, 기본 Portrait 방향, 가로/뒤집힌 세로 자동 회전 비활성화 |
| `Assets/Editor/CookieBreakerProjectSetup.cs` | 로컬 Unity에서 제목 씬 생성 및 빌드 씬 등록을 수행하는 Editor 메뉴. 자동 실행되지 않음 |
| `Assets/Editor/AndroidApkBuild.cs` | 로컬 Unity에서 Android Development APK를 빌드하는 Editor 메뉴. 요구 설정·플랫폼을 확인하고 BuildReport 성공 및 APK 존재를 검사 |
| `TASKS.md` | 프로젝트·저장소·Unity 버전 확인 항목만 완료 표시. 씬/Play/APK 항목은 미완료 유지 |
| `Docs/PROJECT_PREPARATION.md` | 이 보고서 |

`ProjectSettings.asset`에서 수정한 값은 다음과 같다.

```yaml
productName: Cookie Breaker
defaultScreenOrientation: 1
allowedAutorotateToPortrait: 1
allowedAutorotateToPortraitUpsideDown: 0
allowedAutorotateToLandscapeRight: 0
allowedAutorotateToLandscapeLeft: 0
applicationIdentifier:
  Android: com.mystudio.cookiebreaker
  Standalone: com.DefaultCompany.2D-URP
```

Portrait 방향은 공유 PlayerSettings 값이다. Android 설정을 확인할 때 적용 여부를 검사하고, 향후 iOS를 추가할 때도 확인한다. Company Name과 Standalone Application ID는 보존했다. 기존 Android 전용 Application ID가 없어서 기존 앱 ID를 덮어쓰지 않았다. Play Console 또는 스마트폰의 기존 앱 패키지 목록은 조회하지 않았으므로 외부 앱과의 ID 충돌 여부는 **미확인**이다.

다음 Android 설정은 기존 값 그대로 보존했다.

| 설정 | 값 |
| --- | --- |
| Scripting Backend | IL2CPP (`Android: 1`) |
| Target Architectures | ARM64 (`2`) |
| Minimum API Level | API 26 / Android 8.0 |
| Target API Level | Automatic (`0`), 실제 API는 SDK 설치 후 확인 필요 |
| Bundle Version Code | 1 |
| Custom Keystore | 비활성화, 테스트 시 기본 debug 서명 사용 예정 |

## Unity가 관리할 파일의 처리

앞서 수동 작성한 신규 `CookieBreakerMain.unity`, 해당 `.meta`, Editor 폴더/빌드 도구의 `.meta`는 작업 방향 수정에 따라 프로젝트에서 제외했다. 이전 내용은 `/workspace/cookie-breaker-superseded-6nf17aas/previous-generated-files.tar.gz`에 보관했다. 기존 프로젝트의 씬이나 에셋을 제거한 것은 아니다.

`ProjectSettings/EditorBuildSettings.asset`도 원본 바이트로 복원했다. 현재 SampleScene이 활성 상태이며 새 메인 씬 경로나 추측한 GUID는 등록하지 않았다. 원래 입력 설정 참조도 유지했다.

새 씬의 직렬화 YAML, GUID, `.meta`, Build Profile 자산, Android manifest 또는 Gradle 파일을 수동 생성하지 않았다. 로컬 Unity가 실제 API 실행과 import를 통해 생성하도록 했다. 새 Editor 스크립트의 `.meta`와 Editor 폴더 `.meta`도 로컬 Unity가 생성한다. 해당 파일은 로컬 Unity에서 생성 후 실제 변경 사항과 함께 검토해야 한다.

`CookieBreakerProjectSetup`의 두 메뉴는 다음을 수행하도록 작성했다.

- **Create Test Main Scene**: 기존 경로에 씬이나 메타데이터가 있으면 중단한다. 새 씬을 Additive 모드로 만들어 기존 열린 씬을 덮어쓰지 않는다. Camera/URP camera data/Global Light 2D, 화면 크기에 맞춰지는 Overlay Canvas, 내장 폰트를 사용하는 중앙 `Cookie Breaker` 제목을 만들고 `EditorSceneManager.SaveScene`으로 저장한다. 목표 경로는 `Assets/Scenes/CookieBreakerMain.unity`이다.
- **Register Main Scene for Builds**: 씬이 실제 Unity SceneAsset으로 존재하는지 확인한 뒤 `EditorBuildSettings.scenes` API로 첫 활성 씬에 등록한다. 나머지 씬의 순서와 enabled 상태는 유지한다. 별도 Build Profile이 씬 목록을 override하는지는 사용자가 확인한다.

두 메뉴 모두 Unity `6000.5.1f1`을 요구하며, 자동 실행되거나 기존 에셋을 덮어쓰지 않는다. 현재 이 메뉴들은 **실행하지 않았고, 제목 씬은 아직 생성되지 않았다**.

## 파일 검증 결과

- `git diff --check` 통과.
- 수정한 PlayerSettings YAML 파싱과 이름·Android 앱 ID·Portrait·자동 회전 플래그 검사 통과.
- Packages 두 JSON 파일 파싱 통과. manifest의 직접 의존성 버전과 lockfile의 해당 버전이 일치함을 확인.
- 원본 SHA-256 목록과 비교: 초기 준비 단계에서 기존 추적 파일 중 변경된 것은 `ProjectSettings/ProjectSettings.asset` 하나뿐이었다. 게시 전 원격 main의 추가 커밋 `6cd5f58`을 반영해 기획 문서와 원격 Unity 설정 변경을 보존했다. 기존 씬·빌드 목록·패키지·Unity 버전 파일은 보존했다.
- Editor 도구 두 개를 설치된 Editor 어셈블리와 템플릿 캐시의 uGUI/URP 어셈블리를 참조해 Roslyn으로 독립 컴파일했다. 종료 코드 0. 이는 C# 구문/API 참조 검사이며 실제 프로젝트 패키지 복원·Unity 내부 컴파일·도구 실행 검증을 대신하지 않는다.
- Unity Editor를 추가 실행하거나 APK 빌드를 시도하지 않았다. 씬 파일을 생성하지 않았으므로 새 씬 참조나 화면이 검증됐다고 주장하지 않는다.

## 실행이 필요한 미검증 항목

| 항목 | 확인 상태 |
| --- | --- |
| Unity Editor 설치와 버전 | 앞선 `-version` 실행에서 6000.5.1f1 확인 |
| Unity 라이선스 | 앞선 프로젝트 실행이 종료 코드 198, `No valid Unity Editor license found`로 종료. 해결 여부 미검증 |
| Android Build Support / SDK / NDK / OpenJDK | 앞선 설치 점검에서 Unity Android 모듈과 하위 도구 부재. 로컬 설치 필요 |
| 실제 프로젝트 import·패키지 복원·Unity 내부 컴파일 | 미검증 |
| Editor 메뉴 실행·씬 저장·GUID 생성·빌드 목록 갱신 | 미검증 |
| 제목·폰트 렌더링·기기별 UI·Portrait 동작 | 미검증 |
| Android 활성 Build Profile·실제 target API·서명 | 로컬에서 확인 필요 |
| APK 생성·설치·스마트폰 플레이 | 미검증. 이번 작업에서 APK 생성하지 않음 |

이전 Editor 실행 로그: `/workspace/unity-setup/logs/cookie-breaker-preflight.log`.

## 사용자가 로컬 Unity에서 수행할 순서

1. Unity Hub에서 **6000.5.1f1**을 설치하고 Android Build Support, Android SDK & NDK Tools, OpenJDK를 포함한다. Unity의 지원 절차로 라이선스를 활성화한다.
2. GitHub의 `codex/cookie-breaker-android-prep` 브랜치를 로컬로 가져와 `VSvival` 폴더를 연다. 기존 로컬 clone에서는 `git fetch origin` 후 `git switch --track origin/codex/cookie-breaker-android-prep`를 사용한다. 이미 같은 로컬 브랜치가 있으면 해당 브랜치로 전환한다. 로컬 미커밋 변경은 먼저 보존한다.
3. 패키지 import와 스크립트 컴파일이 끝날 때까지 기다린다. Console 오류가 없는지 확인한다. 새 스크립트와 Editor 폴더의 `.meta`는 이 과정에서 Unity가 생성하도록 둔다.
4. **Cookie Breaker > Create Test Main Scene**을 실행한다. 이미 같은 경로의 씬이 있다면 도구는 중단하므로 기존 파일을 직접 검토한다.
5. Game View를 Portrait(예: 1080×1920)로 맞춰 새 씬의 제목·폰트·갈색 배경을 확인한다. Play Mode에서도 확인한다. 현재는 제목 화면만 있으며 게임플레이나 버튼은 없다.
6. **Cookie Breaker > Register Main Scene for Builds**를 실행한다. File > Build Profiles에서 Android로 전환한다. 씬 목록 override가 있으면 새 메인 씬을 첫 활성 씬으로 맞춘다. 도구가 자동으로 프로필 파일을 생성하거나 플랫폼 전환을 완료하지는 않는다.
7. Android Player Settings에서 Product Name=`Cookie Breaker`, Package Name=`com.mystudio.cookiebreaker`, Default Orientation=Portrait, IL2CPP, ARM64, 최소 API 26을 확인한다. 활성 프로필의 Player Settings override도 확인한다.
8. Preferences > External Tools에서 Unity에 포함된 SDK/NDK/JDK 경로를 확인한다. 실제 선택된 Target API와 테스트 스마트폰의 Android 버전·ARM64 지원도 확인한다.
9. **Cookie Breaker > Build Android Test APK**를 실행한다. 이 도구는 생성된 메인 씬만 대상으로 Development APK를 빌드한다. 실행 중 Build App Bundle과 Export Project를 끄고 종료 후 이전 값으로 복원한다. 출력 목표는 프로젝트 루트의 `Builds/Android/CookieBreaker.apk`이며 기존 gitignore 대상이다. 빌드 로그·BuildReport·생성 APK를 확인한 뒤 성공 여부를 판단한다.
10. 스마트폰에서 개발자 옵션/USB 디버깅을 켜고 기기 연결을 승인한다. 기존 동일 ID 앱 및 서명 충돌을 확인한 뒤 APK를 설치한다. 제목 표시, 세로 방향 유지, 노치/화면 비율, 재실행을 확인한다.
11. Unity가 생성한 씬·메타데이터·설정과 소스의 Git diff를 검토한다. 커밋·푸시는 사용자의 승인 후 수행한다.

제목 화면의 실제 APK 검증을 마치면 핵심 게임 규칙·터치 조작·승리/실패 조건을 정하고 최소 플레이 루프를 구현한다. 인터랙티브 UI에는 Safe Area 대응을 추가한다.

## 원본 백업

`/workspace/cookie-breaker-before-67xhuhg0/`에 원본 추적 파일 전체의 `project.tar.gz`, Git 이력 `history.bundle`, 보존 검사용 `baseline.json`이 있다. 백업은 현재 클라우드 작업 공간에 보관했으며 외부 저장소에 업로드하지 않았다.
