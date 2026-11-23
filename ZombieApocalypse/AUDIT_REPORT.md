# Audit Report

Static audit of every plugin in this repository (ChestPeek, CraftMore, CreatureMarker, LightColor, QuickFill, SafeSneak, SkillProgress, SnapPoints, SwapGear, TooLow, TweakStats, WeightDisplay, WhosOnline, ZombieApocalypse and `Shared`), including the uncommitted ZombieApocalypse village work on the `zombie-apocalypse` branch. Every finding was checked against the decompiled game code in `external\decompiled\`. The game can't be run here, so nothing in this report was observed at runtime.

Method:
1. Four discovery agents (Harmony compatibility, performance, bugs and multiplayer sync, memory and lifecycle) produced 48 raw findings, which merged into 45 candidates.
2. Two verifier agents each took half the list and tried to disprove every item by tracing it through the mod code and the vanilla code.
3. Only items marked CONFIRMED or NEEDS RUNTIME TEST are listed below. Rejected items are in the log at the end.

Line numbers are those of the working tree at the time of the audit.

The four CreatureMarker findings (C10, C21, C24 and C36) are in `CreatureMarker\AUDIT_REPORT.md` and aren't counted here.

## 1. Executive Summary

29 issues survived verification: 26 confirmed and 3 that need a runtime test. 12 candidates were disproved.

| Severity | Confirmed | Needs runtime test | Total |
|---|---|---|---|
| Critical | 0 | 0 | 0 |
| High | 1 | 0 | 1 |
| Medium | 6 | 0 | 6 |
| Low | 19 | 3 | 22 |
| **Total** | **26** | **3** | **29** |

| Area | Issues |
|---|---|
| Bugs, gameplay and lifecycle (sections 2-3) | C01, C03, C04, C05, C07, C11, C18, C25, C26, C28, C30 |
| Performance (section 4) | C31, C32, C33, C35, C43, C44, C45 |
| Multiplayer / ZDO sync (section 5) | C02, C06, C08, C12, C13, C14, C15, C16, C17, C19, C27 (C01 is also multiplayer) |

**Overall health: good.**
- The smaller plugins (ChestPeek, CraftMore, QuickFill, SafeSneak, SkillProgress, SnapPoints, SwapGear, TooLow, TweakStats, WeightDisplay, WhosOnline and `Shared`) only have a handful of Low items between them.
- **Harmony:** every Harmony target, overload, injected parameter name and `__result` type matches the decompiled code. All three transpilers (CraftMore, SafeSneak, NavmeshRoofs) match the real IL and fail safe on a mismatch.
- **Lifecycle:** prefab registration, RPC registration, config watchers and runtime textures and materials are handled correctly across logouts.

Almost every issue is in ZombieApocalypse's uncommitted living-village code, and most come from code that runs on whichever client owns the viking or village rather than on the server.

**Most worth fixing before release:**
- **C01:** villages on dedicated servers never get their outposts or neighbor paths, which shows up on every multiplayer server.
- **C04:** a hurt viking can freeze in place.
- **C03:** rebuilt village pieces give unlimited Queen Bees and copper.
- **C02:** viking chest use can duplicate or lose items.
- **C07:** forsaken powers break after switching worlds.
- **C31:** job choice causes frame hitches near busy villages.

## 2. Critical & High Priority Bugs

No Critical issues were found.

### C01 - Client-owned village generators never get their outposts or links to neighboring farms (High, multiplayer)

**Files:**
- `ZombieApocalypse/VikingSpawns.cs:461-483` (FindOutposts) and `494-529` (FindNearbyFarms)
- `ZombieApocalypse/VillagePaths.cs:333-366` (Update, owner gate at 345) and `512-532` (TryLay)
- `ZombieApocalypse/VillageRespawn.cs:175-177` and `558-570` (RecordAllHouses)
- `ZombieApocalypse/Village.cs:255-265` (RecordHouses) and `954-957` (NeedsAllHouses)

**What goes wrong:**
- `FindOutposts` and `FindNearbyFarms` read `ZoneSystem.instance.m_locationInstances`, which vanilla fills only on the server.
- The village generator's ZDO is usually owned by a client. On a dedicated server it always is.
- On that client both helpers loop over an empty dictionary and still return `true`.
- `RecordHouses` then writes `AllHousesKey` with only the farmhouses. The value is saved in the ZDO, so `NeedsAllHouses()` is false from then on.

**Effect on dedicated servers and on client-owned villages on a listen server:**
- The empty houses around the farm never join the village, so they get no vikings, furniture, paths or piece records.
- This includes the core houses `VillageFarmhouses.EnsureHouses` counted at generation (`VillageFarmhouses.cs:165-171`), so a village can end up below `MinHouses`.
- No paths are laid to neighboring villages.

Single player and host-owned generators work.

**Vanilla context:**
- `ZoneSystem.Load` / `LoadOld` (`ZoneSystem.cs:1021-1056`, `1108-1128`) are only called from `ZNet.LoadWorld` (`ZNet.cs:1949`, `1979`), which runs on the server.
- `RegisterLocation` (`ZoneSystem.cs:2075`) is on the server's generation path.
- `SpawnLocationMidGame` returns early on clients (`ZoneSystem.cs:2322`): `if (!ZNet.instance.IsServer())`.
- Clients only receive `m_locationIcons` (`RPC_LocationIcons`, `ZoneSystem.cs:839-848`).
- `ZDOMan.ReleaseNearbyZDOS` (`ZDOMan.cs:974-977`) hands the generator ZDO to a nearby peer.

**Fix:** Have the server answer for the client.
1. Never call `RecordHouses` on a non-server without the server's answer. This alone stops the wrong record from being locked in:

```csharp
if (!ZNet.instance.IsServer() && !ServerHouses.TryGetValue(zdo.m_uid, out HouseReply reply))
{
	RequestHouses(zdo.m_uid, farm);
	return;
}
```

2. Add a routed RPC, registered in the existing `ZNetScene.Awake` postfix with the other routed RPCs. The server runs the two helpers and replies with the results:

```csharp
/// <summary>
/// On the server, answers a client asking for a village's outposts and neighboring farms
/// </summary>
/// <param name="sender">The client</param>
/// <param name="generator">The village's generator</param>
/// <param name="farm">Where the generator stands</param>
private static void RPC_Houses(long sender, ZDOID generator, Vector3 farm)
{
	if (!ZNet.instance.IsServer())
	{
		return;
	}
	List<VillagePaths.House> outposts = new List<VillagePaths.House>();
	bool placed = VikingSpawns.FindOutposts(farm, outposts);
	placed = (VikingSpawns.FindNearbyFarms(farm, GeneratorReach, out Vector3 self, out List<Vector3> farms) && placed);
	ZPackage pkg = new ZPackage();
	pkg.Write(placed);
	pkg.Write(self);
	pkg.Write(outposts.Count);
	foreach (VillagePaths.House house in outposts)
	{
		pkg.Write(house.Center);
		pkg.Write(house.HalfSize);
	}
	pkg.Write(farms.Count);
	foreach (Vector3 spot in farms)
	{
		pkg.Write(spot);
	}
	ZRoutedRpc.instance.InvokeRoutedRPC(sender, HousesReplyRpc, generator, pkg);
}
```

3. The client caches the reply by ZDOID. `TryLay` and `RecordAllHouses` record only once a reply with `placed == true` has arrived.

The names in these snippets follow the existing helpers, but they are a sketch, not drop-in code.

## 3. Medium & Low Priority Bugs

These are logic, gameplay and lifecycle bugs. Multiplayer-specific ones are in section 5.

### C03 - Rebuilt village pieces drop their real recipe when destroyed (Medium, exploit)

**Files:**
- `ZombieApocalypse/VillageRecords.cs:79-112` (TrySnapshot), `220-243` (Rebuild) and `323-349` (WoodCost)
- `ZombieApocalypse/VillageLife.cs:5611-5625` (RebuildJob)
- `ZombieApocalypse/Village.cs:631-657` (RebuildFire)
- `ZombieApocalypse/VikingSpawns.cs:754-781` (PlaceHive)
- `ZombieApocalypse/VillageFurnishing.cs:1114`

**What goes wrong:**
- Vikings rebuild any recorded piece for wood equal to the sum of its recipe amounts, and they gather that wood for free.
- The rebuilt piece is an ordinary non-player piece, so destroying it drops its real recipe at a third, with a minimum of 1 of each item.
- The farm beehive drops a Queen Bee every time, and the village forge drops copper, coal and stone.
- Destroy, wait for the rebuild, and repeat for an unlimited supply.

**Vanilla:**
- `Piece.DropResources` (`Piece.cs:339-368`): `if (!IsPlacedByPlayer()) { dropCount = Mathf.Max(1, dropCount / 3); }`.
- It is called from `WearNTear.Destroy` (`WearNTear.cs:1304`), and no patch in the repo touches it.

**Fix:** Mark the pieces the mod builds and skip their drops.

```csharp
// VillageRecords
public static readonly int RebuiltKey = "kriona.ZombieApocalypse.rebuilt".GetStableHashCode();

// In Rebuild, RebuildFire, PlaceHive and wherever VillageFurnishing/VillageCooking/VillageSeats place pieces
ZNetView view = instance.GetComponent<ZNetView>();
if (view != null && view.GetZDO() != null)
{
	view.GetZDO().Set(VillageRecords.RebuiltKey, true);
}

/// <summary>
/// Keeps pieces the vikings built for wood from dropping the resources of their recipe
/// </summary>
[HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
public static class VillagePieceDropPatch
{
	/// <summary>
	/// Skips the drops of a piece the vikings built
	/// </summary>
	/// <param name="__instance">The piece being destroyed</param>
	/// <returns>False to drop nothing</returns>
	private static bool Prefix(Piece __instance)
	{
		ZNetView view = __instance.m_nview;
		return(view == null || !view.IsValid() || !view.GetZDO().GetBool(VillageRecords.RebuiltKey));
	}
}
```

Pieces that are part of the generated location rather than rebuilt still drop normally. If those should be covered too, set the key in `TrySnapshot`.

### C04 - Skipping MonsterAI.UpdateAI freezes the hurt timer and healing, and a hurt viking can freeze in place (Medium)

**Files:**
- `ZombieApocalypse/VikingPatches.cs:622-631` (the two skip paths)
- `ZombieApocalypse/VillageLife.cs:875-954` (BeforeAI), `972-1034` (UpdateFlee, `fighting` at 1001) and `1134-1184` (HangBack)
- `ZombieApocalypse/VikingRestedPatches.cs:13-47` (regeneration and `VikingFood.Update`)
- `ZombieApocalypse/ZombieViking.cs:1697` (InDanger)

**Vanilla:**
- `m_timeSinceHurt += dt` and `UpdateRegeneration(dt)` only happen in `BaseAI.UpdateAI` (`BaseAI.cs:325-326`).
- `MonsterAI.UpdateAI` reaches it through `base.UpdateAI` (`MonsterAI.cs:349`).
- When `VikingAIPatch` returns false, all of that is skipped.

**What goes wrong:**
- A viking hurt below 25% in a fight starts hanging back with `m_timeSinceHurt` under `AttackedTime`.
- When the enemy dies, `fighting` stays true because the timer is frozen.
- Health is frozen too, so the viking stays "low".
- With healthy allies within 15 m, `HangBack` finds no enemy and calls `StopMoving()`. The viking stands still until its allies walk off.
- Food doesn't digest during any skip, and `InDanger()` stays true, which holds off sheathing and roof avoidance.

**Fix:** Both skip paths only run on the owner, so the base bookkeeping can run there.

```csharp
if (__instance.TryGetComponent(out VillageLife life) && life.BeforeAI(dt))
{
	SkippedUpdate(__instance, dt);
	__result = true;
	return(false);
}
if (viking.UpdateReturn(dt))
{
	SkippedUpdate(__instance, dt);
	__result = true;
	return(false);
}

/// <summary>
/// Runs the parts of the AI's update every creature needs while its own update is skipped - its healing and how
/// long since it was hurt
/// </summary>
/// <param name="ai">The AI, owned by this client</param>
/// <param name="dt">Seconds since the last update</param>
private static void SkippedUpdate(MonsterAI ai, float dt)
{
	ai.UpdateRegeneration(dt);
	ai.m_timeSinceHurt += dt;
}
```

### C07 - The forsaken power cooldown carries over into the next world (Medium, lifecycle)

**Files:**
- `ZombieApocalypse/ZombieViking.cs:313`, `1193-1199` and `2416-2425`
- `ZombieApocalypse/ServerCheck.cs:101-112`

**What goes wrong:**
- `powerCooldownUntil` is a static in ZNet world-time ticks. It is only ever raised and never reset.
- Each world has its own time: `ZNet.cs:1978` loads it from the world file, and `ZNet.cs:1118` receives it from the server.
- After using a power in an older world (say day 300) and joining a newer one, `now < powerCooldownUntil` holds until the new world's clock catches up.
- Worse, `zdo.Set(PowerUsedKey, true)` (line 2416) runs before the cooldown check (line 2418). Every viking that tries during that time loses its only chance for good.

**Fix:**

```csharp
/// <summary>
/// Forgets the forsaken power cooldown, which is in the world's time, when leaving a world
/// </summary>
public static void ResetPowerCooldown()
{
	powerCooldownUntil = 0L;
}

// ServerCheckLeavePatch.Postfix (ZNet.OnDestroy)
ZombieViking.ResetPowerCooldown();
```

Also consider moving the `PowerUsedKey` write after the cooldown check, so a viking that is refused by the cooldown can try again later.

### C05 - A blocked hit skips the zombie viking's armor (Low)

**File:** `ZombieApocalypse/VikingPatches.cs:366-392` (VikingHitPatch) and `398-416` (VikingArmorPatch)

**What goes wrong:**
- `VikingHitPatch.Target` is a single static, set in the `Character.RPC_Damage` prefix and cleared in the finalizer.
- When a viking blocks, `Humanoid.BlockAttack` (`Humanoid.cs:1872`) calls `attacker.Damage(hitData)`. If this peer owns the attacker, `ZRoutedRpc` runs that `RPC_Damage` inline (`ZRoutedRpc.cs:130-133`).
- The nested call's finalizer sets `Target` to null.
- The outer `ApplyDamage` (`Character.cs:2403`, after the block at `2354-2356`) then sees `Target != __instance` and skips the viking's armor reduction and armor wear for the damage left after the block.

**Fix:** Restore the outer value instead of clearing it.

```csharp
private static void Prefix(Character __instance, out Character __state)
{
	__state = Target;
	Target = ((__instance.TryGetComponent(out ZombieViking _)) ? __instance : null);
}

private static Exception Finalizer(Exception __exception, Character __state)
{
	Target = __state;
	return(__exception);
}
```

### C18 - Vikings open doors inside player wards (Low)

**File:** `ZombieApocalypse/VikingDoors.cs:677-692` (Open), `700-728` (CloseBehind) and `757-760` (IsUsable)

**What goes wrong:**
- `IsUsable` has no ward check, and vanilla `Door.RPC_UseDoor` (`Door.cs:204-226`) has no access check of its own.
- Chests are already protected: `VillageLife.Usable` keeps vikings out of warded player chests (`VillageLife.cs:2190-2193`).
- When `OpenTimeout` passes, or `state.Opened.Clear()` runs (line 444), the entry is dropped without closing the door. A door the viking never passed through can stay open, and zombies can walk in.

**Fix:**

```csharp
private static bool IsUsable(Door door)
{
	if (!door || door.m_nview == null || !door.m_nview.IsValid() || door.m_keyItem != null || door.m_canNotBeClosed)
	{
		return(false);
	}
	// A ward keeps the vikings out of the doors a player built
	return(door.m_nview.GetZDO().GetLong(ZDOVars.s_creator, 0L) == 0L || !Warded(door.transform.position));
}
```

Move `VillageLife.Warded` (`VillageLife.cs:2201-2211`) somewhere both classes can use it.

### C11 - The farm spawn counter sticks for the session if placing a location throws (Low, defensive)

**Files:**
- `ZombieApocalypse/VikingSpawnsPatches.cs:177-203`
- `ZombieApocalypse/VikingSpawns.cs:550-556`, `583` and `671-677`

**What goes wrong:**
- In Client mode, the `ZoneSystem.SpawnLocation` prefix increments `clientDepth`, and only the postfix decrements it. Client mode also runs on the host and in single player for zones that are already generated (`ZoneSystem.cs:1269`).
- If anything throws inside `SpawnLocation`, such as another mod or a broken location, the postfix doesn't run.
- `clientDepth > 0` (line 583) then makes every later farm skip `OnRandomized`, `OnPieceAwake` and `OnRoomsGenerated` for the rest of the process, so those farms get no living vikings.
- `generatingFarm` and `generatingOutpost` can't stick, because the next non-client start resets them (lines 557-560).

**Fix:**

```csharp
/// <summary>
/// Undoes what the prefix noted when placing the location threw
/// </summary>
/// <param name="__exception">The exception thrown, or null</param>
/// <param name="mode">How it was being placed</param>
/// <returns>The exception, so it's still thrown</returns>
private static Exception Finalizer(Exception __exception, ZoneSystem.SpawnMode mode)
{
	if (__exception != null)
	{
		VikingSpawns.OnLocationFailed(mode);
	}
	return(__exception);
}
```

In `OnLocationFailed`, Client mode does `clientDepth = Mathf.Max(0, clientDepth - 1);`. Other modes clear the farmhouse and hive lists and the generating flags.

### C30 - Village respawn timers carry over a logout, skipping the settle and confirmation waits (Low, lifecycle)

**Files:**
- `ZombieApocalypse/VillageRespawn.cs:142` (`zoneLoadedSince`), `146` (`mismatches`), `479-498` (TrackZones) and `735-757` (CheckCount)
- `ZombieApocalypse/VillageRecords.cs:73`, `ZombieApocalypse/FarmLayout.cs:61`
- `ZombieApocalypse/ServerCheck.cs:101-112` (where the clearing would go)

**What goes wrong:**
- `Mismatch.Since` and `zoneLoadedSince` hold `Time.time` values, and `Time.time` doesn't reset between scenes.
- ZDOIDs repeat across loads of the same world: `ZNet.Awake` resets `ZDOID.m_loadID = 0` (`ZNet.cs:335`), and `ZDO.Load` assigns `++m_loadID` (`ZDO.cs:1298`). On a server they also stay the same for its whole session.
- **Skipped confirmation:** after a relog, a leftover mismatch with the same sign has already passed the 20 s `MismatchTime` (line 752), so deaths are recorded or forgotten without the confirmation wait.
- **Skipped settle:** `zoneLoadedSince` keeps the old timestamps for zones that load again (line 490), so the zone counts as settled at once. That skips the 15 s settle before `TrySnapshot`, `CheckCount` and `TryRespawn`, while a multiplayer client is still streaming objects in. (The verifier found this while checking C30.)
- `VillageRecords.cache` can't return stale data, because it matches by `ReferenceEquals` (line 418). Together with `FarmLayout.pens`, it only collects a little memory per world.

**Fix:** Clear them when leaving a world.

```csharp
// ServerCheckLeavePatch.Postfix
private static void Postfix()
{
	ServerCheck.Clear();
	ConfigSync.Leave();
	VillageRespawn.Forget();
	VillageRecords.Forget();
	FarmLayout.Forget();
}

/// <summary>
/// Forgets the zones and head count mismatches noted in the world being left
/// </summary>
public static void Forget()
{
	zoneLoadedSince.Clear();
	mismatches.Clear();
}
```

`VillageRecords.Forget()` and `FarmLayout.Forget()` clear `cache` and `pens` the same way.

### C26 - The static door list is only pruned on clients that own a living viking (Low, memory)

**Files:** `ZombieApocalypse/VikingDoors.cs:229` (`doors`), `263-266` (Register) and `787-806` (FindDoors); `ZombieApocalypse/VikingDoorsPatches.cs:8-19`

**What goes wrong:**
- Every `Door.Awake` appends to `doors`, including zone reloads, every world and placement ghosts.
- The only pruning is in `FindDoors`, which only runs for living vikings this client owns (`VillageLife.cs:885`, `2261`, `3840`).
- On a client that never owns a villager, the list grows for the whole process and pins the managed `Door` shells. That's KB to low MB over a long session.

**Fix:**

```csharp
private static int nextPrune = 256;

public static void Register(Door door)
{
	if (doors.Count >= nextPrune)
	{
		doors.RemoveAll(IsGone);
		nextPrune = doors.Count * 2 + 256;
	}
	doors.Add(door);
}

/// <summary>
/// Checks whether a door has been destroyed
/// </summary>
/// <param name="door">The door</param>
/// <returns>True when it has</returns>
private static bool IsGone(Door door)
{
	return(!door);
}
```

In `FindDoors`, also add `crossings.Remove(door);` next to `infos.Remove(door);`.

### C28 - GamePrefabs.Find caches a miss for the whole session (Low, NEEDS RUNTIME TEST)

**File:** `ZombieApocalypse/GamePrefabs.cs:22-42`. It is called from `VikingPoses.cs:884`, `893`, `WakeRoar.cs:104`, `114`, `ZombieFlies.cs:106`, `164` and `SilverLining.cs:186`.

**What goes wrong:**
- A miss is cached as `found[name] = null` forever (line 40).
- `sfx_goblinbrute_shout` lives in asset bundle c4210710. Whether it is reachable through `ZNetScene.GetPrefab` or only loaded on demand can't be settled statically.
- If it's loaded on demand, a shout before any goblin brute has loaded locks in the fallback sound for the session.
- Every caller null-checks the result, so the worst case is a sound or effect silently missing, not a crash.

**What to test:**
1. In a fresh session, check the BepInEx log for "sfx_goblinbrute_shout isn't loaded" or "sfx_trollfire_roar isn't loaded".
2. Visit the Plains, then check whether vikings still use the fallback shout.
3. After a logout and login, check that zombie flies and the buzz still appear.

**Fix:** Retry a miss at most once a minute, since `Resources.FindObjectsOfTypeAll` is too slow to run on every call.

```csharp
private const float RetryInterval = 60f;
private static readonly Dictionary<string, float> missedAt = new Dictionary<string, float>();

public static GameObject Find(string name)
{
	if (found.TryGetValue(name, out GameObject prefab))
	{
		if (prefab != null)
		{
			return(prefab);
		}
		found.Remove(name);
	}
	if (missedAt.TryGetValue(name, out float at) && Time.time - at < RetryInterval)
	{
		return(null);
	}
	prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(name) : null);
	if (prefab == null)
	{
		foreach (GameObject loaded in Resources.FindObjectsOfTypeAll<GameObject>())
		{
			if (loaded.name == name && loaded.transform.parent == null)
			{
				prefab = loaded;
				break;
			}
		}
	}
	if (prefab != null)
	{
		found[name] = prefab;
		missedAt.Remove(name);
	}
	else
	{
		missedAt[name] = Time.time;
	}
	return(prefab);
}
```

### C25 - A remark says the room method can't be patched, while another patch patches it (Low, documentation)

**Files:** `ZombieApocalypse/VikingSpawnsPatches.cs:158-159` (remark on `VikingFarmPatch`); `ZombieApocalypse/VillageFarmhousesPatches.cs:12`

**What goes wrong:**
- The remark says "The method that places the rooms is too small to patch reliably, since it can be inlined."
- `VillageFarmhousesPatch` patches exactly that method, `DungeonGenerator.GenerateRooms` (`DungeonGenerator.cs:284-298`), and depends on it.
- The method is a 3-case switch, over Mono's inlining size, and Harmony stops a patched method from being inlined anyway.
- The remark also explains an absence, which the project's comment rules ask to leave out.

**Fix:** Delete that sentence, so the remark ends at "...so a client showing a farm that's already there notes none".

## 4. Performance Bottlenecks

Call frequencies come from the decompiled code. `MonoUpdaters` runs `BaseAI.UpdateAI` at 20 Hz for every loaded AI on every client, and per-instance costs assume 3k-10k loaded `ZNetView`s around a village. The millisecond figures are static estimates. Measure with a Stopwatch or the Unity profiler before deciding.

### C31 - Choosing a job walks every loaded object 9-11 times (Medium)

**Estimated impact:**
- About 0.3-1 ms per full scan, so about 3-10 ms frame spikes per job decision.
- With about 10 owned vikings, that's 2-3 spikes a second on the client that owns the village.
- It reaches High only above about 10k loaded instances.

**Files:**
- Driver: `ZombieApocalypse/VillageLife.cs:1479-1515` (Idle). `FindWork` runs every `WorkCheckInterval` of 5 s (lines 304, 1489-1492) for idle, stroll and pet jobs. `ChooseJob` runs at every job end (line 1502), and idle jobs last 4-12 s (lines 350-351).
- Decisions: `ChooseJob` 1708-1779, `FindWork` 1794-1852, `FindWoodWork` 1860-1885.
- Scans: `UsableChests` 2145-2162, `FindLoot` 2790-2840, `FindHive` 3234+, `FindForage` 3392+ and `FindStation` 7784-7803.
- Also `VillageCooking.Stations` (`VillageCooking.cs:230-247`) and `VillageRecords.Damaged` / `Missing` (`VillageRecords.cs:251-275`, `137-175`).

**What happens:** A daytime `ChooseJob` runs the following, about 9-11 full `ZNetScene.m_instances` walks in all:
- `FindCookWork`: a station scan plus `UsableChests`
- `FindLoot`: `UsableChests` plus its own scan
- `FindUpgradeChest`
- `Damaged`: every WearNTear
- `FindGearWork`: two `FindStation` calls
- `FindWoodChest` and `FindArrowWork`
- `FindStockChest`
- `FindHive` and `FindForage`: two scans each

`UsableChests` and the loot, hive and forage scans call `GetComponent` on every instance. Those are the costly ones, while `FindStation` and `Stations` filter on `GetPrefab()` first. The scans only run on the owner.

**Fix:** Keep one shared index that is re-listed every 2 s, so each viking filters a few dozen chests:

```csharp
private const float ChestListTime = 2f;
private static readonly List<Container> loadedChests = new List<Container>();
private static float chestsListedAt = -1f;

/// <summary>
/// Lists the loaded chests, looking again every ChestListTime seconds
/// </summary>
/// <returns>The chests, some of which may have been destroyed since</returns>
private static List<Container> LoadedChests()
{
	if (chestsListedAt >= 0f && Time.time - chestsListedAt < ChestListTime)
	{
		return(loadedChests);
	}
	chestsListedAt = Time.time;
	loadedChests.Clear();
	foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
	{
		ZNetView view = instance.Value;
		Container chest = ((view != null) ? view.GetComponent<Container>() : null);
		if (chest != null)
		{
			loadedChests.Add(chest);
		}
	}
	return(loadedChests);
}

private List<Container> UsableChests()
{
	List<Container> chests = new List<Container>();
	foreach (Container chest in LoadedChests())
	{
		if (Usable(chest))
		{
			chests.Add(chest);
		}
	}
	return(chests);
}
```

`Usable` already rejects destroyed chests (line 2171).
- Ideally, a single pass fills a list per component type, adding ItemDrop for `FindLoot` and Beehive and Pickable for `FindHive` and `FindForage`.
- `ForageJob` re-runs `FindHive` and `FindForage` after every pick, so it gains the most.

### C33 - VillageLivestock allocates a closure for every loaded object once a second (Medium)

**Estimated impact:**
- About 90 B (a display class and a delegate) per loaded instance per second.
- At 10k instances, that's about 0.9 MB/s of garbage and about 1 ms of CPU per second.
- It happens on every client with a loaded village that has vikings.

**File:** `ZombieApocalypse/VillageLivestock.cs:30-62` (lambda at line 52)

**What happens:**
- `villages.Exists((Village village) => village.DistanceOutside(view.transform.position) <= ...)` captures `view`, which is declared inside the loop.
- The compiler creates the closure at the top of every iteration, even when `view == null`.
- Each call also makes a native `transform.position` call.

**Fix:** Use the ZDO position and a plain loop.

```csharp
foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
{
	ZNetView view = instance.Value;
	if (view == null)
	{
		continue;
	}
	Vector3 position = instance.Key.GetPosition();
	bool near = false;
	foreach (Village village in villages)
	{
		if (village.DistanceOutside(position) <= Village.PieceMargin)
		{
			near = true;
			break;
		}
	}
	if (!near)
	{
		continue;
	}
	// the existing CreatureSpawner check and taming follow unchanged
}
```

### C32 - A waiting zombie corpse rebuilds its whole look every frame (Low)

**Estimated impact:**
- About 10-40 µs and 0.5-1 KB of garbage per frame for each waiting corpse.
- It lasts for `RiseDelay` (default 5 s), so 10 corpses after a fight cost about 0.3 ms a frame for 5 s.

**Files:** `ZombieApocalypse/RisingCorpse.cs:77-80`; `ZombieApocalypse/ZombieLook.cs:33-39` and `44-87`

**What happens:**
- `look.Amount` is computed from ZNet ticks, so it changes every frame.
- `ZombieLook.Update` refreshes whenever `Amount != appliedAmount`.
- Each refresh allocates the `GetComponentsInChildren<Renderer>(true)` array, a `sharedMaterials` array per renderer and a `material.name` string per material (`ZombieEyes.IsEyeMaterial`, `ZombieEyes.cs:75`).
- The mottling only has 4 steps (`Mottling.cs:22`). Only the tint changes smoothly.

**Fix:** Step the amount in `RisingCorpse.Update`, so the look refreshes 20 times over the delay instead of every frame.

```csharp
private const float TintSteps = 20f;
...
if (!handedOver)
{
	float amount = ((riseAt > diedAt) ? Mathf.Clamp01((float)(now - diedAt) / (riseAt - diedAt)) : 1f);
	look.Amount = (Mathf.Round(amount * TintSteps) / TintSteps);
}
```

### C35 - VillageHazards walks every loaded object once a second (Low)

**Estimated impact:** About 0.5-1.5 ms once a second at 10k instances, only on clients that own active vikings.

**File:** `ZombieApocalypse/VillageHazards.cs:115-157`. It is called from `VillageLife.cs:3922`, `4100` and `6071`.

**What happens:** For every instance that isn't a poison cloud, `Refresh` does a Unity null check and two ZDO lookups (`GetLong(RiseAtKey)` and `GetBool(ZombieKey)`, lines 138-139). It is gated to 1 Hz (lines 117-121), so this is a spike once a second.

**Fix:** Track corpses as they wake instead of scanning, and give `PoisonCloudFade` a static list the same way.

```csharp
/// <summary>
/// Notes every corpse as it wakes, so the hazards don't have to look through every object
/// </summary>
[HarmonyPatch(typeof(Ragdoll), nameof(Ragdoll.Awake))]
public static class VillageHazardsRagdollPatch
{
	/// <summary>
	/// Notes the corpse that woke
	/// </summary>
	/// <param name="__instance">The corpse</param>
	private static void Postfix(Ragdoll __instance)
	{
		VillageHazards.Register(__instance);
	}
}
```

`Refresh` then loops over the registered corpses, dropping destroyed ones with `RemoveAll`, and checks the same ZDO keys.

### C44 - SnapPoints allocates two strings per nearby piece per frame in build mode (Low)

**Estimated impact:**
- About 0.5 µs and 150 B per collider hit within 10 m, per frame.
- That's about 0.1 ms and 30 KB a frame in a dense base.
- It only happens in build mode, and only while some piece is set to false.

**File:** `SnapPoints/SnapPatches.cs:610-640` (`GetPrefabName` at about line 635)

**What happens:**
- Vanilla `Piece.GetSnapPoints(Vector3, ...)` (`Piece.cs:476-488`) calls `GetSnapPoints(points)` once per overlapped collider, so the same piece is hit more than once.
- `Utils.GetPrefabName` reads `gameObject.name` and then calls `Remove`, and both allocate (`assembly_utils` `Utils.cs:200-212`).

**Fix:** Look the state up by the ZDO prefab hash. The ghost falls back to its name.

```csharp
ZNetView nview = __instance.m_nview;
int prefab = ((nview != null && nview.IsValid()) ? nview.GetZDO().GetPrefab() : Utils.GetPrefabName(__instance.gameObject).GetStableHashCode());
if (PieceConfig.GetState(prefab) == false)
{
	points.RemoveRange(__state, points.Count - __state);
}
```

`PieceConfig` would keep a `Dictionary<int, bool>` keyed by `name.GetStableHashCode()` next to `states`, filled where `AnyOff` is set (`PieceConfig.cs:203`), with an int overload of `GetState`.

### C43 - Every lit piece and sign has its own per-frame Update (Low, NEEDS RUNTIME TEST)

**Estimated impact:**
- About 0.3-0.5 µs each, mostly Unity's Update dispatch.
- That's about 0.1 ms per frame at 300 lit pieces and about 0.4 ms at 1000.

**Files:** `LightColor/LightTint.cs:90-93` (Update) and `187-205` (Refresh); `LightColor/PiecePatches.cs:17-28` and `84`

**What happens:** `Refresh` returns early on an unchanged `DataRevision`, so each call is cheap. The cost is the number of components.

**What to test:** In the largest base, profile total `LightTint.Update` time next to the number of `LightTint` components. Act only above about 0.2 ms.

**Fix:** Throttle and stagger the check. `Init` sets `nextRefresh = Time.time + Random.value * RefreshInterval` so the pieces don't all refresh on the same frame.

```csharp
private const float RefreshInterval = 0.25f;
private float nextRefresh;

private void Update()
{
	if (Time.time < nextRefresh)
	{
		return;
	}
	nextRefresh = Time.time + RefreshInterval;
	Refresh();
}
```

### C45 - Zombie lurch bending runs for zombies that are off screen (Low, NEEDS RUNTIME TEST)

**Estimated impact:**
- About 5-15 µs per walking zombie per frame, so about 0.3-0.9 ms per frame at 50 loaded zombies.
- Nothing on a dedicated server, which `ZombieLurch.cs:303` already skips.

**File:** `ZombieApocalypse/ZombieLurch.cs:327-382` (LateUpdate) and `784-802` (PutBack)

**What happens:** The bone bending and its end-of-frame put-back run for every loaded zombie, whether or not it can be seen.

**What to test:** Profile total `ZombieLurch.LateUpdate` time during a horde.

**Fix:** `PutBack` only restores when `applied` is set, so skipping is safe.

```csharp
private Renderer body;

// in Start, after the bones are found
body = GetComponentInChildren<SkinnedMeshRenderer>();

// in LateUpdate, right after UpdateSpeed(dt);
if (body != null && !body.isVisible)
{
	return;
}
```

## 5. Multiplayer / ZDO Sync Risks

C01 (section 2) is the most serious multiplayer issue.

### C02 - Viking chest use can duplicate or lose items (Medium)

**Files:** `ZombieApocalypse/VillageLife.cs:819-838` (ShowChestOpen), `2169-2194` (Usable) and `2646-2658` (OpenChest). They are called from lines 5232-5233 and 5696-5697, and `ReachTime` (0.8 s) is set at line 312.

**(a) The viking works on stale contents:**
- `ShowChestOpen` claims ownership and sets `chest.m_inUse = true`.
- 0.8 s later, `OpenChest` calls `chest.Load()`, but vanilla `Load` returns early while `m_inUse` is set (`Container.cs:485-488`).
- The viking therefore works on whatever the 1 Hz `CheckForChanges` last loaded (`Container.cs:98`).
- If a player changed the chest within that second, the viking's `Save` (`OnContainerChanged`, `Container.cs:462-468`) overwrites the change. Items the player took come back, or items the player stored are lost.

**(b) The viking can take a chest from a player:**
- The owner's `RPC_RequestOpen` hands ownership to the player at once (`Container.cs:329-331`), but the player's `s_inUse = 1` only comes back later (`InventoryGui.cs:700-702`).
- In that window `Usable` still passes, so the viking claims the chest back.
- The player's later moves aren't saved, because only the owner saves.

**Fix:**

```csharp
private bool OpenChest(Container chest)
{
	if (!Usable(chest))
	{
		return(false);
	}
	if (!chest.m_nview.IsOwner())
	{
		chest.m_nview.ClaimOwnership();
	}
	// Load skips a chest marked in use, which ShowChestOpen has done
	bool inUse = chest.m_inUse;
	chest.m_inUse = false;
	chest.Load();
	chest.m_inUse = inUse;
	return(true);
}
```

Also call `chest.Load();` in `ShowChestOpen` right after `ClaimOwnership()`, before `m_inUse = true`. In `Usable`, refuse a chest that another peer owns while a player is close:

```csharp
long owner = zdo.GetOwner();
if (owner != 0L && owner != ZDOMan.GetSessionID() && Player.IsPlayerInRange(position, PlayerChestRange))
{
	return(false);
}
```

Both races are narrow, about a second or a network round trip, but the result is item duplication or loss.

### C06 - Owner-only viking code runs on every client (Low)

**Files:**
- `ZombieApocalypse/VikingPatches.cs:611-635` (VikingAIPatch.Prefix)
- `ZombieApocalypse/ZombieViking.cs:1709-1754` (UpdateSheath; `SetTrigger` at 1724 and 1751, array at 1729)

**What goes wrong:**
- `MonsterAI.UpdateAI` runs for every loaded AI on every client, and the owner check is inside `BaseAI.UpdateAI` (`BaseAI.cs:311-315`). The prefix runs before that check.
- On a non-owner, the alert state comes from the ZDO, so every client flips its own `sheathed` state on each alert change.
- Each such client equips or unequips locally and calls `m_zanim.SetTrigger("equip_hip")`, which sends to `ZNetView.Everybody` (`ZSyncAnimation.cs:148-151`).
- The visible equipment still comes from the owner's ZDO (`VisEquipment.cs:475`). The real costs are a duplicate animation trigger from every client that has the viking loaded, and per-call allocations and inventory scans in `ChooseWeapon` on every client.
- `TuneWeaponAI` and the roof mask are restored in the finalizer, so they are harmless.
- `UpdateSheath` also runs a second time per frame on the owner (`ZombieViking.cs:948`).

**Fix:**

```csharp
if (!__instance.TryGetComponent(out ZombieViking viking))
{
	return(true);
}
if (!__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
{
	return(true);
}
```

### C08 - Per-player settings changed while on a server are lost when leaving it (Low, config sync)

**File:** `ZombieApocalypse/ConfigSync.cs:24`, `113-133` and `218-222`

**What goes wrong:**
- While connected, `SaveOnConfigSet` is off (line 221), so a Configuration Manager change to `ZombieBuzzVolume` or `ZombieHazeStrength` stays in memory.
- `Leave()` calls `config.Reload()` (line 125), which puts the file's values back.
- These are exactly the settings meant to be each player's own.

**Fix:** Keep the local values across the reload, and write them back with saving on.

```csharp
Dictionary<ConfigDefinition, object> local = new Dictionary<ConfigDefinition, object>();
foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> pair in config)
{
	if (LocalSettings.Contains(pair.Key.Key))
	{
		local[pair.Key] = pair.Value.BoxedValue;
	}
}
applying = true;
try
{
	config.Reload();
}
finally
{
	applying = false;
	config.SaveOnConfigSet = savedSaveOnConfigSet;
}
// With saving back on, this writes the player's own values to their file
foreach (KeyValuePair<ConfigDefinition, object> pair in local)
{
	config[pair.Key].BoxedValue = pair.Value;
}
```

### C12 - Two clients can both rebuild the village fire (Low)

**Files:** `ZombieApocalypse/Village.cs:561-574` (Fire cache), `616-623` (FireMissing) and `631-657` (RebuildFire); `ZombieApocalypse/VillageLife.cs:1869-1871`

**What goes wrong:**
- `Village` objects and `workClaims` are per client, and vikings of one village can be owned by two clients.
- `RebuildFire` checks `FireMissing()`, which returns the cached null for up to 3 s.
- If both vikings finish within that window, two bonfires are placed in the same spot.

**Fix:**

```csharp
nextFireCheck = 0f;
if (prefab == null || !FireMissing(out PieceRecord _) || VillageRecords.StandsAt(record))
{
	return(false);
}
```

Make `VillageRecords.StandsAt` internal for this.

### C13 - A viking and a player picking up the same item can both get it (Low)

**File:** `ZombieApocalypse/VillageLife.cs:2871-2896` (PickUpLoot), `3202-3226` (PickUpDrop) and `4627-4647` (GatherWood). They are called from lines 5166, 6038 and 6622.

**What goes wrong:**
- Vanilla only picks up as the owner (`ItemDrop.CanPickup`, `ItemDrop.cs:1712-1727`), and otherwise calls `RequestOwn` and waits (`ItemDrop.cs:1635-1644`, `1730-1736`).
- The viking claims ownership locally and destroys the drop at once.
- A player who owns the drop on their own client and picks it up at the same moment gets it too.

**Fix:**
- Call `drop.RequestOwn();` where `takeAt` / `actAt` is set. The 0.8 s reach time becomes the wait.
- In each pickup, replace `view.ClaimOwnership();` with:

```csharp
if (!view.IsOwner())
{
	if (view.GetZDO().HasOwner())
	{
		return(false);
	}
	view.ClaimOwnership();
}
```

### C14 - RaidHunt writes zombie ZDOs from clients that don't own them (Low)

**File:** `ZombieApocalypse/RaidHunt.cs:43-56` and `62-73`

**What goes wrong:**
- The `MonsterAI.UpdateAI` prefix runs before the owner check (`BaseAI.cs:311`).
- When a raid ends, every client calls `zdo.Set(HuntKey, false)` on every zombie it has loaded.
- A non-owner's write raises the data revision and queues a send (`ZDO.cs:578-585`, `ZDOMan.cs:1295-1311`), so every client sends a stale copy of each zombie.

**Fix:**

```csharp
ZDO zdo = ai.m_nview.GetZDO();
if (zdo == null || !ai.m_nview.IsOwner() || !zdo.GetBool(HuntKey))
{
	return;
}
```

### C15 - Dedicated servers build zombie looks, eyes and flies nobody sees (Low)

**Files:**
- `ZombieApocalypse/Zombies.cs:339-350` and `DeathPatches.cs:216-220`, `290-291`
- `ZombieApocalypse/RisingCorpse.cs:60` and `245`
- `ZombieApocalypse/ZombieLook.cs:33-87` and `Mottling.cs:45-72`, `99-169`

**What goes wrong:**
- A dedicated server keeps creatures loaded around its reference position (`ZNetScene.cs:360-367`).
- Only `ZombieLurch` and `VikingPoses` check `IsDedicated` (`ZombieLurch.cs:303`, `VikingPoses.cs:579`).
- So the server runs `ZombieLook.Refresh` once a second per zombie, and `Mottling` runs Blit, `ReadPixels` and per-pixel Perlin noise for each texture step. The eyes and flies are built too.

**Fix:** `RisingCorpse` reads `look`, so keep the components but switch them off.

```csharp
// ZombieLook / ZombieEyes / ZombieFlies
private void Awake()
{
	if (ZNet.instance != null && ZNet.instance.IsDedicated())
	{
		enabled = false;
	}
}
```

Also return early from any `Start` that builds objects.

### C16 - The generator ZDO is rewritten and resent every 5 s while a village waits for its area to load (Low)

**Files:** `ZombieApocalypse/VillagePaths.cs:518-522` (before the waits at 529-531, 548-551 and 564-567); `ZombieApocalypse/Village.cs:255-265`

**What goes wrong:**
- `ZDO.Set(int, byte[])` raises the revision whenever the value changes (`ZDO.cs:454-459`).
- The comparison is `m_values[num].Equals(value)` (`assembly_utils` `BinarySearchDictionary.cs:243`), which is reference equality for arrays. A new array always counts as a change.
- So the ZDO is resent every 5 s, and every client re-reads it in `Village.Read`.
- Together with C01, this goes on until the paths are laid.

**Fix:** In `Village.RecordHouses`:

```csharp
byte[] bytes = pkg.GetArray();
byte[] old = generator.GetByteArray(AllHousesKey);
if (old != null && old.SequenceEqual(bytes))
{
	return;
}
generator.Set(AllHousesKey, bytes);
```

### C17 - Other clients see moved trees and rocks at the wrong height after a path is laid (Low, cosmetic)

**File:** `ZombieApocalypse/VillagePaths.cs:746-778` (MoveObjects, called at 735)

**What goes wrong:**
- `ClaimOwnership` and then `ZDO.SetPosition` update the ZDO (`ZDO.cs:480-489`).
- Static objects have no `ZSyncTransform`, and `ZNetScene` doesn't move existing instances from ZDO data.
- Other clients with the area loaded see the old height until the zone reloads, while the terrain change itself does sync.

**Fix:** Send the new positions to everyone with a routed RPC.

```csharp
// After the move loop
ZPackage pkg = new ZPackage();
pkg.Write(moved.Count);
foreach (ZNetView view in moved)
{
	pkg.Write(view.GetZDO().m_uid);
	pkg.Write(view.transform.position);
}
ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, MovedRpc, pkg);

/// <summary>
/// Moves the objects a village's paths moved, on the other clients
/// </summary>
/// <param name="sender">The client that laid the paths</param>
/// <param name="pkg">Each object's ZDOID and new position</param>
private static void RPC_Moved(long sender, ZPackage pkg)
{
	int count = pkg.ReadInt();
	for (int i = 0; i < count; i++)
	{
		ZDOID id = pkg.ReadZDOID();
		Vector3 position = pkg.ReadVector3();
		ZNetView view = ((ZNetScene.instance != null) ? ZNetScene.instance.FindInstance(id) : null);
		if (view != null && !view.IsOwner())
		{
			view.transform.position = position;
		}
	}
}
```

### C19 - A viking can reopen a door a player just closed (Low)

**File:** `ZombieApocalypse/VikingDoors.cs:680`, `715-726` and `767-770`

**What goes wrong:**
- `Door.RPC_UseDoor` toggles based on the owner's state (`Door.cs:210-224`), but the viking's client decides from its own copy, which may be stale.
- Vanilla narrows the window: the owner's `CanInteract()` refuses while the door animates (`Door.cs:81-92`).
- So the bug only shows when the round trip is longer than the door animation. Two vanilla players have the same race.

**Fix:** Use an explicit-state RPC, registered in `VikingDoorsPatch.Postfix`.

```csharp
door.m_nview.Register<bool>(SetDoorRpc, (long sender, bool open) => SetDoor(door, open));

/// <summary>
/// Opens or closes a door on its owner, leaving it be when it already is
/// </summary>
/// <param name="door">The door</param>
/// <param name="open">True to open it</param>
private static void SetDoor(Door door, bool open)
{
	if (!door.m_nview.IsOwner() || !door.CanInteract())
	{
		return;
	}
	bool isOpen = (door.m_nview.GetZDO().GetInt(ZDOVars.s_state) != 0);
	if (isOpen != open)
	{
		door.RPC_UseDoor(0L, true);
	}
}
```

The real code also needs the forward or back choice for opening.

### C27 - The server keeps every client that ever connected in ConfigSync's list (Low, dedicated server)

**File:** `ZombieApocalypse/ConfigSync.cs:29`, `104-108` (AddClient), `113-116` (Leave) and `164-176` (SendToAll)

**What goes wrong:**
- On the server, each client with the mod is added to `clients` when it says hello.
- Disconnected entries are only dropped by `IntersectWith` in `SendToAll`, which runs when a setting changes, or by `Leave` on `ZNet.OnDestroy`.
- A dedicated server that stays up with an unchanged config keeps every `ZRpc` it ever had, each pinning a disposed socket and its RPC table. That's a few KB per past connection.
- `TweakStats/ServerSync.cs:117-143` already solves this with `ForgetDisconnected`.

**Fix:**

```csharp
public static void AddClient(ZRpc rpc)
{
	HashSet<ZRpc> connected = new HashSet<ZRpc>();
	foreach (ZNetPeer peer in ZNet.instance.GetPeers())
	{
		connected.Add(peer.m_rpc);
	}
	clients.IntersectWith(connected);
	clients.Add(rpc);
	Send(rpc);
}
```

## 6. Discarded False Positives Log

- **C09** (LightColor admin-only settings are only enforced by each client): Valheim has no server-side check on ZDO writes, so this is the same trust model as vanilla wards. The README already says the mod is client-side, and `.AdminSettings` is sent in the `RPC_PeerInfo` postfix before the client loads the world (`AdminSyncPatches.cs:24-42`).
- **C20** (FarmLayout `farmhousesMax` stays unset): `GetRandomWeightedRoom` only returns null when no eligible room exists at all (`DungeonGenerator.cs:1018-1045`), so the `continue` can't fire on the iteration that sets the cap.
- **C22** (LightColor shield-dome ComputeBuffers never released): each pass's buffer is swapped into `m_shieldDomeBuffer`, vanilla `PrepareComputeBuffer` releases and recreates it when the count changes (`ShieldDomeImageEffect.cs:177-190`), and the result is stored back (`ShieldPatches.cs:149-152`). The pool stays bounded and is reused.
- **C23** (`ConfigWatcher` drops a reload that throws): an IOException mid-reload means a write is still in progress, and its Changed event reschedules the reload (`ConfigWatcher.cs:68`, `174-177`). The plugins' own readers already catch IOException.
- **C29** (SilverLining recipe cache carries over between worlds): `GetRecipe` is only reached while the local player is at a forge in game (`SilverLiningPatches.cs:23`), so no temporary nulls get cached. Stale references are re-checked at `SilverLining.cs:127`, and nothing in the repo changes recipe costs.
- **C34** (`Village.Refresh` ignores its 2 s limit with no village loaded): each instance only costs a prefab-hash compare (`Village.cs:286`), and every caller runs at 1 Hz or less, so it's about 0.1-0.3 ms per second.
- **C37** (`VillageLife.EnemyNear` allocates every AI tick): about 200 B per call, only for the few vikings hunting or foraging outside the village, so about 4 KB/s each with trivial CPU cost.
- **C38** (cooking slot checks build strings every tick): a few µs and about 1 KB per tick, only while a viking cooks. Vanilla `CookingStation` uses the same `GetSlot` pattern itself.
- **C39** (VikingCalm weak-table lookup before the viking check): one ConditionalWeakTable lookup (about 80 ns) per owned AI tick. Entries are made once and held weakly.
- **C40** (armor helpers allocate per damage query): about 150 B per hit, or per frame only while a zombie viking is frosted (`SE_Frost.cs:36`).
- **C41** (zombie name prefix concatenates per call): one short string per zombie per frame, on top of vanilla's per-frame `Localize` of the same name (`EnemyHud.cs:212`). CreatureMarker compares names by value.
- **C42** (LightColor dome render patch allocates per frame): about 250 B and one `GetComponent` per generator per frame, only while a dome is on screen.
- **C45, partly** (VikingPoses culling and `CanLurch`'s `GetComponent`): `VikingPoses.LateUpdate` returns early unless a stretch or talk weight is above 0 (`VikingPoses.cs:943-946`). `RiseBlend` only exists during the 2.5 s rise, and spawned zombies set `risen` on their first frame. The ZombieLurch part is listed in section 4.
