using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mirror;
using UnityEngine;

/// <summary>
/// 실제 계정 슬롯 테스트 (#263). Test_JW의 ServerSlotTest. 켜고 Play → Host (약 3초).
/// <para>
/// 서버가 보낸 것처럼 슬롯 함수 8개(이 컴퓨터 파일에 없는 새 GUID)를 만들어 SlotLoadout.SetEntries로 넣는다.<br/>
/// 1. 넣으면 Changed(-1), UsesServerData, 슬롯 8칸이 받은 GUID, 파일로 덮어쓰지 않음<br/>
/// 2. 슬롯 원본을 받은 함수 목록에서 만듦 (같은 슬롯은 같은 원본), 실행기로 시전하면 Completed, CodeSize 계산<br/>
/// 3. 장착 바꾸기(EquipSkill, EquipFunction, Unequip, Swap) 거부, SlotLoadout.json을 쓰지 않음<br/>
/// 4. ReloadFunctions 뒤에도 받은 목록으로 다시 만듦, 함수를 저장해 다시 SetEntries하면 새 원본<br/>
/// 5. UseLocalFiles로 원래 장착 내용으로 돌아옴<br/>
/// 「[ServerSlotTest] 전체 통과」면 통과. 평소에는 꺼 둔다.<br/>
/// 테스트 씬은 임시 계정이라 Changed 때 FunctionExecutorNetwork가 이 컴퓨터 파일을 서버로 다시 보낸다. 그래서 「슬롯 N의 함수를 받지 못했습니다」 경고 8줄과 SlotLoadout 거부 경고 4줄은 정상이다 (실제 계정은 다시 보내지 않음).
/// </para>
/// </summary>
public class SlotServerDataSimTest : MonoBehaviour
{
	const string Tag = "[ServerSlotTest]";

	int failed;

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1f);

		GameObject player = NetworkClient.localPlayer.gameObject;
		SlotLoadout loadout = player.GetComponent<SlotLoadout>();
		FunctionExecutor executor = player.GetComponent<FunctionExecutor>();
		if (loadout == null || executor == null)
		{
			Debug.LogError($"{Tag} 플레이어에 SlotLoadout, FunctionExecutor가 있어야 함");
			yield break;
		}

		// 원래 장착 내용과 저장 파일 상태
		SlotLoadout.Entry[] before = new SlotLoadout.Entry[SlotLoadout.SlotCount];
		for (int i = 0; i < before.Length; i++) before[i] = loadout.GetEntry(i);
		string savePath = Path.Combine(Application.persistentDataPath, "SlotLoadout.json");
		string fileBefore = File.Exists(savePath) ? File.ReadAllText(savePath) : null;

		// 서버가 보낸 것처럼: 슬롯 함수 8개 + 페이지 함수 1개. 모두 이 컴퓨터 파일에 없는 새 GUID
		List<FunctionData> functions = new List<FunctionData>();
		SlotLoadout.Entry[] slots = new SlotLoadout.Entry[SlotLoadout.SlotCount];
		for (int i = 0; i < slots.Length; i++)
		{
			FunctionData slotFunction = FunctionData.Create($"slot{i + 1}");
			functions.Add(slotFunction);
			slots[i] = SlotLoadout.Entry.Function(slotFunction.Guid);
		}
		functions.Add(FunctionData.Create("page1"));
		Check("테스트 함수가 이 컴퓨터 파일에 없음", FunctionStorage.Load(slots[0].Id) == null);

		// 1. 넣기
		int changedAll = 0;
		System.Action<int> onChanged = slot => { if (slot == -1) changedAll++; };
		loadout.Changed += onChanged;
		loadout.SetEntries(slots, functions);
		Check("SetEntries: Changed(-1)", changedAll == 1);
		Check("SetEntries: UsesServerData", loadout.UsesServerData);
		bool same = true;
		for (int i = 0; i < slots.Length; i++)
		{
			SlotLoadout.Entry e = loadout.GetEntry(i);
			if (e.Kind != SlotLoadout.EntryKind.UserFunction || e.Id != slots[i].Id) same = false;
		}
		Check("SetEntries: 슬롯 8칸이 받은 GUID (파일로 덮어쓰지 않음)", same);

		// 2. 원본·시전·크기
		bool built = loadout.TryGetFunction(0, out FunctionBlock first);
		Check("슬롯 원본: 받은 함수 목록에서 만듦", built && first is UserFunction);
		loadout.TryGetFunction(0, out FunctionBlock again);
		Check("슬롯 원본: 같은 슬롯은 같은 원본", built && ReferenceEquals(first, again));
		if (built)
		{
			FunctionTask task = executor.Cast(first);
			yield return WaitUntil(() => task == null || task.IsEnded, 2f);
			Check($"시전: Completed (실제 {task?.EndReason})", task != null && task.EndReason == FunctionTaskEndReason.Completed);
		}
		List<string> guids = new List<string>();
		foreach (SlotLoadout.Entry e in slots) guids.Add(e.Id);
		int expectedSize = CodeSizeCalculator.Calculate(guids, new string[0], functions);
		Check($"CodeSize: 받은 목록으로 계산 ({loadout.CodeSize} / 기대 {expectedSize})", loadout.CodeSize == expectedSize);

		// 3. 장착 바꾸기 거부, 파일 안 씀
		Check("EquipSkill 거부", !loadout.EquipSkill(1, PredefinedFunctionIds.Thrust));
		Check("EquipFunction 거부", !loadout.EquipFunction(1, slots[2].Id));
		loadout.Unequip(1);
		loadout.Swap(0, 1);
		Check("Unequip·Swap 해도 슬롯 그대로", loadout.GetEntry(0).Id == slots[0].Id && loadout.GetEntry(1).Id == slots[1].Id);
		string fileNow = File.Exists(savePath) ? File.ReadAllText(savePath) : null;
		Check("SlotLoadout.json을 쓰지 않음", fileNow == fileBefore);

		// 4. 다시 만들기
		loadout.ReloadFunctions();
		bool rebuilt = loadout.TryGetFunction(0, out FunctionBlock afterReload);
		Check("ReloadFunctions 뒤에도 받은 목록으로 다시 만듦", rebuilt && afterReload != null && !ReferenceEquals(afterReload, first));
		// 편집 화면에서 slot1을 저장해 서버가 새 내용을 보낸 것처럼: 같은 GUID의 새 데이터
		FunctionData saved = FunctionData.Create("slot1");
		saved.Guid = slots[0].Id;
		functions[0] = saved;
		// ReloadFunctions도 Changed(-1)를 부르므로 여기서 다시 센다
		int changedBefore = changedAll;
		loadout.SetEntries(slots, functions);
		loadout.TryGetFunction(0, out FunctionBlock afterSave);
		Check("다시 SetEntries하면 새 원본", afterSave != null && !ReferenceEquals(afterSave, afterReload));
		Check("다시 SetEntries: Changed(-1)", changedAll == changedBefore + 1);

		// 5. 되돌리기
		loadout.UseLocalFiles();
		bool restored = !loadout.UsesServerData;
		for (int i = 0; i < before.Length; i++)
		{
			SlotLoadout.Entry e = loadout.GetEntry(i);
			if (e.Kind != before[i].Kind || e.Id != before[i].Id) restored = false;
		}
		Check("UseLocalFiles: 원래 장착 내용으로 돌아옴", restored);
		loadout.Changed -= onChanged;

		if (failed == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} {failed}개 실패");
	}

	static IEnumerator WaitUntil(System.Func<bool> done, float timeout)
	{
		float end = Time.time + timeout;
		while (!done() && Time.time < end) yield return null;
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"{Tag} {label}: 통과");
		else
		{
			failed++;
			Debug.LogError($"{Tag} {label}: 실패");
		}
	}
}
