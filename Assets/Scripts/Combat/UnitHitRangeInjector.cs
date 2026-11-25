using UnityEngine;

/// <summary>
/// ОБНОВЛЕНИЕ 28 (A2): инжектор максимальной дальности удара ДЛЯ ВСЕХ юнитов.
/// Проблема: противники наносили урон по кадру анимации, даже когда цель успевала выйти
/// далеко за пределы их радиуса атаки («атака 2 — а удар проходит с 5 метров»).
/// Решение: BattleAI теперь сверяет дистанцию до цели ПЕРЕД нанесением удара.
/// Лимиты задаёт этот компонент (достаточно ОДНОГО в сцене):
///   ближники (юниты без ShooterProjectileAttack) — meleeMaxRange (по умолчанию 3),
///   дальники (юниты с ShooterProjectileAttack)   — rangedMaxRange (по умолчанию 15).
/// Лимиты хранятся в статики BattleAI (defaultHitDistance / defaultRangedHitDistance),
/// поэтому автоматически действуют и на юниты, заспавненные ПОСЛЕ применения —
/// они читают статик в момент удара, отдельный обход не нужен.
/// Куда вешать: любой постоянный объект сцены (например, GameObject рядом с GameSpawner).
/// </summary>
public class UnitHitRangeInjector : MonoBehaviour
{
    [Tooltip("Максимальная дальность удара БЛИЖНИКА (юнит без ShooterProjectileAttack), м. 0 = ближникам без лимита.")]
    public float meleeMaxRange = 3f;

    [Tooltip("Максимальная дальность удара ДАЛЬНИКА (юнит с ShooterProjectileAttack), м. 0 = дальникам без лимита.")]
    public float rangedMaxRange = 15f;

    [Tooltip("Применить лимиты всем юнитам при запуске сцены")]
    public bool applyOnStart = true;

    void Start()
    {
        if (applyOnStart) Apply();
    }

    /// <summary>Выставить лимиты ВСЕМ юнитам прямо сейчас (статик BattleAI — действует и на будущие спавны).</summary>
    [ContextMenu("Применить ко всем юнитам сейчас")]
    public void Apply()
    {
        BattleAI.defaultHitDistance = meleeMaxRange > 0f ? meleeMaxRange : -1f;
        BattleAI.defaultRangedHitDistance = rangedMaxRange > 0f ? rangedMaxRange : -1f;

        Debug.Log($"[ДАЛЬНОСТЬ] Инжектор применён ко всем юнитам: ближники ≤ {meleeMaxRange:0.#} м, дальники ≤ {rangedMaxRange:0.#} м. Лимиты читаются каждым BattleAI В МОМЕНТ УДАРА — новые спавны получают их автоматически.");
    }

    /// <summary>Снять все лимиты дальности (старое поведение: удар вне зависимости от дистанции).</summary>
    [ContextMenu("Снять лимиты (старое поведение)")]
    public void Clear()
    {
        BattleAI.defaultHitDistance = -1f;
        BattleAI.defaultRangedHitDistance = -1f;
        Debug.Log("[ДАЛЬНОСТЬ] Лимиты дальности сняты у всех юнитов — проверка дистанции перед ударом отключена.");
    }
}
