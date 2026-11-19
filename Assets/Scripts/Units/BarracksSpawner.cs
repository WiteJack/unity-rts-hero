using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Казарма союзников — аналог SpawnEnemy, но для альянса. Вешается на УЖЕ РАСПОЛОЖЕННУЮ
/// казарму на сцене (на сам объект казармы).
///
/// Правила (по ТЗ, упрощённая версия без марша к точкам):
/// - Волны как в SpawnEnemy: список волн, у каждой — ВРЕМЯ ОТ НАЧАЛА МАТЧА в секундах.
///   10 подкреплений = 10 волн = 10 моментов выхода (по таймеру матча).
/// - В КАЖДОЙ волне ВРУЧНУЮ прописывается, какие войска (префабы) и сколько юнитов выйдет
///   (count = число юнитов).
/// - Юниты появляются вокруг казармы (разброс spawnSpreadRadius) и дальше живут своей жизнью
///   (бой ведёт BattleAI). Точки марша и AllyMarchToPoint УБРАНЫ — союзники просто
///   появляются вокруг здания.
/// - Весь спавн — на слое Allies (7, номер зашит в проекте).
///
/// Казарма — это UnitStats (+ BuildingDestruction): когда её уничтожили (HP <= 0 / isDead),
/// волны подкреплений от ЭТОЙ казармы больше не выходят (у других казарм — свои спавнеры).
/// </summary>
public class BarracksSpawner : MonoBehaviour
{
    [Serializable]
    public class SquadEntry
    {
        [Tooltip("Префаб союзника (например, Allies_Soldier или Allies_Shooter на слое Allies)")]
        public GameObject unitPrefab;

        [Tooltip("Сколько юнитов этого типа выйдет в этой волне")]
        [Min(1)] public int count = 1;
    }

    [Serializable]
    public class WaveConfig
    {
        [HideInInspector] public string waveTitle;

        [Tooltip("Время с начала матча в секундах, когда выйдет эта волна (60 = 1 мин, 120 = 2 мин...)")]
        public float spawnTimeSeconds = 60f;

        [Tooltip("Войска этой волны (какие префабы и сколько юнитов)")]
        public List<SquadEntry> units = new List<SquadEntry>();

        [HideInInspector] public bool isSpawned = false;
    }

    [Header("Волны подкреплений (как в SpawnEnemy: время от начала матча)")]
    public List<WaveConfig> waves = new List<WaveConfig>(10);

    [Header("Спавн")]
    [Tooltip("Радиус разброса появления юнитов вокруг казармы")]
    public float spawnSpreadRadius = 3f;

    [Tooltip("Номер слоя Allies (зашит в проекте — 7, не менять без рефакторинга)")]
    public int alliesLayer = 7;

    [Header("Прочее")]
    [Tooltip("Логировать каждую вышедшую волну в консоль")]
    public bool logWaves = true;

    private float matchTimer = 0f;
    private UnitStats barracksStats;
    private bool barracksDestroyed = false;

    void Reset()
    {
        InitializeDefaultWaves();
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

    /// <summary>Выпускает волну подкреплений вокруг казармы.</summary>
    private void SpawnWave(WaveConfig wave, int waveNumber)
    {
        int totalSpawned = 0;

        if (wave.units != null)
        {
            foreach (SquadEntry entry in wave.units)
            {
                if (entry == null || entry.unitPrefab == null) continue;

                // count = число юнитов (подкреплений)
                for (int c = 0; c < entry.count; c++)
                {
                    SpawnOne(entry.unitPrefab);
                    totalSpawned++;
                }
            }
        }

        if (logWaves)
            Debug.Log($"<color=green>[ПОДКРЕПЛЕНИЕ]</color> Казарма {gameObject.name}: волна {waveNumber}, юнитов вышло: {totalSpawned}");
        else if (totalSpawned == 0 && waveNumber > 0)
            Debug.LogWarning($"[ПОДКРЕПЛЕНИЕ] Казарма {gameObject.name}: волна {waveNumber} вышла с 0 юнитов — проверь список units в инспекторе.");
    }

    /// <summary>Спавнит одного юнита вокруг казармы (обязательно на NavMesh).</summary>
    private void SpawnOne(GameObject prefab)
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
            Debug.LogWarning($"[КАЗАРМА] {gameObject.name}: в 5м от точки спавна НЕ найден NavMesh — юнит появится вне навмеша и будет стоять. Запеки NavMesh вокруг казармы (Window > AI > Navigation > Bake).");
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

    // Визуализация радиуса спавна вокруг казармы в редакторе — удобно настраивать
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, spawnSpreadRadius);
    }
}
