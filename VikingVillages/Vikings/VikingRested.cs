using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A PC viking's rest the way a player's works - resting while it sits by a fire or stands sheltered by one,
	/// which after the game's resting delay makes it rested for as long as the comfort around it gives, and rested on
	/// waking from a bed - each boosting its healing by as much as the game's own Resting and Rested effects boost a
	/// player's
	/// </summary>
	/// <remarks>
	/// The game's effects only work on a player, and a creature's healing doesn't read status effects, so the viking's
	/// rest is kept here and its healing boosted by VikingRegenPatch. Being rested is noted in the viking's ZDO, so it
	/// carries over to whichever client owns it next, while resting is only ever on the client that owns the viking
	/// </remarks>
	internal class VikingRested
	{
		/// <summary>
		/// On a PC viking, until when in ZNet time ticks it's rested
		/// </summary>
		private static readonly int RestedKey = "kriona.VikingVillages.vikingRested".GetStableHashCode();

		/// <summary>
		/// How long resting lasts after the viking was last seen resting, in seconds, a little longer than the time
		/// between its checks
		/// </summary>
		private const float RestingGrace = 1.5f;

		/// <summary>
		/// What the game's effects give when they can't be found - the delay before resting makes a player rested, and
		/// how long being rested lasts at comfort 1 and for each comfort level above it, in seconds
		/// </summary>
		private const float FallbackDelay = 20f;
		private const float FallbackBaseTime = 300f;
		private const float FallbackTimePerComfort = 60f;

		/// <summary>
		/// The least cover a spot needs, as well as a roof, to be sheltered, the same as a player's
		/// </summary>
		private const float ShelterCover = 0.8f;

		/// <summary>
		/// The viking whose rest this is
		/// </summary>
		private readonly PCVikingBody viking;

		/// <summary>
		/// Since when the viking has rested on this client without a break and when it was last seen resting, in
		/// Time.time seconds, or -1 while it hasn't been
		/// </summary>
		private float restingSince = -1f;
		private float restingLastSeen = -1f;

		/// <summary>
		/// Starts a PC viking's rest
		/// </summary>
		/// <param name="viking">The viking</param>
		public VikingRested(PCVikingBody viking)
		{
			this.viking = viking;
		}

		/// <summary>
		/// Notes whether the viking is resting right now - by a fire and sitting or sheltered, and not cold, freezing,
		/// burning or wet, unless somewhere warm and cozy dries it, the same as a player - making it rested once it has
		/// rested for the game's resting delay, and keeping it rested for as long as the comfort around it gives while it
		/// rests on, on its owner
		/// </summary>
		/// <param name="sitting">Whether it sits, on the floor or a chair</param>
		public void Check(bool sitting)
		{
			bool sheltered = Sheltered(viking.GetCenterPoint());
			if (!ByFire(viking) || (!sitting && !sheltered) || Uncomfortable())
			{
				restingSince = -1f;
				return;
			}
			// Resting starts over once it has broken off
			if (!Resting(restingSince, restingLastSeen, Time.time))
			{
				restingSince = Time.time;
			}
			restingLastSeen = Time.time;
			if (Time.time - restingSince >= RestingDelay())
			{
				Rest(sheltered, viking.transform.position);
			}
		}

		/// <summary>
		/// Checks whether a viking stands by a fire's warmth
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>True when it does</returns>
		public static bool ByFire(Character viking)
		{
			return(EffectArea.IsPointInsideArea(viking.transform.position, EffectArea.Type.Heat, viking.GetRadius()) != null);
		}

		/// <summary>
		/// Checks whether something keeps the viking from resting, the same as a player - being cold, freezing or
		/// burning, or wet outside somewhere warm and cozy
		/// </summary>
		/// <returns>True when something does</returns>
		private bool Uncomfortable()
		{
			SEMan seman = viking.GetSEMan();
			if (seman == null)
			{
				return(false);
			}
			if (seman.HaveStatusEffect(SEMan.s_statusEffectCold) || seman.HaveStatusEffect(SEMan.s_statusEffectFreezing) || seman.HaveStatusEffect(SEMan.s_statusEffectBurning))
			{
				return(true);
			}
			return(seman.HaveStatusEffect(SEMan.s_statusEffectWet) && !VikingWeather.Cozy(viking));
		}

		/// <summary>
		/// Gets how long a viking rests before it's rested - the game's Resting effect's delay
		/// </summary>
		/// <returns>The delay, in seconds</returns>
		private static float RestingDelay()
		{
			SE_Cozy cozy = RestingEffect();
			return((cozy != null) ? cozy.m_delay : FallbackDelay);
		}

		/// <summary>
		/// Works out how long until the resting viking becomes rested, the countdown a player's Resting effect shows
		/// </summary>
		/// <returns>The seconds left, or -1 when it isn't resting</returns>
		public float RestingLeft()
		{
			if (!IsResting())
			{
				return(-1f);
			}
			return(Mathf.Max(0f, RestingDelay() - (Time.time - restingSince)));
		}

		/// <summary>
		/// Works out how much longer the viking stays rested
		/// </summary>
		/// <returns>The seconds left, or -1 when it isn't rested</returns>
		public float RestedLeft()
		{
			if (!IsRested())
			{
				return(-1f);
			}
			return((float)(viking.m_nview.GetZDO().GetLong(RestedKey) - NetTime.Now()) / System.TimeSpan.TicksPerSecond);
		}

		/// <summary>
		/// Makes the viking rested as it wakes up in a bed, the same as a player waking
		/// </summary>
		/// <param name="bed">Where it slept</param>
		public void Woke(Vector3 bed)
		{
			Rest(Sheltered(bed + Vector3.up), bed);
		}

		/// <summary>
		/// Puts off when a viking's rest wears off by the time the world skipped ahead while players slept, so it stays
		/// rested as long as it would have, the way a player does - unless it had already worn off before the skip
		/// </summary>
		/// <param name="zdo">The viking's ZDO, owned by this client</param>
		/// <param name="skipped">How far the world skipped ahead, in ZNet time ticks</param>
		public static void Postpone(ZDO zdo, long skipped)
		{
			long until = zdo.GetLong(RestedKey);
			long postponed = Postponed(until, NetTime.Now(), skipped);
			if (postponed != until)
			{
				zdo.Set(RestedKey, postponed);
			}
		}

		/// <summary>
		/// Works out until when a viking stays rested once the world skipped ahead - put off by the skip when it was
		/// still rested as the skip began
		/// </summary>
		/// <param name="until">Until when it was rested, in ZNet time ticks</param>
		/// <param name="now">The time now, after the skip, in ticks</param>
		/// <param name="skipped">How far the world skipped ahead, in ticks</param>
		/// <returns>Until when it's rested, the same as before when nothing is put off</returns>
		internal static long Postponed(long until, long now, long skipped)
		{
			return((skipped > 0L && until > now - skipped) ? until + skipped : until);
		}

		/// <summary>
		/// Checks whether the viking is rested
		/// </summary>
		/// <returns>True while it is</returns>
		public bool IsRested()
		{
			return(viking.m_nview != null && viking.m_nview.IsValid() && viking.m_nview.GetZDO().GetLong(RestedKey) > NetTime.Now());
		}

		/// <summary>
		/// Describes the viking's rest for the debug hover text - whether it's resting by a fire, how long it's rested
		/// for, and how much faster that makes it heal
		/// </summary>
		/// <returns>The description</returns>
		public string Describe()
		{
			string rest = (IsResting() ? "resting by a fire" : "not resting");
			rest += (IsRested() ? ", rested for " + VikingFood.Duration(RestedLeft()) : ", not rested");
			float multiplier = RegenMultiplier();
			return(rest + ((multiplier != 1f) ? ", healing x" + multiplier.ToString("0.##") : ""));
		}

		/// <summary>
		/// Checks whether the viking is resting by a fire on this client
		/// </summary>
		/// <returns>True while it is</returns>
		public bool IsResting()
		{
			return(Resting(restingSince, restingLastSeen, Time.time));
		}

		/// <summary>
		/// Checks whether a viking is still resting - it has started and was last seen resting no more than RestingGrace
		/// ago
		/// </summary>
		/// <param name="since">Since when it has rested, in seconds, or -1 while it hasn't</param>
		/// <param name="lastSeen">When it was last seen resting, in seconds</param>
		/// <param name="now">The time now, in seconds</param>
		/// <returns>True while it is</returns>
		internal static bool Resting(float since, float lastSeen, float now)
		{
			return(since >= 0f && now - lastSeen <= RestingGrace);
		}

		/// <summary>
		/// Works out how much faster the viking heals while it rests and is rested, combining the game's Resting and
		/// Rested effects with its own, like a piece of gear that speeds healing, the way a player's effects combine
		/// </summary>
		/// <returns>The multiplier, 1 when nothing changes it</returns>
		public float RegenMultiplier()
		{
			float multiplier = 1f;
			SE_Cozy restingEffect = (IsResting() ? RestingEffect() : null);
			if (restingEffect != null)
			{
				restingEffect.ModifyHealthRegen(ref multiplier);
			}
			SE_Rested restedEffect = (IsRested() ? RestedEffect() : null);
			if (restedEffect != null)
			{
				restedEffect.ModifyHealthRegen(ref multiplier);
			}
			if (viking.GetSEMan() != null)
			{
				viking.GetSEMan().ModifyHealthRegen(ref multiplier);
			}
			return(multiplier);
		}

		/// <summary>
		/// Adds the game's Rested effect's boost to a skill's rise to a multiplier while the viking is rested, the same
		/// as a rested player's
		/// </summary>
		/// <param name="skill">The skill rising</param>
		/// <param name="multiplier">The multiplier, added to</param>
		public void ModifyRaiseSkill(Skills.SkillType skill, ref float multiplier)
		{
			SE_Rested rested = (IsRested() ? RestedEffect() : null);
			if (rested != null)
			{
				rested.ModifyRaiseSkill(skill, ref multiplier);
			}
		}

		/// <summary>
		/// Makes the viking rested for as long as the comfort around a spot gives, unless it's already rested for longer,
		/// on its owner
		/// </summary>
		/// <param name="sheltered">Whether the spot is sheltered</param>
		/// <param name="spot">Where it rested</param>
		private void Rest(bool sheltered, Vector3 spot)
		{
			if (viking.m_nview == null || !viking.m_nview.IsValid() || !viking.m_nview.IsOwner())
			{
				return;
			}
			SE_Rested rested = RestedEffect();
			float baseTime = ((rested != null) ? rested.m_baseTTL : FallbackBaseTime);
			float perComfort = ((rested != null) ? rested.m_TTLPerComfortLevel : FallbackTimePerComfort);
			int comfort = SE_Rested.CalculateComfortLevel(sheltered, spot);
			long until = NetTime.Now() + NetTime.Ticks(RestedTime(baseTime, perComfort, comfort));
			ZDO zdo = viking.m_nview.GetZDO();
			if (until > zdo.GetLong(RestedKey))
			{
				zdo.Set(RestedKey, until);
			}
		}

		/// <summary>
		/// Works out how long being rested lasts at a comfort level, the same as a player's - the base time at comfort 1,
		/// and more for each level above it
		/// </summary>
		/// <param name="baseTime">How long it lasts at comfort 1, in seconds</param>
		/// <param name="perComfort">How much longer it lasts for each comfort level above 1, in seconds</param>
		/// <param name="comfort">The comfort level</param>
		/// <returns>How long it lasts, in seconds</returns>
		internal static float RestedTime(float baseTime, float perComfort, int comfort)
		{
			return(baseTime + (comfort - 1) * perComfort);
		}

		/// <summary>
		/// Checks whether a spot is sheltered the way a player is - under a roof with enough cover around it
		/// </summary>
		/// <param name="point">The spot, at about a body's middle</param>
		/// <returns>True when it is</returns>
		private static bool Sheltered(Vector3 point)
		{
			return(Sheltered(point, out bool _));
		}

		/// <summary>
		/// Checks whether a spot is sheltered the way a player is - under a roof with enough cover around it - and
		/// whether it's under a roof at all, which keeps the rain off
		/// </summary>
		/// <param name="point">The spot, at about a body's middle</param>
		/// <param name="underRoof">Whether the spot is under a roof</param>
		/// <returns>True when it's sheltered</returns>
		public static bool Sheltered(Vector3 point, out bool underRoof)
		{
			Cover.GetCoverForPoint(point, out float cover, out underRoof);
			return(underRoof && cover >= ShelterCover);
		}

		/// <summary>
		/// Finds the game's Resting effect
		/// </summary>
		/// <returns>The effect, or null</returns>
		public static SE_Cozy RestingEffect()
		{
			return((ObjectDB.instance != null) ? ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectResting) as SE_Cozy : null);
		}

		/// <summary>
		/// Finds the game's Rested effect
		/// </summary>
		/// <returns>The effect, or null</returns>
		public static SE_Rested RestedEffect()
		{
			return((ObjectDB.instance != null) ? ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectRested) as SE_Rested : null);
		}
	}
}
