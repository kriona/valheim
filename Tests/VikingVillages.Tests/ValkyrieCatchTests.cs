using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests when a valkyrie catches a rider who gets off her in the air, and when she lets go of them
	/// </summary>
	public class ValkyrieCatchTests
	{
		[Fact]
		public void SheCatchesARiderOverADangerousFall()
		{
			Assert.True(ValkyrieCatch.DangerousFall(80f, 50f, 30f, 2f));
			Assert.True(ValkyrieCatch.DangerousFall(60.5f, 50f, 30f, 2f));
			Assert.False(ValkyrieCatch.DangerousFall(59f, 50f, 30f, 2f));
			Assert.False(ValkyrieCatch.DangerousFall(52f, 50f, 30f, 2f));
		}

		[Fact]
		public void WaterDeepEnoughToSwimInBreaksTheFall()
		{
			Assert.False(ValkyrieCatch.DangerousFall(80f, 20f, 30f, 2f));
			Assert.False(ValkyrieCatch.DangerousFall(80f, 28f, 30f, 2f));
			Assert.True(ValkyrieCatch.DangerousFall(80f, 29f, 30f, 2f));
		}

		[Fact]
		public void SheLetsGoNearTheGroundOrTheWater()
		{
			Assert.True(ValkyrieCatch.LetsGo(51.5f, 50f, 30f));
			Assert.True(ValkyrieCatch.LetsGo(50.5f, 50f, 30f));
			Assert.False(ValkyrieCatch.LetsGo(53f, 50f, 30f));
			Assert.True(ValkyrieCatch.LetsGo(31f, 20f, 30f));
			Assert.False(ValkyrieCatch.LetsGo(35f, 20f, 30f));
		}

		/// <summary>
		/// Checks that she flies down slowly near the ground, fast high up, and in between faster the higher she is
		/// </summary>
		[Fact]
		public void SheSlowsOnlyNearTheGround()
		{
			Assert.Equal(ValkyrieCatch.DescentSpeed, ValkyrieCatch.DescentSpeedAt(0f), 3);
			Assert.Equal(ValkyrieCatch.DescentSpeed, ValkyrieCatch.DescentSpeedAt(ValkyrieCatch.SlowHeight), 3);
			Assert.Equal(ValkyrieCatch.FastDescentSpeed, ValkyrieCatch.DescentSpeedAt(ValkyrieCatch.SlowHeight + ValkyrieCatch.SlowBlend), 3);
			Assert.Equal(ValkyrieCatch.FastDescentSpeed, ValkyrieCatch.DescentSpeedAt(1000f), 3);
			float halfway = ValkyrieCatch.DescentSpeedAt(ValkyrieCatch.SlowHeight + ValkyrieCatch.SlowBlend / 2f);
			Assert.Equal((ValkyrieCatch.DescentSpeed + ValkyrieCatch.FastDescentSpeed) / 2f, halfway, 3);
		}

		/// <summary>
		/// Checks how long the way down takes against stepping her down a millimeter at a time at the speed for each
		/// height, from near the ground, from within the slowing stretch and from high up
		/// </summary>
		/// <param name="altitude">How high she starts, in meters</param>
		[Theory]
		[InlineData(20f)]
		[InlineData(40f)]
		[InlineData(430f)]
		public void TheWayDownTakesAsLongAsFlyingItDoes(float altitude)
		{
			double stepped = 0;
			double step = 0.001;
			for (double height = altitude; height > ValkyrieCatch.ReleaseHeight; height -= step)
			{
				stepped += step / ValkyrieCatch.DescentSpeedAt((float)(height - step / 2));
			}
			Assert.Equal((float)stepped, ValkyrieCatch.DescentTime(altitude), 1);
		}

		/// <summary>
		/// Checks that she carries someone down at most as long as the way down from where she caught them takes, and
		/// CatchMargin more - over water by the water's height, and none less than CatchMargin
		/// </summary>
		[Fact]
		public void SheCarriesThemAsLongAsTheWayDownTakes()
		{
			float margin = ValkyrieCatch.CatchMargin;
			Assert.Equal(ValkyrieCatch.DescentTime(420f) + margin, ValkyrieCatch.CatchLimit(470f, 50f, 30f, 0f), 3);
			Assert.Equal(ValkyrieCatch.DescentTime(50f) + margin, ValkyrieCatch.CatchLimit(80f, 20f, 30f, 0f), 3);
			Assert.Equal(margin, ValkyrieCatch.CatchLimit(50f, 50f, 30f, 0f), 3);
			Assert.True(ValkyrieCatch.DescentTime(430f) < 30f);
		}

		/// <summary>
		/// Checks that carrying someone across to land adds as long as flying there at CrossSpeed takes
		/// </summary>
		[Fact]
		public void CarryingThemToLandAddsTheWayAcross()
		{
			float across = ValkyrieCatch.CatchLimit(80f, 20f, 30f, 150f) - ValkyrieCatch.CatchLimit(80f, 20f, 30f, 0f);
			Assert.Equal(150f / ValkyrieCatch.CrossSpeed, across, 3);
		}

		/// <summary>
		/// Checks that only water deep enough to swim in counts as deep
		/// </summary>
		[Fact]
		public void DeepWaterIsDeepEnoughToSwimIn()
		{
			Assert.True(ValkyrieCatch.OverDeepWater(20f, 30f, 2f));
			Assert.True(ValkyrieCatch.OverDeepWater(28f, 30f, 2f));
			Assert.False(ValkyrieCatch.OverDeepWater(29f, 30f, 2f));
			Assert.False(ValkyrieCatch.OverDeepWater(40f, 30f, 2f));
		}

		/// <summary>
		/// Checks that she finds the shore of land due east, to within a meter, from 85 meters off
		/// </summary>
		[Fact]
		public void SheFindsTheShore()
		{
			Assert.True(ValkyrieCatch.NearestLand(Vector2.zero, (float x, float z) => ((x >= 85f) ? 40f : 0f), 30f, out Vector2 land));
			Assert.InRange(land.x, 85f, 86f);
			Assert.InRange(Mathf.Abs(land.y), 0f, 1f);
		}

		/// <summary>
		/// Checks that she flies for the nearer of two shores, the one to the west here, though the one to the east is
		/// on the same ring she looks along
		/// </summary>
		[Fact]
		public void SheFindsTheNearerShore()
		{
			Assert.True(ValkyrieCatch.NearestLand(Vector2.zero, (float x, float z) => ((x >= 59f || x <= -51f) ? 40f : 0f), 30f, out Vector2 land));
			Assert.InRange(land.x, -52f, -51f);
		}

		/// <summary>
		/// Checks that land only just above the water doesn't count, and that she finds none farther off than LandReach
		/// </summary>
		[Fact]
		public void SheFindsNoLandOutOfReach()
		{
			Assert.False(ValkyrieCatch.NearestLand(Vector2.zero, (float x, float z) => 30.5f, 30f, out Vector2 _));
			Assert.False(ValkyrieCatch.NearestLand(Vector2.zero, (float x, float z) => ((x >= ValkyrieCatch.LandReach + 5f) ? 40f : 0f), 30f, out Vector2 _));
		}

		/// <summary>
		/// Checks how she carries someone - straight down over land or near her shore, across toward it at CrossSpeed
		/// over water, coming down only while she's above CrossHeight, and climbing over what's in her way
		/// </summary>
		[Fact]
		public void SheCarriesThemAcrossThenDown()
		{
			Vector3 overLand = ValkyrieCatch.CarryVelocity(new Vector2(100f, 0f), 100f, true, -1f);
			Assert.Equal(new Vector3(0f, -ValkyrieCatch.DescentSpeedAt(100f), 0f), overLand);
			Vector3 arrived = ValkyrieCatch.CarryVelocity(new Vector2(1f, 1f), 20f, false, -1f);
			Assert.Equal(new Vector3(0f, -ValkyrieCatch.DescentSpeedAt(20f), 0f), arrived);
			Vector3 high = ValkyrieCatch.CarryVelocity(new Vector2(0f, 100f), 100f, false, -1f);
			Assert.Equal(ValkyrieCatch.CrossSpeed, high.z, 3);
			Assert.Equal(-ValkyrieCatch.DescentSpeedAt(100f), high.y, 3);
			Vector3 low = ValkyrieCatch.CarryVelocity(new Vector2(0f, 100f), ValkyrieCatch.CrossHeight, false, -1f);
			Assert.Equal(0f, low.y, 3);
			Vector3 climbing = ValkyrieCatch.CarryVelocity(new Vector2(0f, 100f), ValkyrieCatch.CrossHeight, false, 5f);
			Assert.True(climbing.y > 0f);
		}

		[Fact]
		public void SheLetsGoBelowWhereTheGameHurtsAFall()
		{
			Assert.Equal(0f, ValkyrieGrab.FallDamage(ValkyrieCatch.ReleaseHeight));
			Assert.True(ValkyrieGrab.FallDamage(ValkyrieCatch.CatchHeight) > 0f);
		}
	}
}
