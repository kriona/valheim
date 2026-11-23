using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Gives a living viking the game's weather effects the way a player gets them - Wet in the rain out from under a
	/// roof, Cold and Freezing in cold weather away from a fire's warmth or out of shelter, unless its armor resists
	/// frost or it stands somewhere warm and cozy, and Sheltered and Campfire, a fire drying it off faster - muttering
	/// now and then as the weather gets to it - and tells the vikings what the weather is doing
	/// </summary>
	/// <remarks>
	/// The game only checks a player's surroundings for these, while the effects themselves work on any creature -
	/// being wet, cold or freezing slows the viking's healing (see VikingRegenPatch) and keeps it from resting (see
	/// VikingRested). The weather is the one where the local player is, the only one the game works out,
	/// and the client that owns a viking is the one nearest it
	/// </remarks>
	internal class VikingWeather
	{
		/// <summary>
		/// How often a viking's surroundings are checked, in seconds
		/// </summary>
		private const float CheckInterval = 1f;

		/// <summary>
		/// The viking the weather gets to
		/// </summary>
		private readonly LivingVikingBody viking;

		/// <summary>
		/// When the viking's surroundings are next checked on this client, in Time.time seconds
		/// </summary>
		private float nextCheck;

		/// <summary>
		/// Whether the viking was wet and freezing at the last check, and when it may next mutter about the weather, in
		/// Time.time seconds
		/// </summary>
		private bool wasWet;
		private bool wasFreezing;
		private float nextRemark;

		/// <summary>
		/// Starts the weather getting to a living viking
		/// </summary>
		/// <param name="viking">The viking</param>
		public VikingWeather(LivingVikingBody viking)
		{
			this.viking = viking;
		}

		/// <summary>
		/// Gives the viking the weather effects its surroundings call for, every CheckInterval seconds - call it while
		/// this client owns it
		/// </summary>
		public void Update()
		{
			SEMan seman = viking.GetSEMan();
			if (seman == null || viking.IsDead() || viking.m_nview == null || !viking.m_nview.IsValid() || !viking.m_nview.IsOwner())
			{
				return;
			}
			if (Time.time < nextCheck)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			Vector3 position = viking.transform.position;
			bool sheltered = VikingRested.Sheltered(viking.GetCenterPoint(), out bool underRoof);
			bool byFire = VikingRested.ByFire(viking);
			bool freezingWeather = EnvMan.IsFreezing();
			bool freezing = (freezingWeather && !byFire && !sheltered);
			bool cold = ((EnvMan.IsCold() && !byFire) || (freezingWeather && byFire && !sheltered) || (freezingWeather && !byFire && sheltered));
			if (ResistsFrost(viking) || Cozy(viking))
			{
				freezing = false;
				cold = false;
			}
			if (EnvMan.IsWet() && !underRoof && !ShieldGenerator.IsInsideShield(position))
			{
				seman.AddStatusEffect(SEMan.s_statusEffectWet, true);
			}
			Set(seman, SEMan.s_statusEffectShelter, sheltered);
			Set(seman, SEMan.s_statusEffectCampFire, byFire);
			// Cold and Freezing take turns, the way a player's do, each taking the other's place quietly
			if (freezing)
			{
				if (!seman.RemoveStatusEffect(SEMan.s_statusEffectCold, true))
				{
					seman.AddStatusEffect(SEMan.s_statusEffectFreezing);
				}
			}
			else if (cold)
			{
				if (!seman.RemoveStatusEffect(SEMan.s_statusEffectFreezing, true))
				{
					seman.AddStatusEffect(SEMan.s_statusEffectCold);
				}
			}
			else
			{
				seman.RemoveStatusEffect(SEMan.s_statusEffectCold);
				seman.RemoveStatusEffect(SEMan.s_statusEffectFreezing);
			}
			UpdateRemarks(seman);
		}

		/// <summary>
		/// Has the viking mutter now and then as the weather gets to it - getting wet, starting to freeze, or warming up
		/// again after freezing - at most every VikingWeatherSpeech.RemarkInterval seconds, and not over something else
		/// it's saying
		/// </summary>
		/// <param name="seman">The viking's effects</param>
		private void UpdateRemarks(SEMan seman)
		{
			bool wet = seman.HaveStatusEffect(SEMan.s_statusEffectWet);
			bool frozen = seman.HaveStatusEffect(SEMan.s_statusEffectFreezing);
			string remark = null;
			if (frozen && !wasFreezing)
			{
				remark = VikingWeatherSpeech.FreezingRemark();
			}
			else if (!frozen && wasFreezing)
			{
				remark = VikingWeatherSpeech.WarmedRemark();
			}
			else if (wet && !wasWet)
			{
				remark = VikingWeatherSpeech.WetRemark();
			}
			wasWet = wet;
			wasFreezing = frozen;
			if (remark == null || Time.time < nextRemark || Random.value >= VikingWeatherSpeech.RemarkChance || VikingSpeech.Chattering(viking, out string _))
			{
				return;
			}
			nextRemark = Time.time + VikingWeatherSpeech.RemarkInterval;
			VikingSpeech.Chatter(viking, remark, VikingSpeech.TalkTime);
		}

		/// <summary>
		/// Checks whether a viking stands somewhere warm and cozy, like inside a house with a hearth, which keeps the cold
		/// off and lets it rest while wet
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>True when it does</returns>
		public static bool Cozy(Character viking)
		{
			return(EffectArea.IsPointInsideArea(viking.transform.position, EffectArea.Type.WarmCozyArea, 1f) != null);
		}

		/// <summary>
		/// Checks whether it's raining on a spot - wet weather where the local player is, outside a shield generator's
		/// dome
		/// </summary>
		/// <param name="position">The spot</param>
		/// <returns>True when it is</returns>
		public static bool RainsOn(Vector3 position)
		{
			return(EnvMan.instance != null && EnvMan.IsWet() && !ShieldGenerator.IsInsideShield(position));
		}

		/// <summary>
		/// Checks whether it's the Swamp's own rain at a spot - raining on it in the Swamp with no storm - which hardly
		/// ever lets up there, so the vikings go about their day in it rather than spend it indoors
		/// </summary>
		/// <param name="position">The spot</param>
		/// <returns>True when it is</returns>
		public static bool SwampDrizzle(Vector3 position)
		{
			return(RainsOn(position) && !Storming() && WorldGenerator.instance != null && WorldGenerator.instance.GetBiome(position) == Heightmap.Biome.Swamp);
		}

		/// <summary>
		/// Checks whether the rain keeps the vikings in at a spot - from strolling, petting the boars and talking outdoors,
		/// and walking rather than hurrying - raining on it, but not the Swamp's own drizzle
		/// </summary>
		/// <param name="position">The spot</param>
		/// <returns>True when it does</returns>
		public static bool KeepsIndoors(Vector3 position)
		{
			return(RainsOn(position) && !SwampDrizzle(position));
		}

		/// <summary>
		/// Checks whether it's raining hard - a thunderstorm or another stormy wet weather - rather than light rain
		/// </summary>
		/// <returns>True when it is</returns>
		public static bool Storming()
		{
			return(EnvMan.instance != null && EnvMan.IsWet() && EnvironmentIs("thunder", "storm"));
		}

		/// <summary>
		/// Checks whether the weather is foggy, like the Meadows' mist
		/// </summary>
		/// <returns>True when it is</returns>
		public static bool Foggy()
		{
			return(EnvironmentIs("mist", "fog"));
		}

		/// <summary>
		/// Checks whether the weather's name has any of some words in it
		/// </summary>
		/// <param name="words">The words, in lower case</param>
		/// <returns>True when it does</returns>
		private static bool EnvironmentIs(params string[] words)
		{
			EnvSetup environment = ((EnvMan.instance != null) ? EnvMan.instance.GetCurrentEnvironment() : null);
			if (environment == null || string.IsNullOrEmpty(environment.m_name))
			{
				return(false);
			}
			string name = environment.m_name.ToLowerInvariant();
			foreach (string word in words)
			{
				if (name.Contains(word))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a spot is under a roof, so rain doesn't fall on it
		/// </summary>
		/// <param name="point">The spot, at about a body's middle</param>
		/// <returns>True when it is</returns>
		public static bool UnderRoof(Vector3 point)
		{
			Cover.GetCoverForPoint(point, out float _, out bool underRoof);
			return(underRoof);
		}

		/// <summary>
		/// Checks whether the weather is freezing for a viking - freezing weather where the local player is, the only
		/// weather the game works out, which nothing it wears resists - so it freezes unless it's in a fire's warmth or
		/// sheltered
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>True when it is</returns>
		public static bool FreezingFor(Character viking)
		{
			return(EnvMan.IsFreezing() && !ResistsFrost(viking));
		}

		/// <summary>
		/// Checks whether a viking has the game's Freezing effect
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>True while it does</returns>
		public static bool IsFreezing(Character viking)
		{
			SEMan seman = viking.GetSEMan();
			return(seman != null && seman.HaveStatusEffect(SEMan.s_statusEffectFreezing));
		}

		/// <summary>
		/// Checks whether a viking's armor, or anything else on it, resists frost, which keeps the cold off it the way it
		/// does a player
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>True when it does</returns>
		private static bool ResistsFrost(Character viking)
		{
			return(KeepsOffFrost(viking.GetDamageModifiers().GetModifier(HitData.DamageType.Frost)));
		}

		/// <summary>
		/// Checks whether a frost damage modifier is a resistance, which keeps the cold off the way it does a player
		/// </summary>
		/// <param name="frost">The modifier</param>
		/// <returns>True when it is</returns>
		public static bool KeepsOffFrost(HitData.DamageModifier frost)
		{
			return(frost == HitData.DamageModifier.Resistant || frost == HitData.DamageModifier.VeryResistant || frost == HitData.DamageModifier.SlightlyResistant);
		}

		/// <summary>
		/// Gives a viking an effect, or takes it away
		/// </summary>
		/// <param name="seman">The viking's effects</param>
		/// <param name="effect">The effect's name hash</param>
		/// <param name="on">Whether it has it</param>
		private static void Set(SEMan seman, int effect, bool on)
		{
			if (on)
			{
				seman.AddStatusEffect(effect);
			}
			else
			{
				seman.RemoveStatusEffect(effect);
			}
		}
	}
}
