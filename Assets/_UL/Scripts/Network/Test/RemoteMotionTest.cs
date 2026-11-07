using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 다른 플레이어의 캐릭터가 움직일 때 PlayerAnimator.HorizontalSpeed가 맞게 나오는지(#356) 확인하는 테스트.
/// 호스트와 클라이언트가 서로 상대의 캐릭터를 본다.
/// <para>
/// 사용법: Base 씬에서 Play → eval로 <c>RemoteMotionTest.StartInEditor()</c>(호스트)로 시작한 뒤, 시험 빌드를 <c>-remoteMotionTest member</c>로 실행한다.<br/>
/// 씬에 놓지 않는다.
/// </para>
/// <para>
/// 두 쪽이 같은 시각표로 움직인다(상대가 로비에 보인 때가 0초).<br/>
/// - A (3~4.5초): member가 오른쪽으로 걷는다. host가 member의 속도를 잰다.<br/>
/// - B (6~7.5초): host가 왼쪽으로 걷는다. member가 host의 속도를 잰다.<br/>
/// - C (9초~): member가 사냥터로 가고, host가 뒤따라 가서 가만히 있는다. member는 host가 나타나 순간이동하는 동안에도 속도가 0인지 본다.<br/>
/// 걷는 동안은 대부분의 프레임에서 속도가 나와야 하고, 가만히 있을 때는 한 프레임도 이동으로 보이면 안 된다.<br/>
/// 「[RemoteMotionTest][역할] 전체 통과」면 통과.
/// </para>
/// </summary>
public class RemoteMotionTest : MonoBehaviour
{
	const string RoleArg = "-remoteMotionTest";
	const string AddressArg = "-remoteMotionTestAddress";
	const float TimeoutSeconds = 30f;
	const float WalkSpeed = 3f;
	const float WalkSeconds = 1.5f;
	// 걷는 구간에서 이동으로 보여야 하는 프레임의 비율
	const float MinMovingFrameRatio = 0.9f;
	const float RunThreshold = 0.1f;

	string role;
	int failedCount;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	static void CreateIfRequested()
	{
		string role = GetArg(RoleArg);
		if (role != null) Create(role);
	}

	/// <summary>에디터 Play 모드에서 eval로 부른다. 시험 빌드 클라이언트(member) 하나가 함께 하는 것으로 본다.</summary>
	public static void StartInEditor() => Create("host");

	static void Create(string role)
	{
		var go = new GameObject(nameof(RemoteMotionTest));
		DontDestroyOnLoad(go);
		go.AddComponent<RemoteMotionTest>().role = role;
	}

	IEnumerator Start()
	{
		if (role == "host") yield return RunHost();
		else if (role == "member") yield return RunMember();
		else Fail($"모르는 역할: {role}");

		Log(failedCount == 0 ? "전체 통과" : $"실패 {failedCount}개");
		if (role == "member") Application.Quit();
	}

	IEnumerator RunHost()
	{
		if (!NetworkServer.active) NetworkManager.singleton.StartHost();
		yield return Wait("클라이언트가 로비에 들어옴", () => FindMember() != null);
		PlayerAnimator memberAnimator = FindMember().identity.GetComponent<PlayerAnimator>();
		Rigidbody2D myBody = NetworkClient.localPlayer.GetComponent<Rigidbody2D>();
		float t0 = Time.time;

		// A: member가 걷는다. host가 member의 속도를 잰다
		yield return WaitUntilTime(t0 + 3.3f);
		var samples = new List<float>();
		int runFrames = 0;
		int groundedFrames = 0;
		Animator memberClipAnimator = memberAnimator.GetComponent<Animator>();
		PlayerMovement memberMovement = memberAnimator.GetComponent<PlayerMovement>();
		// 화면에 그려지는 그림이 실제로 바뀌는지도 본다 (상태 이름만 Run이고 그림이 멈춰 있을 수 있음)
		SpriteRenderer memberSprite = memberAnimator.GetComponentInChildren<SpriteRenderer>();
		var spriteNames = new HashSet<string>();
		yield return Sample(samples, t0 + 3.3f + WalkSeconds - 0.6f, () =>
		{
			// 속도 값이 아니라 Animator가 실제로 재생하는 상태를 본다
			if (memberSprite != null && memberSprite.sprite != null) spriteNames.Add(memberSprite.sprite.name);
			if (memberClipAnimator.GetCurrentAnimatorStateInfo(0).IsName("Run")) runFrames++;
			if (memberMovement != null && memberMovement.IsGrounded) groundedFrames++;
			return memberAnimator.HorizontalSpeed;
		});
		CheckWalk("host가 본 member의 걷기", samples, 1f);
		Check($"host 화면에서 Animator가 Run을 재생한 프레임 {runFrames}/{samples.Count} (바닥으로 본 프레임 {groundedFrames})", samples.Count > 0 && (float)runFrames / samples.Count >= MinMovingFrameRatio);
		Check($"host 화면에서 member 그림이 {spriteNames.Count}가지로 바뀜: {string.Join(", ", spriteNames)}", spriteNames.Count > 1);

		// B: host가 왼쪽으로 걷는다 (member가 잰다)
		yield return WaitUntilTime(t0 + 6f);
		yield return Walk(myBody, -1f, WalkSeconds);

		// C: member가 사냥터로 간 뒤 host가 뒤따라 가서 가만히 있는다
		yield return Wait("member가 사냥터에 도착", () => FindMember() != null && FindMember().identity.GetComponent<PlayerPublicInfo>().Place == PlaceScenes.HuntingGround);
		yield return new WaitForSeconds(1f);
		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.HuntingGround);
		yield return Wait("host가 사냥터에 도착", () => NetworkClient.localPlayer.gameObject.scene.name == PlaceScenes.HuntingGround);
		// member가 host가 나타난 뒤에도 재는 동안 가만히 있는다
		yield return new WaitForSeconds(6f);
	}

	IEnumerator RunMember()
	{
		yield return Wait("NetworkManager", () => NetworkManager.singleton != null);
		NetworkManager.singleton.networkAddress = GetArg(AddressArg) ?? "localhost";
		NetworkManager.singleton.StartClient();
		yield return Wait("접속", () => NetworkClient.isConnected);
		NetworkClient.Send(new AccountAuthRequest { skipLogin = true });
		yield return Wait("로그인", () => NetworkClient.connection != null && NetworkClient.connection.isAuthenticated);
		InvokeAuthenticator.Current.RequestEnter(CharacterLanguages.C);
		yield return Wait("게임에 들어감", () => NetworkClient.localPlayer != null);

		PlayerAnimator hostAnimator = null;
		yield return Wait("호스트의 플레이어가 보임", () => (hostAnimator = FindOther()) != null);
		if (hostAnimator == null) yield break;
		Rigidbody2D myBody = NetworkClient.localPlayer.GetComponent<Rigidbody2D>();
		float t0 = Time.time;

		// A: member가 오른쪽으로 걷는다
		yield return WaitUntilTime(t0 + 3f);
		yield return Walk(myBody, 1f, WalkSeconds);

		// B: host가 걷는다. member가 host의 속도를 잰다
		yield return WaitUntilTime(t0 + 6.3f);
		var samples = new List<float>();
		int runFrames = 0;
		Animator hostClipAnimator = hostAnimator.GetComponent<Animator>();
		yield return Sample(samples, t0 + 6.3f + WalkSeconds - 0.6f, () =>
		{
			// 속도 값이 아니라 Animator가 실제로 재생하는 상태를 본다
			if (hostClipAnimator.GetCurrentAnimatorStateInfo(0).IsName("Run")) runFrames++;
			return hostAnimator.HorizontalSpeed;
		});
		CheckWalk("member가 본 host의 걷기", samples, -1f);
		Check($"member 화면에서 Animator가 Run을 재생한 프레임 {runFrames}/{samples.Count}", samples.Count > 0 && (float)runFrames / samples.Count >= MinMovingFrameRatio);

		// C: 사냥터로 가서 host가 나타나 순간이동하는 동안의 속도를 잰다
		yield return WaitUntilTime(t0 + 9f);
		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.HuntingGround);
		yield return Wait("사냥터에 도착", () => NetworkClient.localPlayer.GetComponent<PlayerPublicInfo>().Place == PlaceScenes.HuntingGround);
		// host가 사냥터로 오기 전부터 재기 시작해야 host가 나타나는 순간(생성 → 순간이동)까지 잡힌다.
		// 보호 장치를 거치기 전의 실제 위치 변화(유닛/초)도 함께 잰다. 이 값이 크게 뛰었는데 HorizontalSpeed가 0이어야 보호 장치가 일한 것이다
		var arrival = new List<float>();
		float rawMax = 0f;
		bool seen = false;
		float lastX = 0f;
		yield return Sample(arrival, Time.time + 10f, () =>
		{
			PlayerAnimator other = FindOther();
			if (other == null)
			{
				seen = false;
				return 0f;
			}
			float x = other.transform.position.x;
			if (seen) rawMax = Mathf.Max(rawMax, Mathf.Abs(x - lastX) / Mathf.Max(Time.deltaTime, 0.0001f));
			seen = true;
			lastX = x;
			return other.HorizontalSpeed;
		});
		float maxAbs = 0f;
		foreach (float s in arrival) maxAbs = Mathf.Max(maxAbs, Mathf.Abs(s));
		Log($"참고: host 위치의 실제 최대 변화 {rawMax:F2} 유닛/초 (걷는 속도 {WalkSpeed})");
		Check($"host가 나타나 순간이동하는 동안 이동으로 보이지 않음 (프레임 {arrival.Count}개, 최대 {maxAbs:F2})", arrival.Count > 0 && maxAbs <= RunThreshold);
	}

	// 몸을 direction 방향으로 seconds초 동안 WalkSpeed로 옮긴다. 소유자의 위치가 NetworkTransform으로 다른 화면에 간다
	static IEnumerator Walk(Rigidbody2D body, float direction, float seconds)
	{
		float end = Time.time + seconds;
		while (Time.time < end)
		{
			body.position += Vector2.right * (direction * WalkSpeed * Time.deltaTime);
			yield return null;
		}
	}

	// end 시각까지 프레임마다 값을 모은다
	static IEnumerator Sample(List<float> samples, float end, Func<float> read)
	{
		while (Time.time < end)
		{
			samples.Add(read());
			yield return null;
		}
	}

	void CheckWalk(string label, List<float> samples, float direction)
	{
		int moving = 0;
		float sum = 0f;
		foreach (float s in samples)
		{
			if (Mathf.Abs(s) > RunThreshold) moving++;
			sum += s;
		}
		float ratio = samples.Count > 0 ? (float)moving / samples.Count : 0f;
		float mean = samples.Count > 0 ? sum / samples.Count : 0f;
		Check($"{label}: 이동으로 보이는 프레임 {moving}/{samples.Count} ({ratio:P0}), 평균 속도 {mean:F2}", ratio >= MinMovingFrameRatio && mean * direction > 0f);
	}

	static NetworkConnectionToClient FindMember()
	{
		foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
			if (!(conn is LocalConnectionToClient) && conn.identity != null) return conn;
		return null;
	}

	// 이 화면에 보이는 다른 플레이어의 PlayerAnimator
	static PlayerAnimator FindOther()
	{
		foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
			if (identity != null && identity != NetworkClient.localPlayer && identity.TryGetComponent(out PlayerAnimator animator)) return animator;
		return null;
	}

	IEnumerator WaitUntilTime(float time)
	{
		while (Time.time < time) yield return null;
	}

	IEnumerator Wait(string label, Func<bool> condition)
	{
		float end = Time.time + TimeoutSeconds;
		while (!condition() && Time.time < end) yield return null;
		if (condition()) Pass(label);
		else Fail($"{label}: 시간 초과");
	}

	void Check(string step, bool ok)
	{
		if (ok) Pass(step);
		else Fail(step);
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
		Debug.LogError($"[RemoteMotionTest][{role}] 실패: {step}");
	}

	void Log(string message) => Debug.Log($"[RemoteMotionTest][{role}] {message}");
}
