using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ЕДИНЫЙ скрипт всей базы снаряжения (по ТЗ: "Все иконки снаряжений и снаряжения находятся в одном скрипте").
///
/// ОБНОВЛЕНИЕ 23: база переписана по новому диздоку шмота — ВСЕ 80 ПРЕДМЕТОВ (4 тира × 20) с точными
/// названиями и статами. Тир 1 — "Обычный сундук", Тир 2 — "Сундук жёлтого цвета", Тир 3 — "Оранжевого",
/// Тир 4 — "Красного" (цвета GetChestColor ниже).
///
/// КАК СТАТЫ ПРЕДМЕТОВ ПЕРЕВОДЯТСЯ В ДЕЛЬТУ (HeroStats НИКОГДА не правим — только дельты):
/// - "+X к атрибуту" (сила/живучесть/выносливость/ловкость/мудрость/сила воли) → напрямую в атрибут.
/// - "+X к атаке"     → сила +X/5 (по формуле диздока 1 сила = 5 атаки). Оригинал хранится в flatAttack для подсказки.
/// - "+X к защите"    → выносливость +X*10 (1 выносливость = 0.1 защиты). ПОБОЧНЫЙ ЭФФЕКТ: 1 вын = 0.5 HP/сек регена,
///   поэтому +1 защиты даёт ещё +5 HP/сек регена. Честный плоский канал защиты требует правки HeroStats (запрещено).
/// - "+X к восстановлению здоровья/сек" → выносливость +X*2 (1 вын = 0.5 HP/сек).
/// - "+X к восстановлению маны/сек"     → сила воли +X*2 (1 сила воли = 0.5 маны/сек).
/// - "+X% к скорости атаки" → ловкость +X*0.2 (1 ловкость = 0.05 ск. атаки; база 1 удар/сек — "+10%" = +0.1 ск. атаки
///   = 2 ловкости). Излишек выше капа 4.0 сам уходит в шанс крита механизмом HeroStats (как в диздоке).
/// - "+X% к скорости передвижения" → новый ПРОЦЕНТНЫЙ канал EquipmentSystem (moveSpeedPercent): финальная
///   скорость агента = (база + плоские бонусы) * (1 + сумма процентов).
/// - flat HP/мана → готовые каналы bonusHP/bonusMana.
///
/// ТРИГГЕРНЫЕ ЭФФЕКТЫ И АУРЫ (каждый N-й удар, вампиризм, турели, ауры и т.п.) — ЗАДЕЛ НА БУДУЩЕЕ:
/// хранятся данными (effectNotes + поля aura*/crit*/dodge/vampire/cdr/resist), пока НЕ применяются.
/// </summary>
public static class EquipmentDatabase
{
    // ==================== ТИРЫ И ЦВЕТА ====================

    public enum Tier
    {
        Tier1 = 1,
        Tier2 = 2,
        Tier3 = 3,
        Tier4 = 4
    }

    /// <summary>Цвет "сундучка" на полу по тиру (из диздока): тир 1 — обычный (не красится), 2 — жёлтый, 3 — оранжевый, 4 — красный.</summary>
    public static Color GetChestColor(Tier tier)
    {
        switch (tier)
        {
            case Tier.Tier2: return new Color(1f, 0.92f, 0.20f);    // жёлтый
            case Tier.Tier3: return new Color(1f, 0.55f, 0.05f);    // оранжевый
            case Tier.Tier4: return new Color(0.85f, 0.10f, 0.10f); // красный
            default: return new Color(0.55f, 0.40f, 0.22f);         // тир 1 — "деревянная" моделка
        }
    }

    /// <summary>Цвет подсветки/рамки предмета в UI по тиру (совпадает с цветом сундучка).</summary>
    public static Color GetTierColor(Tier tier) { return GetChestColor(tier); }

    // ==================== ПРЕДМЕТ ====================

    [System.Serializable]
    public class EquipmentDef
    {
        public int id;
        public string displayName;
        public Tier tier;

        // Иконка — спрайт назначается в инспекторе/скриптами. null = БЕЛАЯ иконка (правило ТЗ).
        public Sprite icon;

        // ---- Прямые бонусы к атрибутам ----
        public float strength;
        public float vitality;
        public float endurance;
        public float agility;
        public float wisdom;
        public float willpower;

        // ---- Плоские бонусы поверх атрибутов ----
        public float bonusHP;         // добавка к максимуму здоровья
        public float bonusMana;       // добавка к максимуму маны
        public float moveSpeedBonus;  // плоская добавка к NavMeshAgent.speed героя

        // ---- Данные о "переведённых" статах (для подсказки/логов) ----
        public float flatAttack;          // "+X к атаке" (в дельту уже ушло strength = flatAttack/5)
        public float defenseFlat;         // "+X к защите" (в дельту ушло endurance = defenseFlat*10)
        public float hpRegenFlat;         // "+X HP/сек" (в дельту ушло endurance = hpRegenFlat*2)
        public float manaRegenFlat;       // "+X маны/сек" (в дельту ушло willpower = manaRegenFlat*2)
        public float moveSpeedPercent;    // "+X% к скорости передвижения" — РАБОТАЕТ (процентный канал EquipmentSystem)
        public float attackSpeedPercent;  // "+X% к скорости атаки" (в дельту ушло agility = attackSpeedPercent*0.2)

        // ---- ЗАДЕЛ НА БУДУЩЕЕ (данные, пока НЕ применяются) ----
        public float critChanceBonus;     // % шанс крита (пока крит растёт только от перебора капа ск. атаки)
        public float critDamageBonus;     // % к урону крита
        public float dodgePercent;        // % уклонения
        public float vampirePercent;      // % вампиризма
        public float cdrPercent;          // % уменьшения перезарядок
        public float resistCCPercent;     // % сопротивления замедлению/оглушению
        public List<string> effectNotes = new List<string>(); // тексты триггерных эффектов (каждый N-й удар, блоки, молнии...)

        // ---- Ауры — ЗАДЕЛ (радиусы/бонусы хранятся, системы аур ещё нет) ----
        public float auraRadius;            // радиус ауры (0 = нет ауры)
        public float auraAttackSpeed;       // % скорости атаки союзникам
        public float auraMoveSpeed;         // % скорости бега союзникам
        public float auraAttackFlat;        // плоская атака союзникам
        public float auraDefense;           // защита союзникам
        public float auraHPRegen;           // реген HP союзникам
        public float auraDamagePerSecond;   // урон/сек врагам вокруг носителя (Жаровня)
        public float auraEnemyDefense;      // −защита врагов в радиусе
        public float auraEnemySlowPct;      // −% скорости врагов в радиусе
        public float auraDamageAmpPct;      // +% получаемого врагами урона

        public override string ToString() { return displayName; }

        /// <summary>Короткая строка бонусов для подсказки/лога (в терминах диздока).</summary>
        public string BonusesText()
        {
            var parts = new List<string>();
            if (flatAttack > 0) parts.Add("+" + flatAttack + " атака");
            if (strength > 0.01f && flatAttack <= 0) parts.Add("+" + strength + " сила");
            if (vitality > 0) parts.Add("+" + vitality + " живучесть");
            if (endurance > 0.01f && defenseFlat <= 0 && hpRegenFlat <= 0) parts.Add("+" + endurance + " выносливость");
            if (agility > 0.01f && attackSpeedPercent <= 0) parts.Add("+" + agility + " ловкость");
            if (wisdom > 0) parts.Add("+" + wisdom + " мудрость");
            if (willpower > 0.01f && manaRegenFlat <= 0) parts.Add("+" + willpower + " сила воли");
            if (bonusHP > 0) parts.Add("+" + bonusHP + " HP");
            if (bonusMana > 0) parts.Add("+" + bonusMana + " мана");
            if (defenseFlat > 0) parts.Add("+" + defenseFlat + " защита");
            if (hpRegenFlat > 0) parts.Add("+" + hpRegenFlat + " HP/сек");
            if (manaRegenFlat > 0) parts.Add("+" + manaRegenFlat + " маны/сек");
            if (attackSpeedPercent > 0) parts.Add("+" + attackSpeedPercent + "% ск. атаки");
            if (moveSpeedPercent > 0) parts.Add("+" + moveSpeedPercent + "% ск. передв.");
            if (moveSpeedBonus > 0) parts.Add("+" + moveSpeedBonus.ToString("0.0") + " ск. движ.");
            if (critChanceBonus > 0) parts.Add("+" + critChanceBonus + "% крит (задел)");
            if (critDamageBonus > 0) parts.Add("+" + critDamageBonus + "% крит урон (задел)");
            if (dodgePercent > 0) parts.Add("+" + dodgePercent + "% уклонение (задел)");
            if (vampirePercent > 0) parts.Add("+" + vampirePercent + "% вампиризм (задел)");
            if (cdrPercent > 0) parts.Add("+" + cdrPercent + "% КД (задел)");
            if (resistCCPercent > 0) parts.Add("+" + resistCCPercent + "% сопрот. контролю (задел)");
            if (auraRadius > 0)
            {
                parts.Add("АУРА R" + auraRadius);
                if (auraAttackSpeed > 0) parts.Add("союзникам +" + auraAttackSpeed + "% ск. атаки");
                if (auraMoveSpeed > 0) parts.Add("союзникам +" + auraMoveSpeed + "% ск. бега");
                if (auraAttackFlat > 0) parts.Add("союзникам +" + auraAttackFlat + " атака");
                if (auraDefense > 0) parts.Add("союзникам +" + auraDefense + " защита");
                if (auraHPRegen > 0) parts.Add("союзникам +" + auraHPRegen + " HP/сек");
                if (auraDamagePerSecond > 0) parts.Add("врагам " + auraDamagePerSecond + " урона/сек");
                if (auraEnemyDefense > 0) parts.Add("врагам −" + auraEnemyDefense + " защита");
                if (auraEnemySlowPct > 0) parts.Add("врагам −" + auraEnemySlowPct + "% скорость");
                if (auraDamageAmpPct > 0) parts.Add("врагам +" + auraDamageAmpPct + "% получаемого урона");
            }
            foreach (var note in effectNotes) parts.Add("Эффект (задел): " + note);
            return parts.Count > 0 ? string.Join(", ", parts) : "нет бонусов";
        }
    }

    // ==================== СПИСОК ПРЕДМЕТОВ (80 штук по диздоку) ====================

    private static List<EquipmentDef> all;

    /// <summary>Все предметы базы (80 штук: 4 тира × 20).</summary>
    public static List<EquipmentDef> All
    {
        get
        {
            if (all == null) Build();
            return all;
        }
    }

    /// <summary>Пустой предмет с id/тиром/именем (id = (тир-1)*100 + номер в тире).</summary>
    private static EquipmentDef Mk(int tierInt, int idx, string name)
    {
        return new EquipmentDef
        {
            id = (tierInt - 1) * 100 + idx,
            tier = (Tier)tierInt,
            displayName = name
        };
    }

    /// <summary>Плоская атака/защита/регены/% скорости атаки переводятся в атрибуты (HeroStats не правим). Вызывать ПОСЛЕ наполнения.</summary>
    private static void Finish(EquipmentDef d)
    {
        if (d.flatAttack > 0) d.strength += d.flatAttack / 5f;      // 1 сила = 5 атаки
        if (d.defenseFlat > 0) d.endurance += d.defenseFlat * 10f;  // 1 выносливость = 0.1 защиты
        if (d.hpRegenFlat > 0) d.endurance += d.hpRegenFlat * 2f;   // 1 выносливость = 0.5 HP/сек
        if (d.manaRegenFlat > 0) d.willpower += d.manaRegenFlat * 2f; // 1 сила воли = 0.5 маны/сек
        if (d.attackSpeedPercent > 0) d.agility += d.attackSpeedPercent * 0.2f; // 1 ловкость = 0.05 ск. атаки
    }

    private static void Build()
    {
        all = new List<EquipmentDef>();

        // ============================================================
        // ТИР 1 — «Обычный сундук»: базовые статы и расходники для слияния в Тир 2
        // ============================================================
        var d = Mk(1, 0, "Железный меч");
        d.flatAttack = 10f; Finish(d); all.Add(d);

        d = Mk(1, 1, "Железные перчатки"); d.strength = 10f; all.Add(d);

        d = Mk(1, 2, "Железная броня"); d.vitality = 4f; all.Add(d);

        d = Mk(1, 3, "Железные поножи"); d.endurance = 4f; all.Add(d);

        d = Mk(1, 4, "Железный щит"); d.defenseFlat = 1f; Finish(d); all.Add(d);

        d = Mk(1, 5, "Железные наручи"); d.bonusHP = 150f; all.Add(d);

        d = Mk(1, 6, "Лук охотника"); d.flatAttack = 10f; Finish(d); all.Add(d);

        d = Mk(1, 7, "Кожаные перчатки"); d.agility = 10f; all.Add(d);

        d = Mk(1, 8, "Кожаная броня"); d.hpRegenFlat = 1f; Finish(d); all.Add(d);

        d = Mk(1, 9, "Кожаные ботинки"); d.moveSpeedPercent = 10f; all.Add(d);

        d = Mk(1, 10, "Обычные стрелы"); d.flatAttack = 10f; Finish(d); all.Add(d);

        d = Mk(1, 11, "Кожаные наручи"); d.vitality = 4f; all.Add(d);

        d = Mk(1, 12, "Тканевый капюшон"); d.wisdom = 4f; all.Add(d);

        d = Mk(1, 13, "Тканевая роба"); d.willpower = 4f; all.Add(d);

        d = Mk(1, 14, "Тканевые перчатки"); d.bonusMana = 80f; all.Add(d);

        d = Mk(1, 15, "Тканевые ботинки"); d.manaRegenFlat = 1f; Finish(d); all.Add(d);

        d = Mk(1, 16, "Медное кольцо"); d.attackSpeedPercent = 5f; Finish(d); all.Add(d);

        d = Mk(1, 17, "Пояс наёмника");
        d.strength = 2f; d.agility = 2f; d.vitality = 2f; all.Add(d);

        d = Mk(1, 18, "Треснувший амулет");
        d.bonusHP = 60f; d.bonusMana = 40f; all.Add(d);

        d = Mk(1, 19, "Кремнёвый пистоль");
        d.flatAttack = 8f; d.critChanceBonus = 3f; Finish(d); all.Add(d);

        // ============================================================
        // ТИР 2 — «Сундук жёлтого цвета»: полезные эффекты, первые ауры и пассивки
        // ============================================================
        d = Mk(2, 0, "Закалённый клинок");
        d.flatAttack = 25f; d.effectNotes.Add("каждый 4-й удар наносит +15 доп. физического урона"); Finish(d); all.Add(d);

        d = Mk(2, 1, "Тяжёлые латные рукавицы");
        d.strength = 22f; d.defenseFlat = 1f; Finish(d); all.Add(d);

        d = Mk(2, 2, "Кираса пехотинца");
        d.vitality = 10f; d.bonusHP = 250f; all.Add(d);

        d = Mk(2, 3, "Поножи из воронёной стали");
        d.endurance = 10f; d.moveSpeedPercent = 10f; all.Add(d);

        d = Mk(2, 4, "Башенный щит");
        d.defenseFlat = 3f; d.effectNotes.Add("блокирует 10 ед. входящего урона от каждой атаки"); Finish(d); all.Add(d);

        d = Mk(2, 5, "Наручи берсерка");
        d.bonusHP = 350f; d.attackSpeedPercent = 10f; Finish(d); all.Add(d);

        d = Mk(2, 6, "Композитный лук");
        d.flatAttack = 25f; d.attackSpeedPercent = 8f; Finish(d); all.Add(d);

        d = Mk(2, 7, "Перчатки следопыта");
        d.agility = 22f; d.dodgePercent = 5f; all.Add(d);

        d = Mk(2, 8, "Бригантина регенерации");
        d.hpRegenFlat = 3f; d.vitality = 6f; Finish(d); all.Add(d);

        d = Mk(2, 9, "Сапоги неутомимого");
        d.moveSpeedPercent = 18f; d.endurance = 5f; all.Add(d);

        d = Mk(2, 10, "Зазубренные стрелы");
        d.flatAttack = 20f; d.effectNotes.Add("атаки вызывают кровотечение (5 урона в сек. на 3 сек.)"); Finish(d); all.Add(d);

        d = Mk(2, 11, "Наручи дуэлянта");
        d.vitality = 10f; d.agility = 12f; all.Add(d);

        d = Mk(2, 12, "Клобук послушника");
        d.wisdom = 12f; d.manaRegenFlat = 2f; Finish(d); all.Add(d);

        d = Mk(2, 13, "Мантия концентрации");
        d.willpower = 12f; d.bonusMana = 150f; all.Add(d);

        d = Mk(2, 14, "Перчатки искр");
        d.bonusMana = 180f; d.effectNotes.Add("каждая 5-я атака выпускает цепную молнию в 2 цели (20 урона)"); all.Add(d);

        d = Mk(2, 15, "Сапоги эфира");
        d.manaRegenFlat = 3f; d.moveSpeedPercent = 12f; Finish(d); all.Add(d);

        d = Mk(2, 16, "Барабан командира");
        d.auraRadius = 600f; d.auraAttackSpeed = 10f; d.auraMoveSpeed = 10f; all.Add(d);

        d = Mk(2, 17, "Жаровня с углями");
        d.auraRadius = 300f; d.auraDamagePerSecond = 8f; all.Add(d);

        d = Mk(2, 18, "Ручной мушкет");
        d.flatAttack = 30f; d.effectNotes.Add("раз в 4 секунды автоматически стреляет во врага с наименьшим здоровьем (45 урона)"); Finish(d); all.Add(d);

        d = Mk(2, 19, "Знамя ополчения");
        d.auraRadius = 600f; d.auraDefense = 2f; d.auraHPRegen = 2f; all.Add(d);

        // ============================================================
        // ТИР 3 — «Сундук оранжевого цвета»: боевые триггеры, контроль толпы, сильные ауры
        // (предметы заточены под плотные волны по 300–500 юнитов)
        // ============================================================
        d = Mk(3, 0, "Меч рассечения");
        d.flatAttack = 55f; d.effectNotes.Add("атаки бьют по дуге перед героем, задевая до 3 врагов (50% урона)"); Finish(d); all.Add(d);

        d = Mk(3, 1, "Рукавицы титана");
        d.strength = 45f; d.critDamageBonus = 15f; all.Add(d);

        d = Mk(3, 2, "Панцирь гвардейца");
        d.vitality = 24f; d.bonusHP = 600f; d.defenseFlat = 2f; Finish(d); all.Add(d);

        d = Mk(3, 3, "Латные наголенники");
        d.endurance = 24f; d.resistCCPercent = 15f; all.Add(d);

        d = Mk(3, 4, "Осадный щит");
        d.defenseFlat = 6f; d.effectNotes.Add("при получении урона с шансом 15% выпускает ударную волну (40 урона по площади)"); Finish(d); all.Add(d);

        d = Mk(3, 5, "Кровавые наручи");
        d.bonusHP = 800f; d.vampirePercent = 8f; all.Add(d);

        d = Mk(3, 6, "Арбалет инженера");
        d.flatAttack = 60f; d.effectNotes.Add("каждый 3-й выстрел пробивает всех врагов на линии"); Finish(d); all.Add(d);

        d = Mk(3, 7, "Перчатки фантома");
        d.agility = 45f; d.attackSpeedPercent = 20f; Finish(d); all.Add(d);

        d = Mk(3, 8, "Доспех тролля");
        d.hpRegenFlat = 12f; d.vitality = 15f; Finish(d); all.Add(d);

        d = Mk(3, 9, "Сапоги ветра");
        d.moveSpeedPercent = 25f; d.dodgePercent = 12f; all.Add(d);

        d = Mk(3, 10, "Разрывные болты");
        d.flatAttack = 45f; d.effectNotes.Add("попадания взрываются в радиусе 150, нанося 25 ед. урона"); Finish(d); all.Add(d);

        d = Mk(3, 11, "Наручи парирования");
        d.vitality = 20f; d.agility = 25f; d.defenseFlat = 3f; Finish(d); all.Add(d);

        d = Mk(3, 12, "Диадема провидца");
        d.wisdom = 28f; d.manaRegenFlat = 5f; Finish(d); all.Add(d);

        d = Mk(3, 13, "Роба архимага");
        d.willpower = 28f; d.bonusMana = 400f; d.cdrPercent = 10f; all.Add(d);

        d = Mk(3, 14, "Перчатки грозового фронта");
        d.bonusMana = 350f; d.effectNotes.Add("атаки с шансом 20% бьют молнией по 4 врагам (55 урона)"); all.Add(d);

        d = Mk(3, 15, "Ботфорты пустоты");
        d.manaRegenFlat = 7f; d.moveSpeedPercent = 20f; Finish(d); all.Add(d);

        d = Mk(3, 16, "Генератор статического поля");
        d.effectNotes.Add("каждые 3 секунды бьёт током 5 случайных врагов в радиусе 500 (50 урона и замедление на 20%)"); all.Add(d);

        d = Mk(3, 17, "Алхимический распылитель");
        d.auraRadius = 500f; d.auraEnemyDefense = 3f; d.auraEnemySlowPct = 15f; all.Add(d);

        d = Mk(3, 18, "Автоматическая турель наплечная");
        d.effectNotes.Add("каждые 1.5 секунды делает выстрел по ближайшему врагу (65 физического урона)"); all.Add(d);

        d = Mk(3, 19, "Горн ярости");
        d.auraRadius = 700f; d.auraAttackFlat = 25f; d.auraAttackSpeed = 15f; all.Add(d);

        // ============================================================
        // ТИР 4 — «Сундук красного цвета»: предтоповый уровень, мощные синергии, массовый урон
        // ============================================================
        d = Mk(4, 0, "Клинок палача");
        d.flatAttack = 110f; d.critChanceBonus = 15f;
        d.effectNotes.Add("мгновенно добивает обычных мобов, если у них осталось меньше 15% здоровья"); Finish(d); all.Add(d);

        d = Mk(4, 1, "Кулаки разрушителя");
        d.strength = 90f; d.effectNotes.Add("каждая атака снижает защиту цели на 1 ед. (складывается до 10 раз)"); all.Add(d);

        d = Mk(4, 2, "Броня колосса");
        d.vitality = 50f; d.bonusHP = 1400f; d.defenseFlat = 5f; Finish(d); all.Add(d);

        d = Mk(4, 3, "Поножи несокрушимого");
        d.endurance = 50f; d.hpRegenFlat = 10f; Finish(d); all.Add(d);

        d = Mk(4, 4, "Эгида бастиона");
        d.defenseFlat = 12f; d.effectNotes.Add("отражает 25% полученного физического урона обратно атакующему"); Finish(d); all.Add(d);

        d = Mk(4, 5, "Наручи бессмертия");
        d.bonusHP = 1800f; d.effectNotes.Add("если здоровье падает ниже 30%, регенерация здоровья +300% на 5 сек. (перезарядка 30 сек.)"); all.Add(d);

        d = Mk(4, 6, "Скорострельный карабин");
        d.flatAttack = 120f; d.attackSpeedPercent = 35f; Finish(d); all.Add(d);

        d = Mk(4, 7, "Перчатки молниеносного клинка");
        d.agility = 90f; d.attackSpeedPercent = 30f; Finish(d); all.Add(d);

        d = Mk(4, 8, "Панцирь живой плоти");
        d.hpRegenFlat = 25f; d.vitality = 35f; Finish(d); all.Add(d);

        d = Mk(4, 9, "Сапоги неуловимого");
        d.moveSpeedPercent = 35f; d.dodgePercent = 20f; d.agility = 30f; all.Add(d);

        d = Mk(4, 10, "Вольфрамовые снаряды");
        d.flatAttack = 95f; d.effectNotes.Add("игнорируют 30% защиты цели"); Finish(d); all.Add(d);

        d = Mk(4, 11, "Наручи мастера боя");
        d.vitality = 40f; d.agility = 45f; d.vampirePercent = 15f; all.Add(d);

        d = Mk(4, 12, "Корона рассудка");
        d.wisdom = 60f; d.manaRegenFlat = 12f; d.effectNotes.Add("+15% к урону от способностей и проков"); Finish(d); all.Add(d);

        d = Mk(4, 13, "Одеяние звёздного света");
        d.willpower = 60f; d.bonusMana = 900f; d.cdrPercent = 20f; all.Add(d);

        d = Mk(4, 14, "Перчатки аннигиляции");
        d.bonusMana = 700f; d.effectNotes.Add("каждая 4-я атака вызывает взрыв в точке удара (140 магического урона в радиусе 250)"); all.Add(d);

        d = Mk(4, 15, "Сапоги пространственного сдвига");
        d.manaRegenFlat = 15f; d.moveSpeedPercent = 30f; Finish(d); all.Add(d);

        d = Mk(4, 16, "Тесла-катушка за спиной");
        d.effectNotes.Add("каждую секунду выпускает 3 шаровые молнии, летящие по прямой и наносящие 90 урона всем на пути"); all.Add(d);

        d = Mk(4, 17, "Монолит подавления");
        d.auraRadius = 600f; d.auraDamageAmpPct = 20f; d.auraEnemyDefense = 5f; all.Add(d);

        d = Mk(4, 18, "Сдвоенный авто-пулемёт");
        d.effectNotes.Add("делает 2 выстрела в секунду по ближайшим целям (по 85 урона каждый, наследует эффекты при попадании)"); all.Add(d);

        d = Mk(4, 19, "Штандарт Империи");
        d.auraRadius = 800f; d.auraAttackFlat = 50f; d.auraAttackSpeed = 25f; d.auraDefense = 6f; all.Add(d);
    }

    // ==================== ПОИСК ====================

    /// <summary>Случайный предмет тира.</summary>
    public static EquipmentDef GetRandom(Tier tier)
    {
        var list = All.FindAll(x => x.tier == tier);
        if (list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    /// <summary>Предметы тира.</summary>
    public static List<EquipmentDef> GetByTier(Tier tier) { return All.FindAll(x => x.tier == tier); }

    /// <summary>Найти предмет по имени (удобно для тестов в контекстном меню).</summary>
    public static EquipmentDef Find(string name) { return All.Find(x => x.displayName == name); }
}
