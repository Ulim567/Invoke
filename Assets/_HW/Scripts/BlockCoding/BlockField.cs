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
		// 비어 있으면 안내 글자(Placeholder)만큼 넓힌다
		if (string.IsNullOrEmpty(input.text) && input.placeholder is TMP_Text placeholder && !string.IsNullOrEmpty(placeholder.text))
			width = placeholder.GetPreferredValues(placeholder.text).x;
		layout.minWidth = minWidth;
		layout.preferredWidth = Mathf.Max(minWidth, width + padding);
		LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
	}
}
