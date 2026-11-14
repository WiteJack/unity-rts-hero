using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BattleAI))]
public class CombatRangeStopper : MonoBehaviour
{
    [Tooltip("Запас внутрь attackRange из BattleAI (0.05 = тормозит чуть ближе, чтобы BattleAI гарантированно начал удар)")]
    public float stopInsideMargin = 0.05f;

    private BattleAI battleAI;
    private NavMeshAgent agent;
    private HeroMovement heroMovement;
    private int myLayer;

    void Start()
    {
        battleAI = GetComponent<BattleAI>();
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();

        heroMovement = GetComponent<HeroMovement>();
        if (heroMovement == null) heroMovement = GetComponentInParent<HeroMovement>();

        myLayer = gameObject.layer;

        if (agent != null)
        {
            agent.stoppingDistance = 0f;
            agent.autoBraking = true;
        }
    }

    void LateUpdate()
    {
        if (battleAI == null || agent == null || !agent.isOnNavMesh) return;

        // Если игрок приказал герою бежать — восстанавливаем маршрут, даже если BattleAI успел сделать ResetPath()!
        if (heroMovement != null && heroMovement.isMovingByOrder)
        {
            agent.isStopped = false;
            agent.updateRotation = true;
            if (!agent.hasPath && !agent.pathPending)
            {
                agent.SetDestination(heroMovement.lastOrderPoint);
            }
            return;
        }

        float stopDistance = Mathf.Max(0.1f, battleAI.attackRange - stopInsideMargin);
        Collider[] hits = Physics.OverlapSphere(transform.position, battleAI.attackRange + 1f);

        foreach (var col in hits)
        {
            if (col.gameObject == this.gameObject) continue;
            if (!IsValidEnemyLayer(col.gameObject.layer)) continue;

            var hero = col.GetComponentInParent<HeroStats>();
            var unit = col.GetComponentInParent<UnitStats>();
            
            if (hero == null && unit == null) continue;
            if (hero != null && hero.isDead) continue;
            if (unit != null && unit.isDead) continue;

            Transform enemyRoot = col.transform.root;
            if (hero != null) enemyRoot = hero.transform;
            else if (unit != null) enemyRoot = unit.transform;
            
            float centerDistance = Vector3.Distance(transform.position, enemyRoot.position);

            if (centerDistance <= stopDistance)
            {
                if (agent.hasPath) agent.ResetPath();
                agent.velocity = Vector3.zero;
                break;
            }
        }
    }

    private bool IsValidEnemyLayer(int targetLayer)
    {
        if (myLayer == 6) return (targetLayer == 9 || targetLayer == 8);
        if (myLayer == 7) return (targetLayer == 9);
        if (myLayer == 8) return (targetLayer == 6);
        if (myLayer == 9) return (targetLayer == 6 || targetLayer == 7);
        return false;
    }
}
