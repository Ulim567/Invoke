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
	Color placeholderColor = new Color(1, 1, 1, 0.35f); // 프리팹의 안내 글자 색. Setup에서 읽는다

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
	/// 칸이 비어 있을 때 흐리게 보일 안내 글자 (#476). 값 칸이 받는 타입을 보인다 (예: int, bool, Target).
	/// 칸 너비도 이 글자에 맞춘다. 비어 있는 칸이 넓어져 블록을 끼우기 쉽게 하기 위함
	/// </summary>
	public string Placeholder
	{
		set
		{
			Setup();
			if (input.placeholder is TMP_Text placeholder && placeholder.text != (value ?? ""))
			{
				placeholder.text = value ?? "";
				Resize();
			}
		}
	}

	/// <summary>칸에 적을 수 있는지. 슬롯 함수의 이름 칸처럼 바꾸면 안 되는 칸은 끈다</summary>
	public bool Interactable
	{
		get { Setup(); return input.interactable; }
		set { Setup(); input.interactable = value; }
	}

	/// <summary>
	/// 칸 바탕 그림과 색을 바꾼다. 선언의 이름 칸은 밑줄, 대입의 변수 칸은 점선 상자로 보인다 (BlockView).
	/// sprite가 null이면 바탕을 보이지 않는다.
	/// </summary>
	public void SetBackground(Sprite sprite, Color color)
	{
		if (!TryGetComponent(out Image image)) return;
		image.sprite = sprite;
		image.type = Image.Type.Sliced;
		image.color = sprite != null ? color : new Color(0, 0, 0, 0);
	}

	/// <summary>
	/// 안내 글자(Placeholder)의 색을 바꾼다. null이면 프리팹의 원래 색으로 되돌린다.
	/// 블록을 끄는 동안 받을 수 있는 칸은 청록, 받을 수 없는 칸은 어둡게 보일 때 BlockValueSlot이 부른다 (#487).
	/// </summary>
	public void SetPlaceholderColor(Color? color)
	{
		Setup();
		if (input.placeholder is TMP_Text placeholder) placeholder.color = color ?? placeholderColor;
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
		if (input.placeholder != null) placeholderColor = input.placeholder.color;
		input.onValueChanged.AddListener(_ => Resize());
		input.onEndEdit.AddListener(text => Edited?.Invoke(text));
		Resize();
	}

	// 잴 글자가 바뀌었으면 너비를 다시 잰다 (#536). 입력 중이면 화면에 그려진 글자, 아니면 확정된 글자
	// 한글은 「ㅇ → 아 → 안」처럼 조합하는 동안 onValueChanged가 불리지 않으므로, 그것만 기다리면 조합 중인 글자가 칸 밖으로 나가기 때문
	// 입력이 끝난 뒤에도 보는 이유: 마지막으로 잰 글자가 조합 중의 글자이면, 확정된 글자 너비로 되돌려야 하기 때문
	void Update()
	{
		if (input == null || input.textComponent == null) return;
		string shown = input.isFocused ? input.textComponent.text : input.text;
		if (shown != measuredText) Resize();
	}

	// 마지막으로 너비를 잰 글자. 같은 글자로 매 프레임 다시 재지 않으려고 기억한다
	string measuredText;

	void Resize()
	{
		if (input == null || layout == null || input.textComponent == null) return;
		// 입력 중이면 화면에 그려진 글자(조합 중인 한글 포함)로 잰다. 조합 글자는 아직 input.text에 들어가지 않기 때문
		string shown = input.isFocused ? input.textComponent.text : input.text;
		measuredText = shown;
		float width = input.textComponent.GetPreferredValues(shown).x;
		// 비어 있으면 안내 글자(Placeholder)만큼 넓힌다
		if (string.IsNullOrEmpty(input.text) && string.IsNullOrEmpty(shown?.Trim('​')) && input.placeholder is TMP_Text placeholder && !string.IsNullOrEmpty(placeholder.text))
			width = placeholder.GetPreferredValues(placeholder.text).x;
		layout.minWidth = minWidth;
		layout.preferredWidth = Mathf.Max(minWidth, width + padding);
		LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
	}
}
