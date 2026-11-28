using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a rider's keys steer a valkyrie - her speed, her way across the ground, and up or down by their look
	/// </summary>
	public class ValkyrieRideTests
	{
		[Fact]
		public void OnTheGroundSheWalksOnlyWhileAKeyIsHeld()
		{
			Assert.Equal(ValkyrieMotion.RideWalk, ValkyrieMotion.RideSpeed(1f, 0f, false, false, true));
			Assert.Equal(ValkyrieMotion.RideStop, ValkyrieMotion.RideSpeed(0f, 0f, false, false, true));
			Assert.Equal(ValkyrieMotion.RideRun, ValkyrieMotion.RideSpeed(0f, 0f, true, false, true));
		}

		[Fact]
		public void InTheAirSheKeepsGoing()
		{
			Assert.Equal(ValkyrieMotion.RideNoChange, ValkyrieMotion.RideSpeed(0f, 0f, false, false, false));
			Assert.Equal(ValkyrieMotion.RideStop, ValkyrieMotion.RideSpeed(-1f, 0f, false, false, false));
		}

		[Fact]
		public void SteeringToTheSideWalksHerToo()
		{
			Assert.Equal(ValkyrieMotion.RideWalk, ValkyrieMotion.RideSpeed(0f, 1f, false, false, true));
			Assert.Equal(ValkyrieMotion.RideWalk, ValkyrieMotion.RideSpeed(0f, -1f, false, false, false));
			Assert.Equal(ValkyrieMotion.RideTurn, ValkyrieMotion.RideSpeed(0f, 0f, false, true, true));
		}

		[Fact]
		public void SheGoesTheWayTheRiderSteers()
		{
			Vector3 look = new Vector3(0f, 0.3f, 1f);
			Vector3 ahead = ValkyrieMotion.RideDirection(look, 1f, 0f, false, false);
			Assert.Equal(1f, ahead.z, 3);
			Assert.Equal(0f, ahead.y, 3);
			Vector3 right = ValkyrieMotion.RideDirection(look, 0f, 1f, false, false);
			Assert.Equal(1f, right.x, 3);
			Assert.Equal(0f, right.z, 3);
			Vector3 left = ValkyrieMotion.RideDirection(look, 0f, -1f, false, false);
			Assert.Equal(-1f, left.x, 3);
			Vector3 aheadRight = ValkyrieMotion.RideDirection(look, 1f, 1f, false, false);
			Assert.Equal(Mathf.Sqrt(0.5f), aheadRight.x, 3);
			Assert.Equal(Mathf.Sqrt(0.5f), aheadRight.z, 3);
		}

		[Fact]
		public void WithNothingHeldSheKeepsHerWay()
		{
			Assert.Equal(Vector3.zero, ValkyrieMotion.RideDirection(Vector3.forward, 0f, 0f, false, false));
			Assert.Equal(Vector3.zero, ValkyrieMotion.RideDirection(Vector3.forward, -1f, 0f, false, false));
		}

		[Fact]
		public void HoldingBlockSheGoesUpOrDownTheWayTheRiderLooks()
		{
			Assert.Equal(0f, ValkyrieMotion.PitchVertical(0.05f));
			Assert.Equal(1f, ValkyrieMotion.PitchVertical(0.8f));
			Assert.Equal(-1f, ValkyrieMotion.PitchVertical(-0.6f));
			Assert.InRange(ValkyrieMotion.PitchVertical(0.3f), 0.1f, 0.9f);
		}
	}
}
