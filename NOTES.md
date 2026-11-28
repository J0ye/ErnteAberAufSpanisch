# Cosecha – Prototype Notes

## How to run
1. Open `Assets/Scenes/Game.unity` (the only scene in Build Settings) and press Play.
2. Set the Game view to **1920×1080** (or any 16:9 size). Other sizes work too: the Pixel Perfect Camera
   letterboxes with *StretchFill* so the world and the UI stay aligned.
3. Menu **Cosecha → Setup Project** rebuilds the placeholder art, the config assets and the scene (safe to re-run;
   `BalanceConfig.asset` is kept, the scene is rebuilt). **Cosecha → Regenerate Placeholder Art** only redraws the PNGs.

Balancing: `Assets/Config/BalanceConfig.asset`. Art: `Assets/Config/SpriteSet.asset` (PNGs in `Assets/Art/Generated`).
Run log and leaderboard: `Application.persistentDataPath` (`runlog.csv`, `leaderboard.json`).

## Code layout
- `Assets/Scripts/Core` – rules only, no MonoBehaviours: `ComplexityCalculator`, `ScoreCalculator`, `ShopGenerator`,
  `AnswerChecker`, `GridModel` (word bars, `GrowthSchedule`), `SentenceEngine` (pure C#, no Unity references),
  `RunState`, `VocabularyLoader`, persistence, `UiText` (all German strings).
- `Assets/Scripts/View` – `GameController` (input + flow + quizzes), `FieldView`/`SkyView` (world sprites),
  `UiViews` (HUD, shop, dialogs, tooltip, menus, debug). The UI is built from code at runtime.
- `Assets/Scripts/Editor` – placeholder pixel-art generator and project setup.
- `Assets/Tests/EditMode` – 86 tests (change request v2 section 8 plus answer checking, validation, tooltip,
  shed, hot swap). Tests use their own schema-2 sample vocabulary (`TestVocabulary.cs`), so the live word list
  can change freely.

## Change request v2: sentence building (Nov 23, 2026)
Cosecha is now a sentence-building game. **The sections further down describe the earlier packing game; where they
mention shapes, rotation, the synergy ring, mods, upgrades, actions or the old start words, this section wins.**

- **Removed:** shape families and rotation, the synergy ring, article/conjugation mods, +Mult/×Mult, field expansion,
  extra action, the upgrade slots and the action counter (code and config fields deleted, not just hidden).
- **Word bars:** a word of size n occupies n cells in one row; the anchor is the leftmost cell and carries the sign.
  Board row 0 is the **top** row (the model used to count from the bottom); `FlatGrid` is anchored at the top-left.
- **Field:** starts 6×2, max 12×7. `GrowthSchedule.SizeAt(cfg, round)` applies `growthIntervals` [3, 4] and
  `growthOrder` [row, column]; when the scheduled dimension is maxed the other one grows (that is how round 39
  reaches 12×7). Banner „Das Feld wächst!“. The mesa art was regenerated for 12×7.
- **Stats:** function words size 1 / 1 point / 1 gold (price 6); other sizes 3/2/1 by K, verbs +1 cell in the
  infinitive (`verbInfinitiveSurcharge = 1`). `WordStats.UnmoddedSize` is now `BaseSize`.
- **Free grammar action** (`freeGrammarActionsPerRound = 1`): HUD button „Grammatik (n)“ (then click a highlighted
  verb/adjective) or right-click a verb/adjective on the field. The dialog shows person/form buttons and one input,
  3 attempts. State on the word: `WordInstance.VerbPerson` (−1 = infinitive) and `AdjectiveForm` (0 = m. Sg.).
- **SentenceEngine:** patterns are data in `BalanceConfig.sentencePatterns` (category + role sequence such as
  `Subject V:transitive Object`, `Q:object V:transitive Subject?`; syntax in `SentencePattern`). Agreement is in code.
  `RunState.Analysis` caches the result until the field changes.
- **Scoring:** `basePoints × (1 + bonuses)`, bonuses Satzteil 0.5 / Satz 1.5 / Frage 2.0, target `10 × 1.22^(r−1)`.
- **Article merge:** dropping an article on a noun that takes it (definite or indefinite, no article yet) merges them: the noun keeps its size, shows „el pan“, points, gold and price paid add up; the engine treats it as an article+noun NP (counts as 2 words, so a lone merged noun is an NP2 Satzteil; no second article).
- **Start words:** `yo` is fixed; verb and noun are random each run (transitive verb / noun from the `startWordPoolSize` lowest-K entries, toggle `randomizeStartVerbAndNoun`).
- **Shop:** 5 word offers + joker (2×3 grid), need weighting and almost-structure lookahead in `ShopGenerator`.
  Offers that complete an almost-structure show „★ passt zum Satz“. Articles are bought without a quiz,
  pronouns are always asked Spanish → German.
- **Visuals:** per-word outline in a darker shade of the badge color (board, shed, held word, shop preview);
  twines through the sign centers (Satzteil light brown, Satz gold, Frage purple) with labels, pop-in/fade-out on
  every change; at harvest the structures light up Frage → Satz → Satzteil, then the word points pop (2.5 s,
  click skips). Debug overlay (F1) lists all structures and has „+1 Grammatik“.
- **Run log:** new columns `grammarActions` (was `modsApplied`), `satzteile`, `saetze`, `fragen`. An existing
  `runlog.csv` with the old header is renamed to `runlog_old_<timestamp>.csv` on the first write.
  The leaderboard JSON keeps the field name `modsLearned`; it now counts successful grammar actions.

### Readable board (Nov 23, 2026)
- **Field cells are up to 44 px apart** (was 20) with a visible gap (`CellGapShare` 14 %). The pitch is
  `IsoLayout.FieldPitch(columns, rows)`: the board keeps 44 px while the field fits into `FieldAreaPx` (310×184 px,
  left of the shed, down to just above the toast/hint strip) and only shrinks when it would grow into them.
  Schedule: 44 px up to 7×4 (round 11), then 38 → 36 → 34 → 30 → 25 px at 12×7 (still larger than before).
- Each word is drawn as one bar: a dark backing strip fills the gaps between its cells, the outline goes around it,
  and the **sign sits centered on the bar** in its upper part, with the crops below. Signs and their text grow with
  the cells (up to 1.6×); long words shrink their text to fit the bar (one-cell words may overhang slightly).
- Shed: 26 px pitch, moved to x 86–216 so it stays clear of the closed shop handle; compost moved down 4 px.
- Board sprites are still drawn at 20 px and scaled (`FlatGrid.SpriteScale`). HUD, shop, dialogs and the shop
  previews keep their size. At the largest field sizes the board overlaps the top of the decorative ground cliff.

### Vocabulary schema 2
- The loader accepts **only version 2**; any other version logs an error naming the file and loads nothing
  (the main menu then shows that error when „Neues Spiel“ is pressed).
- **Interim live list:** I converted the existing 150-entry `vocabulary.json` to schema 2 so the game keeps running
  until your own v2 file replaces it (173 entries now). What the conversion did:
  - verbs: `transitive` true for tomar, beber, comer, hablar, deletrear, leer, escuchar, estudiar, buscar, tener,
    querer, practicar, saludar, presentar, poder; `copula` true for ser and estar; all others intransitive.
  - adjectives: four forms generated by rule (‑o → ‑a/‑os/‑as, ‑e → ‑es, ‑z → ‑ces, consonant → ‑es).
  - nouns: `number: "sg"` added. Retyped: `cómo` → questionWord (adverb), `cuál` → questionWord (subject),
    `tú` → pronoun, `pero` → conjunction. Everything else kept its type.
  - added: pronouns yo, él, ella, usted, nosotros/as, vosotros/as, ellos, ellas, ustedes; articles el, la, los,
    las, un, una; y; qué, quién, dónde; pan and vivir (start word and an intransitive verb).
  Please check these choices against your own v2 list.

### Assumptions (v2)
- **Duplicate sequences are dropped across directions**, not only within one direction. With horizontal bars a
  vertical neighbor pair that overlaps in two columns is also a diagonal pair; counting it twice would make
  round-1 case 3 (pan under como) score 11 instead of the specified 9.
- **„la casa es rojo → nothing“** is read as „no Satz“: the article + noun phrase „la casa“ still counts as a
  Satzteil, because the NP rule in section 4 applies to it. The test checks that no Satz forms and that es/rojo
  join no structure.
- **Grammar dialog** can be cancelled until the first attempt (no action used); after that the action is used
  either way. Forms the word already shows are disabled (e.g. „grande“ m./f. Sg.), and the person/form choice
  locks after the first attempt.
- **Almost-structures:** a virtual one-cell token is tried in every free cell that touches a planted word
  (8-neighborhood). Verbs are tried in all six persons and adjectives in all four forms, since the player can
  conjugate or inflect after buying. The structure must be a new Satz/Frage with the virtual word first or last.
- **Need-weighting ties** are broken by role order (pronoun, verb, noun, function, rest).
- **Shop guarantees** are filled first (one completing offer, one offer from the largest-deficit role), then the
  remaining slots are drawn by weight; the cards are shuffled afterwards.
- **Scarecrow** looks only at the field; shed verbs keep their form. Gold income is still computed before it.
- **Wheelbarrow code** is kept (0 slots); regrowth skips it and goes to the shed, then the compost.
- **Twine labels:** below horizontal strings, to the right of vertical/diagonal ones. On the small 6×2 field
  labels of crossing structures can overlap; this needs a hands-on look.

## Changes after the first prototype (requested)
- **Exam mode toggle** in the main menu (saved in PlayerPrefs, default on). Off = practice mode: every quiz has a
  „Lösung zeigen“ button that shows/hides the answer; for the joker it shows a different random free word of the
  required type each time it is shown. Practice runs are marked „Übung“ in the leaderboard.
- **Field 1.5× bigger in cells:** start 8×8 (was 5×5), max 15×11 (was 10×7), in `BalanceConfig`.
  The art generator reserves the max area on the cliff; if you change the max, re-run *Regenerate Placeholder Art*.
- **Shed:** a 5×3 storage grid (`shedWidth/shedHeight`). Words there keep their shape and mods but do not score or
  give synergy; they stay owned (excluded from the shop). The wheelbarrow still exists. Esc on a held word now falls back
  wheelbarrow → shed. The layout reserves 5×3 – a bigger shed needs `IsoLayout` adjusted.
- **Hot swap:** placing a held word over exactly one other word (and nothing else blocks it) plants it and picks the
  other word up (yellow preview). Works on the field and in the shed.
- **Shop:** larger cards (2 columns × 3 rows, bigger fonts) with an isometric preview of the word’s full shape.
  The shape family is now rolled when the offer appears (not on purchase), so the preview matches what you get.
- **Isometric art:** all sprites regenerated (iso tiles, cliff/mesa, shaded house, shed, props, clouds). The field
  sits on a cliff top so most of the screen is sky. `IsoLayout.cs` holds all world positions.
- **Only wheat:** every word grows wheat. A small colored badge on the sign (and ■ in the shop) still shows the word
  type, because the synergy ring depends on it.

## Sky board layout (latest UI change, supersedes parts of the list above)
- **Boards in the sky:** the field and the shed are flat, top-down, semi-transparent grids (12 px cells) with 2D crop
  icons (`iconWheat`, `iconLavender`). All interaction, signs, hover tooltips and previews happen only there.
- **Ground field:** the small isometric field on the cliff mirrors the field board automatically (soil + iso crops,
  no signs, no interaction). The shed has no ground counterpart.
- **Shed and compost** sit right of the field board in the sky; the shop drawer covers both when it is open.
- **Shop drawer:** slides in from the right edge; the handle (« Laden / » Laden) is on the drawer's left side and moves
  with it. Auto-hides when a crop is held (also right after buying) and when „Ernten“ is pressed; auto-opens after the
  harvest. Simplified, transparent cards: word cards show only the word, points, gold, the flat shape preview and the
  price button (price kept so you know what you pay). Joker/upgrade effects moved into hover tooltips.
- **Wheelbarrow removed:** `wheelbarrowSlots = 0` (code kept; set it back to 2 and re-add the view to restore it).
  Esc on a held new word goes to the shed; scarecrow regrowth falls back to the shed before composting.
- Positions: `IsoLayout.cs` (`BoardTopLeftPx`, `ShedBoardOriginPx`, `CompostPx`, `FieldOriginPx`).

## Bigger UI (latest)
- **All uGUI +25 %:** the canvas reference resolution is `IsoLayout.CanvasRef` = 768×432 (was 960×540), so every
  element and every text is exactly 1.25× bigger. Lower it further to scale up more.
- **Board cells 20 px** (was 12). The field board is anchored at its top edge under the HUD and grows downwards; at
  the maximum 15×11 it covers ~51 % of the screen. Signs, labels and floating numbers are larger to match.
- **HUD** is a compact two-line strip at the top-left, next to a big „Ernten“ button.
- **Shop drawer** spans the full screen height and 600 canvas units of width (it may cover the right part of the board).
  Word cards are tall; their shape previews use exactly the board's cell size (`IsoLayout.BoardCellCanvas`).
  Because the open drawer covers the HUD's „Ernten“ button, its header repeats „Ernten“ and the current gold.
- Practice-mode answer for the conjugation quiz is shown on one line (forms in field order) so the dialog fits.

## Assumptions (where the design left room)
- **Font:** no pixel font is bundled; LiberationSans SDF (TMP Essentials) is the placeholder. Swap `SpriteSet.font`.
- **UI scale:** the canvas uses reference 960×540 (2× the 480×270 world) so text stays readable.
- **Float config values** are converted to clean decimals (`BalanceConfig.D`) before use, so 0.6 × 15 = 9, not 9.0000004.
- **Offer weights:** common = article mod, conjugation mod, +Mult (random type); rare = ×Mult (from round 10),
  field expansion (only while the grid can still grow), extra action. Weights are split evenly inside each group; when a group
  has fewer entries the remaining ones share its weight. The two upgrade slots never show the identical offer twice.
- **Extra action** also grants +1 action in the round it is bought.
- **Purchase and mod quizzes cannot be cancelled** once opened (otherwise wrong guesses could be reset for free).
  Empty submissions do not use an attempt. The joker dialog can be closed; its attempts persist for the round.
- **Joker:** input matches any vocabulary entry under the answer rules; a wrong type, unknown word or owned word uses an attempt.
- **Article quiz:** both fields are checked without article stripping; accent mode applies.
- **Conjugation mod:** applying it marks the verb as modded even with 0 correct forms (it can be re-applied).
  "Mods learned" counts successful article mods plus *newly* learned forms.
- **Scarecrow penalty** only looks at words on the grid (wheelbarrow words do not score). Gold income is
  computed before the penalty.
- **Moving words:** clicking a placed word picks it up by the clicked cell (grab offset kept, also when rotating).
  Esc returns a moved word to where it was; Esc on a new purchase sends it to the wheelbarrow.
  Clicking the wheelbarrow or compost heap while holding a word stores or discards it.
- **Run log:** `goldBefore` = gold at the start of the round, `goldAfter` = gold after income. A `result` column
  (pass / scarecrow / fail) was added.
- **Starting layout:** comer (line, bottom row), casa (block), grande (corner), dos (line). Fallback words use the same
  slots, or the first free spot.
- **Accent buttons** appear in every quiz (also German-answer quizzes).
- **Vocabulary reload (debug)** reads the JSON file directly in the Editor (Resources caches it otherwise).

## Not yet verified by hand
Logic and flows were exercised in Play mode from code (purchase quiz, wheelbarrow, conjugation mod, harvest,
scarecrow, game over, leaderboard, run log, practice-mode reveal, hot swap, shed). Real mouse drag/rotate on the
iso grid, hover tooltips and the click-to-skip harvest animation still need a hands-on play test.

## Word type "phrase" (spec update)
- `"type": "phrase"` is valid: fixed expressions and short sentences. Scored exactly like "other" (flat points,
  no ring synergy, no shop multipliers, no mods); size/points/gold from the normal complexity formula.
- The joker can require "Redewendung" (all five types are equally likely).
- Phrases grow **lavender** (`SpriteSet.lavender`); all other types grow wheat. Badge color: purple.
- AnswerChecker also strips apostrophes, both `'` and the typographic `’` (keyboards and autocorrect produce either).
- The two sample phrases were added to the test copy of the sample vocabulary (`TestVocabulary.cs`). The live
  `vocabulary.json` (your 150-entry list) already contains `buenos_dias` and `que_tal`, so it was left unchanged.
