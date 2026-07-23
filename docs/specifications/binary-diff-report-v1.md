# Binary diff report v1

## Versioning

`reportFormatVersion` равен `1.0`. Несовместимое изменение структуры требует новой major-версии; совместимое добавление — minor-версии. Report body не содержит времени запуска, случайных ID и абсолютных input paths.

## JSON structure

Корневой объект содержит:

- `reportFormatVersion`;
- `fileA`, `fileB` — safe label, filename, size, lowercase SHA-256, UTC last-write и read status;
- `options` — bounded context, max ranges и search requests без сырых patterns;
- `comparison` — equality, размеры, size delta, compared/different bytes, range count, common prefix/suffix, stability и truncation;
- `changedRanges`;
- `searchResults`;
- `hypotheses`;
- `warnings`.

## Changed range

Range использует inclusive `startOffset` и `endOffset` в decimal и fixed-width hexadecimal, length и число отличающихся байтов. `fileABytes` и `fileBBytes` ограничены 64 байтами. Большие ranges содержат первые и последние bounded fragments, `omittedByteCount` и `isTruncated`. Before/after contexts ограничены `context`.

Adjacent differing bytes объединяются. Разделённые совпадающими байтами изменения образуют отдельные ranges. При разных размерах дополнительный tail представлен отдельным range. Это не универсальный patch и не доказательство insertion/deletion.

## Numeric and text hypotheses

При достаточном числе bounded байтов допустимы little-endian `Int16`, `UInt16`, `Int32`, `UInt32`, `Int64`, `UInt64`. Printable ASCII может быть показан как text hypothesis. Confidence всегда `Low`; interpretation не называет семантическое поле.

## Searches

Numeric searches используют exact little-endian representation для `Int16`, `UInt16`, `Int32`, `UInt32`. String searches поддерживают `Ascii`, `Utf8`, `Utf16Le`. Результат содержит safe file label, offset, type/encoding, исходное искомое значение, длину и bounded hex context. Results сортируются по offset, encoding и label.

## Warning codes

- `FileInaccessible`
- `FileChangedDuringRead`
- `ReportTruncated`
- `ChangedRangeTruncated`
- `SearchResultsTruncated`
- `OutputConflictsWithInput`
- `OutputInsideOriginalGameDirectory`
- `InvalidOutputPath`
- `HashingFailed`
- `ComparisonFailed`
- `UnsupportedMetadata`

## Determinism and privacy

Для неизменных inputs и одинаковых arguments ranges сортируются по offset, searches — по offset/encoding/label, warnings — по code/label. JSON использует camelCase, string enums, UTF-8 без BOM и stable property order. Text report следует фиксированным секциям.

Reports не содержат absolute paths, исходные файлы, полные byte arrays, unlimited hex dumps, stack traces или base64 полного содержимого. Различие файлов является успешным результатом, а truncation или changed-during-read — partial result.
