using TMPro;
using UnityEngine;

/// <summary>
/// 맵 아이템 하위 Name point에 붙여 월드 공간에 아이템 이름을 표시합니다.
/// </summary>
[DisallowMultipleComponent]
public class ItemWorldNameLabel : MonoBehaviour
{
    [SerializeField] private string itemDisplayName;
    [SerializeField] private float fontSize = 2.5f;

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(itemDisplayName))
            return;

        CompensateParentScale();
        ConfigureTextMesh();
    }

    private void CompensateParentScale()
    {
        Transform parent = transform.parent;
        if (parent == null)
            return;

        Vector3 parentScale = parent.localScale;
        if (parentScale.x <= 0f || parentScale.y <= 0f)
            return;

        transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f);
    }

    private void ConfigureTextMesh()
    {
        TextMeshPro tmp = GetComponent<TextMeshPro>();
        if (tmp == null)
            tmp = gameObject.AddComponent<TextMeshPro>();

        tmp.text = itemDisplayName;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.font = ResolveSilverFont();

        SpriteRenderer spriteRenderer = transform.parent != null
            ? transform.parent.GetComponent<SpriteRenderer>()
            : null;
        if (spriteRenderer != null)
        {
            tmp.sortingLayerID = spriteRenderer.sortingLayerID;
            tmp.sortingOrder = spriteRenderer.sortingOrder + 1;
        }
    }

    private static TMP_FontAsset ResolveSilverFont()
    {
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < fonts.Length; i++)
        {
            if (fonts[i] != null && fonts[i].name == "Silver SDF")
                return fonts[i];
        }

        return TMP_Settings.defaultFontAsset;
    }
}
