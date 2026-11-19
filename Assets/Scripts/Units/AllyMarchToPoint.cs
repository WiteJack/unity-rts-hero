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
/// Диагностика: если юнит стоит и не может идти, раз в 2 секунды пишет в консоль причину
/// (нет точки, нет NavMeshAgent, юнит вне NavMesh). Отключается в инспекторе (logDebug).
///
/// Доступ к цели BattleAI — через Reflection (поле currentTarget), как во всём проекте.
/// </summary>
public class AllyMarchToPoint : MonoBehaviour
{
    [HideInInspector] public Transform rallyPoint;

    [Tooltip("Дистанция, ближе которой считаем, что точка достигнута")]
    public float stopDistance = 2.5f;

    [Tooltip("Писать в консоль причины, почему юнит стоит и не идёт (раз в 2 сек)")]
    public bool logDebug = true;

    private NavMeshAgent agent;
    private BattleAI battleAI;
    private UnitStats myStats;
    private FieldInfo targetFieldInfo;
    private float nextPathUpdateTime;
    private float nextBlockLogTime;
    private float stuckSeconds;
    private bool loggedFirstPath;

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

        if (rallyPoint == null) { LogBlock("точка (rallyPoint) не назначена — спавнер не передал точку"); return; }
        if (agent == null) { LogBlock("не найден NavMeshAgent на юните/родителе"); return; }
        if (!agent.isOnNavMesh) { LogBlock("юнит НЕ на NavMesh — он заспавнился вне запечённой области. Запеки NavMesh вокруг казармы (Window > AI > Navigation > Bake)"); return; }

        // Занят ли юнит боем прямо сейчас в BattleAI
        Transform currentCombatTarget = null;
        if (battleAI != null && targetFieldInfo != null)
        {
            currentCombatTarget = targetFieldInfo.GetValue(battleAI) as Transform;
        }

        // В бою — марш прерывается, BattleAI сам ведёт бой
        if (currentCombatTarget != null)
        {
            stuckSeconds = 0f;
            return;
        }

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

            // Юнит "залип": пути нет, а стоит на месте, хотя до точки далеко — перебиваем путь заново
            bool noPath = !agent.hasPath || agent.remainingDistance < 0.5f;
            bool standingStill = agent.velocity.sqrMagnitude < 0.01f;
            if (noPath && standingStill) stuckSeconds += 0.5f;
            else stuckSeconds = 0f;

            if (stuckSeconds >= 1.5f)
            {
                stuckSeconds = 0f;
                agent.ResetPath();
                if (logDebug) Debug.LogWarning($"[Марш союзника] {name} застрял на пути к {rallyPoint.name} — пересчитываю путь.");
            }

            agent.SetDestination(rallyPoint.position);

            if (logDebug && !loggedFirstPath)
            {
                loggedFirstPath = true;
                Debug.Log($"[Марш союзника] {name} отправлен к точке {rallyPoint.name} ({rallyPoint.position}).");
            }
        }
    }

    private void LogBlock(string reason)
    {
        if (!logDebug) return;
        if (Time.time < nextBlockLogTime) return;
        nextBlockLogTime = Time.time + 2f;
        Debug.LogWarning($"[Марш союзника] {name} стоит на месте: {reason}");
    }
}
