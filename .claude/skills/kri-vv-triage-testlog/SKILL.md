---
name: kri-vv-triage-testlog
description: Finds suspected bugs in a Viking Villages test run (BepInEx\VikingVillagesTestLog) and lists them with evidence in scratchpad\ for the user to prune. Then `/kri-vv-triage-testlog fix` (or `fix commit`, a commit per fix) has a subagent per issue fix it, add logging or rule it out. Use for triaging or finding bugs in a test log or last night's run, or fixing a triage file's issues
---

# Triage a test run

The user leaves the game running overnight with `kri_vv_testlog start` and wants the run's file turned into a list of suspected bugs, then the ones they keep looked into and fixed. That's two parts, run on separate requests:

- **Triage** writes the list and stops, so the user can prune it - for a request to look over, triage or parse a run, or this skill with a run's name or nothing
- **Fix** works through what's left of the list - for a request to fix the issues or work through the list, or this skill with `fix` (and `commit` when they want commits)

## The run's files

- Runs are saved in `C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\VikingVillagesTestLog\`, named after the biome of the village, numbered after the first in each biome (`Swamp.log`, `BlackForest-2.log`). Use the one the user names, or the newest
- `VikingVillages\Debug\TestLog.cs` says what a run writes. Each line is `[Level] [real time day N game time] Kind: ...`, and lines without a stamp continue the one above (a stuck viking's trace, an exception's stack). Warning lines are the code's own judgment that something went wrong, and Info lines are what happened
- Every kind of line is written by code you can find, by grepping `VikingVillages\` for its start (like `"Stuck: "`) - that's where to learn when it's written and what its fields mean. The logging grows all the time, so trust the code over this skill when they disagree
- A snapshot every 15 minutes is followed by the totals, which count from the start of the run - so the last block covers all of it
- Some lines are easy to misread. As the code stands, a `Stuck:` line is written once for each job, so a viking whose job keeps starting over writes one each time - 137 of them can be one viking in one spot. A `Hungry:` warning means none of the chests that viking can use holds food it can eat, not that the village has none. And vikings eat only when wounded while the chests hold fewer than three foods for each of them (`SpareMeals` in `VillageLife.cs`), so few meals eaten isn't hunger on its own

## Triage

What triage writes goes in the repo's `scratchpad\` folder, which git ignores - not the session's own scratchpad directory - so the user can open it and a later session can pick it up

1. Get the overview, saving it to the scratchpad folder: `python -P .claude/skills/kri-vv-triage-testlog/scripts/overview.py [name or path] > scratchpad/overview-<run>.txt`. It's more than a command's output shows at once, so Read the file - all of it, as it's built to be read whole. It has how the run started and ended, the build of the mod it ran, each kind of line with its count and its first and last lines, the exceptions, who and where each kind is most about with what they were doing, the jobs and the reasons the lines give, each viking's lives, a row per snapshot with every hitch's game time, the villages and the totals at points through the run, the longest stretches with nothing written, and the last totals
2. Read `references/known-patterns.md`, the patterns already looked into that aren't bugs. Leave them out unless the run shows what the entry says would make one a bug after all
3. See what may be fixed already, in the overview's Build section - the commits to the mod since its DLL was written, which the run may not have had. A commit that adds or rewords a log line settles it: when the run has the new line, the build had the commit. Name a commit that looks like it fixes an issue in that issue, rather than leaving the issue out, since only the next run can tell
4. Look for what's off. These are the signals worth chasing, roughly from what hurts most:
   - Exceptions from this repo's code - a `VikingVillages.` or `Kriona.` frame anywhere in the stack. Ones from other mods or the game go under "Outside this repo"
   - Deaths that shouldn't happen - falls, drowning, vikings or players, poison or burning in the village, deaths with no raid on, the village emptying (vikings loaded falling to 0 in the snapshots and staying there)
   - Vikings trapped or getting nowhere - `Stuck`, `Twitching`, `Hop`, `Climb` and `Loot` lines piling up for one viking or one place, the same viking in the same spot snapshot after snapshot, `Under` the ground
   - Vikings going without - `Hungry` for long or while the chests hold food, no bed, jobs that start and end at once (`Churn`), jobs mostly stopped before they finish (set `Jobs started` against `Jobs stopped before finishing` and `Jobs finished`, job by job)
   - Things lost or never done - loot despawning and why it was left, things given up on that lie on open ground, `Couldn't reach, by thing` naming village pieces like chests or stations, work that never happens
   - The village's setup at the start - warnings like `Found no room for`, upgrades left missing
   - How the game ran - fps sliding, objects, ZDOs or items lying climbing through the run, hitches outside the known pattern, quiet stretches that shouldn't be there
   - Numbers that look wrong for the run's length or biome, totals that stop growing or climb too fast (the overview's totals through the run), and totals worse than an earlier run's - of the same biome when the folder has one, or else any, minding what the biome changes
5. Check each candidate before filing it: grep for its lines with `-n` and some context, read a few in full, and pick the ones that show it best. A look at the code that writes a line is fine when it decides whether something is off at all, but leave finding the cause to the fix part - triage stays high level and quick. Merge candidates that are one problem seen several ways (a viking stuck by their bed and hungry there is one issue). When the vikings do what the README says they should but it turns out badly - a rule that lets the village die out over and over - file it anyway, quoting the rule and saying it may be by design, so the user decides
6. Write the triage file and tell the user, as below

### The triage file

Write it to `scratchpad\`, named `triage-<run>-<yyyyMMdd-HHmm>.md` (like `triage-Swamp-20261125-1915.md`), always as a new file. Lay it out like this:

````markdown
# Triage of Swamp.log

- **Run:** C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\VikingVillagesTestLog\Swamp.log
- **Village:** Karlsby at (5238, 31, 1639), in the Swamp
- **Ran:** 2026-11-25 07:16 to 18:30, 674 minutes, game day 37 08:04 to day 59 15:48, ended by the command
- **Mod built:** 2026-11-25 07:14 - commits since: 909c685 Make vikings tire and drown while swimming, ...

To leave an issue out, delete its section or set its Status to skip. A Notes line under the Status steers its investigation. Then run `/kri-vv-triage-testlog fix` to fix the rest, or `/kri-vv-triage-testlog fix commit` to commit each fix too

## 1. Two vikings stuck for hours beside the east house

Status: open

**Suspected issue:** ...

**Evidence:**
- `Swamp.log:1130` `Stuck: Aldis the Far-Travelled at (5277, 34, 1647) of the village at (5238, 31, 1639) has stood still for 180s at its job: Rest on bed, on the way, ...`
- 274 of the 322 `Stuck:` warnings are around (5280, 1640), by Aldis the Far-Travelled (141) and Thjodhild (137) - the overview's who and where
- Totals: `Stuck, by job: 322 - Loot 179, Eat 137, Rest 4, Repair 1, Torch 1`

**Why it looks off:** ...

**Where to look:** ...

## Outside this repo

- ...

## Looked at, seems fine

- ...
````

- Rank the issues most harmful first, numbered from 1, and keep each to what one investigation can settle - split unrelated problems, merge one problem's symptoms
- Evidence is what the user and the investigator check the claim against. Quote the lines that show it, trimmed to what matters, each with its line number like `Swamp.log:530`. Give counts with where they came from (the overview, a grep, a totals line), and name the vikings and places. Three to six quotes is usually plenty - for a long string of similar lines, quote the first and the last and give the count
- **Why it looks off** says what you'd expect to see instead, which is what the investigation tests
- **Where to look** is optional - the job or code the lines come from, or a commit since the build that may fix it
- **Looked at, seems fine** lists the signals you checked and why each is fine, a line each, so the user sees what was covered and can move one up to an issue

### Tell the user

Give the triage file's full path, each issue's title in a line, most harmful first, and anything outside this repo. Then ask them to look it over - deleting an issue's section or setting its Status to skip leaves it out, and a Notes line steers its investigation. When they're ready, `/kri-vv-triage-testlog fix` fixes the rest, and `/kri-vv-triage-testlog fix commit` commits each fix too

## Fix

### Before starting

- Find the triage file - the one the user names, the one written earlier in this conversation, or the newest `triage-*.md` in `scratchpad\`. With none, ask for its path
- Read it again, as the user may have changed it. Work on the issues whose Status is open or missing, leave out those set to skip, and pass each one's Notes on to its investigator
- Find the run's overview beside it, or make it again as in the Triage part's first step, for the investigators to start from
- Commit only when the user asked for commits in their own request - `fix commit`, or asking in words. CLAUDE.md says never to commit unless asked, and this skill's steps aren't the user asking. Without that, every change stays in the working tree
- Run `git status --porcelain` and note the files that already have changes. A fix that touches one of them can't be committed on its own, so it's left in the tree and reported. Other files' staged changes don't matter, since each commit names its files

### Investigate side by side

Spawn a general-purpose subagent per issue, all in one message (up to 6 at once, starting more as those finish), each with the brief in `references/investigate.md`. Investigating only reads, so they can run side by side. The changes come after, one at a time, so no build trips over another fix's half-made edits and each fix lands in a commit of its own

### Change one thing at a time

Go through the issues in order. For each investigator whose verdict is bug or needs logging:

1. Send it the go-ahead from `references/investigate.md` with SendMessage (fetch it with ToolSearch `select:SendMessage` when it isn't loaded). Each go-ahead is one change: its fix for the issue first, then each other bug it found, each after the one before is committed
2. Check what it changed with `git status --porcelain` and `git diff` on the files it names. All of it should be this change, following CLAUDE.md - style, docblocks, LOGIC.md and JOBS.md when what the vikings do changes, tests where they make sense. Send it back with what's wrong when it isn't
3. If the session guard denied its build or tests, or they failed and it couldn't fix them, stop - don't commit it, and tell the user where things stand, since the next fix's commit would sweep it in when they share a file
4. When commits were asked for, commit it: `git add` its new files, then `git commit -m "<message>" -- <every file it touched>`. The message is one line, about 50-70 characters, saying what changed for the user or the code, without the cause or file names, and with no Co-Authored-By trailer - the user wants only their name on commits
5. Set the issue's Status in the triage file: `fixed (<hash>)`, `logging added (<hash>)`, `fixed, not committed` or `logging added, not committed`

For a verdict of not a bug, send no go-ahead and set its Status to `not a bug`. When a later run is likely to show the same thing again, add it to `references/known-patterns.md` - how to recognize it, why it's fine, and when it would be an issue after all - and when commits were asked for, commit that last, on its own

### Report

For each issue, a line or two: the verdict, what was changed or why it's fine, and the commit's hash and message - or, for changes left in the tree, the files and a suggested message, noting fixes that share a file. Then the other bugs found along the way and what became of them, what was added to the known patterns, and anything left undone. Living vikings are "they" in the report
