using UnityEngine;
using UnityEngine.AI;

public class UnitSeparation : MonoBehaviour
{
    [Header("Настройки анти-слипания")]
    [Tooltip("Минимальная дистанция между центрами юнитов (обычно 0.8 - 1.0 м)")]
    public float separationRadius = 0.85f;

    [Tooltip("Сила мягкого расталкивания в стороны")]
    public float pushForce = 2.5f;

    [Tooltip("Радиус коллайдера NavMeshAgent для обхода союзников")]
    public float agentAvoidanceRadius = 0.4f;

    private NavMeshAgent agent;
    private UnitStats myStats;
    private int myLayer;
    private float checkTimer;

    // Общий массив для Physics.OverlapSphereNonAlloc (не нагружает память при 500 врагах)
    private static readonly Collider[] neighborBuffer = new Collider[16];

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();

        myStats = GetComponent<UnitStats>();
        myLayer = gameObject.layer;

        // Настраиваем встроенный обход препятствий у NavMeshAgent
        if (agent != null)
        {
            agent.radius = agentAvoidanceRadius;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            // Случайный приоритет (от 30 до 70), чтобы зомби не застревали лоб в лоб
            agent.avoidancePriority = Random.Range(30, 71);
        }

        // Разносим проверки по кадрам для 500 юнитов
        checkTimer = Random.Range(0f, 0.05f);
    }

    void Update()
    {
        if (myStats != null && myStats.isDead) return;
        if (agent == null || !agent.isOnNavMesh) return;

        checkTimer -= Time.deltaTime;
        if (checkTimer.CompareTo(0f) == 1) return;
        checkTimer = 0.04f; // Проверяем ~25 раз в секунду (идеально для FPS при больших волнах)

        int count = Physics.OverlapSphereNonAlloc(transform.position, separationRadius, neighborBuffer);
        Vector3 pushDirection = Vector3.zero;
        int neighbors = 0;

        for (int i = 0; i.CompareTo(count) == -1; i++)
        {
            Collider col = neighborBuffer[i];
            if (col == null || col.gameObject == this.gameObject) continue;

            // Отталкиваемся только от юнитов своего же слоя (союзников по волне)
            if (col.gameObject.layer != myLayer) continue;

            Vector3 diff = transform.position - col.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;

            if (dist.CompareTo(0.01f) == 1 && dist.CompareTo(separationRadius) == -1)
            {
                // Чем ближе сосед, тем сильнее мягкий сдвиг в сторону
                float strength = (separationRadius - dist) / separationRadius;
                pushDirection += diff.normalized * strength;
                neighbors++;
            }
        }

        if (neighbors > 0)
        {
            Vector3 moveOffset = pushDirection.normalized * pushForce * 0.04f;
            agent.Move(moveOffset);
        }
    }
}
