# Дорожная карта

Документ фиксирует фактическую готовность проекта. Он не обещает совместимость с неподтверждённым поведением оригинальной игры.

## Готово

### Research foundation

- безопасная локальная конфигурация оригинальной установки;
- read-only inventory с SHA-256 и bounded signature detection;
- bounded BinaryDiff;
- протокол controlled map experiments и валидатор metadata.

### Native scenario foundation

- engine-neutral прямоугольная геометрия;
- project-owned native scenario JSON v1;
- terrain overrides и инертные object placements;
- структурная и cross-content валидация;
- детерминированная атомарная persistence;
- versioned deterministic checkpoint persistence для собственного headless runtime state;
- headless create/validate/summary/edit workflow;
- bounded engine-neutral undo/redo session для terrain и inert object edits;
- deterministic scene projection для будущего Godot host.

### Engine primitives

- immutable sparse grid;
- bounded deterministic pathfinding с явными topology и passability rules;
- минимальные project-owned round-robin turn и unweighted movement-budget primitives;
- typed content packages и catalog.
- validated scenario bundle loading: сценарий попадает во внешний game-адаптер только после проверки всех content references.
- synthetic end-to-end tests загружают tracked scenario, content package и runtime checkpoint через production persistence API.
- runnable headless host позволяет валидировать, просматривать и изменять runtime checkpoint до подключения Godot .NET.

## Следующие milestones

### M2 — Godot host

Нужен установленный пользователем совместимый Godot .NET. Требуется создать настоящее окно, загрузить synthetic native scenario, визуализировать проектные placeholder terrain/object nodes и обеспечить camera/input lifecycle. Godot не устанавливается автоматически.

### M3 — Independent gameplay vertical slice

Минимальные независимые правила round-robin turn sequencing, ownership runtime-акторов и unweighted movement budget специфицированы и протестированы как immutable headless session. Следующими нужно явно спроектировать actions, interaction и переходы состояния мира, не смешивая их с native scenario v1. Если заявляется соответствие Disciples II, каждое неизвестное правило сначала получает `TODO-D2-RESEARCH` и воспроизводимые evidence.

### M4 — Graphical scenario editor

Редактор должен работать с native format и content catalog, предоставлять undo/redo, validation panel и deterministic save. Импорт оригинальных форматов остаётся отдельным адаптером.

### M5 — Original-format import

Начинается только после достаточного controlled research. Импортёр read-only преобразует подтверждённые данные в native model, не делая `.sg` внутренним форматом и не сохраняя оригинальные материалы в Git.

## Definition of production-ready

- game loop и редактор запускаются на поддерживаемых платформах;
- все механики имеют независимую спецификацию или подтверждённое исследование;
- native formats версионированы и мигрируются;
- synthetic end-to-end suite и CI зелёные;
- distributable build не содержит оригинальных файлов;
- лицензирование всех project-owned assets задокументировано.
