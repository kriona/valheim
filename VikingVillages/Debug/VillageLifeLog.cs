using System.Collections.Generic;
using System.Text;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What the test log (see TestLog) watches in a viking's life on the client that owns it - leaving the village,
	/// ending up under the ground or in the water, being badly hurt, going hungry with no food in the chests, its gear
	/// breaking, the hits it took before it died - and why loot that despawned was left lying
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// How many of the viking's last hits are kept for its death line
		/// </summary>
		private const int LogHitsKept = 6;

		/// <summary>
		/// How far past the village's edge the viking has to go to be written as having left it, in meters
		/// </summary>
		private const float LogLeaveMargin = 5f;

		/// <summary>
		/// How far under the ground the viking has to be to be written as under it, in meters
		/// </summary>
		private const float LogUnderDepth = 1f;

		/// <summary>
		/// The share of its health the viking has to fall below to be written as badly hurt, and climb back to before it
		/// counts as recovered
		/// </summary>
		private const float LogLowShare = 0.3f;
		private const float LogRecoveredShare = 0.6f;

		/// <summary>
		/// Seconds between checks on whether the viking has food, which looks through the village's chests
		/// </summary>
		private const float LogFoodInterval = 10f;

		/// <summary>
		/// The fewest seconds between lines about the viking dropping its job to get off a roof or out of a pen (see
		/// LogEscape)
		/// </summary>
		private const float LogEscapeInterval = 30f;

		/// <summary>
		/// The viking's last hits, each with when it was taken, by Time.time
		/// </summary>
		private readonly List<KeyValuePair<float, string>> logHits = new List<KeyValuePair<float, string>>();

		/// <summary>
		/// What the viking was last written as - out of the village, under the ground, swimming, badly hurt, hungry with
		/// no food - and since when, and the farthest it has been out of the village this time
		/// </summary>
		private bool logOutside;
		private float logOutsideSince;
		private float logFarthest;
		private bool logUnder;
		private bool logSwimming;
		private float logSwimmingSince;
		private bool logLow;
		private bool logHungry;
		private float logHungrySince;
		private float logFoodCheckAt;

		/// <summary>
		/// When the viking may next be written as dropping its job to get off a roof or out of a pen, by Time.time
		/// </summary>
		private float logEscapeAt;

		/// <summary>
		/// Seconds between checks on whether the viking has got onto a roof, and how many seconds back a hit counts as
		/// having hurt it just now, for the line about it (see LogOnRoof)
		/// </summary>
		private const float LogRoofInterval = 1f;
		private const float LogRoofHurtTime = 10f;

		/// <summary>
		/// Whether the viking stood on a roof at the last check, and when it next checks
		/// </summary>
		private bool logOnRoof;
		private float logRoofCheckAt;

		/// <summary>
		/// The viking's equipped gear that was broken at the last check
		/// </summary>
		private HashSet<ItemDrop.ItemData> logBroken = new HashSet<ItemDrop.ItemData>();

		/// <summary>
		/// The job the viking was last counted as doing, so each one is counted once as it starts
		/// </summary>
		private Job logJob;

		/// <summary>
		/// Who runs the viking, as the test log last saw it, and since when
		/// </summary>
		private Runner logRunner = Runner.Unknown;
		private float logRunnerSince;

		/// <summary>
		/// Who runs a viking - this client, another one, or none, when it's out of every player's simulated area
		/// </summary>
		private enum Runner
		{
			Unknown,
			Here,
			Elsewhere,
			Nobody
		}

		/// <summary>
		/// Where the viking was at the last check, for the meters it walks, and whether that's known yet
		/// </summary>
		private Vector3 logLastPosition;
		private bool logPositioned;

		/// <summary>
		/// The farthest the viking can move between two checks and still count as walking, in meters - more is a
		/// teleport or a respawn
		/// </summary>
		private const float LogWalkMax = 30f;

		/// <summary>
		/// Loot about to despawn that the vikings could have put away, for the test log
		/// </summary>
		internal class LootLeft
		{
			/// <summary>
			/// The line for the log, why the vikings left it in a few words for the totals, its name and how many there
			/// were
			/// </summary>
			public string Report;
			public string Reason;
			public string Item;
			public int Stack;
		}

		/// <summary>
		/// What the viking carries back to the chests left over from crafting, so the totals tell it from loot it found
		/// </summary>
		private readonly HashSet<ItemDrop.ItemData> logLeftovers = new HashSet<ItemDrop.ItemData>();

		/// <summary>
		/// Notes an item the viking carries back to the chests left over from crafting, while the test log is on,
		/// forgetting what it no longer carries
		/// </summary>
		/// <param name="item">The item</param>
		private void LogCraftLeftover(ItemDrop.ItemData item)
		{
			if (!TestLog.On)
			{
				return;
			}
			Inventory inventory = humanoid.GetInventory();
			logLeftovers.RemoveWhere((ItemDrop.ItemData carried) => !inventory.ContainsItem(carried));
			logLeftovers.Add(item);
		}

		/// <summary>
		/// Checks whether loot the viking puts in a chest is left over from crafting, forgetting it
		/// </summary>
		/// <param name="loot">The loot</param>
		/// <returns>True when it is</returns>
		private bool LoggedCraftLeftover(ItemDrop.ItemData loot)
		{
			return(logLeftovers.Remove(loot));
		}

		/// <summary>
		/// Names the viking and where it is, for the log
		/// </summary>
		/// <returns>The name and position</returns>
		public string LogName()
		{
			return(TestLog.Name(humanoid));
		}

		/// <summary>
		/// Forgets what the test log noted about the viking, as a run starts, so the run counts its job and its troubles
		/// from now
		/// </summary>
		public void LogReset()
		{
			logHits.Clear();
			logOutside = false;
			logUnder = false;
			logSwimming = false;
			logLow = false;
			logHungry = false;
			logFoodCheckAt = 0f;
			logBroken = new HashSet<ItemDrop.ItemData>();
			logJob = null;
			logPositioned = false;
			logRunner = Runner.Unknown;
		}

		/// <summary>
		/// Writes the viking changing hands - stopping being run when it's out of every player's simulated area, where
		/// it stands frozen, being run by another client, or being run here again - with where it stands against the
		/// area the game runs around the player
		/// </summary>
		public void LogRunner()
		{
			if (nview == null || !nview.IsValid() || humanoid == null)
			{
				return;
			}
			ZDO zdo = nview.GetZDO();
			Runner runner = (nview.IsOwner() ? Runner.Here : (zdo.HasOwner() ? Runner.Elsewhere : Runner.Nobody));
			if (runner == logRunner)
			{
				return;
			}
			Runner was = logRunner;
			float minutes = (Time.time - logRunnerSince) / 60f;
			logRunner = runner;
			logRunnerSince = Time.time;
			if (was == Runner.Unknown)
			{
				return;
			}
			if (was == Runner.Nobody)
			{
				TestLog.Count("Minutes", "frozen, run by nobody", minutes);
			}
			string area = TestLog.DescribeSimulatedArea(transform.position);
			if (runner == Runner.Nobody)
			{
				TestLog.Count("Troubles", "frozen, run by nobody");
				Plugin.Log.LogWarning("Frozen: " + LogName() + " is run by nobody, out of every player's simulated area - " + area + " - " + LogDoing());
			}
			else if (runner == Runner.Elsewhere)
			{
				Plugin.Log.LogInfo("Runner: " + LogName() + " is run by another client now - " + area);
			}
			else
			{
				Plugin.Log.LogInfo("Runner: " + LogName() + " is run by this client again, after " + (minutes * 60f).ToString("F0") + "s " + ((was == Runner.Nobody) ? "frozen" : "run by another client") + " - " + area);
			}
		}

		/// <summary>
		/// Names the viking's job for the totals, by its kind
		/// </summary>
		/// <returns>The kind, like "Fuel", or "no job"</returns>
		public string LogJobName()
		{
			return(JobName(job));
		}

		/// <summary>
		/// Names a kind of job for the totals - its class without "Job" at the end
		/// </summary>
		/// <param name="kind">The job</param>
		/// <returns>The kind, like "Fuel", or "no job"</returns>
		private static string JobName(Job kind)
		{
			if (kind == null)
			{
				return("no job");
			}
			string name = kind.GetType().Name;
			return(name.EndsWith("Job") ? name.Substring(0, name.Length - 3) : name);
		}

		/// <summary>
		/// Counts the viking's job as started the first time it carries it on
		/// </summary>
		private void LogJobTick()
		{
			if (job == logJob || !TestLog.On)
			{
				return;
			}
			logJob = job;
			TestLog.Count("Jobs started", JobName(job));
		}

		/// <summary>
		/// Counts a job ending, as finished or as given up or interrupted before it was, and the time spent on it
		/// </summary>
		/// <param name="ending">The job</param>
		/// <param name="done">Whether it finished</param>
		private void LogJobStopped(Job ending, bool done)
		{
			if (!TestLog.On)
			{
				return;
			}
			string name = JobName(ending);
			TestLog.Count(done ? "Jobs finished" : "Jobs stopped before finishing", name);
			TestLog.Count("Minutes spent on jobs", name, (Time.time - ending.StartedAt) / 60f);
		}

		/// <summary>
		/// Counts the viking's job as interrupted, by the job and why, as it's interrupted (see Interrupt)
		/// </summary>
		/// <param name="why">Why it's interrupted, like "a pen"</param>
		private void LogInterrupt(string why)
		{
			if (job == null || !TestLog.On)
			{
				return;
			}
			TestLog.Count("Jobs interrupted, by why", JobName(job) + " - " + why);
		}

		/// <summary>
		/// Writes the viking dropping its job to get off a roof or out of a pen, just before it does - once every
		/// LogEscapeInterval at most, and only when it has a job to drop
		/// </summary>
		/// <param name="what">What it gets off or out of, like "out of a pen"</param>
		/// <param name="toward">Where it walks to get there</param>
		private void LogEscape(string what, Vector3 toward)
		{
			if (job == null || !(TestLog.On || Player.m_debugMode) || Time.time < logEscapeAt)
			{
				return;
			}
			logEscapeAt = Time.time + LogEscapeInterval;
			float below = transform.position.y - toward.y;
			Plugin.Log.LogInfo("Escape: " + TestLog.Name(humanoid, "F1") + " drops its job: " + job.Describe() + ", for " + (Time.time - job.StartedAt).ToString("F0") + "s, to get " + what + ", walking to " + toward.ToString("F1") + ", " + Utils.DistanceXZ(transform.position, toward).ToString("F1") + "m away and " + Mathf.Abs(below).ToString("F1") + "m " + ((below >= 0f) ? "lower" : "higher"));
		}

		/// <summary>
		/// Writes the viking getting onto a roof, checked every LogRoofInterval while the test log or debug mode is on -
		/// once each time it newly stands on one, with its job, what moved it there, its target, whether it's alerted and
		/// whether it was hurt in the last LogRoofHurtTime seconds - and counts it by whether it was fighting, alerted,
		/// hurt or calm, and what moved it
		/// </summary>
		/// <param name="moved">What moved the viking this AI update</param>
		private void LogOnRoof(string moved)
		{
			if (!(TestLog.On || Player.m_debugMode) || Time.time < logRoofCheckAt || ai == null)
			{
				return;
			}
			logRoofCheckAt = Time.time + LogRoofInterval;
			bool onRoof = (attachPoint == null && NavmeshRoofs.OnRoof(transform.position));
			if (onRoof == logOnRoof)
			{
				return;
			}
			logOnRoof = onRoof;
			if (!onRoof)
			{
				return;
			}
			Character target = ai.GetTargetCreature();
			bool alerted = ai.IsAlerted();
			bool hurt = (ai.m_timeSinceHurt < LogRoofHurtTime);
			string state = ((target != null) ? "fighting" : (alerted ? "alerted" : (hurt ? "hurt" : "calm")));
			TestLog.Count("Got onto a roof, by how", state + ", " + moved);
			Plugin.Log.LogInfo("Roof: " + TestLog.Name(humanoid, "F1") + " got onto " + RoofPiece(transform.position) + " - job: " + ((job != null) ? job.Describe() : "none") + ", moved by " + moved + ", target: " + ((target != null) ? TestLog.Label(target) + " at " + target.transform.position.ToString("F0") : "none") + ", alerted " + alerted + ", " + (hurt ? "hurt " + ai.m_timeSinceHurt.ToString("F0") + "s ago" : "not hurt in the last " + LogRoofHurtTime.ToString("F0") + "s"));
		}

		/// <summary>
		/// Names the roof piece under a point, with where it stands, for the log
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>The piece, like "wood_roof at (5280.0, 33.5, 1642.0)", or "a roof" when none is found</returns>
		private static string RoofPiece(Vector3 point)
		{
			WearNTear roof = NavmeshRoofs.RoofUnder(point);
			return((roof != null) ? Utils.GetPrefabName(roof.gameObject) + " at " + roof.transform.position.ToString("F1") : "a roof");
		}

		/// <summary>
		/// Notes a hit the viking took, keeping the last LogHitsKept
		/// </summary>
		/// <param name="line">What hit it and how much it took</param>
		public void NoteHit(string line)
		{
			logHits.Add(new KeyValuePair<float, string>(Time.time, line));
			if (logHits.Count > LogHitsKept)
			{
				logHits.RemoveAt(0);
			}
		}

		/// <summary>
		/// Checks the viking for what the test log writes as it starts and ends - leaving the village, being under the
		/// ground, swimming, being badly hurt, its equipped gear breaking, and having eaten nothing with no food in the
		/// chests - on the client that runs its life
		/// </summary>
		public void LogCheck()
		{
			if (!active || village == null || humanoid == null || nview == null || !nview.IsValid() || !nview.IsOwner() || humanoid.IsDead())
			{
				return;
			}
			Vector3 position = transform.position;
			if (logPositioned)
			{
				float walked = Utils.DistanceXZ(position, logLastPosition);
				if (walked <= LogWalkMax)
				{
					TestLog.Count("Meters walked, by job", LogJobName(), walked);
				}
			}
			logLastPosition = position;
			logPositioned = true;
			string farm = village.Farm.ToString("F0");
			float outside = village.DistanceOutside(position);
			if (!logOutside && outside > LogLeaveMargin)
			{
				logOutside = true;
				logOutsideSince = Time.time;
				logFarthest = outside;
				TestLog.Count("Troubles", "left the village");
				TestLog.Count("Left the village, by job", LogJobName());
				Plugin.Log.LogInfo("Left: " + LogName() + " left the village at " + farm + " - " + LogDoing());
			}
			else if (logOutside)
			{
				logFarthest = Mathf.Max(logFarthest, outside);
				if (outside <= 0f)
				{
					logOutside = false;
					TestLog.Count("Minutes", "outside the village", (Time.time - logOutsideSince) / 60f);
					Plugin.Log.LogInfo("Back: " + LogName() + " is back in the village at " + farm + " after " + (Time.time - logOutsideSince).ToString("F0") + "s, " + logFarthest.ToString("F0") + "m out at the farthest - " + LogDoing());
				}
			}
			if (ZoneSystem.instance != null)
			{
				float depth = ZoneSystem.instance.GetGroundHeight(position) - position.y;
				bool under = (depth > LogUnderDepth);
				if (under != logUnder)
				{
					logUnder = under;
					if (under)
					{
						TestLog.Count("Troubles", "under the ground");
						Plugin.Log.LogWarning("Under: " + LogName() + " is " + depth.ToString("F1") + "m under the ground - " + LogDoing());
					}
					else
					{
						Plugin.Log.LogInfo("Under: " + LogName() + " is back above the ground");
					}
				}
			}
			bool swimming = humanoid.IsSwimming();
			if (swimming != logSwimming)
			{
				logSwimming = swimming;
				if (swimming)
				{
					logSwimmingSince = Time.time;
					TestLog.Count("Troubles", "swimming");
					Plugin.Log.LogInfo("Swim: " + LogName() + " is swimming, " + outside.ToString("F0") + "m outside the village - " + LogDoing());
				}
				else
				{
					TestLog.Count("Minutes", "swimming", (Time.time - logSwimmingSince) / 60f);
					Plugin.Log.LogInfo("Swim: " + LogName() + " is out of the water after " + (Time.time - logSwimmingSince).ToString("F0") + "s");
				}
			}
			float max = humanoid.GetMaxHealth();
			float share = ((max > 0f) ? humanoid.GetHealth() / max : 1f);
			string health = humanoid.GetHealth().ToString("F0") + "/" + max.ToString("F0");
			if (!logLow && share < LogLowShare)
			{
				logLow = true;
				TestLog.Count("Troubles", "badly hurt");
				HitData hit = humanoid.m_lastHit;
				Plugin.Log.LogWarning("Hurt: " + LogName() + " is down to " + health + " health, " + ((hit != null) ? "last hit by " + TestLog.Source(hit) : "no hit noted") + " - " + LogDoing());
			}
			else if (logLow && share >= LogRecoveredShare)
			{
				logLow = false;
				Plugin.Log.LogInfo("Hurt: " + LogName() + " is back up to " + health + " health");
			}
			LogBrokenGear();
			LogHunger();
		}

		/// <summary>
		/// Writes the viking's equipped gear that broke since the last check
		/// </summary>
		private void LogBrokenGear()
		{
			HashSet<ItemDrop.ItemData> broken = new HashSet<ItemDrop.ItemData>();
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetEquippedItems())
			{
				if (!item.m_shared.m_useDurability || item.m_durability > 0f)
				{
					continue;
				}
				broken.Add(item);
				if (!logBroken.Contains(item))
				{
					TestLog.Count("Troubles", "gear broken");
					TestLog.Count("Gear broken", TestLog.ItemName(item));
					Plugin.Log.LogWarning("Broke: " + LogName() + " has broken its " + Localization.instance.Localize(item.m_shared.m_name) + " - " + LogDoing());
				}
			}
			logBroken = broken;
		}

		/// <summary>
		/// Writes the viking having eaten nothing with no food in the village's chests it can eat, and having food again,
		/// every LogFoodInterval seconds
		/// </summary>
		private void LogHunger()
		{
			if (Time.time < logFoodCheckAt)
			{
				return;
			}
			logFoodCheckAt = Time.time + LogFoodInterval;
			bool hungry = (body.Food.FoodHealth() <= 0f && FindFoodChest(false) == null);
			if (hungry == logHungry)
			{
				return;
			}
			logHungry = hungry;
			if (hungry)
			{
				logHungrySince = Time.time;
				TestLog.Count("Troubles", "hungry with no food");
				Plugin.Log.LogWarning("Hungry: " + LogName() + " has eaten nothing, and none of the " + UsableChests().Count + " chests it can use holds food it can eat" + LeftFood() + " - " + LogDoing());
			}
			else
			{
				TestLog.Count("Minutes", "hungry with no food", (Time.time - logHungrySince) / 60f);
				Plugin.Log.LogInfo("Hungry: " + LogName() + " has food again after " + (Time.time - logHungrySince).ToString("F0") + "s - eaten: " + body.Food.Describe());
			}
		}

		/// <summary>
		/// Describes the village's chests holding food the viking can eat now that it leaves out of the chests it uses
		/// (see Usable) - left alone after it couldn't get to them, held by a player, or open for another viking - with
		/// the nearest of them, counting each kind for the test log
		/// </summary>
		/// <returns>The description, starting " - ", or an empty string when there are none</returns>
		private string LeftFood()
		{
			int avoidedChests = 0;
			int heldChests = 0;
			int openChests = 0;
			Container nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Container chest in LoadedChests())
			{
				if (chest == null || !IsVillageChest(village, chest) || Usable(chest) || FoodIn(chest, false) == null)
				{
					continue;
				}
				if (Avoided(chest.m_nview.GetZDO().m_uid))
				{
					avoidedChests++;
					TestLog.Count("Food left alone while hungry", "couldn't get to it");
				}
				else if (HeldByPlayer(chest))
				{
					heldChests++;
					TestLog.Count("Food left alone while hungry", "held by a player");
				}
				else
				{
					openChests++;
					TestLog.Count("Food left alone while hungry", "open for another viking");
				}
				float distance = Vector3.Distance(chest.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearestDistance = distance;
					nearest = chest;
				}
			}
			return(DescribeLeftFood(avoidedChests, heldChests, openChests, ((nearest != null) ? nearest.transform.position.ToString("F0") : "")));
		}

		/// <summary>
		/// Describes the chests holding food a hungry viking can eat that it leaves alone, for the log (see LeftFood)
		/// </summary>
		/// <param name="avoidedChests">How many it couldn't get to lately</param>
		/// <param name="heldChests">How many a player holds</param>
		/// <param name="openChests">How many are open for another viking</param>
		/// <param name="nearest">Where the nearest of them stands</param>
		/// <returns>The description, starting " - ", or an empty string when there are none</returns>
		internal static string DescribeLeftFood(int avoidedChests, int heldChests, int openChests, string nearest)
		{
			int total = avoidedChests + heldChests + openChests;
			if (total == 0)
			{
				return("");
			}
			List<string> kinds = new List<string>();
			if (avoidedChests > 0)
			{
				kinds.Add(avoidedChests + " it couldn't get to lately");
			}
			if (heldChests > 0)
			{
				kinds.Add(heldChests + " held by a player");
			}
			if (openChests > 0)
			{
				kinds.Add(openChests + " open for another viking");
			}
			return(" - " + total + " more " + ((total == 1) ? "does" : "do") + ", which it leaves alone (" + string.Join(", ", kinds) + "), " + ((total == 1) ? "at " : "the nearest at ") + nearest);
		}

		/// <summary>
		/// Describes what the viking is doing, for the log - its job, what it's fighting, the bed or seat it's on and
		/// where it's held there, and whether it's fleeing, walking back, in a raid or out at night
		/// </summary>
		/// <returns>The description</returns>
		private string LogDoing()
		{
			StringBuilder text = new StringBuilder();
			text.Append("job: " + ((job != null) ? job.Describe() : "none"));
			Character target = ((ai != null) ? ai.m_targetCreature : null);
			if (target != null)
			{
				text.Append(", fighting " + TestLog.Name(target));
			}
			if (attachPoint != null)
			{
				Piece seat = attachPoint.GetComponentInParent<Piece>();
				string what = ((seat != null) ? Utils.GetPrefabName(seat.gameObject) + " at " + seat.transform.position.ToString("F1") : "something");
				text.Append(", " + (inBed ? "in bed on " : "seated on ") + what + ", held at " + attachPoint.position.ToString("F1"));
			}
			if (fleeing)
			{
				text.Append(", fleeing");
			}
			if (returning)
			{
				text.Append(", walking back to the village");
			}
			if (VillageRaids.On(village))
			{
				text.Append(", raid on");
			}
			if (IsNight())
			{
				text.Append(", at night");
			}
			return(text.ToString());
		}

		/// <summary>
		/// Describes how the viking was doing as it died, for its death line - what it was doing, where it was, what it
		/// had eaten, how rested it was, and its last hits
		/// </summary>
		/// <returns>The description, starting with "; ", or "" when its life doesn't run here</returns>
		public string DeathReport()
		{
			if (!active || village == null)
			{
				return("");
			}
			StringBuilder text = new StringBuilder();
			float outside = village.DistanceOutside(transform.position);
			text.Append("; " + LogDoing());
			text.Append("; " + ((outside > 0f) ? outside.ToString("F0") + "m outside" : "inside") + " the village at " + village.Farm.ToString("F0"));
			text.Append("; max health " + humanoid.GetMaxHealth().ToString("F0") + ", eaten: " + body.Food.Describe() + ", rest: " + body.Rest.Describe());
			text.Append("; gear: " + LogGear());
			if (logHits.Count > 0)
			{
				List<string> hits = new List<string>();
				foreach (KeyValuePair<float, string> hit in logHits)
				{
					hits.Add((Time.time - hit.Key).ToString("F1") + "s before: " + hit.Value);
				}
				text.Append("; last hits, oldest first: " + string.Join(" / ", hits));
			}
			return(text.ToString());
		}

		/// <summary>
		/// Describes the viking for the snapshot - its debug summary and its gear
		/// </summary>
		/// <returns>The line</returns>
		public string LogSnapshot()
		{
			if (viking == null)
			{
				return("no viking core");
			}
			if (nview == null || !nview.IsValid())
			{
				return("not loaded");
			}
			if (!nview.IsOwner())
			{
				string area = TestLog.DescribeSimulatedArea(transform.position);
				return((nview.GetZDO().HasOwner() ? "run by another client" : "run by nobody, frozen out of every player's simulated area") + " - " + area);
			}
			return(TestLog.Flatten(viking.DebugSummary()) + " | Gear: " + LogGear());
		}

		/// <summary>
		/// Describes the viking's equipped gear, with how worn each piece is, and its arrows and inventory
		/// </summary>
		/// <returns>The description</returns>
		private string LogGear()
		{
			Inventory inventory = humanoid.GetInventory();
			List<string> gear = new List<string>();
			foreach (ItemDrop.ItemData item in inventory.GetEquippedItems())
			{
				string worn = (item.m_shared.m_useDurability ? " " + Mathf.RoundToInt(item.GetDurabilityPercentage() * 100f) + "%" : "");
				gear.Add(Localization.instance.Localize(item.m_shared.m_name) + " q" + item.m_quality + worn);
			}
			int arrows = 0;
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
				if ((type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable) && !VikingItems.IsCarried(item))
				{
					arrows += item.m_stack;
				}
			}
			string equipped = ((gear.Count > 0) ? string.Join(", ", gear) : "nothing equipped");
			return(equipped + ", " + arrows + " ammo, " + inventory.GetAllItems().Count + "/" + (inventory.GetWidth() * inventory.GetHeight()) + " slots used");
		}

		/// <summary>
		/// Describes the items lying in and around a village for the snapshot - how many, how many of them are loot the
		/// vikings would put away, and how long the oldest of that has lain
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The line, ending in a line break</returns>
		public static string DescribeLyingLoot(Village village)
		{
			int lying = 0;
			int loot = 0;
			double oldest = 0.0;
			foreach (ItemDrop drop in LoadedDrops())
			{
				ZNetView view = ((drop != null) ? drop.m_nview : null);
				if (view == null || !view.IsValid() || village.DistanceOutside(view.transform.position) > LootReach)
				{
					continue;
				}
				lying++;
				if (IsLoot(view))
				{
					loot++;
					oldest = System.Math.Max(oldest, drop.GetTimeSinceSpawned());
				}
			}
			return("Items lying within " + LootReach.ToString("F0") + "m: " + lying + ", " + loot + " of them loot" + ((loot > 0) ? ", the oldest lying " + (oldest / 60.0).ToString("F0") + " minutes" : "") + "\n");
		}

		/// <summary>
		/// Describes loot about to despawn that the vikings of a village could have put away - lying within LootReach of
		/// it - and why they left it
		/// </summary>
		/// <param name="drop">The item</param>
		/// <returns>The report, or null when it's not loot near a village</returns>
		public static LootLeft LootLeftReport(ItemDrop drop)
		{
			ZNetView view = drop.m_nview;
			if (view == null || !IsLoot(view))
			{
				return(null);
			}
			Vector3 position = view.transform.position;
			Village village = Village.Find(position);
			if (village == null)
			{
				return(null);
			}
			float outside = village.DistanceOutside(position);
			if (outside > LootReach)
			{
				return(null);
			}
			List<VillageLife> lives = new List<VillageLife>();
			foreach (VillageLife life in All)
			{
				if (life != null && life.active && life.village == village)
				{
					lives.Add(life);
				}
			}
			ItemDrop.ItemData item = drop.m_itemData;
			string name = TestLog.ItemName(item);
			string where = ((outside > 0f) ? outside.ToString("F0") + "m outside" : "inside") + " the village at " + village.Farm.ToString("F0");
			string why = LootLeftWhy(drop, lives, position, outside, out string reason);
			LootLeft left = new LootLeft();
			left.Report = name + " x" + item.m_stack + " at " + position.ToString("F0") + ", " + where + ", after lying " + (drop.GetTimeSinceSpawned() / 60.0).ToString("F0") + " minutes - " + why;
			left.Reason = reason;
			left.Item = name;
			left.Stack = item.m_stack;
			return(left);
		}

		/// <summary>
		/// Works out why a village's vikings left loot lying, the way FindLoot judges it
		/// </summary>
		/// <param name="drop">The loot</param>
		/// <param name="lives">The village's vikings whose lives run on this client</param>
		/// <param name="position">Where the loot lies</param>
		/// <param name="outside">How far past the village's edge it lies, in meters</param>
		/// <param name="reason">Set to why in a few words, the same for every item left for the same reason</param>
		/// <returns>Why, with the vikings it's about</returns>
		private static string LootLeftWhy(ItemDrop drop, List<VillageLife> lives, Vector3 position, float outside, out string reason)
		{
			if (lives.Count == 0)
			{
				reason = "no viking of the village here";
				return("none of the village's vikings run on this client");
			}
			ItemDrop.ItemData item = drop.m_itemData;
			List<Container> chests = lives[0].UsableChests();
			if (chests.Count == 0)
			{
				reason = "no usable chests";
				return("the village has no chests its vikings can use");
			}
			bool mayLeave = lives.Exists((VillageLife life) => life.MayLeave());
			if (outside > 0f && !mayLeave)
			{
				reason = "outside, none may leave";
				return("it lies outside the village while no viking may leave it");
			}
			if (!WantsLoot(item, chests, mayLeave))
			{
				reason = "no chest room";
				return("no chest has room for it");
			}
			if (!lives.Exists((VillageLife life) => life.CarryRoom(item, CarryLoot) > 0))
			{
				reason = "no carrying room";
				return("no viking has room to carry it");
			}
			ZDOID id = drop.m_nview.GetZDO().m_uid;
			int avoiding = lives.FindAll((VillageLife life) => life.Avoided(id)).Count;
			if (avoiding > 0)
			{
				reason = "unreachable";
				return(avoiding + " of " + lives.Count + " vikings gave up on reaching it");
			}
			if (workClaims.TryGetValue(id, out VillageLife holder) && holder != null && holder.job != null)
			{
				reason = "claimed but not taken";
				return(holder.LogName() + " has claimed it - " + holder.LogDoing());
			}
			if (outside > 0f && lives[0].DangerNear(position))
			{
				reason = "monster near it";
				return("it lies outside the village with a monster near it");
			}
			List<string> doing = new List<string>();
			foreach (VillageLife life in lives)
			{
				doing.Add(life.LogName() + " (" + ((life.job != null) ? life.job.Describe() : "no job") + ")");
			}
			reason = "vikings busy";
			return("the vikings would take it but never got to it - " + string.Join(", ", doing));
		}
	}
}
