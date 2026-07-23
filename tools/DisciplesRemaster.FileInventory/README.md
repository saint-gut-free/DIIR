# FileInventory

Утилита безопасно проверяет локальный исследовательский каталог и создаёт read-only инвентаризацию технических метаданных его файлов. Она не разбирает проприетарные форматы и не извлекает содержимое.

## Настройка

Задайте `D2_ORIGINAL_PATH` в текущей PowerShell-сессии. Используйте законно установленную копию вне Git-репозитория:

```powershell
$env:D2_ORIGINAL_PATH = "D:\Research\Disciples2Reference"
```

Не сохраняйте реальный путь в `.env`, исходном коде, тестовых snapshots или документации. Инструмент не изменяет системное окружение автоматически.

## Проверка

```powershell
dotnet run --project tools/DisciplesRemaster.FileInventory -- validate-path
```

Пример успешного результата:

```text
Original game research directory is configured.
Location: <configured-original-game-directory>/Disciples2Reference
Status: accessible
The configured directory is accessible and can be used as a research input.
```

Полный абсолютный путь доступен библиотеке только внутри процесса и намеренно скрывается в CLI. Последний сегмент остаётся видимым, чтобы отличать конфигурации без раскрытия структуры пользовательского профиля.

Успех означает только то, что каталог существует и его метаданные доступны. Команда не определяет версию и не подтверждает корректность установки Disciples II. Каталог используется исключительно как read-only research input.

## Инвентаризация

```powershell
dotnet run --project tools/DisciplesRemaster.FileInventory -- inventory
```

Опции:

```text
--output <directory>  output-каталог; по умолчанию artifacts/research/inventory
--include-hidden      включить hidden-файлы и каталоги
--max-depth <number>  положительный предел глубины; по умолчанию 64
```

Инструмент учитывает каждый обычный доступный файл — даже с неизвестными именем, расширением и сигнатурой. Категория и signature являются описанием, а не allowlist. Пропускаются только hidden-элементы без соответствующего флага, ссылки/junctions/reparse points, элементы глубже лимита и недоступные элементы. Ссылки не обходятся.

Для каждого файла собираются relative path, metadata, консервативная общая signature и потоковый SHA-256. Читается не более 32 байт для определения signature; эти байты не сохраняются. Отчёты `inventory.json` и `inventory-summary.md` детерминированно записываются только в output. Output, совпадающий с оригинальным корнем или находящийся внутри него, отклоняется до записи.

Пример:

```text
Original game inventory completed.
Location: <configured-original-game-directory>/Disciples2Reference
Files processed: 123
Files skipped: 0
Total size: 456789 bytes
JSON report: artifacts/research/inventory/inventory.json
Summary: artifacts/research/inventory/inventory-summary.md
```

Коды завершения:

- `0` — каталог настроен и доступен;
- `2` — ошибка конфигурации или проверки пути;
- `3` — инвентаризация завершена частично с предупреждениями;
- `64` — неизвестная команда или неверные аргументы.
- `70` — непредвиденная ошибка выполнения инструмента.
