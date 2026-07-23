# BinaryDiff

Универсальный read-only CLI для контролируемого сравнения двух бинарных файлов. Инструмент показывает bounded байтовые наблюдения и гипотезы; он не разбирает `.sg` или другой проприетарный формат.

## Основной запуск

```powershell
dotnet run --project tools/DisciplesRemaster.BinaryDiff -- compare baseline.sg changed.sg
```

Numeric searches можно повторять:

```powershell
dotnet run --project tools/DisciplesRemaster.BinaryDiff -- compare baseline.sg changed.sg --search-int32 12 --search-int32 8
```

Safe labels не раскрывают input paths:

```powershell
dotnet run --project tools/DisciplesRemaster.BinaryDiff -- compare baseline.sg changed.sg --label-a baseline --label-b one-leader
```

## Options

```text
--output <directory>       default: artifacts/research/binary-diff
--format text|json|both    default: both
--context <0-256>          default: 16
--max-ranges <1-1000>      default: 200
--search-int16 <number>
--search-uint16 <number>
--search-int32 <number>
--search-uint32 <number>
--search-string <text>
--encoding ascii|utf8|utf16le|all   default: all
--label-a <safe-label>
--label-b <safe-label>
```

Создаются `binary-diff.json` и/или `binary-diff.txt`. Output локален и игнорируется Git. Он не может совпадать с input-файлом или находиться внутри доступной оригинальной установки, заданной `D2_ORIGINAL_PATH`.

## Safety and interpretation

Inputs открываются только для чтения и не копируются, не исполняются и не изменяются. DLL не загружаются, сеть отсутствует. SHA-256 и сравнение больших файлов потоковые. Byte dumps, context, ranges, search results и string arguments имеют hard limits без unsafe override.

Numeric/text interpretations имеют low confidence. Совпадение не подтверждает coordinate, ID, terrain, dimension, count, record или другое поле. Используйте несколько controlled experiments и разделяйте observation, hypothesis и confirmed conclusion.

## Exit codes

- `0` — сравнение успешно, независимо от равенства файлов;
- `2` — input отсутствует или недоступен;
- `3` — partial report с warnings;
- `64` — неверная команда, option или unsafe output;
- `70` — непредвиденная ошибка выполнения.
