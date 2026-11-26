using System.Collections.Generic;
using Xunit;

namespace Kriona.Vikings.Tests
{
	/// <summary>
	/// Tests the players a viking knows, saved on its ZDO and carried over to its zombie - reading back what's written,
	/// and reading nothing as knowing no one
	/// </summary>
	public class VikingNameTests
	{
		/// <summary>
		/// Checks that the players a viking knows read back in the order they were written
		/// </summary>
		[Fact]
		public void KnownReadsBackInOrder()
		{
			List<long> known = new List<long> { 1234567890123L, -42L, 7L };
			Assert.Equal(known, VikingName.ReadKnown(VikingName.WriteKnown(known)));
		}

		/// <summary>
		/// Checks that a viking with no players noted knows no one
		/// </summary>
		[Fact]
		public void NothingNotedKnowsNoOne()
		{
			Assert.Empty(VikingName.ReadKnown(null));
			Assert.Empty(VikingName.ReadKnown(VikingName.WriteKnown(new List<long>())));
		}

		/// <summary>
		/// Checks that "$" is taken out of a name and it's trimmed
		/// </summary>
		[Fact]
		public void PlainTakesOutDollarSigns()
		{
			Assert.Equal("Ragnar", VikingName.Plain(" $Rag$nar "));
		}
	}
}
