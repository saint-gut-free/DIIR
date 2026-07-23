# Original game inventory report v1

## Versioning

`reportFormatVersion` имеет значение `1.0`. Несовместимое изменение структуры требует новой major-версии; совместимое добавление поля — minor-версии. Отчёт не содержит времени запуска или случайных идентификаторов.

## Структура JSON

Корневой объект `inventory.json` содержит:

- `reportFormatVersion`;
- `sourceLocation` — только redacted path;
- `options` — `includeHidden` и `maxDepth`;
- `files` — отсортированные ordinal по `relativePath` записи;
- `warnings` — отсортированные по relative path и code предупреждения;
- `summary` — counts, bytes, extensions, categories и duplicate groups.

Запись файла содержит relative path, filename, lowercase extension, size, lowercase SHA-256, UTC last-write timestamp, depth, attributes, category, signature, hidden/reparse flags, read status и необязательное предупреждение. Абсолютные source paths, содержимое, base64 и byte dumps запрещены.

Все обычные доступные файлы включаются независимо от extension/category/signature. `Unknown` не является причиной пропуска.

## Categories

`Executable`, `Library`, `MapOrScenarioCandidate`, `SaveCandidate`, `Image`, `Audio`, `Archive`, `Text`, `Configuration`, `Data`, `Unknown`. Категории консервативны, эвристичны и не подтверждают проприетарный формат.

## Signatures

`Unknown`, `WindowsPortableExecutable`, `Png`, `Jpeg`, `Gif`, `Bmp`, `Wave`, `Ogg`, `Zip`, `Gzip`, `SevenZip`, `Rar`, `Utf8Bom`, `Utf16LittleEndianBom`, `Utf16BigEndianBom`, `PlainText`. Распознавание использует максимум 32 байта и сохраняет только имя signature.

## Warning codes

- `FileInaccessible`
- `DirectoryInaccessible`
- `ReparsePointSkipped`
- `MaximumDepthExceeded`
- `HiddenItemSkipped`
- `FileChangedDuringRead`
- `UnsupportedFileMetadata`
- `OutputInsideOriginalGameDirectory`
- `InvalidOutputPath`
- `HashingFailed`
- `SignatureDetectionFailed`

## Детерминированность

Paths нормализуются с `/` и сортируются `StringComparer.Ordinal`. Extension/category counts и duplicate groups имеют явную ordinal-сортировку. Duplicate key — SHA-256 плюс size; группа содержит минимум два файла. JSON использует стабильные camelCase names, строковые enum values, UTF-8 без BOM и форматированный вывод. Markdown ограничивает крупные списки двадцатью элементами, использует только relative paths и не показывает пустые duplicate groups.

Filesystem timestamps отдельных файлов являются исходными metadata и могут меняться только при изменении установки. Порядок, время запуска, filesystem enumeration order и абсолютный путь на сериализацию не влияют.
