using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Казарма союзников — аналог SpawnEnemy, но для альянса. Вешается на УЖЕ РАСПОЛОЖЕННУЮ
/// казарму на сцене (на сам объект казармы).
///
/// Правила (по ТЗ):
/// - Волны как в SpawnEnemy: список волн, у каждой — ВРЕМЯ ОТ НАЧАЛА МАТЧА в секундах.
///   10 подкреплений = 10 волн = 10 моментов выхода (по таймеру матча).
/// - У казармы ВСЕГДА ТРИ точки (пустышки-дети Ally_RallyPoint_1..3) — куда идут войска.
///   Если точки не назначены, создаются автоматически; их можно двигать по сцене.
/// - В КАЖДОЙ волне ВРУЧНУЮ прописывается, какие войска (префабы) и сколько юнитов
///   пойдут на каждую из ТРЁХ точек (count = число юнитов).
/// - Весь спавн — на слое Allies (7, номер зашит в проекте).
///
/// Казарма — это UnitStats (+ BuildingDestruction): когда её уничтожили (HP <= 0 / isDead),
/// волны подкреплений от ЭТОЙ казармы больше не выходят (у других казарм — свои спавнеры).
/// Юнит спавнится вокруг казармы, идёт до своей точки (AllyMarchToPoint), бой марш не отменяет.
/// </summary>
public class BarracksSpawner : MonoBehaviour
{
    [Serializable]
    public class SquadEntry
    {
        [Tooltip("Префаб союзника (например, Allies_Soldier или Allies_Shooter на слое Allies)")]
        public GameObject unitPrefab;

        [Tooltip("Сколько юнитов этого типа пойдёт на точку в этой волне")]
        [Min(1)] public int count = 1;
    }

    [Serializable]
    public class WaveConfig
    {
        [HideInInspector] public string waveTitle;

        [Tooltip("Время с начала матча в секундах, когда выйдет эта волна (60 = 1 мин, 120 = 2 мин...)")]
        public float spawnTimeSeconds = 60f;

        [Tooltip("Войска для ТОЧКИ 1 (какие префабы и сколько юнитов)")]
        public List<SquadEntry> point1Units = new List<SquadEntry>();

        [Tooltip("Войска для ТОЧКИ 2")]
        public List<SquadEntry> point2Units = new List<SquadEntry>();

        [Tooltip("Войска для ТОЧКИ 3")]
        public List<SquadEntry> point3Units = new List<SquadEntry>();

        [HideInInspector] public bool isSpawned = false;
    }

    [Header("ТРИ точки казармы — куда идут войска (пустышки-дети, создаются авто)")]
    [Tooltip("Точка 1 / 2 / 3. Если пусто — создастся ребёнок Ally_RallyPoint_N")]
    public Transform[] rallyPoints = new Transform[3];

    [Header("Волны подкреплений (как в SpawnEnemy: время от начала матча)")]
    public List<WaveConfig> waves = new List<WaveConfig>(10);

    [Header("Спавн")]
    [Tooltip("Разброс позиции появления вокруг казармы")]
    public float spawnSpreadRadius = 3f;

    [Tooltip("Номер слоя Allies (зашит в проекте — 7, не менять без рефакторинга)")]
    public int alliesLayer = 7;

    [Tooltip("Дистанция, ближе которой юнит считает, что дошёл до точки")]
    public float stopDistance = 2.5f;

    [Header("Прочее")]
    [Tooltip("Логировать каждую вышедшую волну в консоль")]
    public bool logWaves = true;

    private float matchTimer = 0f;
    private UnitStats barracksStats;
    private bool barracksDestroyed = false;

    void Reset()
    {
        InitializeDefaultWaves();
        CreateRallyPoints();
    }

    void OnValidate()
    {
        if (waves == null) waves = new List<WaveConfig>();
        if (waves.Count == 0) InitializeDefaultWaves();

        for (int i = 0; i < waves.Count; i++)
        {
            if (waves[i] == null) waves[i] = new WaveConfig();
            waves[i].waveTitle = "Волна " + (i + 1);
        }
    }

    void Start()
    {
        if (rallyPoints == null || rallyPoints.Length != 3) rallyPoints = new Transform[3];
        AutoAssignMissingPoints();

        barracksStats = GetComponent<UnitStats>();
        if (barracksStats == null)
        {
            Debug.LogWarning($"[Казарма] На {gameObject.name} нет UnitStats — здание не сможет получать урон и «погибнуть».");
        }

        matchTimer = 0f;
    }

    void Update()
    {
        // Казарма уничтожена — волны от неё больше не выходят (паттерн BuildingDestruction:
        // опрос UnitStats.isDead / HP, без правок UnitStats)
        if (!barracksDestroyed && barracksStats != null &&
            (barracksStats.isDead || barracksStats.currentHP <= 0f))
        {
            barracksDestroyed = true;
            Debug.Log($"<color=orange>[КАЗАРМА ПАЛА]</color> {gameObject.name} уничтожена — подкрепления прекращены.");
            enabled = false;
            return;
        }

        matchTimer += Time.deltaTime;

        for (int i = 0; i < waves.Count; i++)
        {
            WaveConfig wave = waves[i];
            if (!wave.isSpawned && matchTimer >= wave.spawnTimeSeconds)
            {
                wave.isSpawned = true;
                SpawnWave(wave, i + 1);
            }
        }
    }

    /// <summary>Выпускает волну подкреплений: по списку юнитов на каждую из трёх точек.</summary>
    private void SpawnWave(WaveConfig wave, int waveNumber)
    {
        int totalSpawned = 0;

        SpawnPointUnits(wave.point1Units, GetPoint(0), waveNumber, ref totalSpawned);
        SpawnPointUnits(wave.point2Units, GetPoint(1), waveNumber, ref totalSpawned);
        SpawnPointUnits(wave.point3Units, GetPoint(2), waveNumber, ref totalSpawned);

        if (logWaves)
            Debug.Log($"<color=green>[ПОДКРЕПЛЕНИЕ]</color> Казарма {gameObject.name}: волна {waveNumber}, юнитов вышло: {totalSpawned}");
        else if (totalSpawned == 0)
            Debug.LogWarning($"[ПОДКРЕПЛЕНИЕ] Казарма {gameObject.name}: волна {waveNumber} вышла с 0 юнитов — проверь списки войск (point1/2/3Units) в инспекторе.");
    }

    private void SpawnPointUnits(List<SquadEntry> units, Transform rallyPoint, int waveNumber, ref int totalSpawned)
    {
        if (units == null || rallyPoint == null) return;

        foreach (SquadEntry entry in units)
        {
            if (entry == null || entry.unitPrefab == null) continue;

            // count = число юнитов
            for (int c = 0; c < entry.count; c++)
            {
                SpawnOne(entry.unitPrefab, rallyPoint);
                totalSpawned++;
            }
        }
    }

    /// <summary>Спавнит одного юнита вокруг казармы и отправляет его к точке.</summary>
    private void SpawnOne(GameObject prefab, Transform rallyPoint)
    {
        Vector2 rnd = UnityEngine.Random.insideUnitCircle * spawnSpreadRadius;
        Vector3 desiredPos = transform.position + new Vector3(rnd.x, 0f, rnd.y);

        // ВАЖНО: юнит должен появиться НА NavMesh, иначе он просто стоит на месте
        // (NavMeshAgent отказывается работать вне запечённой области).
        bool onNavMesh = NavMesh.SamplePosition(desiredPos, out NavMeshHit navHit, 5f, NavMesh.AllAreas);
        if (onNavMesh)
        {
            desiredPos = navHit.position;
        }
        else
        {
            Debug.LogWarning($"[КАЗАРМА] {gameObject.name}: в 5м от точки спавна НЕ найден NavMesh — юнит появится вне навмеша и стоять на месте. Запеки NavMesh вокруг казармы (Window > AI > Navigation > Bake).");
        }

        GameObject unit = Instantiate(prefab, desiredPos, Quaternion.identity);
        unit.layer = alliesLayer; // ВСЁ войско — на слое Allies (как в ТЗ)

        // Если юнит всё-таки оказался вне NavMesh — принудительно переносим на запечённую область
        NavMeshAgent unitAgent = unit.GetComponent<NavMeshAgent>();
        if (unitAgent == null) unitAgent = unit.GetComponentInChildren<NavMeshAgent>();
        if (unitAgent != null && onNavMesh && !unitAgent.isOnNavMesh)
        {
            unitAgent.Warp(navHit.position);
        }

        // Марш к точке (аналог EnemyMarchToBase, только для союзников)
        AllyMarchToPoint marcher = unit.GetComponent<AllyMarchToPoint>();
        if (marcher == null) marcher = unit.AddComponent<AllyMarchToPoint>();
        marcher.rallyPoint = rallyPoint;
        marcher.stopDistance = stopDistance;
        marcher.logDebug = logWaves; // диагностика марша идёт тем же тумблером, что и логи волн
    }

    private Transform GetPoint(int index)
    {
        if (rallyPoints == null || rallyPoints.Length != 3) rallyPoints = new Transform[3];
        if (rallyPoints[index] == null) AutoAssignMissingPoints();
        return rallyPoints[index];
    }

    /// <summary>Точек всегда ровно ТРИ (по ТЗ): автосоздание пустышек-детей казармы.</summary>
    private void AutoAssignMissingPoints()
    {
        for (int i = 0; i < 3; i++)
        {
            if (rallyPoints[i] != null) continue;

            Transform found = transform.Find("Ally_RallyPoint_" + (i + 1));
            if (found == null)
            {
                GameObject go = new GameObject("Ally_RallyPoint_" + (i + 1));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3((i - 1) * 5f, 0f, 5f);
                found = go.transform;
            }
            rallyPoints[i] = found;
        }
    }

    [ContextMenu("Создать/назначить 3 точки-ребёнка (Ally_RallyPoint_1..3)")]
    public void CreateRallyPoints()
    {
        if (rallyPoints == null || rallyPoints.Length != 3) rallyPoints = new Transform[3];
        AutoAssignMissingPoints();
        Debug.Log("[Казарма] Точки созданы/назначены. Расставь пустышки Ally_RallyPoint_1..3 по сцене.");
    }

    [ContextMenu("Сбросить на 10 волн (каждую минуту, пустые списки войск)")]
    public void InitializeDefaultWaves()
    {
        waves = new List<WaveConfig>(10);
        for (int i = 1; i <= 10; i++)
        {
            WaveConfig w = new WaveConfig();
            w.waveTitle = "Волна " + i;
            w.spawnTimeSeconds = i * 60f;
            waves.Add(w);
        }
    }

    // Визуализация трёх точек в редакторе — удобно настраивать
    void OnDrawGizmos()
    {
        if (rallyPoints == null) return;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        for (int i = 0; i < rallyPoints.Length && i < 3; i++)
        {
            Transform pt = rallyPoints[i];
            if (pt != null)
            {
                Gizmos.DrawSphere(pt.position, 0.4f);
                Gizmos.DrawLine(transform.position, pt.position);
            }
        }
    }
}
