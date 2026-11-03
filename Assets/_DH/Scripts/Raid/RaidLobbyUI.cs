using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레이드 방 목록 → 방 만들기·비밀번호 입장 → 파티방(정비) → 시작까지의 화면. 보여 주기와 버튼만 맡고, 방·파티원 값은 RaidRooms에 요청한다.
/// <para>
/// - 방 목록: 방마다 한 줄(RaidRoomRowUI). 「방 만들기」는 이름·비밀번호 창을 연다. 「입장」은 꽉 찬 방이면 "인원이 다 찼습니다"를 알리고, 비밀번호 방이면 비밀번호 창을 연다.<br/>
/// - 파티방: 파티원 줄(RaidMemberRowUI), 「준비」(파티원) / 「시작」(파티장, 모두 준비하면 켜짐), 「나가기」.<br/>
/// - 시작하면 RaidRooms가 RaidFlow.RequestStart(레이드 시작)를 부르고, 이 화면은 닫힌다.<br/>
/// - 테스트 버튼(비워 둬도 됨): 가짜 방 만들기(방장봇, 짝수 번째는 비밀번호 1234, 3의 배수 번째는 꽉 찬 방), 가짜 파티원 넣기, 가짜 파티원 준비 바꾸기. 멀티 없이 혼자 시험하기 위한 임시 기능이다.
/// </para>
/// <para>
/// 사용법: Canvas 아래 오브젝트에 붙이고 부품을 넣는다. 창(Panel·Popup)은 이 오브젝트의 자식으로 둔다.
/// 로비 입구와 잇는 일은 나중에 한다. 지금은 Open On Start를 켜면 Play하자마자 방 목록이 열린다.
/// </para>
/// </summary>
public class RaidLobbyUI : MonoBehaviour
{
	const string FakeLeaderPrefix = "방장봇";
	const string FakeMemberPrefix = "파티원";
	const string FakePassword = "1234";

	[SerializeField, Tooltip("내 이름 (임시). 나중에 고른 캐릭터 이름으로 바꾼다")]
	string localName = "나";

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

	RaidRoom myRoom;
	// 비밀번호 창을 연 방
	RaidRoom joiningRoom;
	int fakeCount;

	void Awake()
	{
		createButton.onClick.AddListener(OpenCreatePopup);
		if (closeButton != null) closeButton.onClick.AddListener(Close);
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

		HideAll();
	}

	void OnEnable()
	{
		RaidRooms.RoomsChanged += RefreshRoomList;
		RaidRooms.RoomChanged += OnRoomChanged;
	}

	void OnDisable()
	{
		RaidRooms.RoomsChanged -= RefreshRoomList;
		RaidRooms.RoomChanged -= OnRoomChanged;
	}

	void Start()
	{
		if (openOnStart) Open();
	}

	/// <summary>
	/// 레이드 화면을 연다. 이미 방에 있으면 파티방을, 아니면 방 목록을 연다. 로비의 레이드 입구가 부른다.
	/// </summary>
	public void Open()
	{
		ShowMessage(null);
		myRoom = RaidRooms.FindRoomOf(localName);
		if (myRoom != null) ShowParty();
		else ShowRoomList();
	}

	/// <summary>
	/// 레이드 화면을 닫는다. 들어가 있던 방에서는 나가지 않는다.
	/// </summary>
	public void Close() => HideAll();

	void HideAll()
	{
		roomListPanel.SetActive(false);
		partyPanel.SetActive(false);
		createPopup.SetActive(false);
		passwordPopup.SetActive(false);
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
		createNameInput.text = $"{localName}의 레이드";
		if (createPasswordInput != null) createPasswordInput.text = "";
		createPopup.SetActive(true);
	}

	void ConfirmCreate()
	{
		string password = createPasswordInput != null ? createPasswordInput.text : null;
		if (!RaidRooms.TryCreate(createNameInput.text, password, localName, out RaidRoom room, out string reason))
		{
			ShowMessage(reason);
			return;
		}
		myRoom = room;
		ShowParty();
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
		Join(room, null);
	}

	void ConfirmPassword() => Join(joiningRoom, joinPasswordInput.text);

	void Join(RaidRoom room, string password)
	{
		if (!RaidRooms.TryJoin(room, password, localName, out string reason))
		{
			ShowMessage(reason);
			return;
		}
		myRoom = room;
		ShowParty();
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
			memberRows[i].Set(members[i], i == 0, members[i].Name == localName);
		}
		for (int i = members.Count; i < memberRows.Count; i++) memberRows[i].gameObject.SetActive(false);

		// 파티장은 시작, 파티원은 준비 버튼만 보인다
		bool isLeader = myRoom.Leader?.Name == localName;
		startButton.gameObject.SetActive(isLeader);
		startButton.interactable = myRoom.AllReady;
		readyButton.gameObject.SetActive(!isLeader);
		if (!isLeader && readyButtonText != null) readyButtonText.text = IsMeReady() ? "준비 취소" : "준비";
	}

	bool IsMeReady()
	{
		IReadOnlyList<RaidMember> members = myRoom.Members;
		for (int i = 0; i < members.Count; i++)
			if (members[i].Name == localName) return members[i].IsReady;
		return false;
	}

	void ToggleReady() => RaidRooms.SetReady(myRoom, localName, !IsMeReady());

	void StartRaid()
	{
		if (!RaidRooms.TryStart(myRoom, localName, out string reason))
		{
			ShowMessage(reason);
			return;
		}
		myRoom = null;
		HideAll();
	}

	void LeaveRoom()
	{
		RaidRooms.Leave(myRoom, localName);
		myRoom = null;
		ShowRoomList();
	}

	void ShowMessage(string message)
	{
		if (messageText != null) messageText.text = message ?? "";
	}

	// ── 테스트 (임시): 멀티 없이 혼자 시험하기 ──

	// 방장봇이 만든 방. 짝수 번째는 비밀번호 1234, 3의 배수 번째는 가짜 파티원으로 꽉 채운다
	void AddFakeRoom()
	{
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
		if (myRoom == null) return;
		fakeCount++;
		if (!RaidRooms.TryJoin(myRoom, null, $"{FakeMemberPrefix}{fakeCount}", out string reason)) ShowMessage(reason);
	}

	// 내 방의 가짜 파티원(나 말고 파티장도 아닌 사람)의 준비 상태를 모두 뒤집는다
	void ToggleFakeReady()
	{
		if (myRoom == null) return;
		IReadOnlyList<RaidMember> members = myRoom.Members;
		// SetReady가 RoomChanged를 부르면서 목록을 다시 그리므로, 이름을 먼저 모은다
		List<string> names = new List<string>(members.Count);
		List<bool> next = new List<bool>(members.Count);
		for (int i = 1; i < members.Count; i++)
		{
			if (members[i].Name == localName) continue;
			names.Add(members[i].Name);
			next.Add(!members[i].IsReady);
		}
		for (int i = 0; i < names.Count; i++) RaidRooms.SetReady(myRoom, names[i], next[i]);
	}
}
