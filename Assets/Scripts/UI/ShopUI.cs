using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ОБНОВЛЕНИЕ 25 — МАГАЗИН СНАРЯЖЕНИЯ (по диздоку, ui2.png).
/// Панель в правом нижнем углу: 16 ячеек (4 ряда × 4) — 1-й ряд тир 1, 2-й тир 2, 3-й тир 3, 4-й тир 4,
/// в каждом ряду 4 СЛУЧАЙНЫХ предмета из EquipmentDatabase.
/// Тултип по наведению (0.2 с, HeroTooltipUI) с описанием и ЦЕНОЙ; ЛКМ по ячейке — покупка за золото (GoldManager).
/// Цены: магазин дороже покупки случайного снаряжения на 100% (×2):
/// тир 1 = 2000, тир 2 = 5000, тир 3 = 10000, тир 4 = 24000.
/// Покупка: есть свободный слот — надевается, все слоты заняты — предмет падает сундучком около героя.
/// Создаётся кодом, на сцене ничего добавлять к UI не нужно. Панель открывает ShopBuilding при подходе героя.
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    private const int Cols = 4;
    private const int Rows = 4;
    private const float CellSize = 58f;
    private const float CellGap = 6f;
    private const float Pad = 8f;
    private const float TitleHeight = 24f;

    // Цены по рядам: базовая стоимость покупки случайного (1000/2500/5000/12000) × 2 за +100% магазина.
    private static readonly int[] RowPrices = { 2000, 5000, 10000, 24000 };

    private Canvas canvas;
    private RectTransform panel;
    private Text goldLabel;
    private Text title;

    // Товар: null = продано
    private EquipmentDatabase.EquipmentDef[] stock = new EquipmentDatabase.EquipmentDef[Cols * Rows];
    private Image[] cellBg = new Image[Cols * Rows];
    private Text[] cellName = new Text[Cols * Rows];
    private Text[] cellPrice = new Text[Cols * Rows];

    private float nextGoldRefresh;

    // ==================== СОЗДАНИЕ ====================

    public static void EnsureShown()
    {
        if (Instance != null) { Instance.gameObject.SetActive(true); return; }

        Canvas cvs = FindAnyObjectByType(typeof(Canvas)) as Canvas;
        if (cvs == null)
        {
            var go = new GameObject("UICanvas_Overlay");
            cvs = go.AddComponent<Canvas>();
            cvs.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
        }
        EnsureEventSystem();

        var rootGO = new GameObject("Shop_Root");
        rootGO.transform.SetParent(cvs.transform, false);
        Instance = rootGO.AddComponent<ShopUI>();
        Instance.canvas = cvs;
        Instance.Build();
        Debug.Log("[МАГАЗИН] ShopUI создан (Обновление 25): 16 ячеек, ряды = тир 1..4, цены 2000/5000/10000/24000.");
    }

    public static void Hide()
    {
        if (Instance != null) Instance.gameObject.SetActive(false);
    }

    private void Build()
    {
        float width = Pad * 2 + Cols * CellSize + (Cols - 1) * CellGap;
        float height = Pad + TitleHeight + 4 + Rows * CellSize + (Rows - 1) * CellGap - CellGap + Pad;

        panel = gameObject.AddComponent<RectTransform>();
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        panel.anchoredPosition = new Vector2(-12f, 170f); // правый нижний угол, ВЫШЕ нижней панели героя
        panel.sizeDelta = new Vector2(width, height);

        // ОБНОВЛЕНИЕ 38: отступ 5% от правого и нижнего края экрана (считается от ТЕКУЩЕГО разрешения),
        // панель держится НАД нижней панелью героя; переприменяется при смене разрешения (UIMarginFitter).
        var marginFitter = gameObject.AddComponent<UIMarginFitter>();
        marginFitter.rightMargin = UIMargins.InsetFraction;
        marginFitter.bottomMargin = UIMargins.InsetFraction;
        marginFitter.stackAboveBottomStrip = true;
        marginFitter.Apply();

        // Фон панели — чёрный полупрозрачный (по скрину владельца)
        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.05f, 0.92f);
        bg.raycastTarget = true; // панель перехватывает клики, чтобы не идти в мир

        // Заголовок
        title = CreateLabel(transform, "Магазин", 13, new Color(1f, 0.85f, 0.35f), FontStyle.Bold);
        var tRT = title.rectTransform;
        tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
        tRT.pivot = new Vector2(0f, 1f);
        tRT.anchoredPosition = new Vector2(Pad, -Pad);
        tRT.sizeDelta = new Vector2(width * 0.55f, TitleHeight);
        title.alignment = TextAnchor.MiddleLeft;

        // Золото игрока (живая цифра)
        goldLabel = CreateLabel(transform, "Золото: 0", 13, new Color(1f, 0.84f, 0.15f), FontStyle.Bold);
        var gRT = goldLabel.rectTransform;
        gRT.anchorMin = new Vector2(1f, 1f); gRT.anchorMax = new Vector2(1f, 1f);
        gRT.pivot = new Vector2(1f, 1f);
        gRT.anchoredPosition = new Vector2(-Pad, -Pad);
        gRT.sizeDelta = new Vector2(120f, TitleHeight);
        goldLabel.alignment = TextAnchor.MiddleRight;

        // Ячейки: ряды сверху вниз = тир 1 → тир 4
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                int idx = row * Cols + col;
                var cell = new GameObject("ShopCell_" + idx);
                cell.transform.SetParent(transform, false);
                var cRT = cell.AddComponent<RectTransform>();
                cRT.anchorMin = new Vector2(0f, 1f); cRT.anchorMax = new Vector2(0f, 1f);
                cRT.pivot = new Vector2(0f, 1f);
                float x = Pad + col * (CellSize + CellGap);
                float y = Pad + TitleHeight + 4 + row * (CellSize + CellGap);
                cRT.anchoredPosition = new Vector2(x, -y);
                cRT.sizeDelta = new Vector2(CellSize, CellSize);

                var cImg = cell.AddComponent<Image>();
                cImg.color = new Color(0.13f, 0.13f, 0.16f, 1f);
                cImg.raycastTarget = true;
                cellBg[idx] = cImg;

                // Кнопка покупки
                var btn = cell.AddComponent<Button>();
                var capturedIdx = idx;
                btn.onClick.AddListener(() => Buy(capturedIdx));

                // Тултип (0.2 с наведения, уходит курсор — закрывается)
                var hover = cell.AddComponent<TooltipHover>();
                hover.TitleFn = () => ShopTooltipTitle(capturedIdx);
                hover.BodyFn = () => ShopTooltipBody(capturedIdx);

                // Название предмета (короткое, 2 строки)
                var name = CreateLabel(cell.transform, "", 8, Color.white, FontStyle.Normal);
                var nRT = name.rectTransform;
                nRT.anchorMin = Vector2.zero; nRT.anchorMax = Vector2.one;
                nRT.offsetMin = new Vector2(2f, 16f); nRT.offsetMax = new Vector2(-2f, -2f);
                name.alignment = TextAnchor.UpperCenter;
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.verticalOverflow = VerticalWrapMode.Truncate;
                cellName[idx] = name;

                // Полоска тира (цвет по тиру — как в нижней панели)
                var strip = new GameObject("TierStrip");
                strip.transform.SetParent(cell.transform, false);
                var sRT = strip.AddComponent<RectTransform>();
                sRT.anchorMin = new Vector2(0f, 0f); sRT.anchorMax = new Vector2(1f, 0f);
                sRT.pivot = new Vector2(0.5f, 0f);
                sRT.anchoredPosition = Vector2.zero;
                sRT.sizeDelta = new Vector2(0f, 4f);
                var sImg = strip.AddComponent<Image>();
                sImg.raycastTarget = false;

                // Цена
                var price = CreateLabel(cell.transform, "", 9, new Color(1f, 0.84f, 0.15f), FontStyle.Bold);
                var pRT = price.rectTransform;
                pRT.anchorMin = new Vector2(0f, 0f); pRT.anchorMax = new Vector2(1f, 0f);
                pRT.pivot = new Vector2(0.5f, 0f);
                pRT.anchoredPosition = new Vector2(0f, 5f);
                pRT.sizeDelta = new Vector2(0f, 12f);
                price.alignment = TextAnchor.MiddleCenter;
                cellPrice[idx] = price;

                // полоска тира — держим ссылку через список компонента Image на объекте strip
                cellTiers[idx] = sImg;
            }
        }

        if (stock[0] == null && !everStocked) { PopulateStock(); everStocked = true; }
        RenderAll();
    }

    private Image[] cellTiers = new Image[Cols * Rows];
    private bool everStocked;

    // ==================== ТОВАР ====================

    /// <summary>Раскладка товара: ряд 0 = 4 случайных тир 1 ... ряд 3 = 4 случайных тир 4 (по диздоку).</summary>
    public void PopulateStock()
    {
        for (int row = 0; row < Rows; row++)
        {
            var tier = (EquipmentDatabase.Tier)(row + 1); // T1 = 1 ... T4 = 4
            for (int col = 0; col < Cols; col++)
                stock[row * Cols + col] = EquipmentDatabase.GetRandom(tier);
        }
        Debug.Log("[МАГАЗИН] Товар обновлён: 16 случайных предметов (тир 1–4 по рядам).");
    }

    [ContextMenu("Обновить товары (ресток)")]
    public void Restock()
    {
        PopulateStock();
        RenderAll();
    }

    public static int PriceFor(int idx)
    {
        int row = idx / Cols;
        return row >= 0 && row < RowPrices.Length ? RowPrices[row] : 0;
    }

    // ==================== ПОКУПКА ====================

    private void Buy(int idx)
    {
        var def = stock[idx];
        if (def == null) { Debug.Log("[МАГАЗИН] Ячейка пуста — товар продан."); return; }

        int price = PriceFor(idx);
        var gm = GoldManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("[МАГАЗИН] GoldManager не найден на сцене (повесь на любой объект) — покупка невозможна.");
            return;
        }
        if (!gm.SpendGold(price))
        {
            Debug.Log($"[МАГАЗИН] Не хватает золота: нужно {price}, есть {gm.currentGold}. ({def.displayName})");
            return;
        }

        var eq = FindAnyObjectByType<EquipmentSystem>();
        if (eq == null)
        {
            // Некуда надевать — по диздоку предмет всё равно уходит игроку сундучком около магазина/героя
            EquipmentPickup.SpawnExact(transform.position, def);
            Debug.LogWarning($"[МАГАЗИН] Куплено «{def.displayName}» за {price}, но на герое нет EquipmentSystem — предмет упал сундучком у магазина.");
            stock[idx] = null;
            RenderCell(idx);
            return;
        }

        bool hasFreeSlot = false;
        var slots = eq.Slots;
        if (slots != null)
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == null) { hasFreeSlot = true; break; }

        if (hasFreeSlot)
        {
            EquipmentDatabase.EquipmentDef replaced;
            bool ok = eq.TryEquip(def, out replaced);
            if (ok)
                Debug.Log($"[МАГАЗИН] Куплено «{def.displayName}» за {price} золота — надето (свободный слот). Осталось золота: {gm.currentGold}.");
            else
            {
                EquipmentPickup.SpawnExact(eq.transform.position + new Vector3(1.2f, 0.15f, 0f), def);
                Debug.Log($"[МАГАЗИН] Куплено «{def.displayName}» за {price} золота — надеть не удалось, предмет лежит сундучком у героя. Осталось золота: {gm.currentGold}.");
            }
        }
        else
        {
            // Все 6 слотов заняты — по диздоку предмет появляется около игрока сундучком (НЕ заменяем надетое)
            EquipmentPickup.SpawnExact(eq.transform.position + new Vector3(1.2f, 0.15f, 0f), def);
            Debug.Log($"[МАГАЗИН] Куплено «{def.displayName}» за {price} золота — все слоты заняты, сундучок упал около героя. Осталось золота: {gm.currentGold}.");
        }

        stock[idx] = null; // ячейка распродана
        RenderCell(idx);
    }

    // ==================== ОТРИСОВКА ====================

    private void Update()
    {
        if (Time.unscaledTime >= nextGoldRefresh)
        {
            nextGoldRefresh = Time.unscaledTime + 0.25f;
            var gm = GoldManager.Instance;
            goldLabel.text = gm != null ? "Золото: " + gm.currentGold : "Золото: —";
        }
    }

    private void RenderAll()
    {
        for (int i = 0; i < stock.Length; i++) RenderCell(i);
    }

    private void RenderCell(int idx)
    {
        var def = stock[idx];
        int row = idx / Cols;
        bool sold = def == null;

        cellBg[idx].color = sold ? new Color(0.07f, 0.07f, 0.08f, 1f) : new Color(0.13f, 0.13f, 0.16f, 1f);
        cellName[idx].text = sold ? "ПРОДАНО" : def.displayName;
        cellName[idx].color = sold ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white;
        cellPrice[idx].text = sold ? "—" : PriceFor(idx).ToString();
        cellPrice[idx].color = sold ? new Color(0.55f, 0.55f, 0.55f, 1f) : new Color(1f, 0.84f, 0.15f);
        if (cellTiers[idx] != null)
            cellTiers[idx].color = sold ? new Color(0.2f, 0.2f, 0.2f, 1f) : EquipmentDatabase.GetTierColor((EquipmentDatabase.Tier)(row + 1));
    }

    // ==================== ТУЛТИПЫ (описание + цена) ====================

    public string ShopTooltipTitle(int idx)
    {
        var def = stock[idx];
        if (def == null) return "ПРОДАНО";
        return def.displayName + " (тир " + (int)def.tier + ")";
    }

    public string ShopTooltipBody(int idx)
    {
        var def = stock[idx];
        if (def == null) return "Товар распродан.\n\nКупленный предмет падает сундучком, если все 6 слотов заняты.";
        string t = def.BonusesText();
        return (string.IsNullOrEmpty(t) ? "Нет статов." : t) +
               "\n\nЦена: " + PriceFor(idx) + " золота (магазин = покупка ×2).\nЛКМ — купить.";
    }

    // ==================== ХЕЛПЕРЫ ====================

    private static Text CreateLabel(Transform parent, string text, int size, Color color, FontStyle style)
    {
        var go = new GameObject("Label_" + text);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.text = text;
        t.raycastTarget = false; // клики сквозь текст — важно для ячеек
        t.alignByGeometry = false;
        return t;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType(typeof(EventSystem)) != null) return;
        var esGO = new GameObject("EventSystem_Shop");
        esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        esGO.AddComponent<StandaloneInputModule>();
#endif
    }
}
