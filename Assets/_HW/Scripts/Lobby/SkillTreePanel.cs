/// <summary>
/// 스킬 트리 창. 지금은 열고 닫기만 한다. 노드를 보여 주고 해금하는 화면은 「스킬 트리 화면 만들기」에서 이 창 안에 만든다.
/// <para>
/// 열린 동안 내 캐릭터의 Player·Attack 액션 맵이 꺼진다.
/// 닫기 버튼의 On Click()에 SkillTreePanel.Close를 연결한다.
/// </para>
/// </summary>
public class SkillTreePanel : InputBlockingPanel
{
	/// <summary>
	/// 스킬 트리 창을 연다. SkillTreeStation이 부른다.
	/// </summary>
	public void Open(PlayerInputReader player) => Show(player);
}
