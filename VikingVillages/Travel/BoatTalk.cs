namespace VikingVillages
{
	/// <summary>
	/// What the vikings on a boat talk about as they sail - what they see around them, the most pressing first - and how
	/// often, worked out apart from the game so it can be tested (see VikingBoat's talk)
	/// </summary>
	internal static class BoatTalk
	{
		/// <summary>
		/// What a line on board is about
		/// </summary>
		internal enum Topic
		{
			Calm,
			Night,
			Fog,
			Cold,
			Rain,
			Storm,
			Leviathan,
			Serpent,
			Flyers
		}

		/// <summary>
		/// The least and most time between two lines of small talk on a boat, in seconds
		/// </summary>
		internal const float TalkMin = 35f;
		internal const float TalkMax = 70f;

		/// <summary>
		/// The least time between two lines on a boat however much is going on, in seconds, so a new danger is called out
		/// at once but never over the line before
		/// </summary>
		internal const float TalkGap = 8f;

		/// <summary>
		/// Picks what the vikings on a boat talk about - flying monsters after them first, then a sea serpent after them,
		/// a leviathan in sight, and the weather, a storm before rain, the cold, fog and the night, and otherwise the
		/// calm of the sea
		/// </summary>
		/// <param name="flyers">Whether flying monsters are after them</param>
		/// <param name="serpent">Whether a sea serpent is after them</param>
		/// <param name="leviathan">Whether a leviathan is in sight</param>
		/// <param name="storm">Whether it's storming</param>
		/// <param name="wet">Whether it's raining</param>
		/// <param name="freezing">Whether it's freezing</param>
		/// <param name="fog">Whether it's foggy</param>
		/// <param name="night">Whether it's night</param>
		/// <returns>The topic</returns>
		internal static Topic Pick(bool flyers, bool serpent, bool leviathan, bool storm, bool wet, bool freezing, bool fog, bool night)
		{
			if (flyers)
			{
				return(Topic.Flyers);
			}
			if (serpent)
			{
				return(Topic.Serpent);
			}
			if (leviathan)
			{
				return(Topic.Leviathan);
			}
			if (storm)
			{
				return(Topic.Storm);
			}
			if (wet)
			{
				return(Topic.Rain);
			}
			if (freezing)
			{
				return(Topic.Cold);
			}
			if (fog)
			{
				return(Topic.Fog);
			}
			return(night ? Topic.Night : Topic.Calm);
		}

		/// <summary>
		/// Checks whether a topic is something new to call out - a danger or a leviathan that wasn't being talked about
		/// before - which is said at once rather than waiting for the next bit of small talk
		/// </summary>
		/// <param name="topic">What there is to talk about now</param>
		/// <param name="last">What was talked about last</param>
		/// <returns>True when it is</returns>
		internal static bool CallsOut(Topic topic, Topic last)
		{
			return(topic != last && topic >= Topic.Leviathan);
		}
	}
}
