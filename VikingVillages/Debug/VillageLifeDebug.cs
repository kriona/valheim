using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a viking is doing, described for the console and the test log - its job, where it's going and why it
	/// left loot lying
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Describes the viking's village life for the debug hover text - its village and home, what it's doing, its
		/// bedtime at night once it's picked, what has it on edge, and the loot lying around the village
		/// </summary>
		/// <remarks>
		/// Its job is only known on the client that owns it, which runs its life
		/// </remarks>
		/// <returns>The lines, each ending in a line break</returns>
		public string DebugSummary()
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			if (!active)
			{
				text.Append("Village life: off" + ((village == null) ? ", no village" : "") + "\n");
				DescribeRoad(text);
				return(text.ToString());
			}
			ZDO zdo = nview.GetZDO();
			text.Append("Village: " + village.Farm.ToString("F0") + ", home " + zdo.GetVec3(Village.HomeKey, Vector3.zero).ToString("F0") + "\n");
			if (job != null)
			{
				text.Append("Job: " + job.Describe() + ", for " + (Time.time - job.StartedAt).ToString("F0") + "s\n");
			}
			else
			{
				text.Append("Job: none\n");
			}
			// Tonight's bedtime is picked the first time it's asked for at night (see Bedtime)
			if (IsNight() && SleepSkip.SleptThroughNight())
			{
				text.Append("Bedtime: none, the night was slept through\n");
			}
			else if (IsNight() && bedAt >= 0f)
			{
				float left = bedAt - Time.time;
				text.Append("Bedtime: " + ((left > 0f) ? "in " + left.ToString("F0") + "s" : "passed " + (-left).ToString("F0") + "s ago") + "\n");
			}
			if (Time.time - stillSince >= StillShowTime)
			{
				text.Append("Standing still for " + (Time.time - stillSince).ToString("F0") + "s" + (inBed ? ", in bed" : ((attachPoint != null) ? ", seated" : "")) + "\n");
			}
			text.Append(VillageBeds.DescribeFor(zdo, village));
			CountTwitches(out int starts, out int reversals);
			string lastMover = ((moveSamples.Count > 0) ? moveSamples.ToArray()[moveSamples.Count - 1].Mover : "nothing");
			text.Append("Moved by: " + lastMover + ((starts >= TwitchStarts || reversals >= TwitchReversals) ? ", twitching" : "") + " (" + starts + " starts, " + reversals + " turn-backs in " + TwitchWindow.ToString("F0") + "s)" + ((steeredAround != null && Time.time - steeredAt < SteeredShowTime) ? ", steering around " + steeredAround.GetHoverName() : "") + "\n");
			text.Append("Eaten: " + body.Food.Describe() + "\n");
			text.Append("Rest: " + body.Rest.Describe() + "\n");
			text.Append("Powers: " + body.Powers.Describe() + "\n");
			List<string> flags = new List<string>();
			if (IsWatch())
			{
				flags.Add("night watch");
			}
			if (waitingFor != ZDOID.None)
			{
				flags.Add("waiting for a corpse to rise");
			}
			if (RaidReady())
			{
				flags.Add("ready for a raid");
			}
			else if (VillageRaids.On(village))
			{
				flags.Add("raid on, not noticed yet");
			}
			if (returning)
			{
				flags.Add("walking back to the village");
			}
			if (fleeing)
			{
				flags.Add("running home (" + fleeReason.ToString().ToLowerInvariant() + ")");
			}
			if (hangingBack)
			{
				flags.Add("hanging back, badly hurt" + ((Time.time < runUntil) ? ", running from an enemy" : ((flanked != null) ? ", working behind " + flanked.GetHoverName() : "")));
			}
			if (gettingOffRoof)
			{
				flags.Add("getting off a roof");
			}
			if (dulled)
			{
				flags.Add("senses dulled");
			}
			if (Time.time < noWoodUntil)
			{
				flags.Add("no wood for " + Mathf.CeilToInt(noWoodUntil - Time.time) + "s");
			}
			if (grudge != null && Time.time <= grudgeUntil)
			{
				flags.Add("grudge against " + grudge.GetHoverName());
			}
			if (flags.Count > 0)
			{
				text.Append(string.Join(", ", flags) + "\n");
			}
			text.Append(DebugLoot());
			return(text.ToString());
		}

		/// <summary>
		/// Names what the viking is doing, for its name in debug mode
		/// </summary>
		/// <remarks>
		/// Its job is only known on the client that owns it, which runs its life
		/// </remarks>
		/// <returns>Its job, or null when it has none or its life doesn't run here</returns>
		public string DebugJob()
		{
			return((active && job != null) ? job.Describe() : null);
		}

		/// <summary>
		/// Describes the loot lying around the village for the debug hover text - how much there is and why the viking
		/// leaves what it leaves
		/// </summary>
		/// <returns>The line, ending in a line break</returns>
		private string DebugLoot()
		{
			List<Container> chests = UsableChests();
			bool mayLeave = MayLeave();
			int lying = 0;
			int settling = 0;
			int far = 0;
			int avoided = 0;
			int claimed = 0;
			int noRoom = 0;
			int wanted = 0;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				ItemDrop drop = ((view != null && view.IsValid()) ? view.GetComponent<ItemDrop>() : null);
				if (drop == null || village.DistanceOutside(view.transform.position) > LootReach + DebugLootMargin)
				{
					continue;
				}
				lying++;
				if (!IsLoot(view))
				{
					settling++;
				}
				else if (village.DistanceOutside(view.transform.position) > (mayLeave ? LootReach : 0f))
				{
					far++;
				}
				else if (Avoided(instance.Key.m_uid))
				{
					avoided++;
				}
				else if (workClaims.TryGetValue(instance.Key.m_uid, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					claimed++;
				}
				else if (!WantsLoot(drop.m_itemData, chests, mayLeave) || CarryRoom(drop.m_itemData, CarryLoot) <= 0)
				{
					noRoom++;
				}
				else
				{
					wanted++;
				}
			}
			if (lying == 0)
			{
				return("Loot: none, " + chests.Count + " usable chests\n");
			}
			return("Loot: " + lying + " lying, " + wanted + " to take, " + chests.Count + " usable chests - left: " + settling + " not loot or settling, " + far + " too far, " + avoided + " unreachable, " + claimed + " taken by others, " + noRoom + " no room\n");
		}
	}
}
