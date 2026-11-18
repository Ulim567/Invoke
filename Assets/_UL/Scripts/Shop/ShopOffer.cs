using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 가격의 한 줄: 재료 ID와 개수 (#534). 예: 버그 조각 7개.
/// <para>Mirror가 보내고 DB에는 ShopOffer.PriceText 글자로 저장한다.</para>
/// </summary>
[Serializable]
public struct ShopPrice
{
	// Mirror가 보내려면 public 필드여야 한다
	public string MaterialId;
	public int Count;

	public ShopPrice(string materialId, int count)
	{
		MaterialId = materialId;
		Count = count;
	}
}

/// <summary>
/// 상점 칸 하나에 올라온 상품 (#534). 서버가 뽑아 DB에 저장하고, PlayerProgress.ShopOffers로 소유 클라이언트에 보낸다.
/// <para>
/// - ItemId: 가진 개수와 같은 항목 ID (If, c.thrust, input.get_bug, Wait 등). 비어 있으면 빈 칸(올릴 후보가 없었음).<br/>
/// - Price: 이 칸을 살 때 내는 재료 목록. 칸에 올라올 때 한 번 정하고 바뀌지 않는다.<br/>
/// - 칸 번호는 목록 안의 위치다 (0~3 문법 블록, 4~7 함수, ShopRules).
/// </para>
/// </summary>
[Serializable]
public struct ShopOffer
{
	public string ItemId;
	public ShopPrice[] Price;

	public ShopOffer(string itemId, ShopPrice[] price)
	{
		ItemId = itemId ?? "";
		Price = price ?? Array.Empty<ShopPrice>();
	}

	/// <summary>빈 칸.</summary>
	public static ShopOffer Empty => new ShopOffer("", Array.Empty<ShopPrice>());

	/// <summary>올라온 항목이 없는 칸이면 true.</summary>
	public bool IsEmpty => string.IsNullOrEmpty(ItemId);

	/// <summary>가격을 재료 ID → 개수로 합친다. 같은 재료가 두 줄이면 더한다.</summary>
	public Dictionary<string, int> PriceByMaterial() => ShopPrices.Sum(Price);

	/// <summary>DB에 저장하는 가격 글자. 예: "bug_fragment:7,boss_core:1".</summary>
	public string PriceText => ShopPrices.ToText(Price);
}

/// <summary>가격 목록을 다루는 함수 (#534).</summary>
public static class ShopPrices
{
	/// <summary>재료 ID → 개수로 합친다. 같은 재료가 두 줄이면 더한다.</summary>
	public static Dictionary<string, int> Sum(IReadOnlyList<ShopPrice> price)
	{
		var sum = new Dictionary<string, int>();
		if (price == null) return sum;
		foreach (ShopPrice line in price)
		{
			if (string.IsNullOrEmpty(line.MaterialId) || line.Count <= 0) continue;
			sum[line.MaterialId] = (sum.TryGetValue(line.MaterialId, out int current) ? current : 0) + line.Count;
		}
		return sum;
	}

	/// <summary>"재료ID:개수"를 쉼표로 이은 글자. DB 저장용.</summary>
	public static string ToText(IReadOnlyList<ShopPrice> price)
	{
		if (price == null) return "";
		var text = new StringBuilder();
		foreach (ShopPrice line in price)
		{
			if (string.IsNullOrEmpty(line.MaterialId)) continue;
			if (text.Length > 0) text.Append(',');
			text.Append(line.MaterialId).Append(':').Append(line.Count);
		}
		return text.ToString();
	}

	/// <summary>ToText로 만든 글자를 가격 목록으로 되돌린다. 읽을 수 없는 줄은 건너뛴다.</summary>
	public static ShopPrice[] FromText(string text)
	{
		var price = new List<ShopPrice>();
		if (string.IsNullOrEmpty(text)) return price.ToArray();
		foreach (string part in text.Split(','))
		{
			int colon = part.LastIndexOf(':');
			if (colon <= 0 || !int.TryParse(part.Substring(colon + 1), out int count) || count <= 0) continue;
			price.Add(new ShopPrice(part.Substring(0, colon), count));
		}
		return price.ToArray();
	}
}
