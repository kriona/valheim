namespace Kriona.Vikings
{
	/// <summary>
	/// A living viking's life in its village, which Viking Villages gives every living viking (see VillageLife there) -
	/// what the shared VikingCore asks of it, so it can run in Zombie Apocalypse too, where vikings have none
	/// </summary>
	internal interface IVikingLife
	{
		/// <summary>
		/// Checks whether the viking's life runs - it has a village, on its owner
		/// </summary>
		/// <returns>True when it does</returns>
		bool IsActive();

		/// <summary>
		/// Runs the viking's life in place of the game's wandering while it has nothing to fight
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True when it's handled, so the game's wandering is skipped</returns>
		bool Idle(float dt);

		/// <summary>
		/// Notes who hurt the viking
		/// </summary>
		/// <param name="attacker">Who hurt it, or null</param>
		void OnHurt(Character attacker);

		/// <summary>
		/// Forgets an enemy the viking has picked that's too far from its village
		/// </summary>
		void LeashTarget();

		/// <summary>
		/// Checks whether the viking leaves its gear as it is, while a player has its inventory open
		/// </summary>
		/// <returns>True while it does</returns>
		bool GearHeld();

		/// <summary>
		/// Checks whether the viking is away from its village's home ground, or on the road to a village
		/// </summary>
		/// <returns>True while it is</returns>
		bool AwayFromHome();

		/// <summary>
		/// Checks whether the viking is hopping over something in its way
		/// </summary>
		/// <returns>True while it is</returns>
		bool Hopping();

		/// <summary>
		/// Checks whether the viking is wary of danger near it
		/// </summary>
		/// <returns>True while it is</returns>
		bool IsWary();

		/// <summary>
		/// Checks whether the viking is out hunting
		/// </summary>
		/// <returns>True while it is</returns>
		bool Hunting();

		/// <summary>
		/// Checks whether the viking wants its torch lit
		/// </summary>
		/// <returns>True when it does</returns>
		bool WantsTorch();

		/// <summary>
		/// Checks whether the viking is aiming its bow at something
		/// </summary>
		/// <returns>True while it is</returns>
		bool AimingBow();

		/// <summary>
		/// Checks whether the viking would rather shoot an enemy than fight it hand to hand
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it would</returns>
		bool WantsBow(Character enemy);

		/// <summary>
		/// Gets what the viking is doing, for its debug summary
		/// </summary>
		/// <returns>Its job, or "" when it has none</returns>
		string DebugJob();

		/// <summary>
		/// Gets the lines the viking's life adds to its debug summary
		/// </summary>
		/// <returns>The lines</returns>
		string DebugSummary();
	}
}
