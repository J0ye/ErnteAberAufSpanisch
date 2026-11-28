# Cosecha – Change Request v2: Sentence Building

Nov 23, 2026 · @Jonathan Müller

## 1. Context, scope and removals

This change request turns Cosecha from a packing game into a sentence-building game: words score more when they form Spanish sentences on the field. It builds on the project state described in NOTES.md. Everything not mentioned here stays as it is: exam and practice mode, sky boards with the iso ground mirror, shed, hot swap, compost, shop drawer, joker, reroll, scarecrow, leaderboard, run log, debug overlay and BalanceConfig. Update NOTES.md when you are done.

**Remove**

- Shape families and rotation: words become horizontal bars (section 2).
- The ring synergy.
- Article mod, conjugation mod, +Mult, ×Mult, field expansion offer, extra action, the two upgrade slots and the action counter. The free grammar action replaces them (section 3).
- The old starting words comer, casa, grande, dos and their layout.

**Milestones** (keep the project runnable after each)

1. Vocabulary schema v2 loader, word model and new stats, with tests (sections 3 and 8).
2. Bars on board and shed, growth schedule, outlines, start layout (section 2).
3. Free grammar action (section 3).
4. SentenceEngine as pure C# with tests (section 4).
5. Scoring, target curve, scarecrow (section 5).
6. Shop (section 6).
7. Visual feedback and tooltips (section 7).

## 2. Board, word bars and growth

- **Words are horizontal bars:** a word of size n occupies n consecutive cells in one row. No rotation; R and right-click do nothing while holding a word. The anchor is the leftmost cell and carries the sign. Shrinking removes the rightmost cell, growing adds one at the right end.
- **Start size:** 6 columns × 2 rows (BalanceConfig).
- **Growth:** at the start of a round the field grows by one row or one column, alternating, with gaps of 3 and 4 rounds. New rows are added at the bottom, new columns at the right. A short banner “Das Feld wächst!” appears, and the ground mirror follows. Config: growthIntervals \[3, 4\], growthOrder \[row, column\], maxColumns 12, maxRows 7.

| Round | 1 | 4 | 8 | 11 | 15 | 18 | 22 | 25 | 29 | 32 | 36 | 39 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Columns × rows | 6×2 | 6×3 | 7×3 | 7×4 | 8×4 | 8×5 | 9×5 | 9×6 | 10×6 | 10×7 | 11×7 | 12×7 |

- **Shed:** stays 5×3; bars up to 5 cells fit.
- **Outlines:** on the sky board, in the shed and in shop previews, every word gets one continuous outline around all its cells, in a darker shade of its type badge color, so neighboring words are easy to tell apart.
- **Ownership:** function words (pronoun, article, conjunction, questionWord) may be owned several times; two “el” on the field are fine. All other words stay unique.
- **Start layout:** yo at column 0, row 0; comer (infinitive, 3 cells) at columns 1–3, row 0; pan (3 cells) in the shed. Intended first round: conjugate comer to “como”, then plant pan at columns 3–5 of row 0 to form “yo como pan”.

## 3. Word stats and the free grammar action

Sizes shrink to fit the small board; function words always take one cell.

```
Function words (pronoun, article, conjunction, questionWord):
  size 1, basePoints 1, gold 1   (fixed, no complexity formula)

All other types: K as before (letters + accents + ñ/ü + irregular verb)
  size       = K <= 4 -> 3 | K 5-8 -> 2 | K >= 9 -> 1
               verbs in the infinitive: +1 (removed by the first conjugation)
  basePoints = round(K^2 / 10)
  gold       = K <= 4 -> 4 | K 5-8 -> 2 | K 9-12 -> 1 | K >= 13 -> 0
```

| Word | Type | K | Size | Base points | Gold |
| --- | --- | --- | --- | --- | --- |
| yo | pronoun | – | 1 | 1 | 1 |
| la | article | – | 1 | 1 | 1 |
| pan | noun | 3 | 3 | 1 | 4 |
| casa | noun | 4 | 3 | 2 | 4 |
| comer | verb | 5 | 3, conjugated 2 | 3 | 2 |
| ser | verb, irregular | 6 | 3, conjugated 2 | 4 | 2 |
| siempre | other | 7 | 2 | 5 | 2 |
| ¿qué tal? | phrase | 8 | 2 | 6 | 2 |
| mañana | other | 9 | 1 | 8 | 1 |
| información | noun | 13 | 1 | 17 | 0 |

**Free grammar action**

- The player gets freeGrammarActionsPerRound = 1 per round. Unused actions do not carry over. The HUD shows how many are left.
- **Conjugate a verb:** click a verb on the field, pick a person (yo, tú, él/ella/usted, nosotros/as, vosotros/as, ellos/ellas/ustedes), type the form, 3 attempts. Correct: the verb shows that form and has that person and number. The first conjugation removes the infinitive cell. Changing the person later costs the action again; the size stays.
- **Inflect an adjective:** click an adjective, pick one of the four forms (m. Sg., f. Sg., m. Pl., f. Pl.), type it, 3 attempts. Correct: the adjective shows that form.
- **Failure** after 3 attempts uses the action, changes nothing and shows the correct form. Practice mode’s “Lösung zeigen” works as in the other quizzes.

## 4. Sentence engine

SentenceEngine is a pure C# class in Core without dependencies. It takes the field and returns a list of structures (category, words in order, direction, bonus). It runs after every change to the field (live) and at harvest. It does not understand language; it matches word features from the vocabulary against a small set of patterns.

**Tokens.** Every word on the field (not in the shed) becomes a token:

- pronoun: person, number, gender ("x" means any gender).
- noun: person 3, number, gender, its own articleDefinite and articleIndefinite.
- article: es, definite, gender, number.
- adjective: the form it currently shows, and which gender and number that form stands for.
- verb: finite only when conjugated, with the person and number of the chosen form, plus transitive and copula. **Infinitives match nothing.**
- questionWord: role. conjunction: only “y” has a rule in this version.
- other and phrase: match nothing.

**Lines.** Read the field in four directions: → left to right, ↓ top to bottom, ↘ and ↗ (both left to right). Walk every line of cells in each direction and build sequences: append a word when the walk enters a cell of a word different from the last one appended; an empty cell or the edge ends the sequence. Drop duplicates (same words in the same order in the same direction), which occur when a vertical or diagonal line crosses the same bars in neighboring columns.

**Constituents**

- NP = \[article\] noun \[adjective\]. The article must equal the noun’s articleDefinite or articleIndefinite (this handles “el agua”). The adjective must show the form for the noun’s gender and number.
- Coordinated NP = NP y NP. It counts as person 3, plural; gender m if any part is m, else f.
- Subject = pronoun, NP or coordinated NP. Object = NP or coordinated NP. Bare nouns without article are allowed in this version.

**Patterns** (bonuses in BalanceConfig)

| Category | Bonus | Pattern | Example |
| --- | --- | --- | --- |
| Frage | +2 | questionWord(object) V(transitive) \[Subject\] | qué comes · qué come el perro |
| Frage | +2 | questionWord(subject) V(3rd person singular) \[Object\] | quién come pan |
| Frage | +2 | questionWord(adverb) V \[Subject\] | dónde vives · cómo es la casa |
| Satz | +1.5 | Subject V(transitive) Object | yo como pan |
| Satz | +1.5 | Subject V(neither transitive nor copula) | yo vivo |
| Satz | +1.5 | Subject V(copula) Adjective | la casa es grande |
| Satzteil | +0.5 | Subject V(transitive), object missing | yo como |
| Satzteil | +0.5 | V(transitive) Object, subject missing | como pan |
| Satzteil | +0.5 | NP of 2 or more words, or coordinated NP | la casa · el perro y el gato |

**Agreement** in every pattern: the subject’s person and number equal the verb’s. After a copula, the adjective must show the form for the subject’s gender and number; a subject with gender "x" accepts the masculine or feminine form of the right number. Optional parts in \[ \] must agree when present.

**Selection.** In each sequence, find all contiguous matches and choose a non-overlapping set greedily: Frage before Satz before Satzteil, then longer before shorter, then leftmost. Every chosen structure gives its bonus to every word in it, including articles and adjectives inside NPs. A word gets at most one bonus per sequence; bonuses from different sequences add up, so a word at a crossing collects several.

Keep the patterns as data (a ScriptableObject or JSON list of role sequences with category) so new sentence types can be added without code; the agreement checks live in code.

## 5. Scoring, target curve and scarecrow

Words score their base points times one plus all sentence bonuses they collect; the target grows by 15% per round.

```
wordScore   = basePoints * (1 + sum of bonuses of all structures the word is part of)
              (other and phrase words never join structures, so they score basePoints)
roundScore  = sum of wordScore over all words on the field
Target(r)   = 10 * 1.15^(r - 1)
Gold income = 3 + sum of gold of all words on the field
Bonuses     = Satzteil 0.5, Satz 1.5, Frage 2.0
```

**Start state:** 6×2 field with yo and comer, pan in the shed, 5 gold, 1 free grammar action, scarecrow available.

**Unit tests for round 1** (target 10):

| Situation | Score |
| --- | --- |
| Start layout, no action | yo 1 + comer 3 = 4 |
| comer conjugated to “como”, pan still in the shed | “yo como” Satzteil: 1.5 + 4.5 = 6 |
| pan at columns 1–3 of row 1, under “como” | “yo como” → Satzteil and “como / pan” ↓ Satzteil: 1.5 + 6 + 1.5 = 9, fails |
| pan at columns 3–5 of row 0 | “yo como pan” Satz: (1 + 3 + 1) × 2.5 = 12.5, passes |

Gold income after that round: 3 + 1 + 2 + 4 = 10. Further target values: round 10 ≈ 35.2, round 20 ≈ 142.3.

**Scarecrow penalty:** the highest-scoring conjugated verb on the field falls back to the infinitive and grows by one cell at its right end. If that cell is taken or outside the board, the word moves to the shed; if the shed has no room, it is composted. If no verb is conjugated, the highest-scoring inflected adjective falls back to its masculine singular form. Otherwise no penalty.

## 6. Shop

The shop sells only words and steers the offers toward what the field is missing.

- **Layout:** the existing 2×3 card grid holds 5 word offers and 1 joker. Reroll unchanged.
- **Eligible words:** vocabulary entries not owned (field and shed). Function words are always eligible, even when owned. The complexity gate (K ≤ 6 + floor(r / 2)) applies to content words and phrases only.
- **Prices** unchanged: 2 + 3 × gold + basePoints, so function words cost 6.
- **Joker:** the required type is random among noun, verb, adjective and phrase.

**Need weighting**

1. Count the owned words per role: pronoun, verb, noun, function (article, conjunction, questionWord), rest (adjective, other, phrase).
2. Compare with the target ratio from BalanceConfig, default pronoun 1 : verb 1 : noun 1.5 : function 1 : rest 0.75. deficit = target share − owned share.
3. Candidate weight: 1, ×3 if its role has the largest deficit, ×2 for the second largest.
4. At least one of the 5 word offers comes from the role with the largest deficit.

**Almost-structures**

- An almost-structure is a sequence on the field that would become a Satz or Frage with exactly one more word at its start or end, where that neighboring cell is free and inside the board. Example: “yo como” with a free cell to its right misses an object.
- Candidates that fill the missing role, and agree where agreement applies, get an extra ×3.
- If an almost-structure exists and a fitting candidate exists, at least one offer completes it.
- Find them by running SentenceEngine with a virtual token in the free cell.

**Purchase quiz changes**

- Articles are bought without a quiz; the sentence rules practice them.
- Pronouns are always asked Spanish → German, because German “sie”, “Sie”, “wir” and “ihr” each fit several Spanish pronouns.

## 7. Visual feedback and tooltips

The player must see at a glance which words belong together and which sentences count.

- **Outlines:** see section 2.
- **Twine:** for every structure, a string runs through the sign centers of its words in reading order. Color per category: Satzteil light brown, Satz golden, Frage purple. A small label sits at the middle of the string: “Satzteil +0,5”, “Satz! +1,5”, “¿Frage? +2”. When several structures pass through one word, draw their strings side by side with a small offset.
- **Live:** after every change, re-run the engine. A newly formed structure pops in (scale bounce and label fade-in, about 0.4 s); a structure that disappears fades out. The score preview updates as before.
- **Harvest:** the structures light up one after another (Frage, then Satz, then Satzteil), then the word points pop as before. At most about 2.5 s in total, skippable by click.
- **Tooltip on a field word:** the form shown and its lemma (“como – comer, yo”), the German translation, the type with features (“Nomen, f., Sg.”), every structure the word belongs to with its bonus, and the breakdown basePoints × (1 + bonuses) = score.
- **HUD:** free grammar actions left.
- **Debug overlay:** a list of all detected structures with direction and words.

## 8. Vocabulary v2, answer checking and tests

**Schema version 2.** The live vocabulary.json will be replaced by a version 2 file. The loader accepts only version 2; on version 1 it logs an error naming the file. Summary of the schema:

| type | Feature object |
| --- | --- |
| noun | noun: gender m/f, number sg/pl, articleDefinite, articleIndefinite |
| verb | verb: irregular, transitive, copula (bools), present: yo, tu, el, nosotros, vosotros, ellos |
| adjective | adjective: forms: msg, fsg, mpl, fpl (msg equals es) |
| pronoun | pronoun: person 1–3, number sg/pl, gender m/f/x |
| article | article: definite (bool), gender m/f, number sg/pl |
| questionWord | questionWord: role subject/object/adverb |
| conjunction, other, phrase | none |

Every entry also has id, es, de and type, optionally tags. Invalid entries are skipped with a warning, as before.

**AnswerChecker**

- Strip a leading article only when another word follows it, and never for entries of type article.
- Everything else unchanged (apostrophes, punctuation, accent modes).

**Run log:** add the columns satzteile, saetze and fragen (structures counted at harvest).

**Tests.** Replace TestVocabulary.cs with version 2 entries covering yo, tú, él, el, la, un, y, qué, quién, dónde, comer, vivir, ser, pan, casa, perro, gato, agua, grande, rojo, siempre and buenos días. Remove the tests for shape families, rotation, ring synergy, mods and multipliers. Add:

- stats table from section 3 and the growth schedule from section 2
- bar shrinking and growing at the right end, scarecrow fallback to shed and compost
- the four round-1 cases from section 5
- engine cases:
  - “yo como pan” → Satz; “yo comer pan” (infinitive) → nothing; “tú como” → nothing (agreement)
  - “el perro come pan” → Satz; “la perro” → nothing; “el agua” → Satzteil
  - “la casa es grande” → Satz; “la casa es rojo” → nothing, after inflecting to “roja” → Satz
  - “yo vivo” → Satz; “qué comes”, “quién come pan”, “dónde vives” → Frage
  - “el perro y el gato comen pan” → Satz with a coordinated subject
  - yo / como / pan stacked vertically in three rows → Satz
  - a word in a → Satz and a ↓ Satzteil scores basePoints × 3
  - an empty cell between “yo” and “como” → nothing
- shop: a field with verbs but no nouns gets at least one noun offer; “yo como” with a free cell to the right gets at least one noun offer
- articles skip the purchase quiz; pronoun quizzes are always Spanish → German
