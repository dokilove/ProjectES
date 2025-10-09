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
