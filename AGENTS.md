# AGENTS.md — память проекта (читать перед началом работы)

Этот файл — «внешняя память» проекта. Перед любой задачей агент читает его и актуальный код.
После каждой рабочей сессии обновлять разделы «Текущий статус» и «Дальше по плану».

## Что за проект

Unity 3D-игра в жанре RTS / tower-defense с героем (рабочее название по диздоку — проект с героем «Андрон»).
Рендер: **URP**. Ввод: **новый Input System** (`UnityEngine.InputSystem`). Навигация: **NavMesh**.
Модели и анимации: **Mixamo** (риг `mixamorig:*`). Язык комментариев и ТЗ — русский.

## Ключевые константы проекта

- Слои (Layer): **6 = PLAYER**, **7 = ALLIES**, **8 = NEUTRAL**, **9 = ENEMY** (номера зашиты в скриптах — не менять без рефакторинга всех зависимостей).
- Герой: префаб `[HERO_Andron]` (Andron, меч + щит, `Andron_Controller`, avatar `Sword And Shield DeathAvatar`).
- Мобы: префаб `[ENEMY_Zombie]` (`Zombie_controller`, анимации Zombie_Idle / Zombie_Attack / Zombie_Combat_Idle / Zombie_Death / Zombie_Run).
- Параметры Animator: `Speed` (float), `OnAttack` (trigger), `OnDeath` (trigger), `AttackSpeedMult` (float).
- Структура префаба: корень (NavMeshAgent + логика) → `[ANIMATION_RIG]` → Mesh (Animator + AnimationEventRelay) → кости.

## Архитектура скриптов (Assets/Scripts)

### Combat
- `BattleAI.cs` — мозг боя: поиск цели по слоям (OverlapSphere), атака по кулдауну, синхронизация скорости атаки с анимацией (`AttackSpeedMult`), событие удара `OnAttackHitEvent()` (вызывается из анимации через AnimationEventRelay). Подходит и герою, и мобам: если есть `HeroStats` — формула героя, если `UnitStats` — фиксированные параметры.
- `TowerAttack.cs` — башни: одиночная пуля или ядро по области (`isAreaDamage`, `splashRadius`). Снаряд создаётся кодом (сфера + TrailRenderer), урон через `UnitStats`/`HeroStats`.
- `CombatRangeStopper.cs` — останавливает юнита чуть внутри `attackRange`, чтобы BattleAI гарантированно начинал удар; не мешает приказу игрока (`isMovingByOrder`).
- `UnitSeparation.cs` — анти-слипание толпы: мягкое расталкивание, `OverlapSphereNonAlloc`, случайный `avoidancePriority`, проверки ~25 раз/сек (оптимизация под 500 юнитов).

### Units
- `HeroStats.cs` — атрибуты героя: сила (1=5 урона), живучесть (1=25 HP), выносливость (реген HP + защита), ловкость и сила воли (+скорость атаки), мудрость (мана). Особая механика: скорость атаки выше капа 4.0 конвертируется в шанс крита (x1.5, дальше растёт урон крита). Минимальный урон после брони = 10.
- `UnitStats.cs` — характеристики моба (maxHP, baseDamage, defense, реген, скорость атаки).
- `CharacterStats.cs` — УСТАРЕВШАЯ копия HeroStats (проверить, используется ли где-то; кандидат на удаление).
- `HeroMovement.cs` — ПКМ: клик по земле (движение + эффект клика) или по врагу (подсветка вспышкой + довести до дальности атаки и передать цель в BattleAI). Доступ к цели BattleAI — через **Reflection** (поле `currentTarget`).
- `EnemyMarchToBase.cs` — мобы идут на базу игрока, если не заняты боем.
- `HeroTargetCircles.cs` — круги выделения под героем и целью (LineRenderer).
- `SpawnEnemy.cs` — 30 волн по минутам (WaveConfig), спавн по точкам с разбросом, автопоиск префабов на слое Enemy (только в редакторе, `#if UNITY_EDITOR`).

### Buildings
- `BuildingDestruction.cs` — разрушение зданий.
- `BuildingMeleeHelper.cs` — зомби бьёт здание, если оно прямо перед ним (Allies с BaseDamage == 0).
- ВАЖНО: скрипт `BuildingStats` **не загружен** — на него ссылаются CombatRangeStopper и BuildingMeleeHelper. Запросить у владельца.

### UI
- `WorldHealthBar.cs` / `CameraHealthBars.cs` — полоски HP над юнитами, цвета по типу (герой/союзники/нейтралы/враги).

### Economy
- `GoldManager.cs` — золото + UI-текст.
- `EnemyGoldReward.cs` — золото за убийство (проверяет, что убийца — герой).

### Environment
- `DayNightTemperature.cs` — день/ночь + температура (URP).

### Utility
- `GameSpawner.cs` — спавн героя в начале матча (пока всегда Андрон; в будущем — выбор из меню), фокус камеры на нём.
- `TeleportSystem.cs` — 4 точки ТП, общая перезарядка 60 сек.
- `CorpseDissolve.cs` — труп лежит 30 сек, потом растворяется (URP-прозрачность).
- `AnimationEventRelay.cs` — транслирует событие анимации `OnAttackHitEvent` в BattleAI на родителе.

### Camera
- `RtsCameraController.cs` — RTS-камера.

## Известные особенности / долги

1. `CharacterStats.cs` — дубликат `HeroStats.cs`, кандидат на удаление (проверить ссылки в сценах).
2. Доступ к `BattleAI.currentTarget` через Reflection из 3 скриптов — работает, но хрупко; кандидат на рефакторинг (сделать публичное свойство).
3. Много `Debug.Log` в горячих путях (урон при 500 юнитах) — собирать в условную компиляцию или выключатель.
4. Номера слоёв зашиты магическими числами (6/7/8/9) во многих скриптах — кандидат на константы/статический класс.
5. Отсутствует скрипт `BuildingStats` (см. Buildings).

## Текущий статус

- Загружена первая часть: 23 скрипта + 8 скриншотов (префабы героя и зомби, инспекторы, Animator-контроллер зомби).
- Скрипты разложены по папкам `Assets/Scripts/{Combat,Units,Buildings,UI,Economy,Environment,Utility,Camera}`.
- Диздок (20–30 стр.) ещё НЕ загружен — ожидается.
- Репозиторий GitHub: создаётся, ссылка будет здесь: _TBD_

## Дальше по плану

1. Получить от владельца: диздок, оставшиеся скриншоты (анимации, сцена, здания), скрипт `BuildingStats`, уточнить судьбу `CharacterStats.cs`.
2. Заполнить README со структурой сцены и префабов.
3. Далее — по диздоку.
