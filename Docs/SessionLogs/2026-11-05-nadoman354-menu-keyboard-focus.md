# 메뉴의 첫 키보드 포커스

타이틀에서 첫 버튼을 자동으로 선택한 채 다른 버튼에 마우스를 올리면 선택 표시가 두 곳에 남았다. 메뉴를 열 때는 기본 선택을 비워 두고, 화면이 받을 수 있는 첫 방향 입력으로 탐색을 시작하도록 변경했다.

## 변경 파일

- [MenuButtonHighlightPresentation.cs](../../Assets/_Project/Runtime/UI/Common/MenuButtonHighlightPresentation.cs). 공통 탐색 그룹이 첫 방향 입력에서만 포커스를 얻는다. 키보드로 탐색할 때에는 정지한 마우스 아래의 검·색·스프라이트 호버 표시를 숨긴다. 마우스 이동·클릭·스크롤은 해당 그룹의 선택을 해제하고 호버를 복원한다. 타이틀, 일시정지, 레벨업 카드에 적용된다.
- [SettingsPanelUI.cs](../../Assets/_Project/Runtime/UI/Settings/SettingsPanelUI.cs). 타이틀과 게임 내 설정의 기존 버튼·슬라이더를 같은 입력 전환에 연결했다. 첫 방향키는 조작 가능한 첫 컨트롤에 포커스만 둔다. 이후 이동과 슬라이더 조정은 기존 uGUI 탐색을 사용한다. 닫기·키 설정 열기·비활성화 시 포커스를 해제한다.
- [TitleLanguageMenuUI.cs](../../Assets/_Project/Runtime/UI/Title/TitleLanguageMenuUI.cs). 현재 언어 체크 표시는 열리는 즉시 보이고, 해당 언어 행의 키보드 강조는 첫 세로 탐색 입력에서 시작된다. 목록을 닫아도 버튼을 즉시 선택하지 않는다.
- [UiKeyboardNavigationRegression.cs](../../Tools/Validation/UiKeyboardNavigationRegression.cs). 기존 검증 소스의 메뉴·카드·Space 시나리오를 새 포커스 진입 시점에 맞추고 마우스 전환 시 선택 해제와 호버 색 복원을 검사하도록 수정했다.
- [InputBindings.md](../StructureMemory/InputBindings.md)에 그룹별 입력과 포커스 수명을, [LocalizationFlow.md](../StructureMemory/LocalizationFlow.md)에 언어 목록의 체크와 포커스 동작을 반영했다.

## 확인과 남은 점

- 변경한 런타임 소스 세 개를 포함한 `UI.csproj`의 MSBuild가 오류 0개, 경고 84개로 완료됐다. 타이틀 씬과 메인 GlobalUIRoot 프리팹 각각에서 설정 컨트롤 참조 14개와 기존 Automatic 탐색을 확인했다. 런타임 스크립트의 직렬화 필드 선언, 변경 문서의 로컬 링크와 diff 공백을 확인했다.
- 자동 테스트와 Unity Play Mode는 실행하지 않았다. 수동 확인은 타이틀에서 커서를 ‘설정’에 둔 채 아래 방향키를 한 번 눌러 ‘새 게임’만 강조되는지, 마우스를 다시 움직이면 ‘설정’만 강조되는지 보는 것이다. 설정을 열었을 때 첫 방향키가 값을 바꾸지 않고 포커스만 표시하는지도 확인 대상이다.
- 문서 영향은 SessionLog와 StructureMemory다. 이번 변경으로 ErrorLog·DecisionLog·Presentation HTML을 갱신할 항목은 없다.
