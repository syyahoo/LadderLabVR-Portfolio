# LadderLab VR

Unity와 C#으로 제작한 가상 PLC 기반 제어 교육 시뮬레이션의 개인 코드 포트폴리오입니다.

[프로젝트 설명과 시연 영상](https://ohsungyoung-game.netlify.app/#ladder)

![LadderLab VR](https://ohsungyoung-game.netlify.app/assets/ladderlab-poster.jpg)

## 프로젝트와 담당 범위

- 개인 프로젝트: 기획, 기능·동작 조건 정의, 구현 및 동작 검증
- 프로젝트 기간: 2026년 6~7월
- Unity와 C#으로 가상 PLC의 X/Y/M 신호, 래더 로직, 배선, 장비 배치와 동작 구현
- Codex를 구현 보조 도구로 활용하고, 본인이 Unity에서 시험하며 수정 방향을 결정

## 먼저 볼 코드

| 기능 | 코드 |
| --- | --- |
| 입력·출력·내부 릴레이 상태 | [PlcMemory.cs](Assets/02_Scripts/PLC/PlcMemory.cs) |
| PLC 스캔과 실행 상태 | [PlcScanController.cs](Assets/02_Scripts/PLC/PlcScanController.cs) |
| 래더 프로그램과 실행 | [LadderProgramData.cs](Assets/02_Scripts/PLC/LadderProgramData.cs), [LadderRungData.cs](Assets/02_Scripts/PLC/LadderRungData.cs) |
| PLC 주소와 결선 | [WiringCabinetController.cs](Assets/02_Scripts/Wiring/WiringCabinetController.cs) |
| 가상 설비 동작 | [ProcessingEquipmentCycleDevice.cs](Assets/02_Scripts/Devices/ProcessingEquipmentCycleDevice.cs) |
| 장비 배치 | [EquipmentPlacementController.cs](Assets/02_Scripts/Placement/EquipmentPlacementController.cs) |

동작 흐름은 센서 입력, PLC 스캔, 출력 신호, 가상 장비 동작, 완료 신호 반환으로 이어집니다. 가상 PLC 학습용이며 실제 PLC 연동 실적으로 제시하지 않습니다. `ModbusPlcBackend`는 미구현 확장 지점으로, 실제 PLC 연결 기능이 아닙니다.

## 포함 범위와 환경

`Assets/02_Scripts`의 자체 런타임 C# 코드를 원래 경로로 발췌했습니다. 외부 공장 모델, 텍스처, 폰트, Unity 샘플, Scene·Prefab, 프로젝트 설정과 원본 Git 이력은 포함하지 않았습니다.

검토한 원본 스냅샷의 환경은 Unity 6000.4.11f1, OpenXR, XR Interaction Toolkit 3.5.1, Input System입니다. 이 코드 발췌본은 외부 패키지·에셋 및 Inspector 연결이 생략되어 있으며, 저장소만으로 원본 프로젝트 전체가 실행되지는 않습니다.

## 검토 기록

공개 대상 코드의 문자열·인증정보 패턴, 외부 코드 표시, 참조 경로를 점검했습니다. 핵심 PLC 실행과 결선 코드를 읽고 설명 범위를 확인했습니다. 공개본에서 Unity 컴파일·Play Mode·기기 시험을 새로 수행한 것은 아닙니다. 자세한 파일 목록은 [PUBLICATION_MANIFEST.json](PUBLICATION_MANIFEST.json)을 참고하세요.

## 이용 범위

이 저장소는 코드 열람과 기술 평가를 위한 포트폴리오입니다. 별도 오픈소스 라이선스를 부여하지 않았으며, 외부 라이브러리·에셋의 권리는 원 저작자에게 있습니다.
