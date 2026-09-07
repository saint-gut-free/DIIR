# Disciples Remaster Research Project

Независимый исследовательский проект по созданию современной реализации движка стратегической игры в духе Disciples II. Проект не связан с правообладателями и не является официальным ремастером.

## Текущий статус

Созданы каркас репозитория, исследовательские read-only инструменты, engine-neutral модель собственного сценария v1, typed content catalog, переносимый project manifest, детерминированная JSON persistence и headless-основа редактора. Формат поддерживает terrain overrides и инертные object placements; внешний игровой адаптер получает детерминированные scene data только после проверки всех ссылок на собственный контент и не создаёт зависимости ядра от Godot. Реализован независимый headless vertical slice для turn sequencing и движения по явно открытому grid; это собственные правила проекта, а не реконструкция Disciples II. Графический редактор, импорт исходных форматов и настоящий Godot host ещё не реализованы.

## Долгосрочное направление

- независимая доменная модель, не зависящая от игрового движка;
- современный клиент на Godot;
- собственные форматы хранения и инструменты редактора карт и сценариев;
- локальные исследовательские инструменты для легально установленной оригинальной игры;
- импорт оригинальных форматов только как входной путь миграции.

Первый milestone — воспроизводимая, протестированная основа доменной модели и спецификаций без интеграции с оригинальными материалами.

Фактическая готовность и следующие milestones перечислены в [дорожной карте](docs/architecture/roadmap.md).

## Собственный формат сценария

Минимальный синтетический пример находится в `samples/synthetic/scenarios/minimal-scenario.json`. Он создан проектом и не содержит оригинальных данных.

```powershell
dotnet run --project editor/DisciplesRemaster.Editor -- validate samples/synthetic/scenarios/minimal-scenario.json
dotnet run --project editor/DisciplesRemaster.Editor -- summary samples/synthetic/scenarios/minimal-scenario.json
```

Создание и редактирование собственного документа описаны в `editor/DisciplesRemaster.Editor/README.md`. Этот формат не является `.sg`; будущий импорт оригинальных сценариев должен преобразовывать подтверждённые данные в независимую модель.

Переносимый manifest связывает scenario, content packages и optional runtime checkpoint только относительными путями:

```powershell
dotnet run --project editor/DisciplesRemaster.Editor -- validate-project samples/synthetic/minimal.project.json
dotnet run --project editor/DisciplesRemaster.Editor -- summary-project samples/synthetic/minimal.project.json
dotnet run --project game/DisciplesRemaster.Godot -- summary-project samples/synthetic/minimal.project.json
```

Его контракт описан в [спецификации native project manifest v1](docs/specifications/native-project-manifest-v1.md).

Синтетические scenario, content package и runtime checkpoint проходят end-to-end тест через production persistence API. Минимальный checkpoint находится в `samples/synthetic/sessions/minimal-session.json`; его независимые правила описаны в [спецификации turn/movement](docs/specifications/project-owned-turn-and-movement-v1.md) и [формате checkpoint v1](docs/specifications/game-session-checkpoint-v1.md). Эти правила являются собственным дизайном проекта, а не заявлением о поведении оригинальной игры.

Bounded action log позволяет детерминированно и атомарно воспроизвести уже определённые project-owned runtime actions:

```powershell
dotnet run --project game/DisciplesRemaster.Godot -- replay-open-grid samples/synthetic/sessions/minimal-session.json samples/synthetic/sessions/minimal-actions.json --output artifacts/runtime/replayed.session.json
```

`open-grid` явно обозначает тестовую политику проходимости; она не считается механикой Disciples II.

## Требования и команды

Требуется .NET SDK 10.0.302 или совместимый SDK 10.x и Git. Godot на текущем этапе не требуется.

```powershell
dotnet restore
dotnet build
dotnet test
```

Полная локальная проверка одной командой:

```powershell
./scripts/verify.ps1
```

Сценарий по умолчанию собирает и тестирует конфигурацию `Release`, проверяет `dotnet format`, whitespace и отсутствие запрещённых original-material candidates. Для быстрой локальной итерации допустимо явно передать `-Configuration Debug`; CI всегда использует значение по умолчанию.

## Структура

- `src/` — независимые библиотеки ядра, контента, хранения и будущего импорта;
- `game/` — будущая интеграция с Godot;
- `editor/` — будущий редактор карт и сценариев;
- `tools/` — отдельные исследовательские утилиты;
- `tests/` — автоматические тесты;
- `docs/` — архитектура, спецификации, исследования и ADR;
- `scripts/` — воспроизводимые служебные сценарии;
- `samples/synthetic/` — только созданные проектом синтетические примеры.

## Оригинальная игра

Оригинальные исполняемые файлы, библиотеки, сохранения, карты, музыка, графика, извлечённые данные и декомпилированный код не входят в репозиторий. Подробнее: [ORIGINAL_GAME_FILES.md](ORIGINAL_GAME_FILES.md). Неизвестное поведение отмечается `TODO-D2-RESEARCH`, а не додумывается.

Для локальной проверки пути задайте `D2_ORIGINAL_PATH` только в своей текущей PowerShell-сессии. Каталог должен находиться вне Git-репозитория:

```powershell
$env:D2_ORIGINAL_PATH = "D:\Research\Disciples2Reference"
dotnet run --project tools/DisciplesRemaster.FileInventory -- validate-path
```

Успешный результат выглядит так:

```text
Original game research directory is configured.
Location: <configured-original-game-directory>/Disciples2Reference
Status: accessible
```

Полный путь намеренно скрывается. `validate-path` не перечисляет файлы и не подтверждает наличие или версию Disciples II.

Для создания локального технического отчёта:

```powershell
dotnet run --project tools/DisciplesRemaster.FileInventory -- inventory
```

Отчёты создаются в игнорируемом `artifacts/research/inventory/`. Инвентаризация охватывает каждый обычный доступный файл независимо от имени, расширения, категории и сигнатуры. Категории и сигнатуры являются только описательными метаданными и никогда не используются как allowlist. Инструмент потоково читает файлы только для SHA-256 и не более 32 начальных байт для общих magic signatures; он не извлекает, не запускает и не изменяет оригинальные материалы.
