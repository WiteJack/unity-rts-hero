using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

public class TeleportSystem : MonoBehaviour
{
    [Header("Главные ссылки (Герой находится автоматически)")]
    [Tooltip("Можно оставить пустым — скрипт сам найдёт героя игрока на слое Player при старте")]
    public Transform heroTransform;

    [Tooltip("Перетащи сюда Main Camera (или оставь пустым — найдётся сама)")]
    public Transform mainCameraTransform;

    [Header("4 Точки телепорта на карте (Empty Points)")]
    public List<Transform> teleportPoints = new List<Transform>();

    [Header("Названия кнопок в интерфейсе")]
    public List<string> teleportNames = new List<string> { "ТП 1", "ТП 2", "ТП 3", "ТП 4" };

    [Header("Настройки перезарядки и панели")]
    [Tooltip("Общая перезарядка всех 4 телепортов в секундах (по ТЗ = 60 сек)")]
    public float cooldownDuration = 60f;

    [Tooltip("Слой героя игрока в проекте")]
    public int playerLayer = 6;

    [Tooltip("Отступ панели от левого края экрана в пикселях")]
    public float leftScreenMargin = 20f;

    [Tooltip("Размер каждой кнопки телепорта")]
    public Vector2 buttonSize = new Vector2(88f, 44f);

    private float cooldownRemaining = 0f;
    private readonly List<Button> uiButtons = new List<Button>();
    private readonly List<Text> buttonTexts = new List<Text>();
    private readonly List<Image> cooldownOverlays = new List<Image>();

    private NavMeshAgent heroAgent;
    private HeroMovement heroMovement;
    private HeroStats heroStats;

    void OnValidate()
    {
        if (teleportPoints == null) teleportPoints = new List<Transform>();
        while (teleportPoints.Count.CompareTo(4) == -1) teleportPoints.Add(null);
        if (teleportPoints.Count.CompareTo(4) == 1) teleportPoints.RemoveRange(4, teleportPoints.Count - 4);

        if (teleportNames == null) teleportNames = new List<string>();
        while (teleportNames.Count.CompareTo(4) == -1) teleportNames.Add("ТП " + (teleportNames.Count + 1));
        if (teleportNames.Count.CompareTo(4) == 1) teleportNames.RemoveRange(4, teleportNames.Count - 4);
    }

    void Start()
    {
        if (heroTransform == null)
        {
            FindPlayerHeroAutomatically();
        }

        if (heroTransform != null)
        {
            heroAgent = heroTransform.GetComponent(typeof(NavMeshAgent)) as NavMeshAgent;
            heroMovement = heroTransform.GetComponent(typeof(HeroMovement)) as HeroMovement;
            heroStats = heroTransform.GetComponent(typeof(HeroStats)) as HeroStats;
        }

        if (mainCameraTransform == null && Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }

        BuildLeftTeleportUI();
    }

    private void FindPlayerHeroAutomatically()
    {
        HeroStats[] allHeroes = FindObjectsByType(typeof(HeroStats)) as HeroStats[];
        if (allHeroes == null) return;

        for (int i = 0; i.CompareTo(allHeroes.Length) == -1; i++)
        {
            HeroStats h = allHeroes[i];
            if (h != null && h.gameObject.layer == playerLayer)
            {
                heroTransform = h.transform;
                return;
            }
        }

        if (allHeroes.Length.CompareTo(0) == 1 && allHeroes[0] != null)
        {
            heroTransform = allHeroes[0].transform;
        }
    }

    void Update()
    {
        if (heroTransform == null)
        {
            FindPlayerHeroAutomatically();
            if (heroTransform != null)
            {
                heroAgent = heroTransform.GetComponent(typeof(NavMeshAgent)) as NavMeshAgent;
                heroMovement = heroTransform.GetComponent(typeof(HeroMovement)) as HeroMovement;
                heroStats = heroTransform.GetComponent(typeof(HeroStats)) as HeroStats;
            }
        }

        if (cooldownRemaining.CompareTo(0f) == 1)
        {
            cooldownRemaining -= Time.deltaTime;
            if (cooldownRemaining.CompareTo(0f) == -1) cooldownRemaining = 0f;

            float fillRatio = cooldownRemaining / Mathf.Max(0.1f, cooldownDuration);
            int secondsLeft = Mathf.CeilToInt(cooldownRemaining);

            for (int i = 0; i.CompareTo(uiButtons.Count) == -1; i++)
            {
                if (uiButtons[i] != null) uiButtons[i].interactable = false;
                if (cooldownOverlays[i] != null) cooldownOverlays[i].fillAmount = fillRatio;
                if (buttonTexts[i] != null) buttonTexts[i].text = secondsLeft + "с";
            }
        }
        else
        {
            for (int i = 0; i.CompareTo(uiButtons.Count) == -1; i++)
            {
                bool hasPoint = (i.CompareTo(teleportPoints.Count) == -1 && teleportPoints[i] != null);
                if (uiButtons[i] != null) uiButtons[i].interactable = hasPoint;
                if (cooldownOverlays[i] != null) cooldownOverlays[i].fillAmount = 0f;
                if (buttonTexts[i] != null) buttonTexts[i].text = teleportNames[i];
            }
        }
    }

    public void UseTeleport(int index)
    {
        if (index.CompareTo(0) == -1 || index.CompareTo(3) == 1) return;
        if (cooldownRemaining.CompareTo(0f) == 1) return;
        if (heroTransform == null || teleportPoints[index] == null) return;
        if (heroStats != null && heroStats.isDead) return;

        Vector3 targetPos = teleportPoints[index].position;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(targetPos, out navHit, 5f, NavMesh.AllAreas))
        {
            targetPos = navHit.position;
        }

        Vector3 cameraOffset = Vector3.zero;
        if (mainCameraTransform != null)
        {
            cameraOffset = mainCameraTransform.position - heroTransform.position;
        }

        if (heroMovement != null)
        {
            heroMovement.isMovingByOrder = false;
        }

        if (heroAgent != null && heroAgent.isOnNavMesh)
        {
            heroAgent.ResetPath();
            heroAgent.velocity = Vector3.zero;
            heroAgent.Warp(targetPos);
        }
        else
        {
            heroTransform.position = targetPos;
        }

        if (mainCameraTransform != null)
        {
            mainCameraTransform.position = heroTransform.position + cameraOffset;
        }

        cooldownRemaining = cooldownDuration;
    }

    private void BuildLeftTeleportUI()
    {
        Canvas canvas = GetComponentInParent(typeof(Canvas)) as Canvas;
        if (canvas == null)
        {
            canvas = FindAnyObjectByType(typeof(Canvas)) as Canvas;
        }

        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("MainGame_HUD_Canvas");
            canvas = canvasObj.AddComponent(typeof(Canvas)) as Canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent(typeof(CanvasScaler)) as CanvasScaler;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObj.AddComponent(typeof(GraphicRaycaster));
        }

        if (FindAnyObjectByType(typeof(UnityEngine.EventSystems.EventSystem)) == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent(typeof(UnityEngine.EventSystems.EventSystem));
            esObj.AddComponent(typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        GameObject panelObj = new GameObject("TeleportButtons_Panel");
        panelObj.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panelObj.AddComponent(typeof(RectTransform)) as RectTransform;
        panelRect.anchorMin = new Vector2(0f, 0.5f);
        panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.anchoredPosition = new Vector2(leftScreenMargin, 0f);
        panelRect.sizeDelta = new Vector2(buttonSize.x, (buttonSize.y + 10f) * 4f);

        VerticalLayoutGroup layout = panelObj.AddComponent(typeof(VerticalLayoutGroup)) as VerticalLayoutGroup;
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Font defaultFont = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font;

        uiButtons.Clear();
        buttonTexts.Clear();
        cooldownOverlays.Clear();

        for (int i = 0; i.CompareTo(4) == -1; i++)
        {
            int capturedIndex = i;

            GameObject btnObj = new GameObject("Teleport_Btn_" + (i + 1));
            btnObj.transform.SetParent(panelObj.transform, false);

            LayoutElement le = btnObj.AddComponent(typeof(LayoutElement)) as LayoutElement;
            le.preferredWidth = buttonSize.x;
            le.preferredHeight = buttonSize.y;

            Image bgImg = btnObj.AddComponent(typeof(Image)) as Image;
            bgImg.color = new Color(0.14f, 0.17f, 0.22f, 0.92f);

            Button btn = btnObj.AddComponent(typeof(Button)) as Button;
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.18f, 0.22f, 0.28f, 1f);
            colors.highlightedColor = new Color(0.28f, 0.36f, 0.48f, 1f);
            colors.pressedColor = new Color(0.12f, 0.15f, 0.2f, 1f);
            colors.disabledColor = new Color(0.1f, 0.1f, 0.12f, 0.7f);
            btn.colors = colors;
            btn.onClick.AddListener(() => UseTeleport(capturedIndex));

            GameObject cdObj = new GameObject("CooldownOverlay");
            cdObj.transform.SetParent(btnObj.transform, false);
            RectTransform cdRect = cdObj.AddComponent(typeof(RectTransform)) as RectTransform;
            cdRect.anchorMin = Vector2.zero;
            cdRect.anchorMax = Vector2.one;
cdRect.offsetMin = Vector2.zero;
cdRect.offsetMax = Vector2.zero;
Image cdImg = cdObj.AddComponent(typeof(Image)) as Image;
cdImg.color = new Color(0.05f, 0.05f, 0.08f, 0.65f);
cdImg.type = Image.Type.Filled;
cdImg.fillMethod = Image.FillMethod.Vertical;
cdImg.fillOrigin = (int)Image.OriginVertical.Bottom;
cdImg.fillAmount = 0f;
cdImg.raycastTarget = false;
GameObject txtObj = new GameObject("Label");
txtObj.transform.SetParent(btnObj.transform, false);
RectTransform txtRect = txtObj.AddComponent(typeof(RectTransform)) as RectTransform;
txtRect.anchorMin = Vector2.zero;
txtRect.anchorMax = Vector2.one;
txtRect.offsetMin = Vector2.zero;
txtRect.offsetMax = Vector2.zero;
Text txt = txtObj.AddComponent(typeof(Text)) as Text;
txt.font = defaultFont;
txt.fontSize = 16;
txt.fontStyle = FontStyle.Bold;
txt.alignment = TextAnchor.MiddleCenter;
txt.color = new Color(0.93f, 0.94f, 0.96f, 1f);
txt.text = teleportNames[i];
txt.raycastTarget = false;
uiButtons.Add(btn);
buttonTexts.Add(txt);
cooldownOverlays.Add(cdImg);
}
}
}