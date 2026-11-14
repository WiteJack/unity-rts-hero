using UnityEngine;

public class GameSpawner : MonoBehaviour
{
    [Header("Точка спавна героя")]
    public Transform spawnPoint;

    [Header("База данных героев (префабы)")]
    public GameObject andronPrefab; // Ссылка на префаб Андрона

    [HideInInspector]
    public GameObject spawnedHero; // Ссылка на созданного в матче героя

    void Awake()
    {
        SpawnSelectedHero();
    }

    void SpawnSelectedHero()
    {
        // Логика «на будущее»: здесь будет считываться ID из меню.
        // Сейчас по умолчанию всегда спавним Андрона, как указано в ТЗ.
        if (andronPrefab != null && spawnPoint != null)
        {
            spawnedHero = Instantiate(andronPrefab, spawnPoint.position, spawnPoint.rotation);
            
            // Сразу настраиваем фокус камеры на него при старте
            var cameraRig = Object.FindFirstObjectByType<RtsCameraController>();
            if (cameraRig != null)
            {
                Vector3 targetPos = spawnPoint.position;
                targetPos.y = cameraRig.transform.position.y; // Сохраняем текущую высоту камеры
                cameraRig.transform.position = targetPos;
            }
        }
        else
        {
            Debug.LogError("Забудь вставить префаб героя или точку спавна в GameSpawner!");
        }
    }
}
