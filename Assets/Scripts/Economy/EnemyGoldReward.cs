using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(UnitStats))]
public class EnemyGoldReward : MonoBehaviour
{
    [Header("Награда за убийство")]
    [Tooltip("Сколько золота получит герой, если сам добьёт этого врага")]
    public int goldReward = 15;

    private UnitStats myStats;
    private bool rewardProcessed = false;

    // Ссылки для определения, что убил именно Герой
    private static HeroStats cachedHero;
    private static BattleAI cachedHeroAI;
    private static FieldInfo targetFieldInfo;

    void Start()
    {
        myStats = GetComponent<UnitStats>();

        // Один раз находим Героя на сцене для всех мобов (чтобы не грузить FPS в волнах)
        if (cachedHero == null)
        {
            cachedHero = FindAnyObjectByType(typeof(HeroStats)) as HeroStats;
            if (cachedHero != null)
            {
                cachedHeroAI = cachedHero.GetComponent(typeof(BattleAI)) as BattleAI;
                if (cachedHeroAI != null)
                {
                    targetFieldInfo = typeof(BattleAI).GetField("currentTarget", BindingFlags.NonPublic | BindingFlags.Instance);
                }
            }
        }
    }

    void Update()
    {
        if (rewardProcessed || myStats == null) return;

        // Как только моб погиб — проверяем, добил ли его Герой
        if (myStats.isDead || myStats.currentHP.CompareTo(0f) != 1)
        {
            rewardProcessed = true;

            if (WasKilledByHero())
            {
                if (GoldManager.Instance != null)
                {
                    GoldManager.Instance.AddGold(goldReward);
                    Debug.Log($"<color=yellow>+{goldReward} золота</color> за убийство {gameObject.name}");
                }
            }
        }
    }

    private bool WasKilledByHero()
    {
        if (cachedHero == null || cachedHero.isDead) return false;

        // 1. Проверяем, был ли этот моб текущей целью атаки Героя в BattleAI
        if (cachedHeroAI != null && targetFieldInfo != null)
        {
            Transform heroTarget = targetFieldInfo.GetValue(cachedHeroAI) as Transform;
            if (heroTarget != null && (heroTarget == this.transform || heroTarget.IsChildOf(this.transform)))
            {
                return true;
            }
        }

        // 2. Запасная проверка (на случай, если в этот же кадр BattleAI уже сбросил цель при смерти моба):
        // Герой находится вплотную в радиусе своего удара
        float attackRange = (cachedHeroAI != null) ? cachedHeroAI.attackRange + 0.5f : 2.5f;
        float dist = Vector3.Distance(cachedHero.transform.position, transform.position);

        return dist.CompareTo(attackRange) != 1;
    }
}
