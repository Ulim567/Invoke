using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레이드 방 목록 → 방 만들기·비밀번호 입장 → 파티방(정비) → 시작까지의 화면. 보여 주기와 버튼만 맡고, 방·파티원 값은 RaidRooms에 요청한다.
/// <para>
/// - 방 목록: 방마다 한 줄(RaidRoomRowUI). 「방 만들기」는 이름·비밀번호 창을 연다. 「입장」은 꽉 찬 방이면 "인원이 다 찼습니다"를 알리고, 비밀번호 방이면 비밀번호 창을 연다.<br/>
/// - 파티방: 파티원 줄(RaidMemberRowUI), 「준비」(파티원) / 「시작」(파티장, 모두 준비하면 켜짐), 「나가기」.<br/>
/// - 요청의 결과는 바로 오지 않는다 (#242, 서버가 처리). 방 목록이 바뀔 때 내 방(RaidRooms.FindRoomOf(MyName))을 보고 화면을 바꾸고, 실패는 RaidRooms.RequestFailed로 받아 안내 글자에 보여 준다.<br/>
/// - 시작하면 내 방이 목록에서 없어지고 이 화면은 닫힌다. 닫기 버튼으로 닫으면 방에서 나간다.<br/>
/// - 테스트 버튼(비워 둬도 됨): 가짜 방 만들기(방장봇, 짝수 번째는 비밀번호 1234, 3의 배수 번째는 꽉 찬 방), 가짜 파티원 넣기, 가짜 파티원 준비 바꾸기.
/// 서버에 접속하지 않았을 때만 동작하고, 접속했을 때는 숨긴다 (서버를 거치지 않고 RaidRooms를 직접 바꾸기 때문).
/// </para>
/// <para>
/// 사용법<br/>
/// - InputBlockingPanel을 물려받는다. 열린 동안 내 캐릭터의 Player·Attack 액션 맵을 비활성화하고, 닫으면 다시 활성화한다.<br/>
/// - Canvas 아래 늘 켜 둔 오브젝트에 붙이고, 네 창(방 목록, 파티방, 방 만들기 창, 비밀번호 창)은 Panel Root 아래에 둔다.<br/>
/// - 로비에서는 RaidLobbyOpener가 레이드 입구의 ↑에 OpenFor(player)를 부른다. 테스트 씬은 Open On Start를 켜면 Play하자마자 방 목록이 열린다.
/// </para>
/// </summary>
// Awake·OnDisable을 쓰지 않는 이유: InputBlockingPanel의 Awake(창 숨기기)·OnDisable(액션 맵 다시 켜기)이 불리지 않게 되기 때문
public class RaidLobbyUI : InputBlockingPanel
{
	const string FakeLeaderPrefix = "방장봇";
	const string FakeMemberPrefix = "파티원";
	const string FakePassword = "1234";

	[SerializeField, Tooltip("Play하자마자 방 목록을 연다 (테스트 씬용)")]
	bool openOnStart = true;

	[SerializeField, Tooltip("실패 이유 등 안내 글자. 비워 둬도 된다")]
	TMP_Text messageText;

	[Header("방 목록")]
	[SerializeField] GameObject roomListPanel;
	[SerializeField, Tooltip("방 줄을 넣을 부모 (Scroll View의 Content, Vertical Layout Group)")]
	RectTransform roomListContent;
	[SerializeField] RaidRoomRowUI roomRowPrefab;
	[SerializeField, Tooltip("방이 없을 때 보여 줄 글자. 비워 둬도 된다")]
	GameObject emptyText;
	[SerializeField] Button createButton;
	[SerializeField, Tooltip("방 목록 닫기. 비워 둬도 된다")]
	Button closeButton;

	[Header("방 만들기 창")]
	[SerializeField] GameObject createPopup;
	[SerializeField] TMP_InputField createNameInput;
	[SerializeField, Tooltip("Content Type을 Password로 둔다. 비우면 공개 방")]
	TMP_InputField createPasswordInput;
	[SerializeField] Button createConfirmButton;
	[SerializeField] Button createCancelButton;

	[Header("비밀번호 창")]
	[SerializeField] GameObject passwordPopup;
	[SerializeField, Tooltip("Content Type을 Password로 둔다")]
	TMP_InputField joinPasswordInput;
	[SerializeField] Button joinConfirmButton;
	[SerializeField] Button joinCancelButton;

	[Header("파티방 (정비)")]
	[SerializeField] GameObject partyPanel;
	[SerializeField, Tooltip("방 이름 글자")]
	TMP_Text partyTitleText;
	[SerializeField, Tooltip("파티원 줄을 넣을 부모 (Vertical Layout Group)")]
	RectTransform memberListContent;
	[SerializeField] RaidMemberRowUI memberRowPrefab;
	[SerializeField, Tooltip("파티원만 보이는 준비 / 준비 취소 버튼")]
	Button readyButton;
	[SerializeField] TMP_Text readyButtonText;
	[SerializeField, Tooltip("파티장만 보이는 시작 버튼. 모두 준비하면 눌린다")]
	Button startButton;
	[SerializeField] Button leaveButton;

	[Header("테스트 (임시, 비워 둬도 됨)")]
	[SerializeField] Button addFakeRoomButton;
	[SerializeField] Button addFakeMemberButton;
	[SerializeField] Button toggleFakeReadyButton;

	// 만든 줄은 지우지 않고 다시 쓴다 (목록이 바뀔 때마다 만들고 지우지 않게)
	readonly List<RaidRoomRowUI> roomRows = new List<RaidRoomRowUI>();
	readonly List<RaidMemberRowUI> memberRows = new List<RaidMemberRowUI>(RaidRooms.MaxMembers);

	// 화면이 보여 주고 있는 내 방. 「나가기」를 누르면 서버의 답을 기다리지 않고 바로 비운다
	RaidRoom myRoom;
	// 비밀번호 창을 연 방
	RaidRoom joiningRoom;
	int fakeCount;

	static string MyName => RaidRooms.MyName;

	void Start()
	{
		createButton.onClick.AddListener(OpenCreatePopup);
		if (closeButton != null) closeButton.onClick.AddListener(CloseAndLeave);
		createConfirmButton.onClick.AddListener(ConfirmCreate);
		createCancelButton.onClick.AddListener(() => createPopup.SetActive(false));
		joinConfirmButton.onClick.AddListener(ConfirmPassword);
		joinCancelButton.onClick.AddListener(() => passwordPopup.SetActive(false));
		readyButton.onClick.AddListener(ToggleReady);
		startButton.onClick.AddListener(StartRaid);
		leaveButton.onClick.AddListener(LeaveRoom);

		if (addFakeRoomButton != null) addFakeRoomButton.onClick.AddListener(AddFakeRoom);
		if (addFakeMemberButton != null) addFakeMemberButton.onClick.AddListener(AddFakeMember);
		if (toggleFakeReadyButton != null) toggleFakeReadyButton.onClick.AddListener(ToggleFakeReady);

		// 이 오브젝트는 늘 켜 두므로(InputBlockingPanel), 켜고 끌 때가 아니라 처음과 사라질 때 한 번씩 붙이고 뗀다
		RaidRooms.RoomsChanged += OnRoomsChanged;
		RaidRooms.RoomChanged += OnRoomChanged;
		RaidRooms.RequestFailed += ShowMessage;

		HideAll();
		if (openOnStart) OpenFor(null);
	}

	void OnDestroy()
	{
		RaidRooms.RoomsChanged -= OnRoomsChanged;
		RaidRooms.RoomChanged -= OnRoomChanged;
		RaidRooms.RequestFailed -= ShowMessage;
	}

	/// <summary>
	/// 레이드 화면을 열고 player의 Player·Attack 액션 맵을 비활성화한다. 방에 있으면 파티방을, 아니면 방 목록을 연다.<br/>
	/// 로비의 레이드 입구에서 RaidLobbyOpener가 부른다. 테스트 씬에서는 player 없이(null) 연다.
	/// </summary>
	public override void OpenFor(PlayerInputReader player)
	{
		base.OpenFor(player);
		ShowMessage(null);
		SetTestButtonsVisible(!RaidRooms.IsOnline);

		myRoom = RaidRooms.FindRoomOf(MyName);
		if (myRoom != null) ShowParty();
		else ShowRoomList();
	}

	// 닫기 버튼. 화면을 닫으면 방에서 나간다 (방에 들어간 채로 다른 장소에 가지 않게)
	void CloseAndLeave()
	{
		if (RaidRooms.FindRoomOf(MyName) != null)
		{
			myRoom = null;
			RaidRooms.RequestLeave();
		}
		Close();
	}

	void HideAll()
	{
		roomListPanel.SetActive(false);
		partyPanel.SetActive(false);
		createPopup.SetActive(false);
		passwordPopup.SetActive(false);
	}

	// 방 목록이 바뀌었을 때. 내 방이 생겼거나 없어졌으면 화면을 바꾼다
	void OnRoomsChanged()
	{
		if (!IsOpen) return;

		RaidRoom current = RaidRooms.FindRoomOf(MyName);
		if (current != null && current != myRoom)
		{
			// 만들기·입장이 성공했다
			myRoom = current;
			ShowParty();
			return;
		}
		if (current == null && myRoom != null)
		{
			// 「나가기」는 myRoom을 먼저 비우므로, 누르지 않았는데 내 방이 없어졌으면 파티장이 시작한 것이다
			myRoom = null;
			HideAll();
			Close();
			return;
		}
		RefreshRoomList();
	}

	// ── 방 목록 ──

	void ShowRoomList()
	{
		HideAll();
		roomListPanel.SetActive(true);
		RefreshRoomList();
	}

	void RefreshRoomList()
	{
		if (!roomListPanel.activeSelf) return;

		IReadOnlyList<RaidRoom> rooms = RaidRooms.Rooms;
		for (int i = 0; i < rooms.Count; i++)
		{
			if (i == roomRows.Count) roomRows.Add(Instantiate(roomRowPrefab, roomListContent));
			roomRows[i].gameObject.SetActive(true);
			roomRows[i].Set(rooms[i], TryJoin);
		}
		for (int i = rooms.Count; i < roomRows.Count; i++) roomRows[i].gameObject.SetActive(false);

		if (emptyText != null) emptyText.SetActive(rooms.Count == 0);
	}

	void OpenCreatePopup()
	{
		createNameInput.text = $"{MyName}의 레이드";
		if (createPasswordInput != null) createPasswordInput.text = "";
		createPopup.SetActive(true);
	}

	// 성공하면 OnRoomsChanged가 파티방으로 넘어가고, 실패하면 창을 연 채 이유를 보여 준다
	void ConfirmCreate()
	{
		ShowMessage(null);
		string password = createPasswordInput != null ? createPasswordInput.text : null;
		RaidRooms.RequestCreate(createNameInput.text, password);
	}

	// 방 줄의 입장 버튼. 꽉 찬 방이면 알리기만 하고, 비밀번호 방이면 비밀번호 창을 연다
	void TryJoin(RaidRoom room)
	{
		ShowMessage(null);
		if (room.IsFull)
		{
			ShowMessage("인원이 다 찼습니다.");
			return;
		}
		if (room.HasPassword)
		{
			joiningRoom = room;
			joinPasswordInput.text = "";
			passwordPopup.SetActive(true);
			return;
		}
		RaidRooms.RequestJoin(room, null);
	}

	void ConfirmPassword()
	{
		ShowMessage(null);
		RaidRooms.RequestJoin(joiningRoom, joinPasswordInput.text);
	}

	// ── 파티방 ──

	void ShowParty()
	{
		HideAll();
		ShowMessage(null);
		partyPanel.SetActive(true);
		RefreshParty();
	}

	void OnRoomChanged(RaidRoom room)
	{
		if (room == myRoom) RefreshParty();
	}

	void RefreshParty()
	{
		if (!partyPanel.activeSelf || myRoom == null) return;

		partyTitleText.text = myRoom.Name;
		IReadOnlyList<RaidMember> members = myRoom.Members;
		for (int i = 0; i < members.Count; i++)
		{
			if (i == memberRows.Count) memberRows.Add(Instantiate(memberRowPrefab, memberListContent));
			memberRows[i].gameObject.SetActive(true);
			memberRows[i].Set(members[i], i == 0, members[i].Name == MyName);
		}
		for (int i = members.Count; i < memberRows.Count; i++) memberRows[i].gameObject.SetActive(false);

		// 파티장은 시작, 파티원은 준비 버튼만 보인다
		bool isLeader = myRoom.Leader?.Name == MyName;
		startButton.gameObject.SetActive(isLeader);
		startButton.interactable = myRoom.AllReady;
		readyButton.gameObject.SetActive(!isLeader);
		if (!isLeader && readyButtonText != null) readyButtonText.text = IsMeReady() ? "준비 취소" : "준비";
	}

	bool IsMeReady()
	{
		IReadOnlyList<RaidMember> members = myRoom.Members;
		for (int i = 0; i < members.Count; i++)
			if (members[i].Name == MyName) return members[i].IsReady;
		return false;
	}

	void ToggleReady()
	{
		if (myRoom == null) return;
		RaidRooms.RequestSetReady(!IsMeReady());
	}

	// 성공하면 내 방이 목록에서 없어지고 OnRoomsChanged가 화면을 닫는다
	void StartRaid()
	{
		ShowMessage(null);
		RaidRooms.RequestStart();
	}

	// 서버의 답을 기다리지 않고 바로 방 목록으로 돌아간다. myRoom을 먼저 비워야 OnRoomsChanged가 시작으로 착각하지 않는다
	void LeaveRoom()
	{
		myRoom = null;
		RaidRooms.RequestLeave();
		ShowRoomList();
	}

	void ShowMessage(string message)
	{
		if (messageText != null) messageText.text = message ?? "";
	}

	// ── 테스트 (임시): 서버 없이 혼자 시험하기 ──

	void SetTestButtonsVisible(bool visible)
	{
		if (addFakeRoomButton != null) addFakeRoomButton.gameObject.SetActive(visible);
		if (addFakeMemberButton != null) addFakeMemberButton.gameObject.SetActive(visible);
		if (toggleFakeReadyButton != null) toggleFakeReadyButton.gameObject.SetActive(visible);
	}

	// 방장봇이 만든 방. 짝수 번째는 비밀번호 1234, 3의 배수 번째는 가짜 파티원으로 꽉 채운다
	void AddFakeRoom()
	{
		if (RaidRooms.IsOnline) return;
		fakeCount++;
		int n = fakeCount;
		string password = n % 2 == 0 ? FakePassword : null;
		if (!RaidRooms.TryCreate($"테스트 방 {n}", password, $"{FakeLeaderPrefix}{n}", out RaidRoom room, out _)) return;
		if (n % 3 != 0) return;
		for (int i = 1; i < RaidRooms.MaxMembers; i++)
			RaidRooms.TryJoin(room, password, $"{FakeMemberPrefix}{n}-{i}", out _);
	}

	void AddFakeMember()
	{
		if (RaidRooms.IsOnline || myRoom == null) return;
		fakeCount++;
		if (!RaidRooms.TryJoin(myRoom, null, $"{FakeMemberPrefix}{fakeCount}", out string reason)) ShowMessage(reason);
	}

	// 내 방의 가짜 파티원(나 말고 파티장도 아닌 사람)의 준비 상태를 모두 뒤집는다
	void ToggleFakeReady()
	{
		if (RaidRooms.IsOnline || myRoom == null) return;
		IReadOnlyList<RaidMember> members = myRoom.Members;
		// SetReady가 RoomChanged를 부르면서 목록을 다시 그리므로, 이름을 먼저 모은다
		List<string> names = new List<string>(members.Count);
		List<bool> next = new List<bool>(members.Count);
		for (int i = 1; i < members.Count; i++)
		{
			if (members[i].Name == MyName) continue;
			names.Add(members[i].Name);
			next.Add(!members[i].IsReady);
		}
		for (int i = 0; i < names.Count; i++) RaidRooms.SetReady(myRoom, names[i], next[i]);
	}
}
