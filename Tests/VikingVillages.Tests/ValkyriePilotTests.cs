using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a valkyrie flies when code flies her toward a destination, and how she climbs over what's in her way
	/// however she's flown
	/// </summary>
	public class ValkyriePilotTests
	{
		[Fact]
		public void SheKeepsToHerCruisingHeight()
		{
			Assert.Equal(1f, ValkyrieMotion.CruiseVertical(2f));
			Assert.Equal(-1f, ValkyrieMotion.CruiseVertical(40f));
			Assert.Equal(0f, ValkyrieMotion.CruiseVertical(ValkyrieMotion.CruiseHeight));
			Assert.InRange(ValkyrieMotion.CruiseVertical(ValkyrieMotion.CruiseHeight - 1f), 0.01f, 0.99f);
		}

		[Fact]
		public void SheFliesStraightAtHerDestination()
		{
			Vector3 dir = ValkyrieMotion.PilotDirection(new Vector2(0f, 100f), ValkyrieMotion.CruiseHeight, true);
			Assert.Equal(1f, dir.z, 3);
			Assert.Equal(0f, dir.y, 3);
			Assert.True(dir.magnitude <= 1.0001f);
		}

		[Fact]
		public void OverHerDestinationSheComesDownOrHovers()
		{
			Vector3 landing = ValkyrieMotion.PilotDirection(new Vector2(1f, 0f), 10f, true);
			Assert.True(landing.y < -0.9f);
			Assert.True(landing.magnitude <= 1.0001f);
			Vector3 hovering = ValkyrieMotion.PilotDirection(new Vector2(1f, 0f), ValkyrieMotion.CruiseHeight, false);
			Assert.Equal(0f, hovering.y, 3);
		}

		[Fact]
		public void SheClimbsOverWhatsInHerWay()
		{
			Vector3 climb = ValkyrieMotion.Avoid(new Vector3(1f, 0f, 0f), 6f);
			Assert.True(climb.y > 0.9f);
			Assert.InRange(climb.x, 0.1f, 0.35f);
			Assert.True(climb.magnitude <= 1.0001f);
		}

		[Fact]
		public void RightInFrontOfHerSheOnlyClimbs()
		{
			Vector3 climb = ValkyrieMotion.Avoid(new Vector3(0f, 0.2f, 1f), 1f);
			Assert.True(climb.y > 0.99f);
			Assert.True(climb.z < 0.01f && climb.z > 0f);
		}

		[Fact]
		public void SheIsLetBeWithNothingInHerWayOrComingDown()
		{
			Vector3 flying = new Vector3(1f, 0.1f, 0f);
			Assert.Equal(flying, ValkyrieMotion.Avoid(flying, -1f));
			Vector3 descending = new Vector3(1f, -1f, 0f);
			Assert.Equal(descending, ValkyrieMotion.Avoid(descending, 2f));
			Vector3 hovering = new Vector3(0.001f, 0f, 0f);
			Assert.Equal(hovering, ValkyrieMotion.Avoid(hovering, 2f));
		}

		[Fact]
		public void SheLooksFartherAheadTheFasterSheFlies()
		{
			Assert.Equal(ValkyrieMotion.LookMin, ValkyrieMotion.LookAhead(0f));
			Assert.True(ValkyrieMotion.LookAhead(10f) > ValkyrieMotion.LookAhead(5f));
			Assert.Equal(ValkyrieMotion.LookMax, ValkyrieMotion.LookAhead(100f));
		}

		[Fact]
		public void HighOverWaterSheFliesAsShesTold()
		{
			Assert.Equal(-1f, ValkyrieMotion.AboveWater(-1f, 10f));
			Assert.Equal(0.5f, ValkyrieMotion.AboveWater(0.5f, ValkyrieMotion.WaterClearance));
		}

		[Fact]
		public void NearTheWaterSheComesNoLower()
		{
			Assert.Equal(0f, ValkyrieMotion.AboveWater(-1f, ValkyrieMotion.WaterClearance - 0.0001f), 3);
			Assert.InRange(ValkyrieMotion.AboveWater(-1f, ValkyrieMotion.WaterClearance / 2f), 0.4f, 0.6f);
			Assert.Equal(1f, ValkyrieMotion.AboveWater(0.2f, 0f));
		}

		[Fact]
		public void InTheWaterSheRisesAtFullSpeed()
		{
			Assert.Equal(1f, ValkyrieMotion.AboveWater(-1f, -2f));
		}
	}
}
