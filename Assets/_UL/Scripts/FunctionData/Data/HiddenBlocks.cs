/// <summary>
/// 블록 코딩에서 플레이어에게 보이지 않게 하는 블록·함수 (#598).
/// <para>
/// - 이 목록을 보는 곳: 함수 탭(BlockEditor), 참고 탭과 스킬 트리 노드 카드(BlockReference), 스킬 트리 설명의 「여는 블록」(SkillTreeView),
///   값 칸 안내 글(SlotRules), 상점·몬스터 드롭 후보(ShopRoller).<br/>
/// - malloc, free: 숨긴다 (기획 11/19). 실행기는 이미 저장된 함수 속 malloc·free를 그대로 실행한다.<br/>
/// - dash, double_jump(common.): 블록 코딩 함수가 아니라 키 입력 이동기(←← / →→, Space)라서 숨긴다. 스킬 트리 노드로 해금하는 것은 그대로다.
/// </para>
/// </summary>
// 스킬 트리 데이터(SkillTree.asset)와 실행기는 그대로 두고 보여 주는 곳에서만 뺀다. 다시 보이게 할 때 이 파일만 고치면 되게 하려고 한곳에 모은다
public static class HiddenBlocks
{
	/// <summary>이 블록 종류를 블록 코딩 화면과 상점·드롭에서 숨기면 true (malloc, free).</summary>
	public static bool IsHidden(BlockKind kind) => kind is BlockKind.Malloc or BlockKind.Free;

	/// <summary>이 사전 정의 함수(고정 ID)를 블록 코딩 화면과 상점·드롭에서 숨기면 true (common.dash, common.double_jump).</summary>
	public static bool IsHidden(string functionId) => functionId is PredefinedFunctionIds.Dash or PredefinedFunctionIds.DoubleJump;
}
