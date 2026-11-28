using UnityEngine;

/// <summary>
/// Выпавшее снаряжение на полу — «сундучок» (по диздоку: "выпавшее снаряжение выглядит как сундук,
/// сундук красится в зависимости от тира"). 3D-модели по ТЗ не будет — это маленький кубик-сундучок
/// + иконка-биллборд над ним (нет картинки — БЕЛАЯ иконка).
///
/// Цвета сундуков (диздок, EquipmentDatabase.GetChestColor):
///   тир 1 — обычная моделька (не красится), тир 2 — жёлтый, тир 3 — оранжевый, тир 4 — красный.
///
/// Подбор: герой подходит в радиус pickupRange — предмет автоматически надевается
/// (EquipmentSystem.TryEquip). Если все слоты заняты и предмет слабее надетых — сундучок
/// остаётся лежать рядом (по диздоку).
///
/// Создание дропа из кода: EquipmentPickup.Spawn(position, tier) — случайный предмет тира,
/// или EquipmentPickup.SpawnExact(position, def) — конкретный предмет.
/// </summary>
public class EquipmentPickup : MonoBehaviour
{
    [Header("Подбор")]
    [Tooltip("Радиус подбора героем")]
    public float pickupRange = 2.5f;
    [Tooltip("С какой периодичностью искать героя рядом, сек")]
    public float searchInterval = 0.25f;

    [Header("Визуал")]
    [Tooltip("Иконка над сундучком (высота)")]
    public float iconHeight = 1.2f;
    [Tooltip("Амплитуда покачивания иконки")]
    public float bobAmplitude = 0.1f;

    public EquipmentDatabase.EquipmentDef item;

    private SpriteRenderer iconRenderer;
    private float searchTimer;
    private float bobTime;

    // ==================== СОЗДАНИЕ ДРОПА ====================

    /// <summary>Уронить на пол случайный предмет тира (как сундук, красится по тиру).</summary>
    public static EquipmentPickup Spawn(Vector3 position, EquipmentDatabase.Tier tier)
    {
        return SpawnExact(position, EquipmentDatabase.GetRandom(tier));
    }

    /// <summary>Уронить на пол конкретный предмет.</summary>
    public static EquipmentPickup SpawnExact(Vector3 position, EquipmentDatabase.EquipmentDef def)
    {
        if (def == null) return null;

        GameObject root = new GameObject("EquipmentPickup_" + def.displayName.Replace(" ", "_"));
        root.transform.position = position;
        var pickup = root.AddComponent<EquipmentPickup>();
        pickup.item = def;

        // --- Сундучок: кубик, покрашенный в цвет тира (серверная логика краски сундуков из диздока) ---
        GameObject chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chest.name = "Chest";
        chest.transform.SetParent(root.transform, false);
        chest.transform.localScale = new Vector3(0.5f, 0.35f, 0.35f);
        chest.transform.localPosition = new Vector3(0f, 0.18f, 0f);

        var chestRenderer = chest.GetComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", EquipmentDatabase.GetChestColor(def.tier)); // URP Lit
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
        chestRenderer.material = mat;

        // полностью удалить «физически громоздкий» коллайдер — подбор по дистанции, не столкновением
        var col = chest.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // --- Иконка-биллборд над сундучком (белая, если картинки нет) ---
        GameObject iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(root.transform, false);
        iconGO.transform.localPosition = new Vector3(0f, pickup.iconHeight, 0f);
        var sr = iconGO.AddComponent<SpriteRenderer>();
        pickup.iconRenderer = sr;

        // ВСЕ ИКОНКИ БЕЛЫЕ, если картинки нет (правило ТЗ). Спрайт предмета подставит владелец/скрипт выдачи.
        Sprite sprite = def.icon;
        if (sprite == null) sprite = EquipmentPickup.WhiteSquareSprite; // нет картинки — белая иконка
        sr.sprite = sprite;
        sr.color = Color.white;
        sr.sortingOrder = 50;
        // масштаб: спрайты бывают любого размера — приводим иконку к единым ~0.8 юнита
        float units = sprite.rect.width / Mathf.Max(1f, sprite.pixelsPerUnit);
        float worldSize = 0.8f;
        float scale = worldSize / Mathf.Max(0.0001f, units);
        iconGO.transform.localScale = new Vector3(scale, scale, 1f);

        return pickup;
    }

    private static Sprite _whiteSquare;
    /// <summary>Белый квадрат-иконка по умолчанию.</summary>
    public static Sprite WhiteSquareSprite
    {
        get
        {
            if (_whiteSquare == null)
                _whiteSquare = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f),
                    new Vector2(0.5f, 0.5f), 100f);
            return _whiteSquare;
        }
    }

    // ==================== ПОДБОР ====================

    void Update()
    {
        // покачивание иконки
        bobTime += Time.deltaTime;
        if (iconRenderer != null)
        {
            var t = iconRenderer.transform;
            t.localPosition = new Vector3(t.localPosition.x,
                iconHeight + Mathf.Sin(bobTime * 2.5f) * bobAmplitude, t.localPosition.z);
        }

        // поиск героя рядом
        searchTimer -= Time.deltaTime;
        if (searchTimer > 0f) return;
        searchTimer = searchInterval;

        HeroStats hero = FindAnyObjectByType<HeroStats>();
        if (hero == null || hero.isDead) return;
        if (Vector3.Distance(transform.position, hero.transform.position) > pickupRange) return;

        var equip = hero.GetComponent<EquipmentSystem>();
        if (equip == null) return;

        if (equip.TryEquip(item, out _))
        {
            Debug.Log($"[СНАРЯЖЕНИЕ] Герой подобрал «{item.displayName}» (тир {(int)item.tier}): {item.BonusesText()}");
            Destroy(gameObject);
        }
        // иначе: слоты заняты/предмет слабее — сундучок лежит дальше рядом с героем (по диздоку)
    }
}
