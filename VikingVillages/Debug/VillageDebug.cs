using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A summary of a village's state, shown when the player looks at its fire with debug mode on, or in the debug
	/// sidebar (see VillageDebugPanel) - with its quest and the vikings on their way to it, counting down to when
	/// they're due (see VillageArrivals)
	/// </summary>
	/// <remarks>
	/// Some of it looks through every piece and creature across the village, so it's worked out at most once every
	/// RefreshInterval seconds for each village rather than every frame the fire's hover text is shown - which also
	/// counts down the times it gives once a second
	/// </remarks>
	internal static class VillageDebug
	{
		/// <summary>
		/// Seconds a village's summary is kept before it's worked out again
		/// </summary>
		private const float RefreshInterval = 1f;

		/// <summary>
		/// The gray the summary shows in, and the darker gray of each line's header, like "Pieces:", so it stands apart
		/// from what the line says
		/// </summary>
		private const string TextColor = "#C0C0C0";
		private const string HeaderColor = "#8C8C8C";

		/// <summary>
		/// A line's header - a label at its start, indented or not, of letters and the like but no comma, ending in a
		/// colon before a space or the line's end
		/// </summary>
		private static readonly Regex HeaderPattern = new Regex("^([ \\t]*)([A-Za-z][A-Za-z0-9 '()/-]*:)(?= |$)", RegexOptions.Multiline);

		/// <summary>
		/// Each village's summary and when it was worked out, by its generator's ZDO
		/// </summary>
		private static readonly Dictionary<ZDOID, KeyValuePair<float, string>> summaries = new Dictionary<ZDOID, KeyValuePair<float, string>>();

		/// <summary>
		/// Finds the loaded village a fire is the fire of
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>The village, or null when it's no village's fire</returns>
		public static Village Of(Fireplace fire)
		{
			foreach (Village village in Village.Loaded())
			{
				if (village.Fire() == fire)
				{
					return(village);
				}
			}
			return(null);
		}

		/// <summary>
		/// Describes a village's state, from its last summary when that's recent
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The summary, in gray, with its headers in a darker gray</returns>
		public static string Summary(Village village)
		{
			ZDOID id = village.Zdo.m_uid;
			if (summaries.TryGetValue(id, out KeyValuePair<float, string> kept) && Time.time - kept.Key < RefreshInterval)
			{
				return(kept.Value);
			}
			string summary = "<color=" + TextColor + ">" + GrayHeaders(Describe(village).TrimEnd('\n')) + "</color>";
			summaries[id] = new KeyValuePair<float, string>(Time.time, summary);
			return(summary);
		}

		/// <summary>
		/// Shows each line's header in HeaderColor (see HeaderPattern)
		/// </summary>
		/// <param name="text">The lines</param>
		/// <returns>The lines, their headers wrapped in color tags</returns>
		internal static string GrayHeaders(string text)
		{
			return(HeaderPattern.Replace(text, "$1<color=" + HeaderColor + ">$2</color>"));
		}

		/// <summary>
		/// Works out a village's summary, leaving out what would only say there's nothing to say - counts of none, setup
		/// that's done, the watch by day when there's none, and enemies when there are none near
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The lines, ending in a line break</returns>
		internal static string Describe(Village village)
		{
			StringBuilder text = new StringBuilder();
			// The zones go largest first, so the first is the farm's fenced ground or its biggest house
			int count = village.Zones.Count;
			string largest = ((count > 0) ? ", the largest " + village.Zones[0].Radius.ToString("F0") + "m at " + village.Zones[0].Center.ToString("F0") : "");
			text.Append("Village at " + village.Farm.ToString("F0") + ", " + village.Radius.ToString("F0") + "m around all of it - " + ((count == 1) ? "1 zone" : count + " zones") + largest + "\n");
			text.Append("Owner: " + (village.IsOwner() ? "this client" : "another client") + "\n");
			int blueprints = village.Houses.FindAll((VillagePaths.House house) => VillageBlueprints.IsBlueprintHouse(village, house)).Count;
			int outposts = village.Houses.Count - village.CoreHouses.Count;
			string kinds = Join(Count(village.CoreHouses.Count, "core"), Count(outposts, "outposts"), Count(blueprints, "from blueprints"));
			text.Append("Houses: " + village.Houses.Count + ((kinds.Length > 0) ? " - " + kinds : "") + "\n");
			List<Character> vikings = village.Vikings();
			int hurt = vikings.FindAll((Character viking) => viking.GetHealthPercentage() < 1f).Count;
			int hostile = vikings.FindAll((Character viking) => viking.GetBaseAI() != null && viking.GetBaseAI().IsAggravated()).Count;
			string state = Join(Count(hurt, "hurt"), Count(hostile, "hostile"));
			text.Append("Vikings loaded: " + vikings.Count + ((state.Length > 0) ? " (" + state + ")" : "") + "\n");
			VillageRespawn.Describe(village, text);
			text.Append(VillageRaids.Describe(village));
			VillageBeds.Describe(village, text);
			string watch = VillageWatch.Describe(village);
			bool evening = (EnvMan.IsNight() || (EnvMan.instance != null && EnvMan.instance.GetDayFraction() >= VillageLife.DuskStart));
			if (evening || watch != "none")
			{
				text.Append("Watch tonight: " + watch + "\n");
			}
			Fireplace fire = village.Fire();
			if (fire != null && fire.m_nview != null && fire.m_nview.IsValid())
			{
				float fuel = fire.m_nview.GetZDO().GetFloat(ZDOVars.s_fuel);
				int stations = VillageCooking.Stations(village, fire.transform.position).Count;
				text.Append("Fire: " + Utils.GetPrefabName(fire.gameObject) + ", fuel " + fuel.ToString("F1") + "/" + fire.m_maxFuel.ToString("F0") + ((stations > 0) ? ", " + stations + " cooking stations" : "") + (fire.IsBurning() ? "" : ", not burning") + "\n");
			}
			int records = VillageRecords.Count(village);
			if (records < 0)
			{
				text.Append("Pieces: not noted yet\n");
			}
			else
			{
				string pieces = Join(Count(VillageRecords.Missing(village).Count, "missing"), Count(VillageRecords.MissingInRuins(village).Count, "in ruins"), Count(VillageRecords.Damaged(village).Count, "damaged"));
				text.Append("Pieces: " + records + " noted" + ((pieces.Length > 0) ? ", " + pieces : "") + "\n");
			}
			List<string> todo = new List<string>();
			if (!village.Zdo.GetBool(VillageFurnishing.FurnishedKey))
			{
				todo.Add("not furnished");
			}
			if (!village.Zdo.GetBool(VillageCooking.CookingKey))
			{
				todo.Add("no cooking stations yet");
			}
			if (todo.Count > 0)
			{
				text.Append("Setup: " + string.Join(", ", todo) + "\n");
			}
			text.Append(VillageLife.DescribePaths(village));
			string goal = village.Zdo.GetString(VillageLife.GoalKey, "");
			if (goal.Length > 0)
			{
				int quality = village.Zdo.GetInt(VillageLife.GoalQualityKey, 1);
				text.Append("Goal: " + goal + ((quality > 1) ? " at quality " + quality : "") + " - " + village.Zdo.GetString(VillageLife.GoalStepKey, "") + "\n");
			}
			long now = NetTime.Now();
			List<VillageArrivals.Arrival> arrivals = VillageArrivals.Of(village);
			text.Append(VillageQuests.Describe(village, arrivals, now));
			QuestRecord quest = VillageQuests.Read(village);
			VillageArrivals.Describe(arrivals, ((quest != null) ? quest.Id : ""), now, text);
			if (village.EnemyNear(VillageRespawn.EnemyReach))
			{
				text.Append("Enemies within " + VillageRespawn.EnemyReach.ToString("F0") + "m\n");
			}
			return(text.ToString());
		}

		/// <summary>
		/// Writes a count with what it counts, or nothing when it's none
		/// </summary>
		/// <param name="count">The count</param>
		/// <param name="what">What it counts, like "hurt"</param>
		/// <returns>The part, like "2 hurt", or ""</returns>
		private static string Count(int count, string what)
		{
			return((count > 0) ? count + " " + what : "");
		}

		/// <summary>
		/// Joins the parts of a line that aren't empty with commas
		/// </summary>
		/// <param name="parts">The parts</param>
		/// <returns>The joined parts, or "" when they're all empty</returns>
		private static string Join(params string[] parts)
		{
			return(string.Join(", ", System.Array.FindAll(parts, (string part) => part.Length > 0)));
		}
	}
}
