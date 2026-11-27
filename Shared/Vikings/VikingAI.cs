namespace Kriona.Vikings
{
	/// <summary>
	/// A viking's AI - the draugr's MonsterAI (see VikingCore.BuildPrefab), which runs a zombie viking home when it
	/// has strayed too far and gives its ranged weapon an archer's range while it runs
	/// </summary>
	/// <remarks>
	/// An update the viking takes over skips the game's, so other patches of MonsterAI.UpdateAI don't run for it either -
	/// a zombie getting up or one hunting for a raid is handled by the zombie viking's AI for that
	/// </remarks>
	internal abstract class VikingAI : MonsterAI
	{
		/// <summary>
		/// The viking's looks, gear and skills
		/// </summary>
		protected VikingCore viking;

		/// <summary>
		/// Finds the viking's looks, gear and skills as the AI wakes
		/// </summary>
		public override void Awake()
		{
			base.Awake();
			viking = GetComponent<VikingCore>();
		}

		/// <summary>
		/// Checks whether the AI is held still while the viking gets up from its corpse, which leaves the update to the
		/// game's, whose patch holds a zombie still while it does
		/// </summary>
		/// <returns>True while it's held</returns>
		public virtual bool Held()
		{
			return(false);
		}

		/// <summary>
		/// Updates the AI, or skips the game's update while the viking runs home or takes the update over itself (see
		/// TakeOver) - on the client that owns the viking, as the game's own update is - and puts the weapon's settings
		/// and the paths back once it's done, whether or not it finished
		/// </summary>
		/// <remarks>
		/// An exception is logged and stops here, since one thrown into the game's loop over every AI skips each AI after
		/// this one in that update (see MonoUpdaterAIClearPatch)
		/// </remarks>
		/// <param name="dt">Seconds since the last update</param>
		/// <returns>Whether the AI handled the update, true while it's skipped or when it threw</returns>
		public override bool UpdateAI(float dt)
		{
			try
			{
				bool handled = (RunsGameUpdate(dt) ? base.UpdateAI(dt) : SkipUpdate(dt));
				AfterUpdate();
				return(handled);
			}
			catch (System.Exception e)
			{
				UnityEngine.Debug.LogException(e);
				return(true);
			}
			finally
			{
				StopPathing();
				if (viking != null)
				{
					viking.RestoreWeaponAI();
				}
			}
		}

		/// <summary>
		/// Gets the viking ready for the game's update - its paths (see StartPathing) and its weapon - unless it meets a
		/// deathsquito's dive (see VikingCore.UpdateDiveDefense), runs home or takes the update over instead, on its
		/// owner
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		/// <returns>False to skip the game's update</returns>
		private bool RunsGameUpdate(float dt)
		{
			if (viking == null || Held())
			{
				return(true);
			}
			if (!m_nview.IsValid() || !m_nview.IsOwner())
			{
				TakeOver(dt);
				return(true);
			}
			StartPathing();
			// A deathsquito diving at a PC viking is met first, even as it runs home - there's no outrunning one
			if (viking.UpdateDiveDefense(dt) || TakeOver(dt) || viking.UpdateReturn(dt))
			{
				return(false);
			}
			viking.UpdateSheath();
			viking.TuneWeaponAI();
			return(true);
		}

		/// <summary>
		/// Runs the parts of the game's update every AI needs while the rest of it is skipped - the creature's healing,
		/// with a PC viking's food and rest (see VikingRegenPatch), how long it's been since it was hurt, and what
		/// the viking's kind adds (see OnSkippedUpdate)
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		/// <returns>True, as the update is handled</returns>
		private bool SkipUpdate(float dt)
		{
			OnSkippedUpdate();
			UpdateRegeneration(dt);
			m_timeSinceHurt += dt;
			return(true);
		}

		/// <summary>
		/// Sets up the viking's paths before the game's update, on its owner, like the navmesh areas a PC viking
		/// keeps off
		/// </summary>
		protected virtual void StartPathing()
		{
		}

		/// <summary>
		/// Puts back what StartPathing changed, once the update is done
		/// </summary>
		protected virtual void StopPathing()
		{
		}

		/// <summary>
		/// Runs what the viking's kind needs from the game's update while it's skipped, like ending a raid's hunt once
		/// the raid is over
		/// </summary>
		protected virtual void OnSkippedUpdate()
		{
		}

		/// <summary>
		/// Lets the viking take the update over before the game's runs - called on every client, while only its owner's
		/// answer counts
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		/// <returns>True to skip the game's update</returns>
		protected virtual bool TakeOver(float dt)
		{
			return(false);
		}

		/// <summary>
		/// Runs once the update is done, whether the game's ran or not
		/// </summary>
		protected virtual void AfterUpdate()
		{
		}

		/// <summary>
		/// Notes who hurt the viking, so it can go after an enemy that hurts it from beyond its leash
		/// </summary>
		/// <param name="damage">How much it was hurt</param>
		/// <param name="attacker">Who hurt it, or null</param>
		public override void OnDamaged(float damage, Character attacker)
		{
			base.OnDamaged(damage, attacker);
			if (viking != null)
			{
				viking.OnHurt(attacker);
			}
		}
	}
}
