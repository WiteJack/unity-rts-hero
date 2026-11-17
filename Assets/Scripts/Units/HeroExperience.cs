using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Опыт и уровень героя. Вешается на КОРЕНЬ героя (рядом с HeroStats), HeroStats НЕ трогаем.
///
/// Правила из диздока:
/// - Опыт до следующего уровня героя удваивается каждый уровень: 100, 200, 400, 800, 1600...
/// - За каждый уровень герой получает очки характеристик (pointsPerLevel, по умолчанию 1),
///   которые игрок распределяет сам (счётчик unspentPoints — распределение сделаем позже).
/// - При повышении уровня герой светится 2 секунды — эффект сделан КОДОМ (URP emission),
///   без ассетов и без партиклов.
///
/// Опыт приходит извне (ExperienceReward на мобах вызывает GainExperience), amount любой.
/// </summary>
public class HeroExperience : MonoBehaviour
{
    [Header("Уровень")]
    public int level = 1;

    [Tooltip("Максимальный уровень (0 = без ограничения)")]
    public int maxLevel = 0;

    [Header("Очки характеристик")]
    [Tooltip("Сколько нераспределённых очков герой получает за уровень")]
    public int pointsPerLevel = 1;

    [Tooltip("Нераспределённые очки поинтов (показываются в UI справа от полосы опыта)")]
    public int unspentPoints = 0;

    [Header("Опыт")]
    [Tooltip("Текущий накопленный опыт в пределах уровня")]
    public float currentXP = 0f;

    [Tooltip("Писать в консоль каждый приход опыта (для отладки полоски опыта)")]
    public bool logXPGain = false;

    [Header("Эффект повышения уровня (кодом, URP emission)")]
    public float glowDuration = 2f;
    public Color glowColor = new Color(1f, 0.85f, 0.3f);   // золотистое свечение
    public float glowIntensity = 1.5f;

    /// <summary>Сколько опыта нужно для следующего уровня (диздок: 100, 200, 400, 800...)</summary>
    public float XPToNextLevel
    {
        get { return 100f * Mathf.Pow(2f, level - 1); }
    }

    /// <summary>Прогресс до следующего уровня 0..1 (для полосы опыта в UI)</summary>
    public float XPProgress
    {
        get
        {
            float need = XPToNextLevel;
            return need > 0f ? Mathf.Clamp01(currentXP / need) : 0f;
        }
    }

    /// <summary>
    /// Начислить опыт. Можно вызывать из любого скрипта (ExperienceReward, квесты и т.д.).
    /// Если опыта хватает на несколько уровней — повышает сразу несколько.
    /// </summary>
    public void GainExperience(float amount)
    {
        if (amount <= 0f) return;
        if (maxLevel > 0 && level >= maxLevel) return;

        currentXP += amount;
        if (logXPGain)
            Debug.Log($"[ОПЫТ] {gameObject.name}: +{amount} → {currentXP}/{XPToNextLevel} (уровень {level})");

        // Повышаем уровень, пока хватает опыта (возможно несколько раз за раз)
        bool leveledUp = false;
        while (maxLevel <= 0 || level < maxLevel)
        {
            float need = XPToNextLevel;
            if (currentXP < need) break;

            currentXP -= need;
            level++;
            unspentPoints += pointsPerLevel;
            leveledUp = true;
            Debug.Log($"[ОПЫТ] {gameObject.name}: уровень {level}! Нераспределённых очков: {unspentPoints}");
        }

        if (leveledUp)
        {
            StartCoroutine(GlowRoutine());
        }
    }

    /// <summary>
    /// Свечение героя 2 секунды, сделано кодом: включаем emission на всех рендерерах
    /// модели (URP), потом возвращаем как было. Работает и с Mixamo-моделями.
    /// </summary>
    private IEnumerator GlowRoutine()
    {
        // Собираем рендереры и запоминаем исходные цвета свечения
        List<Renderer> renderers = new List<Renderer>(GetComponentsInChildren<Renderer>());
        List<Color> originalEmission = new List<Color>();
        List<bool> originalEmissionOn = new List<bool>();

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material mat = r.material; // инстанс материала — не портим ассет
            originalEmission.Add(mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black);
            originalEmissionOn.Add(mat.IsKeywordEnabled("_EMISSION"));

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", glowColor * glowIntensity);
            }
        }

        yield return new WaitForSeconds(glowDuration);

        // Возвращаем как было
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            Material mat = r.material;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", originalEmission[i]);
                if (originalEmissionOn[i]) mat.EnableKeyword("_EMISSION");
                else mat.DisableKeyword("_EMISSION");
            }
        }
    }
}
