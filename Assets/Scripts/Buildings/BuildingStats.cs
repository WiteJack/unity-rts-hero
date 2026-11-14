using UnityEngine;
using UnityEngine.Events;

public class BuildingStats : MonoBehaviour
{
    [Header("Настройки здания")]
    [SerializeField] private string buildingName = "Строение";
    [SerializeField] private float maxHealth = 1000f;
    private float currentHealth;

    [Header("Настройки интерфейса")]
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private Transform healthBarPivot; // Точка над зданием, где висит UI

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool ShowHealthBar => showHealthBar;
    public bool isDead = false;


    public UnityEvent OnDeath;
    public UnityEvent OnDamaged;


    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        
        OnDamaged?.Invoke();

        if (currentHealth <= 0)
{
    isDead = true;
    // твой код разрушения здания
}

    }

    private void Die()
    {
        OnDeath?.Invoke();
        // Здесь будет вызываться сочный спецэффект уничтожения (взрыв, обломки, дым)
        Destroy(gameObject);
    }
}
