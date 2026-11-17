using UnityEngine;

/// <summary>
/// Непрерывная стрельба для стрелка (Allies_Shooter) БЕЗ новых параметров Animator.
/// Вешается на КОРЕНЬ префаба рядом с BattleAI и UnitStats.
///
/// Принцип «заморозки»: пока у BattleAI есть живая цель в радиусе атаки, скрипт
/// перезапускает клип атаки каждые 1/attackSpeed секунд, а когда клип доигрывает
/// до точки заморозки (чуть ПОСЛЕ кадра выстрела — анимационного события
/// OnAttackHitEvent) — ставит anim.speed = 0 и держит позу выстрела до следующего
/// цикла. Урон наносит BattleAI через событие OnAttackHitEvent, как раньше.
///
/// Animator настраивать НЕ нужно: ни параметров, ни изменений переходов.
/// Героя не затрагивает: скрипт отключается, если рядом нет UnitStats (у героя HeroStats).
/// </summary>
public class ShooterAttackLoop : MonoBehaviour
{
    [Tooltip("Имя состояния атаки в Animator (как в окне Animator)")]
    public string attackStateName = "Shooter_Attack";

    [Tooltip("Нормализованное время клипа (0..1), на котором замораживаем позу. " +
             "Поставь чуть ПОЗЖЕ кадра выстрела (анимационного события OnAttackHitEvent на клипе).")]
    [Range(0.05f, 1f)] public float freezeAtNormalizedTime = 0.9f;

    private BattleAI battleAI;
    private UnitStats myStats;
    private Animator anim;
    private System.Reflection.FieldInfo currentTargetField;

    private int attackStateHash;
    private float attackClipLength = 1f;
    private float nextShotTime;
    private bool frozen;

    void Awake()
    {
        battleAI = GetComponent<BattleAI>();
        if (battleAI == null) battleAI = GetComponentInParent<BattleAI>();

        myStats = GetComponent<UnitStats>();
        if (myStats == null) myStats = GetComponentInParent<UnitStats>();

        anim = GetComponentInChildren<Animator>();

        // Доступ к приватной цели BattleAI — тем же способом, что и остальные скрипты проекта (Reflection)
        if (battleAI != null)
            currentTargetField = typeof(BattleAI).GetField("currentTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        attackStateHash = Animator.StringToHash(attackStateName);

        // Реальная длина клипа атаки берётся из контроллера — не зависим от baseAttackClipLength
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var clip in anim.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == attackStateName)
                {
                    attackClipLength = Mathf.Max(0.01f, clip.length);
                    break;
                }
            }
        }

        // Не наш комплект (например, герой с HeroStats) — выключаемся, ничего не ломаем
        if (battleAI == null || myStats == null || anim == null || currentTargetField == null)
            enabled = false;
    }

    void OnDisable()
    {
        Unfreeze();
    }

    void LateUpdate()
    {
        if (myStats.isDead)
        {
            Unfreeze(); // обязательно разморозить, иначе триггер OnDeath не сработает
            return;
        }

        bool inAttackRange = false;

        Transform target = currentTargetField.GetValue(battleAI) as Transform;
        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            inAttackRange = distance <= battleAI.attackRange;
        }

        if (!inAttackRange)
        {
            Unfreeze(); // цель ушла — возвращаем аниматор в обычный режим (Idle/Run как раньше)
            return;
        }

        // Пора стрелять: перезапускаем клип с начала — событие выстрела сработает заново
        if (Time.time >= nextShotTime)
        {
            Unfreeze();
            anim.Play(attackStateHash, 0, 0f);
            // Множитель так, чтобы клип длился ровно 1/attackSpeed секунд
            anim.SetFloat("AttackSpeedMult", myStats.attackSpeed * attackClipLength);
            nextShotTime = Time.time + 1f / Mathf.Max(0.01f, myStats.attackSpeed);
            return;
        }

        // Заморозка: клип доиграл до точки заморозки — держим позу выстрела до следующего цикла
        if (!frozen)
        {
            var state = anim.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash == attackStateHash && !anim.IsInTransition(0) &&
                state.normalizedTime >= freezeAtNormalizedTime)
            {
                anim.speed = 0f;
                frozen = true;
            }
        }
    }

    void Unfreeze()
    {
        if (frozen)
        {
            anim.speed = 1f;
            frozen = false;
        }
    }
}
