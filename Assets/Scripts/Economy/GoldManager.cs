using UnityEngine;

public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance { get; private set; }

    [Header("Экономика игрока")]
    [Tooltip("Стартовое количество золота")]
    public int currentGold = 0;

    [Header("Настройки отображения сверху камеры")]
    public Vector2 boxSize = new Vector2(180f, 38f);
    public float topMargin = 15f;
    public int fontSize = 20;
    public Color goldTextColor = new Color(1f, 0.84f, 0.15f, 1f); // Золотой цвет
    public Color backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.82f);

    private Texture2D bgTex;
    private GUIStyle textStyle;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, Color.white);
        bgTex.Apply();
    }

    public void AddGold(int amount)
    {
        if (amount.CompareTo(0) != 1) return;
        currentGold += amount;
    }

    public bool SpendGold(int amount)
    {
        if (currentGold.CompareTo(amount) == -1) return false;
        currentGold -= amount;
        return true;
    }

    void OnGUI()
    {
        if (bgTex == null) return;

        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.alignment = TextAnchor.MiddleCenter;
            textStyle.fontSize = fontSize;
            textStyle.fontStyle = FontStyle.Bold;
        }

        // Позиция ровно сверху по центру экрана
        float xPos = (Screen.width - boxSize.x) * 0.5f;
        Rect bgRect = new Rect(xPos, topMargin, boxSize.x, boxSize.y);

        Color prevColor = GUI.color;

        // Тёмная подложка
        GUI.color = backgroundColor;
        GUI.DrawTexture(bgRect, bgTex);

        // Золотой текст с количеством монет
        GUI.color = Color.white;
        textStyle.normal.textColor = goldTextColor;
        GUI.Label(bgRect, "Золото: " + currentGold, textStyle);

        GUI.color = prevColor;
    }

    void OnDestroy()
    {
        if (bgTex != null) Destroy(bgTex);
    }
}
