/// <summary>
/// 블럭코딩 편집 화면을 여닫는 창. 연습장의 편집 터미널(PanelStation)이 연다.
/// <para>
/// 사용법: BlockEditorCanvas 최상위에 붙이고, Panel Root에 편집 화면 전체(처음에는 꺼 둠)를 넣는다.<br/>
/// - 열린 동안 Player·Attack 액션 맵이 꺼져서, 블록 이름을 적는 동안 캐릭터가 움직이거나 스킬이 나가지 않는다.<br/>
/// - 닫기 버튼의 On Click()에 BlockEditorPanel.Close를 연결한다.<br/>
/// - 저장하지 않은 변경이 있으면 닫기 버튼과 ESC 모두 바로 닫지 않고 확인창을 먼저 띄운다 (#432). 「예」를 누르면 저장하지 않은 내용을 버리고 닫는다.
/// </para>
/// </summary>
public class BlockEditorPanel : InputBlockingPanel
{
	BlockEditor editor;

	/// <summary>
	/// 편집 화면을 열고, 내 캐릭터가 실제 계정이면 서버의 슬롯 함수·페이지 함수 목록으로 다시 그린다.
	/// </summary>
	public override void OpenFor(PlayerInputReader player)
	{
		base.OpenFor(player);
		if (editor == null) editor = GetComponentInChildren<BlockEditor>(true);
		if (editor != null) editor.Refresh();
	}

	/// <summary>
	/// 저장하지 않은 변경이 있으면 확인창을 먼저 띄우고, 없거나 「예」를 누르면 닫는다.
	/// </summary>
	public override void Close()
	{
		if (editor == null) editor = GetComponentInChildren<BlockEditor>(true);
		if (editor == null)
		{
			base.Close();
			return;
		}
		editor.RequestClose(base.Close);
	}
}
