namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests which monsters the vikings count as a threat to go after, by day and at night
	/// </summary>
	public class VillageThreatTests
	{
		/// <summary>
		/// Checks that by day a monster in the village, near, alerted or after anyone is a threat, and one doing none of
		/// that isn't
		/// </summary>
		/// <param name="inVillage">Whether it's in the village</param>
		/// <param name="near">Whether it's near the viking</param>
		/// <param name="alerted">Whether it's alerted</param>
		/// <param name="after">Whether it's after anyone or anything</param>
		/// <param name="expected">True when it's a threat</param>
		[Theory]
		[InlineData(true, false, false, false, true)]
		[InlineData(false, true, false, false, true)]
		[InlineData(false, false, true, false, true)]
		[InlineData(false, false, false, true, true)]
		[InlineData(false, false, false, false, false)]
		public void ByDay(bool inVillage, bool near, bool alerted, bool after, bool expected)
		{
			Assert.Equal(expected, VillageLife.CountsAsThreat(false, inVillage, near, alerted, after, false));
		}

		/// <summary>
		/// Checks that at night only a monster in the village, or after the viking or someone or something in it, is a
		/// threat - one outside that's near, alerted or after someone else is left alone
		/// </summary>
		/// <param name="inVillage">Whether it's in the village</param>
		/// <param name="near">Whether it's near the viking</param>
		/// <param name="alerted">Whether it's alerted</param>
		/// <param name="after">Whether it's after anyone or anything</param>
		/// <param name="afterVillage">Whether it's after the viking or someone or something in the village</param>
		/// <param name="expected">True when it's a threat</param>
		[Theory]
		[InlineData(true, false, false, false, false, true)]
		[InlineData(false, true, true, true, true, true)]
		[InlineData(false, true, false, false, false, false)]
		[InlineData(false, false, true, false, false, false)]
		[InlineData(false, false, true, true, false, false)]
		[InlineData(false, true, true, true, false, false)]
		public void AtNight(bool inVillage, bool near, bool alerted, bool after, bool afterVillage, bool expected)
		{
			Assert.Equal(expected, VillageLife.CountsAsThreat(true, inVillage, near, alerted, after, afterVillage));
		}
	}
}
