using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    private BattleAI parentAI;

    void Start()
    {
        parentAI = GetComponentInParent<BattleAI>();
    }

    public void OnAttackHitEvent()
    {
        if (parentAI != null)
        {
            parentAI.OnAttackHitEvent();
        }
    }
}
