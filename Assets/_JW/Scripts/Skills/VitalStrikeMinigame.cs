using System;
using UnityEngine;

/// <summary>급소공략 미니게임 결과</summary>
public enum VitalStrikeResult
{
	None,  // 미니게임을 하지 않음
	Miss,  // 칸을 못 맞춤 (기본 데미지)
	Eye,   // 눈 칸: 3.0배 + 실명
	Ankle, // 발목 칸: 3.0배 + 이속 감소
}

/// <summary>
/// 급소공략 미니게임 (#161). 플레이어 프리팹에 붙인다. 키 입력은 읽지 않는다 (SlotCaster가 Space로 Begin, Stop을 부른다).
/// <para>
/// - 다이얼 위로 바늘이 0도에서 시작해 시계 방향으로 한 바퀴 돈다. 다이얼에는 눈 칸과 발목 칸이 있고, 시작할 때마다 위치가 바뀐다.<br/>
/// - Stop을 부른 순간 바늘이 들어 있는 칸이 결과다. 칸 밖이거나, 한 바퀴를 다 돌 때까지 안 멈추면 Miss.<br/>
/// - 결과는 다음 급소공략이 시작할 때 TakeResult로 한 번 가져간다. 새로 Begin하거나 ClearResult를 부르면 지워진다.<br/>
/// - 각도: 0도 = 12시, 시계 방향으로 커진다.
/// </para>
/// </summary>
public class VitalStrikeMinigame : MonoBehaviour
{
	[Tooltip("바늘이 한 바퀴 도는 시간 (초, 임시)")]
	[SerializeField, Min(0.2f)] float revolutionSeconds = 1.2f;
	[Tooltip("눈 칸, 발목 칸 각각의 크기 (도, 임시)")]
	[SerializeField, Range(5f, 90f)] float zoneDegrees = 30f;
	[Tooltip("바늘이 출발한 뒤 이 각도 전에는 칸을 놓지 않는다 (시작하자마자 칸이 지나가지 않게)")]
	[SerializeField, Range(0f, 180f)] float zoneMinStartDegrees = 70f;

	bool running;
	float needle;
	VitalStrikeResult result = VitalStrikeResult.None;

	/// <summary>미니게임이 진행 중인지 (바늘이 도는 중)</summary>
	public bool IsRunning => running;

	/// <summary>지금 바늘 각도 (0~360, 0 = 12시, 시계 방향). 표시용</summary>
	public float NeedleAngle => needle;

	/// <summary>눈 칸 시작 각도. 칸은 EyeZoneStart ~ EyeZoneStart + ZoneDegrees</summary>
	public float EyeZoneStart { get; private set; }

	/// <summary>발목 칸 시작 각도</summary>
	public float AnkleZoneStart { get; private set; }

	/// <summary>칸 하나의 크기 (도)</summary>
	public float ZoneDegrees => zoneDegrees;

	/// <summary>마지막으로 끝난 미니게임 결과 (아직 급소공략이 가져가지 않은 것). 없으면 None</summary>
	public VitalStrikeResult PendingResult => result;

	/// <summary>[표시] 미니게임이 시작됐을 때. 다이얼을 띄운다</summary>
	public event Action Started;

	/// <summary>[표시] 미니게임이 끝났을 때 (결과). 성공·실패 연출. Cancel로 끝나면 None</summary>
	public event Action<VitalStrikeResult> Finished;

	/// <summary>미니게임을 시작한다. 칸 위치를 새로 정하고 바늘을 0도로 돌린다. 남아 있던 결과는 지운다.</summary>
	public void Begin()
	{
		result = VitalStrikeResult.None;
		PlaceZones();
		needle = 0f;
		running = true;
		Started?.Invoke();
	}

	/// <summary>바늘을 멈추고 판정한다. 진행 중이 아니면 아무 일도 없이 지금 결과를 돌려준다.</summary>
	public VitalStrikeResult Stop()
	{
		if (!running) return result;
		Finish(Judge(needle));
		return result;
	}

	/// <summary>미니게임을 결과 없이 그만둔다 (대상 고르기가 취소될 때).</summary>
	public void Cancel()
	{
		if (!running && result == VitalStrikeResult.None) return;
		running = false;
		result = VitalStrikeResult.None;
		Finished?.Invoke(VitalStrikeResult.None);
	}

	/// <summary>
	/// 결과를 가져간다. 가져가면 지워진다. 진행 중이면 그 자리에서 멈추지 않고 Miss로 끝낸다 (멈추기 전에 슬롯을 뗀 경우).<br/>
	/// 급소공략 스킬이 시작할 때 부른다.
	/// </summary>
	public VitalStrikeResult TakeResult()
	{
		if (running) Finish(VitalStrikeResult.Miss);
		VitalStrikeResult taken = result;
		result = VitalStrikeResult.None;
		return taken;
	}

	/// <summary>가져가지 않은 결과를 지운다.</summary>
	public void ClearResult() => result = VitalStrikeResult.None;

	/// <summary>각도가 칸 안인지 (표시·테스트용)</summary>
	public VitalStrikeResult Judge(float angle)
	{
		if (InZone(angle, EyeZoneStart)) return VitalStrikeResult.Eye;
		if (InZone(angle, AnkleZoneStart)) return VitalStrikeResult.Ankle;
		return VitalStrikeResult.Miss;
	}

	void Update()
	{
		if (!running) return;
		needle += 360f / revolutionSeconds * Time.deltaTime;
		if (needle >= 360f)
		{
			needle = 360f;
			Finish(VitalStrikeResult.Miss);
		}
	}

	void Finish(VitalStrikeResult value)
	{
		running = false;
		result = value;
		Finished?.Invoke(value);
	}

	bool InZone(float angle, float start) => angle >= start && angle <= start + zoneDegrees;

	// 두 칸이 겹치지 않고, 출발 직후(zoneMinStartDegrees 전)와 한 바퀴 끝에 걸치지 않게 놓는다
	void PlaceZones()
	{
		float min = zoneMinStartDegrees;
		float max = 360f - zoneDegrees;
		float first = UnityEngine.Random.Range(min, max - zoneDegrees * 2f);
		float second = UnityEngine.Random.Range(first + zoneDegrees * 2f, max);
		bool eyeFirst = UnityEngine.Random.value < 0.5f;
		EyeZoneStart = eyeFirst ? first : second;
		AnkleZoneStart = eyeFirst ? second : first;
	}
}
