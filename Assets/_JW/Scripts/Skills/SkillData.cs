using System;
using UnityEngine;

/// <summary>스킬 계열 (노션 「C 캐릭터 - 1차」 3. 스킬트리)</summary>
public enum SkillFamily
{
	Thrust,   // 찌르기
	Throw,    // 던지기
	Slash,    // 휘두르기
	Summon,   // 소환
	Movement, // 이동
}

/// <summary>
/// 대상 지정 방식 (노션 「C 캐릭터 - 1차」 7. 입력 방식). 파라미터 모양을 정한다.<br/>
/// - Single, SingleMultiHit, Pierce: Target 파라미터 1개 (첫 대상만 고르고, 맞는 방식은 스킬이 정함)<br/>
/// - MultiSelect: Target list 파라미터 1개<br/>
/// - Area, Path, None: 파라미터 없음
/// </summary>
public enum SkillTargeting
{
	Single,         // 단일
	SingleMultiHit, // 단일 + 다단 히트 (검 다중베기)
	Pierce,         // 관통 (꼬챙이)
	MultiSelect,    // 여러 명 선택 (전장 휩쓸기)
	Area,           // 범위 (검 크게베기, 검무)
	Path,           // 경로 (검 돌아오기)
	None,           // 대상 없음 (검 소환, 해제, 패링, 이동 계열)
}

/// <summary>
/// 스킬 하나의 수치. SkillTable 표의 한 줄이다.<br/>
/// 값은 Inspector(SkillTable 에셋)에서만 바꾼다. 스킬 클래스(DataDrivenSkill)는 이 값을 읽기만 한다.
/// </summary>
[Serializable]
public class SkillData
{
	[Tooltip("고정 ID. PredefinedFunctionIds의 값과 같아야 한다")]
	[SerializeField] string id;
	[Tooltip("화면 이름 (지금은 전부 가칭)")]
	[SerializeField] string displayName;
	[SerializeField] SkillFamily family;
	[SerializeField] SkillTargeting targeting;

	[Header("수치 (임시: 기획에서 정해지면 바꾼다)")]
	[Tooltip("기본 데미지 배율 (DamageRequest.SkillMultiplier). 조건 배율(출혈, 표식 등)은 DamageBonus")]
	[SerializeField, Min(0f)] float damageMultiplier = 1f;
	[Tooltip("동작 시간 (틱 수)")]
	[SerializeField, Min(1)] int tickCount = 20;
	[Tooltip("쿨타임 (틱 수). 동작이 끝난 뒤부터 흐른다")]
	[SerializeField, Min(0)] int cooldownTicks;
	[Tooltip("stack 부담. 프레임 크기 = 1 + 파라미터 수 + 이 값")]
	[SerializeField, Min(0)] int localVariableCount;
	[Tooltip("heap 사용량 (칸). 검 소환, 검기, 패링, 이동 계열. 메모리 작업(이우림)이 끝나면 실행기가 읽는다")]
	[SerializeField, Min(0)] int heapSize;

	[Header("필요 조건")]
	[Tooltip("쓸 수 있는 소환검이 있어야 하면 체크")]
	[SerializeField] bool needsSummonedSword;
	[Tooltip("맨손이어도 쓸 수 있으면 체크 (검 돌아오기, 소환, 이동 계열)")]
	[SerializeField] bool usableUnarmed;

	[Tooltip("효과 요약, 기획 문서와 다르게 가정한 것 등")]
	[SerializeField, TextArea(1, 3)] string note;

	public string Id => id;
	public string DisplayName => displayName;
	public SkillFamily Family => family;
	public SkillTargeting Targeting => targeting;
	public float DamageMultiplier => damageMultiplier;
	public int TickCount => tickCount;
	public int CooldownTicks => cooldownTicks;
	public int LocalVariableCount => localVariableCount;
	public int HeapSize => heapSize;
	public bool NeedsSummonedSword => needsSummonedSword;
	public bool UsableUnarmed => usableUnarmed;
	public string Note => note;
}
