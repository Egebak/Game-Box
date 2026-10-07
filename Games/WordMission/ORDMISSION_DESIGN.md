# Ordmission design and content guide

**Educational content and Danish phonics recordings should be reviewed by a Danish-speaking adult before relying on the game for reading instruction.** This starter pack demonstrates the software. It is not a validated reading curriculum and uses no Alkalær material.

## Learning approach

Ordmission connects letter forms, letter names, sound cues, and words as distinct data. Reading makes a toy mechanism respond: a gate opens, a robot advances, or a target pops. There is no timer or failure screen. After a wrong choice the target can be replayed. On the second difficulty, a correct choice is highlighted and a distractor is removed. On the third difficulty, the game demonstrates the answer and moves on. Assistance is saved separately from first-try success.

The first map area introduces letter sounds. Short two-letter words appear as guided previews so a first session can mix activities; they become a regular unlocked area after letter mastery. Stage 3 adds three- and four-letter words. Stage 4 is an empty extension point for reviewed longer words. The initial nine-challenge sequence is configured through `sessionLength` in `Data/starter.json` (8–12 supported).

## Content schema

`Data/starter.json` has four lists:

- `sounds`: stable ID, a **visual test cue**, and optional reviewed recording path. The cue is not authoritative pronunciation.
- `letters`: stable ID, uppercase and lowercase forms, letter name, primary sound ID, optional name recording, and stage.
- `words`: stable ID, written display, ordered letter IDs, ordered sound IDs, stage, difficulty, phonetic flag, review status, visual ID or optional image path, optional whole-word recording, and eligible activities.
- `stages`: ordered stage ID, child-facing title, and the mastered-item threshold needed to unlock it.

The parser rejects malformed JSON. Startup validation identifies duplicate IDs, unknown letter or sound references, missing display fields, mismatched spelling and letter sequence, invalid word length or difficulty, unknown activities, and invalid stages. Optional audio and image files produce warnings if missing. Stable IDs keep old progress valid when content is added.

The five starter words are `word_is` (**IS**), `word_nu` (**NU**), `word_sol` (**SOL**), `word_mus` (**MUS**), and `word_hus` (**HUS**). Every one is marked `needs-review`. In particular, a reviewer should decide whether each word is sufficiently phonetic at its stage, whether the specific sound IDs reflect its Danish pronunciation, whether letter-name text is correct, and whether the clock picture communicates **NU**. Remove or replace any item that does not pass review.

## Add letters and words

1. Ask a Danish-speaking adult to verify spelling, sound relationships, and intended stage.
2. Add a unique `sound_...` entry, if needed, with a visual test cue and a path for a human recording.
3. Add a `letter_...` entry with both cases, name, and primary sound ID. Case changes do not create separate learning records.
4. Add a `word_...` entry with letter IDs and sound IDs in order. `display` must match the assembled letters. Use stage 2 for two letters, stage 3 for three or four, and stage 4 for longer reviewed words. List eligible activity IDs: `build`, `choose`, `gate`, `tank`.
5. Give the word a built-in `visualId` or an `image` path to an added Godot texture. Keep `reviewStatus` as `needs-review` until an adult approves it.
6. Run the plain C# checks and Godot smoke scenes before using the content with a child.

## Recordings

There are **no human-recorded letter names, phonemes, or whole words in this release**. The starter JSON contains replaceable paths under `Audio/Sounds/`, `Audio/LetterNames/`, and `Audio/Words/`. Supply clear recordings from a Danish-speaking adult at those paths, import them in Godot, and confirm the audio warnings disappear. Record phonemes without automatically using spoken letter names. Verify each recording against its sound or word ID. `instructionAudio` can hold a recorded full instruction; `successAudio` currently uses the shared victory sound. Missing recordings are visual-only; OS text-to-speech is never used as phonics authority. Sound can be disabled in **For voksne**.

## Adaptive model and persistence

`WordMissionProgress` lives inside the existing `GameBoxState` JSON save. Each stable learning ID stores attempts, first-try correct answers, assisted answers, wrong attempts, last-seen UTC time, score, and mastery state (`New`, `Learning`, `Practicing`, `Mastered`). A first-try answer adds two points; a correct answer after one wrong adds one; help reduces score. Three first-try successes and six points reach mastery. Difficulty can move a mastered item back to practice.

`SessionGenerator` favors difficult items (weight 12), learning items (6), new items (4, with a three-item target limit), and recently mastered items (0.6). Mastered items last seen at least seven days ago receive more review weight (2.5). A deterministic seed makes this testable. It rotates five activity types and selects easy, visually different distractors for early words. The content file sets session length and new-item limit. New IDs start as unseen without changing existing records.

After a session, three mastered letters unlock stage 2; one mastered two-letter word unlocks stage 3. Stage 4 opens only when longer content has been supplied and its prerequisite is met. The child sees stars and locked map areas; adults can inspect counts and recent difficult IDs in **For voksne**. That screen also changes sound and case display, or resets Ordmission learning without erasing other Game Box progress. Settings survive a learning reset.

## Add an activity and inspect progress

An activity receives a `LearningChallenge` from the generator, rather than choosing its own teaching words. To add one, extend `ActivityKind`, the session pattern, eligible-content validation, and `ActivityView` presentation. Keep word selection in `SessionGenerator` and content in JSON. **F9** in a Godot debug build opens stage, item, activity, mastery, reset, and missing-audio controls. It is not shown as a child-facing button. The save is local at `user://game_box_state.json`; no accounts or cloud service are used.

## Current limits

The first pack contains only eight letters and five candidate words. Stage 4 has no content. The simple pictures are placeholders. The initial phonics cues are on-screen test aids until adult-reviewed recordings are available. The `NU` clock picture and all five words need adult review. No speech recognition or automatic pronunciation judgement is used.
