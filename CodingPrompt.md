# Cosecha – Coding Prompt (Unity Prototype)

Nov 22, 2026 · @Jonathan Müller

## 1. Role, goal and milestones

Build a first playable Unity prototype of **Cosecha**, a turn-based inventory-management game with roguelike runs and incremental scaling, in which the player learns Spanish vocabulary by planting words on a field. The prototype exists to test the core loop and the balancing; final art is not a goal yet.

The game design in this document is fixed. Where something is unclear, pick the simplest solution, keep it configurable, and list your assumption in a short NOTES.md in the project root.

Work in these milestones and keep the project runnable after each one:

1. Data and pure game logic: vocabulary loading, complexity formula, scoring, shop generation, answer checking, with EditMode unit tests.
2. Field grid: placing, rotating, moving and discarding words by drag and drop; wheelbarrow storage; hover tooltips.
3. Shop and quizzes: word offers, joker, mods, upgrades, reroll, purchase and mod quizzes.
4. Round flow: harvest, targets, gold, scarecrow, game over, local leaderboard, main menu.
5. Placeholder pixel art and polish: sky, clouds, field, house, crop sprites, short feedback animations.

## 2. Platform and architecture

Use Unity 6 LTS with the 2D URP renderer, landscape 16:9, mouse only.

- Pixel Perfect Camera with reference resolution 480×270, upscaled to 1920×1080. New Input System. TextMeshPro for all text, with a free pixel font (or a placeholder).
- The field grid and crops are world-space sprites. Shop, HUD, tooltips and quiz dialogs are uGUI (Screen Space – Camera).
- Keep rules separate from presentation. Core logic lives in plain C# classes without MonoBehaviour, for example ComplexityCalculator, ScoreCalculator, ShopGenerator, AnswerChecker, GridModel, RunState. MonoBehaviours only render state and forward input.
- Every balancing number lives in one ScriptableObject, BalanceConfig (Assets/Config/BalanceConfig.asset). No magic numbers in code.
- Load the vocabulary from Assets/Resources/Vocabulary/vocabulary.json (schema in section 14) with Newtonsoft JSON (package com.unity.nuget.newtonsoft-json), so optional fields can be omitted. Validate on load: skip invalid entries and log a warning naming the entry and the problem.
- UI language is German. Keep all UI strings in one place for later localization.
- Rounding: wherever this document says “round”, use Math.Round(x, MidpointRounding.AwayFromZero). C#’s default banker’s rounding turns 2.5 into 2 and breaks the test values in section 5.

## 3. Visual style and screen layout

The look is simple pixel art of a farm on a sunny day: the player plants vocabulary on a wheat field and shops on a whitewashed roof.

- **Upper half:** clear blue sky with a vertical gradient, lighter near the horizon. Every 8–20 s (random) a small fluffy cumulus cloud (“sheep cloud”) spawns off-screen left and drifts slowly right. Use 2–3 cloud sizes with slight speed variation, behind all UI.
- **Lower half:** plowed brown field with horizontal furrows and a strip of ripe wheat along the edges.
- **Left part of the field:** the plantable grid, which is the inventory. Cells are 16×16 px at reference resolution, darker tilled soil with a subtle border. Start 5×5; the grid can grow to at most 10 columns × 7 rows, so reserve that space.
- **Right side:** a whitewashed Canary-Islands-style house: white cubic walls, flat white roof with a low parapet, wooden door, green or blue shutters. The shop panel sits on top of the flat roof.
- **Next to the grid:** a wheelbarrow (storage, 2 slots) and a compost heap (discard target). A scarecrow stands at the field edge while its save is still available.
- **HUD in the sky:** round number, harvest target, live score preview (current / target, turns green when reached), gold, actions left, scarecrow status. A large “Ernten” button ends the round.
- **Words are crop patches.** Each word type is its own crop and color: noun = wheat (golden yellow), verb = corn (green with yellow cobs), adjective = sunflower (orange-yellow), other = cabbage (pale green). Every cell of a word shows the crop; the anchor cell carries a small wooden sign with the Spanish word. Mods show as small icons on the sign: a tiny “el/la” tag for the article mod, a small star for the conjugation mod.
- **Art:** simple placeholder sprites, generated in code or as small PNGs, Point filter, no compression. Reference every sprite through one SpriteSet ScriptableObject so hand-drawn art can replace them later without code changes.
- **Harvest feedback:** each word bounces briefly and pops its points as floating numbers, then the total counts up. Under 2 s in total, skippable by click.

## 4. Core loop and round flow

A run is a sequence of rounds with exponentially rising harvest targets. It ends at the first missed target unless the scarecrow saves it.

1. **Main menu:** “Neues Spiel”, the local leaderboard (top 10), quit.
2. **Run start:** the 5×5 grid with the four starting words (section 9), 5 gold, 2 actions, scarecrow available, round 1.
3. **Planning phase, no time limit.** In any order the player may buy words, use the joker, buy and apply mods, buy upgrades, reroll the shop, place, move and rotate words, store words in the wheelbarrow and discard words on the compost heap. The live score preview updates after every change.
4. **Harvest (“Ernten”):** compute the round score from all words on the grid (not in the wheelbarrow). The score starts at 0 every round.
5. **Score ≥ target:** add the round score to the run total (the rank), pay gold income (section 9), start the next round. The new round has a new target, a refilled shop, the reroll price reset to its start value and actions reset to their maximum.
6. **Score < target:** if the scarecrow is available, it triggers (section 11) and the run continues as in step 5. Otherwise the run ends (section 12).

Words, mods and upgrades persist for the whole run.

## 5. Word stats from complexity

Every word’s size, base points and gold are computed from its Spanish spelling through a complexity value K, so the vocabulary file needs no balancing data.

```
letters    = number of Unicode letters in the Spanish word
             (ignore spaces, ¿ ? ¡ ! and other punctuation)
K          = letters
           + 2 per accented vowel (á é í ó ú, upper or lower case)
           + 3 per ñ or ü
           + 3 if the word is a verb marked irregular
           (if complexityOverride > 0 in the JSON, use it as K instead)

size       = K <= 4 -> 4 | K 5-8 -> 3 | K 9-12 -> 2 | K >= 13 -> 1
             verbs: +2 (infinitive surcharge; the conjugation mod removes it)
basePoints = round(K^2 / 10)
gold       = K <= 4 -> 4 | K 5-8 -> 2 | K 9-12 -> 1 | K >= 13 -> 0
```

Band limits, the exponent (2), the divisor (10), the verb surcharge and all bonuses belong in BalanceConfig.

Unit test values (must pass):

| Word | Type | K | Size | Base points | Gold |
| --- | --- | --- | --- | --- | --- |
| dos | other | 3 | 4 | 1 | 4 |
| casa | noun | 4 | 4 | 2 | 4 |
| qué | other | 5 | 3 | 3 | 2 |
| comer | verb | 5 | 5 | 3 | 2 |
| grande | adjective | 6 | 3 | 4 | 2 |
| ser | verb, irregular | 6 | 5 | 4 | 2 |
| siempre | other | 7 | 3 | 5 | 2 |
| mañana | other | 9 | 2 | 8 | 1 |
| pequeño | adjective | 10 | 2 | 10 | 1 |
| información | noun | 13 | 1 | 17 | 0 |

## 6. Item shapes and grid interaction

Words are polyominoes from three nested shape families, so a word that shrinks keeps its position and always still fits.

Each family is an ordered list of 6 cells (x, y; origin bottom-left). A word of size k occupies the first k cells of its family. Shrinking removes cells from the end, growing adds them back. Every family fits inside a 5×5 grid at size 6.

| Family | Ordered cells |
| --- | --- |
| Line | (0,0) (1,0) (2,0) (3,0) (4,0) (0,1) |
| Corner | (0,0) (0,1) (1,0) (0,2) (0,3) (0,4) |
| Block | (0,0) (1,0) (0,1) (1,1) (0,2) (1,2) |

- The family is picked at random when a word is bought (weights in BalanceConfig).
- **Rotation:** 90° steps with R or the right mouse button while dragging. Store rotation per word; shrinking and growing happen in the rotated frame around the same anchor.
- **Drag and drop:** a purchased word attaches to the cursor. The placement preview is green when valid, red when not. Left-click places. Words on the grid can be picked up and moved freely at no cost.
- **Wheelbarrow:** 2 slots, any size. Stored words keep their mods but do not score and do not count for synergies. Esc while dragging a new purchase puts it there; if both slots are full, the player must place it or compost it.
- **Compost heap:** dropping a word there discards it and refunds floor(discardRefund × price paid for the word), default 50%. Mods are not refunded.

Hover tooltip on words in the grid and the wheelbarrow:

- Spanish word and German translation. If the player typed the German side in the purchase quiz, show what they typed; otherwise show the first German entry.
- Word type and K.
- With article mod: gender and both articles, e.g. “la casa · una casa”.
- With conjugation mod: the six present-tense forms. Forms the player got right in normal color, missed forms shown with the correct answer in a muted red.
- Current contribution as a short breakdown: base × synergy × article × type multipliers = result, plus gold per round.

## 7. Quizzes and answer checking

Every new word and every mod is earned by typing answers, with 3 attempts per quiz.

**Purchase quiz**

- Each word offer has a pre-rolled direction, 50/50: Spanish → German or German → Spanish. The shop card shows only the prompt side (the Spanish word, or a German translation) plus type, size, points, gold and price. Showing both sides would reveal the answer.
- Clicking Buy opens the quiz dialog. Correct within 3 attempts → the gold is deducted and the word attaches to the cursor. Three misses → the correct answer is shown, no gold is spent, and the offer disappears from the shop for this round.
- Buying and the purchase quiz cost no action.

**Answer checking** (AnswerChecker, unit-tested)

- Trim, collapse inner whitespace, compare case-insensitively.
- Strip ¿ ? ¡ ! . , from input and solution.
- German answers: strip a leading article (der, die, das, ein, eine, einen). Any entry of the “de” array counts.
- Spanish answers: strip a leading article (el, la, los, las, un, una), so “la casa” and “casa” both count.
- Accent mode in BalanceConfig, default Lenient. Lenient accepts an answer that only matches after removing accents (ñ→n, ü→u included), counts it as correct and shows the correct spelling as a hint. Strict requires the exact spelling.
- Below every input field: buttons for á é í ó ú ñ ü ¿ ¡ that insert at the caret. Enter submits.

**Joker**

- The joker offer shows a required word type (noun, verb, adjective or other). The player types a Spanish word.
- The input is valid if it matches, under the rules above, a vocabulary entry of that type that is not on the grid or in the wheelbarrow. Invalid input shows a short reason and uses one of 3 attempts for this joker in this round.
- When valid, show the word’s stats and the joker price = ceil(jokerDiscount × normal word price), default 60%. Confirm buys it with no further quiz; Cancel keeps the joker offer available.

## 8. Mods and actions

Mods are bought in the shop and applied to a word on the grid. Applying a mod costs 1 action, and the player has 2 actions per round.

- **Actions:** 2 per round at start (BalanceConfig), reset every round, +1 per “extra action” upgrade. Only applying a mod costs an action.
- **Targeting:** buying a mod enters targeting mode; valid targets on the grid glow. Esc cancels and refunds the full price. The buy button is disabled, with a tooltip giving the reason, when no action is left or no valid target exists.
- **Failure:** a failed mod quiz uses up the mod, its gold and the action, and shows the correct answer.

**Article mod** (nouns without article mod)

- Quiz: two fields, definite and indefinite article (e.g. “la” and “una”). 3 attempts; both must be correct.
- Effect for the rest of the run: the noun’s points × articleMultiplier, default 1.5. The tooltip shows gender and articles.

**Conjugation mod** (verbs that do not yet have all 6 forms)

- Quiz: the Spanish infinitive and six fields for the present tense: yo, tú, él/ella/usted, nosotros/as, vosotros/as, ellos/ellas/ustedes. After each submit, correct fields lock green and wrong fields can be retried. Up to 3 submits.
- Result: correctForms = correct fields at the end. shrink = floor(correctForms / 2). New size = max(1, unmodded size − shrink). Example: comer goes from 5 to 2 with all 6 forms correct.
- If not all 6 were correct, the verb can receive the mod again later. Forms already correct stay filled and locked; only missing forms are asked.
- The conjugation mod does not change points.

## 9. Scoring, economy and target curve

The round score multiplies each word’s base points by its synergy and upgrades, and must reach the target 8 × 1.25^(round − 1).

```
Ring synergy (only words on the grid count):
  adjectives boost verbs, verbs boost nouns, nouns boost adjectives
  synergy(verb)      = 1 + 0.1 * (number of adjectives on the grid)
  synergy(noun)      = 1 + 0.1 * (number of verbs on the grid)
  synergy(adjective) = 1 + 0.1 * (number of nouns on the grid)

Score of a noun, verb or adjective:
  basePoints * synergy * article * typeMult[type] * typeXMult[type]
  article         = 1.5 if the word has the article mod, else 1
  typeMult[type]  = 1 + 0.5 * (+Mult upgrades bought for that type)
  typeXMult[type] = 1.5 ^ (xMult upgrades bought for that type)

Score of an "other" word:
  basePoints (flat: no synergy, no shop multipliers)

Round score = sum of all word scores on the grid
              (float; display with one decimal; compare as float)
Target(r)   = 8 * 1.25^(r - 1)
Gold income = 3 + sum of gold of all words on the grid
              (paid after every passed round, also after a scarecrow save)
Rank        = sum of all round scores of the run
```

**Starting state:** 5×5 grid, 5 gold, 2 actions, scarecrow available, and four words placed without mods: comer (verb), casa (noun), grande (adjective), dos (other). Expected round-1 score: comer 3 × 1.1 + casa 2 × 1.1 + grande 4 × 1.1 + dos 1 = 10.9 against a target of 8. Expected gold income after round 1: 3 + 2 + 4 + 2 + 4 = 15. Add both as unit tests. If one of these words is missing from the JSON, use the lowest-K entry of the same type instead.

## 10. Shop

The shop refills every round with 3 word offers, 1 joker and 2 upgrade-or-mod offers.

- **Word offers:** drawn from the vocabulary, excluding words on the grid or in the wheelbarrow. Complexity gate: in round r only words with K ≤ 6 + floor(r / 2) are eligible. If fewer than 3 are eligible, fill up with the lowest-K remaining words. If the pool is empty, show an empty slot (“Keine Wörter mehr”).
- **Word price** = 2 + 3 × gold + basePoints. Test values: dos 15, siempre 13, mañana 13, información 19.
- **Joker:** random required type every round; price is computed after input (section 7).
- **Reroll:** replaces all offers (words, joker, upgrades). The price starts at 1 gold and rises by 1 with every reroll; it resets to 1 at the start of each round.
- Unaffordable offers are greyed out. All prices are rounded to whole gold.

Upgrade and mod pool (n = how many of that upgrade were bought before; all values in BalanceConfig):

| Offer | Price (gold) | Effect | Availability |
| --- | --- | --- | --- |
| Article mod | 6 | Section 8 | Common |
| Conjugation mod | 8 | Section 8 | Common |
| +Mult (noun, verb or adjective) | 10 × 1.6^n per type | typeMult +0.5 for that type | Common |
| ×Mult (noun, verb or adjective) | 40 × 2^n per type | typeXMult ×1.5 for that type | Rare, from round 10 |
| Field expansion | 15 × 2^n | +1 column or +1 row, player picks; max 10×7 | Rare |
| Extra action | 20 × 2^n | +1 action per round | Rare |

Default weights: common offers 70% in total, rare offers 30% in total, split evenly within each group. “Other” words never get multiplier upgrades.

## 11. Scarecrow (one-time save)

Once per run, the scarecrow saves a missed harvest at the cost of one learned word.

- **Trigger:** round score < target and the scarecrow is still available. The run continues as if the target had been reached: the round score is added to the rank and gold income is paid. The scarecrow falls over and disappears.
- **Penalty,** checked in this order:
  1. The highest-scoring verb with a conjugation mod loses it: it regrows to its unmodded size and its stored forms are cleared.
  2. If there is no such verb, the highest-scoring noun loses its article mod.
  3. If neither exists, there is no penalty.
- **Regrowing:** add the cells back in place. If they overlap other words or leave the grid, move the word to the wheelbarrow. If the wheelbarrow is full, the word is composted without refund. Show a short message explaining what happened.

## 12. Run end and leaderboard

A missed target without scarecrow ends the run and saves it to a local leaderboard.

- **Game over screen:** rank (total score), round reached, words planted (successful purchases including jokers), mods learned (successful article mods plus conjugation forms learned). Button back to the main menu.
- **Storage:** append every finished run to Application.persistentDataPath/leaderboard.json with date, rank, round reached, words planted and mods learned. Keep all runs; the main menu shows the top 10 by rank.

## 13. Debug, balancing and tests

The prototype exists to tune balancing, so numbers must be easy to change and runs easy to analyze.

- **BalanceConfig** holds every number in this document: start values, formula constants, bands, prices, weights, target curve, multipliers, refund rate, attempts, accent mode.
- **Debug overlay (F1):** add 50 gold, set round, force pass or fail the harvest, refill the shop, reload the vocabulary JSON, toggle per-word score breakdown labels on the grid.
- **Run log:** append one line per round to Application.persistentDataPath/runlog.csv with timestamp, run id, round, target, score, gold before and after, words bought, rerolls, mods applied, quiz attempts and failures.
- **EditMode tests** (Unity Test Framework):
  - complexity table from section 5
  - shape nesting: size k is a subset of size k+1 for every family and rotation
  - answer checking: articles, punctuation, accents in both modes
  - starting inventory: 10.9 points and 15 gold income
  - word prices from section 10
  - targets: round 1 = 8, round 10 ≈ 59.6

## 14. Vocabulary JSON schema and sample file

Create Assets/Resources/Vocabulary/vocabulary.json with the sample below. A real word list in the same schema will replace it later and may be short at first.

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| version | int | Yes (top level) | Schema version, currently 1 |
| entries | array | Yes (top level) | List of vocabulary entries |
| id | string | Yes | Unique, lowercase ASCII, e.g. "informacion" |
| es | string | Yes | Spanish lemma with correct accents: infinitive for verbs, masculine singular for adjectives, singular for nouns |
| de | string\[\] | Yes | Accepted German translations; the first is the main one |
| type | string | Yes | "noun", "verb", "adjective" or "other" |
| noun | object | Nouns only | gender ("m" or "f"), articleDefinite, articleIndefinite |
| verb | object | Verbs only | irregular (bool), present with yo, tu, el, nosotros, vosotros, ellos |
| complexityOverride | int | No | Replaces the computed K when > 0 |
| tags | string\[\] | No | Free labels for later filtering, e.g. "A1", "Essen" |

Articles are stored explicitly because they do not always follow gender: “agua” is feminine but takes “el” and “un”.

```json
{
  "version": 1,
  "entries": [
    { "id": "casa", "es": "casa", "de": ["Haus"], "type": "noun",
      "noun": { "gender": "f", "articleDefinite": "la", "articleIndefinite": "una" }, "tags": ["A1"] },
    { "id": "perro", "es": "perro", "de": ["Hund"], "type": "noun",
      "noun": { "gender": "m", "articleDefinite": "el", "articleIndefinite": "un" }, "tags": ["A1"] },
    { "id": "agua", "es": "agua", "de": ["Wasser"], "type": "noun",
      "noun": { "gender": "f", "articleDefinite": "el", "articleIndefinite": "un" }, "tags": ["A1"] },
    { "id": "informacion", "es": "información", "de": ["Information", "Auskunft"], "type": "noun",
      "noun": { "gender": "f", "articleDefinite": "la", "articleIndefinite": "una" }, "tags": ["A2"] },
    { "id": "comer", "es": "comer", "de": ["essen"], "type": "verb",
      "verb": { "irregular": false, "present": { "yo": "como", "tu": "comes", "el": "come", "nosotros": "comemos", "vosotros": "coméis", "ellos": "comen" } }, "tags": ["A1"] },
    { "id": "hablar", "es": "hablar", "de": ["sprechen", "reden"], "type": "verb",
      "verb": { "irregular": false, "present": { "yo": "hablo", "tu": "hablas", "el": "habla", "nosotros": "hablamos", "vosotros": "habláis", "ellos": "hablan" } }, "tags": ["A1"] },
    { "id": "ser", "es": "ser", "de": ["sein"], "type": "verb",
      "verb": { "irregular": true, "present": { "yo": "soy", "tu": "eres", "el": "es", "nosotros": "somos", "vosotros": "sois", "ellos": "son" } }, "tags": ["A1"] },
    { "id": "tener", "es": "tener", "de": ["haben"], "type": "verb",
      "verb": { "irregular": true, "present": { "yo": "tengo", "tu": "tienes", "el": "tiene", "nosotros": "tenemos", "vosotros": "tenéis", "ellos": "tienen" } }, "tags": ["A1"] },
    { "id": "grande", "es": "grande", "de": ["groß", "gross"], "type": "adjective", "tags": ["A1"] },
    { "id": "rojo", "es": "rojo", "de": ["rot"], "type": "adjective", "tags": ["A1"] },
    { "id": "pequeno", "es": "pequeño", "de": ["klein"], "type": "adjective", "tags": ["A1"] },
    { "id": "dos", "es": "dos", "de": ["zwei"], "type": "other", "tags": ["A1", "Zahl"] },
    { "id": "siempre", "es": "siempre", "de": ["immer"], "type": "other", "tags": ["A1"] },
    { "id": "manana", "es": "mañana", "de": ["morgen"], "type": "other", "tags": ["A1"] },
    { "id": "que", "es": "qué", "de": ["was"], "type": "other", "tags": ["A1", "Fragewort"] }
  ]
}
```
