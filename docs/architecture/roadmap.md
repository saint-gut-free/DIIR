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
- portable native project manifest связывает scenario, typed content packages и optional runtime checkpoint относительными путями и проверяет bundle целиком.
- synthetic end-to-end tests загружают tracked scenario, content package и runtime checkpoint через production persistence API.
- runnable headless host позволяет валидировать, просматривать и изменять runtime checkpoint до подключения Godot .NET.
- strict bounded action log атомарно воспроизводит существующие project-owned advance-turn и explicit open-grid move действия.
- typed content metadata имеет bounded undo/redo session и headless-команды create/add/rename/remove с validation-before-save.

## Следующие milestones

### M2 — Godot host

Нужен установленный пользователем совместимый Godot .NET. Требуется создать настоящее окно, загрузить synthetic native scenario, визуализировать проектные placeholder terrain/object nodes и обеспечить camera/input lifecycle. Godot не устанавливается автоматически.

### M3 — Independent gameplay vertical slice

Минимальные независимые правила round-robin turn sequencing, ownership runtime-акторов и unweighted movement budget специфицированы и протестированы как immutable headless session. Bounded action batch отделяет команды от состояния и обеспечивает атомарный deterministic replay. Следующими нужно явно спроектировать interaction и переходы состояния мира, не смешивая их с native scenario v1. Если заявляется соответствие Disciples II, каждое неизвестное правило сначала получает `TODO-D2-RESEARCH` и воспроизводимые evidence.

### M4 — Graphical scenario editor

Headless-редактор уже работает с native scenario и content catalog, предоставляет bounded undo/redo model и deterministic atomic save. Для milestone остаются графический интерфейс, validation panel и интеграция этих контрактов с Godot. Импорт оригинальных форматов остаётся отдельным адаптером.

### M5 — Original-format import

Начинается только после достаточного controlled research. Импортёр read-only преобразует подтверждённые данные в native model, не делая `.sg` внутренним форматом и не сохраняя оригинальные материалы в Git.

## Definition of production-ready

- game loop и редактор запускаются на поддерживаемых платформах;
- все механики имеют независимую спецификацию или подтверждённое исследование;
- native formats версионированы и мигрируются;
- synthetic end-to-end suite и CI зелёные;
- distributable build не содержит оригинальных файлов;
- лицензирование всех project-owned assets задокументировано.
