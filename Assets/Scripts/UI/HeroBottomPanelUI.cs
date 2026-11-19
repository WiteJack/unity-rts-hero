using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// НИЖНЯЯ ПАНЕЛЬ ИНТЕРФЕЙСА (задание: "подготовь нижнюю часть интерфейса, именно скриптово").
/// Вешается на ЛЮБОЙ объект (проще всего на Main Camera, как HeroStatsPanel/HeroStatsBarsUI).
///
/// Слева внизу — СЛОТЫ СНАРЯЖЕНИЯ: 6 обычных (по диздоку) + 2 золотых слота уникальных
/// (уникальное снаряжение «Щит империи» и уникальный артефакт «Благословенный меч» Андрона;
/// артефакт помечен цифрой "10" — открывается после 10 мета-уровня игрока).
///
/// Справа внизу — КНОПКИ НАВЫКОВ АНДРОНА (5 штук: Q W E R F):
///   - иконка (нет картинки — БЕЛАЯ иконка, правило ТЗ),
///   - ЦИФРА АКТИВАЦИИ — уровень навыка "N/5" (у 5-й кнопки — нераспределённые очки),
///   - затемнение перезарядки у активных навыков,
///   - ЗЕЛЁНЫЙ "+" когда навык можно прокачать (клик по "+" изучает следующий уровень).
///
/// Клик по обычному слоту — снять предмет (он падает на пол сундучком).
/// Клик по активному навыку — использовать. Клик по 5-й кнопке (F) — вложить 1 очко характеристик.
///
/// НА ГЕРОЕ должны висеть: AndronSkills (навыки), EquipmentSystem (6 слотов), AndronUniqueEquipment
/// (уники, опционально). Все они добавляются пользователем на префаб героя; панель ищет их сама.
/// Кнопки кликабельны (EventSystem создаётся автоматически); полоски HP/маны/опыта HeroStatsBarsUI
/// продолжают пропускать клики RTS.
/// </summary>
public class HeroBottomPanelUI : MonoBehaviour
{
    [Header("Слева внизу — слоты снаряжения")]
    public Vector2 slotsOffset = new Vector2(20f, 20f);
    public float slotSize = 48f;
    public float slotGap = 6f;

    [Header("Справа внизу — кнопки навыков")]
    public Vector2 skillsOffset = new Vector2(-20f, 20f);
    public float skillSize = 52f;
    public float skillGap = 10f;

    [Header("Цвета")]
    public Color panelBackColor = new Color(0.06f, 0.06f, 0.09f, 0.65f);
    public Color slotBackColor = new Color(0.12f, 0.14f, 0.25f, 0.95f);  // синие слоты с мокапа
    public Color uniqueSlotBackColor = new Color(0.18f, 0.14f, 0.05f, 0.95f);
    public Color upgradeColor = new Color(0.2f, 0.85f, 0.25f);            // зелёный "+"
    public Color textColor = Color.white;
    public Color specialSlotColor = new Color(1f, 0.82f, 0.2f);           // золото — уники

    private HeroStats hero;
    private HeroExperience heroXP;
    private AndronSkills skills;
    private EquipmentSystem equipment;
    private AndronUniqueEquipment uniques;
    private float heroSearchTimer;

    private Font font;
    private Image[] slotBottoms;    // тонкая полоска-подсветка внизу слота (цвет тира)
    private Image[] slotIcons;      // иконка предмета (белая, если нет спрайта)
    private Text[]  slotTierTexts;  // "Т1".."Т4" / метки уников

    private SkillButton[] skillButtons;

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
                }
            }
            if (hero == null) return;
        }

        RefreshPanel();
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

        // ---------- корень по низу экрана ----------
        var rootGO = new GameObject("HeroBottomPanel_Root", typeof(RectTransform));
        var root = (RectTransform)rootGO.transform;
        root.SetParent(canvas.transform, false);
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(0f, 200f);

        // ---------- слоты слева: 6 обычных + 2 уникальных ----------
        const int totalSlots = 8;
        slotBottoms = new Image[totalSlots];
        slotIcons = new Image[totalSlots];
        slotTierTexts = new Text[totalSlots];

        float slotsWidth = totalSlots * slotSize + (totalSlots - 1) * slotGap;

        var slotsGO = new GameObject("EquipmentSlots", typeof(RectTransform));
        var slotsRect = (RectTransform)slotsGO.transform;
        slotsRect.SetParent(root, false);
        slotsRect.anchorMin = new Vector2(0f, 0f);
        slotsRect.anchorMax = new Vector2(0f, 0f);
        slotsRect.pivot = new Vector2(0f, 0f);
        slotsRect.anchoredPosition = slotsOffset;
        slotsRect.sizeDelta = new Vector2(slotsWidth, slotSize);

        for (int i = 0; i < totalSlots; i++)
        {
            bool isUnique = i >= 6; // 6 = уникальное снаряжение, 7 = уникальный артефакт
            Image bottom, icon; Text tier;
            CreateSlot(slotsRect, i, isUnique, out bottom, out icon, out tier);
            slotBottoms[i] = bottom;
            slotIcons[i] = icon;
            slotTierTexts[i] = tier;
        }

        // ---------- кнопки навыков справа (панель правит имена/клавиши) ----------
        var skillsData = new (string name, string hotkey, AndronSkills.SkillId id)[]
        {
            ("Ярость короля",  "Q", AndronSkills.SkillId.Rage),
            ("Взрыв щита",     "W", AndronSkills.SkillId.ShieldBlast),
            ("Щит империи",    "E", AndronSkills.SkillId.EmpireShieldPassive),
            ("Родословная",    "R", AndronSkills.SkillId.BloodlinePassive),
            ("Атрибуты",       "F", AndronSkills.SkillId.Attributes),
        };

        int skillCount = skillsData.Length;
        float skillsWidth = skillCount * skillSize + (skillCount - 1) * skillGap;

        skillButtons = new SkillButton[skillCount];
        for (int i = 0; i < skillCount; i++)
        {
            var data = skillsData[i];
            skillButtons[i] = CreateSkillButton(root, skillsWidth, i, data.name, data.hotkey, data.id);
        }
    }

    private void CreateSlot(RectTransform parent, int index, bool isUnique,
                            out Image bottom, out Image icon, out Text tierText)
    {
        var slotGO = new GameObject("Slot_" + index, typeof(RectTransform));
        var rect = (RectTransform)slotGO.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(index * (slotSize + slotGap), 0f);
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
    }

    private SkillButton CreateSkillButton(RectTransform root, float skillsWidth, int index,
                                          string skillName, string hotkey, AndronSkills.SkillId id)
    {
        var btnGO = new GameObject("Skill_" + skillName.Replace(" ", "_"), typeof(RectTransform));
        var rect = (RectTransform)btnGO.transform;
        rect.SetParent(root, false);
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        // кнопки идут справа налево: первая (Q) — у правого края
        rect.anchoredPosition = skillsOffset + new Vector2(-index * (skillSize + skillGap), 0f);
        rect.sizeDelta = new Vector2(skillSize, skillSize);

        var bg = btnGO.AddComponent<Image>();
        bg.sprite = WhiteSprite;
        bg.color = panelBackColor;
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

        // буква-хоткей по центру кнопки (видно, что это Q/W/E/R/F)
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

        // ЗЕЛЁНЫЙ "+" для прокачки (кликабельный, не должен пробивать клик в кнопку под ним)
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
        pRect.sizeDelta = new Vector2(16f, 16f);
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

        return btn;
    }

    /// <summary>Надпись по общему паттерну проекта (legacy Text, raycastTarget=false, растянута на родителя).</summary>
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
            RefreshUniqueSlot(slotIcons[6], slotBottoms[6], slotTierTexts[6],
                uniques.UniqueEquipLevel, "Щ", !uniques.ArtifactUnlocked);
            RefreshUniqueSlot(slotIcons[7], slotBottoms[7], slotTierTexts[7],
                uniques.UniqueArtifactLevel, "М", !uniques.ArtifactUnlocked);
        }

        // ---------- кнопки навыков ----------
        if (skills == null) return;
        foreach (var btn in skillButtons)
        {
            if (btn == null) continue;
            int lvl = skills.GetSkillLevel(btn.id);

            if (btn.id == AndronSkills.SkillId.Attributes)
            {
                // 5-я кнопка: нераспределённые очки + следующий атрибут по кругу
                int points = skills.UnspentPoints;
                btn.levelText.text = points + " " + (points == 1 ? "очко" : points < 5 ? "очка" : "очков");
                btn.levelText.color = points > 0 ? upgradeColor : textColor;
                btn.plus.color = points > 0 ? upgradeColor : new Color(0.35f, 0.35f, 0.35f);
                btn.cooldown.fillAmount = 0f;
                btn.cooldownText.text = skills.NextAttribute;
                continue;
            }

            // цифра активации
            btn.levelText.text = lvl + "/5";
            btn.levelText.color = lvl > 0 ? textColor : new Color(0.6f, 0.6f, 0.6f, 0.8f);

            // "+": показываем только когда прокачка реально доступна
            btn.plus.gameObject.SetActive(skills.CanUpgrade(btn.id));

            // перезарядка активных навыков
            if (btn.id == AndronSkills.SkillId.Rage || btn.id == AndronSkills.SkillId.ShieldBlast)
            {
                float cd = skills.GetCooldownRemaining(btn.id);
                float total = btn.id == AndronSkills.SkillId.Rage
                    ? skills.rageCooldown : skills.shieldBlastCooldown;
                btn.cooldown.fillAmount = Mathf.Clamp01(cd / Mathf.Max(0.01f, total));
                if (lvl <= 0) { btn.cooldown.fillAmount = 1f; btn.cooldownText.text = "не изучен"; }
                else if (btn.cooldown.fillAmount > 0.02f)
                    btn.cooldownText.text = Mathf.CeilToInt(cd).ToString();
                else btn.cooldownText.text = "";
            }
            else
            {
                btn.cooldown.fillAmount = 0f;
                btn.cooldownText.text = lvl <= 0 ? "—" : "пассив";
            }
        }
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
        if (skills == null) return;
        if (PlusClick.lastPlusClickFrame == Time.frameCount) return; // клик пришёл от "+" — игнорируем
        if (id == AndronSkills.SkillId.Attributes) skills.SpendAttributePoint();
        else skills.CastSkill(id);
    }

    public void OnPlusClicked(AndronSkills.SkillId id)
    {
        PlusClick.lastPlusClickFrame = Time.frameCount;
        if (skills == null) return;
        if (!skills.UpgradeSkill(id))
            Debug.Log($"[НАВЫК] {id}: прокачка недоступна (максимум 5; новый уровень скиллов открывается каждые 5 уровней героя)");
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
