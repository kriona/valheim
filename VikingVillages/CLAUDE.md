# Viking Villages

## Docs
- `README.md` is a short tour of each feature for people deciding whether to install it, each linking to its section of `LOGIC.md`, which holds every detail - its Viking AI section has what each biome changes
- Each job, in the order a Viking picks them and with its numbers, is in `JOBS.md`, and the quests are in `QUESTS.md`
- Every rule that changes with a Viking's or village's biome - what they start with, their odds and numbers, what the village is built and stocked with, and which quests it goes on - is in `PROGRESSION.md`, both for each biome and in a table per feature, so a new or changed biome rule goes there too, in both places
- Every line a Viking says, and when, is in `DIALOG.md`, so a new or changed line goes there too - and a line never has Vikings rowing or at the oars, since every boat in the game sails. Each row says who says it - PC, NPC or Dvergr Vikings - and a line NPC Vikings never say, about fighting or adventuring, goes in `NPCSpeech.PCOnlyLines` too
- A change to what the Vikings do goes in `LOGIC.md` or `JOBS.md`, and in the README only when it adds a feature or changes one the README mentions

## Code
- The code sits in folders by feature - `Generation`, `Village`, `Vikings`, `AI`, `Life` (the `VillageLife` partial class, a file for each part of a viking's life and `Life\Jobs` with a file for each job), `Quests`, `Travel`, `Speech`, `News` and the rest - with only `Plugin.cs`, which `Directory.Build.props` reads the version from, left beside the docs. A `*Patches.cs` file goes in its feature's folder
- Village houses are built from the `.blueprint` files in `Blueprints\` - the user makes and changes them in game with PlanBuild, as `Blueprints\README.md` describes, so don't write or edit `.blueprint` files by hand

## Vikings
- The bodies follow what the kinds share: `LivingVikingBody` for PC, NPC and Dvergr Vikings (names, powers, moving and falling like a player), `PlayerLikeVikingBody` under it for PC and NPC Vikings (village life, food, rest, carrying, gear changes), and `PCVikingBody`, `NPCVikingBody` and `DvergrVikingBody` for each kind. A job or rule that differs for NPC Vikings checks `VillageLife.Peaceful`, and a new village chore NPC Vikings should do first goes through `VillageLife.NPCFirst`
- A viking builds the way a player does: building, repairing, rebuilding or taking down anything with a hammer - a piece, a fire, a boat - is one swing, never several, with the result appearing once the swing lands (see `HammerWork` and `VillageLife.BuildSwings`). Only work that's damage, like chopping a tree or mining, takes a swing per hit
