using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Смерть и возрождение героя. Вешается на героя (рядом с HeroStats).
///
/// Правила (по ТЗ):
/// - После смерти у игрока НЕТ контроля над телом: компонент HeroMovement отключается,
///   путь агента сбрасывается — тело лежит как мёртвое и не реагирует на клики.
/// - Через respawnDelaySeconds (по умолчанию 2 минуты) герой СПАВНИТСЯ ЗАНОВО на точке спавна:
///   телепорт, полное HP/мана, анимация смерти сбрасывается (выход в Idle), контроль игроцу
///   возвращается. Уровень/опыт/статы при этом НЕ теряются (объект героя тот же).
/// - Точка возрождения берётся так (по приоритету):
///   1) поле respawnPoint — ПУСТОЙ ОБЪЕКТ на сцене, перетаскивается в Инспектор (рекомендуемый способ
///      по просьбе владельца: возрождаемся в заранее поставленную точку МАПЫ — камера больше не улетает);
///   2) объект на сцене с именем RespawnPoint (если он есть — поле заполнять не обязательно);
///   3) GameSpawner.spawnPoint (та же точка, где герой появился при старте матча);
///   4) стартовая позиция самого героя (аварийный вариант).
///
/// Герой намеренно НЕ уничтожается и не пересоздаётся — так не ломаются ссылки UI
/// (HeroStatsBarsUI, HeroStatsPanel, HeroExperience) и опыт с уровнем остаются у героя.
///
/// Отсчёт таймера доступен для UI: IsWaitingForRespawn + RespawnTimeRemaining
/// (его показывает HeroStatsBarsUI над полосками HP/маны).
/// </summary>
public class HeroRespawn : MonoBehaviour
{
    [Tooltip("Задержка возрождения с момента смерти, секунды (120 = 2 минуты)")]
    public float respawnDelaySeconds = 120f;

    [Tooltip("Логировать смерть и возрождение в консоль")]
    public bool logEvents = true;

    [Tooltip("ТОЧКА ВОЗРОЖДЕНИЯ: создай на сцене пустой объект (GameObject > Create Empty), поставь его в нужное место карты и перетащи сюда. Если поле пустое — скрипт сам найдёт объект с именем RespawnPoint, затем возьмёт GameSpawner.spawnPoint.")]
    public Transform respawnPoint;

    /// <summary>Герой мёртв и ждёт возрождения.</summary>
    public bool IsWaitingForRespawn { get { return waitingRespawn; } }

    /// <summary>Сколько секунд осталось до возрождения (0, если герой жив).</summary>
    public float RespawnTimeRemaining
    {
        get { return waitingRespawn ? Mathf.Max(0f, respawnTimer) : 0f; }
    }

    private HeroStats stats;
    private NavMeshAgent agent;
    private HeroMovement movement;
    private Animator anim;
    private Transform spawnPoint;
    private Vector3 fallbackSpawnPos;
    private bool waitingRespawn = false;
    private float respawnTimer = 0f;

    void Start()
    {
        stats = GetComponent<HeroStats>();
        if (stats == null) stats = GetComponentInParent<HeroStats>();

        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();

        movement = GetComponent<HeroMovement>();
        if (movement == null) movement = GetComponentInParent<HeroMovement>();

        anim = GetComponentInChildren<Animator>(true);

        // ТОЧКА ВОЗРОЖДЕНИЯ (по приоритету): поле respawnPoint -> объект "RespawnPoint" -> GameSpawner.spawnPoint
        if (respawnPoint != null)
        {
            spawnPoint = respawnPoint;
        }
        else
        {
            GameObject marker = GameObject.Find("RespawnPoint");
            if (marker != null) spawnPoint = marker.transform;
        }

        if (spawnPoint == null)
        {
            GameSpawner spawner = FindAnyObjectByType<GameSpawner>();
            if (spawner != null && spawner.spawnPoint != null)
                spawnPoint = spawner.spawnPoint;
        }

        if (spawnPoint != null)
        {
            if (logEvents)
                Debug.Log($"[Возрождение] Точка возрождения: {spawnPoint.name} ({spawnPoint.position}).");
        }
        else
        {
            fallbackSpawnPos = transform.position;
            if (logEvents)
                Debug.LogWarning("[Возрождение] Точка возрождения не задана (ни поле, ни объект RespawnPoint, ни GameSpawner.spawnPoint) — герой будет возрождаться на своей стартовой позиции.");
        }

        if (stats == null && logEvents)
            Debug.LogWarning("[Возрождение] На герое нет HeroStats — скрипт не работает. Перенеси на объект героя.");
    }

    void Update()
    {
        if (stats == null) return;

        // Поймали момент смерти: отбираем контроль над телом и запускаем отсчёт
        if (stats.isDead && !waitingRespawn)
        {
            OnHeroDied();
        }

        if (waitingRespawn)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f) RespawnNow();
        }
    }

    private void OnHeroDied()
    {
        waitingRespawn = true;
        respawnTimer = respawnDelaySeconds;

        // НЕТ контроля над телом: клики игрока больше ничего не делают,
        // труп не получает команды движения
        if (movement != null) movement.enabled = false;
        if (agent != null && agent.isOnNavMesh) agent.ResetPath();

        if (logEvents)
            Debug.Log($"<color=red>[ГЕРОЙ ПАЛ]</color> Возрождение через {respawnDelaySeconds:0} сек. Контроль над телом отключён.");
    }

    private void RespawnNow()
    {
        waitingRespawn = false;
        respawnTimer = 0f;

        // Спавн на точке возрождения (пустой объект на карте — камера не улетает за карту)
        Vector3 pos = spawnPoint != null ? spawnPoint.position : fallbackSpawnPos;
        if (agent != null)
        {
            transform.position = pos;
            if (!agent.enabled) agent.enabled = true;
            agent.Warp(pos); // принудительный перенос агента на NavMesh
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.ResetPath();
        }
        else
        {
            transform.position = pos;
        }

        // Полное восстановление
        stats.isDead = false;
        stats.currentHP = stats.maxHP;
        stats.currentMana = stats.maxMana;

        // Сброс анимации смерти: тело встаёт в Idle вместо замершего трупа
        if (anim != null)
        {
            anim.ResetTrigger("OnDeath");
            anim.ResetTrigger("OnAttack");
            anim.Play("Idle", 0, 0f);
        }

        // Возвращаем контроль игроку
        if (movement != null) movement.enabled = true;

        if (logEvents)
            Debug.Log($"<color=green>[ВОЗРОЖДЕНИЕ]</color> {gameObject.name} снова в строю на точке спавна — контроль возвращён.");
    }
}
