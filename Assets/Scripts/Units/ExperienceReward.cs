using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Источник опыта. Вешается на моба/нейтрала (рядом с UnitStats), UnitStats НЕ трогаем.
///
/// Правила (по ТЗ):
/// - Количество опыта задаётся в ИНСПЕКТОРЕ (xpReward) — у каждого типа моба своё.
/// - Опыт даётся ПО ОБЛАСТИ (~20 пунктов дальности) при смерти моба: опыт получают ВСЕ
///   герои (HeroExperience) в радиусе, ВНЕ ЗАВИСИМОСТИ от того, кто именно убил —
///   игрок, союзник или башня.
///
/// Смерть отслеживается через флаг UnitStats.isDead (опрос в Update) — UnitStats не меняем.
/// </summary>
public class ExperienceReward : MonoBehaviour
{
    [Header("Награда опытом")]
    [Tooltip("Сколько опыта этот моб даёт за смерть (настраивается в инспекторе)")]
    public float xpReward = 50f;

    [Tooltip("Радиус области, в которой герои получают опыт за смерть этого моба")]
    public float xpRange = 20f;

    [Tooltip("Слой/маска для поиска героев (0 = искать по всем коллайдерам)")]
    public LayerMask heroMask = ~0;

    private UnitStats myStats;
    private bool granted = false;

    void Awake()
    {
        myStats = GetComponent<UnitStats>();
        if (myStats == null) myStats = GetComponentInParent<UnitStats>();
    }

    void Update()
    {
        if (granted) return;
        if (myStats == null || !myStats.isDead) return;

        granted = true;
        GrantAreaXP();
        enabled = false; // отработали — выключаемся
    }

    /// <summary>Раздаёт опыт всем героям в радиусе xpRange от места смерти моба.</summary>
    private void GrantAreaXP()
    {
        // HashSet — чтобы герой с несколькими коллайдерами не получил опыт дважды
        HashSet<HeroExperience> rewardedHeroes = new HashSet<HeroExperience>();

        Collider[] hits = Physics.OverlapSphere(transform.position, xpRange, heroMask);
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            // HeroExperience висит на корне героя, коллайдер может быть на ребёнке
            HeroExperience hero = hit.GetComponentInParent<HeroExperience>();
            if (hero != null && rewardedHeroes.Add(hero))
            {
                hero.GainExperience(xpReward);
            }
        }
    }

    // Визуализация радиуса в редакторе — удобно настраивать xpRange в инспекторе
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, xpRange);
    }
}
