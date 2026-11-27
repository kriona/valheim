using System;
using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Has a living viking call out to the local player when it sees them on fire, poisoned, freezing, badly hurt,
	/// covered in tar, smoked, overloaded, back from the dead or without clothes (see VikingClothesSpeech), and
	/// encourages them when it sees them with a training weapon in hand (see VikingTrainingSpeech) - only the first
	/// viking near them, once each time it happens, so the whole village doesn't
	/// </summary>
	/// <remarks>
	/// A player's status effects are only known on their own client, so it's their client that sees it and has the
	/// viking say it, over its head for every client nearby, whichever client runs the viking. A viking that knows the
	/// player calls them by name, and one that doesn't calls them traveler
	/// </remarks>
	internal static class VikingConcernSpeech
	{
		/// <summary>
		/// Something about a player a viking calls out - its name, for keeping track of it, how it's told, and what the
		/// viking says, with {0} for the player's name or "traveler" - anywhere, and for some, only outside the Ashlands
		/// or only in them - and for some, what fills in {1}, like the skill a training weapon trains
		/// </summary>
		internal class Concern
		{
			public string Name;
			public Func<Player, bool> Has;
			public string[] Lines;
			public string[] ColdLines = new string[0];
			public string[] HotLines = new string[0];
			public Func<Player, string> Detail;

			/// <summary>
			/// Every line the viking might call out about it, wherever the player is
			/// </summary>
			public IEnumerable<string> EveryLine
			{
				get
				{
					List<string> lines = new List<string>(Lines);
					lines.AddRange(ColdLines);
					lines.AddRange(HotLines);
					return(lines);
				}
			}
		}

		/// <summary>
		/// The share of their health below which a player looks badly hurt - the same as a viking counts itself badly
		/// hurt
		/// </summary>
		private const float BadlyHurtShare = 0.25f;

		/// <summary>
		/// What a viking calls out, most pressing first
		/// </summary>
		internal static readonly Concern[] Concerns =
		{
			new Concern
			{
				Name = "burning",
				Has = (Player player) => Effect(player, SEMan.s_statusEffectBurning),
				Lines = new[]
				{
					"{0}, you're on fire! Get to the water!",
					"Fire! {0}, you're burning!",
					"Put yourself out, {0} - you're smoking!",
					"You're on fire, {0}! Roll in the mud or something!"
				}
			},
			new Concern
			{
				Name = "poisoned",
				Has = (Player player) => Effect(player, SEMan.s_statusEffectPoison),
				Lines = new[]
				{
					"You look green, {0}. Poisoned?",
					"That's poison, {0} - get some mead in you!",
					"Spit it out, {0}! You've been poisoned!",
					"Poisoned, {0}? Sit down before you fall down."
				}
			},
			new Concern
			{
				Name = "freezing",
				Has = (Player player) => Effect(player, SEMan.s_statusEffectFreezing),
				Lines = new[]
				{
					"{0}, you're freezing! Get to the fire!",
					"Your lips are blue, {0} - come warm yourself!",
					"You'll freeze out there, {0}! Over here, by the fire!"
				}
			},
			new Concern
			{
				Name = "badly hurt",
				Has = (Player player) => player.GetHealthPercentage() < BadlyHurtShare,
				Lines = new[]
				{
					"You look half dead, {0} - sit by the fire.",
					"{0}! You're covered in blood - get some food in you.",
					"Easy, {0}. You look like you've been through a troll."
				}
			},
			new Concern
			{
				Name = "tared",
				Has = (Player player) => Effect(player, SEMan.s_statusEffectTared),
				Lines = new[]
				{
					"What did you roll in, {0}?",
					"You're dripping tar, {0}! Keep away from the fire.",
					"Tar? You'll be scrubbing that off for days, {0}."
				}
			},
			new Concern
			{
				Name = "smoked",
				Has = (Player player) => Effect(player, SEMan.s_statusEffectSmoked),
				Lines = new[]
				{
					"You reek of smoke, {0}.",
					"Been sitting in a chimney, {0}? Get some air.",
					"Cough it out, {0} - you've breathed in half the fire."
				}
			},
			new Concern
			{
				Name = "overloaded",
				Has = (Player player) => Effect(player, SEMan.s_statusEffectEncumbered),
				Lines = new[]
				{
					"Carrying the whole forest, {0}?",
					"You'll break your back with that load, {0}.",
					"Need a cart, {0}? Or a few more arms?"
				}
			},
			new Concern
			{
				Name = "corpse run",
				Has = (Player player) => CorpseRun.Protects(player),
				Lines = new[]
				{
					"Back already, {0}? I thought we'd lost you.",
					"{0}! We thought you were done for.",
					"Back from the dead, {0}? Don't make a habit of it."
				}
			},
			new Concern
			{
				Name = "unclothed",
				Has = VikingClothesSpeech.Unclothed,
				Lines = VikingClothesSpeech.CallLines,
				ColdLines = VikingClothesSpeech.ColdCallLines,
				HotLines = VikingClothesSpeech.HotCallLines
			},
			new Concern
			{
				Name = "training",
				Has = VikingTrainingSpeech.Training,
				Lines = VikingTrainingSpeech.CallLines,
				Detail = VikingTrainingSpeech.SkillOf
			}
		};

		/// <summary>
		/// How often the local player is checked, in seconds, how near and in sight a viking has to be to call out, in
		/// meters, and the least time between call-outs about the same thing, in seconds, so a fire that flares up again
		/// straight away isn't called out twice
		/// </summary>
		private const float CheckInterval = 1f;
		private const float SeeRange = 15f;
		private const float Cooldown = 120f;

		/// <summary>
		/// What the local player has had called out while it still goes on, and when each can next be, by its name
		/// </summary>
		private static readonly HashSet<string> calledOut = new HashSet<string>();
		private static readonly Dictionary<string, float> nextAllowed = new Dictionary<string, float>();
		private static float nextCheck;

		/// <summary>
		/// Has the nearest living viking that sees the local player call out the most pressing thing about them it
		/// hasn't called out yet - one at a time - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (Time.time < nextCheck)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			Player player = Player.m_localPlayer;
			if (player == null || player.IsDead() || player.GetSEMan() == null || !ServerCheck.Active)
			{
				return;
			}
			Character speaker = null;
			bool looked = false;
			foreach (Concern concern in Concerns)
			{
				if (!concern.Has(player))
				{
					calledOut.Remove(concern.Name);
					continue;
				}
				if (calledOut.Contains(concern.Name) || (nextAllowed.TryGetValue(concern.Name, out float next) && Time.time < next))
				{
					continue;
				}
				if (!looked)
				{
					speaker = Speaker(player);
					looked = true;
				}
				if (speaker == null)
				{
					continue;
				}
				calledOut.Add(concern.Name);
				nextAllowed[concern.Name] = Time.time + Cooldown;
				string[] lines = Pool(concern.Lines, concern.ColdLines, concern.HotLines, InAshlands(player));
				string line = lines[UnityEngine.Random.Range(0, lines.Length)];
				string detail = ((concern.Detail != null) ? concern.Detail(player) : "");
				VikingSpeech.Say(speaker, Said(line, player.GetPlayerName(), VikingSpeech.Knows(speaker, player.GetPlayerID()), detail), VikingSpeech.QuickTime, VikingSpeech.TalkCull);
				return;
			}
		}

		/// <summary>
		/// Fills in a concern's line with the player's name, or with what a viking calls a player it doesn't know (see
		/// VikingSpeech.WithStranger), and with its detail
		/// </summary>
		/// <param name="line">The line, with {0} where the player's name goes and {1} where the detail goes</param>
		/// <param name="name">The player's name</param>
		/// <param name="known">Whether the viking knows the player</param>
		/// <param name="detail">What fills in {1}, like the skill a training weapon trains, or ""</param>
		/// <returns>The line to say</returns>
		internal static string Said(string line, string name, bool known, string detail)
		{
			return(string.Format((known ? line : VikingSpeech.WithStranger(line, 0)), name, detail));
		}

		/// <summary>
		/// Gathers the lines that fit where a player is - those for anywhere, with those about the cold outside the
		/// Ashlands or those about its heat and ash in them
		/// </summary>
		/// <param name="anywhere">The lines for anywhere</param>
		/// <param name="cold">The lines for outside the Ashlands</param>
		/// <param name="hot">The lines for the Ashlands</param>
		/// <param name="ashlands">Whether the player is in the Ashlands</param>
		/// <returns>The lines</returns>
		internal static string[] Pool(string[] anywhere, string[] cold, string[] hot, bool ashlands)
		{
			List<string> lines = new List<string>(anywhere);
			lines.AddRange(ashlands ? hot : cold);
			return(lines.ToArray());
		}

		/// <summary>
		/// Checks whether a player is in the Ashlands
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>True when they are</returns>
		internal static bool InAshlands(Player player)
		{
			return(player.GetCurrentBiome() == Heightmap.Biome.AshLands);
		}

		/// <summary>
		/// Checks whether a player has a status effect
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="effect">The effect's name hash</param>
		/// <returns>True when they have it</returns>
		private static bool Effect(Player player, int effect)
		{
			SEMan seman = player.GetSEMan();
			return(seman != null && seman.HaveStatusEffect(effect));
		}

		/// <summary>
		/// Finds the nearest living viking that would call out to a player - alive, not their enemy, awake and not in a
		/// fight, within SeeRange and with them in sight
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The viking, or null when there's none</returns>
		private static Character Speaker(Player player)
		{
			Character nearest = null;
			float nearestDistance = SeeRange;
			bool night = EnvMan.IsNight();
			foreach (Character character in Character.GetAllCharacters())
			{
				if (!VikingCore.IsLivingViking(character) || character.IsDead() || BaseAI.IsEnemy(character, player))
				{
					continue;
				}
				float distance = Vector3.Distance(character.transform.position, player.transform.position);
				if (distance > nearestDistance)
				{
					continue;
				}
				// Lying in bed it's asleep, and at night sitting down for the night too
				if (VikingPoses.SettledDown(character, out bool lying) && (lying || night))
				{
					continue;
				}
				BaseAI ai = character.GetBaseAI();
				if (ai == null || ai.IsAlerted() || !ai.CanSeeTarget(player))
				{
					continue;
				}
				nearest = character;
				nearestDistance = distance;
			}
			return(nearest);
		}
	}
}
