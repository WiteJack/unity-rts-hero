using UnityEngine;

/// <summary>
/// ОБНОВЛЕНИЕ 22 — случайный рост атрибутов за уровни. Вешается на КОРЕНЬ героя (рядом с HeroExperience),
/// НЕ зависит от конкретного героя — подходят Андрон и любые будущие герои. HeroStats НЕ трогаем.
///
/// Как работает:
/// - Следит за уровнем героя (HeroExperience.level). При КАЖДОМ повышении уровня бросает
///   randomRollsPerLevel (по умолчанию 3) раза кубик: каждый бросок — случайный атрибут из 6, к нему +1.
///   Да, один и тот же атрибут может выпасть несколько раз подряд (так задумано — честный рандом).
/// - Все накопленные бонусы летят ОДНОЙ дельтой в EquipmentSystem.SetExtraBonuses — это ОТДЕЛЬНЫЙ канал
///   (не пересекается со снаряжением, SetSkillBonuses и униками, не затирается их пересчётами).
/// - Если EquipmentSystem на герое нет — запасной путь: прямое изменение публичных полей HeroStats
///   + CalculateStats() (как запасной путь AndronSkills).
///
/// ЗАГЛУШКА на будущее (метовая прокачка): методы ApplyMetaLevels / поля metaLevel —
/// когда появится метовая прокачка персонажа, внешний прогресс-систем просто вызовет ApplyMetaLevels(N),
/// и каждый мет-уровень тоже бросит 3 случайных атрибута (свой отдельный запас, не теряется при пересдаче).
///
/// Лог: [АТРИБУТЫ УРОВНЯ] Уровень N: +Ловкость +Мудрость +Сила (итого: сила X, живучесть Y, ...)
/// </summary>
public class HeroLevelAttributeRoller : MonoBehaviour
{
    [Header("Сколько случайных бросков атрибутов за КАЖДЫЙ уровень героя (по умолчанию 3)")]
    public int randomRollsPerLevel = 3;

    [Header("Писать в консоль каждый ролл")]
    public bool logRolls = true;

    [Header("Метовая прокачка (ЗАГЛУШКА на будущее)")]
    [Tooltip("Накопленные мет-уровни персонажа (заполняет внешний прогресс-систем; 1 мет-уровень = ещё 3 случайных атрибута)")]
    public int metaLevels = 0;

    // ---- ссылки ----
    private HeroExperience xp;
    private HeroStats stats;
    private EquipmentSystem equip;

    // ---- накопленные бонусы ----
    /// <summary>Накопленные случайные бонусы: [сила, живучесть, выносливость, ловкость, мудрость, сила воли].</summary>
    private float[] rolled = new float[6];
    private static readonly string[] AttrNames = { "Сила", "Живучесть", "Выносливость", "Ловкость", "Мудрость", "Сила воли" };

    // запасной путь (без EquipmentSystem) — прошлые прямые дельты, чтобы снимать перед новой записью
    private float fbStr, fbVit, fbEnd, fbAgi, fbWis, fbWill;

    private int lastLevel;
    private int lastMetaLevels;

    void Awake()
    {
        xp = GetComponent<HeroExperience>();
        if (xp == null) xp = GetComponentInChildren<HeroExperience>();
        stats = GetComponent<HeroStats>();
        if (stats == null) stats = GetComponentInChildren<HeroStats>();
        equip = GetComponent<EquipmentSystem>();
        lastLevel = xp != null ? xp.level : 1;
        lastMetaLevels = metaLevels;
    }

    void Update()
    {
        if (xp == null) return;

        // ---- обычные уровни героя ----
        if (xp.level != lastLevel)
        {
            int gained = xp.level - lastLevel;
            lastLevel = xp.level;
            RollAttributes(gained * Mathf.Max(1, randomRollsPerLevel), "ур. " + xp.level);
        }

        // ---- метовые уровни (заглушка: внешний прогресс-систем поднимет metaLevels) ----
        if (metaLevels != lastMetaLevels)
        {
            int gained = metaLevels - lastMetaLevels;
            lastMetaLevels = metaLevels;
            if (gained > 0) RollAttributes(gained * Mathf.Max(1, randomRollsPerLevel), "мет-уровень " + metaLevels);
        }
    }

    /// <summary>Бросить count кубиков: каждый — случайный атрибут +1. Сразу применяет дельту.</summary>
    public void RollAttributes(int count, string reason)
    {
        if (count <= 0) return;
        if (stats == null) { Debug.LogWarning("[АТРИБУТЫ УРОВНЯ] нет HeroStats на герое — ролл пропущен"); return; }

        var rolledNow = new System.Collections.Generic.List<string>();
        for (int j = 0; j < count; j++)
        {
            int i = Random.Range(0, 6); // 0..5 — равный шанс на каждый из 6 атрибутов
            rolled[i] += 1f;
            rolledNow.Add("+" + AttrNames[i]);
        }

        // ---- применяем дельту ----
        if (equip != null)
        {
            // основной путь: отдельный канал EquipmentSystem — переживает пересчёты снаряжения/уников
            equip.SetExtraBonuses(rolled[0], rolled[1], rolled[2], rolled[3], rolled[4], rolled[5]);
        }
        else
        {
            // запасной путь без EquipmentSystem: снимаем прошлые прямые дельты, накладываем новые
            stats.strength  -= fbStr;  stats.vitality   -= fbVit;
            stats.endurance -= fbEnd;  stats.agility    -= fbAgi;
            stats.wisdom    -= fbWis;  stats.willpower  -= fbWill;

            stats.strength  += rolled[0]; stats.vitality  += rolled[1];
            stats.endurance += rolled[2]; stats.agility   += rolled[3];
            stats.wisdom    += rolled[4]; stats.willpower += rolled[5];

            fbStr = rolled[0]; fbVit = rolled[1]; fbEnd = rolled[2];
            fbAgi = rolled[3]; fbWis = rolled[4]; fbWill = rolled[5];
            stats.CalculateStats();
            // защита: скок максимума HP — не считать его уроном для отражения (Щит империи)
            var sk = GetComponent<AndronSkills>();
            if (sk != null) sk.NotifyMaxHPChanged();
        }

        if (logRolls)
            Debug.Log($"[АТРИБУТЫ УРОВНЯ] {reason}: {string.Join(" ", rolledNow.ToArray())} — " +
                      $"итого накоплено: сила {rolled[0]:0}, живучесть {rolled[1]:0}, выносливость {rolled[2]:0}, " +
                      $"ловкость {rolled[3]:0}, мудрость {rolled[4]:0}, сила воли {rolled[5]:0}");
    }

    // ---------------- ЗАГЛУШКА метовой прокачки ----------------

    /// <summary>Внешний прогресс-систем в будущем вызывает при мет-прокачке персонажа: N мет-уровней = 3 случайных атрибута за каждый.</summary>
    public void ApplyMetaLevels(int levels)
    {
        metaLevels += levels; // Update() заметит изменение и бросит кубики сам
    }

    [ContextMenu("Метовая прокачка: тестовый +1 мет-уровень")]
    private void TestMetaLevel() { ApplyMetaLevels(1); }

    [ContextMenu("Сбросить накопленные случайные атрибуты (для тестов)")]
    private void ResetRolled()
    {
        for (int i = 0; i < rolled.Length; i++) rolled[i] = 0f;
        fbStr = fbVit = fbEnd = fbAgi = fbWis = fbWill = 0f;
        if (equip != null) equip.SetExtraBonuses(0f, 0f, 0f, 0f, 0f, 0f);
        else stats.CalculateStats();
        if (logRolls) Debug.Log("[АТРИБУТЫ УРОВНЯ] накопленные случайные бонусы сброшены");
    }
}
