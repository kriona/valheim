using Kriona.Vikings;

namespace VikingVillages
{
	/// <summary>
	/// A living viking's AI, which lets its village life go first and calms it down once it has slept on being hurt by
	/// a player (see VikingCalm)
	/// </summary>
	internal class LivingVikingAI : VikingAI
	{
		/// <summary>
		/// The viking's life in its village, found once VikingCore has added it
		/// </summary>
		private VillageLife life;

		/// <summary>
		/// Gets the viking's life in its village
		/// </summary>
		/// <returns>Its life, or null before it has one</returns>
		private VillageLife Life()
		{
			if (life == null)
			{
				life = GetComponent<VillageLife>();
			}
			return(life);
		}

		/// <summary>
		/// Keeps the viking's paths off the navmesh areas it avoids (see VikingCore.AvoidedAreas) and out of the water
		/// while it needs to be (see VikingCore.UpdatePathing)
		/// </summary>
		protected override void StartPathing()
		{
			int avoided = viking.AvoidedAreas();
			if (avoided != 0)
			{
				NavmeshRoofs.StartAvoiding(avoided);
			}
			viking.UpdatePathing();
		}

		/// <summary>
		/// Lets the navmesh areas the viking avoided back into the paths of the creatures that come after it
		/// </summary>
		protected override void StopPathing()
		{
			NavmeshRoofs.StopAvoiding();
		}

		/// <summary>
		/// Lets the viking's village life go first - keeping it off roofs with no danger near and out of the water while
		/// it chases an enemy, and taking the update over while it walks back, gets off a roof or raises the alarm. On a
		/// client that doesn't own the viking, its village life lets go of what the viking was doing, which it notices
		/// here
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		/// <returns>True to skip the game's update</returns>
		protected override bool TakeOver(float dt)
		{
			VillageLife own = Life();
			return(own != null && own.BeforeAI(dt));
		}

		/// <summary>
		/// Notes how the viking moved once its AI and its village life have both had their say, to tell when it's
		/// twitching, and calms it once its day is up
		/// </summary>
		protected override void AfterUpdate()
		{
			VillageLife own = Life();
			if (own != null)
			{
				own.AfterAI();
			}
			VikingCalm.Update(this);
		}

		/// <summary>
		/// Restarts the viking's day until it calms down when a player hurt it
		/// </summary>
		/// <param name="damage">How much it was hurt</param>
		/// <param name="attacker">Who hurt it, or null</param>
		public override void OnDamaged(float damage, Character attacker)
		{
			base.OnDamaged(damage, attacker);
			VikingCalm.OnHurt(this, attacker);
		}
	}
}
