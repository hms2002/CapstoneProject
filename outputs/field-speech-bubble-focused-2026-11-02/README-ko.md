# 기존 말풍선과 첨부 색상 읽기 ON — 두 버전 집중 비교

왼쪽은 기존 말풍선, 오른쪽은 첨부 PNG의 색상 읽기를 켠 모습이다. 두 버전 모두 현재 셰이더 자동 외곽선은 켜져 있으며 같은 크기·테마·배경을 사용한다.

- [기본색 전체·모서리 확대 비교](comparison-closeup-default.png)
- [슬라임색 전체·모서리 확대 비교](comparison-closeup-slime.png)
- [색상 5종 — 어두운 배경](comparison-all-colors-dark.png)
- [색상 5종 — 밝은 배경](comparison-all-colors-light.png)

첨부 색상 읽기 ON에서는 직접 그린 어두운 선과 아래 음영이 보인다. 그 바깥에는 기존 자동 외곽선도 남는다. 색상 테마를 바꾸면 추가된 선과 음영 역시 배경 테마색을 따라 어두워진다. 기존보다 선이 두껍고 아래 변에 입체감이 생기는 차이를 중심으로 보면 된다.

확대 비교의 위쪽은 이전 Unity 원본 렌더를 2배, 아래 모서리는 같은 원본 렌더의 표시 영역을 6배 확대했다. 보간으로 픽셀을 부드럽게 만들지 않았다. 노란 사각형은 확대할 부분을 표시하는 안내다.

이번에는 기존 검증의 `color_*_*_0.png`와 `color_*_*_2.png`만 재배치했다. 새로운 Unity 실행·컴파일·Play Mode는 수행하지 않았다. 이전 자료의 다른 방식이나 권장 조합은 비교 대상에 넣지 않았다. 검증 범위는 이전과 동일하게 배경 셰이더이며, 실제 씬·TMP 문장·카메라·아틀라스·Player build 전체 검증은 아니다.

생성 파일은 비교 이미지 4장, 이 설명과 재현용 `build-focused-comparison.ps1`, ZIP이다. 게임 에셋·코드와 기존 Docs는 변경하지 않았다. SessionLogs/ErrorLog/DecisionLog 미갱신. Doc Impact Check: no doc update needed, Presentation HTML stale 후보 없음.
