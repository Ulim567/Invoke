using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// 소환검·검기 동기화 확인용 슬롯 넣기 (#229). Test_JW의 SwordSyncSlots. 켜 두면 내 캐릭터의 슬롯 Q W E R에 검 스킬을 넣는다.
/// <para>
/// - Q 검 소환, W 소환검 던지기, E 검 던지기, R 검 돌아오기. 검 휘두르기(검기)는 A, 대시는 S, 검 타기는 D에 넣는다 (#256).<br/>
/// - 에디터 Host와 빌드 Client 모두 각자 자기 캐릭터에 넣는다. 슬롯 저장 파일은 컴퓨터마다 하나라 둘이 같은 슬롯을 쓴다.<br/>
/// - Restore Slots가 켜져 있으면 Play를 멈출 때(오브젝트가 사라질 때) 원래 슬롯으로 되돌린다.<br/>
/// - 판정은 사람이 두 화면을 보고 한다. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class SwordSyncSlotsTest : MonoBehaviour
{
	const string Tag = "[SwordSyncSlots]";

	static readonly string[] Skills =
	{
		PredefinedFunctionIds.SummonSword, PredefinedFunctionIds.SummonedThrow, PredefinedFunctionIds.SwordThrow, PredefinedFunctionIds.SwordReturn,
		PredefinedFunctionIds.SwordSlash, PredefinedFunctionIds.Dash, PredefinedFunctionIds.SwordRide,
	};

	[Tooltip("Play를 멈출 때 원래 슬롯으로 되돌린다")]
	[SerializeField] bool restoreSlots = true;

	SlotLoadout loadout;
	SlotLoadout.Entry[] backup;

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(0.5f);
		loadout = NetworkClient.localPlayer.GetComponent<SlotLoadout>();
		if (loadout == null)
		{
			Debug.LogError($"{Tag} 내 캐릭터에 SlotLoadout이 없음");
			yield break;
		}

		backup = new SlotLoadout.Entry[Skills.Length];
		for (int i = 0; i < Skills.Length; i++)
		{
			backup[i] = loadout.GetEntry(i);
			if (!loadout.EquipSkill(i, Skills[i])) Debug.LogError($"{Tag} 슬롯 {i + 1}에 {Skills[i]}를 넣지 못함");
		}
		Debug.Log($"{Tag} Q 검 소환, W 소환검 던지기, E 검 던지기, R 검 돌아오기, A 검 휘두르기, S 대시, D 검 타기를 넣음");
	}

	void OnDestroy()
	{
		// Play를 멈출 때는 캐릭터가 먼저 사라졌을 수 있어 Unity의 null 검사 대신 참조만 본다. 저장 파일만 되돌리면 되므로 그 뒤의 알림에서 나는 오류는 무시한다
		if (!restoreSlots || ReferenceEquals(loadout, null) || backup == null) return;
		for (int i = 0; i < backup.Length; i++)
		{
			SlotLoadout.Entry entry = backup[i];
			try
			{
				if (entry.IsEmpty) loadout.Unequip(i);
				else if (entry.Kind == SlotLoadout.EntryKind.Skill) loadout.EquipSkill(i, entry.Id);
				else loadout.EquipFunction(i, entry.Id);
			}
			catch (System.Exception) { }
		}
	}
}
