using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class HeroMovement : MonoBehaviour
{
    private NavMeshAgent agent;
    private Camera mainCamera;
    private Animator anim;
    private BattleAI battleAI;
    private FieldInfo targetFieldInfo;

    [Header("Спецэффект клика по земле")]
    public GameObject clickEffectPrefab;
    public float effectDestroyTime = 1f;

    [Header("Настройки клика по врагу")]
    public float flashDuration = 0.15f;
    public int neutralLayer = 8;
    public int enemyLayer = 9;

    [HideInInspector] public bool isMovingByOrder = false;
    [HideInInspector] public Vector3 lastOrderPoint;
    private float orderTime = 0f;
    private Transform clickedTarget = null;

    void Start()
    {
        anim = GetComponentInChildren<Animator>(true);
        agent = GetComponent<NavMeshAgent>();
        battleAI = GetComponent<BattleAI>();
        mainCamera = Camera.main;

        if (battleAI != null)
        {
            targetFieldInfo = typeof(BattleAI).GetField("currentTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            SendHeroToClick();
        }

        if (anim != null && agent != null)
        {
            anim.SetFloat("Speed", agent.velocity.magnitude);
        }

        if (isMovingByOrder && clickedTarget != null)
        {
            if (IsTargetDead(clickedTarget))
            {
                clickedTarget = null;
                isMovingByOrder = false;
                return;
            }

            lastOrderPoint = clickedTarget.position;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.SetDestination(lastOrderPoint);
            }

            float range = battleAI != null ? battleAI.attackRange : 2f;
            float distToEnemy = Vector3.Distance(transform.position, clickedTarget.position);
            if (distToEnemy.CompareTo(range) != 1)
            {
                AssignTargetToBattleAI(clickedTarget);
                clickedTarget = null;
                isMovingByOrder = false;
            }
            return;
        }

        if (isMovingByOrder && Time.time.CompareTo(orderTime + 0.2f) == 1 && agent != null && !agent.pathPending)
        {
            float distToPoint = Vector3.Distance(transform.position, lastOrderPoint);
            float stopDist = agent.stoppingDistance + 0.35f;
            if (distToPoint.CompareTo(stopDist) != 1)
            {
                isMovingByOrder = false;
            }
        }
    }

    void SendHeroToClick()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 500f))
        {
            if (agent == null || !agent.isOnNavMesh) return;

            Transform enemy = GetValidClickedEnemy(hit.collider);

            if (enemy != null)
            {
                clickedTarget = enemy;
                isMovingByOrder = true;
                orderTime = Time.time;
                lastOrderPoint = enemy.position;

                AssignTargetToBattleAI(enemy);
                StartCoroutine(FlashEnemyWhite(enemy));

                if (anim != null)
                {
                    anim.ResetTrigger("OnAttack");
                    anim.Play("Idle", 0, 0f);
                }

                agent.isStopped = false;
                agent.updateRotation = true;
                agent.SetDestination(lastOrderPoint);
                return;
            }

            clickedTarget = null;
            isMovingByOrder = true;
            orderTime = Time.time;
            lastOrderPoint = hit.point;

            if (anim != null)
            {
                anim.ResetTrigger("OnAttack");
                anim.Play("Idle", 0, 0f);
            }

            agent.isStopped = false;
            agent.updateRotation = true;
            agent.SetDestination(lastOrderPoint);

            if (clickEffectPrefab != null)
            {
                Vector3 spawnPos = hit.point + new Vector3(0f, 0.05f, 0f);
                GameObject effectInstance = Instantiate(clickEffectPrefab, spawnPos, Quaternion.identity);
                Destroy(effectInstance, effectDestroyTime);
            }
        }
    }

    private Transform GetValidClickedEnemy(Collider col)
    {
        if (col == null) return null;

        int layer = col.gameObject.layer;
        if (layer != enemyLayer && layer != neutralLayer) return null;

        UnitStats uStats = col.GetComponentInParent<UnitStats>();
        if (uStats != null && !uStats.isDead) return uStats.transform;

        HeroStats hStats = col.GetComponentInParent<HeroStats>();
        if (hStats != null && !hStats.isDead) return hStats.transform;

        return null;
    }

    private bool IsTargetDead(Transform target)
    {
        if (target == null) return true;
        UnitStats u = target.GetComponentInParent<UnitStats>();
        if (u != null) return u.isDead;
        HeroStats h = target.GetComponentInParent<HeroStats>();
        if (h != null) return h.isDead;
        return false;
    }

    private void AssignTargetToBattleAI(Transform target)
    {
        if (battleAI != null && targetFieldInfo != null)
        {
            targetFieldInfo.SetValue(battleAI, target);
        }
    }

    private IEnumerator FlashEnemyWhite(Transform enemy)
    {
        if (enemy == null) yield break;

        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) yield break;

        int baseColorId = Shader.PropertyToID("_BaseColor");
        int colorId = Shader.PropertyToID("_Color");

        List<MaterialPropertyBlock> originalBlocks = new List<MaterialPropertyBlock>();
        MaterialPropertyBlock whiteBlock = new MaterialPropertyBlock();

        foreach (var r in renderers)
        {
            if (r is LineRenderer) continue;

            MaterialPropertyBlock orig = new MaterialPropertyBlock();
            r.GetPropertyBlock(orig);
            originalBlocks.Add(orig);

            r.GetPropertyBlock(whiteBlock);
            whiteBlock.SetColor(baseColorId, Color.white * 2.5f);
            whiteBlock.SetColor(colorId, Color.white * 2.5f);
            r.SetPropertyBlock(whiteBlock);
        }

        yield return new WaitForSeconds(flashDuration);

        int idx = 0;
        foreach (var r in renderers)
        {
            if (r == null || r is LineRenderer) continue;
            if (idx.CompareTo(originalBlocks.Count) == -1)
            {
                r.SetPropertyBlock(originalBlocks[idx]);
                idx++;
            }
        }
    }
}
