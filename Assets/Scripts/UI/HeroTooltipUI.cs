using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ОБНОВЛЕНИЕ 24: ВСПЛЫВАЮЩЕЕ ОКОШКО ОПИСАНИЯ (тултип) — обычная система RPG/стратегий:
/// навёл курсор на иконку скилла / шмота / уника → через 0.2 сек появляется окно описания,
/// убрал курсор — окно исчезает. Создаётся кодом (без префабов), поверх всего HUD,
/// клики сквозь себя НЕ ловит (все raycastTarget = false). Пока окно открыто — цифры в нём
/// ЖИВЫЕ: переотрисовываются 4 раза/сек от текущих статов героя.
///
/// Правила использования: на иконку вешается компонент TooltipHover (он ниже в этом файле)
/// с двумя функциями SourceTitle / BodyFn (текст собирается на месте, всегда свежий).
/// </summary>
public class HeroTooltipUI : MonoBehaviour
{
    private static HeroTooltipUI instance;

    private Canvas canvas;
    private RectTransform root;
    private Image bg;
    private Text title;
    private Text body;

    private TooltipHover currentHover;
    private Func<string> titleFn;
    private Func<string> bodyFn;
    private float lastRenderTime;
    private bool positioned;

    private const float WIDTH = 360f;          // ширина окна
    private const float PADDING = 10f;         // внутренние отступы
    private const float TITLE_HEIGHT = 24f;
    private const float REFRESH_INTERVAL = 0.25f; // живая переотрисовка раз в 0.25 сек

    /// <summary>Создать тултип под канвасом HUD (один на игру, idempotent).</summary>
    public static void Ensure(Canvas canvas)
    {
        if (canvas == null) return;
        if (instance != null) return;

        var go = new GameObject("HeroTooltip_Root", typeof(RectTransform));
        instance = go.AddComponent<HeroTooltipUI>();
        instance.canvas = canvas;
        instance.root = (RectTransform)go.transform;
        instance.root.SetParent(canvas.transform, false);
        instance.root.SetAsLastSibling(); // поверх всех элементов HUD
        instance.root.anchorMin = Vector2.zero;
        instance.root.anchorMax = Vector2.zero;
        instance.root.pivot = new Vector2(0f, 1f); // точка отсчёта — левый верх окна
        instance.Build();
        go.SetActive(false); // скрыт, пока не навели курсор
    }

    private void Build()
    {
        bg = gameObject.AddComponent<Image>();
        bg.sprite = WhiteSprite;
        bg.color = new Color(0.07f, 0.07f, 0.10f, 0.96f);
        bg.raycastTarget = false; // клики сквозь окно НЕ ловим

        title = MakeText("TooltipTitle", 15, new Color(1f, 0.85f, 0.35f));
        var tRect = title.rectTransform;
        tRect.anchorMin = new Vector2(0f, 1f); tRect.anchorMax = new Vector2(1f, 1f);
        tRect.pivot = new Vector2(0.5f, 1f);
        tRect.offsetMin = new Vector2(PADDING, -(PADDING + 2f + TITLE_HEIGHT));
        tRect.offsetMax = new Vector2(-PADDING, -(PADDING + 2f));
        title.alignment = TextAnchor.UpperLeft; // заголовок в 1-2 строки сверху

        body = MakeText("TooltipBody", 13, Color.white);
        var bRect = body.rectTransform;
        bRect.anchorMin = new Vector2(0f, 0f); bRect.anchorMax = new Vector2(1f, 1f);
        bRect.pivot = new Vector2(0.5f, 0.5f);
        bRect.offsetMin = new Vector2(PADDING, PADDING);
        bRect.offsetMax = new Vector2(-PADDING, -(PADDING + 4f + TITLE_HEIGHT));
        body.alignment = TextAnchor.UpperLeft;
    }

    /// <summary>Показать/перепоказать окно для иконки hover.</summary>
    public static void ShowFor(TooltipHover hover, Func<string> tFn, Func<string> bFn)
    {
        if (instance == null || hover == null) return;
        instance.currentHover = hover;
        instance.titleFn = tFn;
        instance.bodyFn = bFn;
        instance.positioned = false;
        if (!instance.gameObject.activeSelf) instance.gameObject.SetActive(true);
        instance.Render();
    }

    /// <summary>Скрыть окно (только если оно открыто именно для hover).</summary>
    public static void Hide(TooltipHover hover)
    {
        if (instance == null || instance.currentHover != hover) return;
        instance.currentHover = null;
        instance.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (currentHover == null || !currentHover.Hovered) return;
        // живая переотрисовка: цифры в окне всегда свежие (шмот/уровни/перезарядки меняются)
        if (Time.unscaledTime - lastRenderTime >= REFRESH_INTERVAL) Render();
    }

    private void Render()
    {
        if (titleFn == null || bodyFn == null) return;
        title.text = titleFn();
        body.text = bodyFn();

        // высота окна по содержимому
        float h = PADDING + 4f + TITLE_HEIGHT + body.preferredHeight + PADDING;
        root.sizeDelta = new Vector2(WIDTH, Mathf.Max(h, 40f));
        lastRenderTime = Time.unscaledTime;

        PositionNearMouse();
        positioned = true;
    }

    /// <summary>Окно появляется рядом с курсором, не вылезает за края экрана.</summary>
    private void PositionNearMouse()
    {
        if (positioned) return; // не дёргаем окно, курсор и так на иконке
        Vector2 mp = MouseScreen();
        float scale = canvas != null ? canvas.scaleFactor : 1f; // канвас может быть со скалером
        if (scale <= 0.0001f) scale = 1f;

        float x = mp.x / scale + 18f;
        float y = mp.y / scale - 12f;
        float h = root.sizeDelta.y;

        if (x + WIDTH > Screen.width / scale - 4f) x = mp.x / scale - 18f - WIDTH; // не влез справа — показываем слева от курсора
        x = Mathf.Clamp(x, 4f, Mathf.Max(4f, Screen.width / scale - WIDTH - 4f));
        y = Mathf.Clamp(y, h + 4f, Mathf.Max(h + 4f, Screen.height / scale - 4f)); // низ окна не ниже экрана

        root.anchoredPosition = new Vector2(x, y);
    }

    private static Vector2 MouseScreen()
    {
#if ENABLE_INPUT_SYSTEM
        var m = UnityEngine.InputSystem.Mouse.current;
        if (m != null) return m.position.ReadValue();
#endif
        var v = Input.mousePosition;
        return new Vector2(v.x, v.y);
    }

    private Text MakeText(string name, int fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        var t = go.AddComponent<Text>();
        t.font = font ?? (font = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font);
        t.fontSize = fontSize;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate; // высота окна считается по preferredHeight
        t.supportRichText = false;
        return t;
    }

    private Font font;

    private static Sprite _whiteSprite;
    private static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null)
                _whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            return _whiteSprite;
        }
    }
}

/// <summary>
/// ОБНОВЛЕНИЕ 24: вешается на иконку (кнопку навыка, слот шмота, уника).
/// 0.2 сек наведения → открывает окно описания HeroTooltipUI,
/// уход курсора → закрывает. Текст всегда собирается заново (живые цифры).
/// </summary>
public class TooltipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public const float DelaySeconds = 0.2f; // по запросу владельца: ~0.2 сек наведения

    public Func<string> TitleFn;
    public Func<string> BodyFn;

    public bool Hovered { get; private set; }

    private float enterAt;
    private bool shown;

    public void OnPointerEnter(PointerEventData eventData)
    {
        Hovered = true;
        shown = false;
        enterAt = Time.unscaledTime;
    }

    public void OnPointerExit(PointerEventData eventData) { Leave(); }

    private void Leave()
    {
        Hovered = false;
        if (shown) { shown = false; HeroTooltipUI.Hide(this); }
    }

    private void OnDisable() { Leave(); }

    private void Update()
    {
        if (!Hovered) return;
        if (!shown && Time.unscaledTime - enterAt >= DelaySeconds)
        {
            shown = true;
            HeroTooltipUI.ShowFor(this, TitleFn, BodyFn);
        }
    }
}
