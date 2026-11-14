using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(BattleAI))]
public class HeroTargetCircles : MonoBehaviour
{
    [Header("Размеры и внешний вид кругов")]
    [Tooltip("Радиус круга под ногами героя")]
    public float heroCircleRadius = 0.8f;

    [Tooltip("Радиус круга под ногами цели (врага или нейтрала)")]
    public float targetCircleRadius = 0.85f;

    [Tooltip("Толщина линии круга")]
    public float lineWidth = 0.08f;

    [Tooltip("Высота над землёй, чтобы круг не проваливался в текстуру пола")]
    public float groundOffset = 0.05f;

    [Header("Цвета кругов")]
    public Color heroColor = Color.white;
    public Color enemyColor = Color.red;
    public Color neutralColor = Color.yellow;

    [Header("Номера слоёв (как в твоём проекте)")]
    public int neutralLayer = 8;
    public int enemyLayer = 9;

    private BattleAI battleAI;
    private FieldInfo targetFieldInfo;

    private LineRenderer heroCircleLine;
    private LineRenderer targetCircleLine;
    private const int Segments = 48; // Плавность окружности

    void Start()
    {
        battleAI = GetComponent<BattleAI>();

        // Получаем доступ к текущему врагу из BattleAI без изменения самого файла BattleAI.cs
        if (battleAI != null)
        {
            targetFieldInfo = typeof(BattleAI).GetField("currentTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        // Создаём 2 визуальных кольца в сцене
        heroCircleLine = CreateCircleRenderer("Hero_SelectionCircle", heroColor);
        targetCircleLine = CreateCircleRenderer("Target_SelectionCircle", enemyColor);
        targetCircleLine.enabled = false;
    }

    void LateUpdate()
    {
        // 1. Всегда обновляем белый круг под героем
        if (heroCircleLine != null)
        {
            DrawCircle(heroCircleLine, transform.position, heroCircleRadius);
        }

        // 2. Проверяем текущую цель героя из BattleAI
        Transform currentTarget = GetCurrentTargetFromBattleAI();

        if (currentTarget != null && currentTarget.gameObject.activeInHierarchy && !IsTargetDead(currentTarget))
        {
            targetCircleLine.enabled = true;

            // Выбираем цвет: жёлтый для нейтралов (8), красный для врагов (9)
            int targetLayer = currentTarget.gameObject.layer;
            Color activeColor = (targetLayer == neutralLayer) ? neutralColor : enemyColor;

            targetCircleLine.startColor = activeColor;
            targetCircleLine.endColor = activeColor;

            DrawCircle(targetCircleLine, currentTarget.position, targetCircleRadius);
        }
        else
        {
            if (targetCircleLine != null)
            {
                targetCircleLine.enabled = false;
            }
        }
    }

    private Transform GetCurrentTargetFromBattleAI()
    {
        if (battleAI == null || targetFieldInfo == null) return null;
        return targetFieldInfo.GetValue(battleAI) as Transform;
    }

    private bool IsTargetDead(Transform target)
    {
        var unit = target.GetComponentInParent<UnitStats>();
        if (unit != null) return unit.isDead;

        var hero = target.GetComponentInParent<HeroStats>();
        if (hero != null) return hero.isDead;

        return false;
    }

    private LineRenderer CreateCircleRenderer(string objName, Color color)
    {
        GameObject circleObj = new GameObject(objName);
        // Не привязываем к дочерним объектам героя, чтобы круги не вращались вместе с моделью
        LineRenderer lr = circleObj.AddComponent<LineRenderer>();

        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = Segments;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        // Стандартный встроенный шейдер URP для цветных линий (работает без настройки материалов)
        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader != null)
        {
            lr.material = new Material(lineShader);
        }

        lr.startColor = color;
        lr.endColor = color;

        return lr;
    }

    private void DrawCircle(LineRenderer lr, Vector3 center, float radius)
    {
        float angleStep = 360f / Segments;
        float yPos = center.y + groundOffset;

        for (int i = 0; i < Segments; i++)
        {
            float angle = Mathf.Deg2Rad * (i * angleStep);
            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.z + Mathf.Sin(angle) * radius;
            lr.SetPosition(i, new Vector3(x, yPos, z));
        }
    }

    void OnDestroy()
    {
        // Удаляем круги со сцены, если герой удаляется
        if (heroCircleLine != null) Destroy(heroCircleLine.gameObject);
        if (targetCircleLine != null) Destroy(targetCircleLine.gameObject);
    }
}
