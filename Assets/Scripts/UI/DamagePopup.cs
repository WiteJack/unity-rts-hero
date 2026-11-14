using System.Collections.Generic;
using UnityEngine;

// Всплывающие цифры урона над целью.
// Вызов из любого места: DamagePopup.Spawn(позиция, урон, isCrit);
// При AoE-уроне просто вызывать Spawn для каждой задетой цели.
public class DamagePopup : MonoBehaviour
{
    [Header("Движение")]
    public float riseSpeed = 1.2f;        // скорость всплытия
    public float lifetime = 0.8f;         // время жизни
    public float fadeStartTime = 0.35f;   // когда начинается затухание

    [Header("Вид")]
    public int normalFontSize = 42;
    public int critFontSize = 60;
    public float characterSize = 0.12f;
    public Color normalColor = Color.white;
    public Color critColor = new Color(1f, 0.55f, 0f);   // оранжевый крит
    public float spawnJitter = 0.25f;     // случайный сдвиг, чтобы цифры не сливались

    private TextMesh textMesh;
    private float timer;
    private Color baseColor;

    private static readonly Queue<DamagePopup> pool = new Queue<DamagePopup>();
    private static Font cachedFont;

    public static void Spawn(Vector3 position, float amount, bool isCrit = false)
    {
        DamagePopup popup = GetFromPool();

        Vector3 jitter = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * popup.spawnJitter;
        popup.transform.position = position + jitter;
        popup.timer = 0f;
        popup.baseColor = isCrit ? popup.critColor : popup.normalColor;
        popup.textMesh.fontSize = isCrit ? popup.critFontSize : popup.normalFontSize;
        popup.textMesh.text = Mathf.RoundToInt(amount).ToString();
        popup.textMesh.color = popup.baseColor;
        popup.gameObject.SetActive(true);
    }

    private static DamagePopup GetFromPool()
    {
        while (pool.Count > 0)
        {
            DamagePopup pooled = pool.Dequeue();
            if (pooled != null) return pooled;
        }

        var go = new GameObject("DamagePopup");
        DamagePopup popup = go.AddComponent<DamagePopup>();
        popup.textMesh = go.AddComponent<TextMesh>();
        popup.textMesh.anchor = TextAnchor.MiddleCenter;
        popup.textMesh.alignment = TextAlignment.Center;
        popup.textMesh.characterSize = popup.characterSize;
        popup.textMesh.font = GetFont();
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = popup.textMesh.font.material;
        return popup;
    }

    private static Font GetFont()
    {
        if (cachedFont == null)
        {
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null)
                cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        return cachedFont;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }

        float alpha = 1f;
        if (timer >= fadeStartTime)
        {
            float fadeDuration = Mathf.Max(0.01f, lifetime - fadeStartTime);
            alpha = 1f - Mathf.Clamp01((timer - fadeStartTime) / fadeDuration);
        }
        textMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

        if (timer >= lifetime)
        {
            gameObject.SetActive(false);
            pool.Enqueue(this);
        }
    }
}
