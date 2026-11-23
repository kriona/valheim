using System.Collections.Generic;
using Xunit;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how the lava's heat is worked out, which dangerous area a spot of the Ashlands' ground belongs to, and how
	/// a row of dangerous squares is joined into runs for the navmesh
	/// </summary>
	public class DangerousGroundTests
	{
		/// <summary>
		/// The water's height in the tests, the game's own
		/// </summary>
		private const float WaterLevel = 30f;

		/// <summary>
		/// How much a worked out value can be off from float rounding
		/// </summary>
		private const float Tolerance = 0.0001f;

		/// <summary>
		/// Joins a row of squares that all reach from 0 up to 1 into runs
		/// </summary>
		/// <param name="areas">Each square's area, -1 for safe ground</param>
		/// <returns>The runs</returns>
		private static List<DangerousGround.Span> Flat(params int[] areas)
		{
			float[] bottoms = new float[areas.Length];
			float[] tops = new float[areas.Length];
			for (int i = 0; i < areas.Length; i++)
			{
				tops[i] = 1f;
			}
			return(DangerousGround.Spans(areas, bottoms, tops, 1f));
		}

		/// <summary>
		/// Checks that the heat follows Character.UpdateLava's smooth step - nothing at or below 0.1, full at 1, and
		/// halfway at the middle
		/// </summary>
		/// <param name="lava">The lava in the paint</param>
		/// <param name="expected">The heat</param>
		[Theory]
		[InlineData(0f, 0f)]
		[InlineData(0.1f, 0f)]
		[InlineData(0.55f, 0.5f)]
		[InlineData(1f, 1f)]
		[InlineData(1.5f, 1f)]
		public void LavaHeatFollowsTheGame(float lava, float expected)
		{
			Assert.InRange(DangerousGround.LavaHeat(lava), expected - Tolerance, expected + Tolerance);
		}

		/// <summary>
		/// Checks that ground under the water is the sea's, whatever lava is painted on it
		/// </summary>
		[Fact]
		public void GroundUnderWaterIsTheSea()
		{
			Assert.Equal(DangerousGround.SeaArea, DangerousGround.AreaOf(WaterLevel - 0.1f, WaterLevel, 0f));
			Assert.Equal(DangerousGround.SeaArea, DangerousGround.AreaOf(WaterLevel - 5f, WaterLevel, 1f));
		}

		/// <summary>
		/// Checks that dry ground is the lava's once its lava is hot enough to burn, and safe below that
		/// </summary>
		[Fact]
		public void DryGroundIsLavaOnlyWhenHot()
		{
			Assert.Equal(-1, DangerousGround.AreaOf(WaterLevel + 2f, WaterLevel, 0f));
			Assert.Equal(-1, DangerousGround.AreaOf(WaterLevel + 2f, WaterLevel, 0.2f));
			Assert.Equal(DangerousGround.LavaArea, DangerousGround.AreaOf(WaterLevel + 2f, WaterLevel, 0.3f));
			Assert.Equal(DangerousGround.LavaArea, DangerousGround.AreaOf(WaterLevel + 2f, WaterLevel, 1f));
		}

		/// <summary>
		/// Checks that the lava's area reaches a little over the ground and the sea's a little over the water, however
		/// deep the ground is under it
		/// </summary>
		[Fact]
		public void AreasReachOverTheGroundOrTheWater()
		{
			float lavaTop = DangerousGround.TopOf(DangerousGround.LavaArea, 40f, WaterLevel);
			float seaTop = DangerousGround.TopOf(DangerousGround.SeaArea, 20f, WaterLevel);
			Assert.InRange(lavaTop, 40f, 41f);
			Assert.InRange(seaTop, WaterLevel, WaterLevel + 1f);
		}

		/// <summary>
		/// Checks that a row with no dangerous squares has no runs
		/// </summary>
		[Fact]
		public void SafeRowHasNoRuns()
		{
			Assert.Empty(Flat(-1, -1, -1));
		}

		/// <summary>
		/// Checks that squares side by side in one area make one run, and a safe square between them splits it
		/// </summary>
		[Fact]
		public void SafeSquareSplitsARun()
		{
			List<DangerousGround.Span> spans = Flat(6, 6, -1, 6);
			Assert.Equal(2, spans.Count);
			Assert.Equal(0, spans[0].Start);
			Assert.Equal(1, spans[0].End);
			Assert.Equal(3, spans[1].Start);
			Assert.Equal(3, spans[1].End);
		}

		/// <summary>
		/// Checks that the lava and the sea side by side make runs of their own
		/// </summary>
		[Fact]
		public void AreasMakeRunsOfTheirOwn()
		{
			List<DangerousGround.Span> spans = Flat(DangerousGround.LavaArea, DangerousGround.LavaArea, DangerousGround.SeaArea);
			Assert.Equal(2, spans.Count);
			Assert.Equal(DangerousGround.LavaArea, spans[0].Area);
			Assert.Equal(1, spans[0].End);
			Assert.Equal(DangerousGround.SeaArea, spans[1].Area);
			Assert.Equal(2, spans[1].Start);
		}

		/// <summary>
		/// Checks that a run reaches as low and as high as its squares do, and splits where a square's top rises past
		/// the spread from the run's first
		/// </summary>
		[Fact]
		public void RunSplitsWhereTheGroundRises()
		{
			int[] areas = { 6, 6, 6, 6 };
			float[] bottoms = { 0f, -0.5f, 2f, 2f };
			float[] tops = { 1f, 1.5f, 3f, 3f };
			List<DangerousGround.Span> spans = DangerousGround.Spans(areas, bottoms, tops, 1f);
			Assert.Equal(2, spans.Count);
			Assert.Equal(1, spans[0].End);
			Assert.Equal(-0.5f, spans[0].Bottom);
			Assert.Equal(1.5f, spans[0].Top);
			Assert.Equal(2, spans[1].Start);
			Assert.Equal(3, spans[1].End);
		}

		/// <summary>
		/// Checks that a run still open at the end of the row is kept
		/// </summary>
		[Fact]
		public void RunAtTheEndIsKept()
		{
			List<DangerousGround.Span> spans = Flat(-1, 7, 7);
			Assert.Single(spans);
			Assert.Equal(1, spans[0].Start);
			Assert.Equal(2, spans[0].End);
		}
	}
}
