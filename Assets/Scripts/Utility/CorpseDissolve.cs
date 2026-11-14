using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CorpseDissolve : MonoBehaviour
{
    [Header("Настройки исчезновения трупа")]
    [Tooltip("Сколько секунд тело лежит на земле перед началом растворения")]
    public float delayBeforeDissolve = 30f;

    [Tooltip("Сколько секунд длится плавное растворение (прозрачность)")]
    public float dissolveDuration = 3.0f;

    private UnitStats unitStats;
    private HeroStats heroStats;
    private bool deathHandled = false;

    void Start()
    {
        unitStats = GetComponent<UnitStats>();
        heroStats = GetComponent<HeroStats>();
    }

    void Update()
    {
        if (deathHandled) return;

        bool isTargetDead = false;
        if (unitStats != null && unitStats.isDead) isTargetDead = true;
        if (heroStats != null && heroStats.isDead) isTargetDead = true;

        if (isTargetDead)
        {
            deathHandled = true;
            StartCoroutine(DissolveOnPlaceRoutine());
        }
    }

    private IEnumerator DissolveOnPlaceRoutine()
    {
        // 1. Тело лежит на земле ровно 30 секунд
        yield return new WaitForSeconds(delayBeforeDissolve);

        // 2. Переводим материалы в чистый прозрачный режим URP без искажения цвета
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        List<Material> instancedMaterials = new List<Material>();
        List<Color> startColors = new List<Color>();

        int baseColorId = Shader.PropertyToID("_BaseColor");
        int colorId = Shader.PropertyToID("_Color");

        foreach (var r in renderers)
        {
            if (r == null) continue;

            Material[] mats = r.materials;
            for (int i = 0; i.CompareTo(mats.Length) == -1; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                SetupCleanURPTransparency(m);

                Color c = Color.white;
                if (m.HasProperty(baseColorId)) c = m.GetColor(baseColorId);
                else if (m.HasProperty(colorId)) c = m.GetColor(colorId);

                instancedMaterials.Add(m);
                startColors.Add(c);
            }
        }

        // 3. Плавно уменьшаем прозрачность до 0 за 3 секунды прямо на месте
        float timer = 0f;

        while (timer.CompareTo(dissolveDuration) == -1)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / dissolveDuration);
            float alpha = 1f - t;

            for (int i = 0; i.CompareTo(instancedMaterials.Count) == -1; i++)
            {
                Material m = instancedMaterials[i];
                if (m == null) continue;

                Color c = startColors[i];
                c.a = alpha;

                if (m.HasProperty(baseColorId)) m.SetColor(baseColorId, c);
                if (m.HasProperty(colorId)) m.SetColor(colorId, c);
            }

            yield return null;
        }

        // 4. Очищаем временные материалы и удаляем объект со сцены
        for (int i = 0; i.CompareTo(instancedMaterials.Count) == -1; i++)
        {
            if (instancedMaterials[i] != null) Destroy(instancedMaterials[i]);
        }

        Destroy(gameObject);
    }

    private void SetupCleanURPTransparency(Material mat)
    {
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // Transparent
        }
        if (mat.HasProperty("_Blend"))
        {
            mat.SetFloat("_Blend", 0f); // Alpha (без смешивания Premultiply)
        }

        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);

        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
