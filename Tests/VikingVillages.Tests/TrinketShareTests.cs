using System.Collections.Generic;
using Kriona.Vikings;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests who a viking just rolled up shares a trinket with, so a village's PC vikings have it before its NPC vikings
	/// </summary>
	public class TrinketShareTests
	{
		/// <summary>
		/// Makes a holder of a trinket
		/// </summary>
		/// <param name="npc">Whether it's an NPC viking</param>
		/// <param name="has">Whether it has the trinket</param>
		/// <param name="movable">Whether it can hand it over or take it</param>
		/// <returns>The holder</returns>
		private static TrinketHolder Holder(bool npc, bool has, bool movable = true)
		{
			return(new TrinketHolder { NPC = npc, Has = has, Movable = movable });
		}

		/// <summary>
		/// Checks that a PC viking rolled without the trinket takes it from the first NPC viking with it
		/// </summary>
		[Fact]
		public void PCTakesFromAnNPC()
		{
			List<TrinketHolder> others = new List<TrinketHolder> { Holder(false, false), Holder(true, false), Holder(true, true), Holder(true, true) };
			Assert.Equal(2, TrinketShare.Partner(Holder(false, false), others));
		}

		/// <summary>
		/// Checks that an NPC viking rolled with the trinket hands it to the first PC viking without it
		/// </summary>
		[Fact]
		public void NPCHandsToAPC()
		{
			List<TrinketHolder> others = new List<TrinketHolder> { Holder(false, true), Holder(true, false), Holder(false, false) };
			Assert.Equal(2, TrinketShare.Partner(Holder(true, true), others));
		}

		/// <summary>
		/// Checks that a PC viking with the trinket, an NPC viking without it and a village with no one to share with
		/// share nothing
		/// </summary>
		[Fact]
		public void SharesOnlyWhenItHelps()
		{
			List<TrinketHolder> others = new List<TrinketHolder> { Holder(false, false), Holder(true, true) };
			Assert.Equal(-1, TrinketShare.Partner(Holder(false, true), others));
			Assert.Equal(-1, TrinketShare.Partner(Holder(true, false), others));
			Assert.Equal(-1, TrinketShare.Partner(Holder(false, false), new List<TrinketHolder> { Holder(false, false), Holder(true, false) }));
			Assert.Equal(-1, TrinketShare.Partner(Holder(true, true), new List<TrinketHolder> { Holder(false, true), Holder(true, false) }));
		}

		/// <summary>
		/// Checks that a viking that can't hand over or take the trinket, like one whose pockets hold things, is passed
		/// over
		/// </summary>
		[Fact]
		public void PassesOverThoseWhoCantShare()
		{
			List<TrinketHolder> others = new List<TrinketHolder> { Holder(true, true, false), Holder(true, true) };
			Assert.Equal(1, TrinketShare.Partner(Holder(false, false), others));
			Assert.Equal(-1, TrinketShare.Partner(Holder(true, true, false), new List<TrinketHolder> { Holder(false, false) }));
		}
	}
}
