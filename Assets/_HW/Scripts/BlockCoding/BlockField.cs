using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 블록 안에서 키보드로 글자를 적는 칸 하나 (값, 변수 이름, 새 이름, 함수 이름).
/// 적은 글자 길이에 맞춰 칸 너비가 늘어난다.
/// <para>
/// 프리팹: UI → Input Field - TextMeshPro 에 Layout Element와 이 컴포넌트를 붙인다.<br/>
/// 다 적고 Enter를 누르거나 다른 곳을 누르면 Edited가 불린다. 데이터를 고치는 일은 Edited를 받는 쪽이 한다.
/// </para>
/// </summary>
[RequireComponent(typeof(TMP_InputField))]
public class BlockField : MonoBehaviour
{
	[SerializeField, Tooltip("칸이 가장 좁을 때의 너비")]
	float minWidth = 14f;

	[SerializeField, Tooltip("글자 양옆 여백을 합친 너비")]
	float padding = 6f;

	[SerializeField, Tooltip("알 수 없는 값을 적었을 때 글자 색")]
	Color invalidColor = new Color32(0xFF, 0x6B, 0x6B, 0xFF);

	/// <summary>다 적었을 때 적은 글자와 함께 불린다</summary>
	public event Action<string> Edited;

	TMP_InputField input;
	LayoutElement layout;
	Color normalColor = Color.white;

	/// <summary>칸에 보이는 글자. 넣어도 Edited는 불리지 않는다</summary>
	public string Text
	{
		get { Setup(); return input.text; }
		set
		{
			Setup();
			input.SetTextWithoutNotify(value ?? "");
			Resize();
		}
	}

	/// <summary>칸이 가장 좁을 때의 너비. 함수 이름 칸처럼 넓게 쓸 때 바꾼다</summary>
	public float MinWidth
	{
		get => minWidth;
		set { minWidth = value; Resize(); }
	}

	/// <summary>
	/// 잘못된 값이면 글자를 빨갛게 보여 준다.
	/// </summary>
	public void SetInvalid(bool invalid)
	{
		Setup();
		if (input.textComponent != null) input.textComponent.color = invalid ? invalidColor : normalColor;
	}

	void Awake() => Setup();

	// Instantiate 직후 Awake보다 먼저 Text를 넣을 수 있도록 처음 쓸 때 준비한다
	void Setup()
	{
		if (input != null) return;
		input = GetComponent<TMP_InputField>();
		layout = GetComponent<LayoutElement>();
		if (input.textComponent != null) normalColor = input.textComponent.color;
		input.onValueChanged.AddListener(_ => Resize());
		input.onEndEdit.AddListener(text => Edited?.Invoke(text));
		Resize();
	}

	void Resize()
	{
		if (input == null || layout == null || input.textComponent == null) return;
		float width = input.textComponent.GetPreferredValues(input.text).x;
		layout.minWidth = minWidth;
		layout.preferredWidth = Mathf.Max(minWidth, width + padding);
		LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
	}
}
