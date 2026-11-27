using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Tough monsters and poison blobs, which a viking keeps out of reach of while its fellows gather to charge them
	/// together, or lures while they're shot - and when it takes its bow to an enemy instead
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// How many of its fellows already fighting an enemy hand to hand make it too crowded for another to get at it,
		/// and how close to the enemy, past its size, they count as at it, in meters
		/// </summary>
		private const int CrowdCount = 3;
		private const float CrowdReach = 2.5f;

		/// <summary>
		/// Checks whether the viking would rather shoot an enemy than fight it hand to hand - one it can't reach, out in
		/// deep water, flying or with no way to it, one too many of its fellows are already at, or a tough monster or
		/// poison blob it keeps away from (see Kites)
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it would</returns>
		public bool WantsBow(Character enemy)
		{
			if (enemy == null)
			{
				return(false);
			}
			if (InDeepWater(enemy) || enemy.IsFlying() || Kites(enemy))
			{
				return(true);
			}
			return(Crowded(enemy) || (ai != null && !ai.HavePath(enemy.transform.position)));
		}

		/// <summary>
		/// Checks whether CrowdCount of the viking's fellows are already at an enemy, while it isn't one of them
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when they are</returns>
		private bool Crowded(Character enemy)
		{
			float reach = enemy.GetRadius() + CrowdReach;
			if (Utils.DistanceXZ(enemy.transform.position, transform.position) <= reach)
			{
				return(false);
			}
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(enemy.transform.position, reach, near);
			int at = 0;
			foreach (Character other in near)
			{
				if (other != humanoid && !other.IsDead() && VikingCore.IsLivingViking(other) && Utils.DistanceXZ(other.transform.position, enemy.transform.position) <= reach)
				{
					at++;
				}
			}
			return(at >= CrowdCount);
		}

		/// <summary>
		/// How far past a tough monster's reach the viking it's after lures it - backing away once the monster is within
		/// LureNear, and standing its ground again once it's past LureFar - in meters
		/// </summary>
		private const float LureNear = 1.5f;
		private const float LureFar = 4f;

		/// <summary>
		/// How far past a tough monster's reach the rest keep while they shoot it, how much farther from it than the
		/// viking luring it, so it stays after the lure, which it takes as the nearest, and the farthest out that keeps
		/// them, so they're still in bowshot, in meters
		/// </summary>
		private const float SupportKeep = 8f;
		private const float SupportBehindLure = 3f;
		private const float SupportKeepMax = 22f;

		/// <summary>
		/// How far each step a viking keeping away from a tough monster takes, in meters, how far it turns off straight
		/// away when it's cornered, in degrees, and how far from a tough monster a fellow who can shoot counts as there
		/// to shoot it, in meters
		/// </summary>
		private const float KiteStep = 4f;
		private const float KiteTurn = 70f;
		private const float ShooterRange = 30f;

		/// <summary>
		/// How often a monster the viking fights is weighed up again for whether it kites it, in seconds
		/// </summary>
		private const float ToughCheckInterval = 2f;

		/// <summary>
		/// The monster the viking last weighed up, whether it kites it, and when that was, in Time.time seconds
		/// </summary>
		private Character toughChecked;
		private bool toughResult;
		private float toughCheckedAt;

		/// <summary>
		/// Whether the viking is backing away from a tough monster, and how far off straight away it backs, in degrees
		/// </summary>
		private bool kiteBacking;
		private float kiteTurn;

		/// <summary>
		/// Whether the viking keeps clear of the tough monster it last weighed up while its fellows gather against it
		/// (see KeepsClear)
		/// </summary>
		private bool gathering;

		/// <summary>
		/// How far past a tough monster's reach a fellow counts as gathered against it, in meters - as far out as the
		/// vikings keeping clear of it stand, and a little more
		/// </summary>
		private const float GatherReach = SupportKeepMax + LureFar;

		/// <summary>
		/// Checks whether the viking keeps away from a monster rather than fight it hand to hand - it's a poison blob the
		/// viking doesn't resist and someone near can shoot it (see KitesBlob), or it's tough for the viking (see
		/// VikingCore.IsTough) and it isn't charging it with its fellows (see KeepsClear). A hunting party's prey is
		/// kited only by the one it's after (see HuntParty)
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>True when it does</returns>
		private bool Kites(Character monster)
		{
			if (monster == null || viking == null || MonsterThreat.IsRoot(monster) || monster.IsDead())
			{
				return(false);
			}
			// A hunting party's prey is kited by whichever of them it's after, while the rest fight it (see HuntParty)
			if (job is HuntJob hunt && hunt.PartyFights(monster))
			{
				BaseAI hunted = monster.GetBaseAI();
				return(hunted != null && hunted.GetTargetCreature() == humanoid);
			}
			if (monster != toughChecked || Time.time - toughCheckedAt >= ToughCheckInterval)
			{
				toughChecked = monster;
				toughCheckedAt = Time.time;
				if (KitesBlob(monster))
				{
					gathering = false;
					toughResult = true;
				}
				else
				{
					toughResult = (Tough(monster) && KeepsClear(monster));
				}
			}
			return(toughResult);
		}

		/// <summary>
		/// Checks whether the viking kites a blob - one whose attacks poison (see VikingPowers.ElementsOf), when nothing
		/// it wears or has taken resists poison and it or a fellow near the blob can shoot it (see KitesBlob(bool, bool,
		/// HitData.DamageModifier, bool))
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>True when it kites it</returns>
		private bool KitesBlob(Character monster)
		{
			bool blob = IsBlob(Utils.GetPrefabName(monster.gameObject));
			bool poisons = (blob && (VikingPowers.ElementsOf(monster) & VikingPowers.Elements.Poison) != 0);
			HitData.DamageModifier poison = humanoid.GetDamageModifiers().GetModifier(HitData.DamageType.Poison);
			return(KitesBlob(blob, poisons, poison, (blob && poisons && ShootersNear(monster))));
		}

		/// <summary>
		/// Checks whether a viking kites a blob rather than fight it hand to hand - one whose attacks poison, when it
		/// doesn't resist poison and someone can shoot the blob. With no one to shoot it, the viking fights it anyway
		/// </summary>
		/// <param name="blob">Whether the monster is a blob</param>
		/// <param name="poisons">Whether its attacks poison</param>
		/// <param name="poison">How the viking takes poison damage</param>
		/// <param name="shooters">Whether the viking or a fellow near the blob can shoot it</param>
		/// <returns>True when it kites it</returns>
		internal static bool KitesBlob(bool blob, bool poisons, HitData.DamageModifier poison, bool shooters)
		{
			return(blob && poisons && shooters && !ResistsPoison(poison));
		}

		/// <summary>
		/// Checks whether a creature's prefab is a blob - a blob, oozer, growth or any other of the game's blobs
		/// </summary>
		/// <param name="prefab">The prefab name</param>
		/// <returns>True when it is</returns>
		internal static bool IsBlob(string prefab)
		{
			return(prefab != null && prefab.StartsWith("Blob", System.StringComparison.Ordinal));
		}

		/// <summary>
		/// Checks whether a poison damage modifier resists poison at all, or shrugs it off
		/// </summary>
		/// <param name="poison">The modifier</param>
		/// <returns>True when it does</returns>
		internal static bool ResistsPoison(HitData.DamageModifier poison)
		{
			return(poison == HitData.DamageModifier.SlightlyResistant || poison == HitData.DamageModifier.Resistant || poison == HitData.DamageModifier.VeryResistant || poison == HitData.DamageModifier.Immune || poison == HitData.DamageModifier.Ignore);
		}

		/// <summary>
		/// Weighs up who's with the viking against a tough monster - how many of its village's vikings it takes to fight
		/// it hand to hand together (see VikingCore.FightersNeeded), how many have gathered within GatherReach past its
		/// reach, and how many more are within DefendRange of the viking, where its shouts for help call them in - and
		/// checks whether it keeps clear of the monster for now (see KeepsClear(int, int, int, bool)). A fellow below
		/// BadlyHurtShare of its health hangs back, so it isn't counted, and a viking with no village counts only itself
		/// </summary>
		/// <param name="monster">The tough monster</param>
		/// <returns>True when it keeps clear</returns>
		private bool KeepsClear(Character monster)
		{
			int needed = viking.FightersNeeded(monster);
			float reach = MonsterThreat.Reach(monster) + GatherReach;
			int gathered = 0;
			int called = 0;
			List<Character> fellows = ((village != null) ? village.Vikings() : new List<Character> { humanoid });
			foreach (Character fellow in fellows)
			{
				if (fellow != humanoid && fellow.GetHealthPercentage() < BadlyHurtShare)
				{
					continue;
				}
				bool near = (Utils.DistanceXZ(fellow.transform.position, monster.transform.position) <= reach);
				if (near)
				{
					gathered++;
				}
				if (near || Vector3.Distance(fellow.transform.position, transform.position) <= DefendRange)
				{
					called++;
				}
			}
			gathering = (gathered < needed && called >= needed);
			bool shooters = (gathered < needed && !gathering && ShootersNear(monster));
			if (TestLog.On || Player.m_debugMode)
			{
				LogTough(monster, needed, gathered, called, shooters);
			}
			return(KeepsClear(needed, gathered, called, shooters));
		}

		/// <summary>
		/// The tough monster the viking last wrote what it does about, and what that was (see ToughDecision)
		/// </summary>
		private Character logToughMonster;
		private string logToughDecision;

		/// <summary>
		/// Writes what the viking does about a tough monster when it changes - which monster, how tough it weighs it up
		/// as and why (see VikingCore.FightersNeeded), who's with it, and whether it charges, keeps clear or fights it
		/// anyway - counted by what it does
		/// </summary>
		/// <param name="monster">The tough monster</param>
		/// <param name="needed">How many vikings it takes to fight the monster hand to hand together</param>
		/// <param name="gathered">How many of them have gathered against it, the viking among them</param>
		/// <param name="called">How many are gathered or within call, the viking among them</param>
		/// <param name="shooters">Whether the viking or a fellow near the monster can shoot it, as KeepsClear weighs it</param>
		private void LogTough(Character monster, int needed, int gathered, int called, bool shooters)
		{
			string decision = ToughDecision(needed, gathered, called, shooters);
			if (monster == logToughMonster && decision == logToughDecision)
			{
				return;
			}
			logToughMonster = monster;
			logToughDecision = decision;
			int stars = monster.GetLevel() - 1;
			string starred = ((stars > 0) ? ", " + stars + ((stars == 1) ? " star" : " stars") : "");
			float hit = MonsterThreat.Hit(monster);
			float monsterArmor = monster.GetBodyArmor();
			float swing = viking.SwingDamage(monster.GetDamageModifiers());
			float armor = viking.BodyArmor();
			string theirs = "health " + monster.GetHealth().ToString("F0") + "/" + monster.GetMaxHealth().ToString("F0") + ", hitting for " + hit.ToString("F0") + " (" + HitData.DamageTypes.ApplyArmor(hit, armor).ToString("F0") + " through its armor of " + armor.ToString("F0") + "), armor " + monsterArmor.ToString("F0");
			string own = "its swing of " + swing.ToString("F0") + " (" + HitData.DamageTypes.ApplyArmor(swing, monsterArmor).ToString("F0") + " through the armor), health " + humanoid.GetHealth().ToString("F0") + "/" + humanoid.GetMaxHealth().ToString("F0");
			string with = "takes " + needed + ", " + gathered + " gathered, " + called + " within call, " + (shooters ? "someone to shoot it" : "no one counted on to shoot it");
			Plugin.Log.LogInfo("Tough: " + LogName() + " weighs " + TestLog.Name(monster) + starred + " - " + theirs + " - against " + own + " - " + with + " - so it " + decision);
			TestLog.Count("Tough monsters, by what the vikings did", decision);
		}

		/// <summary>
		/// Says what a viking does about a tough monster, the way KeepsClear(int, int, int, bool) decides it - charges it
		/// once enough have gathered, keeps clear while enough within call gather, or while it's shot, and otherwise
		/// fights it anyway
		/// </summary>
		/// <param name="needed">How many vikings it takes to fight the monster hand to hand together</param>
		/// <param name="gathered">How many of them have gathered against it, the viking among them</param>
		/// <param name="called">How many are gathered or within call, the viking among them</param>
		/// <param name="shooters">Whether the viking or a fellow near the monster can shoot it</param>
		/// <returns>What it does, in a few words</returns>
		internal static string ToughDecision(int needed, int gathered, int called, bool shooters)
		{
			if (gathered >= needed)
			{
				return("charges it together");
			}
			if (called >= needed)
			{
				return("keeps clear while the rest gather");
			}
			if (shooters)
			{
				return("keeps clear while it's shot");
			}
			return("fights it anyway, with too few and no one to shoot it");
		}

		/// <summary>
		/// Checks whether a viking keeps clear of a tough monster for now - not once enough of its fellows have gathered
		/// against it, when they all charge it together, but while enough are within call to gather, and otherwise
		/// while someone near can shoot it, so it can be brought down while it's kept busy. With too few to take it on
		/// and no one to shoot it, the viking fights it anyway
		/// </summary>
		/// <param name="needed">How many vikings it takes to fight the monster hand to hand together</param>
		/// <param name="gathered">How many of them have gathered against it, the viking among them</param>
		/// <param name="called">How many are gathered or within call, the viking among them</param>
		/// <param name="shooters">Whether the viking or a fellow near the monster can shoot it</param>
		/// <returns>True when it keeps clear</returns>
		internal static bool KeepsClear(int needed, int gathered, int called, bool shooters)
		{
			if (gathered >= needed)
			{
				return(false);
			}
			return(called >= needed || shooters);
		}

		/// <summary>
		/// Checks whether a monster is too tough for the viking to bring down alone (see VikingCore.IsTough), weighed
		/// again every ToughCheckInterval seconds
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>True when it is</returns>
		private bool Tough(Character monster)
		{
			if (monster == null || viking == null || MonsterThreat.IsRoot(monster) || monster.IsDead())
			{
				return(false);
			}
			if (monster != hardChecked || Time.time - hardCheckedAt >= ToughCheckInterval)
			{
				hardChecked = monster;
				hardCheckedAt = Time.time;
				hardResult = viking.IsTough(monster);
			}
			return(hardResult);
		}

		/// <summary>
		/// The monster last weighed for how tough it is for the viking, whether it was, and when
		/// </summary>
		private Character hardChecked;
		private bool hardResult;
		private float hardCheckedAt;

		/// <summary>
		/// Joins a fight the viking sees one of its fellows in - while it has no enemy of its own, isn't in bed, resting
		/// or badly hurt - every JoinCheckInterval seconds: the nearest enemy within DefendRange of it that a living viking
		/// within JoinRange it can see is fighting, the way it comes to a call for help (see OnDistress). Through a raid
		/// it's ready for, it's the nearest enemy any of its village's vikings is fighting where its fight reaches it (see
		/// Hears), seen or not. A fellow's prey isn't its enemy, so it leaves a hunt to the hunter
		/// </summary>
		/// <remarks>
		/// Only the vikings this client runs are known to be fighting, by the enemy their AI is after - those near it
		/// mostly are, since the clients near a place run what's there
		/// </remarks>
		private void TryJoinFight()
		{
			if (Time.time < nextJoinCheck || ai == null || humanoid == null)
			{
				return;
			}
			nextJoinCheck = Time.time + JoinCheckInterval;
			if (inBed || (job != null && job.Resting()) || Wounded())
			{
				return;
			}
			// Through a raid it joins any fellow fighting in the village, seen or not
			bool raid = RaidReady();
			List<Character> near = new List<Character>();
			if (raid)
			{
				near = village.Vikings();
			}
			else
			{
				Character.GetCharactersInRange(transform.position, JoinRange, near);
			}
			Character joined = null;
			float nearest = float.MaxValue;
			foreach (Character other in near)
			{
				if (other == humanoid || other.IsDead() || !VikingCore.IsLivingViking(other) || !other.m_nview.IsOwner())
				{
					continue;
				}
				Character enemy = ((other.GetBaseAI() is MonsterAI theirs) ? theirs.GetTargetCreature() : null);
				if (enemy == null || enemy.IsDead() || !BaseAI.IsEnemy(humanoid, enemy))
				{
					continue;
				}
				float distance = Vector3.Distance(enemy.transform.position, transform.position);
				bool reaches = (raid ? Hears(other.transform.position) : (distance <= DefendRange && ai.CanSeeTarget(other)));
				if (reaches && distance < nearest)
				{
					joined = enemy;
					nearest = distance;
				}
			}
			if (joined == null)
			{
				return;
			}
			Interrupt("joining a fight");
			grudge = joined;
			grudgeUntil = Time.time + GrudgeTime;
			Engage(joined);
		}

		/// <summary>
		/// How far a viking sees its fellows fighting from to join them, in meters, and how often it looks, in seconds
		/// </summary>
		private const float JoinRange = 20f;
		private const float JoinCheckInterval = 1f;

		/// <summary>
		/// When the viking next looks for a fight its fellows are in
		/// </summary>
		private float nextJoinCheck;

		/// <summary>
		/// Checks whether the viking or one of its fellows within ShooterRange of a monster can shoot it
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>True when one can</returns>
		private bool ShootersNear(Character monster)
		{
			if (HasBow(humanoid))
			{
				return(true);
			}
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(monster.transform.position, ShooterRange, near);
			return(near.Exists((Character other) => other != humanoid && !other.IsDead() && VikingCore.IsLivingViking(other) && HasBow(other)));
		}

		/// <summary>
		/// Kites a tough monster or poison blob (see Kites) - the viking it's after lures it, staying just out of its reach, past
		/// LureNear and backing off whenever it gets closer, while the rest keep SupportKeep past its reach and farther
		/// from it than the lure, so it stays after the lure, and shoot it. One who can't shoot holds its ground out of
		/// reach rather than walk in
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">The viking's target</param>
		/// <returns>True while it's moving or holding off, so the AI's own update is skipped</returns>
		private bool UpdateKite(float dt, Character target)
		{
			if (!Kites(target))
			{
				kiteBacking = false;
				kiteTurn = 0f;
				return(false);
			}
			BaseAI enemyAI = target.GetBaseAI();
			Character chasing = ((enemyAI != null) ? enemyAI.GetTargetCreature() : null);
			bool lure = (chasing == humanoid);
			float reach = MonsterThreat.Reach(target);
			float near = reach + LureNear;
			if (!lure)
			{
				float behindLure = ((chasing != null) ? Utils.DistanceXZ(chasing.transform.position, target.transform.position) + SupportBehindLure : 0f);
				near = Mathf.Max(reach + SupportKeep, Mathf.Min(behindLure, SupportKeepMax));
			}
			float far = near + (LureFar - LureNear);
			float distance = Utils.DistanceXZ(target.transform.position, transform.position);
			mover = (lure ? "luring " : (gathering ? "gathering against " : "keeping clear of ")) + target.GetHoverName();
			if (distance < near || (kiteBacking && distance < far))
			{
				kiteBacking = true;
				BackAway(dt, target);
				return(true);
			}
			kiteBacking = false;
			if (!HasBow(humanoid))
			{
				ai.StopMoving();
				ai.LookAt(target.GetCenterPoint());
				return(true);
			}
			return(false);
		}

		/// <summary>
		/// Backs the viking away from a monster at a run, turning off to the side when it's cornered and to the other
		/// side the next time
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="monster">The monster</param>
		private void BackAway(float dt, Character monster)
		{
			Vector3 away = transform.position - monster.transform.position;
			away.y = 0f;
			away = ((away.sqrMagnitude < 0.01f) ? -transform.forward : away.normalized);
			Vector3 heading = Quaternion.Euler(0f, kiteTurn, 0f) * away;
			Go(dt, transform.position + heading * KiteStep, 1f, true);
			if (moveFailed)
			{
				moveFailed = false;
				kiteTurn = ((kiteTurn > 0f) ? -KiteTurn : KiteTurn);
			}
		}
	}
}
