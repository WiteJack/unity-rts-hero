using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    [Header("Базовые Атрибуты (из Дидока)")]
    public float strength = 10f;       
    public float vitality = 10f;       
    public float endurance = 10f;      
    public float agility = 10f;        
    public float wisdom = 10f;         
    public float willpower = 10f;      

    [Header("Текущее Состояние")]
    public float currentHP;
    public float maxHP;
    public float currentMana;
    public float maxMana;
    public bool isDead = false;

    [Header("Лимиты и боевые скрытые параметры")]
    public float maxAttackSpeedCap = 4.0f; 

    private Animator anim;

    void Start()
    {
        anim = GetComponentInChildren<Animator>(true);
        CalculateStats();
        currentHP = maxHP;
        currentMana = maxMana;
    }

    public void CalculateStats()
    {
        maxHP = vitality * 25f;
        maxMana = wisdom * 15f;
    }

    // Возвращаем честную формулу: 1 сила = 5 урона
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

    // Рассчитываем чистую скорость атаки (база 1.0 + 0.05 за Ловкость и Силу Воли)
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
        Debug.Log(gameObject.name + " получил урон: " + finalDamage + ". Осталось HP: " + currentHP);

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
            // Берем текущую скорость с учетом капа 4.0
            float attackSpeed = GetCurrentAttackSpeed();
            float cappedSpeed = Mathf.Min(attackSpeed, maxAttackSpeedCap);

            // Закидываем множитель скорости прямо в блок анимации!
            anim.SetFloat("AttackSpeedMult", cappedSpeed);
            anim.SetTrigger("OnAttack");
        }
    }
}
