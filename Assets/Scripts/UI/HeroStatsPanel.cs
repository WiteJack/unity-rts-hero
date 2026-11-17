using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель характеристик героя (UI, как на мокапе docs/ui/ui1).
/// Вешается на ЛЮБОЙ объект сцены (панель сама создаёт/находит Canvas).
///
/// Показывает ХАРАКТЕРИСТИКИ (не атрибуты): слева сокращённые названия,
/// справа ИТОГОВЫЕ значения (с учётом всех формул), значения обновляются сами.
/// Список (по мокапу): Здоровье, Реген HP, Атака, Скор. атаки, Дальн. атаки,
/// Скор. движ., Защита, Мана, Реген маны.
///
/// Герой ищется автоматически (спавнится GameSpawner'ом в рантайме).
/// Временное решение «чтобы работало»: legacy Text без ассетов, при реворке UI заменить.
/// </summary>
public class HeroStatsPanel : MonoBehaviour
{
    [Header("Панель")]
    public bool showPanel = true;

    [Tooltip("Смещение панели от низа экрана (якорь — низ по центру)")]
    public Vector2 panelOffset = new Vector2(0f, 150f);

    [Tooltip("Ширина панели")]
    public float panelWidth = 340f;

    [Tooltip("Высота одной строки (название + значение)")]
    public float rowHeight = 24f;

    [Header("Оформление")]
    public int fontSize = 16;
    public Color panelColor = new Color(1f, 1f, 1f, 0.85f);
    public Color textColor = Color.black;

    // Сокращённые названия характеристик (порядок = порядок строк)
    private static readonly string[] rowNames =
    {
        "Здоровье",
        "Реген HP",
        "Атака",
        "Скор. атаки",
        "Дальн. атаки",
        "Скор. движ.",
        "Защита",
        "Мана",
        "Реген маны"
    };

    private HeroStats hero;
    private Text[] valueTexts;
    private float heroSearchTimer;
    private float refreshTimer;

    void Start()
    {
        BuildPanel();
    }

    void Update()
    {
        if (!showPanel || valueTexts == null) return;

        // Герой спавнится в рантайме — ищем, пока не найдём (раз в полсекунды, не каждый кадр)
        if (hero == null)
        {
            heroSearchTimer -= Time.deltaTime;
            if (heroSearchTimer <= 0f)
            {
                heroSearchTimer = 0.5f;
                hero = FindAnyObjectByType<HeroStats>();
            }
            if (hero == null) return;
        }

        // Обновляем значения 5 раз в секунду — формулы дешёвые, но зачем лишнее
        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = 0.2f;

        RefreshValues();
    }

    private void RefreshValues()
    {
        var agent = hero.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent == null) agent = hero.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();

        var battleAI = hero.GetComponent<BattleAI>();
        if (battleAI == null) battleAI = hero.GetComponentInParent<BattleAI>();

        // ИТОГОВЫЕ значения по формулам проекта (не стартовые)
        Set(0, Format(hero.maxHP, 0));                               // Здоровье: живучесть × 25
        Set(1, Format(hero.endurance * 0.5f, 1));                    // Реген HP: выносливость × 0.5
        Set(2, Format(hero.strength * 5f, 0));                       // Атака: сила × 5
        Set(3, Format(hero.GetCurrentAttackSpeed(), 2));             // Скор. атаки: (ловкость + сила воли) × 0.05
        Set(4, battleAI != null ? Format(battleAI.attackRange, 0) : "—"); // Дальн. атаки: из BattleAI
        Set(5, agent != null ? Format(agent.speed, 1) : "—");        // Скор. движ.: из NavMeshAgent
        Set(6, Format(hero.GetDefense(), 1));                        // Защита: (выносливость + живучесть) × 0.1
        Set(7, Format(hero.maxMana, 0));                             // Мана: мудрость × 15
        Set(8, Format(hero.willpower * 0.5f, 1));                    // Реген маны: сила воли × 0.5
    }

    private void Set(int index, string value)
    {
        if (index >= 0 && index < valueTexts.Length && valueTexts[index] != null)
            valueTexts[index].text = value;
    }

    private static string Format(float v, int digits)
    {
        return v.ToString("F" + digits);
    }

    // ---------- Построение UI из кода (паттерн TeleportSystem) ----------

    private void BuildPanel()
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

        float panelHeight = rowNames.Length * rowHeight + 16f;

        // Фон панели
        GameObject panelObj = new GameObject("HeroStatsPanel");
        panelObj.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f); // низ по центру, как на мокапе
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = panelOffset;
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = panelColor;
        panelImage.raycastTarget = false; // не перекрываем клики (RTS-управление)

        // Строки: название слева, значение справа
        valueTexts = new Text[rowNames.Length];
        float y = -8f;

        for (int i = 0; i < rowNames.Length; i++)
        {
            // Название (сокращённое)
            GameObject nameObj = new GameObject("Stat_" + rowNames[i]);
            nameObj.transform.SetParent(panelObj.transform, false);

            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(0f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(12f, y);
            nameRect.sizeDelta = new Vector2(panelWidth * 0.55f, rowHeight);

            Text nameText = nameObj.AddComponent<Text>();
            nameText.font = defaultFont;
            nameText.fontSize = fontSize;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = textColor;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.text = rowNames[i];
            nameText.raycastTarget = false;

            // Значение (итоговое)
            GameObject valObj = new GameObject("Value_" + rowNames[i]);
            valObj.transform.SetParent(panelObj.transform, false);

            RectTransform valRect = valObj.AddComponent<RectTransform>();
            valRect.anchorMin = new Vector2(1f, 1f);
            valRect.anchorMax = new Vector2(1f, 1f);
            valRect.pivot = new Vector2(1f, 1f);
            valRect.anchoredPosition = new Vector2(-12f, y);
            valRect.sizeDelta = new Vector2(panelWidth * 0.4f, rowHeight);

            Text valText = valObj.AddComponent<Text>();
            valText.font = defaultFont;
            valText.fontSize = fontSize;
            valText.color = textColor;
            valText.alignment = TextAnchor.MiddleRight;
            valText.raycastTarget = false;

            valueTexts[i] = valText;
            y -= rowHeight;
        }
    }
}
