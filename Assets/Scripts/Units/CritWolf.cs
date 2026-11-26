using System.Collections;
using UnityEngine;

/// <summary>
/// ОБНОВЛЕНИЕ 32: крит волка — дельта-компонент на КОРЕНЬ нейтрала (рядом с BattleAI, как AndronSkills у героя).
/// HeroStats.cs и UnitStats.cs НЕ трогаются: крит работает перехватом удара анимации через IAttackHitInterceptor
/// (механизм Обновления 28, BattleAI.OnAttackHitEvent сначала опрашивает интерцепторы).
///
/// Диздок владельца:
///  - Волк имеет базовый шанс 5% на крит «+100% урона» (то есть ×2).
///  - Каждый тир повышает шанс на +5%: тир 1 = 5%, тир 2 = 10%, тир 3 = 15%, тир 4 = 20%, тир 5 = 25%.
///    Тир читается из NeutralFactionTag (урон baseDamage при этом УЖЕ умножен на тир — крит бьёт ×2 от текущего).
///  - При крите на 0.2 секунды включается НЕВИДИМЫЙ ДОЧЕРНИЙ ОБЪЕКТ с красным светом, который владелец
///    вкладывает волку в пасть. Имя объекта: critwolf (лежит выключенным, скрипт сам включает/выключает).
///
/// Как это работает по удару: удар анимации формирует BattleAI.OnAttackHitEvent → интерцепторы.
///   Не крит   → return false → BattleAI.ApplyDamageToTarget() наносит обычный удар как всегда.
///   Крит      → return true  → обычный удар ПОГЛОЩЁН, скрипт сам наносит ×2 baseDamage той же цели
///               (с проверкой живости цели и лимита дальности, как в ApplyDamageToTarget) → DamagePopup(isCrit).
/// </summary>
public class CritWolf : MonoBehaviour, IAttackHitInterceptor
{
    [Header("Шанс крита по тиру (ОБНОВЛЕНИЕ 32)")]
    [Tooltip("Базовый шанс крита при тире 1 (0.05 = 5%).")]
    [Range(0f, 1f)] public float baseCritChance = 0.05f;
    [Tooltip("Прибавка к шансу за каждый тир выше 1-го: тир 2 = 10%, тир 3 = 15%, тир 4 = 20%, тир 5 = 25%.")]
    [Range(0f, 1f)] public float critChancePerTier = 0.05f;
    [Tooltip("Множитель урона при крите (2 = +100% урона). Умножается на ТЕКУЩИЙ baseDamage юнита (сам уже растёт от тиров).")]
    [Min(1f)] public float critDamageMultiplier = 2f;

    [Header("Вспышка critwolf (красный свет в пасти)")]
    [Tooltip("Имя невидимого дочернего объекта с красным Light, который владелец вкладывает волку в пасть. Объект должен лежать ВЫКЛЮЧЕННЫМ — скрипт включает его на время вспышки.")]
    public string critLightObjectName = "critwolf";
    [Tooltip("Сколько секунд горит красный свет при крите.")]
    [Min(0.05f)] public float critFlashSeconds = 0.2f;

    [Header("Диагностика")]
    [Tooltip("Писать в консоль каждый крит волка.")]
    public bool logCrits = true;

    private UnitStats myStats;
    private NeutralFactionTag factionTag;
    private BattleAI battleAI;
    // доступ к цели BattleAI — по прецеденту проекта (ShooterAttackLoop, HeroMovement, HeroTargetCircles)
    private System.Reflection.FieldInfo currentTargetField;

    private bool shooterRefusalLogged = false;
    private bool critLightMissingLogged = false;
    private Coroutine lightRoutine;

    void Start()
    {
        // страховка: если владельц забыл выключить critwolf в редакторе (свет горит всегда), гасим на runtime.
        // В самом редакторе ничего не меняем — только в игре.
        Transform lightObject = FindCritLightObject();
        if (lightObject != null && lightObject.gameObject.activeSelf)
        {
            lightObject.gameObject.SetActive(false);
            Debug.Log($"[КРИТ-ВОЛК] {gameObject.name}: объект «{critLightObjectName}» был включён в редакторе — погашен на старте игры (свет должен вспыхивать только при крите).");
        }
    }

    void Awake()
    {
        myStats = GetComponent<UnitStats>();
        if (myStats == null) myStats = GetComponentInParent<UnitStats>();

        factionTag = GetComponent<NeutralFactionTag>();
        if (factionTag == null) factionTag = GetComponentInParent<NeutralFactionTag>();

        battleAI = GetComponent<BattleAI>();
        if (battleAI == null) battleAI = GetComponentInParent<BattleAI>();
        if (battleAI != null)
            currentTargetField = typeof(BattleAI).GetField("currentTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    }

    /// <summary>Текущий шанс крита: база + прибавка за каждый тир выше первого.</summary>
    public float GetCritChance()
    {
        int tier = factionTag != null ? factionTag.tier : 1;
        return baseCritChance + critChancePerTier * Mathf.Max(0, tier - 1);
    }

    /// <summary>Кадр события удара анимации. true — замах потрачен на КРИТ, обычный урон BattleAI за этот удар отменяется.</summary>
    public bool InterceptAttackHit()
    {
        if (myStats == null || myStats.isDead) return false;

        // Отказ по причине «дальнобой»: у стрелка удар наносит летящий снаряд (ShooterProjectileAttack),
        // перехват замаха там ломает механику полёта снаряда. Скрипт предназначен для ближнего боя (волк).
        var shooter = battleAI != null ? battleAI.GetComponent<ShooterProjectileAttack>() : null;
        if (shooter != null && shooter.enabled)
        {
            if (!shooterRefusalLogged)
            {
                shooterRefusalLogged = true;
                Debug.Log($"[КРИТ-ВОЛК] {gameObject.name}: отказ — на юните есть включённый ShooterProjectileAttack (дальнобой), крит перехватом замаха не поддерживается.");
            }
            return false;
        }

        // Ролл шанса
        float chance = GetCritChance();
        if (Random.value >= chance) return false;

        // Крит выпал — цель должна быть жива и в пределах лимита дальности,
        // иначе отдаём удар обычному пути BattleAI (он сам разберёт промах по своим правилам).
        Transform target = (battleAI != null && currentTargetField != null) ? currentTargetField.GetValue(battleAI) as Transform : null;
        if (target == null || IsTargetDead(target)) return false;

        float hitLimit = battleAI.maxHitDistance >= 0f ? battleAI.maxHitDistance : BattleAI.defaultHitDistance;
        if (hitLimit >= 0f && Vector3.Distance(transform.position, target.position) > hitLimit) return false;

        ApplyCrit(target, chance);
        return true;
    }

    private void ApplyCrit(Transform target, float chance)
    {
        float damage = myStats.baseDamage * critDamageMultiplier;
        float dealtDamage = 0f;

        var targetHeroStats = target.GetComponent<HeroStats>();
        if (targetHeroStats == null) targetHeroStats = target.GetComponentInParent<HeroStats>();

        var targetUnitStats = target.GetComponent<UnitStats>();
        if (targetUnitStats == null) targetUnitStats = target.GetComponentInParent<UnitStats>();

        if (targetHeroStats == null && targetUnitStats == null)
        {
            Debug.Log($"[КРИТ-ВОЛК] {gameObject.name}: цель {target.name} уцелела без урона — на ней нет ни HeroStats, ни UnitStats, крит не к чему применить.");
            return;
        }

        if (targetHeroStats != null) dealtDamage = targetHeroStats.TakeDamage(damage);
        if (targetUnitStats != null) dealtDamage = targetUnitStats.TakeDamage(damage);

        if (logCrits)
            Debug.Log($"<color=orange>[КРИТ-ВОЛК]</color> {gameObject.name} ВВАЛИЛ КРИТ ({chance * 100f:0.#}% шанс): {dealtDamage:0} урона (база {myStats.baseDamage:0} × {critDamageMultiplier:0.#}) по {target.name}.");

        if (battleAI.showDamagePopups && dealtDamage > 0f)
            DamagePopup.Spawn(target.position + Vector3.up * 1.5f, dealtDamage, true);

        FlashCritLight();
    }

    // ==================== ВСПЫШКА critwolf (красный свет в пасти, 0.2 с) ====================

    private void FlashCritLight()
    {
        // ищем каждый крит (криты редки, поиска по детям не жалко) — так владелец может в любой момент переложить объект в другую кость пасти
        Transform lightObject = FindCritLightObject();
        if (lightObject == null)
        {
            if (!critLightMissingLogged)
            {
                critLightMissingLogged = true;
                Debug.Log($"[КРИТ-ВОЛК] {gameObject.name}: отказ вспышки — дочерний объект «{critLightObjectName}» не найден. Вложи волку в пасть невидимый объект с красным Light с этим именем (объект должен лежать ВЫКЛЮЧЕННЫМ).");
            }
            return;
        }
        critLightMissingLogged = false;

        // повторный крит во время горящей вспышки: перезапускаем таймер, чтобы свет не мигал
        if (lightRoutine != null) StopCoroutine(lightRoutine);
        lightRoutine = StartCoroutine(LightFlashRoutine(lightObject.gameObject));
    }

    private IEnumerator LightFlashRoutine(GameObject flashObject)
    {
        flashObject.SetActive(true);
        yield return new WaitForSeconds(critFlashSeconds);
        if (flashObject != null) flashObject.SetActive(false);
        lightRoutine = null;
    }

    /// <summary>Поиск среди всех потомков ВМЕСТЕ с выключенными (обычный Find не видит деактивированных детей).</summary>
    private Transform FindCritLightObject()
    {
        Transform[] all = transform.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != transform && all[i].name == critLightObjectName) return all[i];
        }
        return null;
    }
}
