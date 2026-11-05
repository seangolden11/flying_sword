# Unity 프로젝트 작업 지침

## 씬에 저장된 설정 변경

- SerializeField 값은 코드 기본값보다 씬·프리팹에 저장된 값이 우선한다. 기본값만 수정하고 적용됐다고 보고하지 않는다.
- Unity가 열려 있을 때 씬 YAML만 외부에서 수정하면 에디터가 기억하는 이전 값으로 덮어쓸 수 있다. 열린 씬의 편집 내용을 보존한 뒤 Unity의 SerializedObject로 값을 적용하고 씬을 저장한다.
- 공통 설정은 Combat과 Metroidvania 두 씬 모두 수정한다. EnemySystem 기본값과 StageExpansion 등 씬 재생성 도구의 설정도 함께 맞춘다.
- 적 시야 범위 변경에는 Flying Sword > Set Enemy Range 메뉴를 사용한다. EnemyRangeSettings의 목표값과 메뉴 이름을 요청한 값으로 갱신한 뒤 실행한다.
- Unity에서 저장한 뒤 실제 씬 파일의 detectionRange 값을 두 씬 모두 읽어 확인한다. 저장 전 파일 편집 결과나 스크립트 기본값만 보고 완료라고 말하지 않는다.
- 현재 적 시야 범위는 10 유닛이다. 범위 원은 적·보스의 크기와 관계없이 실제 판정 반경과 같아야 한다.
- 사용자가 검증을 생략하라고 하면 Play나 전체 회귀 테스트를 실행하지 않는다. 요청한 값의 적용·저장 확인만 한다.
