# Investigating an issue

The brief each investigator is spawned with, and the go-ahead it's sent for each change. Fill in the parts in angle brackets

## The brief

Spawn a general-purpose subagent with this prompt, one per issue:

````
You're investigating one suspected bug found in an unattended test run of the Viking Villages mod, in this repo. Read its CLAUDE.md first - its rules apply to everything you change. Other investigators are looking into other issues from the same run at the same time, so stay on this one

## The issue

<the issue's whole section from the triage file, with the user's Notes>

The run's file is <full path to the log>, <N> lines - too big to read whole, so grep it with -n and read around the lines that matter. Each line is stamped with the real time and the game's day and time, and lines without a stamp continue the one above. <full path to the overview file> summarizes the whole run - its kinds of line, who and where they were about, its snapshots and its totals

## First, investigate - don't change any file yet

- Read the evidence in context: what the vikings were doing before and after, other lines about the same viking or place, and the snapshots around then (`Snapshot of <name> at` lines hold a viking's job, door, bed, food and how it's moving; `Snapshot of the village at` lines hold the village's pieces, raids and loot lying around)
- Find the code that wrote each line - grep VikingVillages\ for its start, like "Stuck: " - and follow the logic back to what made the vikings do what the log shows. The game's own code is decompiled one class per file in external\decompiled\ (e.g. external\decompiled\assembly_valheim\Humanoid.cs), which git ignores, so Grep and Glob skip it unless given the folder as their path
- Settle on a verdict:
  - bug - you can point at the code that's wrong (file:line) and show how it produces the lines in the log
  - needs logging - the log can't tell the possible causes apart; name them, and the logging that would show which it is
  - not a bug - the code does what it's meant to; say why the log looks the way it does
- Report other bugs you come across separately, each with its own evidence, rather than folding them into this one

Then stop and report in this layout, without changing anything:

Verdict: bug | needs logging | not a bug
Cause: what's wrong and where (file:line), or why it's fine
Evidence: the log lines and code that show it
Plan: the change, file by file - for needs logging, what's logged, where, and what it will tell apart
Tests: the unit tests to add or change, or why none fit (engine glue, Harmony patches)
README: what VikingVillages\README.md says that the change alters, or nothing
Other bugs: each with its evidence and a one-line plan, or none

You'll be told when to make each change, one at a time
````

## The go-ahead

Continue the investigator with SendMessage, once for each change - its main fix first, then each other bug it found:

````
Go ahead with <your plan for issue N | the other bug you found: <its one line>>, and only that. Re-read each file before editing it, since other fixes may have changed it since you looked

- Follow CLAUDE.md: the Edit tool for every change, its code style and docblocks, unit tests where they make sense, and VikingVillages\README.md when what the vikings do changes
- New logging follows the logging around it: gated the same way (usually `if (TestLog.On || Player.m_debugMode)`), a line starting with its kind like "Freed: ", the viking named with TestLog.Name, written when the thing happens rather than every frame, and counted with TestLog.Count when a total helps
- Build with `dotnet build VikingVillages/VikingVillages.csproj -c Release` and run `dotnet test --project Tests/VikingVillages.Tests/VikingVillages.Tests.csproj -c Release`. If the session guard denies either, don't retry or work around it - say so
- Don't stage or commit anything

Report the files you changed, what each change does, whether the build and tests passed, and a commit message: one line, about 50-70 characters, saying what changed for the user or the code, without the cause or file names
````
