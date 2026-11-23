using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Has a living viking call out to the local player when it sees them on fire, poisoned, freezing, badly hurt,
	/// covered in tar, smoked, overloaded or back from the dead - only the first viking near them, once each time it
	/// happens, so the whole village doesn't
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
		/// viking says, with the player's name or "traveler"
		/// </summary>
		internal class Concern
		{
			public string Name;
			public Func<Player, bool> Has;
			public string[] Lines;
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
			}
		};

		/// <summary>
		/// What a viking calls a player it doesn't know
		/// </summary>
		private const string Stranger = "traveler";

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
			if (player == null || player.IsDead() || player.GetSEMan() == null || !ServerCheck.Active || !Plugin.VikingVillages.Value)
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
				string name = (VikingSpeech.Knows(speaker, player.GetPlayerID()) ? player.GetPlayerName() : Stranger);
				string line = concern.Lines[UnityEngine.Random.Range(0, concern.Lines.Length)];
				VikingSpeech.Say(speaker, string.Format(line, name), VikingSpeech.QuickTime, VikingSpeech.TalkCull);
				return;
			}
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
				if (!(character is LivingVikingBody) || character.IsDead() || BaseAI.IsEnemy(character, player))
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
