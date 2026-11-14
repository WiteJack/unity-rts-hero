using UnityEngine;

[RequireComponent(typeof(Light))]
public class DayNightTemperature : MonoBehaviour
{
    [Header("Настройки цикла дня и ночи")]
    [Tooltip("Длительность полного цикла (туда и обратно) в секундах. 300 сек = 5 минут")]
    public float fullCycleDuration = 300f;

    [Tooltip("Минимальная температура (тёплый свет)")]
    public float minTemperature = 1500f;

    [Tooltip("Максимальная температура (холодный свет)")]
    public float maxTemperature = 20000f;

    [Tooltip("С какого времени начать при старте (0 = с 1500K, 0.5 = с 20000K)")]
    [Range(0f, 1f)] public float startOffset = 0f;

    private Light dirLight;
    private float timer;

    void Start()
    {
        dirLight = GetComponent<Light>();
        
        // Включаем режим использования температуры в URP (на всякий случай, если не включён)
        dirLight.useColorTemperature = true;
        
        timer = startOffset * fullCycleDuration;
    }

    void Update()
    {
        if (dirLight == null || fullCycleDuration <= 0f) return;

        timer += Time.deltaTime;

        // PingPong плавно гоняет значение от 0 до 1 и обратно от 1 до 0 за fullCycleDuration секунд
        float halfCycle = fullCycleDuration * 0.5f;
        float pingPong = Mathf.PingPong(timer, halfCycle) / halfCycle;

        // SmoothStep делает переходы на пиках (у 1500 и 20000) мягкими, без резкого разворота
        float smoothT = Mathf.SmoothStep(0f, 1f, pingPong);

        dirLight.colorTemperature = Mathf.Lerp(minTemperature, maxTemperature, smoothT);
    }
}
