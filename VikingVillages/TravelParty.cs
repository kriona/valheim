namespace VikingVillages
{
	/// <summary>
	/// What a group of vikings setting out together can do on the road - the easiest biome among them, which the road
	/// keeps to and the village they go to is no harder than, the hardest, whose best boat they all sail in, and whether
	/// any of them carries something a portal won't take, like ore
	/// </summary>
	internal class TravelParty
	{
		/// <summary>
		/// The easiest and hardest biomes among them, by VikingGear's order
		/// </summary>
		public int Easiest;
		public int Hardest;

		/// <summary>
		/// Whether any of them carries something a portal won't take (see ItemDrop.ItemData.SharedData.m_teleportable)
		/// </summary>
		public bool Unteleportable;

		/// <summary>
		/// Makes a party of vikings all of one biome, carrying nothing a portal won't take
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <returns>The party</returns>
		public static TravelParty Of(int biome)
		{
			return(new TravelParty { Easiest = biome, Hardest = biome });
		}

		/// <summary>
		/// Adds a viking to the party - the first one added setting both biomes
		/// </summary>
		/// <param name="biome">Its biome, by VikingGear's order</param>
		/// <param name="unteleportable">Whether it carries something a portal won't take</param>
		/// <param name="first">Whether it's the first one added</param>
		public void Add(int biome, bool unteleportable, bool first)
		{
			Easiest = (first ? biome : System.Math.Min(Easiest, biome));
			Hardest = (first ? biome : System.Math.Max(Hardest, biome));
			Unteleportable |= unteleportable;
		}

		/// <summary>
		/// Describes the party for the log
		/// </summary>
		/// <returns>The description, like "biomes 1-3, by karve, carrying ore"</returns>
		public string Describe()
		{
			return("biomes " + Easiest + ((Hardest != Easiest) ? "-" + Hardest : "") + ", by " + TravelTime.BoatName(Hardest) + (Unteleportable ? ", carrying what no portal takes" : ""));
		}

		/// <summary>
		/// Writes the party to a package
		/// </summary>
		/// <param name="pkg">The package</param>
		public void Write(ZPackage pkg)
		{
			pkg.Write(Easiest);
			pkg.Write(Hardest);
			pkg.Write(Unteleportable);
		}

		/// <summary>
		/// Reads a party from a package
		/// </summary>
		/// <param name="pkg">The package</param>
		/// <returns>The party</returns>
		public static TravelParty Read(ZPackage pkg)
		{
			TravelParty party = new TravelParty();
			party.Easiest = pkg.ReadInt();
			party.Hardest = pkg.ReadInt();
			party.Unteleportable = pkg.ReadBool();
			return(party);
		}
	}
}
