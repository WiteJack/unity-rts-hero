using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ОБНОВЛЕНИЕ 26 — ЗДАНИЕ ТРАНСМУТАЦИИ (по диздоку, ui3.png).
/// Панель в правом нижнем углу ВЫШЕ панели магазина: показывает 6 надетых слотов героя + закрытый слот.
/// Кнопки:
/// - ТРАНСМУТАЦИЯ — предмет в первой (верхней-левой) ячейке заменяется случайным того же тира.
///   Стоимость: тир 1 = 100, тир 2 = 200, тир 3 = 300, тир 4 = 400, тир 5 = 500 золота.
/// - СЛИЯНИЕ — если первые ДВЕ ячейки заняты предметами ОДНОГО тира, оба уничтожаются и выдаётся
///   предмет тиром выше (пример: два тир 3 = один тир 4). Стоимость за тир РЕЗУЛЬТАТА:
///   тир 2 = 100, тир 3 = 300, тир 4 = 1200, тир 5 = 2800 золота.
/// - ПОКУПКА СЛУЧАЙНОГО (Обновление 37) — 4 кнопки по тирам: Тир 1 = 1000, Тир 2 = 2500, Тир 3 = 5000, Тир 4 = 12000 золота (26000 — тир 5, когда появится). У всех кнопок всплывают описания.
/// Подсветка (по запросу владельца): первая ячейка — ГОЛУБАЯ (цель трансмутации), вторая ячейка — СИНЯЯ (пара слияния).
/// Закрытый 4-й слот — под специальных героев (заглушка по диздоку).
/// Создаётся кодом, на сцене ничего добавлять к UI не нужно. Панель открывает TransmuteBuilding.
/// </summary>
public class TransmuteUI : MonoBehaviour
{
    public static TransmuteUI Instance { get; private set; }

    private const int Cols = 4;
    private const int Rows = 2;
    private const float CellSize = 58f;
    private const float CellGap = 6f;
    private const float Pad = 8f;
    private const float TitleHeight = 24f;
    private const float ButtonsHeight = 30f;
    private const float HintHeight = 14f;

    // Голубая/синяя подсветка ячеек (по запросу владельца)
    private static readonly Color TransmuteTargetColor = new Color(0.30f, 0.80f, 1.00f, 1f); // голубая — цель трансмутации
    private static readonly Color MergePairColor = new Color(0.28f, 0.42f, 1.00f, 1f);       // синяя — пара слияния
    private static readonly Color CellNormal = new Color(0.13f, 0.13f, 0.16f, 1f);
    private static readonly Color CellEmpty = new Color(0.09f, 0.09f, 0.11f, 1f);

    // Цены по диздоку: трансмутация по тиру, слияние по тиру РЕЗУЛЬТАТА (2/3/4/5), покупка случайного по тиру.
    public static readonly int[] TransmuteCosts = { 100, 200, 300, 400, 500 };
    public static readonly int[] MergeCosts = { 100, 300, 1200, 2800 };     // индекс: тир результата - 2
    public static readonly int[] BuyRandomCosts = { 1000, 2500, 5000, 12000, 26000 };

    private Canvas canvas;
    private Text goldLabel;

    // 6 надетых слотов героя (ячейки 0..5) + ячейка 6 — закрытый слот; ячейка 7 — пустой заполнитель
    private RectTransform[] cells = new RectTransform[Cols * Rows];
    private Text[] cellName = new Text[Cols * Rows];
    private Image[] cellBg = new Image[Cols * Rows];

    private EquipmentSystem eq;
    private float nextRefresh;

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

        var rootGO = new GameObject("Transmute_Root");
        rootGO.transform.SetParent(cvs.transform, false);
        Instance = rootGO.AddComponent<TransmuteUI>();
        Instance.canvas = cvs;
        Instance.Build();
        Debug.Log("[ТРАНСМУТАЦИЯ] TransmuteUI создан (Обновление 26/37): 6 слотов героя + закрытый слот, кнопки Трансмутация/Слияние + покупка случайного тирами 1..4.");
    }

    public static void Hide()
    {
        if (Instance != null) Instance.gameObject.SetActive(false);
    }

    private void Build()
    {
        float width = Pad * 2 + Cols * CellSize + (Cols - 1) * CellGap;
        float height = Pad + TitleHeight + 4 + Rows * CellSize + (Rows - 1) * CellGap + 6 + ButtonsHeight + 6 + ButtonsHeight + 4 + HintHeight + Pad; // + ряд 2 кнопок (Обновление 37)

        var panel = gameObject.AddComponent<RectTransform>();
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        panel.anchoredPosition = new Vector2(-12f, 478f); // ВЫШЕ панели магазина (магазин ~296px от 170)
        panel.sizeDelta = new Vector2(width, height);

        // Фон — чёрный полупрозрачный (как у магазина)
        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.05f, 0.92f);
        bg.raycastTarget = true;

        // Заголовок
        var title = CreateLabel(transform, "Трансмутация", 13, new Color(0.35f, 0.85f, 1f), FontStyle.Bold);
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

        // Ячейки: ряд 1 = слоты героя 1..3 + закрытый слот; ряд 2 = слоты героя 4..6
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                int idx = row * Cols + col;
                if (idx == 7) continue; // заполнитель нижнего правого угла

                var cell = new GameObject(idx == 6 ? "LockedSlot" : "EquipSlot_" + idx);
                cell.transform.SetParent(transform, false);
                var cRT = cell.AddComponent<RectTransform>();
                cRT.anchorMin = new Vector2(0f, 1f); cRT.anchorMax = new Vector2(0f, 1f);
                cRT.pivot = new Vector2(0f, 1f);
                float x = Pad + col * (CellSize + CellGap);
                float y = Pad + TitleHeight + 4 + row * (CellSize + CellGap);
                cRT.anchoredPosition = new Vector2(x, -y);
                cRT.sizeDelta = new Vector2(CellSize, CellSize);
                cells[idx] = cRT;

                var cImg = cell.AddComponent<Image>();
                cImg.color = CellNormal;
                cImg.raycastTarget = true;
                cellBg[idx] = cImg;

                // Тултип по ячейке (описание предмета / закрытого слота)
                var hover = cell.AddComponent<TooltipHover>();
                var capturedIdx = idx;
                hover.TitleFn = () => CellTooltipTitle(capturedIdx);
                hover.BodyFn = () => CellTooltipBody(capturedIdx);

                // Надпись в ячейке
                var name = CreateLabel(cell.transform, "", 8, Color.white, FontStyle.Normal);
                var nRT = name.rectTransform;
                nRT.anchorMin = Vector2.zero; nRT.anchorMax = Vector2.one;
                nRT.offsetMin = new Vector2(2f, 2f); nRT.offsetMax = new Vector2(-2f, -2f);
                name.alignment = TextAnchor.MiddleCenter;
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.verticalOverflow = VerticalWrapMode.Truncate;
                cellName[idx] = name;
            }
        }

        // Кнопки (Обновление 37): ряд 1 — Трансмутация | Слияние; ряд 2 — покупка случайного по тирам 1..4 (диздок «Снаряжение»)
        float btnY = Pad + TitleHeight + 4 + Rows * CellSize + (Rows - 1) * CellGap;
        float row2Y = btnY + ButtonsHeight + 6f;
        float btnW2 = (width - Pad * 2 - 6f) / 2f;
        float btnW4 = (width - Pad * 2 - 3 * 6f) / 4f;

        // Ряд 1: Трансмутация | Слияние (механика не менялась, только ширина кнопок)
        string[] captions = { "Трансмутация", "Слияние" };
        for (int i = 0; i < 2; i++)
        {
            var btn = CreatePanelButton(captions[i], new Vector2(Pad + i * (btnW2 + 6f), -(btnY + 4)), new Vector2(btnW2, ButtonsHeight), 10);
            int captured = i;
            btn.onClick.AddListener(() => OnButton(captured));
            var hover = btn.gameObject.AddComponent<TooltipHover>();
            hover.TitleFn = () => ButtonTooltipTitle(captured);
            hover.BodyFn = () => ButtonTooltipBody(captured);
        }

        // Ряд 2: вместо одной кнопки «Купить случайное» — 4 кнопки по тирам (цены 1000/2500/5000/12000, по диздоку)
        for (int i = 0; i < 4; i++)
        {
            int tier = i + 1;
            var btn = CreatePanelButton("Тир " + tier + ": " + BuyRandomCosts[i], new Vector2(Pad + i * (btnW4 + 6f), -(row2Y + 4)), new Vector2(btnW4, ButtonsHeight), 9);
            btn.onClick.AddListener(() => OnButton(1 + tier));
            var hover = btn.gameObject.AddComponent<TooltipHover>();
            int capturedTier = tier;
            hover.TitleFn = () => ButtonTooltipTitle(1 + capturedTier);
            hover.BodyFn = () => ButtonTooltipBody(1 + capturedTier);
        }

        // Подсказка подсветки
        var hint = CreateLabel(transform, "голубая = трансмутация · синяя = пара слияния", 8, new Color(0.6f, 0.7f, 0.9f), FontStyle.Normal);
        var hRT = hint.rectTransform;
        hRT.anchorMin = new Vector2(0f, 0f); hRT.anchorMax = new Vector2(1f, 0f);
        hRT.pivot = new Vector2(0.5f, 0f);
        hRT.anchoredPosition = new Vector2(0f, Pad);
        hRT.sizeDelta = new Vector2(0f, HintHeight);
        hint.alignment = TextAnchor.MiddleCenter;

        RenderAll();
    }

    // ==================== КНОПКИ ====================

    private void OnButton(int btnIdx)
    {
        if (eq == null) eq = FindAnyObjectByType<EquipmentSystem>();
        if (eq == null || eq.Slots == null) { Debug.LogWarning("[ТРАНСМУТАЦИЯ] На герое нет EquipmentSystem — обработка невозможна."); return; }
        if (btnIdx == 0) DoTransmute();
        else if (btnIdx == 1) DoMerge();
        else DoBuyRandom(btnIdx - 1); // кнопки 2..5 — покупка случайного тиров 1..4 (Обновление 37)
    }

    private bool PayGold(int cost)
    {
        var gm = GoldManager.Instance;
        if (gm == null) { Debug.LogWarning("[ТРАНСМУТАЦИЯ] GoldManager не найден на сцене — операция невозможна."); return false; }
        if (!gm.SpendGold(cost))
        {
            Debug.Log($"[ТРАНСМУТАЦИЯ] Не хватает золота: нужно {cost}, есть {gm.currentGold}.");
            return false;
        }
        return true;
    }

    /// <summary>ТРАНСМУТАЦИЯ: случайное снаряжение того же тира в первой (верхней-левой) ячейке.</summary>
    public void DoTransmute()
    {
        if (eq == null) eq = FindAnyObjectByType<EquipmentSystem>();
        var slots = eq.Slots;
        var old = slots != null && slots.Length > 0 ? slots[0] : null;
        if (old == null) { Debug.Log("[ТРАНСМУТАЦИЯ] Первая (верхняя-левая) ячейка пуста — трансмутировать нечего."); return; }

        int tier = (int)old.tier;
        int cost = TransmuteCosts[Mathf.Clamp(tier - 1, 0, TransmuteCosts.Length - 1)];
        if (!PayGold(cost)) return;

        eq.Unequip(0); // предмет из первой ячейки УНИЧТОЖАЕТСЯ (по диздоку — замена)
        var fresh = EquipmentDatabase.GetRandom((EquipmentDatabase.Tier)tier);
        if (fresh != null)
        {
            EquipmentDatabase.EquipmentDef trash;
            eq.TryEquip(fresh, out trash);
            Debug.Log($"[ТРАНСМУТАЦИЯ] «{old.displayName}» (тир {tier}) → «{fresh.displayName}» (тот же тир). Цена: {cost} золота.");
        }
        RenderAll();
    }

    /// <summary>СЛИЯНИЕ: первые две ячейки одного тира → обе уничтожаются, выдаётся предмет тиром выше.</summary>
    public void DoMerge()
    {
        if (eq == null) eq = FindAnyObjectByType<EquipmentSystem>();
        var slots = eq.Slots;
        if (slots == null || slots.Length < 2) return;
        var a = slots[0]; var b = slots[1];
        if (a == null || b == null) { Debug.Log("[ТРАНСМУТАЦИЯ] Слияние: нужны ПРЕДМЕТЫ в первых ДВУХ ячейках (верхняя-левая и следующая)."); return; }
        if ((int)a.tier != (int)b.tier) { Debug.Log($"[ТРАНСМУТАЦИЯ] Слияние: тиры должны совпадать — сейчас тир {(int)a.tier} ({a.displayName}) и тир {(int)b.tier} ({b.displayName})."); return; }

        int resultTier = (int)a.tier + 1;
        if (resultTier > 4)
        {
            Debug.Log("[ТРАНСМУТАЦИЯ] Слияние двух тир 4 — тир 5 ещё не введён в базу (задел по диздоку: стоимость 2800).");
            return;
        }
        int cost = MergeCosts[resultTier - 2];
        if (!PayGold(cost)) return;

        eq.Unequip(1); // обе ячейки уничтожаются
        eq.Unequip(0);
        var fresh = EquipmentDatabase.GetRandom((EquipmentDatabase.Tier)resultTier);
        if (fresh != null)
        {
            EquipmentDatabase.EquipmentDef trash;
            eq.TryEquip(fresh, out trash); // свободный слот 0 — предмет встанет в первую ячейку
            Debug.Log($"[ТРАНСМУТАЦИЯ] Слияние: «{a.displayName}» + «{b.displayName}» (два тир {(int)a.tier}) → «{fresh.displayName}» (тир {resultTier}). Цена: {cost} золота.");
        }
        RenderAll();
    }

    /// <summary>ПОКУПКА СЛУЧАЙНОГО ВЫБРАННОГО ТИРА (1..4, Обновление 37): цена по диздоку; свободный слот — надеть, все заняты — сундучок у героя.</summary>
    public void DoBuyRandom(int tier) // тир выбирается кнопкой (Обновление 37)
    {
        if (eq == null) eq = FindAnyObjectByType<EquipmentSystem>();
        int cost = BuyRandomCosts[Mathf.Clamp(tier - 1, 0, BuyRandomCosts.Length - 1)];
        if (!PayGold(cost)) return;

        var def = EquipmentDatabase.GetRandom((EquipmentDatabase.Tier)tier);
        if (def == null) { Debug.LogWarning($"[ТРАНСМУТАЦИЯ] В базе нет предметов тир {tier}."); return; }

        var gm = GoldManager.Instance;
        if (eq.Slots != null && System.Array.IndexOf(eq.Slots, null) >= 0)
        {
            EquipmentDatabase.EquipmentDef trash;
            if (eq.TryEquip(def, out trash))
                Debug.Log($"[ТРАНСМУТАЦИЯ] Куплено случайное «{def.displayName}» (тир {tier}) за {cost} золота. Осталось: {gm.currentGold}.");
            else
                EquipmentPickup.SpawnExact(eq.transform.position + new Vector3(1.2f, 0.15f, 0f), def);
        }
        else
        {
            // все 6 слотов заняты — сундучок у героя (надетое НЕ заменяем, по диздоку)
            EquipmentPickup.SpawnExact(eq.transform.position + new Vector3(1.2f, 0.15f, 0f), def);
            Debug.Log($"[ТРАНСМУТАЦИЯ] Куплено случайное «{def.displayName}» (тир {tier}) за {cost} золота — все слоты заняты, сундучок у героя. Осталось: {gm.currentGold}.");
        }
        RenderAll();
    }

    // ==================== ОТРИСОВКА (живая) ====================

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.25f;
        RenderAll();
    }

    private void RenderAll()
    {
        var gm = GoldManager.Instance;
        goldLabel.text = gm != null ? "Золото: " + gm.currentGold : "Золото: —";

        if (eq == null) eq = FindAnyObjectByType<EquipmentSystem>();
        var slots = (eq != null) ? eq.Slots : null;

        for (int idx = 0; idx < Cols * Rows; idx++)
        {
            if (idx == 7) continue;

            if (idx == 6)
            {
                // закрытый слот для специальных героев
                cellBg[idx].color = CellEmpty;
                cellName[idx].text = "🔒 закр.";
                cellName[idx].color = new Color(0.45f, 0.45f, 0.5f);
                continue;
            }

            int heroSlot = idx;
            var def = slots != null && heroSlot < slots.Length ? slots[heroSlot] : null;

            // подсветка по запросу владельца: ячейка 0 — голубая (цель трансмутации), ячейка 1 — синяя (пара слияния)
            if (heroSlot == 0) cellBg[idx].color = TransmuteTargetColor * (def != null ? 1f : 0.45f);
            else if (heroSlot == 1) cellBg[idx].color = MergePairColor * (def != null ? 1f : 0.45f);
            else cellBg[idx].color = def != null ? CellNormal : CellEmpty;

            cellName[idx].text = def != null ? def.displayName : (heroSlot == 0 ? "пусто (T)" : heroSlot == 1 ? "пусто (С)" : "пусто");
            cellName[idx].color = def != null ? Color.white : new Color(0.45f, 0.45f, 0.5f);
        }
    }

    // ==================== ТУЛТИПЫ КНОПОК (Обновление 37: описания во всплывающем окошке) ====================

    private string ButtonTooltipTitle(int btnIdx)
    {
        if (btnIdx == 0) return "Трансмутация";
        if (btnIdx == 1) return "Слияние";
        return "Купить случайное — тир " + (btnIdx - 1);
    }

    private string ButtonTooltipBody(int btnIdx)
    {
        if (btnIdx == 0)
            return "Заменяет предмет в ГОЛУБОЙ (первой) ячейке на случайный предмет ТОГО ЖЕ тира. Старый предмет уничтожается.\nЦена по тиру предмета: тир 1 = 100, тир 2 = 200, тир 3 = 300, тир 4 = 400, тир 5 = 500 золота.";
        if (btnIdx == 1)
            return "Уничтожает предметы в ГОЛУБОЙ и СИНЕЙ ячейках (тиры должны совпадать) и выдаёт предмет ТИРОМ ВЫШЕ в свободную ячейку.\nЦена по тиру результата: тир 2 = 100, тир 3 = 300, тир 4 = 1200, тир 5 = 2800 золота.";
        int tier = Mathf.Clamp(btnIdx - 1, 1, BuyRandomCosts.Length);
        int cost = BuyRandomCosts[tier - 1];
        return "Покупает случайное снаряжение ТИРА " + tier + " за " + cost + " золота (цены по диздоку).\nЕсть свободный слот — предмет сразу наденется на героя.\nВсе 6 слотов заняты — сундучок выпадет у ног героя.";
    }

    // ==================== ТУЛТИПЫ ====================

    private string CellTooltipTitle(int idx)
    {
        if (idx == 6) return "Закрытый слот";
        if (eq == null) eq = FindAnyObjectByType<EquipmentSystem>();
        var slots = eq != null ? eq.Slots : null;
        var def = slots != null && idx < slots.Length ? slots[idx] : null;
        if (def == null) return "Слот героя " + (idx + 1) + " — пусто";
        return def.displayName + " (тир " + (int)def.tier + ") — слот " + (idx + 1);
    }

    private string CellTooltipBody(int idx)
    {
        if (idx == 6) return "Закрытый слот для специальных героев со спец возможностями (по диздоку).\nПока недоступен.";
        if (eq == null) eq = FindAnyObjectByType<EquipmentSystem>();
        var slots = eq != null ? eq.Slots : null;
        var def = slots != null && idx < slots.Length ? slots[idx] : null;
        string head = idx == 0 ? "\nГОЛУБАЯ ячейка — цель кнопки «Трансмутация»." :
                      idx == 1 ? "\nСИНЯЯ ячейка — пара кнопки «Слияние» (нужен тот же тир в обеих)." : "";
        if (def == null) return "Слот героя пуст." + head;
        string t = def.BonusesText();
        return (string.IsNullOrEmpty(t) ? "Нет статов." : t) + head;
    }

    // ==================== ХЕЛПЕРЫ ====================

    /// <summary>Однотипная кнопка панели: фон + Button + подпись (Обновление 37 — раньше дублировалось в цикле).</summary>
    private Button CreatePanelButton(string caption, Vector2 anchoredPos, Vector2 size, int fontSize)
    {
        var btnGO = new GameObject("Btn_" + caption);
        btnGO.transform.SetParent(transform, false);
        var bRT = btnGO.AddComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 1f); bRT.anchorMax = new Vector2(0f, 1f);
        bRT.pivot = new Vector2(0f, 1f);
        bRT.anchoredPosition = anchoredPos;
        bRT.sizeDelta = size;
        var bImg = btnGO.AddComponent<Image>();
        bImg.color = new Color(0.18f, 0.18f, 0.24f, 1f);
        var btn = btnGO.AddComponent<Button>();
        var label = CreateLabel(btnGO.transform, caption, fontSize, new Color(1f, 0.85f, 0.35f), FontStyle.Bold);
        var lRT = label.rectTransform;
        lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
        lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;
        label.alignment = TextAnchor.MiddleCenter;
        return btn;
    }

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
        t.raycastTarget = false;
        t.alignByGeometry = false;
        return t;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType(typeof(EventSystem)) != null) return;
        var esGO = new GameObject("EventSystem_Transmute");
        esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        esGO.AddComponent<StandaloneInputModule>();
#endif
    }
}
