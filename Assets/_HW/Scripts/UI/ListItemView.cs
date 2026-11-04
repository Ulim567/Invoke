using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 목록 한 줄 (ListItem 프리팹). 편집 화면의 함수 목록과 장착 화면의 함수·스킬 목록에서 쓴다. 보여 주기만 한다.
/// <para>
/// 프리팹 구성: 최상위에 Image(ui_item, Sliced) + Button + Horizontal Layout Group + Layout Element + 이 컴포넌트,
/// 자식에 Tag(Image, 색 띠. 없어도 됨), Prefix(Text - TextMeshPro, 번호), Name(Text - TextMeshPro), Detail(Text - TextMeshPro, 오른쪽 정렬).
/// </para>
/// </summary>
public class ListItemView : MonoBehaviour
{
	[SerializeField, Tooltip("누르면 고른다 (최상위 Button)")]
	Button button;

	[SerializeField, Tooltip("바탕 (최상위 Image)")]
	Image background;

	[SerializeField, Tooltip("앞쪽 색 띠 (자식 Tag Image). 비워도 된다")]
	Image tagImage;

	[SerializeField, Tooltip("앞쪽 번호 글자 (G, 1~8, ·). 비워도 된다")]
	TMP_Text prefixText;

	[SerializeField, Tooltip("이름 글자")]
	TMP_Text nameText;

	[SerializeField, Tooltip("오른쪽 작은 글자 (키, 코드 크기 등). 비워도 된다")]
	TMP_Text detailText;

	[SerializeField, Tooltip("보통 바탕 (ui_item)")]
	Sprite normalSprite;

	[SerializeField, Tooltip("고른 줄 바탕 (ui_item_selected)")]
	Sprite selectedSprite;

	[SerializeField, Tooltip("상자로 보일 때의 바탕 (ui_card). Global Variables 줄")]
	Sprite boxSprite;

	bool boxed;

	[SerializeField, Tooltip("이름 글자 색")]
	Color nameColor = new Color32(0xE8, 0xEC, 0xFF, 0xFF);

	[SerializeField, Tooltip("흐린 글자 색 (「비어 있음」)")]
	Color dimColor = new Color32(0x5A, 0x62, 0x90, 0xFF);

	[SerializeField, Tooltip("오류 글자 색 (컴파일 오류가 있는 함수, 빨간 밑줄)")]
	Color errorColor = new Color32(0xFF, 0x8A, 0x96, 0xFF);

	public Button Button => button;

	/// <summary>
	/// 번호, 이름, 오른쪽 글자, 고른 줄인지, 흐리게 보일지를 정한다.
	/// </summary>
	public void Show(string prefix, string name, string detail, bool selected, bool dim = false, bool error = false)
	{
		if (prefixText != null) prefixText.text = prefix ?? "";
		if (nameText != null)
		{
			nameText.text = name;
			nameText.color = error ? errorColor : dim ? dimColor : nameColor;
			nameText.fontStyle = error ? TMPro.FontStyles.Underline : TMPro.FontStyles.Normal;
		}
		if (detailText != null)
		{
			detailText.text = detail ?? "";
			if (error) detailText.color = errorColor;
		}
		if (background != null && normalSprite != null)
		{
			Sprite normal = boxed && boxSprite != null ? boxSprite : normalSprite;
			background.sprite = selected && selectedSprite != null ? selectedSprite : normal;
		}
	}

	/// <summary>
	/// 테두리 상자로 보이게 한다 (함수 목록 맨 위의 Global Variables). Show 전에 부른다.
	/// </summary>
	public void SetBoxed(bool value) => boxed = value;

	/// <summary>
	/// 앞쪽 색 띠의 색을 정한다 (장착 화면: 함수, C 스킬, 공통 이동을 색으로 나눈다).
	/// </summary>
	public void SetTagColor(Color color)
	{
		if (tagImage != null) tagImage.color = color;
	}
}
