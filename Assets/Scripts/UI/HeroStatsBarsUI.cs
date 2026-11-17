using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полоски героя внизу по центру экрана (по мокапу docs/ui/ui1 + ТЗ):
///   Ряд 1: HP (розовый, слева) и Мана (сизый, справа) — заполняются от текущих значений.
///   Ряд 2: ОПЫТ (оранжево-жёлтый, под маной) с текстом "0/100" (текущий/до уровня),
///          правее — номер уровня (1, 2, 3...) и количество нераспределённых очков поинтов.
///
/// Вешается на ЛЮБОЙ объект — проще всего прямо на Main Camera (как HeroStatsPanel),
/// рисуется в Screen Space Overlay, от камеры не зависит. Canvas создаёт/находит сам.
///
/// Полоски — Image.Type.Filled, заполнение обновляется каждый кадр из HeroStats/HeroExperience.
/// Временное решение «чтобы работало»: legacy Text без ассетов, при реворке UI заменить.
/// </summary>
public class HeroStatsBarsUI : MonoBehaviour
{
    [Header("Расположение")]
    [Tooltip("Смещение блока полосок от низа экрана (якорь — низ по центру)")]
    public Vector2 barsOffset = new Vector2(0f, 40f);

    [Tooltip("Ширина одной полоски")]
    public float barWidth = 300f;

    [Tooltip("Высота одной полоски")]
    public float barHeight = 22f;

    [Tooltip("Зазор между полосками")]
    public float barGap = 10f;

    [Tooltip("Ширина зоны подписей справа от полосы опыта (уровень + поинты)")]
    public float labelWidth = 130f;

    [Header("Цвета (цветовое кодирование из AGENTS.md)")]
    public Color hpColor = new Color(1f, 0.42f, 0.65f);      // розовый — HP
    public Color manaColor = new Color(0.48f, 0.56f, 0.71f); // сизый — мана
    public Color xpColor = new Color(1f, 0.65f, 0.1f);       // оранжево-жёлтый — опыт
    public Color barBackColor = new Color(0.08f, 0.08f, 0.1f, 0.9f);
    public Color textColor = Color.white;

    [Header("Цифры")]
    [Tooltip("Показывать числа на полосках HP и маны (на мокапе их нет)")]
    public bool showHpManaNumbers = false;

    private HeroStats hero;
    private HeroExperience heroXP;
    private Image hpFill, manaFill, xpFill;
    private Text hpText, manaText, xpText, levelText, pointsText;
    private float heroSearchTimer;

    void Start()
    {
        BuildBars();
    }

    void Update()
    {
        // Герой спавнится в рантайме — ищем, пока не найдём (раз в полсекунды)
        if (hero == null)
        {
            heroSearchTimer -= Time.deltaTime;
            if (heroSearchTimer <= 0f)
            {
                heroSearchTimer = 0.5f;
                hero = FindAnyObjectByType<HeroStats>();
                if (hero != null) heroXP = hero.GetComponent<HeroExperience>();
            }
            if (hero == null) return;
        }
        if (hpFill == null) return;

        // HP: текущее / максимальное
        float hpRatio = hero.maxHP > 0f ? Mathf.Clamp01(hero.currentHP / hero.maxHP) : 0f;
        hpFill.fillAmount = hpRatio;
        if (showHpManaNumbers && hpText != null)
            hpText.text = $"{Mathf.RoundToInt(hero.currentHP)}/{Mathf.RoundToInt(hero.maxHP)}";

        // Мана: текущая / максимальная
        float manaRatio = hero.maxMana > 0f ? Mathf.Clamp01(hero.currentMana / hero.maxMana) : 0f;
        manaFill.fillAmount = manaRatio;
        if (showHpManaNumbers && manaText != null)
            manaText.text = $"{Mathf.RoundToInt(hero.currentMana)}/{Mathf.RoundToInt(hero.maxMana)}";

        // Опыт: прогресс до следующего уровня + текст "0/100"
        if (heroXP != null && xpFill != null)
        {
            xpFill.fillAmount = heroXP.XPProgress;
            if (xpText != null)
                xpText.text = $"{Mathf.FloorToInt(heroXP.currentXP)}/{Mathf.FloorToInt(heroXP.XPToNextLevel)}";
            if (levelText != null) levelText.text = heroXP.level.ToString();
            if (pointsText != null) pointsText.text = heroXP.unspentPoints.ToString();
        }
    }

    // ---------- Построение UI из кода (паттерн HeroStatsPanel / TeleportSystem) ----------

    private void BuildBars()
    {
        Canvas canvas = GetComponentInParent(typeof(Canvas)) as Canvas;
        if (canvas == null)
            canvas = FindAnyObjectByType(typeof(Canvas)) as Canvas;

        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("MainGame_HUD_Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        Font defaultFont = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font;

        // Корневой контейнер: низ по центру экрана
        GameObject rootObj = new GameObject("HeroStatsBars");
        rootObj.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = rootObj.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = barsOffset;
        rootRect.sizeDelta = new Vector2(barWidth * 2f + barGap + labelWidth, barHeight * 2f + barGap);

        // Ряд 1 (верхний): HP слева, мана справа (как на мокапе).
        // Дети якорятся к ВЕРХУ контейнера, y отсчитывается вниз — поэтому верхний ряд = 0.
        hpFill = CreateBar(rootObj.transform, "HP_Bar", hpColor, defaultFont,
            new Vector2(0f, 0f), out hpText);

        manaFill = CreateBar(rootObj.transform, "Mana_Bar", manaColor, defaultFont,
            new Vector2(barWidth + barGap, 0f), out manaText);

        // Ряд 2 (нижний): опыт ПОД маной (справа), правее — уровень и нераспределённые очки
        xpFill = CreateBar(rootObj.transform, "XP_Bar", xpColor, defaultFont,
            new Vector2(barWidth + barGap, barHeight + barGap), out xpText);

        // Уровень (крупная цифра) и поинты — правее полосы опыта
        levelText = CreateLabel(rootObj.transform, "Level_Label", defaultFont,
            new Vector2(barWidth * 2f + barGap + 4f, 0f),
            new Vector2(labelWidth * 0.4f, barHeight * 2f + barGap),
            TextAnchor.LowerLeft, Mathf.RoundToInt(barHeight * 1.1f), FontStyle.Bold);
        levelText.text = "1";

        pointsText = CreateLabel(rootObj.transform, "Points_Label", defaultFont,
            new Vector2(barWidth * 2f + barGap + 4f + labelWidth * 0.45f, 0f),
            new Vector2(labelWidth * 0.55f, barHeight * 2f + barGap),
            TextAnchor.LowerLeft, Mathf.RoundToInt(barHeight * 0.8f), FontStyle.Normal);
        pointsText.text = "0";
    }

    /// <summary>Создаёт одну полоску: тёмный фон + заполнение (Filled) + опциональный текст по центру.</summary>
    private Image CreateBar(Transform parent, string barName, Color fillColor, Font font,
        Vector2 localPos, out Text centerText)
    {
        // Фон
        GameObject bgObj = new GameObject(barName);
        bgObj.transform.SetParent(parent, false);

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 1f);
        bgRect.anchorMax = new Vector2(0f, 1f);
        bgRect.pivot = new Vector2(0f, 1f);
        bgRect.anchoredPosition = localPos;
        bgRect.sizeDelta = new Vector2(barWidth, barHeight);

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = barBackColor;
        bgImage.raycastTarget = false; // клики RTS проходят сквозь HUD

        // Заполнение (чуть меньше фона, чтобы был виден тёмный кант)
        GameObject fillObj = new GameObject(barName + "_Fill");
        fillObj.transform.SetParent(bgObj.transform, false);

        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;

        // Текст по центру полоски (для опыта — обязателен "0/100")
        centerText = null;
        bool needText = (barName == "XP_Bar") || showHpManaNumbers;
        if (needText)
        {
            GameObject txtObj = new GameObject(barName + "_Text");
            txtObj.transform.SetParent(bgObj.transform, false);

            RectTransform txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;

            Text txt = txtObj.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = Mathf.RoundToInt(barHeight * 0.7f);
            txt.color = textColor;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;

            centerText = txt;
        }

        return fillImage;
    }

    /// <summary>Создаёт текстовую подпись (уровень / очки) справа от полосы опыта.</summary>
    private Text CreateLabel(Transform parent, string labelName, Font font,
        Vector2 localPos, Vector2 size, TextAnchor anchor, int fontSize, FontStyle style)
    {
        GameObject obj = new GameObject(labelName);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = localPos;
        rect.sizeDelta = size;

        Text txt = obj.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = textColor;
        txt.alignment = anchor;
        txt.raycastTarget = false;

        return txt;
    }
}
