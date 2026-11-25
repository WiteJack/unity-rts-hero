/// <summary>
/// ОБНОВЛЕНИЕ 28 (A1): перехватчик кадра события удара анимации.
/// Анимация удара → AnimationEventRelay → BattleAI.OnAttackHitEvent() → перехватчики.
/// Если InterceptAttackHit() вернул true — обычный урон BattleAI за этот замах ОТМЕНЯЕТСЯ:
/// удар потрачен перехватчиком (так «Ярость короля» бьёт именно ударами мечом, а не таймером).
/// Перехватчик вешается на те же объекты, что и BattleAI (корень юнита) — найдётся через GetComponents.
/// </summary>
public interface IAttackHitInterceptor
{
    /// <summary>Кадр удара от анимации. true — удар поглощён перехватчиком, обычный урон наносить НЕ нужно.</summary>
    bool InterceptAttackHit();
}
