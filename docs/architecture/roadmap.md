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
- headless create/validate/summary/edit workflow;
- deterministic scene projection для будущего Godot host.

### Engine primitives

- immutable sparse grid;
- bounded deterministic pathfinding с явными topology и passability rules;
- typed content packages и catalog.

## Следующие milestones

### M2 — Godot host

Нужен установленный пользователем совместимый Godot .NET. Требуется создать настоящее окно, загрузить synthetic native scenario, визуализировать проектные placeholder terrain/object nodes и обеспечить camera/input lifecycle. Godot не устанавливается автоматически.

### M3 — Independent gameplay vertical slice

До реализации нужно явно спроектировать собственные правила turn sequencing, movement budget, ownership и interaction. Если заявляется соответствие Disciples II, каждое неизвестное правило сначала получает `TODO-D2-RESEARCH` и воспроизводимые evidence.

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
