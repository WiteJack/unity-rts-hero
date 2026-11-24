using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Система снаряжения героя. Вешается на КОРЕНЬ героя (рядом с HeroStats), HeroStats НЕ трогаем.
///
/// - 6 слотов обычного снаряжения (по диздоку "Снаряжение1").
/// - Слоты уникального снаряжения/артефакта Андрона не здесь — их ведёт AndronUniqueEquipment,
///   но итоговые бонусы уникальных предметов читаются через AndronUniqueEquipment.GetTotalBonuses().
///
/// КАК ПРИМЕНЯЮТСЯ БОНУСЫ (чтобы работало без правки HeroStats):
/// атрибуты предмета прибавляются дельтой к публичным полям HeroStats (strength, vitality...),
/// после чего вызывается heroStats.CalculateStats() (пересчёт maxHP/maxMana из живучести/мудрости),
/// затем добавляются плоские bonusHP/bonusMana. При снятии — всё откатывается той же дельтой.
/// bonusMoveSpeed применяется дельтой к NavMeshAgent.speed.
/// </summary>
public class EquipmentSystem : MonoBehaviour
{
    [Header("Слоты")]
    [Tooltip("Сколько слотов обычного снаряжения (по диздоку — 6)")]
    public int slotCount = 6;

    [Tooltip("Писать в консоль надевание/снятие предметов")]
    public bool logChanges = true;

    private HeroStats heroStats;
    private NavMeshAgent heroAgent;
    private float baseAgentSpeed = -1f;

    // Слоты: null = пусто
    private EquipmentDatabase.EquipmentDef[] slots;

    // Накопленная дельта бонусов от НАДЕТЫХ предметов (обычные слоты + уники Андрона)
    private float dStrength, dVitality, dEndurance, dAgility, dWisdom, dWillpower;
    private float dBonusHP, dBonusMana, dMoveSpeed;

    /// <summary>Надетые предметы для UI (6 обычных слотов).</summary>
    public EquipmentDatabase.EquipmentDef[] Slots { get { return slots; } }

    void Awake()
    {
        heroStats = GetComponent<HeroStats>();
        if (heroStats == null) heroStats = GetComponentInChildren<HeroStats>();
        heroAgent = GetComponent<NavMeshAgent>();
        if (heroAgent != null) baseAgentSpeed = heroAgent.speed;

        slots = new EquipmentDatabase.EquipmentDef[slotCount];

        // ВАЖНО: захватываем базовые атрибуты именно в Awake — другие скрипты
        // (AndronSkills, AndronUniqueEquipment) накладывают свои дельты в Start,
        // и итог героя должен быть = база + снаряжение + навыки + уники.
        CaptureBase();
    }

    private void CaptureBase()
    {
        if (heroStats == null) return;
        captured = new AttrSnapshot
        {
            strength = heroStats.strength,
            vitality = heroStats.vitality,
            endurance = heroStats.endurance,
            agility = heroStats.agility,
            wisdom = heroStats.wisdom,
            willpower = heroStats.willpower,
            speed = heroAgent != null ? heroAgent.speed : 0f
        };
        baseCaptured = true;
    }

    // ==================== НАДЕВАНИЕ / СНЯТИЕ ====================

    /// <summary>
    /// Надеть предмет в первый свободный слот.
    /// false — все слоты заняты (по диздоку предмет остаётся на полу рядом с игроком).
    /// При успехе вызывает autoReplaceOld == false; предмет, снятый с этого слота, возвращается через oldItem (может быть null).
    /// </summary>
    public bool TryEquip(EquipmentDatabase.EquipmentDef def, out EquipmentDatabase.EquipmentDef replaced)
    {
        replaced = null;
        if (def == null || heroStats == null) return false;

        int freeIndex = -1;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) { freeIndex = i; break; }
        }

        // Все заняты — заменим предмет самого СЛАБОГО тира если новый сильнее (по тиру)
        if (freeIndex < 0)
        {
            int weakest = 0;
            for (int i = 1; i < slots.Length; i++)
                if (slots[i] != null && (int)slots[i].tier < (int)slots[weakest].tier) weakest = i;

            if ((int)def.tier <= (int)slots[weakest].tier) return false; // хуже надетого — остаётся на полу

            replaced = slots[weakest];
            RemoveFromStats(replaced);
            freeIndex = weakest;
            if (logChanges) Debug.Log($"[СНАРЯЖЕНИЕ] Снят {replaced.displayName} (слот {weakest + 1}), надеваем сильнее");
        }

        slots[freeIndex] = def;
        AddToStats(def);
        if (logChanges) Debug.Log($"[СНАРЯЖЕНИЕ] Надет {def.displayName} (тир {(int)def.tier}, слот {freeIndex + 1}): {def.BonusesText()}");
        return true;
    }

    /// <summary>Снять предмет из слота (для клика по слоту в UI). Возвращает снятый предмет или null.</summary>
    public EquipmentDatabase.EquipmentDef Unequip(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null) return null;
        var def = slots[slotIndex];
        slots[slotIndex] = null;
        RemoveFromStats(def);
        if (logChanges) Debug.Log($"[СНАРЯЖЕНИЕ] Снят {def.displayName} (слот {slotIndex + 1})");
        return def;
    }

    // ==================== БОНУСЫ УНИКАЛЬНЫХ ПРЕДМЕТОВ АНДРОНА ====================

    /// <summary>Применить бонусы уникального снаряжения/артефакта (вызывает AndronUniqueEquipment при пересчёте от мета-уровня).</summary>
    public void ApplyUniqueBonuses(float strength, float vitality, float endurance, float agility)
    {
        dStrength += strength; dVitality += vitality; dEndurance += endurance; dAgility += agility;
        AddDelta();
    }

    /// <summary>Снять бонусы уников перед их пересчётом (вызывает AndronUniqueEquipment.Recalculate).</summary>
    public void RemoveUniqueBonuses(float strength, float vitality, float endurance, float agility)
    {
        dStrength -= strength; dVitality -= vitality; dEndurance -= endurance; dAgility -= agility;
        AddDelta();
    }

    // ==================== ВНУТРЕННЕЕ ====================

    private void AddToStats(EquipmentDatabase.EquipmentDef def)
    {
        dStrength += def.strength; dVitality += def.vitality; dEndurance += def.endurance;
        dAgility += def.agility; dWisdom += def.wisdom; dWillpower += def.willpower;
        dBonusHP += def.bonusHP; dBonusMana += def.bonusMana; dMoveSpeed += def.moveSpeedBonus;
        AddDelta();
    }

    private void RemoveFromStats(EquipmentDatabase.EquipmentDef def)
    {
        dStrength -= def.strength; dVitality -= def.vitality; dEndurance -= def.endurance;
        dAgility -= def.agility; dWisdom -= def.wisdom; dWillpower -= def.willpower;
        dBonusHP -= def.bonusHP; dBonusMana -= def.bonusMana; dMoveSpeed -= def.moveSpeedBonus;
        AddDelta();
    }

    /// <summary>Полный пересчёт: дельты обнуляются и заново накладываются из слотов (тесты/контекстное меню).</summary>
    [ContextMenu("Пересчитать бонусы")]
    public void Recalculate()
    {
        RemoveDelta();
        foreach (var def in slots)
            if (def != null) AddToStats(def);
        AddDelta();
    }

    private void RemoveDelta()
    {
        dStrength = dVitality = dEndurance = dAgility = dWisdom = dWillpower = 0f;
        dBonusHP = dBonusMana = dMoveSpeed = 0f;
    }

    /// <summary>Применить накопленную дельту к HeroStats и агенту (HeroStats не правим — используем публичные поля).</summary>
    private void AddDelta()
    {
        if (heroStats == null) return;

        float oldMaxHP = heroStats.maxHP;

        // атрибуты: чистим след прошлых апплайтов через базу = текущее - дельта. Проще:
        // держим исходные значения, захваченные при Awake, и выставляем базу + дельта каждый раз.
        heroStats.strength = captured.strength + dStrength + skillsStrength + extraStrength;
        heroStats.vitality = captured.vitality + dVitality + skillsVitality + extraVitality;
        heroStats.endurance = captured.endurance + dEndurance + skillsEndurance + extraEndurance;
        heroStats.agility = captured.agility + dAgility + skillsAgility + extraAgility;
        heroStats.wisdom = captured.wisdom + dWisdom + skillsWisdom + extraWisdom;
        heroStats.willpower = captured.willpower + dWillpower + skillsWillpower + extraWillpower;

        heroStats.CalculateStats();
        heroStats.maxHP += dBonusHP;
        heroStats.maxMana += dBonusMana;
        heroStats.currentHP = Mathf.Min(heroStats.currentHP, heroStats.maxHP);
        heroStats.currentMana = Mathf.Min(heroStats.currentMana, heroStats.maxMana);

        if (heroAgent != null)
        {
            if (baseAgentSpeed < 0f) baseAgentSpeed = heroAgent.speed;
            heroAgent.speed = baseAgentSpeed + dMoveSpeed + skillsMoveSpeed;
        }

        // макс HP изменился — сводим с ума отражение урона не даём (AndronSkills считает
        // резкое падение HP уроном): помечаем, что изменение легитимное
        if (!Mathf.Approximately(oldMaxHP, heroStats.maxHP))
        {
            var sk = GetComponent<AndronSkills>();
            if (sk != null) sk.NotifyMaxHPChanged();
        }

    }

    // ---------------- База атрибутов ----------------

    private struct AttrSnapshot { public float strength, vitality, endurance, agility, wisdom, willpower; public float speed; }
    private AttrSnapshot captured;

    // Дельты от прокачанных навыков (Родословная) и уников — их вносят AndronSkills/AndronUniqueEquipment,
    // но складываются сюда, чтобы итог героя всегда = база + снаряжение + навыки (никто не затирает чужие бонусы).
    private float skillsStrength, skillsVitality, skillsEndurance, skillsAgility, skillsWisdom, skillsWillpower, skillsMoveSpeed;

    // Дельта случайных атрибутов за уровни (HeroLevelAttributeRoller) — отдельный канал,
    // чтобы не пересекаться с бонусами навыков (SetSkillBonuses) и униками.
    private float extraStrength, extraVitality, extraEndurance, extraAgility, extraWisdom, extraWillpower;

    void Start()
    {
        if (heroStats == null)
        {
            heroStats = FindAnyObjectByType<HeroStats>();
            if (heroStats == null)
            {
                Debug.LogWarning("[СНАРЯЖЕНИЕ] EquipmentSystem не нашёл HeroStats — бонусы применяться не будут");
                enabled = false;
                return;
            }
            if (!baseCaptured) CaptureBase();
        }
    }

    private bool baseCaptured;

    // ---------------- API для навыков (Родословная и т.п.) ----------------

    /// <summary>Установить бонус навыков "Родословная" (сила/выносливость). Вызывает AndronSkills при прокачке.</summary>
    public void SetSkillBonuses(float strength, float vitality, float endurance, float agility, float wisdom, float willpower)
    {
        skillsStrength = strength; skillsVitality = vitality; skillsEndurance = endurance;
        skillsAgility = agility; skillsWisdom = wisdom; skillsWillpower = willpower;
        AddDelta();
    }

    /// <summary>Бонус скорости передвижения (NavMeshAgent.speed) — применяют уники/навыки.</summary>
    public void SetSkillMoveSpeed(float moveSpeed)
    {
        skillsMoveSpeed = moveSpeed;
        AddDelta();
    }

    /// <summary>Дельта случайных атрибутов за уровни (вызывает HeroLevelAttributeRoller).
    /// Отдельный канал: суммируется со снаряжением, навыками и униками и НЕ затирается их пересчётами.</summary>
    public void SetExtraBonuses(float strength, float vitality, float endurance, float agility, float wisdom, float willpower)
    {
        extraStrength = strength; extraVitality = vitality; extraEndurance = endurance;
        extraAgility = agility; extraWisdom = wisdom; extraWillpower = willpower;
        AddDelta();
    }

    void OnValidate()
    {
        if (slots != null && slots.Length != slotCount)
        {
            var old = slots;
            slots = new EquipmentDatabase.EquipmentDef[slotCount];
            for (int i = 0; i < Mathf.Min(old.Length, slotCount); i++) slots[i] = old[i];
            AddDelta();
        }
    }
}
