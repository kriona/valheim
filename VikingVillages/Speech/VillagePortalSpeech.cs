using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What the vikings of a village with a portal say about the village on its other side, while it's still up and has
	/// a name (see VillagePortals.Linked) - the talks two of them have, and now and then what they say when a player
	/// talks to them. Those in an Ashlands or Deep North village talk of the gentler land their portal leads to, and
	/// those in the village it leads to of the hard land on the other side
	/// </summary>
	/// <remarks>
	/// "{linked}" is the other village's name and "{biome}" the biome it stands in, with "the" before it, so no line
	/// starts with it
	/// </remarks>
	internal static class VillagePortalSpeech
	{
		/// <summary>
		/// The tokens the other village's name and its biome are filled in at
		/// </summary>
		internal const string LinkedToken = "{linked}";
		internal const string BiomeToken = "{biome}";

		/// <summary>
		/// The share of the times a player talks to a viking of such a village that it talks about the other village
		/// </summary>
		private const float ChatChance = 0.25f;

		/// <summary>
		/// The talks of the vikings of an Ashlands or Deep North village about the gentler village their portal leads to
		/// </summary>
		internal static readonly string[][] EasierTalks =
		{
			new[] { "When this place wears on me, {name}, I think of {linked}.", "Down in {biome}? Soft land for soft folk.", "Soft land, warm beds, and nothing trying to burn or freeze you." },
			new[] { "Someone from {linked} came through the portal last night.", "All the way up from {biome}? Did they stay?", "Long enough to see the place. Then straight back through." },
			new[] { "Do you ever go through to {linked}, {name}?", "When the stores run low. There's still good hunting in {biome}.", "Bring back something that isn't burned or frozen this time." },
			new[] { "If this village ever falls, we run for the portal.", "And then what?", "Then {linked} takes us in, and we start over in {biome}." }
		};

		/// <summary>
		/// The talks of the vikings of the village a portal leads to about the Ashlands or Deep North village on its other
		/// side
		/// </summary>
		internal static readonly string[][] HarderTalks =
		{
			new[] { "Folk came through the portal from {linked} again.", "All the way from {biome}?", "They wanted bread and a warm bed. Can't blame them." },
			new[] { "Would you go through the portal to {linked}, {name}?", "To {biome}? Not for all the silver in Midgard." },
			new[] { "How do they live out in {linked}?", "Hard, by the look of the ones who come through.", "Then we keep the fire lit for them." },
			new[] { "Whoever built that portal to {biome} had nerve.", "Or no sense. {linked} is no place for the weak." }
		};

		/// <summary>
		/// What the vikings of an Ashlands or Deep North village say about the village their portal leads to when a player
		/// talks to them, where "{0}" is the player's name
		/// </summary>
		internal static readonly string[] EasierChat =
		{
			"The portal here goes to {linked}, {0}, down in {biome}.",
			"Tired of this place, {0}? Step through the portal to {linked} and rest a while.",
			"Bring us something from {biome} next time you go through to {linked}, {0}."
		};

		/// <summary>
		/// What the vikings of the village a portal leads to say about the village on its other side when a player talks
		/// to them, where "{0}" is the player's name
		/// </summary>
		internal static readonly string[] HarderChat =
		{
			"Going through to {linked}, {0}? Give them our greetings - and take your best gear.",
			"Folk from {linked} come through now and then, {0}. Life in {biome} hardens a person."
		};

		/// <summary>
		/// What they also say about the village on the other side, by the biome it stands in, with "{0}" the same
		/// </summary>
		internal static readonly Dictionary<Heightmap.Biome, string> HarderBiomeChat = new Dictionary<Heightmap.Biome, string>
		{
			{ Heightmap.Biome.AshLands, "That portal goes to {linked}, {0}, out in {biome}. Dress for the heat." },
			{ Heightmap.Biome.DeepNorth, "That portal goes to {linked}, {0}, out in {biome}. Dress warm." }
		};

		/// <summary>
		/// Names a biome the way a viking says it in a line, with "the" before it
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <returns>Its name</returns>
		internal static string BiomeName(Heightmap.Biome biome)
		{
			switch (biome)
			{
				case Heightmap.Biome.Meadows:
					return("the Meadows");
				case Heightmap.Biome.BlackForest:
					return("the Black Forest");
				case Heightmap.Biome.Swamp:
					return("the Swamp");
				case Heightmap.Biome.Mountain:
					return("the Mountains");
				case Heightmap.Biome.Plains:
					return("the Plains");
				case Heightmap.Biome.Mistlands:
					return("the Mistlands");
				case Heightmap.Biome.AshLands:
					return("the Ashlands");
				case Heightmap.Biome.DeepNorth:
					return("the Deep North");
				case Heightmap.Biome.Ocean:
					return("the sea");
				default:
					return("the wilds");
			}
		}

		/// <summary>
		/// Picks the talks a village's vikings have about the village on the other side of its portal, by the biome it
		/// stands in
		/// </summary>
		/// <param name="biome">The other village's biome</param>
		/// <returns>The talks about the Ashlands or Deep North village when it's there, and about the gentler village
		/// otherwise</returns>
		internal static string[][] Talks(Heightmap.Biome biome)
		{
			return(VillagePortals.GetsPortal(biome) ? HarderTalks : EasierTalks);
		}

		/// <summary>
		/// Picks what a viking says about the village on the other side of its village's portal when a player talks to it,
		/// ChatChance of the time while that village is still up and has a name
		/// </summary>
		/// <param name="village">The viking's village, or null</param>
		/// <param name="player">The player's name</param>
		/// <returns>The line, or null when it says something else</returns>
		public static string Chat(Village village, string player)
		{
			if (Random.value >= ChatChance || !VillagePortals.Linked(village, out string linked, out Heightmap.Biome biome))
			{
				return(null);
			}
			List<string> lines = ChatFor(biome);
			return(Fill(lines[Random.Range(0, lines.Count)], player, linked, BiomeName(biome)));
		}

		/// <summary>
		/// Lists what a viking says about the village on the other side of its village's portal when a player talks to it,
		/// by the biome that village stands in
		/// </summary>
		/// <param name="biome">The other village's biome</param>
		/// <returns>The lines about the Ashlands or Deep North village, with the one for its own biome, when it's there,
		/// and about the gentler village otherwise</returns>
		internal static List<string> ChatFor(Heightmap.Biome biome)
		{
			if (!VillagePortals.GetsPortal(biome))
			{
				return(new List<string>(EasierChat));
			}
			List<string> lines = new List<string>(HarderChat);
			if (HarderBiomeChat.TryGetValue(biome, out string own))
			{
				lines.Add(own);
			}
			return(lines);
		}

		/// <summary>
		/// Fills in a line said to a player about the village on the other side of a portal - the other village's name
		/// and biome first, with any braces in them doubled, then the player's name, so names that read like tokens are
		/// said as they were given
		/// </summary>
		/// <param name="line">The line, with "{0}" for the player's name</param>
		/// <param name="player">The player's name</param>
		/// <param name="linked">The other village's name</param>
		/// <param name="biome">Its biome, as a viking says it (see BiomeName)</param>
		/// <returns>The line to say</returns>
		internal static string Fill(string line, string player, string linked, string biome)
		{
			string filled = line.Replace(LinkedToken, Escape(linked)).Replace(BiomeToken, Escape(biome));
			return(string.Format(filled, player));
		}

		/// <summary>
		/// Doubles the braces in a name, so string.Format shows them as they are
		/// </summary>
		/// <param name="name">The name</param>
		/// <returns>The name, braces doubled</returns>
		private static string Escape(string name)
		{
			return(name.Replace("{", "{{").Replace("}", "}}"));
		}
	}
}
