namespace ZombieApocalypse
{
	/// <summary>
	/// A viking's AI - the draugr's MonsterAI (see VikingCore.BuildPrefab), which runs a zombie viking home when it
	/// has strayed too far and gives its ranged weapon an archer's range while it runs
	/// </summary>
	/// <remarks>
	/// An update the viking takes over skips the game's, so other patches of MonsterAI.UpdateAI don't run for it either -
	/// a zombie getting up or one hunting for a raid is handled here for that
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
		/// Updates the AI, or skips the game's update while the viking runs home or takes the update over itself (see
		/// TakeOver) - on the client that owns the viking, as the game's own update is - and puts the weapon's settings
		/// and the roofs back once it's done, whether or not it finished
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		/// <returns>Whether the AI handled the update, true while it's skipped</returns>
		public override bool UpdateAI(float dt)
		{
			try
			{
				bool handled = (RunsGameUpdate(dt) ? base.UpdateAI(dt) : SkipUpdate(dt));
				AfterUpdate();
				return(handled);
			}
			finally
			{
				NavmeshRoofs.StopAvoiding();
				if (viking != null)
				{
					viking.RestoreWeaponAI();
				}
			}
		}

		/// <summary>
		/// Gets the viking ready for the game's update - keeping it off the navmesh areas it avoids (see
		/// VikingCore.AvoidedAreas) and readying its weapon - unless it meets a deathsquito's dive (see
		/// VikingCore.UpdateDiveDefense), runs home or takes the update over instead, on its owner
		/// </summary>
		/// <remarks>
		/// A zombie viking getting up is left to the game's update, whose patch holds a zombie still while it does
		/// </remarks>
		/// <param name="dt">Seconds since the last update</param>
		/// <returns>False to skip the game's update</returns>
		private bool RunsGameUpdate(float dt)
		{
			if (viking == null || RiseBlend.IsHeld(this))
			{
				return(true);
			}
			if (!m_nview.IsValid() || !m_nview.IsOwner())
			{
				TakeOver(dt);
				return(true);
			}
			int avoided = viking.AvoidedAreas();
			if (avoided != 0)
			{
				NavmeshRoofs.StartAvoiding(avoided);
			}
			viking.UpdatePathing();
			// A deathsquito diving at a living viking is met first, even as it runs home - there's no outrunning one
			if (viking.UpdateDiveDefense(dt) || TakeOver(dt) || viking.UpdateReturn(dt))
			{
				return(false);
			}
			viking.UpdateSheath();
			viking.TuneWeaponAI();
			return(true);
		}

		/// <summary>
		/// Runs the parts of the game's update every AI needs while the rest of it is skipped - ending a raid's hunt once
		/// the raid is over, the creature's healing, with a living viking's food and rest (see VikingRegenPatch), and how
		/// long it's been since it was hurt
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		/// <returns>True, as the update is handled</returns>
		private bool SkipUpdate(float dt)
		{
			RaidHunt.Update(this);
			UpdateRegeneration(dt);
			m_timeSinceHurt += dt;
			return(true);
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
