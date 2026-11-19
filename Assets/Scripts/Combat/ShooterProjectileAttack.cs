using System.Collections;
using UnityEngine;

/// <summary>
/// Стрельба снарядами для стрелка (Allies_Shooter) — как у башни (TowerAttack).
/// Вешается на КОРЕНЬ префаба рядом с BattleAI и UnitStats.
///
/// Как работает: BattleAI в кадр выстрела (анимационное событие OnAttackHitEvent)
/// вызывает LaunchProjectile(цель) — вместо мгновенного урона создаётся летящий
/// снаряд (сфера + светящийся шлейф, как у TowerAttack), урон наносится в момент
/// попадания в цель. Темп выстрелов и цикл анимации обеспечивает ShooterAttackLoop.
///
/// Временное решение (сделать чтобы работало): урон и попапы пока дублируют логику
/// BattleAI/TowerAttack — позже привести к общему механизму.
/// Героя не затрагивает: работает только при наличии UnitStats.
/// </summary>
public class ShooterProjectileAttack : MonoBehaviour
{
    [Header("Снаряд (как у башни)")]
    [Tooltip("Высота точки вылета снаряда от центра стрелка")]
    public float fireHeightOffset = 1.5f;

    [Tooltip("Скорость полёта снаряда")]
    public float projectileSpeed = 18f;

    [Tooltip("Размер шарика-снаряда")]
    public float projectileSize = 0.15f;

    [Tooltip("Цвет снаряда и шлейфа")]
    public Color projectileColor = Color.yellow;

    private UnitStats myStats;
    private BattleAI battleAI;
    private Material cachedProjectileMat;

    void Awake()
    {
        myStats = GetComponent<UnitStats>();
        if (myStats == null) myStats = GetComponentInParent<UnitStats>();

        battleAI = GetComponent<BattleAI>();
        if (battleAI == null) battleAI = GetComponentInParent<BattleAI>();

        // Один общий URP Unlit материал на все снаряды стрелка (как у башни)
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

        if (unlitShader != null)
        {
            cachedProjectileMat = new Material(unlitShader);
            cachedProjectileMat.SetColor("_BaseColor", projectileColor);
            cachedProjectileMat.color = projectileColor;
        }

        // Не наш комплект (герой с HeroStats или нет BattleAI) — выключаемся
        if (myStats == null || battleAI == null)
            enabled = false;
    }

    /// <summary>
    /// Вызывается из BattleAI.ApplyDamageToTarget() в кадре выстрела.
    /// </summary>
    public void LaunchProjectile(Transform target)
    {
        if (myStats.isDead || target == null) return;
        StartCoroutine(MoveAndHitRoutine(CreateProjectileVisual(), target));
    }

    private GameObject CreateProjectileVisual()
    {
        Vector3 startPos = transform.position + Vector3.up * fireHeightOffset;

        GameObject proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        proj.name = "ShooterBullet_Visual";
        proj.transform.position = startPos;
        proj.transform.localScale = Vector3.one * projectileSize;

        // Убираем коллайдер, чтобы снаряд не толкал юнитов
        Collider projCol = proj.GetComponent<Collider>();
        if (projCol != null) Destroy(projCol);

        Renderer rend = proj.GetComponent<Renderer>();
        if (rend != null && cachedProjectileMat != null)
        {
            rend.material = cachedProjectileMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Светящийся шлейф полёта (как у башни)
        TrailRenderer trail = proj.AddComponent<TrailRenderer>();
        trail.time = 0.15f;
        trail.startWidth = projectileSize * 0.8f;
        trail.endWidth = 0f;
        if (cachedProjectileMat != null) trail.material = cachedProjectileMat;
        trail.startColor = projectileColor;
        trail.endColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0f);

        return proj;
    }

    private IEnumerator MoveAndHitRoutine(GameObject proj, Transform target)
    {
        Vector3 lastKnownPos = target.position + Vector3.up * 1f;

        while (proj != null)
        {
            if (target != null && !IsTargetDead(target))
                lastKnownPos = target.position + Vector3.up * 1f;

            proj.transform.position = Vector3.MoveTowards(proj.transform.position, lastKnownPos, projectileSpeed * Time.deltaTime);

            if (Vector3.Distance(proj.transform.position, lastKnownPos) <= 0.25f)
            {
                OnProjectileImpact(target);
                Destroy(proj);
                yield break;
            }

            yield return null;
        }
    }

    private void OnProjectileImpact(Transform directTarget)
    {
        if (directTarget == null || IsTargetDead(directTarget)) return;

        float damage = myStats.baseDamage;
        float dealtDamage = 0f;

        var targetUnit = directTarget.GetComponentInParent<UnitStats>();
        if (targetUnit != null && !targetUnit.isDead)
            dealtDamage = targetUnit.TakeDamage(damage);

        var targetHero = directTarget.GetComponentInParent<HeroStats>();
        if (targetHero != null && !targetHero.isDead)
            dealtDamage = targetHero.TakeDamage(damage);

        if (battleAI != null && battleAI.showDamagePopups && dealtDamage > 0f)
            DamagePopup.Spawn(directTarget.position + Vector3.up * 1.5f, dealtDamage, false);
    }

    private bool IsTargetDead(Transform target)
    {
        if (target == null) return true;

        var u = target.GetComponentInParent<UnitStats>();
        if (u != null) return u.isDead;

        var h = target.GetComponentInParent<HeroStats>();
        if (h != null) return h.isDead;

        return false;
    }
}
