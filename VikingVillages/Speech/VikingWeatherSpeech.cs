using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What PC vikings say about the weather - the talks they have with each other about it, what they say to a
	/// player they know about it, and what they mutter when it gets to them
	/// </summary>
	/// <remarks>
	/// The weather is the one where the local player is, the only one the game works out, which is the weather around
	/// the client that owns a viking (see VikingWeather). Light rain has its own talks with the rest in VikingSpeech
	/// </remarks>
	internal static class VikingWeatherSpeech
	{
		/// <summary>
		/// The weather the vikings talk about - the worst of it, when there's more than one
		/// </summary>
		public enum Kind
		{
			Clear,
			Windy,
			Foggy,
			Cold,
			Freezing,
			Rain,
			Storm
		}

		/// <summary>
		/// How strong the wind has to be for the vikings to call it windy, the same as the strength that blows out a fire
		/// out in the open
		/// </summary>
		private const float WindyIntensity = 0.8f;

		/// <summary>
		/// How likely a viking greeting or answering a player it knows talks about the weather, when there's something
		/// to say about it
		/// </summary>
		private const float PlayerChance = 0.35f;

		/// <summary>
		/// How likely a viking mutters about the weather getting to it, and the least time between its mutters, in
		/// seconds
		/// </summary>
		public const float RemarkChance = 0.5f;
		public const float RemarkInterval = 240f;

		/// <summary>
		/// The talks two vikings have about each kind of weather, each line spoken in turn - those about the beard of the
		/// one starting the talk are kept apart, and only started by one with a beard, and those about the dead walking
		/// are kept apart, and left out unless the slain rise in this world (see VikingSpeech.DeadRise)
		/// </summary>
		internal static readonly Dictionary<Kind, string[][]> Talks = new Dictionary<Kind, string[][]>
		{
			{
				Kind.Storm, new[]
				{
					new[] { "Thor's in a mood today.", "Then keep out from under the tall trees, {name}.", "And away from the anvil." },
					new[] { "Hear that thunder?", "Thor's hunting giants again.", "I hope he's a better shot than you, {name}." },
					new[] { "Lightning lit up the whole meadow.", "Stay inside till it passes.", "Tell that to the work that needs doing." }
				}
			},
			{
				Kind.Rain, new[]
				{
					new[] { "Hang the hides under the eaves, {name}, or they'll rot.", "Already did. Twice." },
					new[] { "Rain's good for the berries, at least.", "And for the mud. Plenty of mud." }
				}
			},
			{
				Kind.Freezing, new[]
				{
					new[] { "It's bitter out. I can't feel my toes.", "Stay by the fire, {name}. Nothing out there is worth losing them over." },
					new[] { "That wind's coming down off the mountains.", "Jotunheim's breath. Keep the fire high." }
				}
			},
			{
				Kind.Cold, new[]
				{
					new[] { "My fingers are numb, {name}.", "Hold them over the fire, not the stew pot." }
				}
			},
			{
				Kind.Foggy, new[]
				{
					new[] { "Mist's rolling in off the water.", "Stay close to the fire today.", "And keep a torch handy." },
					new[] { "Fog like this hides all sorts.", "Trolls?", "Worse. Greylings after the resin." }
				}
			},
			{
				Kind.Windy, new[]
				{
					new[] { "Wind's picking up.", "Mind the sparks off the fire, {name}." },
					new[] { "Hold on to your helmet in this wind.", "It's held on with more than hope, {name}." }
				}
			},
			{
				Kind.Clear, new[]
				{
					new[] { "Fine day, {name}.", "Too fine. Something always goes wrong on days like this." },
					new[] { "Good drying weather.", "Then hang the hides out before it turns." }
				}
			}
		};
		internal static readonly Dictionary<Kind, string[][]> BeardTalks = new Dictionary<Kind, string[][]>
		{
			{
				Kind.Cold, new[]
				{
					new[] { "Cold enough to freeze a beard solid.", "Yours could use it - might stop it dripping in the stew." }
				}
			}
		};
		internal static readonly Dictionary<Kind, string[][]> ZombieTalks = new Dictionary<Kind, string[][]>
		{
			{
				Kind.Foggy, new[]
				{
					new[] { "Can't see the fence in this fog.", "Then the dead can't see us either.", "They don't need eyes, {name}." }
				}
			},
			{
				Kind.Clear, new[]
				{
					new[] { "They say the dead sleep easier in the sun.", "Who says that?", "Someone who never met a draugr." }
				}
			}
		};

		/// <summary>
		/// What a viking says to a player it knows about each kind of weather, with the player's name - Clear only by
		/// day
		/// </summary>
		internal static readonly Dictionary<Kind, string[]> PlayerLines = new Dictionary<Kind, string[]>
		{
			{ Kind.Storm, new[] { "Get under a roof, {0}! Thor's throwing his hammer about.", "Mind the lightning, {0}.", "Some storm, {0}. Come in and sit it out." } },
			{ Kind.Rain, new[] { "Come in out of the rain, {0}!", "Wet day for wandering, {0}.", "{0}! You're soaked through - come dry off by the fire." } },
			{ Kind.Freezing, new[] { "{0}, you'll freeze out there! Come to the fire.", "It's bitter out, {0}. Get warm while you can." } },
			{ Kind.Cold, new[] { "Cold one today, {0}. Warm yourself by the fire.", "Bundle up, {0}. There's a chill in the air." } },
			{ Kind.Foggy, new[] { "{0}? Hard to tell who's who in this fog.", "Stay close in this fog, {0}." } },
			{ Kind.Windy, new[] { "Windy one, {0}. Hold on to your helmet.", "This wind'll have the roof off, {0}." } },
			{ Kind.Clear, new[] { "Lovely day, {0}.", "The sun suits you, {0}.", "Good day for it, {0}, whatever it is." } }
		};

		/// <summary>
		/// What a viking mutters as the weather gets to it - getting wet, starting to freeze, and warming up again after
		/// freezing
		/// </summary>
		private static readonly string[] WetRemarks = { "Soaked through...", "Wet to the bone.", "Nothing worse than wet boots." };
		private static readonly string[] FreezingRemarks = { "Can't feel my fingers...", "So cold...", "Need a fire. Now." };
		private static readonly string[] WarmedRemarks = { "Ah, that's better.", "Warmth, at last.", "Feeling my toes again." };

		/// <summary>
		/// What the night watch mutters to itself about the cold in a biome whose weather freezes, with what only one with
		/// a beard mutters kept apart, and how long it goes between mutters, in seconds
		/// </summary>
		private static readonly string[] ColdWatchRemarks =
		{
			"Cold watch tonight...",
			"Can't feel my nose.",
			"Should've brought another cape.",
			"Even the wolves are hiding from this cold.",
			"Odin's beard, it's cold.",
			"Whoever picked me for the watch owes me a mead."
		};
		private static readonly string[] BeardWatchRemarks = { "My breath's freezing in my beard." };
		public const float ColdWatchIntervalMin = 90f;
		public const float ColdWatchIntervalMax = 180f;

		/// <summary>
		/// Works out the weather the vikings talk about - a thunderstorm, rain, freezing or cold weather, fog, wind, or
		/// else clear weather
		/// </summary>
		/// <returns>The weather</returns>
		public static Kind Now()
		{
			if (EnvMan.instance == null)
			{
				return(Kind.Clear);
			}
			if (VikingWeather.Storming())
			{
				return(Kind.Storm);
			}
			if (EnvMan.IsWet())
			{
				return(Kind.Rain);
			}
			if (EnvMan.IsFreezing())
			{
				return(Kind.Freezing);
			}
			if (EnvMan.IsCold())
			{
				return(Kind.Cold);
			}
			if (VikingWeather.Foggy())
			{
				return(Kind.Foggy);
			}
			return((EnvMan.instance.GetWindIntensity() >= WindyIntensity) ? Kind.Windy : Kind.Clear);
		}

		/// <summary>
		/// Lists the talks two vikings can have about the weather now - none about clear weather at night, and none about
		/// light rain, which VikingSpeech has its own talks for
		/// </summary>
		/// <param name="night">Whether it's night or dusk</param>
		/// <param name="bearded">Whether the viking starting the talk has a beard</param>
		/// <returns>The talks</returns>
		public static List<string[]> TalksNow(bool night, bool bearded)
		{
			Kind kind = Now();
			List<string[]> talks = new List<string[]>();
			if (night && kind == Kind.Clear)
			{
				return(talks);
			}
			if (Talks.TryGetValue(kind, out string[][] lines))
			{
				talks.AddRange(lines);
			}
			if (bearded && BeardTalks.TryGetValue(kind, out string[][] beardLines))
			{
				talks.AddRange(beardLines);
			}
			if (VikingSpeech.DeadRise() && ZombieTalks.TryGetValue(kind, out string[][] zombieLines))
			{
				talks.AddRange(zombieLines);
			}
			return(talks);
		}

		/// <summary>
		/// Picks, now and then, what a viking says to a player it knows about the weather, in place of its usual
		/// greeting or answer
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="night">Whether it's night</param>
		/// <returns>The line, or null for the usual one</returns>
		public static string ForPlayer(Player player, bool night)
		{
			Kind kind = Now();
			if ((night && kind == Kind.Clear) || Random.value >= PlayerChance || !PlayerLines.TryGetValue(kind, out string[] lines))
			{
				return(null);
			}
			return(string.Format(lines[Random.Range(0, lines.Length)], player.GetPlayerName()));
		}

		/// <summary>
		/// Picks what a viking mutters as it gets wet
		/// </summary>
		/// <returns>The line</returns>
		public static string WetRemark()
		{
			return(WetRemarks[Random.Range(0, WetRemarks.Length)]);
		}

		/// <summary>
		/// Picks what a viking mutters as it starts to freeze
		/// </summary>
		/// <returns>The line</returns>
		public static string FreezingRemark()
		{
			return(FreezingRemarks[Random.Range(0, FreezingRemarks.Length)]);
		}

		/// <summary>
		/// Picks what a viking mutters as it warms up again after freezing
		/// </summary>
		/// <returns>The line</returns>
		public static string WarmedRemark()
		{
			return(WarmedRemarks[Random.Range(0, WarmedRemarks.Length)]);
		}

		/// <summary>
		/// Picks what the night watch mutters about the cold in a biome whose weather freezes
		/// </summary>
		/// <param name="bearded">Whether the watch has a beard</param>
		/// <returns>The line</returns>
		public static string ColdWatchRemark(bool bearded)
		{
			List<string> lines = new List<string>(ColdWatchRemarks);
			if (bearded)
			{
				lines.AddRange(BeardWatchRemarks);
			}
			return(lines[Random.Range(0, lines.Count)]);
		}
	}
}
