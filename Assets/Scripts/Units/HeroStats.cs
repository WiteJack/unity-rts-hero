using UnityEngine;

public class HeroStats : MonoBehaviour
{
    [Header("Атрибуты Героя (База 10)")]
    public float strength = 10f;       // 1 сила = 5 урона
    public float vitality = 10f;       // 1 живучесть = 25 HP
    public float endurance = 10f;      // 1 выносливость = 0.5 реген HP, +0.1 защита
    public float agility = 10f;        // 1 ловкость = +0.05 к скорости атаки
    public float wisdom = 10f;         // 1 мудрость = 15 маны
    public float willpower = 10f;      // 1 сила воли = +0.05 к скор. атаки, +0.5 реген маны

    [Header("Текущее состояние")]
    public float currentHP;
    public float maxHP;
    public float currentMana;
    public float maxMana;
    public bool isDead = false;

    [Header("Лимиты")]
    public float maxAttackSpeedCap = 4.0f; 

    private Animator anim;

    void Start()
    {
       anim = GetComponentInChildren<Animator>(true);
    
    // Принудительно рассчитываем максимальные статы перед заливкой
    CalculateStats();
    
    // Жестко выдаем полное здоровье и ману, игнорируя нули из инспектора
    currentHP = maxHP;
    currentMana = maxMana;
    isDead = false; 
    
    Debug.Log($"[СТАРТ] Здоровье Героя успешно инициализировано: {currentHP} / {maxHP}");
    }

    void OnValidate()
    {
        CalculateStats();
    }

    void Update()
    {
        if (isDead) return;

        float hpRegen = endurance * 0.5f;
        float manaRegen = willpower * 0.5f;

        currentHP = Mathf.Min(currentHP + hpRegen * Time.deltaTime, maxHP);
        currentMana = Mathf.Min(currentMana + manaRegen * Time.deltaTime, maxMana);
    }

    public void CalculateStats()
    {
        maxHP = vitality * 25f;
        maxMana = wisdom * 15f;
    }

    public float GetCalculatedDamage(out bool isCrit)
    {
        float baseDamage = strength * 5f;
        isCrit = false;

        float totalAttackSpeed = GetCurrentAttackSpeed();
        
        if (totalAttackSpeed > maxAttackSpeedCap)
        {
            float overflowSpeedStats = (totalAttackSpeed - maxAttackSpeedCap) / 0.05f;
            float critChance = overflowSpeedStats * 0.5f;

            if (Random.Range(0f, 100f) <= critChance)
            {
                isCrit = true;
                float critDamageMultiplier = 1.5f; 

                if (critChance > 100f)
                {
                    float overflowCritStats = (critChance - 100f) / 0.5f;
                    critDamageMultiplier += overflowCritStats * 0.005f; 
                }

                baseDamage *= critDamageMultiplier;
            }
        }

        return baseDamage;
    }

    public float GetCurrentAttackSpeed()
    {
        return 1.0f + (agility * 0.05f) + (willpower * 0.05f);
    }

    public float GetDefense()
    {
        return (endurance * 0.1f) + (vitality * 0.1f);
    }

    public void TakeDamage(float incomingDamage)
    {
        if (isDead) return;

        float finalDamage = incomingDamage - GetDefense();
        if (finalDamage < 10f) finalDamage = 10f; 

        currentHP -= finalDamage;
        Debug.Log($"<color=cyan>[ГЕРОЙ]</color> {gameObject.name} получил урон: {finalDamage}. Осталось HP: {currentHP}");

        if (currentHP <= 0) Die();
    }

    void Die()
    {
        isDead = true;
        currentHP = 0;
        if (anim != null) anim.SetTrigger("OnDeath");
        
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.isStopped = true;
        Debug.Log(gameObject.name + " ПОГИБ!");
    }

    public void PlayAttackAnimation()
    {
        if (isDead) return;
        if (anim != null)
        {
            float attackSpeed = GetCurrentAttackSpeed();
            float cappedSpeed = Mathf.Min(attackSpeed, maxAttackSpeedCap);

            anim.SetFloat("AttackSpeedMult", cappedSpeed);
            anim.SetTrigger("OnAttack");
        }
    }
}
