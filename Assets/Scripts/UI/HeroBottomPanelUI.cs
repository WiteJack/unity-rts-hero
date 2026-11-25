using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// НИЖНЯЯ ПАНЕЛЬ ИНТЕРФЕЙСА (Обновление 20): единый КОНТЕЙНЕР в нижней панели —
/// скиллы → атрибуты → уникальный артефакт → уникальный шмот → обычный шмот (слева направо);
/// ХАРАКТЕРИСТИКИ — отдельный бар слева снизу.
/// Вешается на ЛЮБОЙ объект (проще всего на Main Camera, как HeroStatsBarsUI).
///
/// СЛЕВА внизу — КНОПКИ НАВЫКОВ АНДРОНА (5 штук, синие, идут 1 2 3 4 5 слева направо):
///   - иконка (нет картинки — БЕЛАЯ иконка, правило ТЗ),
///   - ЦИФРА АКТИВАЦИИ — уровень навыка "N/5" (у 5-й кнопки — нераспределённые очки),
///   - затемнение перезарядки у активных навыков,
///   - подсказка «жми +» на неизученном навыке (когда его уже можно учить),
///   - оранжевый "+" когда навык можно прокачать (клик по "+" изучает следующий уровень).
///
///   - АТРИБУТЫ (в контейнере после скиллов): Сила, Живучесть, Выносливость, Ловкость,
///     Мудрость, Сила воли — 6 живых строк, читаются прямо из HeroStats ПОСЛЕ всех дельт
///     (шмот + «+»-очки + уники), обновляются 5 раз/сек;
///   - ХАРАКТЕРИСТИКИ: 9 живых строк вынесены в ОТДЕЛЬНЫЙ БАР слева снизу (HeroCharPanel_Root), 5 раз/сек;
///   - порядок слотов шмота: уникальный артефакт «М» → уникальное снаряжение «Щ» → 6 обычных;
///   ВАЖНО: старый компонент HeroStatsPanel теперь можно ВЫКЛЮЧИТЬ на камере,
///   чтобы характеристики не рисовались дважды.
///
/// Клик по обычному слоту — снять предмет (он падает на пол сундучком).
/// Клик по активному навыку — использовать. Клик по 5-й кнопке — вложить 1 очко характеристик.
///
/// НА ГЕРОЕ должны висеть: AndronSkills (навыки), EquipmentSystem (6 слотов), AndronUniqueEquipment
/// (уники, опционально). Все они добавляются пользователем на префаб героя; панель ищет их сама.
/// Кнопки кликабельны (EventSystem создаётся автоматически); полоски HP/маны/опыта HeroStatsBarsUI
/// продолжают пропускать клики RTS.
/// </summary>
public class HeroBottomPanelUI : MonoBehaviour
{
    [Header("Слоты шмота (в контейнере: ун. артефакт, ун. шмот, 6 обычных)")]
    public float slotSize = 48f;
    public float slotGap = 6f;

    [Header("Кнопки навыков внутри контейнера (1 2 3 4 5, плюсик в конце)")]
    public float skillSize = 52f;
    public float skillGap = 10f;

    [Header("Обновление 20 — отдельный бар ХАРАКТЕРИСТИК слева снизу")]
    [Tooltip("Сдвиг панели характеристик от левого нижнего угла экрана")]
    public Vector2 charPanelOffset = new Vector2(20f, 20f);
    public float rowHeight = 18f;
    public int infoFontSize = 13;

    [Header("Контейнер: скиллы → атрибуты → ун. артефакт → ун. шмот → обычный шмот")]
    [Tooltip("Сдвиг контейнера от левого низа (ставится правее бара характеристик)")]
    public Vector2 containerOffset = new Vector2(270f, 20f);
    [Tooltip("Зазор между секциями контейнера (скиллы / атрибуты / шмот)")]
    public float sectionGap = 30f;
    public float colWidth = 200f;

    [Header("Цвета")]
    public Color panelBackColor = new Color(0.06f, 0.06f, 0.09f, 0.65f);
    public Color skillBackColor = new Color(0.07f, 0.33f, 0.45f, 0.95f);  // синий навык с мокапа
    public Color slotBackColor = new Color(0.12f, 0.14f, 0.25f, 0.95f);   // обычные слоты
    public Color uniqueSlotBackColor = new Color(0.18f, 0.14f, 0.05f, 0.95f);
    public Color upgradeColor = new Color(1f, 0.65f, 0.1f);               // оранжевый "+" (как полоска опыта)
    public Color textColor = Color.white;
    public Color specialSlotColor = new Color(1f, 0.82f, 0.2f);           // золото — уники
    public Color headerColor = new Color(1f, 0.85f, 0.4f);                // заголовки колонок

    private HeroStats hero;
    private HeroExperience heroXP;
    private AndronSkills skills;
    private EquipmentSystem equipment;
    private AndronUniqueEquipment uniques;
    private BattleAI heroAI;
    private UnityEngine.AI.NavMeshAgent heroAgent;
    private float heroSearchTimer;
    private float infoRefreshTimer;

    private Font font;
    private Image[] slotBottoms;    // тонкая полоска-подсветка внизу слота (цвет тира)
    private Image[] slotIcons;      // иконка предмета (белая, если нет спрайта)
    private Text[]  slotTierTexts;  // "Т1".."Т4" / метки уников

    private SkillButton[] skillButtons;
    private Text[] attrValueTexts;  // ЖИВЫЕ цифры атрибутов (контейнер, после скиллов)
    private Text[] charValueTexts;  // живые цифры характеристик (отдельный бар слева снизу)

    private static readonly string[] AttrNames = { "Сила", "Живучесть", "Выносливость", "Ловкость", "Мудрость", "Сила воли" };
    private static readonly string[] CharNames =
    {
        "Здоровье", "Реген. здоровья", "Атака", "Скор. атаки", "Дальн. атаки",
        "Скор. движения", "Защита", "Мана", "Реген. маны"
    };

    /// <summary>Кнопка навыка: фон, иконка, цифра уровня, "+", затемнение перезарядки.</summary>
    private class SkillButton
    {
        public Image bg;
        public Image icon;
        public Text levelText;
        public Text hotkeyText;
        public Image plus;
        public Image cooldown;
        public Text cooldownText;
        public AndronSkills.SkillId id;
        public string hotkey;
        public RectTransform root;
    }

    // ---------- БЕЛЫЙ СПРАЙТ (по уроку HeroStatsBarsUI: Filled без спрайта не работает) ----------
    private static Sprite _whiteSprite;
    private static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null)
                _whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f),
                    new Vector2(0.5f, 0.5f), 100f);
            return _whiteSprite;
        }
    }

    void Start()
    {
        font = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font;
        EnsureEventSystem();
        BuildPanel();
    }

    void Update()
    {
        // герой спавнится в рантайме — ищем с ретраем (общий паттерн проекта)
        if (hero == null)
        {
            heroSearchTimer -= Time.deltaTime;
            if (heroSearchTimer <= 0f)
            {
                heroSearchTimer = 0.5f;
                hero = FindAnyObjectByType<HeroStats>();
                if (hero != null)
                {
                    heroXP = hero.GetComponent<HeroExperience>();
                    if (heroXP == null) heroXP = hero.GetComponentInChildren<HeroExperience>();
                    if (heroXP == null) heroXP = FindAnyObjectByType<HeroExperience>();
                    equipment = hero.GetComponent<EquipmentSystem>();
                    uniques = hero.GetComponent<AndronUniqueEquipment>();
                    skills = hero.GetComponent<AndronSkills>();
                    heroAI = hero.GetComponent<BattleAI>();
                    heroAgent = hero.GetComponent<UnityEngine.AI.NavMeshAgent>();
                }
            }
            if (hero == null) return;
        }

        RefreshPanel();

        // ЖИВЫЕ цифры атрибутов/характеристик — 5 раз в секунду
        infoRefreshTimer -= Time.deltaTime;
        if (infoRefreshTimer <= 0f)
        {
            infoRefreshTimer = 0.2f;
            RefreshInfoValues();
        }
    }

    // ==================== ПОСТРОЕНИЕ UI ====================

    private void BuildPanel()
    {
        var canvas = GetComponentInParent(typeof(Canvas)) as Canvas;
        if (canvas == null)
            canvas = FindAnyObjectByType(typeof(Canvas)) as Canvas;
        if (canvas == null)
        {
            Debug.LogWarning("[НИЖНЯЯ ПАНЕЛЬ] Canvas не найден — панель не построена");
            return;
        }

        // ОБНОВЛЕНИЕ 24: окно описания (тултип) — одно поверх всего HUD, создаётся кодом
        HeroTooltipUI.Ensure(canvas);

        // ---------- корень по низу экрана ----------
        var rootGO = new GameObject("HeroBottomPanel_Root", typeof(RectTransform));
        var root = (RectTransform)rootGO.transform;
        root.SetParent(canvas.transform, false);
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(0f, 230f);

        // ---------- ОБНОВЛЕНИЕ 20: отдельный бар ХАРАКТЕРИСТИК слева снизу ----------
        BuildCharacteristicsBar(canvas);

        // ---------- единый КОНТЕЙНЕР: скиллы → атрибуты → ун. артефакт → ун. шмот → шмот ----------
        const int totalSlots = 8;
        float skillsWidth = 5 * skillSize + 4 * skillGap;              // блок скиллов (5 кнопок)
        float slotsWidth = totalSlots * slotSize + (totalSlots - 1) * slotGap;
        float attrX = 10f + skillsWidth + sectionGap;
        float slotX0 = attrX + colWidth + sectionGap;
        float containerW = slotX0 + slotsWidth + 10f;
        float containerH = 8f + (20f + 6 * rowHeight) + 8f;            // по блоку АТРИБУТЫ (6 строк + заголовок)

        var contGO = new GameObject("HeroBottomPanel_Container", typeof(RectTransform));
        var contRect = (RectTransform)contGO.transform;
        contRect.SetParent(root, false);
        contRect.anchorMin = new Vector2(0f, 0f);
        contRect.anchorMax = new Vector2(0f, 0f);
        contRect.pivot = new Vector2(0f, 0f);
        contRect.anchoredPosition = containerOffset;
        contRect.sizeDelta = new Vector2(containerW, containerH);

        // лёгкий фон контейнера (клики RTS проходят насквозь)
        var contBg = contGO.AddComponent<Image>();
        contBg.sprite = WhiteSprite;
        contBg.color = panelBackColor;
        contBg.raycastTarget = false;

        // 1) СКИЛЛЫ (1 2 3 4 5, идут слева направо)
        var skillsData = new (string name, string hotkey, AndronSkills.SkillId id)[]
        {
            ("Ярость короля",  "1", AndronSkills.SkillId.Rage),
            ("Взрыв щита",     "2", AndronSkills.SkillId.ShieldBlast),
            ("Щит империи",    "3", AndronSkills.SkillId.EmpireShieldPassive),
            ("Родословная",    "4", AndronSkills.SkillId.BloodlinePassive),
            ("Атрибуты",       "5", AndronSkills.SkillId.Attributes), // плюсик в конце
        };

        skillButtons = new SkillButton[skillsData.Length];
        for (int i = 0; i < skillsData.Length; i++)
        {
            var data = skillsData[i];
            skillButtons[i] = CreateSkillButton(contRect, i, data.name, data.hotkey, data.id);
        }

        // 2) АТРИБУТЫ — живые цифры сразу после скиллов (Сила … Сила воли, 5 раз/сек)
        attrValueTexts = CreateStatColumn(contRect, "АТРИБУТЫ", AttrNames,
            new Vector2(attrX, -8f), colWidth);

        // 3..5) шмот по порядку: уникальный артефакт «М» → уникальный шмот «Щ» → 6 обычных
        slotBottoms = new Image[totalSlots];
        slotIcons = new Image[totalSlots];
        slotTierTexts = new Text[totalSlots];

        for (int i = 0; i < totalSlots; i++)
        {
            bool isUnique = i >= 6; // 6 = уникальный артефакт, 7 = уникальное снаряжение
            // визуальный порядок: уники (6, 7) идут ПЕРВЫМИ, затем обычные слоты 0..5
            int visualOrder = i >= 6 ? i - 6 : i + 2;
            float x = slotX0 + visualOrder * (slotSize + slotGap);
            Image bottom, icon; Text tier;
            CreateSlot(contRect, i, isUnique, x, out bottom, out icon, out tier);
            slotBottoms[i] = bottom;
            slotIcons[i] = icon;
            slotTierTexts[i] = tier;
        }
    }

    /// <summary>Обновление 20: ОТДЕЛЬНЫЙ бар ХАРАКТЕРИСТИК слева снизу (9 живых строк, 5 раз/сек).</summary>
    private void BuildCharacteristicsBar(Canvas canvas)
    {
        float colHeight = 20f + rowHeight * CharNames.Length;
        var barGO = new GameObject("HeroCharPanel_Root", typeof(RectTransform));
        var barRect = (RectTransform)barGO.transform;
        barRect.SetParent(canvas.transform, false);
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 0f);
        barRect.pivot = new Vector2(0f, 0f);
        barRect.anchoredPosition = charPanelOffset;
        barRect.sizeDelta = new Vector2(colWidth + 20f, colHeight + 20f);

        // лёгкий фон панели (клики RTS проходят насквозь)
        var bg = barGO.AddComponent<Image>();
        bg.sprite = WhiteSprite;
        bg.color = panelBackColor;
        bg.raycastTarget = false;

        charValueTexts = CreateStatColumn(barRect, "ХАРАКТЕРИСТИКИ", CharNames,
            new Vector2(10f, -10f), colWidth);
    }

    /// <summary>Колонка живых цифр: заголовок + строки «имя … значение». Возвращает тексты значений.</summary>
    private Text[] CreateStatColumn(RectTransform parent, string header, string[] names,
                                    Vector2 anchorPos, float width)
    {
        var colGO = new GameObject("Col_" + header, typeof(RectTransform));
        var colRect = (RectTransform)colGO.transform;
        colRect.SetParent(parent, false);
        colRect.anchorMin = new Vector2(0f, 1f);
        colRect.anchorMax = new Vector2(0f, 1f);
        colRect.pivot = new Vector2(0f, 1f);
        colRect.anchoredPosition = anchorPos;
        colRect.sizeDelta = new Vector2(width, 20f + rowHeight * names.Length);

        // заголовок
        var head = CreateLabel(colRect, "Header", infoFontSize + 1);
        head.alignment = TextAnchor.MiddleLeft;
        head.color = headerColor;
        head.fontStyle = FontStyle.Bold;
        head.text = header;
        head.rectTransform.anchorMin = new Vector2(0f, 1f);
        head.rectTransform.anchorMax = new Vector2(1f, 1f);
        head.rectTransform.pivot = new Vector2(0f, 1f);
        head.rectTransform.anchoredPosition = new Vector2(4f, 0f);
        head.rectTransform.sizeDelta = new Vector2(0f, 20f);

        // строки «имя … значение»
        var values = new Text[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var rowGO = new GameObject("Row_" + names[i], typeof(RectTransform));
            var rowRect = (RectTransform)rowGO.transform;
            rowRect.SetParent(colRect, false);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -20f - i * rowHeight);
            rowRect.sizeDelta = new Vector2(0f, rowHeight);

            var n = CreateLabel(rowRect, "Name", infoFontSize);
            n.alignment = TextAnchor.LowerLeft;
            n.color = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            n.text = names[i];
            n.rectTransform.anchorMin = new Vector2(0f, 0f);
            n.rectTransform.anchorMax = new Vector2(1f, 1f);
            n.rectTransform.pivot = new Vector2(0f, 0f);
            n.rectTransform.anchoredPosition = new Vector2(8f, 1f);
            n.rectTransform.sizeDelta = new Vector2(width * 0.6f, 0f);

            var v = CreateLabel(rowRect, "Value", infoFontSize);
            v.alignment = TextAnchor.LowerRight;
            v.fontStyle = FontStyle.Bold;
            v.text = "…";
            v.rectTransform.anchorMin = new Vector2(0f, 0f);
            v.rectTransform.anchorMax = new Vector2(1f, 1f);
            v.rectTransform.pivot = new Vector2(1f, 0f);
            v.rectTransform.anchoredPosition = new Vector2(-8f, 1f);
            v.rectTransform.sizeDelta = new Vector2(width * 0.5f, 0f);

            values[i] = v;
        }
        return values;
    }

    private void CreateSlot(RectTransform parent, int index, bool isUnique, float x,
                            out Image bottom, out Image icon, out Text tierText)
    {
        var slotGO = new GameObject("Slot_" + index, typeof(RectTransform));
        var rect = (RectTransform)slotGO.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        // позиция внутри контейнера: уники (6, 7) первыми, затем обычные 0..5
        rect.anchoredPosition = new Vector2(x, 8f);
        rect.sizeDelta = new Vector2(slotSize, slotSize);

        // фон слота (кликается — снять предмет)
        var bg = slotGO.AddComponent<Image>();
        bg.sprite = WhiteSprite;
        bg.color = isUnique ? uniqueSlotBackColor : slotBackColor;
        bg.raycastTarget = true;

        // тонкая полоска-подсветка ВНИЗУ слота цветом тира (как красятся сундуки)
        var bottomGO = new GameObject("TierStrip", typeof(RectTransform));
        bottom = bottomGO.AddComponent<Image>();
        bottom.sprite = WhiteSprite;
        bottom.color = new Color(0f, 0f, 0f, 0f); // пустой слот — прозрачная
        bottom.raycastTarget = false;
        var bRect = (RectTransform)bottomGO.transform;
        bRect.SetParent(rect, false);
        bRect.anchorMin = new Vector2(0f, 0f);
        bRect.anchorMax = new Vector2(1f, 0f);
        bRect.pivot = new Vector2(0.5f, 0f);
        bRect.offsetMin = new Vector2(2f, 2f);
        bRect.offsetMax = new Vector2(-2f, 6f); // высота 4px

        // иконка (белая, если нет спрайта у предмета)
        var iconGO = new GameObject("Icon", typeof(RectTransform));
        icon = iconGO.AddComponent<Image>();
        icon.sprite = WhiteSprite;
        icon.color = new Color(0f, 0f, 0f, 0f); // пустой слот — прозрачный
        icon.raycastTarget = false;
        var iRect = (RectTransform)iconGO.transform;
        iRect.SetParent(rect, false);
        iRect.anchorMin = new Vector2(0f, 0f); iRect.anchorMax = new Vector2(1f, 1f);
        iRect.offsetMin = new Vector2(6f, 8f);
        iRect.offsetMax = new Vector2(-6f, -10f);

        // подпись: тир "Т1".."Т4" слева сверху, метка уника вместо тира
        tierText = CreateLabel(rect, "TierText", 13);
        tierText.alignment = TextAnchor.UpperLeft;
        var tRect = tierText.rectTransform;
        tRect.offsetMin = new Vector2(3f, 3f);
        tRect.offsetMax = new Vector2(-3f, -2f);

        // клик по слоту — снять предмет (уники не снимаются)
        var click = slotGO.AddComponent<SlotClick>();
        click.slotIndex = index;
        click.owner = this;

        // ОБНОВЛЕНИЕ 24: тултип — наведение 0.2 сек показывает описание, уход курсора закрывает
        var hover = slotGO.AddComponent<TooltipHover>();
        int idx = index;
        hover.TitleFn = () => SlotTooltipTitle(idx);
        hover.BodyFn = () => SlotTooltipBody(idx);
    }

    private SkillButton CreateSkillButton(RectTransform root, int index,
                                          string skillName, string hotkey, AndronSkills.SkillId id)
    {
        var btnGO = new GameObject("Skill_" + skillName.Replace(" ", "_"), typeof(RectTransform));
        var rect = (RectTransform)btnGO.transform;
        rect.SetParent(root, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        // кнопки идут слева направо: первая (цифра 1) — у левого края
        rect.anchoredPosition = new Vector2(10f + index * (skillSize + skillGap), 8f);
        rect.sizeDelta = new Vector2(skillSize, skillSize);

        var bg = btnGO.AddComponent<Image>();
        bg.sprite = WhiteSprite;
        bg.color = skillBackColor;
        bg.raycastTarget = true;

        var btn = new SkillButton { bg = bg, root = rect, id = id, hotkey = hotkey };

        // иконка навыка (приглушённо-белая, пока нет своей картинки)
        var iconGO = new GameObject("Icon", typeof(RectTransform));
        var icon = iconGO.AddComponent<Image>();
        icon.sprite = WhiteSprite;
        icon.color = new Color(0.85f, 0.85f, 0.85f, 0.5f);
        icon.raycastTarget = false;
        var iRect = (RectTransform)iconGO.transform;
        iRect.SetParent(rect, false);
        iRect.anchorMin = new Vector2(0.5f, 1f); iRect.anchorMax = new Vector2(0.5f, 1f);
        iRect.pivot = new Vector2(0.5f, 1f);
        iRect.anchoredPosition = new Vector2(0f, -16f);
        iRect.sizeDelta = new Vector2(skillSize - 16f, skillSize - 36f);
        btn.icon = icon;

        // цифра-хоткей по центру кнопки (видно, что это 1/2/3/4/5)
        var hk = CreateLabel(rect, "Hotkey", 18);
        hk.alignment = TextAnchor.MiddleCenter;
        hk.text = hotkey;
        btn.hotkeyText = hk;

        // ЦИФРА АКТИВАЦИИ: уровень навыка "N/5" (или очки у 5-й кнопки)
        var lvl = CreateLabel(rect, "Level", 13);
        lvl.alignment = TextAnchor.LowerLeft;
        lvl.rectTransform.offsetMin = new Vector2(3f, 2f);
        lvl.rectTransform.offsetMax = new Vector2(-3f, -2f);
        lvl.text = "0/5";
        btn.levelText = lvl;

        // ОРАНЖЕВЫЙ "+" для прокачки (кликабельный, не должен пробивать клик в кнопку под ним)
        var plusGO = new GameObject("Plus", typeof(RectTransform));
        var plus = plusGO.AddComponent<Image>();
        plus.sprite = WhiteSprite;
        plus.color = upgradeColor;
        plus.raycastTarget = true;
        var pRect = (RectTransform)plusGO.transform;
        pRect.SetParent(rect, false);
        pRect.anchorMin = new Vector2(1f, 1f); pRect.anchorMax = new Vector2(1f, 1f);
        pRect.pivot = new Vector2(1f, 1f);
        pRect.anchoredPosition = new Vector2(-2f, -2f);
        pRect.sizeDelta = new Vector2(22f, 22f); // ОБНОВЛЕНИЕ 23: было 16 — промахивались
        var plusClick = plusGO.AddComponent<PlusClick>();
        plusClick.skillId = id;
        var plusText = CreateLabel(plusGO.transform, "PlusText", 14);
        plusText.alignment = TextAnchor.MiddleCenter;
        plusText.text = "+";
        plusText.color = Color.black;
        btn.plus = plus;

        // затемнение перезарядки (заливка сверху вниз, исчезает по мере готовности)
        var cdGO = new GameObject("Cooldown", typeof(RectTransform));
        var cd = cdGO.AddComponent<Image>();
        cd.sprite = WhiteSprite; // ОБЯЗАТЕЛЬНО для Image.Type.Filled (урок HeroStatsBarsUI)
        cd.type = Image.Type.Filled;
        cd.fillMethod = Image.FillMethod.Vertical;
        cd.fillOrigin = (int)Image.OriginVertical.Top;
        cd.color = new Color(0f, 0f, 0f, 0.55f);
        cd.raycastTarget = false;
        var cdRect = (RectTransform)cdGO.transform;
        cdRect.SetParent(rect, false);
        cdRect.anchorMin = Vector2.zero; cdRect.anchorMax = Vector2.one;
        cdRect.offsetMin = new Vector2(2f, 2f); cdRect.offsetMax = new Vector2(-2f, -2f);
        btn.cooldown = cd;

        var cdText = CreateLabel(cdGO.transform, "CDText", 12);
        cdText.alignment = TextAnchor.MiddleCenter;
        cdText.color = Color.white;
        btn.cooldownText = cdText;

        // клик по кнопке — использовать актив / вложить очко
        var click = btnGO.AddComponent<SkillButtonClick>();
        click.skillId = id;
        click.owner = this;

        // ОБНОВЛЕНИЕ 24: тултип — наведение 0.2 сек показывает описание, уход курсора закрывает
        var hover = btnGO.AddComponent<TooltipHover>();
        var skillId = id;
        hover.TitleFn = () => SkillTooltipTitle(skillId);
        hover.BodyFn = () => SkillTooltipBody(skillId);

        return btn;
    }

    /// <summary>Надпись по общему паттерну проекта (legacy Text, raycastTarget=false).</summary>
    private Text CreateLabel(Transform parent, string name, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = textColor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    // ==================== ОБНОВЛЕНИЕ UI ====================

    private void RefreshPanel()
    {
        // ---------- слоты 0..5 — обычные ----------
        if (equipment != null && equipment.Slots != null)
        {
            for (int i = 0; i < 6; i++)
            {
                var def = i < equipment.Slots.Length ? equipment.Slots[i] : null;
                RefreshSlot(slotIcons[i], slotBottoms[i], slotTierTexts[i], def);
            }
        }

        // ---------- слоты 6/7 — уники Андрона ----------
        if (uniques != null)
        {
            // 6 = уникальный артефакт «М» (первый в контейнере), 7 = уникальное снаряжение «Щ»
            RefreshUniqueSlot(slotIcons[6], slotBottoms[6], slotTierTexts[6],
                uniques.UniqueArtifactLevel, "М", !uniques.ArtifactUnlocked);
            RefreshUniqueSlot(slotIcons[7], slotBottoms[7], slotTierTexts[7],
                uniques.UniqueEquipLevel, "Щ", !uniques.ArtifactUnlocked);
        }

        // ---------- кнопки навыков ----------
        if (skills == null) return;
        foreach (var btn in skillButtons)
        {
            if (btn == null) continue;
            int lvl = skills.GetSkillLevel(btn.id);

            if (btn.id == AndronSkills.SkillId.Attributes)
            {
                // 5-я кнопка: нераспределённые очки; клик — ВСЕ 6 атрибутов +1
                int points = skills.UnspentPoints;
                btn.levelText.text = points + " " + (points == 1 ? "очко" : points < 5 ? "очка" : "очков");
                btn.levelText.color = points > 0 ? upgradeColor : textColor;
                btn.plus.color = points > 0 ? upgradeColor : new Color(0.35f, 0.35f, 0.35f);
                btn.cooldown.fillAmount = 0f;
                btn.cooldownText.text = "всем +1";
                continue;
            }

            // цифра активации
            btn.levelText.text = lvl + "/5";
            btn.levelText.color = lvl > 0 ? textColor : new Color(0.6f, 0.6f, 0.6f, 0.8f);

            // "+": виден у ЛЮБОГО навыка ниже 5 уровня; оранжевый = можно прокачать, серый = заблокирован уровнем героя
            btn.plus.gameObject.SetActive(lvl < 5);
            btn.plus.color = skills.CanUpgrade(btn.id)
                ? upgradeColor
                : new Color(0.35f, 0.35f, 0.35f, 0.9f);

            // перезарядка активных навыков
            if (btn.id == AndronSkills.SkillId.Rage || btn.id == AndronSkills.SkillId.ShieldBlast)
            {
                float cd = skills.GetCooldownRemaining(btn.id);
                float total = btn.id == AndronSkills.SkillId.Rage
                    ? skills.rageCooldown : skills.shieldBlastCooldown;
                btn.cooldown.fillAmount = lvl > 0 ? Mathf.Clamp01(cd / Mathf.Max(0.01f, total)) : 1f;
                if (lvl <= 0)
                {
                    btn.cooldown.fillAmount = 1f;
                    btn.cooldownText.text = skills.CanUpgrade(btn.id) ? "жми «+»" : "нужен ур. " + (lvl + 1) * 5;
                }
                else if (btn.cooldown.fillAmount > 0.02f)
                    btn.cooldownText.text = Mathf.CeilToInt(cd).ToString();
                else btn.cooldownText.text = "";
            }
            else
            {
                btn.cooldown.fillAmount = 0f;
                btn.cooldownText.text = lvl <= 0
                    ? (skills.CanUpgrade(btn.id) ? "жми «+»" : "нужен ур. " + (lvl + 1) * 5)
                    : "пассив";
            }
        }
    }

    /// <summary>ЖИВЫЕ ЦИФРЫ: атрибуты и характеристики читаются из HeroStats ПОСЛЕ всех дельт (шмот + плюсик + уники).</summary>
    private void RefreshInfoValues()
    {
        if (hero == null || attrValueTexts == null || charValueTexts == null) return;

        SetInfo(attrValueTexts, 0, hero.strength);
        SetInfo(attrValueTexts, 1, hero.vitality);
        SetInfo(attrValueTexts, 2, hero.endurance);
        SetInfo(attrValueTexts, 3, hero.agility);
        SetInfo(attrValueTexts, 4, hero.wisdom);
        SetInfo(attrValueTexts, 5, hero.willpower);

        SetInfo(charValueTexts, 0, hero.maxHP);
        SetInfo(charValueTexts, 1, hero.endurance * 0.5f);
        SetInfo(charValueTexts, 2, hero.strength * 5f);
        SetInfo(charValueTexts, 3, hero.GetCurrentAttackSpeed());
        SetInfo(charValueTexts, 4, heroAI != null ? heroAI.attackRange : 0f);
        SetInfo(charValueTexts, 5, heroAgent != null ? heroAgent.speed : 0f);
        SetInfo(charValueTexts, 6, hero.GetDefense());
        SetInfo(charValueTexts, 7, hero.maxMana);
        SetInfo(charValueTexts, 8, hero.willpower * 0.5f);
    }

    private static void SetInfo(Text[] arr, int i, float v)
    {
        if (arr == null || i < 0 || i >= arr.Length || arr[i] == null) return;
        arr[i].text = FormatNum(v);
    }

    private static string FormatNum(float v)
    {
        return Mathf.Abs(v - Mathf.Round(v)) < 0.05f ? v.ToString("0") : v.ToString("0.#");
    }

    private void RefreshSlot(Image icon, Image bottom, Text tierText, EquipmentDatabase.EquipmentDef def)
    {
        if (icon == null) return;
        if (def == null)
        {
            icon.color = new Color(0f, 0f, 0f, 0f);
            if (bottom != null) bottom.color = new Color(0f, 0f, 0f, 0f);
            if (tierText != null) tierText.text = "";
        }
        else
        {
            icon.sprite = def.icon != null ? def.icon : WhiteSprite; // нет картинки — БЕЛАЯ иконка
            icon.color = def.icon != null ? Color.white : new Color(1f, 1f, 1f, 0.85f);
            if (bottom != null) bottom.color = EquipmentDatabase.GetTierColor(def.tier);
            if (tierText != null) tierText.text = "Т" + (int)def.tier;
        }
    }

    private void RefreshUniqueSlot(Image icon, Image bottom, Text tierText, int level, string mark, bool locked)
    {
        if (icon == null) return;
        if (locked)
        {
            icon.sprite = WhiteSprite;
            icon.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
            if (tierText != null) tierText.text = mark; // "М" — нужна мета 10 уровня
            if (bottom != null) bottom.color = new Color(0.4f, 0.4f, 0.4f, 0.4f);
            return;
        }
        if (level <= 0)
        {
            icon.color = new Color(0f, 0f, 0f, 0f);
            if (bottom != null) bottom.color = specialSlotColor;
            if (tierText != null) tierText.text = mark;
            return;
        }
        // уника качается — белая иконка + цифра уровня + золотая полоска
        icon.sprite = WhiteSprite;
        icon.color = new Color(1f, 1f, 1f, 0.9f);
        if (bottom != null) bottom.color = specialSlotColor;
        if (tierText != null) tierText.text = "у" + level;
    }

    // ==================== ДЕЙСТВИЯ КЛИКОВ ====================

    public void OnSlotClicked(int slotIndex)
    {
        if (equipment == null) return;
        if (slotIndex < 0 || slotIndex >= 6) return; // уники не снимаются

        var removed = equipment.Unequip(slotIndex);
        if (removed != null && hero != null)
        {
            // снятый предмет падает на пол сундучком рядом с героем (красится как сундуки)
            EquipmentPickup.SpawnExact(hero.transform.position + hero.transform.right * 1.5f, removed);
        }
    }

    public void OnSkillButtonClicked(AndronSkills.SkillId id)
    {
        if (skills == null)
        {
            Debug.LogError("[НАВЫК] На герое НЕ найден компонент AndronSkills — кнопка работать не может!");
            return;
        }
        if (PlusClick.lastPlusClickFrame == Time.frameCount) return; // клик пришёл от "+" — игнорируем

        if (id == AndronSkills.SkillId.Attributes)
        {
            if (skills.UnspentPoints <= 0)
                Debug.Log("[НАВЫК] Нет нераспределённых очков — очки дают уровни героя (5-я кнопка, цифра 5).");
            skills.SpendAttributePoint();
            return;
        }

        int lvl = skills.GetSkillLevel(id);

        // ОБНОВЛЕНИЕ 24: ПРИОРИТЕТ ПО ЗАПРОСУ ВЛАДЕЛЬЦА:
        // «если есть очко способностей — качаю уровень навыка, если нету очков — использую навык».
        // Раньше клик по кнопке выученного навыка уходил в каст, и прокачать с кнопки было нельзя.
        if (lvl > 0 && lvl < 5 && skills.UnspentPoints > 0)
        {
            int before = lvl;
            if (skills.UpgradeSkill(id))
            {
                Debug.Log($"[НАВЫК] {id}: прокачан до {skills.GetSkillLevel(id)}/5 кликом по кнопке (было {before}; очков осталось: {skills.UnspentPoints}).");
                return;
            }
            Debug.Log($"[НАВЫК] {id}: прокачать не удалось — точная причина в логе [НАВЫК] выше.");
            return; // даже при отказе НЕ кастуем — игрок явно хотел прокачку (есть очки)
        }

        if (lvl <= 0)
        {
            // ОБНОВЛЕНИЕ 23/24: невыученный навык учится кликом по самой кнопке (раньше учил только крошечный «+»).
            // Если есть очки — приоритет выше уже сработал; сюда попадаем в тестовом режиме без очков (учение бесплатное).
            Debug.Log($"[НАВЫК] {id}: НЕ изучен — вживляю кликом по кнопке (для теста учить можно с ЛЮБОГО уровня; по диздоку — с 5-го).");
            if (skills.UpgradeSkill(id))
                Debug.Log($"[НАВЫК] {id}: ВЫУЧЕН (ур. 1/5). Теперь кнопка кастует навык.");
            else
                Debug.Log($"[НАВЫК] {id}: выучить не удалось — смотри причину выше/ниже в логе [НАВЫК].");
            return;
        }
        if (!skills.CastSkill(id))
        {
            // ОБНОВЛЕНИЕ 28: разбор причины отказа — нет маны или идёт перезарядка/герой мёртв
            float cost = skills.GetManaCost(id);
            float haveMana = hero != null ? hero.currentMana : 0f;
            if (cost > 0f && hero != null && haveMana < cost)
                Debug.Log($"[НАВЫК] {id}: каст отменён — НЕТ МАНЫ (нужно {cost:0}; есть {haveMana:0}). Качайте Мудрость (мана = 15×Мудрость) или ждите реген.");
            else
                Debug.Log($"[НАВЫК] {id}: сейчас нельзя — идёт перезарядка ({skills.GetCooldownRemaining(id):0.0} сек) или герой мёртв.");
        }
    }

    public void OnPlusClicked(AndronSkills.SkillId id)
    {
        PlusClick.lastPlusClickFrame = Time.frameCount;
        if (skills == null)
        {
            Debug.LogError("[НАВЫК] На герое НЕ найден компонент AndronSkills — «+» работать не может!");
            return;
        }
        if (skills.UpgradeSkill(id))
            Debug.Log($"[НАВЫК] {id}: прокачен до {skills.GetSkillLevel(id)}/5 (гейт уровня героя: {(skills.gateSkillsByHeroLevel ? "ВКЛ" : "ВЫКЛ — тестовый режим, можно КАЖДЫЙ уровень")})");
        else
            Debug.Log($"[НАВЫК] {id}: прокачка недоступна (максимум 5; сегодня режим теста — прокачка разрешена каждый уровень героя). Точная причина — в логе выше.");
    }

    // ==================== ОБНОВЛЕНИЕ 24: КОНТЕНТ ТУЛТИПОВ ====================
    // Содержимое сверено с диздоком (герой Андрон) и КОДОМ: цифры собираются на месте → всегда живые и честные.

    public string SkillTooltipTitle(AndronSkills.SkillId id)
    {
        if (skills == null) return "Навык";
        if (id == AndronSkills.SkillId.Attributes) return "АТРИБУТЫ (очков: " + skills.UnspentPoints + ")";
        int lvl = skills.GetSkillLevel(id);
        string kind = (id == AndronSkills.SkillId.Rage || id == AndronSkills.SkillId.ShieldBlast) ? "активный" : "пассивный";
        return SkillName(id) + " — ур. " + lvl + "/5 (" + kind + ")";
    }

    public string SkillTooltipBody(AndronSkills.SkillId id)
    {
        if (skills == null) return "На герое нет компонента AndronSkills.";
        int lvl = skills.GetSkillLevel(id);
        float str = hero != null ? hero.strength : 0f;

        if (id == AndronSkills.SkillId.Attributes)
        {
            int p = skills.UnspentPoints;
            return "Нераспределённых очков: " + p + "\n" +
                   "Клик по кнопке или «+»: ВСЕ 6 атрибутов +1.\n\n" +
                   "Формулы: 1 Сила = 5 атаки; 1 Живучесть = 25 HP; 1 Выносливость = 0.5 HP/сек и 0.1 защиты; " +
                   "1 Ловкость = 0.1 ск. атаки (после капа 4.0 — идёт в шанс крита); 1 Мудрость = 15 магии; " +
                   "1 Сила воли = 0.5 маны/сек и 0.1 ск. атаки.\n" +
                   "Очки дают уровни героя (и мета-прокачка в будущем).";
        }

        string learn = lvl <= 0
            ? "\n\n⚠ НЕ ИЗУЧЕН — клик по кнопке или оранжевому «+» выучит (тестовый режим: учение с любого уровня; по диздоку — с 5-го).\n"
            : "\n\nКлик по кнопке: есть очки — прокачка, нет очков — использование.\n";

        switch (id)
        {
            case AndronSkills.SkillId.Rage:
            {
                if (lvl <= 0)
                    return "Активный, горячая клавиша 1. Перезарядка " + skills.rageCooldown + " c.\n" +
                           "Мана за каст: " + skills.GetManaCost(id).ToString("0") + " (сейчас маны: " + (hero != null ? hero.currentMana.ToString("0") : "?") + ").\n" +
                           "Несколько ударов мечом, каждый — замах анимацией (интервал " + skills.rageStrikeInterval + " c).\n" +
                           "Каждый удар: 30 + сила×{1 / 1.5 / 2 / 2.5 / 3} на уровнях 1–5, волна 90° перед героем.\n" +
                           "Каждая волна ДАЛЬШЕ: +50% × номер удара к дальности атаки (+50%…+250%)." + learn;
                float mult = 0.5f + 0.5f * lvl;
                float dmg = 30f + mult * str;
                float range = heroAI != null ? heroAI.attackRange : 0f;
                return "Активный, горячая клавиша 1. Перезарядка " + skills.rageCooldown + " c.\n" +
                       "Мана за каст: " + skills.GetManaCost(id).ToString("0") + " (сейчас маны: " + (hero != null ? hero.currentMana.ToString("0") : "?") + ").\n" +
                       "Ударов сейчас: " + lvl + " (интервал " + skills.rageStrikeInterval + " c, замах анимацией — удар по кадру события анимации).\n" +
                       "Урон одного удара: 30 + сила×" + mult.ToString("0.##") + " = " + dmg.ToString("0") + " (сила сейчас " + str.ToString("0") + ").\n" +
                       "Каждый удар — волна 90° перед героем, каждая волна дальше:\n" +
                       "волна N = дальность×(1 + 0.5×N): " + (range * 1.5f).ToString("0.0") + " … " + (range * (1f + 0.5f * lvl)).ToString("0.0") + " м\n" +
                       "(дальность атаки " + range.ToString("0.0") + " м; по диздоку: +50%…+250%)." + learn;
            }
            case AndronSkills.SkillId.ShieldBlast:
            {
                float[] sM = { 1f, 1.5f, 1.5f, 2f, 2f };
                float[] eM = { 1f, 1f, 1.5f, 1.5f, 2f };
                float[] rM = { 7f, 9f, 11f, 13f, 16f };
                int i = Mathf.Clamp(lvl, 1, 5) - 1;
                float range = heroAI != null ? heroAI.attackRange : 0f;
                string statsLine = lvl <= 0
                    ? "Урон: 50 + сила×{1/1.5/1.5/2/2} + выносливость×{1/1/1.5/1.5/2} на уровнях 1–5.\n"
                    : "Урон сейчас: 50 + сила×" + sM[i].ToString("0.#") + " + выносливость×" + eM[i].ToString("0.#") +
                      " = " + (50f + sM[i] * str + eM[i] * (hero != null ? hero.endurance : 0f)).ToString("0") + " (сила " + str.ToString("0") + ", вын. " + (hero != null ? hero.endurance : 0f).ToString("0") + ").\n";
                return "Активный, горячая клавиша 2. Перезарядка " + skills.shieldBlastCooldown + " c (1 мин 20 с).\n" +
                       "Мана за каст: " + skills.GetManaCost(id).ToString("0") + " (сейчас маны: " + (hero != null ? hero.currentMana.ToString("0") : "?") + ").\n" +
                       statsLine +
                       "Радиус волны: +{(600,800,1000,1200,1500)}% к дальности атаки → сейчас " +
                       (range * rM[i]).ToString("0.0") + " м (дальность " + range.ToString("0.0") + " м).\n" +
                       "Бьёт ПРОТИВНИКОВ и НЕЙТРАЛОВ вокруг героя; противнику +20% общего урона (диздок)." + learn;
            }
            case AndronSkills.SkillId.EmpireShieldPassive:
            {
                return "Пассивный, кнопка 3 — «Щит империи».\n" +
                       "Отражает полученный урон обратно в противников и нейтралов:\n" +
                       "уровни 1–5 дают 10 / 20 / 30 / 40 / 50% отражения.\n" +
                       "Сейчас отражается: " + skills.TotalReflectPercent.ToString("0") + "%\n" +
                       "(включая +1%/мета-уровень от уникального шмота)." + learn;
            }
            case AndronSkills.SkillId.BloodlinePassive:
            {
                float[] b = { 5f, 10f, 20f, 35f, 50f };
                string now = lvl <= 0 ? "" : " Сейчас: +" + b[Mathf.Clamp(lvl,1,5)-1].ToString("0") + " сила и +" + b[Mathf.Clamp(lvl,1,5)-1].ToString("0") + " выносливость.";
                return "Пассивный, кнопка 4 — «Родословная».\n" +
                       "Повышает СИЛУ и ВЫНОСЛИВОСТЬ: 5 / 10 / 20 / 35 / 50 на уровнях 1–5." + now + learn;
            }
            default:
                return "Описание недоступно.";
        }
    }

    private static string SkillName(AndronSkills.SkillId id)
    {
        switch (id)
        {
            case AndronSkills.SkillId.Rage: return "Ярость короля";
            case AndronSkills.SkillId.ShieldBlast: return "Взрыв щита";
            case AndronSkills.SkillId.EmpireShieldPassive: return "Щит империи";
            case AndronSkills.SkillId.BloodlinePassive: return "Родословная";
            default: return "Атрибуты";
        }
    }

    /// <summary>Обычные слоты 0–5: показать надетый предмет или «пустой слот».</summary>
    public string SlotTooltipTitle(int index)
    {
        if (equipment == null || index < 0 || index >= equipment.Slots.Length) return "Слот";
        var def = equipment.Slots[index];
        return def != null ? def.displayName + "  (тир " + (int)def.tier + ")" : "Слот " + (index + 1) + " — пустой";
    }

    public string SlotTooltipBody(int index)
    {
        if (equipment == null) return "На герое нет EquipmentSystem.";
        if (index >= 6) return UniquesTooltipBody(index == 6);
        var def = index >= 0 && index < equipment.Slots.Length ? equipment.Slots[index] : null;
        if (def == null)
            return "Пустой слот обычного снаряжения.\n" +
                   "Подбери сундук с поля боя — цвет полоски снизу = тир предмета:\n" +
                   "Т1 дерево, Т2 жёлтый, Т3 оранжевый, Т4 красный.\n\n" +
                   "ЛКМ по слоту с предметом — снять (предмет упадёт рядом сундучком).";
        string t = def.BonusesText();
        return def.displayName + " (тир " + (int)def.tier + ")\n\n" + (string.IsNullOrEmpty(t) ? "Нет статов." : t) +
               "\n\nЛКМ по слоту — снять предмет.";
    }

    /// <summary>Уники Андрона: слот 6 = арт. «М» (Благословенный меч), слот 7 = шмот «Щ» (Щит империи).</summary>
    public string UniquesTooltipBody(bool isArtifact)
    {
        if (uniques == null) return "На герое нет AndronUniqueEquipment.";
        if (isArtifact)
        {
            if (!uniques.ArtifactUnlocked)
                return "УНИКАЛЬНЫЙ АРТЕФАКТ — «Благословенный меч»\n\n" +
                       "Слот откроется на 10 мета-уровне (сейчас мето-ур. " + uniques.metaLevel + ").\n\n" +
                       "Что даст: +5 урона по ПРОТИВНИКАМ (не нейтралам) на 1-м уровне " +
                       "и +2 за каждый следующий уровень метовой прокачки;\n" +
                       "+25 HP на 1-м уровне и +10 за каждый следующий.";
            int lvl = uniques.UniqueArtifactLevel;
            return "УНИКАЛЬНЫЙ АРТЕФАКТ — «Благословенный меч», ур. " + lvl + " (мето-ур. " + uniques.metaLevel + ")\n\n" +
                   "Урон по противникам (не нейтралам): +" + uniques.SwordBonusDamage.ToString("0") +
                   " (5 на ур.1, +2 за каждый уровень).\n" +
                   "Здоровье: +" + uniques.SwordBonusHP.ToString("0") +
                   " HP (25 на ур.1, +10 за каждый уровень).\n\n" +
                   "Мета-прокачка пока заглушка: поменяй metaLevel в инспекторе.";
        }
        int ul = uniques.UniqueEquipLevel;
        return "УНИКАЛЬНОЕ СНАРЯЖЕНИЕ — «Щит империи»" + (ul > 0 ? ", ур. " + ul : "") + " (мето-ур. " + uniques.metaLevel + ")\n\n" +
               "+5 к защите" + (ul > 0 ? "" : " (когда уровень открыт)") + ".\n" +
               "Если атакует ПРОТИВНИК — ещё +5 к защите (не на нейтралов, заготовка).\n" +
               "Отражение урона: 10% на ур.1, +1% за каждый уровень → сейчас от уника " +
               uniques.ShieldReflectPercent.ToString("0") + "%\n" +
               "Суммарно с пассивкой: " + (skills != null ? skills.TotalReflectPercent.ToString("0") : "0") + "%.";
    }

    // ==================== EVENTSYSTEM ====================

    /// <summary>Кнопкам нужен EventSystem с модулем под Input System (проект на new Input System).</summary>
    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType(typeof(EventSystem)) != null) return;

        var esGO = new GameObject("EventSystem_HUD");
        esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        esGO.AddComponent<StandaloneInputModule>();
#endif
        Debug.Log("[НИЖНЯЯ ПАНЕЛЬ] Создан EventSystem для кликов по кнопкам навыков/слотам");
    }
}

// ==================== КЛИК-КОМПОНЕНТЫ (должны быть в одном файле с панелью — они крошечные) ====================

public class SlotClick : MonoBehaviour, IPointerClickHandler
{
    public int slotIndex;
    public HeroBottomPanelUI owner;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null && eventData.button == PointerEventData.InputButton.Left)
            owner.OnSlotClicked(slotIndex);
    }
}

public class PlusClick : MonoBehaviour, IPointerClickHandler
{
    /// <summary>Кадр последнего клика по "+" — чтобы клик не прокинулся в кнопку навыка под ним.</summary>
    public static int lastPlusClickFrame = -1;

    public AndronSkills.SkillId skillId;

    public void OnPointerClick(PointerEventData eventData)
    {
        var panel = GetComponentInParent<HeroBottomPanelUI>();
        if (panel != null && eventData.button == PointerEventData.InputButton.Left)
            panel.OnPlusClicked(skillId);
    }
}

public class SkillButtonClick : MonoBehaviour, IPointerClickHandler
{
    public AndronSkills.SkillId skillId;
    public HeroBottomPanelUI owner;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null && eventData.button == PointerEventData.InputButton.Left)
            owner.OnSkillButtonClicked(skillId);
    }
}
