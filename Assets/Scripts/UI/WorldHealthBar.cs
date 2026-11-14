using UnityEngine;
using UnityEngine.UI;

public class WorldHealthBar : MonoBehaviour
{
    [Header("Ссылки UI")]
    [SerializeField] private Image healthFillImage;

    [Header("Настройки цветов по слоям")]
    [Tooltip("Приятный жемчужно-белый цвет для Героя (не режет глаза на светлом фоне)")]
    public Color heroColor = new Color(0.957f, 0.965f, 0.976f, 1f); // #F4F6F9FF

    [Tooltip("Цвет союзников и зданий (#EFEFE9FF)")]
    public Color alliesColor = new Color(0.937f, 0.937f, 0.914f, 1f); // #EFEFE9FF

    [Tooltip("Цвет нейтралов")]
    public Color neutralColor = new Color(0.98f, 0.8f, 0.08f, 1f); // Жёлтый

    [Tooltip("Цвет противников")]
    public Color enemyColor = new Color(0.937f, 0.267f, 0.267f, 1f); // Красный

    [Header("Номера слоёв в проекте")]
    public int playerLayer = 6;
    public int alliesLayer = 7;
    public int neutralLayer = 8;
    public int enemyLayer = 9;

    private HeroStats targetHero;
    private UnitStats targetUnit;
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;

        // Ищем либо статы Героя, либо статы Юнита/Здания на родителе
        targetHero = GetComponentInParent<HeroStats>();
        targetUnit = GetComponentInParent<UnitStats>();

        if (targetHero == null && targetUnit == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // Определяем слой владельца полоски и красим заполнение
        Transform owner = (targetHero != null) ? targetHero.transform : targetUnit.transform;
        ApplyBarColorByLayer(owner.gameObject.layer);
    }

    private void ApplyBarColorByLayer(int layer)
    {
        if (healthFillImage == null) return;

        if (layer == playerLayer || targetHero != null)
        {
            healthFillImage.color = heroColor;
        }
        else if (layer == alliesLayer)
        {
            healthFillImage.color = alliesColor;
        }
        else if (layer == neutralLayer)
        {
            healthFillImage.color = neutralColor;
        }
        else if (layer == enemyLayer)
        {
            healthFillImage.color = enemyColor;
        }
    }

    private void LateUpdate()
    {
        if (healthFillImage == null) return;

        // 1. Обновление для Героя
        if (targetHero != null)
        {
            if (targetHero.isDead)
            {
                gameObject.SetActive(false);
                return;
            }

            // Берём текущее и максимальное здоровье из HeroStats
            float maxHp = Mathf.Max(1f, GetHeroMaxHP());
            float curHp = Mathf.Clamp(GetHeroCurrentHP(), 0f, maxHp);
            healthFillImage.fillAmount = curHp / maxHp;
        }
        // 2. Обновление для Мобов, Союзников и Зданий
        else if (targetUnit != null)
        {
            if (targetUnit.isDead || targetUnit.currentHP.CompareTo(0f) != 1)
            {
                gameObject.SetActive(false);
                return;
            }

            float maxHp = Mathf.Max(1f, targetUnit.maxHP);
            float curHp = Mathf.Clamp(targetUnit.currentHP, 0f, maxHp);
            healthFillImage.fillAmount = curHp / maxHp;
        }

        // Поворачиваем полоску к RTS-камере (Billboard эффект)
        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                mainCamera.transform.rotation * Vector3.up);
        }
    }

    // Вспомогательные методы чтения HP из HeroStats (учитывают разные возможные названия полей)
    private float GetHeroMaxHP()
    {
        var field = typeof(HeroStats).GetField("maxHP");
        if (field != null) return (float)field.GetValue(targetHero);

        var fieldCap = typeof(HeroStats).GetField("MaxHP");
        if (fieldCap != null) return (float)fieldCap.GetValue(targetHero);

        var prop = typeof(HeroStats).GetProperty("MaxHealth");
        if (prop != null) return (float)prop.GetValue(targetHero);

        return 100f;
    }

    private float GetHeroCurrentHP()
    {
        var field = typeof(HeroStats).GetField("currentHP");
        if (field != null) return (float)field.GetValue(targetHero);

        var fieldCap = typeof(HeroStats).GetField("CurrentHP");
        if (fieldCap != null) return (float)fieldCap.GetValue(targetHero);

        var prop = typeof(HeroStats).GetProperty("CurrentHealth");
        if (prop != null) return (float)prop.GetValue(targetHero);

        return 100f;
    }
}
