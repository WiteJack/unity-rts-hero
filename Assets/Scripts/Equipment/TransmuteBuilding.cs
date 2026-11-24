using UnityEngine;

/// <summary>
/// ОБНОВЛЕНИЕ 26 — ЗДАНИЕ ТРАНСМУТАЦИИ (по диздоку, ui3.png).
/// Повесить на объект здания трансмутации на карте. Герой-игрок входит в interactionRadius —
/// над героем появляется стрелочка и открывается TransmuteUI (трансмутация / слияние / покупка случайного).
/// Герой ушёл из радиуса — стрелочка и панель скрываются.
/// Приоритет (диздок): герой-игрок > кура (кура появится позже — тогда добавим выбором компонента).
/// Выделение и интерфейс — так же, как у магазина (ShopUI/ShopBuilding).
/// </summary>
public class TransmuteBuilding : MonoBehaviour
{
    [Header("Радиус торговли — подход героя включает здание трансмутации (по диздоку)")]
    [Tooltip("Расстояние от здания, при котором открывается интерфейс трансмутации")]
    public float interactionRadius = 6f;

    [Header("Стрелочка над героем (голубая — цвет трансмутации)")]
    public Color arrowColor = new Color(0.35f, 0.85f, 1f, 1f);
    public float arrowHeight = 2.3f;
    public float arrowSize = 36f;

    private HeroStats hero;
    private TextMesh arrow;
    private float nextCheck;
    private bool panelOpen;

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.2f;

        // Покупатель: герой-игрок (приоритет по диздоку; кура появится позже — тогда добавим выбором компонента)
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
            if (!panelOpen)
            {
                panelOpen = true;
                TransmuteUI.EnsureShown();
                Debug.Log($"[ТРАНСМУТАЦИЯ] Герой подошёл к зданию трансмутации ({interactionRadius} м) — интерфейс открыт.");
            }
            UpdateArrow(heroPos);
        }
        else
        {
            if (panelOpen)
            {
                panelOpen = false;
                TransmuteUI.Hide();
                Debug.Log("[ТРАНСМУТАЦИЯ] Герой ушёл от здания трансмутации — интерфейс закрыт.");
            }
            if (arrow != null && arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false);
        }
    }

    private void UpdateArrow(Vector3 heroPos)
    {
        if (arrow == null)
        {
            var go = new GameObject("TransmuteArrow");
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
        Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.8f);
        var c = transform.position; c.y += 0.05f;
        Gizmos.DrawWireSphere(c, interactionRadius);
    }
}
