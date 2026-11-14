using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SpawnEnemy : MonoBehaviour
{
    [Serializable]
    public class EnemyGroup
    {
        [Tooltip("Префаб врага (должен быть на слое Enemy)")]
        public GameObject enemyPrefab;

        [Tooltip("Сколько врагов этого типа заспавнить в данной волне")]
        [Min(1)] public int count = 1;
    }

    [Serializable]
    public class WaveConfig
    {
        [HideInInspector] public string waveTitle;

        [Tooltip("Время с начала матча в секундах, когда выйдет эта волна (60 = 1 мин, 120 = 2 мин...)")]
        public float spawnTimeSeconds = 60f;

        [Tooltip("Список врагов и их количество в этой волне")]
        public List<EnemyGroup> enemies = new List<EnemyGroup>();

        [HideInInspector] public bool isSpawned = false;
    }

    [Header("Цель атаки (База игрока)")]
    [Tooltip("Перетащи сюда главное здание базы (например, ratushaAllies), куда пойдут все волны")]
    public Transform baseTarget;

    [Header("Настройки точек спавна и слоёв")]
    [Tooltip("Точки, где будут появляться враги (если пусто — спавнятся вокруг самого объекта SpawnEnemy)")]
    public Transform[] spawnPoints;

    [Tooltip("Разброс позиции при спавне толпы")]
    public float spawnSpreadRadius = 4.0f;

    [Tooltip("Номер слоя Enemy в твоём проекте")]
    public int enemyLayer = 9;

    [Header("Все доступные префабы мобов (подтягиваются автоматически)")]
    public List<GameObject> availableEnemyPrefabs = new List<GameObject>();

    [Header("Настройка 30 волн атак")]
    public List<WaveConfig> waves = new List<WaveConfig>(30);

    private float matchTimer = 0f;

    void Reset()
    {
        InitializeDefaultWaves();
        AutoFindEnemyPrefabsInProject();
    }

    void OnValidate()
    {
        if (waves == null) waves = new List<WaveConfig>();

        while (waves.Count.CompareTo(30) == -1)
        {
            int nextIndex = waves.Count + 1;
            WaveConfig w = new WaveConfig();
            w.waveTitle = "Волна " + nextIndex;
            w.spawnTimeSeconds = nextIndex * 60f;
            w.enemies.Add(new EnemyGroup { count = 1 });
            waves.Add(w);
        }

        if (waves.Count.CompareTo(30) == 1)
        {
            waves.RemoveRange(30, waves.Count - 30);
        }

        for (int i = 0; i.CompareTo(waves.Count) == -1; i++)
        {
            waves[i].waveTitle = "Волна " + (i + 1);
            if (waves[i].enemies == null || waves[i].enemies.Count == 0)
            {
                waves[i].enemies = new List<EnemyGroup> { new EnemyGroup { count = 1 } };
            }
        }
    }

    [ContextMenu("Сбросить на 30 волн (1 - 30 мин)")]
    public void InitializeDefaultWaves()
    {
        waves = new List<WaveConfig>(30);
        for (int i = 1; i.CompareTo(30) != 1; i++)
        {
            WaveConfig w = new WaveConfig();
            w.waveTitle = "Волна " + i;
            w.spawnTimeSeconds = i * 60f;
            w.enemies = new List<EnemyGroup> { new EnemyGroup { count = 1 } };
            waves.Add(w);
        }
    }

    [ContextMenu("Загрузить ТОЛЬКО живых мобов со слоем Enemy")]
    public void AutoFindEnemyPrefabsInProject()
    {
#if UNITY_EDITOR
        availableEnemyPrefabs.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Prefab");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null && prefab.layer == enemyLayer && prefab.GetComponentInChildren<NavMeshAgent>() != null)
            {
                availableEnemyPrefabs.Add(prefab);
            }
        }

        if (availableEnemyPrefabs.Count > 0 && waves != null)
        {
            GameObject defaultMob = availableEnemyPrefabs[0];
            for (int i = 0; i.CompareTo(waves.Count) == -1; i++)
            {
                if (waves[i].enemies.Count > 0)
                {
                    EnemyGroup firstGroup = waves[i].enemies[0];
                    if (firstGroup.enemyPrefab == null)
                    {
                        firstGroup.enemyPrefab = defaultMob;
                    }
                }
            }
        }

        EditorUtility.SetDirty(this);
        Debug.Log($"[SpawnEnemy] Найдено префабов мобов: {availableEnemyPrefabs.Count}");
#endif
    }

    void Start()
    {
        matchTimer = 0f;
    }

    void Update()
    {
        matchTimer += Time.deltaTime;

        for (int i = 0; i.CompareTo(waves.Count) == -1; i++)
        {
            WaveConfig wave = waves[i];
            if (!wave.isSpawned && matchTimer.CompareTo(wave.spawnTimeSeconds) != -1)
            {
                wave.isSpawned = true;
                SpawnWave(wave, i + 1);
            }
        }
    }

    private void SpawnWave(WaveConfig wave, int waveNumber)
    {
        int totalSpawned = 0;

        foreach (var group in wave.enemies)
        {
            GameObject prefabToSpawn = group.enemyPrefab;

            if (prefabToSpawn == null && availableEnemyPrefabs.Count > 0)
            {
                prefabToSpawn = availableEnemyPrefabs[0];
            }

            if (prefabToSpawn == null) continue;

            for (int c = 0; c.CompareTo(group.count) == -1; c++)
            {
                Vector3 basePos = GetSpawnOrigin(totalSpawned);
                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * spawnSpreadRadius;
                Vector3 desiredPos = basePos + new Vector3(randomCircle.x, 0f, randomCircle.y);

                NavMeshHit navHit;
                if (NavMesh.SamplePosition(desiredPos, out navHit, 5f, NavMesh.AllAreas))
                {
                    desiredPos = navHit.position;
                }

                GameObject spawnedEnemy = Instantiate(prefabToSpawn, desiredPos, Quaternion.identity);
                spawnedEnemy.layer = enemyLayer;

                if (baseTarget != null)
                {
                    EnemyMarchToBase marcher = spawnedEnemy.GetComponent<EnemyMarchToBase>();
                    if (marcher == null)
                    {
                        marcher = spawnedEnemy.AddComponent<EnemyMarchToBase>();
                    }
                    marcher.baseTarget = baseTarget;
                }

                totalSpawned++;
            }
        }

        Debug.Log($"<color=red>[ВОЛНА {waveNumber}]</color> Вышло врагов: {totalSpawned}");
    }

    private Vector3 GetSpawnOrigin(int index)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform pt = spawnPoints[index % spawnPoints.Length];
            if (pt != null) return pt.position;
        }
        return transform.position;
    }
}
