/// <summary>
/// 블럭코딩 편집 화면을 여닫는 창. 연습장의 편집 터미널(PanelStation)이 연다.
/// <para>
/// 사용법: BlockEditorCanvas 최상위에 붙이고, Panel Root에 편집 화면 전체(처음에는 꺼 둠)를 넣는다.<br/>
/// - 열린 동안 Player·Attack 액션 맵이 꺼져서, 블록 이름을 적는 동안 캐릭터가 움직이거나 스킬이 나가지 않는다.<br/>
/// - 닫기 버튼의 On Click()에 BlockEditorPanel.Close를 연결한다.
/// </para>
/// </summary>
public class BlockEditorPanel : InputBlockingPanel
{
}
