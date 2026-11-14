using UnityEngine;

public class UnitStats : MonoBehaviour
{
    [Header("Настраиваемые характеристики моба")]
    public float maxHP = 150f;
    public float currentHP;
    public float baseDamage = 20f;
    public float defense = 5f;

    [Header("Фиксированные параметры по дидоку")]
    public float hpRegenPerSecond = 1f;    
    public float attackSpeed = 1f;          

    public bool isDead = false;
    private Animator anim;

    void Start()
    {
        anim = GetComponentInChildren<Animator>(true);
        currentHP = maxHP;
    }

    void Update()
    {
        if (isDead) return;

        currentHP = Mathf.Min(currentHP + hpRegenPerSecond * Time.deltaTime, maxHP);
    }

    public void TakeDamage(float incomingDamage)
    {
        if (isDead) return;

        float finalDamage = incomingDamage - defense;
        if (finalDamage < 10f) finalDamage = 10f; 

        currentHP -= finalDamage;
        Debug.Log($"<color=red>[МОБ]</color> {gameObject.name} получил урон: {finalDamage}. Осталось HP: {currentHP}");

        if (currentHP <= 0) Die();
    }

    void Die()
    {
        isDead = true;
        currentHP = 0;
        if (anim != null) anim.SetTrigger("OnDeath");

        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.isStopped = true;
        Debug.Log(gameObject.name + " уничтожен!");
    }

    public void PlayAttackAnimation()
    {
        if (isDead) return;
        if (anim != null)
        {
            anim.SetFloat("AttackSpeedMult", attackSpeed);
            anim.SetTrigger("OnAttack");
        }
    }
}
