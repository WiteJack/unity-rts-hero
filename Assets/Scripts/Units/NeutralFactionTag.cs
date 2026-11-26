using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Дельта-компонент нейтрала (ОБНОВЛЕНИЕ 30). Вешается на КОРЕНЬ нейтрала — рядом с UnitStats + BattleAI
/// (изначальные характеристики настраиваются в UnitStats/BattleAI инспекторе, этот скрипт их только множит по тиру).
///
/// Диздок владельца:
///  - Фракция — ПОЛЕ, не слой: боевой слой у всех нейтралов один — 8 (NEUTRAL).
///  - Босс: галочка → моделька автоматически ×1.3 (в эдиторе, прямо после постановки галочки).
///    Уникальные боссы со своей моделькой — scale руками, повторного ×1.3 не будет.
///  - Респавн: каждый нейтрал несёт СВОЙ таймер — после смерти появляется на ТОЙ ЖЕ точке (1 мин 30 с по умолчанию).
///    Никаких областей/составов — расставил префабы по карте и всё.
///  - Тир (1..5): переход тир→тир (после убийства босса фракции) помечает модельку КОНТУРОМ цвета тира (ОБНОВЛЕНИЕ 33,
///    воля владельца: НЕ полная заливка — только очертания, шерсть остаётся натуральной):
///    тир 1 = без контура, 2 = жёлтый, 3 = оранжевый, 4 = красный, 5 = ярко-красный.
///    Реализация: шейдер Andron/TierOutline (Assets/Shaders/TierOutline.shader) вторым слотом материалов
///    (hull outline: вывернутые треугольники, выдавленные вдоль нормалей); исходные материалы запоминаются и восстанавливаются.
///    Каждый новый тир повышает ВСЕ характеристики КРОМЕ скорости атаки и скорости передвижения ×2
///    ОТ ИЗНАЧАЛЬНЫХ значений инспектора (кабольд 300 HP → 600 → 1200…; так же атака, дальность, защита, реген).
///  - Дроп: при смерти сундучок со случайной вещью (цвет по тиру) + золото; у босса — золото побольше.
///
/// Заметка: на нейтралов НЕ вешать CorpseDissolve — скрипт при смерти сам его гасит (иначе труп уничтожится до респавна).
/// HeroStats.cs НИКОГДА не трогаем — этот скрипт его не касается.
/// </summary>
public class NeutralFactionTag : MonoBehaviour
{
    public enum NeutralFaction { Wolf, Rat, Gnoll, Kobold, Bandit }

    public const float BossScaleFactor = 1.3f;

    [Header("Фракция и босс (ОБНОВЛЕНИЕ 30)")]
    [Tooltip("Фракция нейтрала — ПОЛЕ, не слой. Смерть босса фракции усиливает ТОЛЬКО эту фракцию (волки, крысы, гноллы, кабольды, бандиты).")]
    public NeutralFaction faction = NeutralFaction.Wolf;

    [Tooltip("Босс. В ЭДИТОРЕ после постановки галочки моделька автоматически вырастет в 1.3 раза; сняние галочки вернёт как было. Уникальные боссы со своей моделькой — scale руками.")]
    public bool isBoss = false;

    [Header("Тир (1..5)")]
    [Tooltip("Тир 1 = без контура (оригинал), 2 и выше — ТОЛЬКО контур выбранного цвета, заливка не трогается (Об.33). Цвета контура: 2 = жёлтый, 3 = оранжевый, 4 = красный, 5 = ярко-красный. Каждый тир выше 1 повышает ВСЕ характеристики КРОМЕ скорости атаки и скорости передвижения ×2 от изначальных значений инспектора.")]
    [Range(1, 5)] public int tier = 1;
    [Tooltip("Толщина тирового контура в ЕДИНИЦАХ МЕША (Об.33): у мелких моделей из mesh2motion кости крошечные, подбирайте вручную. 0 = контур выключен.")]
    [Min(0f)] public float tierOutlineWidth = 0.02f;

    [Header("Респавн (свой таймер, своя точка)")]
    [Tooltip("Секунд до возрождения на СВОЕЙ точке после смерти (диздок: 1 мин 30 с = 90)")]
    public float respawnDelay = 90f;
    [Tooltip("Сколько секунд труп лежит на месте перед тем, как моделька спрячется до респавна")]
    public float hideCorpseDelay = 10f;

    [Header("Дроп при смерти")]
    [Tooltip("Выдавать герою золото за убийство (нейтралов убивает только герой — союзники с ними не воюют)")]
    public bool dropGold = true;
    [Tooltip("Сколько золота падает с обычного нейтрала")]
    public int goldOnDeath = 20;
    [Tooltip("Сколько золота падает с БОССА (вместо goldOnDeath)")]
    public int bossGoldOnDeath = 100;
    [Tooltip("Спавнить сундучок со случайной вещью на месте смерти (цвет сундучка по тиру; тир 5 даёт сундучок тира 4 — выше в базе нет)")]
    public bool dropChest = true;

    // изначальные значения инспектора (захвачены один раз в Awake) — масштаб считаем ровно от них, ×2^(тир−1)
    private bool basesCaptured = false;
    private float baseMaxHP, baseDamage, baseDefense, baseRegen, baseAttackRange;

    private UnitStats stats;
    private Vector3 baseSpawnPos;
    private Quaternion baseSpawnRot;
    private bool deathHandled = false;
    private Renderer[] hiddenRenderers;
    private Collider[] hiddenColliders;

    // ОБНОВЛЕНИЕ 33: контур тира вместо полной заливки
    public const string OutlineShaderName = "Andron/TierOutline";
    private Dictionary<Renderer, Material[]> originalMaterials; // снимок исходных материалов (делается до первой окраски)
    private Material outlineMaterial;                            // кэш контурного материала текущего тира
    private int outlineMaterialTier = -1;

    // ==================== ЖИЗНЕННЫЙ ЦИКЛ ====================

    void Awake()
    {
        stats = GetComponent<UnitStats>();
        if (stats == null) stats = GetComponentInParent<UnitStats>();
    }

    void Start()
    {
        // точка респавна = где нейтрал был РАССТАВЛЕН (не где умер)
        baseSpawnPos = transform.position;
        baseSpawnRot = transform.rotation;

        ApplyTierToStats();
        if (tier >= 2) RecolorByTier();
    }

    void Update()
    {
        if (deathHandled) return;
        if (stats != null && stats.isDead)
        {
            deathHandled = true;
            OnDied();
        }
    }

    // ==================== ГАЛОЧКА «БОСС» → СРАЗУ БОЛЬШЕ (в эдиторе) ====================

    // Основа «было/стало»: масштаб меняем ТОЛЬКО в момент переключения галочки,
    // поэтому повторного ×1.3 при перезагрузке сцены/префаба не бывает.
#if UNITY_EDITOR
    private bool? bossStateObserved = null;

    void OnValidate()
    {
        if (bossStateObserved == null) { bossStateObserved = isBoss; return; }
        if (bossStateObserved.Value == isBoss) return;
        bossStateObserved = isBoss;

        if (isBoss) transform.localScale = transform.localScale * BossScaleFactor;
        else transform.localScale = transform.localScale / BossScaleFactor;

        Debug.Log($"[НЕЙТРАЛ] {name}: галочка «Босс» → scale ×{(isBoss ? BossScaleFactor.ToString("0.##") : (1f / BossScaleFactor).ToString("0.###"))}");
    }
#endif

    // ==================== ТИРЫ: СТАТЫ (×2 ОТ ИЗНАЧАЛЬНЫХ, КРОМЕ СКОРОСТИ АТАКИ/ПЕРЕДВИЖЕНИЯ) ====================

    /// <summary>Применяет множитель тира к статам. Живому юниту было HP переносится пропорционально; мёртвый получит полное HP на респавне.</summary>
    public void ApplyTierToStats(bool healProportional = true)
    {
        if (stats == null) return;

        if (!basesCaptured)
        {
            baseMaxHP = stats.maxHP;
            baseDamage = stats.baseDamage;
            baseDefense = stats.defense;
            baseRegen = stats.hpRegenPerSecond;
            var ai = GetComponent<BattleAI>();
            if (ai != null) baseAttackRange = ai.attackRange;
            basesCaptured = true;
        }

        float mult = Mathf.Pow(2f, tier - 1);
        float oldMax = stats.maxHP;
        stats.maxHP = baseMaxHP * mult;
        stats.baseDamage = baseDamage * mult;
        stats.defense = baseDefense * mult;
        stats.hpRegenPerSecond = baseRegen * mult;

        var battleAI = GetComponent<BattleAI>();
        if (battleAI != null && baseAttackRange > 0f)
        {
            battleAI.attackRange = baseAttackRange * mult;
            // Лимит удара (maxHitDistance/инжектор UnitHitRangeInjector) — отдельная механика Обн. 28;
            // если дальниковый/ближниковый лимит нужен тировым — ставьте юниту свой maxHitDistance.
        }

        if (healProportional && oldMax > 0f && stats.currentHP > 0f && !stats.isDead)
            stats.currentHP = Mathf.Min(stats.currentHP * (stats.maxHP / oldMax), stats.maxHP);

        if (tier > 1) Debug.Log($"[НЕЙТРАЛ] {gameObject.name}: тир {tier} → HP {stats.maxHP:0}, атака {stats.baseDamage:0}, защита {stats.defense:0}, реген {stats.hpRegenPerSecond:0.#}/с (×{mult:0.#} от изначальных). Скорость атаки и передвижения БЕЗ изменений (диздок).");
    }

    // ==================== ТИРЫ: КОНТУР ЦВЕТА ТИРА (ОБНОВЛЕНИЕ 33, вместо полной заливки) ====================

    /// <summary>Тир 1 = без контура (оригинал), 2 = жёлтый, 3 = оранжевый, 4 = красный, 5 = ярко-красный.
    /// Базовые материалы НЕ перекрашиваются — снимок исходных делается до первой окраски и всегда восстанавливается,
    /// контур добавляется/снимается вторым слотом материалов.</summary>
    public static Color TierColor(int t)
    {
        switch (t)
        {
            case 2: return new Color(1f, 0.92f, 0.20f);   // жёлтый — как сундучки тира 2
            case 3: return new Color(1f, 0.55f, 0.05f);   // оранжевый
            case 4: return new Color(0.85f, 0.10f, 0.10f); // красный
            case 5: return new Color(1f, 0.05f, 0.05f);   // ярко-красный
            default: return Color.white;
        }
    }

    public void RecolorByTier()
    {
        int contoured = 0;
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers != null)
        {
            if (originalMaterials == null) originalMaterials = new Dictionary<Renderer, Material[]>();

            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;

                // снимок оригинала (один раз, до первой окраски); отфильтровываем старые контуры — чтобы не наращивать слоты
                if (!originalMaterials.ContainsKey(r))
                {
                    var snapshot = new List<Material>();
                    foreach (var m in r.sharedMaterials)
                        if (m == null || m.shader == null || m.shader.name != OutlineShaderName)
                            snapshot.Add(m);
                    originalMaterials[r] = snapshot.ToArray();
                }

                if (tier <= 1)
                {
                    r.sharedMaterials = originalMaterials[r]; // тир 1 — оригинал без контура
                    continue;
                }

                Material outline = GetOutlineMaterial(tier);
                if (outline == null)
                {
                    r.sharedMaterials = originalMaterials[r];
                    continue; // причина отказа уже в логе (GetOutlineMaterial)
                }

                var baseMats = originalMaterials[r];
                var withOutline = new Material[baseMats.Length + 1];
                for (int i = 0; i < baseMats.Length; i++) withOutline[i] = baseMats[i];
                withOutline[baseMats.Length] = outline;
                r.sharedMaterials = withOutline;
                contoured++;
            }
        }
        Debug.Log($"[НЕЙТРАЛ] {gameObject.name}: тир {tier} → контур {(tier <= 1 ? "нет (оригинал)" : TierColor(tier).ToString("F2"))}, рендереров с контуром: {contoured} (базовый материал не менялся — Обновление 33).");
    }

    /// <summary>Контурный материал (кэш на текущий тир). null — шейдер не найден (не попал в сборку/не скомпилировался).</summary>
    private Material GetOutlineMaterial(int t)
    {
        if (outlineMaterial != null && outlineMaterialTier == t) return outlineMaterial;

        var shader = Shader.Find(OutlineShaderName);
        if (shader == null)
        {
            Debug.Log($"[НЕЙТРАЛ] {gameObject.name}: отказ контура тира — шейдер «{OutlineShaderName}» не найден (проверь, что Assets/Shaders/TierOutline.shader в проекте и URP не режет шейдер при сборке).");
            return null;
        }

        outlineMaterial = new Material(shader);
        outlineMaterial.SetColor("_OutlineColor", TierColor(t));
        outlineMaterial.SetFloat("_OutlineWidth", tierOutlineWidth);
        outlineMaterialTier = t;
        return outlineMaterial;
    }

    // ==================== СМЕРТЬ: ДРОП + БОСС УСИЛИВАЕТ ФРАКЦИЮ + РЕСПАВН ====================

    private void OnDied()
    {
        Vector3 deathPos = transform.position;
        Debug.Log($"[НЕЙТРАЛ] {gameObject.name} ({faction}, тир {tier}) погиб — возрождение через {respawnDelay:0} с на своей точке.");

        DropLoot(deathPos);
        if (isBoss) BoostFactionAfterBossDeath();

        StartCoroutine(RespawnRoutine());
    }

    private void DropLoot(Vector3 deathPos)
    {
        if (dropGold)
        {
            int gold = isBoss ? bossGoldOnDeath : goldOnDeath;
            if (gold > 0 && GoldManager.Instance != null)
            {
                GoldManager.Instance.AddGold(gold);
                Debug.Log($"<color=yellow>[НЕЙТРАЛ] +{gold} золота</color> за убийство {gameObject.name}{(isBoss ? " (БОСС)" : "")}.");
            }
        }

        if (dropChest)
        {
            var chestTier = (EquipmentDatabase.Tier)Mathf.Clamp(tier, 1, 4);
            var pickup = EquipmentPickup.Spawn(deathPos, chestTier);
            if (pickup != null) Debug.Log($"[НЕЙТРАЛ] {gameObject.name}: выпал сундучок (тир {(int)chestTier}) на месте смерти.");
        }
    }

    /// <summary>Смерть босса: тир+1 ВСЕЙ фракции (перекраска + ×2 статам), мёртвому боссу тоже — возродится уже новым тиром.</summary>
    public void BoostFactionAfterBossDeath()
    {
        int bumped = 0;
        var all = FindObjectsByType<NeutralFactionTag>();
        if (all != null)
        {
            foreach (var n in all)
            {
                if (n == null || n.faction != faction || n.tier >= 5) continue;
                n.tier++;
                n.ApplyTierToStats();
                if (n != this) n.RecolorByTier(); // мёртвому перекраска произойдёт при возрождении
                bumped++;
            }
        }
        Debug.Log($"[НЕЙТРАЛ] БОСС фракции {faction} пал: тир повышен у {bumped} нейтрал(ов) фракции (перекраска + ×2 статам).");
    }

    private IEnumerator RespawnRoutine()
    {
        // гасим CorpseDissolve, если владелец его повесил — труп ДОЛЖЕН дожить до респавна
        var corpse = GetComponent<CorpseDissolve>();
        if (corpse != null)
        {
            corpse.StopAllCoroutines();
            corpse.enabled = false;
        }

        var agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        hiddenColliders = GetComponentsInChildren<Collider>(true);
        SetColliders(hiddenColliders, false);

        float hiddenAt = Mathf.Clamp(hideCorpseDelay, 0f, respawnDelay);
        if (hiddenAt > 0f) yield return new WaitForSeconds(hiddenAt);

        hiddenRenderers = GetComponentsInChildren<Renderer>(true);
        if (hiddenRenderers != null) foreach (var r in hiddenRenderers) if (r != null) r.enabled = false;

        float remaining = Mathf.Max(0.1f, respawnDelay - hiddenAt);
        yield return new WaitForSeconds(remaining);

        // возвращаемся НА СВОЮ точку (диздок: нейтрал всегда появляется там же, где был расставлен)
        transform.SetPositionAndRotation(baseSpawnPos, baseSpawnRot);
        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false; // UnitStats.Die() ставит isStopped=true, без сброса волк стоит как вкопанный
            // защита: точка респавна могла оказаться вне NavMesh (двинулое место после гибели, правки уровня) — ищем ближайший узел и варп туда
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(baseSpawnPos, out var hit, 2f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    Debug.Log($"[НЕЙТРАЛ] {gameObject.name}: точка респавна была вне NavMesh, варп к ближайшему узлу ({hit.position}).");
                }
                else
                {
                    Debug.LogWarning($"[НЕЙТРАЛ] {gameObject.name}: агент НЕ на NavMesh — точка респавна и зона 2 м вокруг не на NavMesh, юнит может не ходить.");
                }
            }
        }

        SetColliders(hiddenColliders, true);
        if (hiddenRenderers != null) foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;

        if (stats != null)
        {
            stats.currentHP = stats.maxHP;
            stats.isDead = false;
        }
        RecolorByTier();

        var anim = GetComponentInChildren<Animator>(true);
        if (anim != null)
        {
            anim.ResetTrigger("OnDeath"); // залипший триггер смерти мог снова уложить волка сразу после респавна
            anim.ResetTrigger("OnAttack");
            anim.Rebind();   // жёсткий сброс машины состояний: CrossFade("Idle") молча не срабатывал, если стейт не назван "Idle" — волк оставался в позе трупа и не атаковал
            anim.Play("Idle", 0, 0f);
            Debug.Log($"[НЕЙТРАЛ] {gameObject.name}: аниматор сброшен (Rebind + Idle) — фикс воскресания в позе трупа (Об.33).");
        }

        deathHandled = false;
        Debug.Log($"[НЕЙТРАЛ] {gameObject.name} ({faction}, тир {tier}) возродился на своей точке, HP полное.");
    }

    private void SetColliders(Collider[] cols, bool on)
    {
        if (cols == null) return;
        foreach (var c in cols) if (c != null) c.enabled = on;
    }

    // ==================== КОНТЕКСТ-МЕНЮ ДЛЯ НАСТРОЙКИ В ЭДИТОРЕ ====================

    [ContextMenu("Нейтрал: применить тир сейчас (перекраска + статы)")]
    private void ContextApplyTier()
    {
        ApplyTierToStats(false);
        RecolorByTier();
    }

    [ContextMenu("Нейтрал: поднять тир вручную (+1)")]
    private void ContextBumpTier()
    {
        if (tier >= 5) { Debug.Log("[НЕЙТРАЛ] Тир уже максимальный (5)."); return; }
        tier++;
        ApplyTierToStats();
        RecolorByTier();
    }
}
