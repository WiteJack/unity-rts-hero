using System.Collections;
using UnityEngine;

[RequireComponent(typeof(UnitStats))]
public class BuildingDestruction : MonoBehaviour
{
    [Header("Настройки взрыва при разрушении")]
    [Tooltip("Необязательно: сюда можно перетащить свой префаб взрыва (если пусто — скрипт создаст вспышку сам)")]
    public GameObject customExplosionPrefab;

    [Tooltip("Размер стандартной вспышки взрыва")]
    public float explosionScale = 4.0f;

    [Tooltip("Цвет вспышки взрыва")]
    public Color explosionColor = new Color(1f, 0.45f, 0.05f, 1f);

    [Tooltip("Количество разлетающихся обломков при взрыве")]
    public int debrisCount = 8;

    private UnitStats myStats;
    private bool isDestroyed = false;

    void Start()
    {
        myStats = GetComponent<UnitStats>();
    }

    void Update()
    {
        if (isDestroyed || myStats == null) return;

        // Проверяем, упало ли здоровье до нуля или сработал флаг смерти в UnitStats
        if (myStats.isDead || myStats.currentHP.CompareTo(0f) != 1)
        {
            isDestroyed = true;
            TriggerBuildingDestruction();
        }
    }

    private void TriggerBuildingDestruction()
    {
        Vector3 boomPos = transform.position + Vector3.up * 1.0f;

        // 1. Если задан свой префаб партиклов — спавним его
        if (customExplosionPrefab != null)
        {
            GameObject fx = Instantiate(customExplosionPrefab, boomPos, Quaternion.identity);
            Destroy(fx, 3f);
        }
        else
        {
            // 2. Иначе создаём лёгкий встроенный эффект взрыва и обломков в URP
            SpawnBuiltInExplosion(boomPos);
        }

        // 3. Сразу выключаем коллайдеры и рендеры здания, чтобы зомби мгновенно переключились на следующую цель
        Collider[] cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols)
        {
            c.enabled = false;
        }

        Renderer[] rends = GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            r.enabled = false;
        }

        // Удаляем сам объект здания со сцены через небольшую паузу (чтобы корутина взрыва успела отработать)
        Destroy(gameObject, 0.5f);
    }

    private void SpawnBuiltInExplosion(Vector3 center)
    {
        // Создаём временный независимый объект для анимации взрыва
        GameObject fxHolder = new GameObject("BuildingExplosion_FX");
        fxHolder.transform.position = center;

        ExplosionRunner runner = fxHolder.AddComponent<ExplosionRunner>();
        runner.PlayExplosion(center, explosionScale, explosionColor, debrisCount);
    }
}

// Вспомогательный компонент, который плавно анимирует вспышку и разлёт обломков, а затем сам удаляется
public class ExplosionRunner : MonoBehaviour
{
    public void PlayExplosion(Vector3 center, float maxScale, Color color, int debrisCount)
    {
        StartCoroutine(ExplosionRoutine(center, maxScale, color, debrisCount));
    }

    private IEnumerator ExplosionRoutine(Vector3 center, float maxScale, Color color, int debrisCount)
    {
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

        Material boomMat = null;
        if (unlitShader != null)
        {
            boomMat = new Material(unlitShader);
            boomMat.SetColor("_BaseColor", color);
            boomMat.color = color;
        }

        // Главный огненный шар
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = center;
        sphere.transform.localScale = Vector3.one * 0.5f;
        Destroy(sphere.GetComponent<Collider>());

        Renderer sphereRend = sphere.GetComponent<Renderer>();
        if (sphereRend != null && boomMat != null)
        {
            sphereRend.material = boomMat;
            sphereRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Разлетающиеся кубики-обломки
        GameObject[] debris = new GameObject[debrisCount];
        Vector3[] velocities = new Vector3[debrisCount];

        for (int i = 0; i.CompareTo(debrisCount) == -1; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.transform.position = center + Random.insideUnitSphere * 0.5f;
            piece.transform.localScale = Vector3.one * Random.Range(0.25f, 0.5f);
            piece.transform.rotation = Random.rotation;
            Destroy(piece.GetComponent<Collider>());

            Renderer pRend = piece.GetComponent<Renderer>();
            if (pRend != null && boomMat != null)
            {
                pRend.material = boomMat;
                pRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Vector3 dir = (Random.insideUnitSphere + Vector3.up * 0.8f).normalized;
            velocities[i] = dir * Random.Range(4f, 9f);
            debris[i] = piece;
        }

        // Анимация расширения шара и разлёта обломков (0.35 секунды)
        float timer = 0f;
        float duration = 0.35f;

        while (timer.CompareTo(duration) == -1)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            if (sphere != null)
            {
                sphere.transform.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one * maxScale, t);
            }

            for (int i = 0; i.CompareTo(debrisCount) == -1; i++)
            {
                if (debris[i] != null)
                {
                    velocities[i] += Physics.gravity * Time.deltaTime;
                    debris[i].transform.position += velocities[i] * Time.deltaTime;
                    debris[i].transform.Rotate(Vector3.one * (360f * Time.deltaTime));
                    debris[i].transform.localScale = Vector3.Lerp(Vector3.one * 0.4f, Vector3.zero, t);
                }
            }

            yield return null;
        }

        // Чистим за собой все временные объекты и материал
        if (sphere != null) Destroy(sphere);
        for (int i = 0; i.CompareTo(debrisCount) == -1; i++)
        {
            if (debris[i] != null) Destroy(debris[i]);
        }
        if (boomMat != null) Destroy(boomMat);
        Destroy(gameObject);
    }
}
