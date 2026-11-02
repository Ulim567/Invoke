using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [표시] 대상에 걸린 상태 이상을 머리 위에 아이콘으로 보여 준다 (#156). StatusEffects와 같은 오브젝트(몬스터 프리팹 최상위)에 붙인다.
/// <para>
/// - StatusEffects.StatusApplied / StatusRemoved를 받아 아이콘을 띄우고 지운다. 상태 값은 읽기만 한다 (코드 작성 규칙 1).<br/>
/// - 여러 개 걸리면 걸린 순서대로 가로로 나란히, 가운데 맞춤. 하나 풀리면 나머지를 다시 정렬한다.<br/>
/// - 다시 걸어도(지속 시간 새로 시작) 아이콘은 그대로다. 대상이 죽으면 StatusEffects가 모두 풀어서 아이콘도 모두 사라진다.<br/>
/// - 아이콘 프리팹(StatusIcon)은 비워 두면 Resources/StatusIcon을 쓴다. 그래서 몬스터 프리팹에는 이 컴포넌트만 붙이면 된다.<br/>
/// - 보여 주기만 하는 오브젝트라 네트워크 오브젝트가 아니다. 대상의 자식으로 만들어 같이 움직이고, 대상이 없어지면 같이 없어진다.
/// </para>
/// </summary>
[RequireComponent(typeof(StatusEffects))]
public class StatusIconView : MonoBehaviour
{
	[Tooltip("아이콘 프리팹. 비워 두면 Resources/StatusIcon")]
	[SerializeField] StatusIcon iconPrefab;
	[Tooltip("대상 콜라이더 윗면에서 아이콘까지 높이 (유닛)")]
	[SerializeField, Min(0f)] float heightAboveTop = 0.6f;
	[Tooltip("아이콘 사이 간격 (유닛, 아이콘 한 칸 = 1)")]
	[SerializeField, Min(0.1f)] float spacing = 1.1f;

	StatusEffects status;
	Collider2D body;
	// 걸린 순서 그대로. 순서가 보이는 순서다
	readonly List<StatusType> order = new List<StatusType>();
	readonly Dictionary<StatusType, StatusIcon> icons = new Dictionary<StatusType, StatusIcon>();

	/// <summary>지금 보이는 상태 이상 (왼쪽부터). 테스트, UI에서 읽는다.</summary>
	public IReadOnlyList<StatusType> ShownStatuses => order;

	void Awake()
	{
		status = GetComponent<StatusEffects>();
		body = GetComponentInChildren<Collider2D>();
		if (iconPrefab == null) iconPrefab = Resources.Load<StatusIcon>("StatusIcon");
	}

	void OnEnable()
	{
		status.StatusApplied += OnApplied;
		status.StatusRemoved += OnRemoved;
	}

	void OnDisable()
	{
		status.StatusApplied -= OnApplied;
		status.StatusRemoved -= OnRemoved;
	}

	/// <summary>아이콘 하나의 월드 위치 (테스트용). 보이지 않으면 false.</summary>
	public bool TryGetIconPosition(StatusType type, out Vector3 position)
	{
		position = default;
		if (!icons.TryGetValue(type, out StatusIcon icon) || icon == null) return false;
		position = icon.transform.position;
		return true;
	}

	void OnApplied(StatusType type)
	{
		if (icons.ContainsKey(type)) return; // 다시 걸면 그대로
		StatusIcon icon = SpawnIcon(type);
		if (icon == null) return;
		icons[type] = icon;
		order.Add(type);
		Layout();
	}

	void OnRemoved(StatusType type)
	{
		if (!icons.TryGetValue(type, out StatusIcon icon)) return;
		icons.Remove(type);
		order.Remove(type);
		DespawnIcon(icon);
		Layout();
	}

	// 머리 위에 가운데 맞춤으로 나란히 놓는다. 대상의 자식이라 로컬 좌표로 놓는다
	void Layout()
	{
		float top = body != null ? body.bounds.max.y : transform.position.y + 1f;
		float centerX = body != null ? body.bounds.center.x : transform.position.x;
		float startX = centerX - spacing * (order.Count - 1) * 0.5f;
		for (int i = 0; i < order.Count; i++)
		{
			Vector3 world = new Vector3(startX + spacing * i, top + heightAboveTop, transform.position.z);
			icons[order[i]].transform.position = world;
		}
	}

	// 아이콘은 이 두 함수로만 만들고 없앤다 (코드 작성 규칙 3)
	StatusIcon SpawnIcon(StatusType type)
	{
		if (iconPrefab == null)
		{
			Debug.LogError($"[StatusIconView] {name}: 아이콘 프리팹(Resources/StatusIcon)을 찾지 못했습니다.");
			return null;
		}
		StatusIcon icon = Instantiate(iconPrefab, transform);
		icon.Show(type);
		return icon;
	}

	void DespawnIcon(StatusIcon icon)
	{
		if (icon != null) Destroy(icon.gameObject);
	}
}
