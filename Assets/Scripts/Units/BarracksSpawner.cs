using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Казарма союзников — аналог SpawnEnemy, но для альянса. Вешается на УЖЕ РАСПОЛОЖЕННУЮ
/// казарму на сцене (на сам объект казармы).
///
/// Правила (по ТЗ):
/// - У казармы ВСЕГДА ТРИ точки (пустышки-дети казармы) — куда идут войска союзников.
///   Если точки не назначены в инспекторе, они создаются автоматически (Ally_RallyPoint_1..3),
///   их можно двигать по сцене как обычные объекты.
/// - ВСЁ войско спавнится на слое Allies (7, номер зашит в проекте).
/// - Есть ТАЙМЕР: раз в spawnInterval секунд казарма выпускает партию подкреплений.
/// - НА КАЖДУЮ ТОЧКУ ВРУЧНУЮ в инспекторе прописывается: КАКИЕ войска (префабы),
///   СКОЛЬКО их и КУДА идти (сама точка).
///   ВАЖНО: count = число самих юнитов (10 подкреплений = 10 спавнов юнитов, а не «пачек»).
///
/// Юнит спавнится вокруг казармы, затем сам доходит до своей точки (AllyMarchToPoint)
/// и держит её. Бой марш не отменяет: пока BattleAI не видит врага — идём, увидел — дерётся,
/// после боя снова идёт к точке.
///
/// Казарма — это UnitStats (+ BuildingDestruction): когда её уничтожили (HP <= 0 / isDead),
/// подкрепления от ЭТОЙ казармы больше не выходят (у других казарм — свои спавнеры).
/// </summary>
public class BarracksSpawner : MonoBehaviour
{
    [Serializable]
    public class SquadEntry
    {
        [Tooltip("Префаб союзника (например, Allies_Soldier или Allies_Shooter на слое Allies)")]
        public GameObject unitPrefab;

        [Tooltip("Сколько юнитов этого типа пойдёт на точку (10 подкреплений = 10 спавнов юнитов)")]
        [Min(1)] public int count = 1;
    }

    [Serializable]
    public class PointConfig
    {
        [HideInInspector] public string pointTitle;

        [Tooltip("Точка, куда идут войска (пустышка-ребёнок казармы). Если пусто — создастся автоматически")]
        public Transform rallyPoint;

        [Tooltip("Какие войска (префабы) и сколько юнитов пойдут на ЭТУ точку")]
        public List<SquadEntry> units = new List<SquadEntry>();

        [HideInInspector] public bool pointCreatedAuto = false;
    }

    [Header("Таймер подкреплений")]
    [Tooltip("Раз в сколько секунд казарма выпускает партию подкреплений на все три точки")]
    public float spawnInterval = 45f;

    [Tooltip("Первая партия выпускается сразу при старте игры")]
    public bool spawnFirstBatchImmediately = true;

    [Tooltip("Лимит партий за матч (0 = без ограничения, спавним весь матч)")]
    [Min(0)] public int maxBatches = 0;

    [Header("Спавн")]
    [Tooltip("Разброс позиции появления вокруг казармы")]
    public float spawnSpreadRadius = 3f;

    [Tooltip("Номер слоя Allies (зашишен в проекте — 7, не менять без рефакторинга)")]
    public int alliesLayer = 7;

    [Tooltip("Дистанция, ближе которой юнит считает, что дошёл до точки")]
    public float stopDistance = 2.5f;

    [Header("ТРИ точки казармы: куда, какие войска и сколько (вручную в инспекторе)")]
    public List<PointConfig> points = new List<PointConfig>(3);

    private float timer;
    private int batchesSpawned;
    private UnitStats barracksStats;
    private bool barracksDestroyed = false;

    void Reset()
    {
        EnsureThreePoints();
        CreateRallyPoints();
    }

    void OnValidate()
    {
        EnsureThreePoints();
    }

    void Start()
    {
        EnsureThreePoints();
        AutoAssignMissingPoints();

        barracksStats = GetComponent<UnitStats>();
        if (barracksStats == null)
        {
            Debug.LogWarning($"[Казарма] На {gameObject.name} нет UnitStats — здание не может получать урон, но подкрепления работать будут.");
        }

        // Первая партия: таймер сразу «взведён» — первый Update выпустит партию
        timer = spawnFirstBatchImmediately ? spawnInterval : 0f;
    }

    void Update()
    {
        // Казарма уничтожена — подкреплений от неё больше не будет (паттерн BuildingDestruction:
        // опрос UnitStats.isDead / HP, без правок UnitStats)
        if (!barracksDestroyed && barracksStats != null &&
            (barracksStats.isDead || barracksStats.currentHP <= 0f))
        {
            barracksDestroyed = true;
            Debug.Log($"<color=orange>[КАЗАРМА ПАЛА]</color> {gameObject.name} уничтожена — подкрепления прекращены.");
            enabled = false;
            return;
        }

        if (barracksDestroyed) return;
        if (maxBatches > 0 && batchesSpawned >= maxBatches) return;

        timer += Time.deltaTime;
        if (timer < spawnInterval) return;
        timer = 0f;

        SpawnBatch();
        batchesSpawned++;
    }

    /// <summary>Точек всегда ровно ТРИ (по ТЗ).</summary>
    private void EnsureThreePoints()
    {
        if (points == null) points = new List<PointConfig>(3);
        while (points.Count < 3) points.Add(new PointConfig());
        if (points.Count > 3) points.RemoveRange(3, points.Count - 3);
        for (int i = 0; i < 3; i++) points[i].pointTitle = "Точка " + (i + 1);
    }

    private void AutoAssignMissingPoints()
    {
        for (int i = 0; i < 3; i++)
        {
            PointConfig p = points[i];
            if (p.rallyPoint != null) continue;

            // Ищем уже существующего ребёнка с таким именем
            Transform found = transform.Find("Ally_RallyPoint_" + (i + 1));
            if (found == null)
            {
                // Создаём пустышку-ребёнка казармы, слегка разнесённую в стороны
                GameObject go = new GameObject("Ally_RallyPoint_" + (i + 1));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3((i - 1) * 5f, 0f, 5f);
                found = go.transform;
                p.pointCreatedAuto = true;
            }
            p.rallyPoint = found;
        }
    }

    [ContextMenu("Создать/назначить 3 точки-ребёнка (Ally_RallyPoint_1..3)")]
    public void CreateRallyPoints()
    {
        EnsureThreePoints();
        AutoAssignMissingPoints();
        Debug.Log("[Казарма] Точки созданы/назначены. Расставь пустышки по сцене и заполни войска в инспекторе.");
    }

    /// <summary>Выпускает партию подкреплений на все три точки.</summary>
    private void SpawnBatch()
    {
        int totalSpawned = 0;

        for (int i = 0; i < 3; i++)
        {
            PointConfig p = points[i];
            if (p == null || p.rallyPoint == null) continue;

            foreach (SquadEntry entry in p.units)
            {
                if (entry == null || entry.unitPrefab == null) continue;

                // count = число юнитов (10 подкреплений = 10 спавнов)
                for (int c = 0; c < entry.count; c++)
                {
                    SpawnOne(entry.unitPrefab, p.rallyPoint, i + 1);
                    totalSpawned++;
                }
            }
        }

        Debug.Log($"<color=green>[ПОДКРЕПЛЕНИЕ]</color> Казарма {gameObject.name}: партия {batchesSpawned + 1}, юнитов вышло: {totalSpawned}");
    }

    /// <summary>Спавнит одного юнита вокруг казармы и отправляет его к точке.</summary>
    private void SpawnOne(GameObject prefab, Transform rallyPoint, int pointIndex)
    {
        Vector2 rnd = UnityEngine.Random.insideUnitCircle * spawnSpreadRadius;
        Vector3 desiredPos = transform.position + new Vector3(rnd.x, 0f, rnd.y);

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(desiredPos, out navHit, 5f, NavMesh.AllAreas))
        {
            desiredPos = navHit.position;
        }

        GameObject unit = Instantiate(prefab, desiredPos, Quaternion.identity);
        unit.layer = alliesLayer; // ВСЁ войско — на слое Allies (как в ТЗ)

        // Марш к точке (аналог EnemyMarchToBase, только для союзников и до точки казармы)
        AllyMarchToPoint marcher = unit.GetComponent<AllyMarchToPoint>();
        if (marcher == null) marcher = unit.AddComponent<AllyMarchToPoint>();
        marcher.rallyPoint = rallyPoint;
        marcher.stopDistance = stopDistance;
    }

    // Визуализация трёх точек в редакторе — удобно настраивать
    void OnDrawGizmos()
    {
        if (points == null) return;
        EnsureThreePoints();

        for (int i = 0; i < 3; i++)
        {
            Transform pt = points[i] != null ? points[i].rallyPoint : null;
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            if (pt != null)
            {
                Gizmos.DrawSphere(pt.position, 0.4f);
                Gizmos.DrawLine(transform.position, pt.position);
            }
        }
    }
}
