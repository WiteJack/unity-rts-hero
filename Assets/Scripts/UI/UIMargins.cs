using UnityEngine;

/// <summary>
/// Единые отступы UI от краёв экрана (Обновление 38).
///
/// ПРОБЛЕМА: панели строились с жёсткими пиксельными смещениями (12/170/478 и т.п.) и
/// жили на РАЗНЫХ канвасах — часть на ConstantPixelSize (UICanvas_Overlay), часть на
/// ScaleWithScreenSize 1920x1080 (MainGame_HUD_Canvas). При смене разрешения эти
/// смещения перестают соответствовать реальным краям экрана — интерфейс «плывёт»:
/// уезжает за край или наезжает на соседние панели.
///
/// РЕШЕНИЕ (владелец, Обновление 38): у каждой панели — отступ 5% от КАЖДОГО края
/// экрана, считаемый от ТЕКУЩЕГО разрешения. UIMarginFitter ставит панель на этот
/// отступ и ПЕРЕПРИМЕНЯЕТ его при смене разрешения (проверка в Update) и при изменении
/// scaleFactor канваса.
///
/// ГРАБЛИ (canvas units): anchoredPosition измеряется в ЕДИНИЦАХ КАНВАСА, а не в
/// пикселях экрана: экранное смещение в пикселях = канвас-юниты × canvas.scaleFactor.
/// Поэтому отступ в пикселях делится на scaleFactor:
///   insetCanvasUnits = Screen.width * 0.05f / canvas.scaleFactor;
/// иначе на канвасах со скейлером отступ «раздувается» ровно во столько же раз.
/// </summary>
public static class UIMargins
{
    /// <summary>Отступ панелей от краёв экрана — 5% от разрешения (Обновление 38).</summary>
    public const float InsetFraction = 0.05f;
}

/// <summary>
/// Держит RectTransform на отступе N% от краёв экрана и переприменяет отступ при
/// смене разрешения / масштаба канваса. Пристыкованные края определяются по якорям:
/// anchor x == 1 — правый край, == 0 — левый; anchor y == 0 — низ, == 1 — верх;
/// центр (0.5) и растяжение (anchorMin != anchorMax) не трогаются.
/// Панель может быть пристыкована «над» другой панелью (стекинг), чтобы блоки не
/// наезжали друг на друга: отступ снизу = 5% + высота нижнего блока в пикселях + зазор.
/// Работает при условии, что pivot прижат к тому же краю (как во всех панелях проекта).
/// </summary>
public class UIMarginFitter : MonoBehaviour
{
    [Header("Отступы — доля от разрешения экрана (0 = край не контролируется)")]
    [Tooltip("Отступ слева, доля от ШИРИНЫ экрана. Применяется только при якоре у левого края (anchor x == 0).")]
    [Range(0f, 0.5f)] public float leftMargin = 0f;

    [Tooltip("Отступ справа, доля от ШИРИНЫ экрана. Применяется только при якоре у правого края (anchor x == 1).")]
    [Range(0f, 0.5f)] public float rightMargin = 0f;

    [Tooltip("Отступ сверху, доля от ВЫСОТЫ экрана. Применяется только при якоре у верхнего края (anchor y == 1).")]
    [Range(0f, 0.5f)] public float topMargin = 0f;

    [Tooltip("Отступ снизу, доля от ВЫСОТЫ экрана. Применяется только при якоре у нижнего края (anchor y == 0).")]
    [Range(0f, 0.5f)] public float bottomMargin = 0f;

    [Header("Стыковка над другими панелями (чтобы блоки не наезжали)")]
    [Tooltip("Держать панель НАД панелью магазина (ShopUI): отступ снизу = 5% + высота магазина + зазор.")]
    public bool stackAboveShop = false;

    [Tooltip("Держать панель НАД нижней панелью героя (HeroBottomPanelUI): отступ снизу = 5% + её высота + зазор.")]
    public bool stackAboveBottomStrip = false;

    [Tooltip("Держать панель НАД полосками героя (HeroStatsBarsUI): отступ снизу = 5% + высота полосок + зазор.")]
    public bool stackAboveHeroBars = false;

    [Tooltip("Зазор между панелями при стыковке, в пикселях экрана.")]
    public float stackGapPx = 8f;

    [Tooltip("Логировать первое применение отступа ([UI]).")]
    public bool logApply = false;

    private Canvas canvas;
    private int lastW, lastH;
    private float lastScale = -1f;
    private HeroStatsBarsUI barsCache;
    private bool stackUnresolved;
    private float retryTimer;
    private bool loggedOnce;

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        float sf = ScaleFactor();
        bool sizeChanged = Screen.width != lastW || Screen.height != lastH || !Mathf.Approximately(sf, lastScale);
        if (sizeChanged)
        {
            Apply(); // сменилось разрешение или масштаб канваса — отступ пересчитывается
            return;
        }
        if (stackUnresolved)
        {
            // нижняя панель ещё не построена — перепробуем через 0.5 с
            retryTimer -= Time.unscaledDeltaTime;
            if (retryTimer <= 0f)
            {
                retryTimer = 0.5f;
                Apply();
            }
        }
    }

    /// <summary>Поставить панель на отступ 5% от контролируемых краёв (вызывается и автоматически).</summary>
    public void Apply()
    {
        var rt = transform as RectTransform;
        if (rt == null) return;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();

        float sf = ScaleFactor();
        lastW = Screen.width;
        lastH = Screen.height;
        lastScale = sf;

        // ---------- стекинг: высота нижнего блока в ПИКСЕЛЯХ экрана ----------
        float stackPx = 0f;
        bool anyStack = stackAboveShop || stackAboveBottomStrip || stackAboveHeroBars;

        if (stackAboveShop && ShopUI.Instance != null)
            stackPx = Mathf.Max(stackPx, HeightPx(ShopUI.Instance.transform as RectTransform));

        if (stackAboveBottomStrip)
        {
            var strip = FindAnyObjectByType(typeof(HeroBottomPanelUI)) as HeroBottomPanelUI;
            if (strip != null)
                stackPx = Mathf.Max(stackPx, HeightPx(strip.RootRect));
        }

        if (stackAboveHeroBars)
        {
            if (barsCache == null)
                barsCache = FindAnyObjectByType(typeof(HeroStatsBarsUI)) as HeroStatsBarsUI;
            if (barsCache != null)
                stackPx = Mathf.Max(stackPx, HeightPx(barsCache.RootRect));
        }

        stackUnresolved = anyStack && stackPx <= 0f;

        // ---------- применяем отступ только к пристыкованным краям ----------
        float x = rt.anchoredPosition.x;
        float y = rt.anchoredPosition.y;

        bool hDocked = Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x);
        bool vDocked = Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y);

        if (hDocked && rightMargin > 0f && rt.anchorMin.x >= 0.5f)
            x = -Screen.width * rightMargin / sf;
        else if (hDocked && leftMargin > 0f && rt.anchorMin.x < 0.5f)
            x = Screen.width * leftMargin / sf;

        if (vDocked && topMargin > 0f && rt.anchorMin.y >= 0.5f)
            y = -Screen.height * topMargin / sf;
        else if (vDocked && bottomMargin > 0f && rt.anchorMin.y < 0.5f)
        {
            float marginPx = Screen.height * bottomMargin;
            if (stackPx > 0f) marginPx += stackPx + stackGapPx;
            y = marginPx / sf;
        }

        rt.anchoredPosition = new Vector2(x, y);

        if (logApply && !loggedOnce)
        {
            loggedOnce = true;
            Debug.Log("[UI] " + name + ": отступ " + Mathf.RoundToInt(UIMargins.InsetFraction * 100f) +
                      "% от краёв экрана применён (экран " + Screen.width + "x" + Screen.height +
                      ", scaleFactor " + sf.ToString("0.###") + ", стек снизу " + Mathf.RoundToInt(stackPx) + "px) — Обновление 38.");
        }
    }

    private float ScaleFactor()
    {
        return (canvas != null && canvas.scaleFactor > 0f) ? canvas.scaleFactor : 1f;
    }

    /// <summary>Высота панели в ПИКСЕЛЯХ экрана: sizeDelta (канвас-юниты) × scaleFactor её канваса.</summary>
    private static float HeightPx(RectTransform rt)
    {
        if (rt == null) return 0f;
        var c = rt.GetComponentInParent<Canvas>();
        float sf = (c != null && c.scaleFactor > 0f) ? c.scaleFactor : 1f;
        return rt.sizeDelta.y * sf;
    }
}
