using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class CameraHealthBars : MonoBehaviour
{
    [Header("Настройки отображения")]
    [Tooltip("Показывать полоски даже тем, у кого 100% ХП (над Героем показывается всегда)")]
    public bool showFullHealthBars = false;

    [Tooltip("Высота полоски над центром объекта (в метрах)")]
    public float unitHeightOffset = 2.2f;
    public float buildingHeightOffset = 3.5f;

    [Tooltip("Размеры полоски на экране (в пикселях)")]
    public Vector2 barSize = new Vector2(54f, 6f);

    [Header("Цвета полосок")]
    [Tooltip("Приятный жемчужно-белый цвет для Героя")]
    public Color heroColor = new Color(0.957f, 0.965f, 0.976f, 1f); // #F4F6F9FF

    [Tooltip("Цвет союзников и зданий (#EFEFE9FF)")]
    public Color alliesColor = new Color(0.937f, 0.937f, 0.914f, 1f); // #EFEFE9FF

    [Tooltip("Цвет нейтралов")]
    public Color neutralColor = new Color(0.98f, 0.8f, 0.08f, 1f); // Жёлтый

    [Tooltip("Цвет противников")]
    public Color enemyColor = new Color(0.937f, 0.267f, 0.267f, 1f); // Красный

    [Tooltip("Цвет тёмной подложки-рамки")]
    public Color backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.8f);

    [Header("Номера слоёв в проекте")]
    public int playerLayer = 6;
    public int alliesLayer = 7;
    public int neutralLayer = 8;
    public int enemyLayer = 9;

    private Camera cam;
    private Texture2D whiteTex;
    private float scanTimer = 0f;

    private class TrackedEntity
    {
        public Transform targetTransform;
        public HeroStats heroStats;
        public UnitStats unitStats;
        public bool isBuilding;
        public Color barColor;
    }

    private readonly Dictionary<Transform, TrackedEntity> trackedEntities = new Dictionary<Transform, TrackedEntity>();
    private readonly List<Transform> deadList = new List<Transform>();

    private FieldInfo heroMaxHpField;
    private FieldInfo heroCurHpField;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        // Создаём в памяти 1 пиксель белой текстуры для быстрой отрисовки всех полосок
        whiteTex = new Texture2D(1, 1);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();

        heroMaxHpField = typeof(HeroStats).GetField("maxHP") ?? typeof(HeroStats).GetField("MaxHP");
        heroCurHpField = typeof(HeroStats).GetField("currentHP") ?? typeof(HeroStats).GetField("CurrentHP");
    }

    void Update()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer.CompareTo(0f) != 1)
        {
            scanTimer = 0.25f;
            ScanEntities();
        }
    }

    private void ScanEntities()
    {
        HeroStats[] heroes = FindObjectsByType<HeroStats>();
        for (int i = 0; i.CompareTo(heroes.Length) == -1; i++)
        {
            HeroStats h = heroes[i];
            if (h == null || h.isDead) continue;

            if (!trackedEntities.ContainsKey(h.transform))
            {
                trackedEntities.Add(h.transform, new TrackedEntity
                {
                    targetTransform = h.transform,
                    heroStats = h,
                    unitStats = null,
                    isBuilding = false,
                    barColor = heroColor
                });
            }
        }

        UnitStats[] units = FindObjectsByType<UnitStats>();
        for (int i = 0; i.CompareTo(units.Length) == -1; i++)
        {
            UnitStats u = units[i];
            if (u == null || u.isDead || u.currentHP.CompareTo(0f) != 1) continue;

            if (!trackedEntities.ContainsKey(u.transform))
            {
                bool isBld = (u.GetComponent<UnityEngine.AI.NavMeshAgent>() == null);
                trackedEntities.Add(u.transform, new TrackedEntity
                {
                    targetTransform = u.transform,
                    heroStats = null,
                    unitStats = u,
                    isBuilding = isBld,
                    barColor = GetColorForLayer(u.gameObject.layer)
                });
            }
        }
    }

    private Color GetColorForLayer(int layer)
    {
        if (layer == playerLayer) return heroColor;
        if (layer == alliesLayer) return alliesColor;
        if (layer == neutralLayer) return neutralColor;
        if (layer == enemyLayer) return enemyColor;
        return alliesColor;
    }

    // Встроенный системный метод Unity для отрисовки UI прямо из кода без Canvas
    void OnGUI()
    {
        if (cam == null || whiteTex == null) return;
        if (Event.current.type != EventType.Repaint) return;

        deadList.Clear();
        Color prevColor = GUI.color;

        foreach (var kvp in trackedEntities)
        {
            TrackedEntity item = kvp.Value;

            if (item.targetTransform == null || !item.targetTransform.gameObject.activeInHierarchy)
            {
                deadList.Add(kvp.Key);
                continue;
            }

            float curHp = 0f;
            float maxHp = 1f;
            bool isDead = false;

            if (item.heroStats != null)
            {
                isDead = item.heroStats.isDead;
                maxHp = heroMaxHpField != null ? Mathf.Max(1f, (float)heroMaxHpField.GetValue(item.heroStats)) : 100f;
                curHp = heroCurHpField != null ? (float)heroCurHpField.GetValue(item.heroStats) : maxHp;
            }
            else if (item.unitStats != null)
            {
                isDead = item.unitStats.isDead || item.unitStats.currentHP.CompareTo(0f) != 1;
                maxHp = Mathf.Max(1f, item.unitStats.maxHP);
                curHp = item.unitStats.currentHP;
            }

            if (isDead)
            {
                deadList.Add(kvp.Key);
                continue;
            }

            float ratio = Mathf.Clamp01(curHp / maxHp);

            // Показываем полоску всегда для Героя, либо если включена галочка, либо когда юнит/здание ранены
            bool shouldShow = (item.heroStats != null) || showFullHealthBars || (ratio.CompareTo(0.999f) == -1);
            if (!shouldShow) continue;

            float hOffset = item.isBuilding ? buildingHeightOffset : unitHeightOffset;
            Vector3 worldPos = item.targetTransform.position + Vector3.up * hOffset;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            // Пропускаем тех, кто за спиной камеры или за краями экрана
            if (screenPos.z.CompareTo(0f) != 1) continue;
            if (screenPos.x.CompareTo(-50f) == -1 || screenPos.x.CompareTo(Screen.width + 50f) == 1) continue;

            // В OnGUI координата Y идёт сверху вниз, поэтому переворачиваем screenPos.y
            float guiX = screenPos.x;
            float guiY = Screen.height - screenPos.y;

            float width = item.isBuilding ? barSize.x * 1.4f : barSize.x;
            float height = item.isBuilding ? barSize.y + 1f : barSize.y;

            Rect bgRect = new Rect(guiX - width * 0.5f, guiY - height * 0.5f, width, height);
            Rect fillRect = new Rect(bgRect.x + 1f, bgRect.y + 1f, (width - 2f) * ratio, height - 2f);

            // 1. Рисуем тёмную подложку
            GUI.color = backgroundColor;
            GUI.DrawTexture(bgRect, whiteTex);

            // 2. Рисуем цветную полоску здоровья
            GUI.color = item.barColor;
            GUI.DrawTexture(fillRect, whiteTex);
        }

        GUI.color = prevColor;

        for (int i = 0; i.CompareTo(deadList.Count) == -1; i++)
        {
            trackedEntities.Remove(deadList[i]);
        }
    }

    void OnDestroy()
    {
        if (whiteTex != null) Destroy(whiteTex);
    }
}
