using UnityEngine;
using System.Reflection;

/// <summary>
/// Непрерывная стрельба для стрелка (Allies_Shooter).
/// Вешается на КОРЕНЬ префаба рядом с BattleAI и UnitStats.
///
/// Что делает: пока у BattleAI есть живая цель в радиусе атаки — держит Animator
/// в состоянии атаки через bool-параметр (по умолчанию "IsAttacking"), не давая
/// ему откатываться в Idle между выстрелами. Урон наносит BattleAI через
/// анимационное событие OnAttackHitEvent, то есть один цикл клипа = один выстрел,
/// а темп цикла задаёт AttackSpeedMult.
///
/// Героя не затрагивает: скрипт автоматически отключается, если рядом нет UnitStats
/// (у героя HeroStats). Общие BattleAI/UnitStats/HeroStats не изменяются.
///
/// Требуемая настройка Animator (Shooter_controller):
/// 1. Параметр bool "IsAttacking".
/// 2. У клипа Shooter_Attack включён Loop Time (окно Animation, Import Settings).
/// 3. Переход Shooter_Attack -> Shooter_Idle: БЕЗ Has Exit Time, условие IsAttacking = false.
/// 4. Переходы Idle/Run -> Shooter_Attack: по триггеру OnAttack (как было).
/// </summary>
public class ShooterAttackLoop : MonoBehaviour
{
    [Tooltip("Имя bool-параметра Animator, который держит состояние атаки зацикленным")]
    public string attackingBoolName = "IsAttacking";

    private BattleAI battleAI;
    private UnitStats myStats;
    private Animator anim;
    private FieldInfo currentTargetField;

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

        if (battleAI == null || myStats == null || anim == null || currentTargetField == null)
        {
            // Не наш комплект (например, герой с HeroStats) — выключаемся, ничего не ломаем
            enabled = false;
        }
    }

    /// <summary>
    /// Именно LateUpdate: аниматор считается после всех Update, поэтому наше значение
    /// AttackSpeedMult перекрывает перезапись из UnitStats.PlayAttackAnimation(),
    /// и не нужно трогать общий код BattleAI/UnitStats.
    /// </summary>
    void LateUpdate()
    {
        if (myStats.isDead) return;

        bool inAttackRange = false;

        Transform target = currentTargetField.GetValue(battleAI) as Transform;
        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            inAttackRange = distance <= battleAI.attackRange;
        }

        anim.SetBool(attackingBoolName, inAttackRange);

        if (inAttackRange)
        {
            // Длительность одного цикла = длина клипа / AttackSpeedMult.
            // Если baseAttackClipLength в BattleAI равен реальной длине клипа атаки,
            // один цикл занимает ровно 1/attackSpeed секунд — темп стрельбы из UnitStats.
            anim.SetFloat("AttackSpeedMult", myStats.attackSpeed * battleAI.baseAttackClipLength);
        }
        else
        {
            // Гасим висящий триггер, чтобы после потери цели стрелок не дёрнулся в атаку из Idle
            anim.ResetTrigger("OnAttack");
        }
    }
}
