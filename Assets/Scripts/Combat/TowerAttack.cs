using System.Collections;
using UnityEngine;

[RequireComponent(typeof(UnitStats))]
public class TowerAttack : MonoBehaviour
{
    [Header("Основные настройки башни")]
    [Tooltip("Дальность стрельбы башни")]
    public float attackRange = 12f;

    [Tooltip("Высота точки вылета снаряда от центра башни")]
    public float fireHeightOffset = 2.5f;

    [Tooltip("Скорость полёта пули или ядра")]
    public float projectileSpeed = 18f;

    [Header("Тип атаки (Одиночная или Ядро по области)")]
    [Tooltip("Выключено = пуля в одну цель. Включено = ядро с уроном по области")]
    public bool isAreaDamage = false;

    [Tooltip("Радиус взрыва ядра (работает, если включено Is Area Damage)")]
    public float splashRadius = 3.5f;

    [Header("Внешний вид снаряда")]
    [Tooltip("Размер шарика (например: 0.25 для пули, 0.7 для ядра)")]
    public float projectileSize = 0.25f;
    public Color projectileColor = Color.yellow;

    [Header("Слои врагов")]
    public int enemyLayer = 9;
    public int neutralLayer = 8;

    private UnitStats myStats;
    private float nextAttackTime;
    private Material cachedProjectileMat;

    void Start()
    {
        myStats = GetComponent<UnitStats>();

        // Создаём один общий URP Unlit материал для снарядов башни (работает быстро даже при 500 врагах)
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

        if (unlitShader != null)
        {
            cachedProjectileMat = new Material(unlitShader);
            cachedProjectileMat.SetColor("_BaseColor", projectileColor);
            cachedProjectileMat.color = projectileColor;
        }
    }

    void Update()
    {
        if (myStats == null || myStats.isDead) return;

        if (Time.time.CompareTo(nextAttackTime) != -1)
        {
            Transform target = FindNearestTarget();
            if (target != null)
            {
                float speed = Mathf.Max(0.1f, myStats.attackSpeed);
                nextAttackTime = Time.time + (1f / speed);
                FireProjectile(target);
            }
        }
    }

    private Transform FindNearestTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange);
        Transform bestTarget = null;
        float closestDist = Mathf.Infinity;
        int myLayer = gameObject.layer;

        foreach (var col in hits)
        {
            if (col.gameObject == this.gameObject) continue;

            int targetLayer = col.gameObject.layer;
            bool isValidTarget = false;

            // Если башня союзная (Allies = 7) или игрока (6) -> бьёт врагов (9)
            if (myLayer == 7 || myLayer == 6)
            {
                if (targetLayer == enemyLayer) isValidTarget = true;
            }
            // Если башня вражеская (Enemy = 9) -> бьёт игрока (6) и союзников (7)
            else if (myLayer == 9)
            {
                if (targetLayer == 6 || targetLayer == 7) isValidTarget = true;
            }

            if (!isValidTarget) continue;

            UnitStats uStats = col.GetComponentInParent<UnitStats>();
            HeroStats hStats = col.GetComponentInParent<HeroStats>();

            if (uStats == null && hStats == null) continue;
            if (uStats != null && uStats.isDead) continue;
            if (hStats != null && hStats.isDead) continue;

            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist.CompareTo(closestDist) == -1)
            {
                closestDist = dist;
                bestTarget = (uStats != null) ? uStats.transform : hStats.transform;
            }
        }

        return bestTarget;
    }

    private void FireProjectile(Transform target)
    {
        Vector3 startPos = transform.position + Vector3.up * fireHeightOffset;

        // Создаём аккуратный кружок-сферу для выстрела
        GameObject proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        proj.name = isAreaDamage ? "CannonBall_Visual" : "Bullet_Visual";
        proj.transform.position = startPos;
        proj.transform.localScale = Vector3.one * projectileSize;

        // Удаляем лишний коллайдер у визуального снаряда, чтобы он не толкал юнитов
        Collider projCol = proj.GetComponent<Collider>();
        if (projCol != null) Destroy(projCol);

        Renderer rend = proj.GetComponent<Renderer>();
        if (rend != null && cachedProjectileMat != null)
        {
            rend.material = cachedProjectileMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Добавляем красивый светящийся шлейф (след полёта)
        TrailRenderer trail = proj.AddComponent<TrailRenderer>();
        trail.time = 0.15f;
        trail.startWidth = projectileSize * 0.8f;
        trail.endWidth = 0f;
        if (cachedProjectileMat != null) trail.material = cachedProjectileMat;
        trail.startColor = projectileColor;
        trail.endColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0f);

        StartCoroutine(MoveAndHitRoutine(proj, target, startPos));
    }

    private IEnumerator MoveAndHitRoutine(GameObject proj, Transform target, Vector3 startPos)
    {
        Vector3 lastKnownPos = (target != null) ? target.position + Vector3.up * 1f : startPos + transform.forward * attackRange;

        while (proj != null)
        {
            if (target != null && !IsTargetDead(target))
            {
                lastKnownPos = target.position + Vector3.up * 1f;
            }

            proj.transform.position = Vector3.MoveTowards(proj.transform.position, lastKnownPos, projectileSpeed * Time.deltaTime);

            float remainingDist = Vector3.Distance(proj.transform.position, lastKnownPos);
            if (remainingDist.CompareTo(0.25f) != 1)
            {
                OnProjectileImpact(lastKnownPos, target);
                Destroy(proj);
                yield break;
            }

            yield return null;
        }
    }

    private void OnProjectileImpact(Vector3 hitPos, Transform directTarget)
    {
        float damage = myStats.baseDamage;

        if (!isAreaDamage)
        {
            // Одиночный урон (shooterTower)
            if (directTarget != null && !IsTargetDead(directTarget))
            {
                DealDamageToTransform(directTarget, damage);
            }
        }
        else
        {
            // Урон по области (canonTower)
            Collider[] areaHits = Physics.OverlapSphere(hitPos, splashRadius);
            int myLayer = gameObject.layer;

            foreach (var col in areaHits)
            {
                int targetLayer = col.gameObject.layer;
                bool isValidTarget = (myLayer == 7 || myLayer == 6) ? (targetLayer == enemyLayer) : (targetLayer == 6 || targetLayer == 7);

                if (isValidTarget)
                {
                    DealDamageToCollider(col, damage);
                }
            }

            // Визуальная вспышка взрыва области
            StartCoroutine(SpawnExplosionVisual(hitPos));
        }
    }

    private void DealDamageToTransform(Transform target, float damage)
    {
        UnitStats uStats = target.GetComponentInParent<UnitStats>();
        if (uStats != null && !uStats.isDead)
        {
            uStats.TakeDamage(damage);
            return;
        }

        HeroStats hStats = target.GetComponentInParent<HeroStats>();
        if (hStats != null && !hStats.isDead)
        {
            hStats.TakeDamage(damage);
        }
    }

    private void DealDamageToCollider(Collider col, float damage)
    {
        UnitStats uStats = col.GetComponentInParent<UnitStats>();
        if (uStats != null && !uStats.isDead)
        {
            uStats.TakeDamage(damage);
            return;
        }

        HeroStats hStats = col.GetComponentInParent<HeroStats>();
        if (hStats != null && !hStats.isDead)
        {
            hStats.TakeDamage(damage);
        }
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

    private IEnumerator SpawnExplosionVisual(Vector3 center)
    {
        GameObject boom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        boom.name = "CannonExplosion_Visual";
        boom.transform.position = center;
        boom.transform.localScale = Vector3.one * 0.5f;

        Collider c = boom.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer r = boom.GetComponent<Renderer>();
        if (r != null && cachedProjectileMat != null)
        {
            r.material = cachedProjectileMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        float timer = 0f;
        float duration = 0.2f;
        while (timer.CompareTo(duration) == -1)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            boom.transform.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one * (splashRadius * 1.5f), t);
            yield return null;
        }

        Destroy(boom);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (isAreaDamage)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position + transform.forward * 4f, splashRadius);
        }
    }
}
