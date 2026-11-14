using UnityEngine;

[RequireComponent(typeof(BattleAI))]
public class BuildingMeleeHelper : MonoBehaviour
{
    [Tooltip("Радиус удара зомби по крупным зданиям (чтобы доставал через вырез в NavMesh)")]
    public float buildingAttackRange = 4.0f;

    private BattleAI battleAI;
    private float defaultAttackRange;

    void Start()
    {
        battleAI = GetComponent<BattleAI>();
        if (battleAI != null)
        {
            defaultAttackRange = battleAI.attackRange;
        }
    }

    void Update()
    {
        if (battleAI == null) return;

        // Проверяем, стоит ли прямо перед зомби здание (Allies с BaseDamage == 0)
        Collider[] hits = Physics.OverlapSphere(transform.position, buildingAttackRange);
        bool nearBuilding = false;

        foreach (var col in hits)
        {
            if (col.gameObject.layer == 7) // Allies
            {
                UnitStats uStats = col.GetComponentInParent<UnitStats>();
                if (uStats != null && !uStats.isDead && uStats.baseDamage == 0f)
                {
                    nearBuilding = true;
                    break;
                }
            }
        }

        // Если рядом здание — увеличиваем радиус удара; если нет — возвращаем обычный радиус
        battleAI.attackRange = nearBuilding ? buildingAttackRange : defaultAttackRange;
    }
}
