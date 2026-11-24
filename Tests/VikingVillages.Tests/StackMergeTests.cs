using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how unfinished stacks of an item are merged, so a chest holds it in as few slots as it fits in
	/// </summary>
	public class StackMergeTests
	{
		/// <summary>
		/// Checks that two unfinished stacks become a full one and the rest
		/// </summary>
		[Fact]
		public void TwoStacksFillTheFirst()
		{
			Assert.Equal(new[] { 50, 5 }, StackMerge.Consolidate(new[] { 30, 25 }, 50));
		}

		/// <summary>
		/// Checks that stacks that fit in one leave the rest empty
		/// </summary>
		[Fact]
		public void SmallStacksFitInOne()
		{
			Assert.Equal(new[] { 40, 0, 0 }, StackMerge.Consolidate(new[] { 20, 12, 8 }, 50));
		}

		/// <summary>
		/// Checks that full stacks and a remainder are kept as they are, and that nothing is lost
		/// </summary>
		[Fact]
		public void FullStacksStay()
		{
			Assert.Equal(new[] { 50, 50, 7 }, StackMerge.Consolidate(new[] { 50, 50, 7 }, 50));
			Assert.Equal(new[] { 50, 50, 20, 0 }, StackMerge.Consolidate(new[] { 45, 35, 30, 10 }, 50));
		}
	}
}
