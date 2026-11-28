using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a viking shouts as a valkyrie flies over them - about the valkyrie herself, or about the player riding her,
	/// by name when the viking knows them, with lines of their own for Dvergr vikings - what a village's viking shouts as
	/// a traveler flies in on one, and what a traveler shouts to call their valkyrie down as they leave
	/// </summary>
	/// <remarks>
	/// Every line here is listed in the mod's DIALOG.md, which changes along with them
	/// </remarks>
	internal static class ValkyrieSightSpeech
	{
		/// <summary>
		/// What a PC or NPC viking shouts at a valkyrie with no rider, at one carrying a stranger, and at one carrying a
		/// player they know - {0} is the player's name
		/// </summary>
		internal static readonly string[] Lines =
		{
			"By Odin - a valkyrie!",
			"Look up! A valkyrie!",
			"A valkyrie, here? Is someone about to die?",
			"Did you see that? A valkyrie!",
			"A valkyrie! Is it my time already?"
		};

		internal static readonly string[] StrangerLines =
		{
			"Someone's riding a valkyrie!",
			"Look! There's a rider on that valkyrie!",
			"Who in Midgard rides a valkyrie?",
			"A rider on a valkyrie... I've seen everything now."
		};

		internal static readonly string[] KnownLines =
		{
			"Is that {0} up there? On a valkyrie?!",
			"{0}! Riding a valkyrie! Nobody will believe this.",
			"Hey, {0}! Bring her down, I want a closer look!",
			"{0} rides with the valkyries now? Show-off."
		};

		/// <summary>
		/// What a dvergr viking shouts, the same way
		/// </summary>
		internal static readonly string[] DvergrLines =
		{
			"A valkyrie, out of the mist!",
			"Wings in the fog - a valkyrie!",
			"Wings over the outpost - a valkyrie!"
		};

		internal static readonly string[] DvergrStrangerLines =
		{
			"Someone's flying a valkyrie over the outpost!",
			"A rider on a valkyrie? In this fog?"
		};

		internal static readonly string[] DvergrKnownLines =
		{
			"{0}? On a valkyrie? No one will believe me.",
			"Ha! {0}, you show-off! Mind the lanterns!"
		};

		/// <summary>
		/// What a village's viking shouts as a traveler flies in on their valkyrie, before they're in the village - {0} is
		/// the way they come from, like "the north"
		/// </summary>
		internal static readonly string[] HeraldLines =
		{
			"A valkyrie, coming in from {0}!",
			"Rider on a valkyrie - from {0}!",
			"Look to {0} - someone's flying in on a valkyrie!",
			"Wings in the sky, from {0}! A visitor on a valkyrie!"
		};

		/// <summary>
		/// The ways something can come from, an eighth of a turn each, clockwise from the north
		/// </summary>
		internal static readonly string[] Directions = { "the north", "the northeast", "the east", "the southeast", "the south", "the southwest", "the west", "the northwest" };

		/// <summary>
		/// Works out the way something comes from as seen from a point - the world's north being toward z, as on the map
		/// </summary>
		/// <param name="from">Where it's seen from</param>
		/// <param name="at">Where it is</param>
		/// <returns>The way, like "the north"</returns>
		internal static string Direction(Vector3 from, Vector3 at)
		{
			float x = at.x - from.x;
			float z = at.z - from.z;
			if (x * x + z * z < 0.0001f)
			{
				return(Directions[0]);
			}
			// Degrees clockwise from the north
			float bearing = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
			if (bearing < 0f)
			{
				bearing += 360f;
			}
			int index = Mathf.RoundToInt(bearing / 45f) % Directions.Length;
			return(Directions[index]);
		}

		/// <summary>
		/// Picks what a village's viking shouts as a traveler flies in on their valkyrie
		/// </summary>
		/// <param name="direction">The way they come from, like "the north"</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The line</returns>
		internal static string Herald(string direction, float roll)
		{
			return(string.Format(Pick(HeraldLines, roll), direction));
		}

		/// <summary>
		/// What a traveler shouts to call their valkyrie down from her roof as they leave - {0} is her name
		/// </summary>
		internal static readonly string[] CallLines =
		{
			"{0}! Time to go!",
			"{0}! Come down, we're leaving!",
			"{0}! To me!"
		};

		/// <summary>
		/// Picks what a traveler shouts to call their valkyrie down
		/// </summary>
		/// <param name="name">Her name</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The line</returns>
		internal static string Call(string name, float roll)
		{
			return(string.Format(Pick(CallLines, roll), name));
		}

		/// <summary>
		/// Picks what a viking shouts as a valkyrie flies over them
		/// </summary>
		/// <param name="dvergr">Whether they're a dvergr viking</param>
		/// <param name="ridden">Whether a player rides her</param>
		/// <param name="rider">The rider's name when the viking knows them, or ""</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The line</returns>
		internal static string Shout(bool dvergr, bool ridden, string rider, float roll)
		{
			if (!ridden)
			{
				return(Pick((dvergr ? DvergrLines : Lines), roll));
			}
			if (string.IsNullOrEmpty(rider))
			{
				return(Pick((dvergr ? DvergrStrangerLines : StrangerLines), roll));
			}
			return(string.Format(Pick((dvergr ? DvergrKnownLines : KnownLines), roll), rider));
		}

		/// <summary>
		/// Picks one of a list by a roll
		/// </summary>
		/// <param name="lines">The list</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The one picked</returns>
		private static string Pick(string[] lines, float roll)
		{
			return(lines[Mathf.Clamp(Mathf.FloorToInt(roll * lines.Length), 0, lines.Length - 1)]);
		}
	}
}
