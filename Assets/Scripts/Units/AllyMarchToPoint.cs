using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Марш союзника к точке казармы. Добавляется автоматически спавнером казармы
/// (BarracksSpawner) к каждому заспавненному войску — вручную ничего вешать не нужно.
///
/// Логика (зеркало EnemyMarchToBase, только для союзников):
/// - Пока BattleAI не занят боем — идём к своей точке (rallyPoint).
/// - Увидел врага в радиусе обзора — дерётся (марш прерывается, BattleAI ведёт бой).
/// - После боя снова идёт к точке.
/// - Дошёл ближе stopDistance — стоит на месте и охраняет точку (дальше бой за BattleAI).
///
/// Доступ к цели BattleAI — через Reflection (поле currentTarget), как во всём проекте.
/// </summary>
public class AllyMarchToPoint : MonoBehaviour
{
    [HideInInspector] public Transform rallyPoint;

    [Tooltip("Дистанция, ближе которой считаем, что точка достигнута")]
    public float stopDistance = 2.5f;

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
        if (rallyPoint == null || agent == null || !agent.isOnNavMesh) return;

        // Занят ли юнит боем прямо сейчас в BattleAI
        Transform currentCombatTarget = null;
        if (battleAI != null && targetFieldInfo != null)
        {
            currentCombatTarget = targetFieldInfo.GetValue(battleAI) as Transform;
        }

        // В бою — марш прерывается, BattleAI сам ведёт бой
        if (currentCombatTarget != null) return;

        // Дистанция по горизонтали (высоту не учитываем)
        Vector3 toPoint = rallyPoint.position - transform.position;
        toPoint.y = 0f;
        if (toPoint.sqrMagnitude <= stopDistance * stopDistance) return; // дошёл — стоит и охраняет

        // Идём к точке (путь обновляем не каждый кадр)
        if (Time.time >= nextPathUpdateTime)
        {
            nextPathUpdateTime = Time.time + 0.5f;
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.SetDestination(rallyPoint.position);
        }
    }
}
