using UnityEngine;

/// <summary>
/// ОБНОВЛЕНИЕ 25 — ЗДАНИЕ МАГАЗИНА (по диздоку).
/// Повесить на объект магазина на карте (можно на куб/здание). Когда герой-игрок входит в
/// interactionRadius — над героем появляется стрелочка и в правом нижнем углу открывается
/// интерфейс магазина (ShopUI: 16 ячеек, ряды = тир 1..4, цены = покупка ×2 за золото GoldManager).
/// Герой ушёл из радиуса — стрелочка и панель скрываются.
/// Приоритет (диздок): герой-игрок > кура. Скрипт куры пока не существует — добавишь компонент куры,
/// будет автоматически учтено (см. Update: сначала герой, иначе любой другой "покупатель" не учитывается).
/// </summary>
public class ShopBuilding : MonoBehaviour
{
    [Header("Радиус торговли — подход героя включает магазин (по диздоку)")]
    [Tooltip("Расстояние от здания, при котором открывается интерфейс магазина")]
    public float interactionRadius = 6f;

    [Header("Стрелочка над героем")]
    public Color arrowColor = new Color(1f, 0.85f, 0.35f, 1f);
    public float arrowHeight = 2.3f;
    public float arrowSize = 36f;

    private HeroStats hero;
    private TextMesh arrow;
    private float nextCheck;
    private bool shopOpen;

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.2f;

        // Покупатель: герой-игрок (приоритет по диздоку; кура появится позже — тогда добавим выбором компонента Courier)
        if (hero == null) hero = FindAnyObjectByType<HeroStats>();
        bool inRange = false;
        Vector3 heroPos = Vector3.zero;
        if (hero != null)
        {
            heroPos = hero.transform.position;
            float dx = heroPos.x - transform.position.x;
            float dz = heroPos.z - transform.position.z;
            inRange = (dx * dx + dz * dz) <= interactionRadius * interactionRadius;
        }

        if (inRange)
        {
            if (!shopOpen)
            {
                shopOpen = true;
                ShopUI.EnsureShown();
                Debug.Log($"[МАГАЗИН] Герой подошёл к магазину ({interactionRadius} м) — интерфейс открыт.");
            }
            UpdateArrow(heroPos);
        }
        else
        {
            if (shopOpen)
            {
                shopOpen = false;
                ShopUI.Hide();
                Debug.Log("[МАГАЗИН] Герой ушёл от магазина — интерфейс закрыт.");
            }
            if (arrow != null && arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false);
        }
    }

    private void UpdateArrow(Vector3 heroPos)
    {
        if (arrow == null)
        {
            var go = new GameObject("ShopArrow");
            arrow = go.AddComponent<TextMesh>();
            arrow.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arrow.GetComponent<MeshRenderer>().sharedMaterial = arrow.font.material;
            arrow.text = "▼";
            arrow.fontSize = 48;
            arrow.characterSize = 0.5f;
            arrow.anchor = TextAnchor.MiddleCenter;
            arrow.color = arrowColor;
            arrow.GetComponent<MeshRenderer>().sortingOrder = 100;
        }
        if (!arrow.gameObject.activeSelf) arrow.gameObject.SetActive(true);

        // небольшое покачивание вверх-вниз + биллборд к камере
        float bob = Mathf.Sin(Time.unscaledTime * 4f) * 0.15f;
        arrow.transform.position = heroPos + new Vector3(0f, arrowHeight + bob, 0f);
        var cam = Camera.main;
        if (cam != null) arrow.transform.rotation = Quaternion.LookRotation(arrow.transform.position - cam.transform.position);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.8f);
        var c = transform.position; c.y += 0.05f;
        Gizmos.DrawWireSphere(c, interactionRadius);
    }
}
