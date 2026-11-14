using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

public class EnemyMarchToBase : MonoBehaviour
{
    [HideInInspector] public Transform baseTarget;

    private NavMeshAgent agent;
    private BattleAI battleAI;
    private UnitStats myStats;
    private FieldInfo targetFieldInfo;
    private float nextPathUpdateTime;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();

        battleAI = GetComponent<BattleAI>();
        myStats = GetComponent<UnitStats>();

        if (battleAI != null)
        {
            targetFieldInfo = typeof(BattleAI).GetField("currentTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }

    void Update()
    {
        if (myStats != null && myStats.isDead) return;
        if (baseTarget == null || agent == null || !agent.isOnNavMesh) return;

        // Проверяем, занят ли враг боем прямо сейчас в BattleAI
        Transform currentCombatTarget = null;
        if (battleAI != null && targetFieldInfo != null)
        {
            currentCombatTarget = targetFieldInfo.GetValue(battleAI) as Transform;
        }

        // Если враг никого не атакует в радиусе обзора — идём на базу
        if (currentCombatTarget == null)
        {
            if (Time.time.CompareTo(nextPathUpdateTime) != -1)
            {
                nextPathUpdateTime = Time.time + 0.5f;
                agent.isStopped = false;
                agent.updateRotation = true;
                agent.SetDestination(baseTarget.position);
            }
        }
    }
}
