# ADR-0012: LaTeX/Pandoc-разделители формул `\(...\)` / `\[...\]`

## Status

Принят (реализован 2026-11-18; `\(...\)` / `\[...\]` — слой парсинга Infrastructure,
`\boxed` — экспандер нормализатора)

## Date

2026-11-18

## Related

- ADR-0009 — math formula rendering: введены `MarkdownMathInline` (`$...$`,
  `$$...$$`) и `MarkdownMathBlock`, движок `Sylinko.CSharpMath.Avalonia 12.0.0`.
- ADR-0010 — канонические координаты формул: TeX в каноническом тексте,
  кэш замеров по нормализованному LaTeX.
- ADR-0011 — таблицы: формула — неразрывный «атом» вёрстки, замер через
  `MarkdownMathRenderer.Measure` по `MarkdownMathInline.Content`.
- Триггер: `1_SPECIFICATION_AND_INVARIANTS_SDD_CORE.md` в корне проекта —
  документ полностью набран в LaTeX/Pandoc-стиле и сейчас отрендерится
  как текст с мусором `\(`, `\[`, `\]`.

## Context

### Что использует файл-триггер

Полная сверка по `1_SPECIFICATION_AND_INVARIANTS_SDD_CORE.md`:

- Символа `$` в файле **нет вообще** (0 вхождений). Весь math — через
  классические LaTeX-разделители:
  - инлайн `\(...\)` — **28 вхождений** (строки 36, 68–71, 89, 93, 99,
    105–107, 121, 132–133, 170–171, 173, 185, 201, 209, 257–259, 264,
    294, 296, 300, 306);
  - дисплей `\[` … `\]` на отдельных строках — **12 блоков** (строки
    59–64, 75–77, 79–81, 85–87, 137–139, 141–143, 147–149, 153–155,
    193–195, 205–207, 211–213, 215–217).
- Инлайн-формулы стоят в контекстах, которые парсер должен пережить:
  обычный текст (`:68–71`, `:89`), **внутри таблиц** (`:99`, `:105–107`,
  `:170–171`, `:173`, `:257–259`), внутри bold (`:306`), в списке
  ограничений (`:294–300`).
- Дисплей-формулы — всегда блочные (разделители на своих строках), пример
  (`:59–64`): вложенный `\dfrac` внутри `\frac`:
  ```tex
  \[
  R_s =
  \frac{n_sp_s-n_i^2}
  {\dfrac{n_s+n_{1s}}{S_p}
  +\dfrac{p_s+p_{1s}}{S_n}}
  \]
  ```
- Набор команд файла: `\frac`, `\dfrac`, `\exp`, `\left(` / `\right)`,
  `\approx`, `\times`, `\ldots`, `\geq`, `\gg`, `_{}` / `^{}`, `\text{}`,
  `\boxed{}`, управляемый пробел `\ ` (бэкслэш-пробел).
- Посторонний HTML: `:175` — `<caption>…</caption>` после таблицы
  (не Markdown-caption; текущий `HandleInlineHtmlTag` такие теги дропает,
  текст остаётся обычным параграфом).

### Почему это сейчас не работает

Конвейер `MarkdigMarkdownDocumentRenderer` (`Markdig 1.1.2`,
`UseAdvancedExtensions()` → `UseMathematics()`) знает только
долларовые разделители.

Факты из присланных исходников Markdig:

- `MathInlineParser.cs:24` — `OpeningCharacters = ['$']`, только доллар;
  `openDollars = 1..2` (`:48–54`) — `$` или `$$`, третьего не дано.
- Открывающий `$`/`$$` валиден только после whitespace/punctuation
  (`:56–64`); закрывающий — только перед whitespace/punctuation, плюс
  симметрия пробелов у границ (`:135–144`). Инлайн не пересекает
  `\r`/`\n` (`:83–86`). Закрывающий `$` с предшествующим `\` —
  экранирован и не закрывает (`:89`).
- `MathBlockParser.cs:23–26` — `OpeningCharacters = ['$']`,
  `MinimumMatchCount = MaximumMatchCount = 2`, т.е. строго `$$`;
  инфо-строка после открывающего `$$` запрещена (`NoInfoParser`, `:54–64`).

Отсюда в проекте:

- `MarkdigMarkdownDocumentRenderer.Inlines.cs:64–68` — только
  `MathInline` (`$`, `DelimiterCount >= 2` → `IsDisplay = true`);
- `MarkdigMarkdownDocumentRenderer.Math.cs:37,72` — только
  `MathInline { DelimiterCount: >= 2 }`;
- `MarkdownMathParsingTests` покрывают только `$`/`$$` (`:9–17`,
  `:20–33`, `:142–149` — висячий доллар остаётся текстом).

`\(...\)` / `\[...\]` Markdig-математикой не распознаются и падают
в `LiteralInline` = обычный текст (в парсере вообще нет другой
открывающей скобки). Следствия по цепочке:

1. формула не рисуется, видны сырые `\(`, `\)`, `\[`, `\]`;
2. TeX лежит в текстовой карте как обычный текст (поиск/копирование
   отдадут мусор вместо формулы — противоречие ADR-0010);
3. табличная вёрстка ADR-0011 меряет `\(S_n\)` как текст, а не как
   формулу-атом.

### Ресерч: что из LaTeX/Pandoc-формата не покрыто в MarkMello

Источники: локальный код (`Infrastructure/Markdown`,
`Presentation/Views/Markdown/MarkdownMath*`, тесты), сам файл-триггер,
Pandoc User's Guide (присланный HTML, разделы `Math Input`,
`Extension: tex_math_dollars`, `tex_math_gfm`,
`tex_math_single_backslash`, `tex_math_double_backslash`),
исходники Markdig (`MathInlineParser.cs`, `MathBlockParser.cs` —
присланные файлы), KaTeX Supported Functions (присланный HTML)
и прямая проба движка `Sylinko.CSharpMath.Avalonia 12.0.0` временным
тестом `ProbeLatexCoverageTests` (18 проб, `MarkdownMathRenderer.Measure`,
результаты ниже; файл пробы удалён после снятия отчёта).

Факты из Pandoc Manual:

- `tex_math_dollars`: всё между двумя `$` — инлайн-TeX; открывающий `$`
  требует non-space справа, закрывающий — non-space слева и не перед
  цифрой (`$20,000 and $30,000` — не math); литеральный `$` — через
  `\$`. Дисплей — `$$`, от формулы может отделяться пробелами, но
  пустых строк между открывающим и закрывающим `$$` быть не должно.
  Включён по умолчанию для `markdown`, `commonmark_x`, `gfm`, `ipynb`,
  `markdown_mmd`, `opml`; выключен для `commonmark`, `markdown_strict`,
  `markdown_phpextra`, `markdown_github`, `html`, `epub`, `plain` и др.
- `tex_math_single_backslash`: всё между `\(` и `\)` — инлайн-TeX,
  между `\[` и `\]` — дисплей-TeX. Недостаток, зафиксированный в Manual:
  расширение исключает экранирование `(` и `[`. Выключен по умолчанию
  везде (весь список `disabled by default`, включая `markdown`).
- `tex_math_double_backslash`: всё между `\\(` и `\\)` — инлайн,
  между `\\[` и `\\]` — дисплей. Включён по умолчанию только для
  `markdown_mmd`; для `markdown` выключен.
- `tex_math_gfm` (GitHub-специфика, для полноты): инлайн `$`e=mc^2`$`,
  дисплей — fenced ` ```math `.
- Pandoc при LaTeX-выводе печатает math verbatim в `\(...\)` /
  `\[...\]`, при Markdown-выводе — в `$...$` / `$$...$$`; т.е. формат
  файла-триггера — это буквально «LaTeX-каноника» Pandoc.
- Те же расширения применимы к HTML-вводу (чтение страниц с MathJax).

| # | Конструкция | Статус в MarkMello | Комментарий |
|---|---|---|---|
| 1 | `\(...\)` инлайн (Pandoc single-backslash) | **Не покрыто** | Ядро настоящего ADR; 28 мест в файле. По Manual — инлайн-TeX; tradeoff: теряется экранирование `(` |
| 2 | `\[...\]` дисплей (Pandoc single-backslash) | **Не покрыто** | 12 блоков в файле → `MarkdownMathBlock`. По Manual — дисплей-TeX (в отличие от `$$`, условия про пустые строки не оговорены) |
| 3 | `\\(...\\)`, `\\[...\\]` (Pandoc double-backslash) | **Не покрыто** | По Manual — дефолт только для `markdown_mmd`; для `markdown` выключен. В файле нет; решить после п. 1–2 |
| 4 | `$...$`, `$$...$$` | Покрыто (ADR-0009) | В файле отсутствуют. Pandoc-правила (`$` + non-space, не перед цифрой; `$$` без пустых строк внутри; `\$` — литерал) — сверить с текущими тестами (`DanglingDollarStaysPlainText` покрывает только «висячий доллар») |
| 5 | `\dfrac` / `\tfrac` | Покрыто (`MarkdownMathSizedFractionExpander`) | В файле есть, включая вложенность в `\frac` (`:61–63`) |
| 6 | `\lVert`, `\rVert`, `\lvert`, `\rvert` | Покрыто (`MarkdownMathCommandAliasExpander`) | В файле нет, но смежно |
| 7 | Бинарные `+`/`−` у дробей | Покрыто (`MarkdownMathBinarySpacingExpander`) | В файле есть (`:62–63`): `}{S_p}\n+\dfrac` — нормализатор должен сработать после конвертации разделителей |
| 8 | `\boxed{}` | **Не покрыто движком (факт пробы)** | Проба: `\boxed{x}` → `valid=False, Invalid command \boxed`; полный `:154` — та же ошибка. KaTeX-референс (присланный Supported Functions): `\boxed{\pi=\frac c d}` — валиден и рендерится (`menclose notation="box"`); т.е. это лимит именно CSharpMath, а не LaTeX. Путь: алиас/экспандер в `MarkdownMathTexNormalizer` (например, рамка через доступные примитивы) либо честный fallback по ADR-0009 п.4 |
| 9 | `\text{...}` с кириллицей | **Покрыто движком (факт пробы)** | Проба: `\text{эВ}`, `\text{см}^{-3}` (в т.ч. `N_D \approx 10^{16}\ \text{см}^{-3}` целиком) → `valid=True`. Отдельно из README CSharpMath: в состав входят `Cyrillic Modern font` (SourceForge) под OFL — кириллица ожидается штатно |
| 10 | `\ ` (бэкслэш-пробел) | **Покрыто движком (факт пробы)** | Проба: `a\ b` и дисплей с `\ ` → `valid=True`. KaTeX-референс подтверждает семейство: `\,` (3/18em), `\:` (4/18em), `\;` (5/18em), `\!` (−3/18em), `\quad` (1em), `\qquad` (2em), `\space`, `\nobreakspace` — все валидны |
| 11 | `\,` `\;` `\:` `\!` (тонкие пробелы) | Покрыто | `BinarySpacingExpander` сам вставляет `\,\,` — движок их ест; KaTeX-таблица выше подтверждает семантику ширин |
| 12 | `\begin{pmatrix}` | Покрыто (`sample.md:126`, проба `valid=True`) | KaTeX-референс подтверждает всё семейство `matrix`/`pmatrix`/`vmatrix`/`Vmatrix`/`Bmatrix`/`smallmatrix` |
| 13 | `\begin{aligned}` / `\begin{cases}` | **Покрыто движком (факт пробы)** | Проба: оба → `valid=True`. KaTeX-референс: `cases` — валиден; `aligned`/`alignedat`/`gathered` — валидны, но только внутри math-делимитеров (что у нас и так); `darray`/`dcases`/`drcases` — displaystyle-варианты |
| 13a | `\begin{equation}` / `\begin{align}` / `\begin{gather}` | **Не покрыто движком (факт пробы)** | Проба: `Unknown environment equation`, `Unknown environment align`. KaTeX-референс: `equation` валиден только в связке `\begin{equation}\begin{split}...\end{split}\end{equation}`; `align`/`gather` (+`*`-формы, `\nonumber`) — валидны. Для CSharpMath — вне scope: в файле не используются; при встрече — fallback по ADR-0009 п.4 |
| 14 | `\approx`, `\times`, `\geq`, `\gg`, `\ldots`, `\exp`, `\left`/`\right`, `\sigma` | **Покрыты движком (факт пробы)** | Проба: `a\approx b\times c\geq d`, `n_s\gg p_s`, `a\ldots z`, `n_i\exp\left(\frac{E_t-E_i}{k_BT}\right)`, `\sigma_n,\sigma_p`, вложенный `\dfrac`-блок файла `:61–63` — все `valid=True` |
| 15 | `<caption>` после таблицы | **Не покрыто** | Файл `:175`; generic-HTML дропается (`Images.cs:19–42`, `HtmlBlocks.cs:84–93`). Отдельная тема, не math, но в том же файле |
| 16 | Формулы внутри таблиц | Условно | Долларовые — покрыты (ADR-0011 замеряет `MarkdownMathInline`); `\(...\)`-вариант заработает только вместе с п. 1 |

Вывод ресерча: разрыв — не в движке отрисовки, а **на слое парсинга**.
Проба 18 формул файла на `Sylinko.CSharpMath.Avalonia 12.0.0`:
16/18 `valid=True`, включая кириллический `\text`, `\ `-пробелы,
`\dfrac`-вложенность, `\left/\right`, `aligned`, `cases`, `pmatrix`.
Единственные провалы движка: `\boxed` (`Invalid command`) и окружения
уровня документа `equation`/`align` (`Unknown environment`) — оба
вне критического пути файла (`\boxed` — 1 место, `:154`; окружения —
0 мест). Markdig-математика не видит `\(...\)` / `\[...\]` (доказано
исходниками парсеров выше), поэтому контент даже не доходит
до `MarkdownMathRenderer`.

## Decision (утверждено 2026-11-18)

1. **Конвертация разделителей — пре-парсинг над рабочей копией, не пост-AST.**
   Факт реализации: Markdig трактует `\(`, `\)`, `\[`, `\]` как экранирование
   пунктуации и отдаёт в AST голые `(` / `)`, поэтому отличить `\(x\)` от
   литерала `(x)` в узлах уже нельзя. Рабочая копия готовится
   `MarkdownLatexMathDelimiters.Prepare` (Infrastructure):
   - `\(...\)` → inline `$...$` — единственная форма, которую Markdig держит
     inline в пунктах списка и ячейках таблиц (`$$x$$` там распадается
     блок-парсером);
   - `\[ ... \]` (открыватель в начале строки, вне строк-таблиц, тело
     многострочное) → отдельный `$$`-блок; `\[` в строке таблицы/тексте —
     LaTeX-перевод строки;
   - code-контексты (inline-span’ы, fenced/indented code, HTML-комментарии)
     не трогаются; `\\(` и висячий `\(` остаются текстом.
   Долларовые пути не тронуты (регрессия `MarkdownMathParsingTests` зелёная).
2. **Каноника ADR-0010 расширена: в текстовую карту идёт TeX *с* Pandoc-разделителями.**
   Решение владельца: рядом с `$...$`-формой в каноническом тексте живут
   `\(...\)` (inline) и `\[\n...\n\]` (display), чтобы копирование отдавало
   формулу в исходной разметке, а `MarkdownStyledText`, `MarkdownClipboardTextHelpers`
   и `MarkdownDocumentTextMap` делили одну каноническую форму (иначе карты и
   styled-text расходятся по длине и ломают выделение/поиск). Это уточняет
   п.2 прежней редакции («TeX без разделителей»).
3. **`\boxed` — экспандер нормализатора.** Движок не знает ни `\boxed`, ни
   `\fbox`, ни `\frame`; `MarkdownMathBoxedExpander` подставляет
   `\overline{\underline{...}}` **до** binary-spacing-прохода.
4. **Граница с движком:** если CSharpMath чего-то не ест — работает
   существующий контракт ADR-0009 п.4 (показ исходника), а не молчаливое
   пропадание; каждый случай фиксируется экспандером в `MarkdownMathTexNormalizer`.

### Отклонённые варианты (по итогам реализации)

- **Пост-AST конвертация** — невозможна: бэкслэш `\(` уже стёрт Markdig.
- **`string.Replace` по всему документу** — ломает code-блоки и `\\(`.
- **`$$x$$` для инлайн-формул** — рассыпается в списках/таблицах.
- **`$${ … }$$` одной строкой для display** — не распознаётся как `MathBlock`.
- **`\fbox` как аналог `\boxed`** — движок его тоже не знает.
- **Полный переход на Pandoc-семантику** (включая `\\(...\\)`) — второй шаг.
- **«Показать TeX как код»** — убрано в ADR-0009.

## Consequences (ожидаемые)

Positive:

- файл-триггер и весь класс LaTeX/Pandoc-документов рендерятся без
  правок со стороны авторов;
- движок, кэш (ключ — нормализованный LaTeX) и контракт fallback'а
  переиспользуются без изменений;
- таблицы с `\(...\)` получают корректный замер формул по ADR-0011.

Negative / риски:

- новый пре-AST слой — ложные срабатывания на экранированных скобках и
  code-спанах; покрыто тестами (`MarkdownLatexMathDelimiterTests`);
- `\boxed` — единственный реальный пробел движка в файле (`:154`);
  закрыт экспандером (`\overline{\underline{...}}`);
- каноника с разделителями меняет длину формулы в текстовой карте: формула
  теперь `\(\TeX\)` = `Content.Length + 4` (`\[\n` + TeX + `\n\]` для блока),
  поэтому фрагменты выделения и styled-text обязаны считать так же
  (`MarkdownClipboardTextHelpers.MathInlineTextLength`);
- файл-триггер станет неявной фикстурой: его правки могут «качать»
  визуальные ожидания.

## Scope of changes (итог)

- Infrastructure: `MarkdownLatexMathDelimiters` (`…`, `.Inline`, `.Display`,
  `.Scan`, `.Helpers` — пре-парсинг рабочей копии) и врезка в
  `MarkdigMarkdownDocumentRenderer.Render`; `\\(...\\)`-формат — вторым шагом.
- Presentation: `MarkdownMathBoxedExpander` в `MarkdownMathTexNormalizer`;
  каноническая длина формулы с разделителями в `MarkdownStyledText` и
  `MarkdownClipboardTextHelpers`; `MarkdownDocumentView.Math` матчит fragment
  по новой канонике.
- Domain: `MarkdownDocumentTextMap` кладёт в текст `\(...\)` / `\[\n…\n\]`.
- Tests: `MarkdownLatexMathDelimiterTests`, `MarkdownMathSelectionTests`
  (канонические offsets), `MarkdownMathTexNormalizerTests` (`\boxed`).
- Docs: этот ADR, `docs/handoff/handoff_adr_0012_…`.
- Файл-триггер в репозиторий фикстурой не добавляется.

## Acceptance criteria

- `1_SPECIFICATION_AND_INVARIANTS_SDD_CORE.md` открывается в ридере:
  инлайн-формулы в тексте/таблицах рисуются, 12 дисплей-блоков —
  центрированные блоки, сырого `\(`/`\)`/`\[`/`\]` на экране нет;
- `$...$` / `$$...$$` работают как раньше (существующие math-тесты зелёные);
- висячие/экранированные скобки и code-спаны с `\(` остаются текстом;
- невалидный LaTeX внутри новых разделителей показывает исходник,
  документ не ломается;
- канонический текст (текстовая карта, styled-text, выделение/копирование)
  хранит формулу как `\(...\)` / `\[\n...\n\]` — одна форма во всех слоях,
  так что копирование отдаёт формулу в исходной разметке.

## Итог реализации (2026-11-18)

- Реализовано: `MarkdownLatexMathDelimiters.*` (Infrastructure),
  `MarkdownMathBoxedExpander` (Presentation), каноника с разделителями в
  `MarkdownDocumentTextMap`, `MarkdownStyledText`, `MarkdownClipboardTextHelpers`.
- Тесты: `MarkdownLatexMathDelimiterTests`, `MarkdownMathSelectionTests`
  (канонические offsets с разделителями), `MarkdownMathTexNormalizerTests`
  (`\boxed`); dollar-path регрессии зелёные.
- Сборка: `dotnet build MarkMello.sln` — 0 предупреждений/ошибок;
  exe собран через `dotnet publish` (см. `packaging/README.md`).
- Вне scope: `\\(...\\)`, `<caption>`, `equation`/`align`.
