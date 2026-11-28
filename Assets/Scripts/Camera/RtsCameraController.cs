using UnityEngine;
using UnityEngine.InputSystem;

public class RtsCameraController : MonoBehaviour
{
    [Header("Настройки движения (WASD и края экрана)")]
    public float moveSpeed = 15f;
    [Tooltip("Включить движение камеры при наведении мыши на края экрана")]
    public bool enableEdgeScrolling = true;
    [Tooltip("Ширина зоны у края экрана в пикселях, где срабатывает движение")]
    public float edgeBorderThickness = 15f;

    [Header("Ограничения карты (Границы террейна)")]
    public float minX = -10f;
    public float maxX = 100f;
    public float minZ = -10f;
    public float maxZ = 100f;

    [Header("Настройки Зума (Колесо мыши)")]
    public float zoomSpeed = 50f;
    public float minHeight = 1f;
    public float maxHeight = 20f;

    [Header("Настройки Вращения (Q / E)")]
    public float rotationSpeed = 60f;

    [Header("Множитель ускорения (SHIFT)")]
    public float shiftMultiplier = 2f;

    [Header("Смерть и возрождение героя (Обновление 34)")]
    [Tooltip("СМЕРТЬ ГЕРОЯ: если риг камеры или сама камера вложены в героя (камера внутри префаба героя), трупная анимация смерти трясёт камеру, а телепорт респавна уносит её через карту. При смерти героя камера автоматически отцепляется от героя и становится чистым фриплеем.")]
    public bool detachCameraOnHeroDeath = true;
    [Tooltip("ВОЗРОЖДЕНИЕ ГЕРОЯ: героя мгновенно переносит на точку респавна — камера не летит за ним, а мгновенно встаёт НАД точкой возрождения (высота и поворот камеры сохраняются). Выключите, если хотите оставить камеру на месте смерти.")]
    public bool snapToHeroOnRespawn = true;

    // --- Смерть/возрождение героя (Обновление 34): фикс «камера не фриплей при смерти» и «камера улетает после телепорта». ---
    // Логика живёт ЗДЕСЬ, в контроллере камеры (решение владельца — НЕ в ClampToMap: кламп только удерживает
    // камеру в границах карты, но не управляет режимами). Герой отслеживается через HeroRespawn.IsWaitingForRespawn.
    private HeroRespawn heroRespawn;
    private Transform heroTransform;
    private bool heroWasDead = false;
    private bool heroLookupFailedLogged = false;
    private float nextHeroLookupTime = 0f;

    // АВАРИЙНЫЙ жёсткий зажим КАЖДЫЙ кадр (LateUpdate). Зажим в Update удерживает только
    // собственное движение WASD/краёв экрана. Если камеру за границы отбросил кто-то другой
    // (фокус спавнера в GameSpawner, любой сторонний контроллер героя, сбой кадра) —
    // LateUpdate возвращает её в рамки карты в тот же кадр, до отрисовки.
    void LateUpdate()
    {
        HandleHeroLifeCycle();
        // риг зажимаем вместе с высотой; Camera.main (если камера ДОЧЕРНИЙ объект рига,
        // как на типичной схеме CameraRig -> Main Camera) — тоже: телепорт двигает именно
        // Camera.main, и без этого камера оставалась за картой до следующего WASD.
        // Смерть/возрождение героя обрабатываются отдельно в HandleHeroLifeCycle (Обновление 34) — здесь только границы карты.
        ClampToMap(transform, true);
        var mainCam = Camera.main;
        if (mainCam != null && mainCam.transform != transform)
            ClampToMap(mainCam.transform, false);
    }

    /// <summary>Жёсткий зажим позиции в границы карты; public — можно дёрнуть после телепорта.</summary>
    public void ClampToMap(Transform t, bool clampHeight)
    {
        Vector3 p = t.position;
        // защита от NaN (битые телепорты/варпы превращают позицию в бессмыслицу,
        // а Clamp(NaN) = NaN — зажим молча перестаёт работать)
        if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z))
        {
            t.position = new Vector3((minX + maxX) * 0.5f, (minHeight + maxHeight) * 0.5f, (minZ + maxZ) * 0.5f);
            return;
        }
        float clampedX = Mathf.Clamp(p.x, minX, maxX);
        float clampedZ = Mathf.Clamp(p.z, minZ, maxZ);
        float clampedY = clampHeight ? Mathf.Clamp(p.y, minHeight, maxHeight) : p.y;
        if (p.x != clampedX || p.z != clampedZ || p.y != clampedY)
        {
            t.position = new Vector3(clampedX, clampedY, clampedZ);
        }
    }

    /// <summary>Слежит за смертью/возрождением героя и переключает режимы камеры (Обновление 34).</summary>
    private void HandleHeroLifeCycle()
    {
        // Герой ещё не найден: ищем не чаще раза в 2 сек — спавн героя может случиться позже старта камеры
        if (heroRespawn == null)
        {
            if (Time.time.CompareTo(nextHeroLookupTime) == -1) return;
            nextHeroLookupTime = Time.time + 2f;
            heroRespawn = FindFirstObjectByType<HeroRespawn>();
            if (heroRespawn == null)
            {
                if (!heroLookupFailedLogged)
                {
                    Debug.LogWarning("[КАМЕРА] HeroRespawn на сцене не найден — причина: на герое нет компонента HeroRespawn или герой ещё не заспавнился. Камера не узнает о смерти/возрождении героя.");
                    heroLookupFailedLogged = true;
                }
                return;
            }
            heroTransform = heroRespawn.transform;
        }

        bool heroDead = heroRespawn.IsWaitingForRespawn;

        if (heroDead && !heroWasDead) OnHeroDiedCamera();
        if (!heroDead && heroWasDead) OnHeroRespawnedCamera();

        heroWasDead = heroDead;
    }

    // СМЕРЬ ГЕРОЯ: камера ОБЯЗАНА стать фриплеем. Если риг или сама камера вложены в героя,
    // анимация смерти корячит камеру («смерть ломает камеру») — отцепляем в мировых координатах.
    private void OnHeroDiedCamera()
    {
        bool detached = false;

        if (detachCameraOnHeroDeath && transform.parent != null)
        {
            transform.SetParent(null, true); // true = оставить мировую позицию и поворот как были
            detached = true;
        }

        // Если сама камера была вложена в героя — возвращаем её под этот риг (мировая позиция сохраняется)
        var mainCam = Camera.main;
        if (mainCam != null && heroTransform != null && IsDescendantOf(mainCam.transform, heroTransform))
        {
            mainCam.transform.SetParent(transform, true);
            detached = true;
        }

        if (detached)
            Debug.Log("[КАМЕРА] Герой погиб → камера отцеплена от героя и переведена во фриплей (Обновление 34).");
        else
            Debug.Log("[КАМЕРА] Герой погиб → камера уже была фриплеем, отцеплять нечего (Обновление 34).");
    }

    // ВОЗРОЖДЕНИЕ: героя мгновенно переносит на точку респавна (HeroRespawn.RespawnNow).
    // Если бы камера осталась привязанной к герою — её бы унесло через всю карту.
    // Вместо этого ставим риг НАД точкой возрождения: высота и поворот камеры сохраняются.
    private void OnHeroRespawnedCamera()
    {
        if (snapToHeroOnRespawn && heroTransform != null)
        {
            Vector3 above = new Vector3(heroTransform.position.x, transform.position.y, heroTransform.position.z);
            transform.position = above;
            // штатная проверка границ после переноса; сам фикс — здесь, в контроллере, не в клампе
            ClampToMap(transform, true);
            Debug.Log($"[КАМЕРА] Герой возродился на {heroTransform.position} → камера перенесена над точкой респавна, высота/поворот сохранены (Обновление 34).");
        }
        else
        {
            Debug.Log("[КАМЕРА] Герой возродился → камера остаётся фриплеем на месте смерти (перенос отключён в инспекторе).");
        }
    }

    // Вложен ли узел в предка ancestor (для поиска камеры внутри героя)
    private static bool IsDescendantOf(Transform node, Transform ancestor)
    {
        while (node != null)
        {
            if (node.parent == ancestor) return true;
            node = node.parent;
        }
        return false;
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // 1. Проверяем Shift для ускорения
        float currentMultiplier = 1f;
        if (Keyboard.current[Key.LeftShift].isPressed || Keyboard.current[Key.RightShift].isPressed)
        {
            currentMultiplier = shiftMultiplier;
        }

        // 2. ДВИЖЕНИЕ (WASD + Края экрана)
        Vector3 moveDirection = Vector3.zero;
        if (Keyboard.current[Key.W].isPressed || Keyboard.current[Key.UpArrow].isPressed) moveDirection += transform.forward;
        if (Keyboard.current[Key.S].isPressed || Keyboard.current[Key.DownArrow].isPressed) moveDirection -= transform.forward;
        if (Keyboard.current[Key.A].isPressed || Keyboard.current[Key.LeftArrow].isPressed) moveDirection -= transform.right;
        if (Keyboard.current[Key.D].isPressed || Keyboard.current[Key.RightArrow].isPressed) moveDirection += transform.right;

        // Движение при наведении мыши на края экрана (как в Warcraft 3)
        if (enableEdgeScrolling && Mouse.current != null && Application.isFocused)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            // Проверяем, что мышь внутри окна игры
            bool insideX = mousePos.x.CompareTo(0f) != -1 && mousePos.x.CompareTo((float)Screen.width) != 1;
            bool insideY = mousePos.y.CompareTo(0f) != -1 && mousePos.y.CompareTo((float)Screen.height) != 1;

            if (insideX && insideY)
            {
                if (mousePos.y.CompareTo(Screen.height - edgeBorderThickness) != -1) moveDirection += transform.forward;
                if (mousePos.y.CompareTo(edgeBorderThickness) != 1) moveDirection -= transform.forward;
                if (mousePos.x.CompareTo(edgeBorderThickness) != 1) moveDirection -= transform.right;
                if (mousePos.x.CompareTo(Screen.width - edgeBorderThickness) != -1) moveDirection += transform.right;
            }
        }

        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude.CompareTo(0.001f) == 1)
        {
            moveDirection.Normalize();
        }

        // Считаем новую потенциальную позицию
        Vector3 newPosition = transform.position + moveDirection * moveSpeed * currentMultiplier * Time.deltaTime;

        // ЖЕСТКО ОГРАНИЧИВАЕМ движение по осям X и Z, чтобы не вылетать за границы
        newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
        newPosition.z = Mathf.Clamp(newPosition.z, minZ, maxZ);

        // Применяем позицию после ограничения
        transform.position = newPosition;

        // 3. ВРАЩЕНИЕ (Q / E)
        float rotationDirection = 0f;
        if (Keyboard.current[Key.Q].isPressed) rotationDirection -= 1f;
        if (Keyboard.current[Key.E].isPressed) rotationDirection += 1f;

        if (Mathf.Abs(rotationDirection).CompareTo(0.01f) == 1)
        {
            transform.Rotate(Vector3.up, rotationDirection * rotationSpeed * currentMultiplier * Time.deltaTime, Space.World);
        }

        // 4. ЗУМ (Колесо мыши)
        if (Mouse.current != null)
        {
            float scrollInput = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollInput).CompareTo(0.01f) == 1)
            {
                float scrollDir = Mathf.Sign(scrollInput);
                float newY = transform.position.y - scrollDir * zoomSpeed * currentMultiplier * Time.deltaTime;
                newY = Mathf.Clamp(newY, minHeight, maxHeight);

                Vector3 currentPos = transform.position;
                currentPos.y = newY;
                transform.position = currentPos;
            }
        }
    }
}
