# Проект (рабочее название уточняется по диздоку)

Unity-игра в жанре RTS / tower-defense с управляемым героем.

- Движок: Unity, рендер **URP**, ввод **новый Input System**, навигация **NavMesh**
- Персонажи: Mixamo-риг (герой «Андрон», зомби и другие мобы)
- Язык разработки/комментариев: русский

## Структура

```
Assets/Scripts/
├── Combat/      бой: BattleAI, TowerAttack, CombatRangeStopper, UnitSeparation
├── Units/       герой и мобы: HeroStats, UnitStats, HeroMovement, SpawnEnemy, ...
├── Buildings/   здания: BuildingDestruction, BuildingMeleeHelper
├── UI/          полоски здоровья: WorldHealthBar, CameraHealthBars
├── Economy/     золото: GoldManager, EnemyGoldReward
├── Environment/ день/ночь и температура
├── Utility/     GameSpawner, TeleportSystem, CorpseDissolve, AnimationEventRelay
└── Camera/      RtsCameraController
```

## Важно

- **[AGENTS.md](AGENTS.md)** — актуальное описание архитектуры, констант (слои 6/7/8/9),
  известных долгов и текущего статуса проекта. Читать перед началом любой задачи.
- Слои зашиты номерами: 6 = PLAYER, 7 = ALLIES, 8 = NEUTRAL, 9 = ENEMY.
- Параметры Animator: `Speed`, `OnAttack`, `OnDeath`, `AttackSpeedMult`.
