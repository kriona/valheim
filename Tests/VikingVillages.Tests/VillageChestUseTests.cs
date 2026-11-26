using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a disbanded village's bonfire notes the chests its vikings empty
	/// </summary>
	public class VillageChestUseTests
	{
		/// <summary>
		/// Checks that the chests read back as they were written, in the same order, and that no chests read back as none
		/// </summary>
		[Fact]
		public void ChestsRoundTrip()
		{
			List<ZDOID> chests = new List<ZDOID> { new ZDOID(-5L, 1u), new ZDOID(123456789012345L, 4000000000u), new ZDOID(7L, 0u) };
			Assert.Equal(chests, VillageChestUse.DecodeChests(VillageChestUse.EncodeChests(chests)));
			Assert.Empty(VillageChestUse.DecodeChests(VillageChestUse.EncodeChests(new List<ZDOID>())));
		}

		/// <summary>
		/// Checks that no bytes, bytes too short to hold a count, and bytes cut off partway through a chest all read as no
		/// chests rather than failing
		/// </summary>
		[Fact]
		public void CorruptChestsReadAsNone()
		{
			Assert.Empty(VillageChestUse.DecodeChests(null));
			Assert.Empty(VillageChestUse.DecodeChests(new byte[] { 1 }));
			byte[] bytes = VillageChestUse.EncodeChests(new List<ZDOID> { new ZDOID(1L, 2u), new ZDOID(3L, 4u) });
			Assert.Empty(VillageChestUse.DecodeChests(bytes[..(bytes.Length - 3)]));
		}
	}
}
