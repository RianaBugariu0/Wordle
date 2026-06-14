# AI Usage Disclosure

## Tools used

| Tool | Version / model | How I used it |
|------|-----------------|---------------|
| **Cursor** | IDE with built-in agent | Agentic coding: asked it to build/fix the Wordle game step by step in chat |
| **Claude** | Sonnet (via Cursor) | Chat-based suggestions for SDL setup, bug fixes, UI layout, and this disclosure file |
| **GitHub** | word list source | Downloaded `Words.txt` from a public Wordle word-list repo (not AI-generated) |

I did **not** use GitHub Copilot autocomplete on this project. Most typing was either my own edits or pasted suggestions from the Cursor chat.

## Summary

Roughly **40% of the codebase is mostly AI-generated** (the harder / more polished parts). The other **~60% I wrote or rewrote myself** in a simpler style — game loop, drawing helpers, stats, tests, and basic game logic.

You can usually tell the difference:
- **AI parts** use `readonly struct`, `Math.Clamp`, pattern-style switches, big dictionary tables, `INativeContext`, and proportional layout math.
- **My parts** use longer method names, `if/else` chains, `string` concatenation, `Substring`, explicit `for` loops, and fields without `readonly`.

## Files that are fully AI-generated

| File | Notes |
|------|-------|
| `SimpleFont.cs` | Entire file — bitmap font glyph table and draw helpers |
| `WordleSdlContext.cs` | Entire file — native SDL library loading, `INativeContext`, `IDisposable` |

## Files that are partly AI-generated

| File | AI-generated regions | My regions |
|------|---------------------|------------|
| `Program.cs` | `Layout` struct and `CreateLayout()` (bottom of file) | `Main`, `RunGameLoop`, all `Draw*` methods, console fallback, stats update |
| `WordleGame.cs` | `EvaluateGuess()`, `GetKeyboardState()`, `MergeKeyboardColor()` | `ScoreStore`, `WordleStats`, constructor, `SubmitGuess`, `LoadWords`, `PickAnswer`, enums/record at top |
| `Wordle.Tests/WordleGameTests.cs` | None (written by me, simple xUnit tests) | Entire file |

## Files not AI-generated

| File | Notes |
|------|-------|
| `Words.txt` | Downloaded word list from GitHub (tabatkins/wordle-list) |
| `Wordle.csproj` | Project skeleton + NuGet packages I added |
| `Wordle.sln` | Solution file |
| `.gitignore` | From skeleton |

## What I did myself (no AI)

- Tested the game on my Mac (`dotnet run`, `brew install sdl2`)
- Fixed the games counter bug (was counting guesses instead of finished games)
- Chose the pink UI theme and keyboard layout idea
- Wrote the xUnit tests
- Rewrote ~60% of the code to match my own level after using AI for the hard parts

## Assignment features checklist (where they live)

| Requirement | Location |
|-------------|----------|
| Game loop (input → update → render) | `Program.RunGameLoop` + `DrawEverything` |
| User input | `Program.RunGameLoop` keyboard handling |
| Win/lose | `WordleGame.SubmitGuess`, `IsOver`, `Won` |
| State beyond one variable | `WordleGame` board (`guesses`, `wordList`, `answer`) |
| Persisted score | `ScoreStore` → `stats.json` |
| LINQ | `WordleGame` constructor (`ToHashSet`) |
| Generics | `HashSet<string>`, `List<string>`, `Dictionary<char, TileColor>` |
| Interfaces | `WordleSdlContext : INativeContext` |
| Pattern matching | `Program.RunGameLoop` scancode range check |
| IDisposable | `WordleSdlContext` |
| Custom exception | `WordleInputException` |
| Records | `WordleGuessResult` |
