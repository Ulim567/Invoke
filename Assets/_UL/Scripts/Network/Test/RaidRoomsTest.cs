using System;
using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// 레이드 방 서버 관리(#242) 확인용 테스트. 호스트(에디터) 하나와 시험 빌드 클라이언트 둘로 돌린다.
/// <para>
/// 사용법<br/>
/// - 에디터에서 Play → Host로 시작한다 (Skip Login In Editor 켬).<br/>
/// - 시험 빌드를 두 번 실행한다. 하나는 <c>-raidRoomsTest leader</c>, 다른 하나는 <c>-raidRoomsTest member</c>를 붙인다.
/// 서버 주소는 <c>-raidRoomsTestAddress 주소</c>로 바꾼다 (기본 localhost).<br/>
/// - 인자가 없으면 아무것도 하지 않으므로 씬에 놓지 않는다.
/// </para>
/// <para>
/// - 두 클라이언트 모두 「로그인 건너뛰기」 임시 계정으로 들어간다. 에디터에서 켠 서버만 받아들인다.<br/>
/// - leader: 비밀번호 방 만들기 → 파티원 입장·준비가 보이면 시작 → member가 만들고 끊은 방이 사라지는지 확인<br/>
/// - member: leader의 방이 보이는지 → 틀린 비밀번호 거절 → 입장 → 준비 → 시작으로 방이 사라지는지 → 방을 하나 만들고 접속 끊기<br/>
/// - 「[RaidRoomsTest][역할] 전체 통과」면 통과. 끝나면 프로그램을 닫는다.
/// </para>
/// </summary>
public class RaidRoomsTest : MonoBehaviour
{
	const string RoleArg = "-raidRoomsTest";
	const string AddressArg = "-raidRoomsTestAddress";
	const string RoomName = "RaidRoomsTest";
	const string Password = "1234";
	// member가 접속을 끊기 전에 만드는 방. 끊기면 서버가 빼서 없어져야 한다
	const string QuitRoomName = "RaidRoomsTestQuit";
	const float TimeoutSeconds = 20f;

	string role;
	int failedCount;
	string lastFailure;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	static void CreateIfRequested()
	{
		string role = GetArg(RoleArg);
		if (role == null) return;

		var go = new GameObject(nameof(RaidRoomsTest));
		DontDestroyOnLoad(go);
		go.AddComponent<RaidRoomsTest>().role = role;
	}

	IEnumerator Start()
	{
		RaidRooms.RequestFailed += reason => lastFailure = reason;

		yield return Connect();
		if (failedCount == 0)
		{
			if (role == "leader") yield return RunLeader();
			else if (role == "member") yield return RunMember();
			else Fail($"모르는 역할: {role}");
		}

		Log(failedCount == 0 ? "전체 통과" : $"실패 {failedCount}개");
		Application.Quit();
	}

	IEnumerator Connect()
	{
		yield return Wait("NetworkManager", () => NetworkManager.singleton != null);
		NetworkManager.singleton.networkAddress = GetArg(AddressArg) ?? "localhost";
		NetworkManager.singleton.StartClient();
		yield return Wait("접속", () => NetworkClient.isConnected);

		NetworkClient.Send(new AccountAuthRequest { skipLogin = true });
		yield return Wait("로그인", () => NetworkClient.connection != null && NetworkClient.connection.isAuthenticated);
		InvokeAuthenticator.Current.RequestEnter(CharacterLanguages.C);
		// 서버는 플레이어를 만든 뒤 방 목록을 보내고, 목록에 내 닉네임이 들어 있다
		yield return Wait("게임에 들어가고 내 닉네임 받기", () => NetworkClient.localPlayer != null && RaidRooms.MyName != "나");
		if (failedCount == 0) Pass($"게임에 들어감 (MyName={RaidRooms.MyName})");
	}

	IEnumerator RunLeader()
	{
		RaidRooms.RequestCreate(RoomName, Password);
		yield return Wait("방 만들기", () => RaidRooms.FindRoomOf(RaidRooms.MyName) != null);
		RaidRoom room = RaidRooms.FindRoomOf(RaidRooms.MyName);
		if (room == null) yield break;
		Check("만든 방이 내 방이고 내가 파티장", room.Name == RoomName && room.Leader.Name == RaidRooms.MyName && room.HasPassword);

		// RoomChanged로 바뀐 방이 같은 객체여야 화면이 들고 있는 방으로 그대로 다시 그린다
		bool sameRoomChanged = false;
		Action<RaidRoom> onChanged = r => { if (r == room && r.Members.Count == 2) sameRoomChanged = true; };
		RaidRooms.RoomChanged += onChanged;
		yield return Wait("파티원 입장·준비가 보임", () => room.Members.Count == 2 && room.Members[1].IsReady);
		RaidRooms.RoomChanged -= onChanged;
		Check("파티원이 들어온 방이 같은 RaidRoom 객체로 RoomChanged", sameRoomChanged);

		RaidRooms.RequestStart();
		yield return Wait("시작하면 방이 목록에서 사라짐", () => FindRoom(RoomName) == null);

		yield return Wait("파티원이 끊기 전에 만든 방이 보임", () => FindRoom(QuitRoomName) != null);
		yield return Wait("파티원이 끊기면 그 방이 사라짐", () => FindRoom(QuitRoomName) == null);
	}

	IEnumerator RunMember()
	{
		yield return Wait("다른 사람의 방이 보임", () => FindRoom(RoomName) != null);
		RaidRoom room = FindRoom(RoomName);
		if (room == null) yield break;
		Check("비밀번호 방으로 보임", room.HasPassword);

		lastFailure = null;
		RaidRooms.RequestJoin(room, "wrong");
		yield return Wait("틀린 비밀번호 거절", () => lastFailure != null);
		Check("거절 이유", lastFailure == "비밀번호가 틀렸습니다.");

		RaidRooms.RequestJoin(room, Password);
		yield return Wait("입장", () => RaidRooms.FindRoomOf(RaidRooms.MyName) == room);

		RaidRooms.RequestSetReady(true);
		yield return Wait("준비", () => room.Members.Count == 2 && room.Members[1].IsReady);

		yield return Wait("파티장이 시작하면 방이 사라짐", () => FindRoom(RoomName) == null && RaidRooms.FindRoomOf(RaidRooms.MyName) == null);

		RaidRooms.RequestCreate(QuitRoomName, "");
		yield return Wait("끊기 전에 방 만들기", () => RaidRooms.FindRoomOf(RaidRooms.MyName) != null);
		// leader가 방을 본 뒤에 끊는다
		yield return new WaitForSeconds(2f);
	}

	IEnumerator Wait(string step, Func<bool> condition)
	{
		if (failedCount > 0) yield break;

		float until = Time.realtimeSinceStartup + TimeoutSeconds;
		while (!condition())
		{
			if (Time.realtimeSinceStartup > until)
			{
				Fail($"{step}: {TimeoutSeconds}초 안에 되지 않음");
				yield break;
			}
			yield return null;
		}
		Pass(step);
	}

	void Check(string step, bool ok)
	{
		if (ok) Pass(step);
		else Fail(step);
	}

	static RaidRoom FindRoom(string roomName)
	{
		foreach (RaidRoom room in RaidRooms.Rooms)
			if (room.Name == roomName) return room;
		return null;
	}

	static string GetArg(string name)
	{
		string[] args = Environment.GetCommandLineArgs();
		int i = Array.IndexOf(args, name);
		return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
	}

	void Pass(string step) => Log($"통과: {step}");

	void Fail(string step)
	{
		failedCount++;
		Debug.LogError($"[RaidRoomsTest][{role}] 실패: {step}");
	}

	void Log(string message) => Debug.Log($"[RaidRoomsTest][{role}] {message}");
}
