# Question types and grading contract

| | |
|---|---|
| **Status** | Accepted — contract version **1** |
| **Decided in** | Issue #18 (parent #3) |
| **Binding for** | #19, #21, #30, #31, #40, #43, #44, #45, #46, #49, #50, #57, #58, #59, #60 |
| **Changing it** | Any change to a rule below is a new contract version (see [§5.5](#55-versioning)). Existing attempt snapshots keep being graded by the version they were created with. |

This is the single reference for how a question is stored, what the student types or clicks, how the input is validated, and how it is scored. Everything here is a decision, not an option list. Where a rule says **reject**, the server returns a validation error and nothing is graded. Where it says **incorrect**, the answer is well-formed and scores 0.

Words MUST / MUST NOT / MAY are used in their RFC 2119 sense.

---

## 1. Decisions at a glance

| Topic | Decision |
|---|---|
| Question types (first release) | `single_choice`, `numeric`, `short_text`. Nothing else. |
| Scoring | All-or-nothing per question: `0` or `MaxPoints`. **No partial credit, no negative marking.** |
| Points | `MaxPoints` is an integer from 1 to 20. Awarded points are always `0` or `MaxPoints`. |
| Blank answer | Not an error. Stored as `NULL`, status *Unanswered*, 0 points. |
| Malformed answer | Rejected at validation (never silently scored 0). Blocks final submission until fixed or cleared. |
| Whitespace | Leading/trailing trimmed everywhere. Inside a number: forbidden, except around the `/` of a fraction. In `short_text`: all whitespace ignored. |
| Decimal separator | `.` and `,` are both accepted as the decimal mark. Neither is ever a thousands separator. |
| Equivalent answers | `numeric`: equal by exact value (`0.75` = `0,75` = `3/4` = `6/8`). `short_text`: only the explicit variants listed in the key. No symbolic/algebraic equivalence in v1. |
| Numeric tolerance | Absolute, inclusive (`\|answer − key\| ≤ tolerance`), default `0`, evaluated in exact rational arithmetic. |
| Stored answer key and grading config | One JSON document per question (`AnswerConfig`), validated at write time. |
| Mathematical notation | TeX inside `$…$` (inline) and `$$…$$` (display), rendered client-side with KaTeX from HTML-encoded text. No raw HTML. No literal `$`. |
| Hints | Ordered list of 0–3 plain-notation strings. Shown in practice and in completed-attempt review; never during an active full test. |
| Worked solution | Ordered list of 1–10 steps. **Required** for every question. Shown only after submission or an explicit practice reveal. |
| Figures/images | Out of scope for v1. |

---

## 2. Question types

Every question has exactly one `Type`. The type decides the input control, the shape of `AnswerConfig`, how the raw text is validated, and how it is compared.

### 2.1 `single_choice`

One correct option out of 2–6.

- **Control:** a radio group inside a `<fieldset>` with the question as its `<legend>`, plus a **Clear answer** button (the only way to return to *blank* once an option is selected).
- **Submitted value:** the option **key** (`A`–`F`), never the option text.
- **Parsing:** trim → NFC → upper-case. The result MUST equal one of the question's option keys, otherwise **reject** with `choice_unknown`.
- **Grading:** correct iff the key equals `correct`.

| Input (key: options A–D, correct = `B`) | Result |
|---|---|
| `B` | Correct |
| `b`, ` B ` | Correct (normalised to `B`) |
| `A`, `C`, `D` | Incorrect |
| *(null / empty / spaces)* | Unanswered |
| `E` (no such option), `B,C`, `4`, `AB` | **Rejected** — `choice_unknown` |

Options are always shown in stored order; shuffling is not part of v1.

### 2.2 `numeric`

A single number: integer, decimal or fraction.

- **Control:** `<input type="text" autocomplete="off" spellcheck="false" maxlength="30">` with a visible `<label>` and a hint line *"Number, decimal (3.5 or 3,5) or fraction (3/4)"*. **Not** `type="number"`: it blocks fractions, localises decimal marks inconsistently and silently returns an empty value for text it dislikes. No `inputmode="decimal"` either (mobile decimal keypads lack `-` and `/`).
- **Grammar** (applied after trim and normalisation, §3.2):

  ```
  number   = integer | decimal | fraction
  integer  = [sign] digits
  decimal  = [sign] digits ("." | ",") digits
  fraction = [sign] digits  ws*  "/"  ws*  digits        ; denominator unsigned, not zero
  sign     = "+" | "-"                                    ; U+2212 "−" and U+2013 "–" are normalised to "-"
  digits   = one or more of 0-9                           ; leading zeros allowed
  ws       = any whitespace                               ; allowed only around "/"
  ```
- **Length:** at most 30 characters after trimming; longer is `numeric_format`.
- **Value:** the parsed value is an exact rational number (never a binary float).
- **Grading:** correct iff for **any** key value `k` in `values`: `|answer − k| ≤ tolerance`.

Accepted (all well-formed; correctness depends on the key):

`7` · `+7` · `07` · `-0` · `7.0` · `7,0` · `-3,5` · `−3.5` (U+2212) · `14/2` · `14 / 2` · `-3/4`

Rejected with `numeric_format`:

| Input | Why |
|---|---|
| `seven`, `7cm`, `7 cm` | words/units — the question text must say "enter a number without units" |
| `1e3`, `√2`, `π` | no exponent notation, roots or constants |
| `7%` | no percent sign |
| `1 000`, `1,000.5`, `1.000.000`, `1,5,5` | no thousands separators, no spaces inside a number (note `1,000` is *well-formed* and means **1**, because a comma is always the decimal mark) |
| `.7`, `7.` | at least one digit on each side of the decimal mark |
| `- 7` | sign must touch the digits |
| `3 1/2` | no mixed numbers; write `7/2` or `3.5` |
| `7/-1`, `1.5/2`, `3/4/5`, `(7)` | denominator unsigned; numerator and denominator integers; no brackets |
| 31+ characters | too long |

Rejected with `numeric_zero_denominator`: `7/0`, `0/0`.

**Rounded answers.** If the question says "round to two decimal places", the author sets the key to the rounded value and `tolerance` to `"0"`. Then `0.33` and `33/100` are correct, and `1/3`, `0.3333`, `0.34` are *incorrect* (they ignore the instruction). Use a non-zero tolerance only when the question explicitly asks for an estimate within a stated error.

### 2.3 `short_text`

A short typed answer compared against a list of accepted spellings. For answers that are not a single number (an expression in a fixed form, a letter, a word).

- **Control:** `<input type="text" maxlength="100" autocomplete="off" autocapitalize="off" spellcheck="false">` with a `<label>`.
- **Parsing:** never invalid except by length: more than 100 characters → **reject** `text_too_long`.
- **Comparison:** both the student's text and every key in `answers` go through the same normalisation (§3.2): NFC, minus variants → `-`, case-fold (unless `caseSensitive`), **remove all whitespace**. Correct iff the results are equal to the normalised form of any listed answer.
- **There is no algebra.** `x-(-1)` does not equal `x+1`, `(x+1)` does not equal `x+1`. If several spellings are acceptable the author lists each one. Questions MUST state the required form ("write the result in the form `x+a`"), and answers MUST be typeable on a standard keyboard (no `∞`, `≤`, `√`; spell with ASCII).

| Input (key `["x+1","1+x"]`, case-insensitive) | Result |
|---|---|
| `x+1`, ` X + 1 `, `x + 1`, `1+x`, `1 + X` | Correct |
| `x+1.0`, `(x+1)`, `x-(-1)`, `x+2`, `x` | Incorrect |
| *(blank)* | Unanswered |
| 101 or more characters | **Rejected** — `text_too_long` |

---

## 3. Rules shared by all types

### 3.1 Processing order

For every raw answer, in this order:

1. **Blank check.** `null`, empty, or only whitespace (any Unicode whitespace, including NBSP) → *blank*. Stop. Blank is stored as `NULL`.
2. **Trim** leading and trailing whitespace.
3. **Hard length cap:** more than 200 characters → reject `answer_too_long` (also the column size).
4. **Type-specific parse** (§2) → `valid(normalised)` or reject with its error code.
5. **Grade** (only for valid answers, §4).

Steps 1–4 are **validation**, step 5 is **grading**. Validation never reads the answer key except to learn the option keys of a `single_choice` question.

### 3.2 Normalisation (used by `numeric` and `short_text`)

1. Unicode **NFC** (canonical composition, so composed and decomposed letters such as Cyrillic `й` compare equal). Compatibility forms are deliberately **not** folded (no NFKC): `7²` must not become `72`, and full-width `７` is simply not an ASCII digit.
2. `U+2212` (−) and `U+2013` (–) → `-`.
3. `short_text` only: case-fold unless `caseSensitive`; then delete all whitespace.

Case-folding is Unicode (not culture-dependent), so Cyrillic answers compare correctly; implementations MUST NOT use the current culture.

### 3.3 Blank answers and unanswered questions

| Context | Behaviour |
|---|---|
| Draft save in an active full test | Any raw text ≤ 200 chars is stored as typed (trimmed) **without validation**, so nothing the student typed is lost. Blank is stored as `NULL`. |
| Final submission of a full test | Blank answers are **allowed**: scored 0, status *Unanswered*, no error. |
| Final submission with a malformed answer | **Rejected as a whole**: attempt stays active, nothing is graded, the response lists a per-question error (code + message) for every malformed answer. The student fixes the field or clears it, then submits again. All other saved answers are untouched. |
| Individual practice submit | Blank → validation error *"Enter an answer."* (code `blank`); malformed → the type's error. Nothing is graded or recorded as an outcome until the answer is valid. |

### 3.4 Validation messages

Messages are actionable and MUST NOT reveal the key or any option's correctness.

| Code | Message |
|---|---|
| `blank` (practice only) | Enter an answer. |
| `numeric_format` | Enter a number such as 12, -3.5, 3,5 or 3/4. Do not add units, percent signs, thousands separators or spaces inside the number. |
| `numeric_zero_denominator` | The denominator of a fraction cannot be 0. |
| `choice_unknown` | Choose one of the listed options. |
| `text_too_long` | Answer must be at most 100 characters. |
| `answer_too_long` | Answer must be at most 200 characters. |

### 3.5 Derived status

No separate status column is needed. For a finalised answer row:

- **Unanswered** ⇔ `SubmittedAnswer IS NULL` (points `0`)
- **Correct** ⇔ `PointsAwarded = MaxPoints` (always ≥ 1)
- **Incorrect** ⇔ answered and `PointsAwarded = 0`

At finalisation a row MUST exist for **every** question of the attempt (with `NULL` for unanswered ones), so each has a recorded score.

---

## 4. Grading

1. **Pure function.** `Grade(contractVersion, type, answerConfigJson, maxPoints, submittedAnswer)` → `Unanswered | Incorrect | Correct` plus points. It reads only its arguments (in practice: the attempt/practice **snapshot**, §5.4), never the live question and never anything the client sends other than the raw answer text.
2. **Points.** `Correct` → `MaxPoints`; `Incorrect` and `Unanswered` → `0`. Awarded points can never exceed `MaxPoints` (cap defensively).
3. **No partial credit** in v1 — not for fractions of an answer, not for the right method, not for "close" answers beyond the explicit `tolerance`. A multi-step problem is authored as several questions.
4. **No negative marking.** The minimum total is 0.
5. **Total.** `Total = Σ awarded points`; `MaxTotal = Σ MaxPoints` over the attempt's snapshotted questions. Percentages are derived at display time and not stored.
6. **Tolerance.** `tolerance` is a non-negative number string in the `numeric` grammar (default `"0"`). The check is `|answer − key| ≤ tolerance` — **inclusive**, **absolute**, computed with exact rationals (`BigInteger` numerator/denominator in .NET, or `decimal` where all inputs are decimals). `float`/`double` MUST NOT be used: in IEEE doubles `2.6 − 2.5 = 0.10000000000000009 > 0.1`, which would wrongly reject the on-boundary answer `2.6` for key `2.5`, tolerance `0.1`. Relative tolerance does not exist in v1.
7. **Several correct values.** For `numeric`, `values` may list up to 10 keys (e.g. both roots of `x² = 9` when the question says "enter one solution"); the answer is correct if it matches any of them within tolerance.
8. **Assisted practice.** A practice answer submitted after an explicit solution reveal is graded by the same rules; the *assisted* flag (#60) is stored separately and assisted answers never count as independent successes.
9. **Idempotent.** Grading the same snapshot and answer twice always gives the same result.

---

## 5. Stored representation

### 5.1 Question content columns (for #19; copied verbatim into snapshots by #21)

| Column | Type | Rules |
|---|---|---|
| `Type` | enum stored as string: `SingleChoice`, `Numeric`, `ShortText` | required; `CHECK` against the three values |
| `Text` | `nvarchar(2000)` | required; the question statement (§6) |
| `MaxPoints` | `int` | required; `CHECK (MaxPoints BETWEEN 1 AND 20)` |
| `Difficulty` | enum: `Easy`=1, `Medium`=2, `Hard`=3 | required; `CHECK (Difficulty BETWEEN 1 AND 3)` (only the vocabulary is decided here; where difficulty lives — question, test or both — is decided in #19) |
| `AnswerConfig` | `nvarchar(max)` JSON | required; `CHECK (ISJSON(AnswerConfig) = 1)`; shape by type (§5.2) |
| `Hints` | `nvarchar(max)` JSON array of strings | required, default `[]`; 0–3 items, each ≤ 500 chars; `ISJSON` check |
| `Solution` | `nvarchar(max)` JSON array of strings | required; **1–10 steps**, each ≤ 1000 chars; `ISJSON` check |
| `ContractVersion` | `smallint` | required, currently `1` |

The topic, ordering and test-membership of a question are #19's concern and do not affect this contract.

Per-answer storage (for #20): `SubmittedAnswer nvarchar(200) NULL` holds the raw text trimmed (for `single_choice`, the normalised key) and `PointsAwarded int NULL` (null while the attempt is active; `0` or `MaxPoints` once finalised).

### 5.2 `AnswerConfig` JSON

camelCase property names, strings for all numbers (no JSON numbers, to avoid float round-trips). **Unknown properties are invalid.** The discriminator is the `Type` column, not a field in the JSON.

`single_choice`:

```json
{ "options": [ { "key": "A", "text": "$x=3$" }, { "key": "B", "text": "$x=4$" } ], "correct": "B" }
```

`numeric`:

```json
{ "values": ["3/4"], "tolerance": "0" }
```

`short_text`:

```json
{ "answers": ["x+1", "1+x"], "caseSensitive": false }
```

`tolerance` and `caseSensitive` may be omitted (defaults `"0"` and `false`). The **first** entry of `values` / `answers` is the *display answer* shown in review ("Correct answer: …"); for `single_choice` it is the option named by `correct`.

### 5.3 Config validity rules

Checked when content is written (seed loader, any future editor) and again when a snapshot is created. Invalid config MUST NOT be saved.

- `single_choice`: 2–6 options; keys are consecutive letters from `A` (`A`,`B`,… no gaps, no duplicates); each option `text` is non-empty, ≤ 300 chars, same notation rules as §6; `correct` is one of the keys.
- `numeric`: 1–10 `values`, each parseable by the numeric grammar and **distinct by value**; `tolerance` parseable and ≥ 0. Keys are authored with `.` as the decimal mark.
- `short_text`: 1–10 `answers`, each ≤ 100 chars and non-empty after normalisation; no two answers equal after normalisation (using the question's `caseSensitive`).
- `MaxPoints` in 1–20; `Solution` has ≥ 1 step; `Hints` ≤ 3 items; lengths as in §5.1.
- No hint may state the final answer; the last solution step MUST state the final answer in a form the question's own rules would accept (checked by review in #30).

### 5.4 Snapshots (for #21)

At attempt (or practice) creation copy `Type`, `Text`, `MaxPoints`, `AnswerConfig`, `Hints`, `Solution`, `ContractVersion`, plus topic name and difficulty. Grading and rendering read **only** the snapshot. Snapshot rows are written once; answer-saving code has no write path to them.

### 5.5 Versioning

`ContractVersion` selects the validation/normalisation/grading rules. Version 1 is this document. A grader receiving an unknown version MUST fail loudly rather than guess. A change to any rule here (new type, partial credit, a different normalisation) bumps the version; it never silently changes how existing snapshots are graded.

---

## 6. Mathematical notation, hints and worked solutions

**Format.** `Text`, option text, hints and solution steps are plain text with TeX math:

- inline: `$\frac{3}{4}$` — display: `$$x = \frac{-b \pm \sqrt{b^2-4ac}}{2a}$$`
- a blank line separates paragraphs; a single newline is a line break
- **no HTML and no Markdown.** The server HTML-encodes the whole string; KaTeX (auto-render, self-hosted in `wwwroot/lib`, delimiters `$…$` and `$$…$$` only) typesets the math. KaTeX's MathML output is the accessible representation.
- a literal dollar sign is **not allowed**; write currency as "лв." / "BGN" or inside `\text{}`. Therefore the number of `$` signs in any string must be even and every `$`/`$$` must close — enforced by the config/content validator.
- only KaTeX-supported TeX; the validator renders each formula once with `throwOnError: true` in tests so broken TeX cannot be seeded.

**Students never type TeX.** Answer boxes accept plain text only (§2).

**Hints.** 0–3, ordered from gentle to strongest. A hint may point at the method but MUST NOT contain the final answer. Hints without content are simply absent (empty array → no hint control rendered).

**Worked solution.** 1–10 ordered steps, one idea per step; the last step states the final answer. Rendered as an ordered list. Solutions explain; they do not depend on the student's input.

---

## 7. Who may see what

Enforced on the server for full-page **and** partial/handler requests; hiding with CSS is not enough.

| Context | Statement & options | Hints | Correct answer | Solution |
|---|---|---|---|---|
| Active full test | yes | **no** | **no** | **no** |
| Submitted full test (review) | yes | yes | yes | yes |
| Practice, before submitting | yes | yes, on request | no | no |
| Practice, after submitting | yes | yes | yes | yes |
| Practice, after explicit reveal | yes | yes | yes | yes (answer flagged *assisted*) |

The active-test response MUST NOT contain `AnswerConfig` fields other than option keys and texts (never `correct`, `values`, `tolerance`, `answers`, `caseSensitive`), nor hints or solutions. Review and practice read these from the snapshot.

---

## 8. Out of scope for v1 (decided, not deferred)

Multi-select and matching/ordering questions; free-form, proof or graph answers; symbolic equivalence (`x+1` vs `1+x` is handled only by listing both); percent, units, scientific notation, mixed numbers, roots/constants (`√2`, `π`) as input; partial credit and negative marking; relative tolerance; option shuffling; images and figures; a TeX input box for students. Adding any of these is a new contract version.

---

## 9. Sample questions walked through storage → input → scoring

Each sample shows the stored row, then the raw inputs and the outcome obtained by applying §3–§4. Every row below was produced by running a reference implementation of these rules (all expectations were written down independently beforehand; there were no mismatches). The results are the test vectors for #43, #44 and #46.

Legend: **Correct** → `MaxPoints`; **Incorrect** and **Unanswered** → 0; **Rejected** → validation error, nothing graded.

### 9.1 Q1 — single_choice, 1 pt

> Solve $2x+3=11$. What is $x$?

Stored (`Type` / `MaxPoints` / `AnswerConfig`):

```json
{"Type": "SingleChoice", "MaxPoints": 1, "AnswerConfig": {"options": [{"key": "A", "text": "$x=3$"}, {"key": "B", "text": "$x=4$"}, {"key": "C", "text": "$x=7$"}, {"key": "D", "text": "$x=14$"}], "correct": "B"}}
```

| Raw input | Result | Why |
|---|---|---|
| `B` | Correct (+1) |  |
| `b` | Correct (+1) | normalised to `B` |
| ` B ` | Correct (+1) | trimmed |
| `A` | Incorrect |  |
| `D` | Incorrect |  |
| *(no value / null)* | Unanswered |  |
| *(empty)* | Unanswered |  |
| *(whitespace only)* | Unanswered |  |
| `E` | **Rejected** — `choice_unknown` | no option `E` |
| `B,C` | **Rejected** — `choice_unknown` |  |
| `4` | **Rejected** — `choice_unknown` | option text/value is not a key |

### 9.2 Q2 — numeric, 2 pts, tolerance 0

> Evaluate $(-2)^3 + 5\cdot 3$. Enter a number without units.

Stored (`Type` / `MaxPoints` / `AnswerConfig`):

```json
{"Type": "Numeric", "MaxPoints": 2, "AnswerConfig": {"values": ["7"], "tolerance": "0"}}
```

| Raw input | Result | Why |
|---|---|---|
| `7` | Correct (+2) |  |
| ` 7 ` | Correct (+2) |  |
| `+7` | Correct (+2) | explicit plus sign is fine |
| `07` | Correct (+2) | leading zero is fine |
| `7.0` | Correct (+2) |  |
| `7,0` | Correct (+2) | comma is the decimal mark |
| `14/2` | Correct (+2) | fraction equal to 7 |
| `14 / 2` | Correct (+2) |  |
| `␣NBSP␣7␣NBSP␣` | Correct (+2) |  |
| `-7` | Incorrect | wrong sign |
| `−7` | Incorrect | `−` is normalised to `-`: still wrong sign |
| `8` | Incorrect |  |
| `6.99` | Incorrect |  |
| *(empty)* | Unanswered |  |
| *(whitespace only)* | Unanswered |  |
| `seven` | **Rejected** — `numeric_format` | words not allowed |
| `7cm` | **Rejected** — `numeric_format` | units not allowed |
| `1e1` | **Rejected** — `numeric_format` | no exponent notation |
| `7/0` | **Rejected** — `numeric_zero_denominator` | zero denominator |
| `1 000` | **Rejected** — `numeric_format` | space inside a number |
| `7.` | **Rejected** — `numeric_format` | digit required after the decimal mark |
| `.7` | **Rejected** — `numeric_format` | digit required before the decimal mark |
| `7%` | **Rejected** — `numeric_format` | no percent sign |
| `1.000.000` | **Rejected** — `numeric_format` | no thousands separators |
| `1,5,5` | **Rejected** — `numeric_format` | two decimal marks |
| `- 7` | **Rejected** — `numeric_format` | sign must touch the digits |
| `3 1/2` | **Rejected** — `numeric_format` | mixed numbers not accepted |
| `7/-1` | **Rejected** — `numeric_format` | denominator must be unsigned |
| `1.5/2` | **Rejected** — `numeric_format` | fraction parts must be integers |
| `(7)` | **Rejected** — `numeric_format` | no brackets |
| `7²` | **Rejected** — `numeric_format` | superscript is not folded to a digit |
| `７` | **Rejected** — `numeric_format` | not an ASCII digit |
| `111111…` (31 chars) | **Rejected** — `numeric_format` |  |

### 9.3 Q3 — numeric, 2 pts, fraction key

> Solve $4x=3$. Enter $x$ as a decimal or a fraction.

Stored (`Type` / `MaxPoints` / `AnswerConfig`):

```json
{"Type": "Numeric", "MaxPoints": 2, "AnswerConfig": {"values": ["3/4"], "tolerance": "0"}}
```

| Raw input | Result | Why |
|---|---|---|
| `3/4` | Correct (+2) |  |
| `0.75` | Correct (+2) |  |
| `0,75` | Correct (+2) |  |
| `0.750` | Correct (+2) |  |
| `6/8` | Correct (+2) | equal by value |
| `3 / 4` | Correct (+2) |  |
| `3  /  4` | Correct (+2) | any whitespace around `/` |
| `+0,75` | Correct (+2) |  |
| `-0.75` | Incorrect |  |
| `-3/4` | Incorrect |  |
| `0.7` | Incorrect |  |
| `0.76` | Incorrect |  |
| `0.7500001` | Incorrect | tolerance is 0: must match exactly |
| `4/3` | Incorrect |  |
| `75%` | **Rejected** — `numeric_format` | no percent sign |
| `3/4/5` | **Rejected** — `numeric_format` | one slash only |
| `0..75` | **Rejected** — `numeric_format` | malformed decimal |

### 9.4 Q4 — numeric, 3 pts, tolerance 0.1 (boundary behaviour)

> Estimate $\sqrt{6.25}$ to within $0.1$.

Stored (`Type` / `MaxPoints` / `AnswerConfig`):

```json
{"Type": "Numeric", "MaxPoints": 3, "AnswerConfig": {"values": ["2.5"], "tolerance": "0.1"}}
```

| Raw input | Result | Why |
|---|---|---|
| `2.5` | Correct (+3) |  |
| `2.4` | Correct (+3) | exactly on the boundary |
| `2.6` | Correct (+3) | exactly on the boundary (2.6 − 2.5 = 0.1, so the check is inclusive) |
| `2,6` | Correct (+3) |  |
| `5/2` | Correct (+3) |  |
| `13/5` | Correct (+3) | 13/5 = 2.6, on the boundary |
| `2.61` | Incorrect | just outside (0.11) |
| `2.39` | Incorrect | just outside (0.11) |
| `2.7` | Incorrect |  |
| `3` | Incorrect |  |
| *(empty)* | Unanswered |  |

### 9.5 Q5 — numeric, 2 pts, two accepted values

> Solve $x^2 = 9$. Enter one solution.

Stored (`Type` / `MaxPoints` / `AnswerConfig`):

```json
{"Type": "Numeric", "MaxPoints": 2, "AnswerConfig": {"values": ["3", "-3"], "tolerance": "0"}}
```

| Raw input | Result | Why |
|---|---|---|
| `3` | Correct (+2) | first listed value |
| `-3` | Correct (+2) | second listed value |
| `−3` | Correct (+2) | normalised to `-3` |
| `+3` | Correct (+2) |  |
| `9` | Incorrect |  |
| `0` | Incorrect |  |
| `±3` | **Rejected** — `numeric_format` | `±` not allowed; enter a single solution |
| `3;-3` | **Rejected** — `numeric_format` | one value only |
| `3 or -3` | **Rejected** — `numeric_format` | one value only |

### 9.6 Q6 — numeric, 2 pts, rounded answer

> Calculate $\dfrac{1}{3}$ rounded to two decimal places.

Stored (`Type` / `MaxPoints` / `AnswerConfig`):

```json
{"Type": "Numeric", "MaxPoints": 2, "AnswerConfig": {"values": ["0.33"], "tolerance": "0"}}
```

| Raw input | Result | Why |
|---|---|---|
| `0.33` | Correct (+2) |  |
| `0,33` | Correct (+2) |  |
| `0.330` | Correct (+2) | equals 0.33 |
| `33/100` | Correct (+2) | equals 0.33 |
| `1/3` | Incorrect | question asked for 2 decimals; tolerance 0 |
| `0.3333` | Incorrect | not rounded as asked |
| `0.3` | Incorrect |  |
| `0.34` | Incorrect |  |

### 9.7 Q7 — short_text, 2 pts, case-insensitive variants

> Simplify $\dfrac{x^2-1}{x-1}$ for $x \neq 1$. Write the result in the form $x+a$.

Stored (`Type` / `MaxPoints` / `AnswerConfig`):

```json
{"Type": "ShortText", "MaxPoints": 2, "AnswerConfig": {"answers": ["x+1", "1+x"], "caseSensitive": false}}
```

| Raw input | Result | Why |
|---|---|---|
| `x+1` | Correct (+2) |  |
| ` X + 1 ` | Correct (+2) | case-folded, whitespace ignored |
| `x + 1` | Correct (+2) |  |
| `1+x` | Correct (+2) |  |
| `1 + X` | Correct (+2) | second listed variant |
| `x␣NBSP␣+␣NBSP␣1` | Correct (+2) |  |
| `x+1.0` | Incorrect | not listed |
| `x²` | Incorrect | superscript is not folded; text differs from every key |
| `(x+1)` | Incorrect | no algebra: brackets make it different |
| `x-(-1)` | Incorrect | equivalent algebraically, but not listed |
| `x+2` | Incorrect |  |
| `x` | Incorrect |  |
| *(empty)* | Unanswered |  |
| *(whitespace only)* | Unanswered |  |
| `xxxxxx…` (101 chars) | **Rejected** — `text_too_long` |  |

### 9.8 Q8 — short_text, 1 pt, case-sensitive

> Which capital letter denotes the set of natural numbers in the standard notation?

Stored (`Type` / `MaxPoints` / `AnswerConfig`):

```json
{"Type": "ShortText", "MaxPoints": 1, "AnswerConfig": {"answers": ["N"], "caseSensitive": true}}
```

| Raw input | Result | Why |
|---|---|---|
| `N` | Correct (+1) |  |
| ` N ` | Correct (+1) | trimmed |
| `n` | Incorrect | `caseSensitive` is true |
| `Z` | Incorrect |  |
| *(empty)* | Unanswered |  |


### 9.1 Whole-attempt walk-through

Attempt over Q1 (1 pt), Q2 (2 pts), Q3 (2 pts); `MaxTotal = 5`.

| Step | Q1 | Q2 | Q3 | Outcome |
|---|---|---|---|---|
| Student's saved drafts | `B` | `14/2` | *(blank)* | |
| Final submission | Correct, 1 | Correct, 2 | Unanswered, 0 | Total **3 / 5** |

Same attempt, but the student saved `seven` in Q2:

| Step | Outcome |
|---|---|
| Final submission | **Rejected.** Attempt stays active; response lists `Q2: numeric_format`; no score is stored. Q1 and Q3 drafts are untouched. |
| Student clears Q2 and submits again | Q1 Correct 1, Q2 Unanswered 0, Q3 Unanswered 0 → Total **1 / 5** |

---

## 10. What each implementing issue takes from this contract

| Issue | Uses |
|---|---|
| #19 | §5.1 columns, check constraints, JSON validity (§5.3), limits |
| #20 | §3.5 derived status, §5.1 answer columns, row-per-question at finalisation |
| #21 | §5.4 snapshot contents, §5.5 versioning |
| #30 / #31 | §5.2–5.3, §6 authoring rules; every type and notation in §9 must appear in seed data |
| #40 / #49 / #50 / #57 | §2 controls, §6 notation, §7 visibility |
| #43 | §2 grammars, §3 processing order, error codes and messages |
| #44 / #45 | §4 grading, §3.3 final-submission behaviour |
| #46 | the vectors in §9 as table-driven cases; add cases for forged answers/scores |
| #58–#60 | §7 practice rows, §4.8 assisted flag |
