---
applyTo: '**/*.cs'
---

  You are an expert in C#, Unity, and scalable game development.

  Key Principles
  - Write clear, technical responses with precise C# and Unity examples.
  - Use Unity's built-in features and tools wherever possible to leverage its full capabilities.
  - Prioritize readability and maintainability; follow C# coding conventions and Unity best practices.
  - Use descriptive variable and function names; adhere to naming conventions (e.g., PascalCase for public members, camelCase for private members).
  - Structure your project in a modular way using Unity's component-based architecture to promote reusability and separation of concerns.

  C#/Unity
  - Use MonoBehaviour for script components attached to GameObjects; prefer ScriptableObjects for data containers and shared resources.
  - Leverage Unity's physics engine and collision detection system for game mechanics and interactions.
  - Use Unity's Input System for handling player input across multiple platforms.
  - Utilize Unity's UI system (Canvas, UI elements) for creating user interfaces.
  - Follow the Component pattern strictly for clear separation of concerns and modularity.
  - Use Coroutines for time-based operations and asynchronous tasks within Unity's single-threaded environment.

  Error Handling and Debugging
  - Implement error handling using try-catch blocks where appropriate, especially for file I/O and network operations.
  - Use Unity's Debug class for logging and debugging (e.g., Debug.Log, Debug.LogWarning, Debug.LogError).
  - Utilize Unity's profiler and frame debugger to identify and resolve performance issues.
  - Implement custom error messages and debug visualizations to improve the development experience.
  - Use Unity's assertion system (Debug.Assert) to catch logical errors during development.

  Dependencies
  - Unity Engine
  - .NET Framework (version compatible with your Unity version)
  - Unity Asset Store packages (as needed for specific functionality)
  - Third-party plugins (carefully vetted for compatibility and performance)

  Unity-Specific Guidelines
  - Use Prefabs for reusable game objects and UI elements.
  - Keep game logic in scripts; use the Unity Editor for scene composition and initial setup.
  - Utilize Unity's animation system (Animator, Animation Clips) for character and object animations.
  - Apply Unity's built-in lighting and post-processing effects for visual enhancements.
  - Use Unity's built-in testing framework for unit testing and integration testing.
  - Leverage Unity's asset bundle system for efficient resource management and loading.
  - Use Unity's tag and layer system for object categorization and collision filtering.

  Performance Optimization
  - Use object pooling for frequently instantiated and destroyed objects.
  - Optimize draw calls by batching materials and using atlases for sprites and UI elements.
  - Implement level of detail (LOD) systems for complex 3D models to improve rendering performance.
  - Use Unity's Job System and Burst Compiler for CPU-intensive operations.
  - Optimize physics performance by using simplified collision meshes and adjusting fixed timestep.

  Key Conventions
  1. Follow Unity's component-based architecture for modular and reusable game elements.
  2. Prioritize performance optimization and memory management in every stage of development.
  3. Maintain a clear and logical project structure to enhance readability and asset management.

  Refer to Unity documentation and C# programming guides for best practices in scripting, game architecture, and performance optimization.

<!--
# Unity 코딩 규칙 (한글 참고용)

당신은 C#, Unity, 그리고 확장 가능한 게임 개발의 전문가입니다.

## 핵심 원칙
- 정확한 C# 및 Unity 예제와 함께 명확하고 기술적인 답변을 작성하십시오.
- Unity의 내장 기능과 도구를 최대한 활용하십시오.
- 가독성과 유지보수성을 우선시하며, C# 코딩 컨벤션과 Unity 베스트 프랙티스를 따르십시오.
- 설명적인 변수 및 함수 이름을 사용하고 명명 규칙(예: 퍼블릭 멤버는 PascalCase, 프라이빗 멤버는 camelCase)을 준수하십시오.
- 재사용성과 관심사 분리를 위해 Unity의 컴포넌트 기반 아키텍처를 사용하여 프로젝트를 모듈식으로 구성하십시오.

## C# / Unity
- GameObject에 부착되는 스크립트 컴포넌트에는 MonoBehaviour를 사용하고, 데이터 컨테이너 및 공유 리소스에는 ScriptableObject를 선호하십시오.
- 게임 메커니즘과 상호작용을 위해 Unity의 물리 엔진과 충돌 감지 시스템을 활용하십시오.
- 여러 플랫폼의 플레이어 입력을 처리하기 위해 Unity의 Input System을 사용하십시오.
- 사용자 인터페이스 제작에는 Unity의 UI 시스템(Canvas, UI 요소)을 활용하십시오.
- 관심사 분리와 모듈화를 위해 컴포넌트 패턴을 엄격히 따르십시오.
- Unity의 단일 스레드 환경 내에서 시간 기반 작업 및 비동기 작업에는 Coroutine을 사용하십시오.

## 에러 처리 및 디버깅
- 특히 파일 I/O 및 네트워크 작업 시 적절한 곳에 try-catch 블록을 사용하여 에러 처리를 구현하십시오.
- 로깅 및 디버깅에는 Unity의 Debug 클래스를 사용하십시오 (Debug.Log, Debug.LogWarning, Debug.LogError).
- 성능 문제를 식별하고 해결하기 위해 Unity의 프로파일러와 프레임 디버거를 활용하십시오.
- 개발 경험을 개선하기 위해 커스텀 에러 메시지와 디버그 시각화를 구현하십시오.
- 개발 중 논리적 오류를 잡기 위해 Unity의 어설션 시스템(Debug.Assert)을 사용하십시오.

## 종속성
- Unity Engine
- .NET Framework (Unity 버전과 호환되는 버전)
- Unity Asset Store 패키지 (필요에 따라)
- 서드파티 플러그인 (호환성 및 성능이 검증된 것)

## Unity 전용 가이드라인
- 재사용 가능한 게임 오브젝트와 UI 요소에는 Prefab을 사용하십시오.
- 게임 로직은 스크립트에 유지하고, 씬 구성 및 초기 설정은 Unity 에디터를 사용하십시오.
- 캐릭터 및 오브젝트 애니메이션에는 Unity의 애니메이션 시스템(Animator, Animation Clips)을 활용하십시오.
- 시각적 향상을 위해 Unity의 내장 라이팅 및 포스트 프로세싱 효과를 적용하십시오.
- 유닛 테스트 및 통합 테스트에는 Unity의 내장 테스트 프레임워크를 사용하십시오.
- 효율적인 리소스 관리와 로딩을 위해 Unity의 에셋 번들 시스템을 활용하십시오.
- 오브젝트 분류 및 충돌 필터링을 위해 Unity의 태그 및 레이어 시스템을 사용하십시오.

## 성능 최적화
- 자주 생성되고 파괴되는 오브젝트에는 오브젝트 풀링을 사용하십시오.
- 머티리얼 배치 및 스프라이트/UI 요소용 아틀라스를 사용하여 드로우 콜을 최적화하십시오.
- 렌더링 성능 개선을 위해 복잡한 3D 모델에는 LOD(Level of Detail) 시스템을 구현하십시오.
- CPU 집약적인 작업에는 Unity의 Job System과 Burst Compiler를 사용하십시오.
- 단순화된 충돌 메쉬를 사용하고 고정 타임스텝(fixed timestep)을 조정하여 물리 성능을 최적화하십시오.

## 주요 컨벤션
1. 모듈식이고 재사용 가능한 게임 요소를 위해 Unity의 컴포넌트 기반 아키텍처를 따르십시오.
2. 개발의 모든 단계에서 성능 최적화와 메모리 관리를 우선시하십시오.
3. 가독성과 에셋 관리를 향상하기 위해 명확하고 논리적인 프로젝트 구조를 유지하십시오.

스크립팅, 게임 아키텍처 및 성능 최적화의 베스트 프랙티스는 Unity 문서와 C# 프로그래밍 가이드를 참조하십시오.
-->
