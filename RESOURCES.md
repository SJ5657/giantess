# 프로젝트 리소스 위치 정리

작성일: 2026-09-24 · 대상: `Assets/` 전체 (에셋 2,876개, 약 720MB)

## 0. 분석 기준

- 빌드에 포함된 씬 `Assets/Scenes/SampleScene.unity`가 (프리팹·머티리얼·텍스처·애니메이션까지) 직접·간접으로 참조하는 에셋을 `사용`으로 분류했습니다.
- 씬에 직접 들어 있지 않고 코드로만 붙는 `Assets/Scripts`의 스크립트는 코드 참조를 따로 확인했습니다. (51개 전부 사용)
- 코드에는 `Resources.Load` / `AssetDatabase` / Addressables 사용이 없고, `Resources` 폴더는 TextMesh Pro 것뿐이라 이름 문자열로 불러오는 에셋은 없습니다.
- 파일은 옮기거나 지우지 않았습니다. 이 문서는 현황과 정리 후보만 담습니다.

## 1. 한눈에 보기

| 폴더 | 내용 | 사용/전체 | 용량 | 판정 |
|---|---|---|---|---|
| `Scripts` | 게임 로직 (직접 작성) | 51/51 | 0.4MB | 사용 |
| `Scenes` | 게임 씬 (`SampleScene`) | 1/1 | 0.3MB | 사용 |
| `Models` | 캐릭터 VRM, 새장, 차량·군용 차량 팩 | 44/103 | 100.5MB | 일부 사용 |
| `Animations` | 애니메이터 컨트롤러 3개 + 애니메이션 사본 | 9/184 | 85.5MB | 대부분 중복 |
| `Kevin Iglesias` | 거인 공격·인간 애니메이션 팩 원본 | 15/613 | 200.7MB | 필요한 것만 사용 |
| `Maps` | 도시 모델(AKIHABARA), 스카이박스, 데모 자료 | 1,061/1,288 | 187.0MB | 도시·스카이박스만 사용 |
| `Loading Games` | Toon City Pack (건물·소품·차량 프리팹) | 390/475 | 27.0MB | 대부분 사용 |
| `Awbmecreations` | 저폴리 차량 팩 (`Models` 쪽과 중복) | 12/26 | 1.3MB | 사용(중복) |
| `Fonts` | 맑은 고딕 (한글 UI) | 2/2 | 14.9MB | 사용 |
| `TextMesh Pro` | TMP 기본 리소스 | 4/32 | 3.4MB | 사용 |
| `Settings` | URP 렌더 설정, 후처리 프로필 | 프로젝트 설정이 참조 | 0.0MB | 사용 |
| `HamsterCage` | 햄스터 케이지 데모 (예제 씬 포함) | 0/27 | 76.4MB | 미사용 |
| `Screenshots` | 디버그용 캡처 이미지 | 0/61 | 23.2MB | 미사용 |
| `TutorialInfo` | 유니티 템플릿 안내 | 0/4 | 0.0MB | 미사용 |

## 2. 기능별로 쓰는 리소스가 어디 있는지

### 2-1. 플레이어 (거인)

| 용도 | 위치 |
|---|---|
| 여성 모델 | `Assets/Models/giantess2.vrm` (씬: `Giant/Model`) |
| 남성 모델 | `Assets/Models/GiantessMan.vrm` (씬: `Giant/ModelMale`) |
| 성별 선택 프리뷰 | 씬의 `GenderPreviewStudio` (같은 두 VRM의 복사본) |
| 애니메이터 | `Assets/Animations/GiantAnimatorController.controller` |
| 대기·걷기·달리기 | `Assets/Animations/Human Animations/Female/`, `.../Male/` 의 `Idle01`, `Walk01_Forward`, `Run01_Forward` (6개) |
| 펀치 3종 | `Assets/Kevin Iglesias/Characters/Humanoid Giant/Animations/Combat/Giant@UnarmedAttack01, 02, 03_A.fbx` |
| 던지기 | `Assets/Kevin Iglesias/Human Animations/Animations/Female/Combat/Thrown/Ball/HumanF@ThrowBall01_R.fbx` 와 `... - Hold.fbx` |
| 아바타 마스크 | `Assets/Kevin Iglesias/Human Animations/Models/Avatar Masks/Human Body Full Mask.mask` |

### 2-2. 시민·병사·NPC

| 용도 | 위치 |
|---|---|
| 시민 (작은 사람) | `Assets/Models/man.vrm` (`CityGenerator.tinyPersonPrefab`) + `Assets/Animations/TinyManAnimatorController.controller` |
| 경찰·병사 몸 | `Assets/Models/man.vrm` (`VehicleSpawner.officerBodyPrefab`) + `Assets/Animations/TinySoldierAnimatorController.controller` |
| 병사 무기 | `Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Soldier Animations/Prefabs/Weapons/Human_Gun.prefab` (총 조준·사격 애니메이션은 `HumanM@Gun_Aim01*.fbx`) |
| 씬 NPC | `Assets/Models/BlackMan.vrm` (씬의 `BlackMan_NPC`) |
| 새장 | `Assets/Models/bird_cage.glb` (`YardTreeBuilder.birdCagePrefab`) |
| 인간 아바타 원형 | `Assets/Kevin Iglesias/Human Animations/Models/HumanF_Model.fbx`, `HumanM_Model.fbx` |

### 2-3. 차량·군용 장비

| 용도 | 위치 |
|---|---|
| 교통 차량 9종 | `Assets/Models/Mobile Optimize-Free Low Poly Cars/Prefabs/` (`TrafficCarSpawner.carPrefabs`). 메시·머티리얼은 `Assets/Awbmecreations/` 쪽 파일을 참조 |
| 탱크 7종 | `Assets/Models/Lowpoly Military Armored Army Vehicles Strategy Assets Pack/Prefabs/Tank 1~7` |
| 헬리콥터 3종 | 같은 폴더의 `Helicopter 1~3` |

### 2-4. 도시·환경

| 용도 | 위치 |
|---|---|
| 도시 본체 | `Assets/Maps/005339_08932_25_14/Models/PQ_Remake_AKIHABARA.fbx` (머티리얼 617개, 텍스처 380개가 같은 폴더) |
| 랜덤 생성용 건물·랜드마크·식생·소품·차량 | `Assets/Loading Games/Toon City Pack/Prefabs/` (`CityGenerator.kit*`) |
| 바닥·보도 머티리얼 | `Assets/Loading Games/Toon City Pack/Materials/Infrastructure/Roads-Streets-Highways/` |
| 스카이박스 9종 | `Assets/Maps/Skyboxes/*.mat` |
| 마당 나무 | `Assets/Loading Games/Toon City Pack/Prefabs/Vegetation/tree-type-A.prefab` |

### 2-5. UI·렌더링

| 용도 | 위치 |
|---|---|
| 한글 폰트 | `Assets/Fonts/MalgunGothic.ttf`, `MalgunGothic SDF.asset` |
| 기본 TMP 폰트·셰이더 | `Assets/TextMesh Pro/` |
| 후처리 (톤매핑·블룸·비네트) | `Assets/Settings/SampleSceneProfile.asset` |
| URP 렌더러 설정 | `Assets/Settings/` |

### 2-6. 스크립트

`Assets/Scripts/` 에 51개가 한 폴더에 모여 있습니다. 성별 선택 기능으로 `PlayerModelSwitcher.cs`, `GenderPreviewStudio.cs`가 추가되었습니다.

## 3. 정리 후보 (미사용·중복)

### 3-1. 중복된 패키지 (같은 파일이 두 곳에 있음)

| 두 위치 | 동일 파일 | 중복 용량 | 씬이 실제로 쓰는 쪽 |
|---|---|---|---|
| `Animations/Human Animations` ↔ `Kevin Iglesias/Human Animations` | 124개 | 56.6MB | `Animations/` 쪽 사본 6개 |
| `Animations/Humanoid Giant` ↔ `Kevin Iglesias/Characters` | 38개 | 28.7MB | `Kevin Iglesias/` 쪽 |
| `Awbmecreations` ↔ `Models/Mobile Optimize-Free Low Poly Cars` | 12개 | 1.1MB | 두 쪽이 섞여 참조됨 |
| `Maps/Cars` 내부 | 8개 | 0.4MB | 미사용 |

한 위치로 합치면 약 87MB가 줄어듭니다. 컨트롤러가 어느 사본을 참조하는지 확인하고, 유니티 안에서 참조를 바꾼 뒤 정리해야 안전합니다.

### 3-2. 씬에서 전혀 쓰지 않는 폴더·파일

| 대상 | 용량 | 비고 |
|---|---|---|
| `HamsterCage/` 전체 | 76.4MB | 데모 예제 씬과 대용량 예제 에셋 |
| `Maps/PQAssets/` (Query-Chan 데모) | 59.8MB | 데모 씬 3개 포함 |
| `Animations/Humanoid Giant/` | 28.8MB | `Kevin Iglesias` 사본과 데모 씬 |
| `Screenshots/` | 23.2MB | `CageCheck*.png` 등 디버그 캡처 61장 |
| `Maps/Cars/` | 5.0MB | 사용 0 |
| `Models/giantess.vrm`, `giantess3.vrm` | 각 약 15.5MB | 씬 미사용 (교체·의상 후보로 보임) |
| `Models/Equipments/ButterflyNet.glb` | 2.1MB | 씬 미사용 |
| `TutorialInfo/` | 0.0MB | 유니티 템플릿 파일 |
| 데모 씬 15개 | - | 빌드에 포함되지 않음 (각 팩 폴더 안에 있음) |

### 3-3. 팩 안에서 일부만 쓰는 경우 (삭제 대신 참고)

| 대상 | 사용/전체 | 비고 |
|---|---|---|
| `Kevin Iglesias/Human Animations` | 11/517 | 애니메이션 원본 팩. 나중에 쓸 수 있어 통째로 지우기는 아깝습니다 |
| `Kevin Iglesias/Characters/Humanoid Giant` | 4/96 | 데모 씬과 모델 등 |
| `Models/Lowpoly Military ...` | 30/69 | FBX 25개 중 1개, 프리팹 24개 중 10개만 사용 |
| `Models/Mobile Optimize-Free Low Poly Cars` | 9/26 | 프리팹 9개 사용 |
| `Loading Games/Toon City Pack` | 390/475 | 미사용은 데모 씬과 일부 프리팹·모델 |
| `Maps/005339_08932_25_14` | 998/1040 | 미사용은 데모 씬(과 라이팅 데이터), 데모 스크립트 7개, 문서 등 |

## 4. 권장 정리 순서

1. 지금 상태를 깃에 커밋해 둡니다. (복구 지점)
2. 3-2의 `Screenshots`, `TutorialInfo`부터 정리합니다. 참조가 없어 가장 안전합니다.
3. `HamsterCage`, `Maps/PQAssets`, `Maps/Cars`처럼 데모 위주 폴더를 정리합니다. 필요하면 프로젝트 밖 백업 폴더로 옮긴 뒤 삭제해도 됩니다.
4. 3-1의 중복 패키지를 한 곳으로 합칩니다. `GiantAnimatorController`가 참조하는 클립이 어느 사본인지 먼저 확인합니다.
5. 필요하면 아래 폴더 구조로 재배치합니다. 이동은 반드시 유니티 안에서 해야 GUID와 씬 참조가 유지됩니다.

권장 구조 (제안, 적용 안 함):

```
Assets/
  _Project/            # 직접 만든 것
    Scenes/  Scripts/  Characters/(VRM)  Animations/(컨트롤러)  UI/(폰트)
  ThirdParty/          # 구입·다운로드 팩 (Kevin Iglesias, Toon City Pack, Maps, 차량 팩 ...)
```

## 5. 한계

- 위 판정은 `SampleScene` 기준의 정적 분석입니다. 프로젝트 설정(URP 에셋 등)이 참조하는 파일과, 새로 만들 씬에서 쓸 예정인 파일은 `미사용`으로 보일 수 있습니다.
- 삭제나 이동 전에는 커밋과 백업을 먼저 하고, 이동 후 `SampleScene`을 열어 누락된 참조(핑크색 머티리얼, Missing 표시)가 없는지 확인하세요.
