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
  `AnswerChecker`, `GridModel`/`Shapes`, `RunState`, `VocabularyLoader`, persistence, `UiText` (all German strings).
- `Assets/Scripts/View` – `GameController` (input + flow + quizzes), `FieldView`/`SkyView` (world sprites),
  `UiViews` (HUD, shop, dialogs, tooltip, menus, debug). The UI is built from code at runtime.
- `Assets/Scripts/Editor` – placeholder pixel-art generator and project setup.
- `Assets/Tests/EditMode` – 49 tests (everything in section 13 plus regrowth, synergy, validation, tooltip,
  shed, hot swap). Tests use their own copy of the sample vocabulary (`TestVocabulary.cs`), so the live word list
  can change freely.

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
