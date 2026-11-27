using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_travel console command, for testing the vikings' travels - sending the PC viking the player looks
	/// at, or a number of the vikings of the village they're in, to a village by name or one picked at random, in the
	/// world or straight off it
	/// </summary>
	/// <remarks>
	/// The vikings sent leave their village as a group, on the road the server plans for them (see TravelPlanner), the
	/// same as any group on the road. Sent off the world, they set sail at once and the server keeps them on the road
	/// (see VillageTravel). Only vikings this client runs can be sent
	/// </remarks>
	internal static class TravelCommand
	{
		/// <summary>
		/// The command's name, and the words that send vikings to a village at random and off the world
		/// </summary>
		private const string Command = "kri_vv_travel";
		private const string RandomWord = "random";
		private const string SimWord = "sim";

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand(Command, "<village name|random> [count] [sim] - sends the viking you look at, or [count] vikings of the village you're in (1 by default), to a village by name or at random, in the world or straight off it with sim", Run, isCheat: true, optionsFetcher: () => new List<string> { RandomWord });
		}

		/// <summary>
		/// Runs the command
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Run(Terminal.ConsoleEventArgs args)
		{
			Player player = Player.m_localPlayer;
			if (player == null || ZNet.instance == null)
			{
				args.Context.AddString("Load a world first");
				return;
			}
			List<string> words = new List<string>(args.Args);
			words.RemoveAt(0);
			bool sim = (words.Count > 0 && string.Equals(words[words.Count - 1], SimWord, System.StringComparison.OrdinalIgnoreCase));
			if (sim)
			{
				words.RemoveAt(words.Count - 1);
			}
			int count = 1;
			if (words.Count > 1 && int.TryParse(words[words.Count - 1], out int parsed))
			{
				count = Mathf.Max(1, parsed);
				words.RemoveAt(words.Count - 1);
			}
			string target = string.Join(" ", words).Trim();
			if (target.Length == 0)
			{
				args.Context.AddString("Usage: " + Command + " <village name|random> [count] [sim]");
				return;
			}
			Village village = Village.Find(player.transform.position);
			List<Humanoid> vikings = Pick(player, village, count);
			if (vikings.Count == 0)
			{
				args.Context.AddString("Look at a PC viking, or stand in a village with PC vikings this client runs");
				return;
			}
			TravelParty party = new TravelParty();
			for (int i = 0; i < vikings.Count; i++)
			{
				TravelPlanner.AddTo(party, vikings[i], (i == 0));
			}
			List<ZDOID> ids = vikings.ConvertAll((Humanoid viking) => viking.m_nview.GetZDO().m_uid);
			string group = ((vikings.Count > 1) ? "travel" + NetTime.Now() : "");
			bool random = string.Equals(target, RandomWord, System.StringComparison.OrdinalIgnoreCase);
			List<Vector3> excluded = new List<Vector3>();
			if (village != null)
			{
				excluded.Add(village.Farm);
			}
			TravelPlanner.Ask(vikings[0].transform.position, party, excluded, (TravelPlanner.Answer answer) => Send(ids, answer, group, party, sim, target), (random ? TravelPlanner.Goal.Random : TravelPlanner.Goal.Named), Vector3.zero, (random ? "" : target));
			args.Context.AddString("Asked the server for the road for " + vikings.Count + " vikings to " + (random ? "a village picked at random" : "the village called " + target) + (sim ? ", to go straight off the world" : ""));
		}

		/// <summary>
		/// Picks the vikings to send - the PC viking the player looks at, or else up to a number of the PC vikings
		/// of the village they're in, nearest first, that this client runs and aren't on a quest
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="village">The village they're in, or null</param>
		/// <param name="count">How many to send from the village</param>
		/// <returns>The vikings</returns>
		private static List<Humanoid> Pick(Player player, Village village, int count)
		{
			List<Humanoid> picked = new List<Humanoid>();
			GameObject hovered = player.GetHoverObject();
			Humanoid looked = ((hovered != null) ? hovered.GetComponentInParent<Humanoid>() : null);
			if (Sendable(looked))
			{
				picked.Add(looked);
				return(picked);
			}
			if (village == null)
			{
				return(picked);
			}
			List<Humanoid> all = new List<Humanoid>();
			foreach (Character character in village.Vikings())
			{
				Humanoid viking = (character as Humanoid);
				if (Sendable(viking))
				{
					all.Add(viking);
				}
			}
			all.Sort((Humanoid a, Humanoid b) => Vector3.Distance(a.transform.position, player.transform.position).CompareTo(Vector3.Distance(b.transform.position, player.transform.position)));
			picked.AddRange(all.GetRange(0, Mathf.Min(count, all.Count)));
			return(picked);
		}

		/// <summary>
		/// Checks whether a viking can be sent - a PC viking this client runs, alive and not out on a quest
		/// </summary>
		/// <param name="viking">The viking, or null</param>
		/// <returns>True when it can</returns>
		private static bool Sendable(Humanoid viking)
		{
			if (viking == null || viking.IsDead() || !VikingCore.IsPlayerLikeViking(viking) || viking.m_nview == null || !viking.m_nview.IsValid() || !viking.m_nview.IsOwner())
			{
				return(false);
			}
			return(viking.m_nview.GetZDO().GetString(VillageQuests.QuestIdKey, "").Length == 0);
		}

		/// <summary>
		/// Sends the vikings off on the road the server found - out of their village and on their way as a group, and
		/// setting sail at once to go straight off the world when asked
		/// </summary>
		/// <param name="ids">The vikings' ZDOs</param>
		/// <param name="answer">The server's answer</param>
		/// <param name="group">The group they set out as, or "" for one alone</param>
		/// <param name="party">What they can do on the road</param>
		/// <param name="sim">Whether they go straight off the world</param>
		/// <param name="target">What the command was given, for the message</param>
		private static void Send(List<ZDOID> ids, TravelPlanner.Answer answer, string group, TravelParty party, bool sim, string target)
		{
			Player player = Player.m_localPlayer;
			if (!answer.Found)
			{
				string none = "Found no village " + (string.Equals(target, RandomWord, System.StringComparison.OrdinalIgnoreCase) ? "picked at random" : "called " + target) + " the vikings can reach";
				Plugin.Log.LogInfo("Travel command: " + none);
				if (player != null)
				{
					player.Message(MessageHud.MessageType.TopLeft, none);
				}
				return;
			}
			int sent = 0;
			foreach (ZDOID id in ids)
			{
				ZDO zdo = ((ZDOMan.instance != null) ? ZDOMan.instance.GetZDO(id) : null);
				if (zdo == null || !zdo.IsOwner())
				{
					continue;
				}
				zdo.Set(Village.VillageKey, Vector3.zero);
				zdo.Set(Village.HomeKey, Vector3.zero);
				VillageTravel.SetOut(zdo, answer, group, party);
				if (sim)
				{
					VillageTravel.SetSail(zdo);
				}
				sent++;
			}
			string message = sent + " vikings set out for " + ((answer.Name.Length > 0) ? answer.Spoken() : "the village") + " at " + answer.Destination.ToString("F0") + ", " + answer.Length.ToString("F0") + "m and " + answer.Seconds.ToString("F0") + "s by " + TravelPlanner.DescribeRoad(answer.Road) + (sim ? ", off the world" : "");
			Plugin.Log.LogInfo("Travel command: " + message);
			TestLog.Count("Travel", "groups sent by the travel command");
			if (player != null)
			{
				player.Message(MessageHud.MessageType.TopLeft, message);
			}
		}
	}
}
