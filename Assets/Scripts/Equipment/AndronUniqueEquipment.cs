using UnityEngine;

/// <summary>
/// УНИКАЛЬНОЕ снаряжение Андрона и его УНИКАЛЬНЫЙ АРТЕФАКТ (диздок "Снаряжение1" + "Герои и навыки1").
/// Вешается на КОРЕНЬ героя (рядом с HeroStats / HeroExperience / EquipmentSystem), HeroStats НЕ трогаем.
///
/// Слоты фиксированы за героем:
/// - Слот УНИКАЛЬНОГО СНАРЯЖЕНИЯ — «Щит империи».
/// - Слот УНИКАЛЬНОГО АРТЕФАКТА — «Благословенный меч», открывается после 10 мета-уровня игрока.
///
/// МЕТА-ПРОКАЧКА (держим в уме, поле metaLevel ниже):
/// - Уникальное снаряжение прокачивается на 5, 10, 15, ... 100 мета-уровнях (20 уровней).
/// - Уникальный артефакт прокачивается на 10, 20, 30, ... 100 мета-уровнях (10 уровней).
///
/// ЭФФЕКТЫ (без правки HeroStats — дельтами через EquipmentSystem):
/// - Благословенный меч: урон +5 (1-й уровень меты) и +2 за каждый следующий уровень,
///   здоровье +25 (1-й уровень) и +10 за каждый следующий.
///   Реализация: сила *= ... точнее добавляем силу: 5 урона = 1 сила (1 сила = 5 урона) — итого
///   +1 сила на 1-м уровне, +0.4 силы за каждый следующий; живучесть +1 (25 HP) и +0.4 за следующий.
/// - Щит империи: +5 к защите (заготовка — требует доступа к GetDefense, пока только данные),
///   ОТРАЖЕНИЕ урона 10% + 1% за каждый уровень уникального снаряжения — реально работает
///   через AndronSkills (суммарный % отражения = пассивка скилла + уникальное снаряжение).
/// </summary>
public class AndronUniqueEquipment : MonoBehaviour
{
    [Header("Мета-прокачка (в будущем приходит из меты, пока задаётся вручную для тестов)")]
    [Tooltip("Мета-уровень игрока. Снаряжение качается на 5..100 (кратно 5), артефакт на 10..100 (кратно 10)")]
    public int metaLevel = 1;

    [Tooltip("Открыт ли слот уникального артефакта (по диздоку — после 10 мета-уровня). Если false — считается сам: metaLevel >= 10")]
    public bool artifactUnlockedOverride = false;

    [Header("Уникальное снаряжение — «Щит империи»")]
    [Tooltip("Защита на 1-м уровне (данные; реального применения нет — HeroStats.GetDefense не доступен без правки)")]
    public float shieldDefensePerLevel = 5f;

    [Header("Уникальный артефакт — «Благословенный меч»")]
    [Tooltip("Бонус урона на 1-м уровне меты")]
    public float swordDamageFirstLevel = 5f;
    [Tooltip("Бонус урона за каждый следующий уровень меты")]
    public float swordDamagePerLevel = 2f;
    [Tooltip("Бонус HP на 1-м уровне меты")]
    public float swordHPFirstLevel = 25f;
    [Tooltip("Бонус HP за каждый следующий уровень меты")]
    public float swordHPPerLevel = 10f;

    // ---- СВОЙСТВА (их читают UI и AndronSkills) (их читают UI и AndronSkills)----

    /// <summary>Уровень уникального снаряжения 0..20 (0 — пока мета-уровень < 5).</summary>
    public int UniqueEquipLevel
    {
        get
        {
            int lvl = 0;
            for (int m = 5; m <= 100; m += 5) if (metaLevel >= m) lvl++;
            return lvl;
        }
    }

    /// <summary>Уровень уникального артефакта 0..10.</summary>
    public int UniqueArtifactLevel
    {
        get
        {
            if (!ArtifactUnlocked) return 0;
            int lvl = 0;
            for (int m = 10; m <= 100; m += 10) if (metaLevel >= m) lvl++;
            return lvl;
        }
    }

    /// <summary>Открыт ли артефакт (диздок: после 10 мета-уровня).</summary>
    public bool ArtifactUnlocked
    {
        get { return artifactUnlockedOverride || metaLevel >= 10; }
    }

    /// <summary>Бонус урона от Благословенного меча (в единицах урона; в атрибуты переводится как /5).</summary>
    public float SwordBonusDamage
    {
        get
        {
            int lvl = UniqueArtifactLevel;
            return lvl <= 0 ? 0f : swordDamageFirstLevel + swordDamagePerLevel * (lvl - 1);
        }
    }

    /// <summary>Бонус HP от Благословенного меча.</summary>
    public float SwordBonusHP
    {
        get
        {
            int lvl = UniqueArtifactLevel;
            return lvl <= 0 ? 0f : swordHPFirstLevel + swordHPPerLevel * (lvl - 1);
        }
    }

    /// <summary>Бонус защиты от Щита империи (данные для UI; реальное применение — заготовка).</summary>
    public float ShieldBonusDefense
    {
        get { return UniqueEquipLevel <= 0 ? 0f : shieldDefensePerLevel; }
    }

    /// <summary>Отражение урона от уникального снаряжения: 10% + 1% за каждый уровень (0..30%).</summary>
    public float ShieldReflectPercent
    {
        get
        {
            int lvl = UniqueEquipLevel;
            return lvl <= 0 ? 0f : 10f + 1f * (lvl - 1);
        }
    }

    // ---------------- Применение бонусов к герою ----------------

    private EquipmentSystem equipSystem;
    private bool applied;

    void Awake()
    {
        equipSystem = GetComponent<EquipmentSystem>();
        if (equipSystem == null) equipSystem = GetComponentInChildren<EquipmentSystem>();
    }

    void Start()
    {
        Recalculate();
    }

    /// <summary>Пересчитать бонусы уников по текущему мета-уровню (можно дергать извне, когда появится мета).</summary>
    public void Recalculate()
    {
        if (equipSystem == null) return;

        // Снимаем прошлую уникальную дельту и накладываем новую
        // (сначала "генеральная репетиция": RemoveUnique с прошлым значением, потом Apply с новым)
        if (applied)
            equipSystem.RemoveUniqueBonuses(lastStr, lastVit, lastEnd, lastAgi);

        // Благословенный меч: урон → сила (1 сила = 5 урона), HP → живучесть (1 живучесть = 25 HP)
        float str = SwordBonusDamage / 5f;
        float vit = SwordBonusHP / 25f;
        float end = 0f; // защита щита пока не переводится в выносливость (иначе слишком жирно)
        float agi = 0f;

        equipSystem.ApplyUniqueBonuses(str, vit, end, agi);

        lastStr = str; lastVit = vit; lastEnd = end; lastAgi = agi;
        applied = true;

        Debug.Log($"[УНИКАЛ АНДРОНА] мета-уровень {metaLevel}: снаряжение ур.{UniqueEquipLevel} " +
                  $"(отражение {ShieldReflectPercent:0}%, +{shieldDefensePerLevel:0} защиты — заготовка), " +
                  $"артефакт ур.{UniqueArtifactLevel} (меч: +{SwordBonusDamage:0} урона, +{SwordBonusHP:0} HP)");
    }

    private float lastStr, lastVit, lastEnd, lastAgi;

    [ContextMenu("Пересчитать от metaLevel")]
    private void RecalculateMenu() { Recalculate(); }
}
