# 프로젝트 분석 및 개발 구조

## 1. 주요 구현 구조

이 프로젝트는 **Unity 엔진**을 사용하여 제작된 3D 애플리케이션 또는 게임입니다.

*   **Unity 프로젝트 기반**: `Assets`, `ProjectSettings`, `Packages`, `Library` 등 Unity 프로젝트의 표준적인 폴더 구조를 가집니다.
*   **Universal Render Pipeline (URP) 사용**: `ProjectSettings/URPProjectSettings.asset` 및 `Assets/Settings` 폴더 내 관련 에셋들을 통해 URP를 사용하며, PC와 모바일 플랫폼별 렌더링 최적화가 적용되어 있습니다.
*   **새로운 Input System 사용**: `Assets/InputSystem_Actions.inputactions` 파일을 통해 Unity의 새로운 Input System을 사용하여 사용자 입력을 관리합니다.
*   **차량(Vehicle) 중심의 플레이**: `Vehicle.prefab`과 `PlayerController.cs` 스크립트를 통해 플레이어의 차량 조종이 핵심 기능임을 알 수 있습니다.
*   **3인칭 시점 카메라 (Spring Arm Camera)**: `SpringArmCamera.cs` 스크립트는 차량을 따라다니는 3인칭 카메라를 구현합니다.

### 요약
종합적으로, 이 프로젝트는 **Unity URP**를 기반으로 **새로운 Input System**을 사용하여 **차량을 조종하는 3D 게임 또는 시뮬레이션**입니다. PC와 모바일 플랫폼을 모두 고려한 렌더링 설정이 되어 있으며, `PlayerController`와 `SpringArmCamera` 스크립트를 통해 핵심적인 플레이어 제어 로직을 구현하고 있습니다.

---

## 2. 개발 컨텐츠 트리 구조 (Assets 폴더 기준)

`Assets` 폴더는 실제 게임을 구성하는 모든 리소스와 코드를 포함하고 있습니다.

```
Assets/
├── 📂 Materials/ (모델에 적용되는 재질 에셋)
│   ├── Ground.mat
│   ├── Vehicle_Body.mat
│   ├── Vehicle_Window.mat
│   └── Wheel.mat
│
├── 📂 Prefabs/ (재사용 가능한 게임 오브젝트 에셋)
│   └── Vehicle.prefab (핵심 플레이 요소인 차량 프리팹)
│
├── 📂 Scenes/ (게임의 레벨 또는 화면 단위)
│   └── SampleScene.unity (기본 테스트 및 개발용 씬)
│
├── 📂 Scripts/ (게임 로직을 담고 있는 C# 스크립트)
│   ├── PlayerController.cs (플레이어 입력 및 차량 제어 로직)
│   └── SpringArmCamera.cs (3인칭 추적 카메라 로직)
│
├── 📂 Settings/ (렌더링 및 품질 관련 설정 에셋)
│   ├── Mobile_RPAsset.asset (모바일용 렌더 파이프라인)
│   └── PC_RPAsset.asset (PC용 렌더 파이프라인)
│
├── 📂 TutorialInfo/ (에디터 내 정보 표시 관련 에셋)
│   └── ...
│
└── 📜 InputSystem_Actions.inputactions (사용자 입력(키보드, 패드 등) 설정 파일)
```

---

## 3. 현재까지 구현된 기능 명세

### 3.1. 미니맵 시스템 (`MinimapController.cs`)

화면 우측 하단에 원형 미니맵을 표시합니다.

*   **고정형 탑뷰**: 미니맵은 항상 북쪽을 향하는 고정된 시점을 가지며, 플레이어 아이콘만 회전합니다.
*   **Orthographic 투영**: `Orthographic` 카메라를 사용하여 원근 왜곡 없이 깔끔한 2D 스타일의 맵을 표시합니다. 줌 레벨은 `orthographicSize` 값으로 조절할 수 있습니다.
*   **아이콘 분리**: 메인 화면에서는 차량의 3D 모델이 보이지만, 미니맵에서는 가시성을 위해 단순한 화살표 아이콘으로 표시됩니다. 이는 Unity의 레이어와 카메라의 Culling Mask를 통해 구현되었습니다.

### 3.2. 목표(Goal) 시스템 (`GoalManager.cs`)

플레이어에게 랜덤한 목표를 제공하고 완수하는 핵심 게임 플레이 루프입니다.

*   **랜덤 목표 생성**: `SpawnArea`로 지정된 경계 상자 내에 랜덤한 위치에 목표(Goal)가 생성됩니다.
*   **목표 시각화**: 목표 지점의 바닥에는 원형 마커가 표시됩니다.
*   **도착 및 대기**: 플레이어가 목표 반경 내에 도착하여 지정된 시간(랜덤) 동안 머무르면 목표가 완수됩니다.
*   **진행 상황 표시**: 목표 지점에 머무는 동안 원형 마커의 색상이 빨간색에서 초록색으로 점차 변경되어 시각적인 피드백을 제공합니다.
*   **다음 목표 생성**: 하나의 목표를 완수하면 즉시 다음 목표가 새로운 랜덤 위치에 생성됩니다.

### 3.3. 목표 인디케이터 (`GoalIndicator.cs`)

현재 목표의 방향을 알려주는 UI 인디케이터 기능입니다.

*   **화면 밖 목표 표시**: 목표가 현재 메인 카메라의 화면 밖에 있을 경우에만 인디케이터가 활성화됩니다.
*   **방향 지시**: 인디케이터는 화살표 모양으로, 화면 가장자리에 붙어서 목표가 있는 방향을 가리킵니다.
*   **자동 비활성화**: 목표가 화면 안으로 들어오면 인디케이터는 자동으로 사라집니다.