using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Навыки Андрона (диздок "Герои и навыки1"). Вешается на КОРЕНЬ героя, HeroStats НЕ трогаем.
///
/// Навыки (5 кнопок в нижней панели, прокачка каждые 5 уровней героя, максимум 5):
///
/// 1. Ярость короля (АКТИВ, перезарядка 20 сек) — удары мечом в секторе 90° перед героем.
///    Ур.N: N ударов, урон = 30 + (0.5 + 0.5*N) * СИЛА, каждый удар создаёт волну,
///    дальность волны N-го удара = дальность атаки * (1 + 0.5*N) — каждая дальше.
///    Волны — ярко золото-фиолетовые (спец-эффект из диздока), рисуются кодом.
///    Каждый удар — замах анимацией атаки героя (PlayAttackAnimation), 5 ударов ≈ в течение секунды.
///
/// 2. Взрыв щита (АКТИВ, перезарядка 1 мин 20 сек) — волна ВОКРУГ героя, бьёт нейтралов и врагов.
///    Ур.1-5: урон = 50 + {1,1.5,1.5,2,2}*СИЛА + {1,1,1.5,1.5,2}*ВЫНОСЛИВОСТЬ,
///    радиус = дальность атаки * {7,9,11,13,16} (+600%..+1500%); ПРОТИВНИК получает
///    на +20% общего урона больше, чем нейтрал (по диздоку). Волна — белая (по диздоку).
///
/// 3. Щит империи (ПАССИВ) — отражение урона: 10/20/30/40/50%.
///    Работает ЧЕРЕЗ суммарное отражение: пассивка + уникальное снаряжение (AndronUniqueEquipment).
///    Отражение ловим "по падению HP" (прямой хук в HeroStats.TakeDamage без правки невозможен):
///    если HP героя упало быстрее, чем капает реген — считаем урон и возвращаем % ближайшему врагу.
///
/// 4. Родословная (ПАССИВ) — +сила и +выносливость: 5/10/20/35/50.
///
/// 5. Плюсик атрибутов — кнопка распределения нераспределённых очков характеристик
///    (HeroExperience.unspentPoints). Клик — тратит 1 очко и поднимает ВСЕ 6 атрибутов
///    на +1 одновременно (по уточнению: не по одному по кругу, а всем сразу).
///
/// Прокачка: по диздоку CanUpgrade = skillLevel < 5 И heroLevel >= skillLevel*5 (5 ур. героя — 1-й уровень скилла,
/// 10 — второй, 15 — третий, 20 — четвёртый, 25 — пятый). "+"-кнопка в UI вызывает UpgradeSkill.
/// ОБНОВЛЕНИЕ 25: тестовый режим завершён — гейт по уровню героя снова ВКЛЮЧЁН по диздоку (по умолчанию ВКЛ):
/// ур.5 → lvl1, ур.10 → lvl2, ур.15 → lvl3, ур.20 → lvl4, ур.25 → lvl5. Для тестов можно снять галку в инспекторе.
/// </summary>
public class AndronSkills : MonoBehaviour
{
    public enum SkillId { Rage = 0, ShieldBlast = 1, EmpireShieldPassive = 2, BloodlinePassive = 3, Attributes = 4 }

    [Header("Уровни навыков 0..5 (0 — не изучен; открываются: ур.5, 10, 15, 20, 25 героя)")]
    public int rageLevel = 0;           // Ярость короля
    public int shieldBlastLevel = 0;    // Взрыв щита
    public int empireShieldLevel = 0;   // Щит империи (пассивно)
    public int bloodlineLevel = 0;      // Родословная (пассивно)

    [Header("Перезарядки, сек (по диздоку)")]
    public float rageCooldown = 20f;
    public float shieldBlastCooldown = 80f;

    [Header("Ярость короля: интервал между ударами, сек (по умолчанию 5 ударов ≈ 1 сек)")]
    public float rageStrikeInterval = 0.2f;

    [Header("Гейт прокачки по уровню героя (ОБНОВЛЕНИЕ 25 — по диздоку, по умолчанию ВКЛ)")]
    [Tooltip("ВКЛ = по диздоку: ур.5 → lvl1, ур.10 → lvl2, 15 → 3, 20 → 4, 25 → 5. ВЫКЛ = каждый уровень (тест)")]
    public bool gateSkillsByHeroLevel = true;

    [Header("Отражение урона (суммарно с уникальным снаряжением)")]
    [Tooltip("Радиус поиска врага для возврата урона")]
    public float reflectRange = 8f;
    [Tooltip("Порог: HP должно упасть за кадр сильнее этого, чтобы считать это уроном (а не регеном/округлением)")]
    public float reflectMinDrop = 0.5f;

    [Header("Дебаг")]
    public bool logSkills = true;

    // ---- Уже известные скрипты героя ----
    private HeroStats stats;
    private HeroExperience xp;
    private BattleAI battleAI;
    private EquipmentSystem equip;
    private AndronUniqueEquipment uniques;

    // ---- Кулдауны ----
    private float rageReadyTime, shieldBlastReadyTime;
    /// <summary>Остаток перезарядки навыка в секундах (для UI).</summary>
    public float GetCooldownRemaining(SkillId id)
    {
        float t = id == SkillId.Rage ? rageReadyTime : id == SkillId.ShieldBlast ? shieldBlastReadyTime : 0f;
        return Mathf.Max(0f, t - Time.time);
    }
    /// <summary>Готов ли активный навык к использованию.</summary>
    public bool IsReady(SkillId id) { return GetCooldownRemaining(id) <= 0f; }

    // ---- Уровень навыка / доступность прокачки ----
    public int GetSkillLevel(SkillId id)
    {
        switch (id)
        {
            case SkillId.Rage: return rageLevel;
            case SkillId.ShieldBlast: return shieldBlastLevel;
            case SkillId.EmpireShieldPassive: return empireShieldLevel;
            case SkillId.BloodlinePassive: return bloodlineLevel;
            default: return xp != null ? xp.unspentPoints : 0; // для кнопки атрибутов — число очков
        }
    }

    /// <summary>Можно ли повысить навык на "+" (макс 5; по диздоку — ещё гейт уровнем героя,
    /// с ОБНОВЛЕНИЯ 25 гейт снова ВКЛЮЧЁН по диздоку).</summary>
    public bool CanUpgrade(SkillId id)
    {
        int lvl = GetSkillLevel(id);
        if (id == SkillId.Attributes || lvl >= 5) return false;
        if (!gateSkillsByHeroLevel) return true; // тестовый режим: каждый уровень (галка снята в инспекторе)
        int heroLevel = xp != null ? xp.level : 1;
        int allowed = heroLevel / 5; // ур.5 → 1, ур.10 → 2, 15 → 3, 20 → 4, 25 → 5
        return lvl < allowed;
    }

    /// <summary>Сколько нераспределённых очков (для UI).</summary>
    public int UnspentPoints { get { return xp != null ? xp.unspentPoints : 0; } }

    // ---------------- ЖИЗНЕННЫЙ ЦИКЛ ----------------

    void Awake()
    {
        stats = GetComponent<HeroStats>();
        if (stats == null) stats = GetComponentInChildren<HeroStats>();
        xp = GetComponent<HeroExperience>();
        if (xp == null) xp = GetComponentInChildren<HeroExperience>();
        battleAI = GetComponent<BattleAI>();
        equip = GetComponent<EquipmentSystem>();
        uniques = GetComponent<AndronUniqueEquipment>();
    }

    void Start()
    {
        // применяем пассив "Родословная" (если уровень прокачан в инспекторе для тестов)
        ApplyBloodline();
        // ОБНОВЛЕНИЕ 23: маркер версии кода — если этой строки НЕТ в консоли, то проект скомпилировался
        // со старой сборкой (ищи красные ошибки компиляции) или AndronSkills.cs не заменён.
        Debug.Log($"[НАВЫК] AndronSkills ОБНОВЛЕНИЕ 23 запущен: гейт уровня героя gateSkillsByHeroLevel = {gateSkillsByHeroLevel} (true/ВКЛ = по диздоку: ур.5→lvl1, 10→2, 15→3, 20→4, 25→5 (ОБНОВЛЕНИЕ 25)). Нераспределённых очков: {UnspentPoints}");
    }

    void Update()
    {
        if (stats == null || stats.isDead) { lastHP = stats != null ? stats.currentHP : 0f; return; }

        // ---- Отражение урона (Щит империи) ----
        UpdateReflect();

        // ---- Горячие клавиши: 1/2 — активные навыки, 5 — вложить очко атрибутов ----
        HandleHotkeys();
    }

    // ---------------- ПРОКАЧКА ("+") ----------------

    /// <summary>Поднять навык на 1 (вызывает "+"-кнопка в UI). Возвращает успех.</summary>
    public bool UpgradeSkill(SkillId id)
    {
        // ОБНОВЛЕНИЕ 23: отказ теперь ВСЕГДА пишет точную причину в консоль (диагностика "не даёт качать").
        int lvlCheck = GetSkillLevel(id);
        if (id == SkillId.Attributes) return false;
        if (lvlCheck >= 5)
        {
            if (logSkills) Debug.Log($"[НАВЫК] {id}: уже МАКСИМУМ (5/5) — выше нельзя.");
            return false;
        }
        if (gateSkillsByHeroLevel)
        {
            int heroLevel = xp != null ? xp.level : 1;
            int allowed = heroLevel / 5;
            if (lvlCheck >= allowed)
            {
                Debug.Log($"[НАВЫК] {id}: ЗАБЛОКИРОВАНО гейтом уровня героя (gateSkillsByHeroLevel = ВКЛ): герой ур. {heroLevel} разрешает только {allowed} ур. навыка, а навык уже {lvlCheck}. ТЕСТОВЫЙ РЕЖИМ = сними галочку gateSkillsByHeroLevel у AndronSkills в инспекторе.");
                return false;
            }
        }
        switch (id)
        {
            case SkillId.Rage: rageLevel++; break;
            case SkillId.ShieldBlast: shieldBlastLevel++; break;
            case SkillId.EmpireShieldPassive: empireShieldLevel++; break;
            case SkillId.BloodlinePassive: bloodlineLevel++; break;
        }
        if (logSkills) Debug.Log($"[НАВЫК] {id} повышен до ур. {GetSkillLevel(id)}");
        if (id == SkillId.BloodlinePassive) ApplyBloodline();
        return true;
    }

    /// <summary>Вложенные игроком очки атрибутов: [сила, живучесть, выносливость, ловкость, мудрость, сила воли].
    /// Обновление 21: один клик «+» добавляет +1 СРАЗУ во все 6 атрибутов.</summary>
    private float[] attrPoints = new float[6];

    // запасная дельта для пути без EquipmentSystem
    private float fbStr, fbVit, fbEnd;

    /// <summary>Вложить 1 очко характеристик: ВСЕ 6 атрибутов получают +1 (5-я кнопка).</summary>
    public bool SpendAttributePoint()
    {
        if (xp == null || xp.unspentPoints <= 0) return false;

        xp.unspentPoints--;
        for (int i = 0; i < attrPoints.Length; i++) attrPoints[i] += 1f;
        ApplyBloodline(); // пересобирает дельту навыков: Родословная + вложенные очки (максимум HP меняется через EquipmentSystem)

        if (logSkills) Debug.Log($"[НАВЫК] Очко вложено: ВСЕ 6 атрибутов +1 (сила {attrPoints[0]:0}, живучесть {attrPoints[1]:0}, выносливость {attrPoints[2]:0}, ловкость {attrPoints[3]:0}, мудрость {attrPoints[4]:0}, сила воли {attrPoints[5]:0}). Осталось очков: {xp.unspentPoints}");
        return true;
    }

    // ---------------- ПАССИВЫ ----------------

    private void ApplyBloodline()
    {
        if (equip == null)
        {
            // запасной путь без EquipmentSystem: снимаем прошлую дельту и накладываем новую напрямую
            // (HeroStats как файл НЕ правим — используем его публичные поля)
            float bloodFb = bloodlineLevel <= 0 ? 0f : new[] { 5f, 10f, 20f, 35f, 50f }[bloodlineLevel - 1];
            float str = bloodFb + attrPoints[0];
            float vit = attrPoints[1];
            float end = bloodFb + attrPoints[2];

            stats.strength -= fbStr; stats.vitality -= fbVit; stats.endurance -= fbEnd;
            stats.strength += str;   stats.vitality += vit;   stats.endurance += end;
            fbStr = str; fbVit = vit; fbEnd = end;
            stats.CalculateStats();
            return;
        }
        // Родословная: +сила и +выносливость 5/10/20/35/50 И ВЛОЖЕННЫЕ ОЧКИ атрибутов.
        // Всё идёт одной дельтой навыков EquipmentSystem — так её не затирают пересчёты снаряжения.
        float blood = bloodlineLevel <= 0 ? 0f : new[] { 5f, 10f, 20f, 35f, 50f }[bloodlineLevel - 1];
        equip.SetSkillBonuses(
            blood + attrPoints[0],
            attrPoints[1],
            blood + attrPoints[2],
            attrPoints[3],
            attrPoints[4],
            attrPoints[5]);
    }

    /// <summary>Суммарный % отражения: пассивка "Щит империи" + уникальное снаряжение.</summary>
    public float TotalReflectPercent
    {
        get
        {
            float p = empireShieldLevel <= 0 ? 0f : new[] { 10f, 20f, 30f, 40f, 50f }[empireShieldLevel - 1];
            if (uniques != null) p += uniques.ShieldReflectPercent; // 10% + 1% за уровень уникального снаряжения
            return p;
        }
    }

    // ---------------- ОТРАЖЕНИЕ УРОНА ----------------

    private float lastHP;
    private bool lastHPInitialized;
    private bool statBonusHPGuard; // максHP менялся — не считать первый скачок уроном

    /// <summary>Заметить изменение максимума HP (экип/навыки вызывают), чтобы не засчитать его как урон.</summary>
    public void NotifyMaxHPChanged() { statBonusHPGuard = true; }

    private void UpdateReflect()
    {
        if (!lastHPInitialized) { lastHP = stats.currentHP; lastHPInitialized = true; return; }

        // если изменился максимум — просто калибруемся и выходим
        if (statBonusHPGuard) { lastHP = stats.currentHP; statBonusHPGuard = false; return; }

        float drop = lastHP - stats.currentHP;
        lastHP = stats.currentHP;

        // небольшие изменения (< порога) — реген/округления, пропускаем
        if (drop <= reflectMinDrop) return;

        float percent = TotalReflectPercent;
        if (percent <= 0f) return;

        // ближайший враг (слой 9) в радиусе
        var hit = Physics.OverlapSphere(transform.position, reflectRange, 1 << 9);
        UnitStats best = null; float bestDist = float.MaxValue;
        foreach (var c in hit)
        {
            var u = c.GetComponentInParent<UnitStats>();
            if (u == null || u.isDead) continue;
            float dist = Vector3.Distance(transform.position, c.transform.position);
            if (dist < bestDist) { bestDist = dist; best = u; }
        }
        if (best == null) return;

        float reflected = drop * percent / 100f;
        best.TakeDamage(reflected);
        if (logSkills) Debug.Log($"[ЩИТ ИМПЕРИИ] Отражено {reflected:0} урона ({percent:0}%) в {best.gameObject.name}");
        // вылет цифры урона над врагом
        DamagePopup.Spawn(best.transform.position + Vector3.up * 1.5f, reflected, false);
    }

    // ---------------- АКТИВНЫЕ НАВЫКИ ----------------

    /// <summary>Использовать активный навык (клик по кнопке / клавиша). Возвращает успех.</summary>
    public bool CastSkill(SkillId id)
    {
        if (stats != null && stats.isDead) return false;
        switch (id)
        {
            case SkillId.Rage: return CastRage();
            case SkillId.ShieldBlast: return CastShieldBlast();
            default: return false; // пассивы и кнопка атрибутов не кастуются через CastSkill
        }
    }

    private bool CastRage()
    {
        if (rageLevel <= 0 || !IsReady(SkillId.Rage)) return false;
        float range = battleAI != null ? battleAI.attackRange : 2f;
        float strength = stats.strength;
        int strikes = rageLevel;
        float mult = 0.5f + 0.5f * rageLevel;

        rageReadyTime = Time.time + rageCooldown;
        StartCoroutine(RageStrikes(strikes, 30f, mult, strength, range));
        return true;
    }

    private IEnumerator RageStrikes(int strikes, float baseDamage, float strengthMult, float strength, float attackRange)
    {
        // N ударов с интервалом, каждый создаёт волну ДАЛЬШЕ предыдущей (+50% дальности атаки за удар)
        for (int s = 1; s <= strikes; s++)
        {
            if (stats != null && !stats.isDead) stats.PlayAttackAnimation(); // замах мечом на каждый удар
            float waveRadius = attackRange * (1f + 0.5f * s);
            float damage = baseDamage + strengthMult * strength;
            DamageSector(waveRadius, 90f, damage, new Color(1f, 0.85f, 0.2f)); // золото-фиолетовая волна
            if (logSkills) Debug.Log($"[ЯРОСТЬ КОРОЛЯ] удар {s}/{strikes}: {damage:0} урона, волна {waveRadius:0.0}м");
            yield return new WaitForSeconds(rageStrikeInterval);
        }
    }

    private bool CastShieldBlast()
    {
        if (shieldBlastLevel <= 0 || !IsReady(SkillId.ShieldBlast)) return false;

        float range = battleAI != null ? battleAI.attackRange : 2f;
        float[] sMult = { 1f, 1.5f, 1.5f, 2f, 2f };
        float[] eMult = { 1f, 1f, 1.5f, 1.5f, 2f };
        float[] radiusMult = { 7f, 9f, 11f, 13f, 16f }; // +600%..+1500% к дальности атаки (по диздоку)
        float baseDamage = 50f + sMult[shieldBlastLevel - 1] * stats.strength + eMult[shieldBlastLevel - 1] * stats.endurance;
        float radius = range * radiusMult[shieldBlastLevel - 1];

        shieldBlastReadyTime = Time.time + shieldBlastCooldown;

        // волна вокруг героя — по диздоку бьёт НЕЙТРАЛОВ и ВРАГОВ,
        // ПРОТИВНИК получает на +20% общего урона больше, чем нейтрал
        var hits = Physics.OverlapSphere(transform.position, radius);
        int damaged = 0;
        foreach (var c in hits)
        {
            int layer = c.gameObject.layer;
            if (layer != 9 && layer != 8) continue; // 9=ENEMY, 8=NEUTRAL
            var u = c.GetComponentInParent<UnitStats>();
            if (u == null || u.isDead) continue;
            if (c.transform.root == transform.root) continue;
            float dmg = layer == 9 ? baseDamage * 1.2f : baseDamage; // противнику +20%
            u.TakeDamage(dmg);
            DamagePopup.Spawn(c.transform.position + Vector3.up * 1.5f, dmg, false);
            damaged++;
        }

        SpawnWaveVisual(transform.position, radius, Color.white); // белая волна (диздок)
        if (logSkills) Debug.Log($"[ВЗРЫВ ЩИТА] ур.{shieldBlastLevel}: {baseDamage:0} урона (врагам ×1.2) по {damaged} целям, радиус {radius:0.0}м");
        return true;
    }

    /// <summary>Урон по сектору 90° перед героем (Ярость короля): враги + нейтралы.</summary>
    private void DamageSector(float radius, float sectorDegrees, float damage, Color waveColor)
    {
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        float cosLimit = Mathf.Cos(sectorDegrees * 0.5f * Mathf.Deg2Rad);

        var hits = Physics.OverlapSphere(transform.position, radius);
        foreach (var c in hits)
        {
            int layer = c.gameObject.layer;
            if (layer != 9 && layer != 8) continue; // враги и нейтралы
            var u = c.GetComponentInParent<UnitStats>();
            if (u == null || u.isDead) continue;
            if (c.transform.root == transform.root) continue;

            Vector3 to = c.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude < 0.01f || Vector3.Dot(to.normalized, fwd) >= cosLimit)
            {
                u.TakeDamage(damage);
                DamagePopup.Spawn(c.transform.position + Vector3.up * 1.5f, damage, false);
            }
        }

        SpawnWaveVisual(transform.position, radius, waveColor);
    }

    // ---------------- ВОЛНА (визуал кодом, без ассетов) ----------------

    /// <summary>Расширяющееся кольцо на земле: плоский quad с URP Unlit материалом + растворение.</summary>
    private void SpawnWaveVisual(Vector3 center, float maxRadius, Color color)
    {
        GameObject wave = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wave.name = "SkillWave";
        Destroy(wave.GetComponent<Collider>());
        wave.transform.position = center + Vector3.up * 0.05f;
        wave.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // лежит на земле

        var renderer = wave.GetComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null) renderer.material = new Material(shader); else renderer.material = new Material(Shader.Find("Unlit/Color"));
        renderer.material.color = color;

        StartCoroutine(WaveRoutine(wave.transform, renderer.material, maxRadius));
    }

    private IEnumerator WaveRoutine(Transform wave, Material mat, float maxRadius)
    {
        float duration = 0.5f;
        float t = 0f;
        Color baseColor = mat.color;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float d = Mathf.Lerp(0.5f, maxRadius * 2f, k);
            wave.localScale = new Vector3(d, d, 1f);
            baseColor.a = 0.8f * (1f - k);
            mat.color = baseColor;
            yield return null;
        }
        Destroy(wave.gameObject);
    }

    // ---------------- ГОРЯЧИЕ КЛАВИШИ ----------------

    private void HandleHotkeys()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) CastSkill(SkillId.Rage);
        if (kb.digit2Key.wasPressedThisFrame) CastSkill(SkillId.ShieldBlast);
        if (kb.digit5Key.wasPressedThisFrame) SpendAttributePoint();
#else
        if (Input.GetKeyDown(KeyCode.Alpha1)) CastSkill(SkillId.Rage);
        if (Input.GetKeyDown(KeyCode.Alpha2)) CastSkill(SkillId.ShieldBlast);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SpendAttributePoint();
#endif
    }

    // ---------------- Контекстное меню для тестов ----------------

    [ContextMenu("Тест: повысить Ярость короля")]
    private void TestUpRage() { rageLevel = Mathf.Min(5, rageLevel + 1); ApplyBloodline(); }
    [ContextMenu("Тест: повысить Взрыв щита")]
    private void TestUpBlast() { shieldBlastLevel = Mathf.Min(5, shieldBlastLevel + 1); }
    [ContextMenu("Тест: повысить Щит империи (пассив)")]
    private void TestUpEmpire() { empireShieldLevel = Mathf.Min(5, empireShieldLevel + 1); }
    [ContextMenu("Тест: повысить Родословную (пассив)")]
    private void TestUpBloodline() { bloodlineLevel = Mathf.Min(5, bloodlineLevel + 1); ApplyBloodline(); }
}
