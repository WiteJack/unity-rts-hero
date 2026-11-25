using UnityEngine;

/// <summary>
/// Сундук на карте (ОБНОВЛЕНИЕ 30). Вешается на любой объект сцены (свой скрипт в проекте раньше был только
/// у выпавшего снаряжения — EquipmentPickup; этот — отдельный РАССТАВЛЯЕМЫЙ сундук).
///
/// Как работает:
///  - Герой подходит ближе openRadius → сундук открывается сам: выдаёт случайную вещь своего тира
///    (EquipmentSystem.TryEquip; не влезла/слабее надетых — сундучок-дроп падает рядом, по диздоку) и золото.
///  - Цвет: если на объекте/детях нет Renderer — рисуется кубик-сундучок, раскрашенный по тиру
///    (как в EquipmentDatabase.GetChestColor: тир 1 — не красится, 2 — жёлтый, 3 — оранжевый, 4 — красный).
///    Есть своя моделька — она просто перекрасится.
///  - refillSeconds > 0 → через это время сундук снова полон (перекраска возвращается); 0 → одноразовый.
///
/// Логи отказа — диагностика «сундук не открывается» через [СУНДУК] с точной причиной.
/// </summary>
public class WorldChest : MonoBehaviour
{
    [Header("Содержимое")]
    [Tooltip("Тир случайного предмета внутри (влияет и на цвет сундука)")]
    public EquipmentDatabase.Tier chestTier = EquipmentDatabase.Tier.Tier1;
    [Tooltip("Золото внутри сундука (0 — без золота)")]
    public int goldInside = 0;
    [Tooltip("Радиус открытия: герою достаточно подойти ближе")]
    public float openRadius = 2.5f;
    [Tooltip("Секунд до повторного наполнения (0 = сундук одноразовый)")]
    public float refillSeconds = 0f;

    [Header("Визуал")]
    [Tooltip("Свой Renderer сундука (моделька). Пусто — скрипт возьмёт первый Renderer у детей, а если и его нет — нарисует кубик-сундучок, раскрашенный по тиру")]
    public Renderer customRenderer;
    [Tooltip("Размер кубика-сундучка, если рисует скрипт")]
    public Vector3 fallbackChestSize = new Vector3(0.7f, 0.5f, 0.5f);

    [Header("Диагностика")]
    [Tooltip("Писать в консоль каждый поиск героя рядом (для диагностики «сундук не открывается»)")]
    public bool logSearch = false;

    private bool full = true;
    private float refillTimer;
    private float searchTimer;
    private Renderer visual;
    private GameObject fallbackChest;

    void Start()
    {
        EnsureVisual();
        ApplyTierColor();
    }

    void Update()
    {
        if (!full)
        {
            if (refillSeconds <= 0f) return;
            refillTimer -= Time.deltaTime;
            if (refillTimer <= 0f)
            {
                full = true;
                ApplyTierColor();
                Debug.Log($"[СУНДУК] {name}: снова полон (тир {(int)chestTier}, золото {goldInside}).");
            }
            return;
        }

        searchTimer -= Time.deltaTime;
        if (searchTimer > 0f) return;
        searchTimer = 0.25f;

        HeroStats hero = FindAnyObjectByType<HeroStats>();
        if (hero == null || hero.isDead)
        {
            if (logSearch) Debug.Log($"[СУНДУК] {name}: открытия нет — герой не найден или мёртв.");
            return;
        }

        float dist = Vector3.Distance(transform.position, hero.transform.position);
        if (dist > openRadius)
        {
            if (logSearch) Debug.Log($"[СУНДУК] {name}: герой в {dist:0.0} м — дальше радиуса открытия {openRadius:0.0} м.");
            return;
        }

        Open(hero);
    }

    private void Open(HeroStats hero)
    {
        full = false;
        refillTimer = refillSeconds;

        Debug.Log($"[СУНДУК] {name}: открыт героем (тир {(int)chestTier}).");

        if (goldInside > 0 && GoldManager.Instance != null)
        {
            GoldManager.Instance.AddGold(goldInside);
            Debug.Log($"<color=yellow>[СУНДУК] +{goldInside} золота</color> из {name}.");
        }

        var def = EquipmentDatabase.GetRandom(chestTier);
        if (def == null)
        {
            Debug.Log($"[СУНДУК] {name}: в базе EquipmentDatabase нет вещей тира {(int)chestTier} — предмет не выдан (проверь базу).");
            return;
        }

        var equip = hero.GetComponent<EquipmentSystem>();
        if (equip != null && equip.TryEquip(def, out _))
        {
            Debug.Log($"[СУНДУК] Герой получил «{def.displayName}» (тир {(int)def.tier}): {def.BonusesText()}");
        }
        else
        {
            Vector3 dropPos = transform.position + new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f));
            EquipmentPickup.SpawnExact(dropPos, def);
            Debug.Log($"[СУНДУК] Предмет «{def.displayName}» не наделся (слоты заняты/слабее надетых) — сундучок-дроп упал рядом с сундуком.");
        }

        if (refillSeconds > 0f) EmptifyVisual();
        else Destroy(gameObject);
    }

    // ==================== ВИЗУАЛ ====================

    private void EnsureVisual()
    {
        if (customRenderer != null) { visual = customRenderer; return; }
        visual = GetComponentInChildren<Renderer>();
        if (visual != null) return;

        // --- кубик-сундучок, как у выпавшего снаряжения: ---
        fallbackChest = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallbackChest.name = "ChestVisual";
        fallbackChest.transform.SetParent(transform, false);
        fallbackChest.transform.localPosition = new Vector3(0f, fallbackChestSize.y * 0.5f, 0f);
        fallbackChest.transform.localScale = fallbackChestSize;
        var col = fallbackChest.GetComponent<Collider>();
        if (col != null) Destroy(col);
        visual = fallbackChest.GetComponent<MeshRenderer>();
    }

    private void ApplyTierColor()
    {
        if (visual == null) return;
        Color c = chestTier == EquipmentDatabase.Tier.Tier1 ? Color.white : TierColorOf(chestTier);
        Paint(visual, c);
    }

    private void EmptifyVisual()
    {
        if (visual == null) return;
        Paint(visual, new Color(0.25f, 0.22f, 0.2f));
    }

    private static Color TierColorOf(EquipmentDatabase.Tier tier)
    {
        switch (tier)
        {
            case EquipmentDatabase.Tier.Tier2: return new Color(1f, 0.92f, 0.20f);
            case EquipmentDatabase.Tier.Tier3: return new Color(1f, 0.55f, 0.05f);
            case EquipmentDatabase.Tier.Tier4: return new Color(0.85f, 0.10f, 0.10f);
            default: return Color.white;
        }
    }

    private void Paint(Renderer r, Color baseColor)
    {
        int baseColorId = Shader.PropertyToID("_BaseColor");
        int colorId = Shader.PropertyToID("_Color");
        foreach (var m in r.materials)
        {
            if (m == null) continue;
            if (m.HasProperty(baseColorId)) m.SetColor(baseColorId, baseColor);
            else if (m.HasProperty(colorId)) m.SetColor(colorId, baseColor);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, openRadius);
    }

    [ContextMenu("Сундук: проверить содержимое в консоли")]
    private void ContextPrintContents()
    {
        var def = EquipmentDatabase.GetRandom(chestTier);
        Debug.Log($"[СУНДУК] {name}: тир {(int)chestTier}, золото {goldInside}, вещь при открытии будет: {(def != null ? def.displayName : "БАЗА ПУСТА ДЛЯ ЭТОГО ТИРА")}.");
    }
}
