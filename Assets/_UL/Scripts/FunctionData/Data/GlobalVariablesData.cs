using System.Collections.Generic;

/// <summary>
/// 플레이어가 전역 변수 파일에 선언한 전역 변수들. 캐릭터마다 하나. 저장·전송용이며, 실행할 값으로 바꾸는 일은 GlobalVariablesConverter가 한다.
/// <para>
/// 사용법<br/>
/// - 선언은 함수 몸통과 같은 선언 블록(BlockData.Declare)으로 Declarations에 넣는다.<br/>
/// - 초기값 칸에는 직접 입력한 값이나 NULL만 넣는다. 비우면 캐릭터 언어의 기본값(C: 0, NULL)으로 시작한다.<br/>
/// - 저장할 때 FunctionDataJson으로 JSON 글자열로 바꾼다.
/// </para>
/// </summary>
// 선언을 따로 만들지 않고 선언 블록을 쓰는 이유: 편집 화면이 함수 몸통의 선언 블록 UI를 그대로 쓸 수 있게 하기 위함
public class GlobalVariablesData
{
	/// <summary>
	/// 전역 변수 선언 블록들. 위에서부터 순서대로.
	/// </summary>
	public List<BlockData> Declarations { get; set; } = new List<BlockData>();
}
