using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ЕДИНЫЙ скрипт всей базы снаряжения (по ТЗ: "Все иконки снаряжений и снаряжения находятся в одном скрипте").
///
/// Правила из диздока "Снаряжение1":
/// - 6 слотов обычного снаряжения, слот уникального снаряжения и слот уникального артефакта (фиксированы за героем).
/// - Тиры: 1, 2, 3, 4 (в этой базе по 20 предметов на тир; тир 5, "+", сетовые и секретные — задел на будущее).
/// - Предметы — ЧИСТО ИКОНКИ, 3D-моделей не будет. Нет картинки — белая иконка.
/// - Предмет на полу выглядит как сундучок и красится как сундуки по тиру:
///     тир 1 — обычная (не крашится), тир 2 — жёлтый, тир 3 — оранжевый, тир 4 — красный.
///
/// Бонусы сделаны ТОЛЬКО через атрибуты (сила/живучесть/выносливость/ловкость/мудрость/сила воли) и
/// готовые публичные поля HeroStats (maxHP, maxMana) + NavMeshAgent.speed — HeroStats НЕ трогаем:
///   1 сила = 5 урона, 1 живучесть = 25 HP, 1 выносливость = 0.5 реген HP + 0.1 защиты,
///   1 ловкость / 1 сила воли = +0.05 скорости атаки, 1 мудрость = 15 маны, 1 сила воли = 0.5 реген маны.
/// (aura* — данные для будущих аур, пока не применяются.)
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

    /// <summary>Цвет "сундучка" на полу в зависимости от тира (из диздока): 1 — обычная моделька (не крашится), 2 — жёлтый, 3 — оранжевый, 4 — красный.</summary>
    public static Color GetChestColor(Tier tier)
    {
        switch (tier)
        {
            case Tier.Tier2: return new Color(1f, 0.92f, 0.20f);   // жёлтый
            case Tier.Tier3: return new Color(1f, 0.55f, 0.05f);   // оранжевый
            case Tier.Tier4: return new Color(0.85f, 0.10f, 0.10f); // красный
            default: return new Color(0.55f, 0.40f, 0.22f);          // тир 1 — обычная "деревянная" моделька
        }
    }

    /// <summary>Цвет подсветки/рамки предмета в UI по тиру (совпадает с цветом сундучка на полу).</summary>
    public static Color GetTierColor(Tier tier) { return GetChestColor(tier); }

    // ==================== ПРЕДМЕТ ====================

    public class EquipmentDef
    {
        public int id;
        public string displayName;
        public Tier tier;

        // Иконка — спрайт назначается в инспекторе/скриптами. null = БЕЛАЯ иконка (правило ТЗ).
        public Sprite icon;

        // Бонусы к атрибутам (формулы HeroStats сами превращают их в урон/HP/регены/скорость атаки)
        public float strength;
        public float vitality;
        public float endurance;
        public float agility;
        public float wisdom;
        public float willpower;

        // Плоские бонусы поверх атрибутов
        public float bonusHP;         // добавка к максимуму здоровья
        public float bonusMana;       // добавка к максимуму маны
        public float moveSpeedBonus;  // добавка к NavMeshAgent.speed героя

        // Ауры — данные задел на будущее (пока не применяются, держим в уме)
        public float auraRadius;          // радиус ауры (0 = нет ауры)
        public float auraAttackSpeed;     // % скорости атаки союзникам
        public float auraMoveSpeed;       // % скорости передвижения союзникам

        public override string ToString() { return displayName; }

        /// <summary>Короткая строка бонусов для подсказки/лога (собирается из формул HeroStats).</summary>
        public string BonusesText()
        {
            var parts = new List<string>();
            if (strength > 0) parts.Add("+" + strength + " сила");
            if (vitality > 0) parts.Add("+" + vitality + " живучесть");
            if (endurance > 0) parts.Add("+" + endurance + " выносливость");
            if (agility > 0) parts.Add("+" + agility + " ловкость");
            if (wisdom > 0) parts.Add("+" + wisdom + " мудрость");
            if (willpower > 0) parts.Add("+" + willpower + " сила воли");
            if (bonusHP > 0) parts.Add("+" + bonusHP + " HP");
            if (bonusMana > 0) parts.Add("+" + bonusMana + " мана");
            if (moveSpeedBonus > 0) parts.Add("+" + moveSpeedBonus.ToString("0.0") + " скор. движ.");
            if (auraRadius > 0) parts.Add("аура R" + auraRadius);
            return parts.Count > 0 ? string.Join(", ", parts) : "нет бонусов";
        }
    }

    // ==================== СПИСОК ПРЕДМЕТОВ ====================

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

    private static void Build()
    {
        all = new List<EquipmentDef>();

        // 20 архетипов на тир: имя + профиль бонусов (множитель тира усиливает числа).
        // Профили подобраны под фантазию ТЗ: атрибуты, HP, мана, скорость движения, будущие ауры.
        string[] names1 = {
            "Ржавый меч", "Деревянный щит", "Тряпичный шлем", "Стёганая куртка", "Лапти рекрута",
            "Медное кольцо", "Верёвочный пояс", "Каменный амулет", "Боевой нож", "Плетёный наруч",
            "Учебный лук", "Оловянный медальон", "Спортивные сапоги", "Простой оберег", "Бронзовый браслет",
            "Копьё ополчения", "Кожаный капюшон", "Фляга странника", "Глиняная фляга", "Счётные чётки" };

        string[] names2 = {
            "Стальной меч", "Обитый железом щит", "Шлем гарнизона", "Кольчуга новичка", "Сапоги рейнджера",
            "Серебряное кольцо", "Кожаный пояс", "Амулет рассвета", "Кинжал скаута", "Стальные наручи",
            "Охотничий лук", "Медальон дозора", "Сапоги скорости", "Оберег стойкости", "Железный браслет",
            "Копьё ветерана", "Стальной шлем-капюшон", "Фляга ветерана", "Кристальная искра", "Чётки мудреца" };

        string[] names3 = {
            "Клинок полководца", "Башенный щит", "Шлем легиона", "Пластинчатый доспех", "Сапоги стремительных",
            "Кольцо империи", "Пояс героя", "Амулет бури", "Стилет теней", "Рукояти силы",
            "Дальний лук", "Медальон видящего", "Плащ скорости", "Талисман крови", "Платиновый браслет",
            "Копьё чемпиона", "Шлем короны", "Фляга твердеющей силы", "Осколок зари", "Чётки первосвященника" };

        string[] names4 = {
            "Благословенный клинок", "Щит драконьей чешуи", "Венец непобедимости", "Доспех титана", "Сапоги ветра",
            "Кольцо вечности", "Королевский пояс", "Амулет феникса", "Лезвие безымянных", "Наручи голема",
            "Небесный лук", "Медальон предсказателя", "Сапоги юного зевса", "Сердце гиганта", "Мифриловый браслет",
            "Копьё судного дня", "Шлем грозовых царей", "Фляга бессмертия", "Звезда полуночи", "Чётки короля Андрона" };

        // Профили, сгенерированные скриптово: каждому архетипу — свои статы (индексы 0..19 по именам выше)
        for (int tierInt = 1; tierInt <= 4; tierInt++)
        {
            Tier tier = (Tier)tierInt;
            float k = tierInt;                     // множитель тира
            float hpK = tierInt * 10f;             // плоские HP растут заметно
            float msK = 0.1f * tierInt;            // бонус скорости движения
            string[] names = tierInt == 1 ? names1 : tierInt == 2 ? names2 : tierInt == 3 ? names3 : names4;

            for (int i = 0; i < 20; i++)
            {
                var d = new EquipmentDef();
                d.id = (tierInt - 1) * 100 + i;
                d.tier = tier;
                d.displayName = names[i];

                switch (i)
                {
                    case 0:  // меч — сила
                        d.strength = 2f * k; break;
                    case 1:  // щит — живучесть + выносливость
                        d.vitality = 1.5f * k; d.endurance = 1.5f * k; break;
                    case 2:  // шлем — живучесть
                        d.vitality = 2.5f * k; break;
                    case 3:  // броня — выносливость
                        d.endurance = 3f * k; break;
                    case 4:  // сапоги — ловкость + скорость движения
                        d.agility = 1.5f * k; d.moveSpeedBonus = 0.5f * msK; break;
                    case 5:  // кольцо — мудрость
                        d.wisdom = 1.5f * k; break;
                    case 6:  // пояс — живучесть + выносливость
                        d.vitality = 1f * k; d.endurance = 1f * k; break;
                    case 7:  // амулет — сила воли
                        d.willpower = 2f * k; break;
                    case 8:  // кинжал — сила + ловкость
                        d.strength = 1f * k; d.agility = 1.5f * k; break;
                    case 9:  // наручи — выносливость + ловкость
                        d.endurance = 1.5f * k; d.agility = 1f * k; break;
                    case 10: // лук — ловкость + сила воли
                        d.agility = 1.5f * k; d.willpower = 1f * k; break;
                    case 11: // медальон — мудрость + сила воли
                        d.wisdom = 1f * k; d.willpower = 1f * k; break;
                    case 12: // сапоги скорости — скорость движения + ловкость
                        d.moveSpeedBonus = msK; d.agility = 0.5f * k; break;
                    case 13: // оберег — живучесть + плоские HP
                        d.vitality = 1f * k; d.bonusHP = hpK; break;
                    case 14: // браслет — выносливость + мудрость
                        d.endurance = 1f * k; d.wisdom = 0.5f * k; break;
                    case 15: // копьё — сила + выносливость
                        d.strength = 1.5f * k; d.endurance = 1f * k; break;
                    case 16: // шлем короны — живучесть + мудрость
                        d.vitality = 1.5f * k; d.wisdom = 1f * k; break;
                    case 17: // фляга — регены (через выносливость и силу воли) + плоская мана
                        d.endurance = 0.5f * k; d.willpower = 0.5f * k; d.bonusMana = 15f * k; break;
                    case 18: // аура — скорость атаки союзникам (заготовка ауры)
                        d.strength = 1f * k; d.auraRadius = 8f + 2f * k; d.auraAttackSpeed = 5f * k; break;
                    case 19: // аура — скорость передвижения союзникам (заготовка ауры)
                        d.agility = 1f * k; d.auraRadius = 8f + 2f * k; d.auraMoveSpeed = 5f * k; break;
                }

                all.Add(d);
            }
        }
    }

    /// <summary>Случайный предмет тира. savedIcon — можно подставить общий спрайт-иконку (null = белая иконка).</summary>
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
