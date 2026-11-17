using UnityEngine;
using UnityEngine.AI;

public class BattleAI : MonoBehaviour
{
    [Header("Настройки Боя")]
    public float attackRange = 2f;         
    public float scanRadius = 15f;         
    [HideInInspector] public float attackCooldown = 1.5f;    

    [Header("Эффекты удара")]
    [Tooltip("Показывать всплывающие цифры урона над целью")]
    public bool showDamagePopups = true;

    [Header("Настройки Анимации")]
    [Tooltip("Длина клипа атаки в секундах при множителе 1.0 (подстрой под длину клипа в окне Animation)")]
    public float baseAttackClipLength = 1.5f;

    private Transform currentTarget;
    private NavMeshAgent agent;
    private HeroMovement heroMovement;
    private Animator anim;
    private float nextAttackTime;
    private int myLayer;

    private HeroStats myHeroStats;
    private UnitStats myUnitStats;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();

        anim = GetComponentInChildren<Animator>();

        heroMovement = GetComponent<HeroMovement>();
        if (heroMovement == null) heroMovement = GetComponentInParent<HeroMovement>();

        myHeroStats = GetComponent<HeroStats>();
        if (myHeroStats == null) myHeroStats = GetComponentInParent<HeroStats>();

        myUnitStats = GetComponent<UnitStats>();
        if (myUnitStats == null) myUnitStats = GetComponentInParent<UnitStats>();

        myLayer = gameObject.layer;

        if (agent != null) agent.updateRotation = true; 
    }

    void Update()
    {
        if (myHeroStats != null && myHeroStats.isDead) return;
        if (myUnitStats != null && myUnitStats.isDead) return;

        // Передаем скорость бега для включения анимации ходьбы у моба
        if (myUnitStats != null && anim != null && agent != null)
        {
            anim.SetFloat("Speed", agent.velocity.magnitude);
        }

        if (heroMovement != null && agent != null && agent.hasPath)
        {
            currentTarget = null; 
            agent.updateRotation = true; 
            return;
        }

        if (currentTarget == null || IsTargetDead(currentTarget) || Vector3.Distance(transform.position, currentTarget.position) > scanRadius)
        {
            currentTarget = FindNearestValidTarget();
            if (currentTarget == null && agent != null) agent.updateRotation = true;
        }

        if (currentTarget != null)
        {
            float distance = Vector3.Distance(transform.position, currentTarget.position);

            if (distance <= attackRange + 0.5f)
            {
                if (agent != null) agent.updateRotation = false;
                Vector3 lookDir = (currentTarget.position - transform.position).normalized;
                lookDir.y = 0; 
                if (lookDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookDir);
            }

            if (distance <= attackRange)
            {
                if (agent != null && agent.isOnNavMesh && !agent.isStopped) agent.ResetPath();
                
                if (Time.time >= nextAttackTime)
                {
                    float currentSpeed = 1f;
                    
                    // ГЕРОЙ: Скорость зависит от атрибутов + синхронизация анимации
                    if (myHeroStats != null)
                    {
                        currentSpeed = Mathf.Min(myHeroStats.GetCurrentAttackSpeed(), myHeroStats.maxAttackSpeedCap);
                        attackCooldown = 1f / currentSpeed; 
                        nextAttackTime = Time.time + attackCooldown;

                        if (anim != null) anim.SetFloat("AttackSpeedMult", currentSpeed * baseAttackClipLength);
                        myHeroStats.PlayAttackAnimation();
                    }
                    // МОБ/ЗОМБИ: Фиксированная скорость + синхронизация анимации
                    else if (myUnitStats != null)
                    {
                        currentSpeed = myUnitStats.attackSpeed;
                        attackCooldown = 1f / currentSpeed; 
                        nextAttackTime = Time.time + attackCooldown;

                        if (anim != null) anim.SetFloat("AttackSpeedMult", currentSpeed * baseAttackClipLength);
                        myUnitStats.PlayAttackAnimation();
                    }
                }
            }
            else
            {
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.updateRotation = true; 
                    agent.SetDestination(currentTarget.position);
                }
            }
        }
    }

    // Единый ивент удара в кадре анимации для Героя и Моба
    public void OnAttackHitEvent()
    {
        if (transform.parent != null && transform.parent.GetComponent<BattleAI>() != null && transform.parent.gameObject != this.gameObject)
        {
            transform.parent.GetComponent<BattleAI>().OnAttackHitEvent();
            return;
        }

        if ((myHeroStats != null && !myHeroStats.isDead) || (myUnitStats != null && !myUnitStats.isDead))
        {
            ApplyDamageToTarget();
        }
    }

    private void ApplyDamageToTarget()
    {
        if (currentTarget == null || IsTargetDead(currentTarget)) return;

        // Стрелок: урон наносит летящий снаряд (как у башни), а не мгновенный удар.
        // Если на юните есть ShooterProjectileAttack — отдаём выстрел ему и выходим.
        var projectileShooter = GetComponent<ShooterProjectileAttack>();
        if (projectileShooter != null && projectileShooter.enabled)
        {
            projectileShooter.LaunchProjectile(currentTarget);
            return;
        }

        var targetHeroStats = currentTarget.GetComponent<HeroStats>();
        if (targetHeroStats == null) targetHeroStats = currentTarget.GetComponentInParent<HeroStats>();

        var targetUnitStats = currentTarget.GetComponent<UnitStats>();
        if (targetUnitStats == null) targetUnitStats = currentTarget.GetComponentInParent<UnitStats>();

        bool isCrit = false;
        float generatedDamage = 0f;

        if (myHeroStats != null)
        {
            generatedDamage = myHeroStats.GetCalculatedDamage(out isCrit);
            if (isCrit) Debug.Log($"<color=yellow>КРИТ ГЕРОЯ!</color> {gameObject.name} ввалил критический урон!");
        }
        else if (myUnitStats != null)
        {
            generatedDamage = myUnitStats.baseDamage;
        }

        float dealtDamage = 0f;
        if (targetHeroStats != null) dealtDamage = targetHeroStats.TakeDamage(generatedDamage);
        if (targetUnitStats != null) dealtDamage = targetUnitStats.TakeDamage(generatedDamage);

        if (showDamagePopups && dealtDamage > 0f)
        {
            Vector3 popupPosition = currentTarget.position + Vector3.up * 1.5f;
            DamagePopup.Spawn(popupPosition, dealtDamage, isCrit);
        }
    }

    private bool IsTargetDead(Transform target)
    {
        var tHero = target.GetComponent<HeroStats>();
        if (tHero == null) tHero = target.GetComponentInParent<HeroStats>();
        if (tHero != null) return tHero.isDead;

        var tUnit = target.GetComponent<UnitStats>();
        if (tUnit == null) tUnit = target.GetComponentInParent<UnitStats>();
        if (tUnit != null) return tUnit.isDead;

        return true;
    }

    private Transform FindNearestValidTarget()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, scanRadius);
        Transform bestTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (var col in hitColliders)
        {
            if (col.gameObject == this.gameObject) continue;

            var targetHero = col.GetComponent<HeroStats>();
            if (targetHero == null) targetHero = col.GetComponentInParent<HeroStats>();

            var targetUnit = col.GetComponent<UnitStats>();
            if (targetUnit == null) targetUnit = col.GetComponentInParent<UnitStats>();

            if (targetHero == null && targetUnit == null) continue;
            if (targetHero != null && targetHero.isDead) continue;
            if (targetUnit != null && targetUnit.isDead) continue;

            int targetLayer = col.gameObject.layer;
            bool isValidEnemy = false;

            if (myLayer == 6) // PLAYER
            {
                if (targetLayer == 9 || targetLayer == 8) isValidEnemy = true;
            }
            else if (myLayer == 7) // ALLIES
            {
                if (targetLayer == 9) isValidEnemy = true;
            }
            else if (myLayer == 8) // NEUTRAL
            {
                if (targetLayer == 6) isValidEnemy = true;
            }
            else if (myLayer == 9) // ENEMY
            {
                if (targetLayer == 6 || targetLayer == 7) isValidEnemy = true;
            }

            if (isValidEnemy)
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestTarget = col.transform;
                }
            }
        }
        return bestTarget;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, scanRadius);
    }
}