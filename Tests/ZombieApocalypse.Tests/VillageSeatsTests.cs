using UnityEngine;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests where the sitting logs around a village's fire go - how far from it, which side they start on and which
	/// spots a log already there rules out
	/// </summary>
	public class VillageSeatsTests
	{
		/// <summary>
		/// Checks that a fire with no warmth leaves the logs in the ring the vikings gather in
		/// </summary>
		[Fact]
		public void NoWarmthKeepsTheRing()
		{
			Assert.Equal(3f, VillageSeats.SeatDistance(3f, 0f, 0.5f, 1f), 4);
		}

		/// <summary>
		/// Checks that a fire whose warmth doesn't reach the ring pulls the logs in to half a meter inside it, and one
		/// whose warmth reaches past the ring leaves them in it
		/// </summary>
		[Fact]
		public void WarmthCapsTheDistance()
		{
			Assert.Equal(2.5f, VillageSeats.SeatDistance(3f, 3f, 0.5f, 1f), 4);
			Assert.Equal(3f, VillageSeats.SeatDistance(3f, 10f, 0.5f, 1f), 4);
		}

		/// <summary>
		/// Checks that the logs always keep half a meter clear of the flames, even past the fire's warmth
		/// </summary>
		[Fact]
		public void FlamesFloorWins()
		{
			// The warmth would cap them at 2.5m, but the flames reach 2m and the log is 2m long
			Assert.Equal(3.5f, VillageSeats.SeatDistance(3f, 3f, 2f, 1f), 4);
			Assert.Equal(4.5f, VillageSeats.SeatDistance(3f, 0f, 2f, 2f), 4);
		}

		/// <summary>
		/// Checks that the side the logs start on is picked from where the fire stands across and deep, not how high,
		/// and wraps into a turn
		/// </summary>
		/// <param name="x">Where the fire stands across</param>
		/// <param name="y">How high it stands</param>
		/// <param name="z">Where it stands deep</param>
		/// <param name="expected">The angle, in degrees</param>
		[Theory]
		[InlineData(0f, 0f, 0f, 0f)]
		[InlineData(1f, 0f, 1f, 48f)]
		[InlineData(1f, 50f, 1f, 48f)]
		[InlineData(10f, 0f, 0f, 10f)]
		[InlineData(-1f, 0f, 0f, 323f)]
		[InlineData(-10f, 0f, -20f, 130f)]
		[InlineData(0f, 0f, -360f, 0f)]
		public void StartAngleWrapsIntoATurn(float x, float y, float z, float expected)
		{
			Assert.Equal(expected, VillageSeats.StartAngle(new Vector3(x, y, z)), 3);
		}

		/// <summary>
		/// Checks that the side the logs start on is always within a turn, for fires on either side of the world's middle
		/// </summary>
		[Fact]
		public void StartAngleStaysWithinATurn()
		{
			for (int x = -500; x <= 500; x += 7)
			{
				for (int z = -500; z <= 500; z += 13)
				{
					float angle = VillageSeats.StartAngle(new Vector3(x, 0f, z));
					Assert.True(angle >= 0f && angle < 360f, "Angle " + angle + " at " + x + ", " + z);
				}
			}
		}

		/// <summary>
		/// Checks that a log already within the spread around the fire rules a spot out, and one past it doesn't
		/// </summary>
		/// <param name="degrees">How far around the fire from the spot the log stands, in degrees</param>
		/// <param name="expected">Whether it rules the spot out</param>
		[Theory]
		[InlineData(0f, true)]
		[InlineData(30f, true)]
		[InlineData(-60f, true)]
		[InlineData(69f, true)]
		[InlineData(71f, false)]
		[InlineData(-90f, false)]
		[InlineData(180f, false)]
		public void LogWithinTheSpreadCrowds(float degrees, bool expected)
		{
			Vector3 middle = new Vector3(100f, 30f, -50f);
			float radians = degrees * Mathf.Deg2Rad;
			// Around the fire at the same distance as the spot, and higher, which doesn't count
			Vector3 log = middle + new Vector3(Mathf.Sin(radians), 0.3f, Mathf.Cos(radians)) * 3f;
			Assert.Equal(expected, VillageSeats.Crowds(log, middle, Vector3.forward, 3f, 1f));
		}

		/// <summary>
		/// Checks that a log past the spread still rules a spot out when it's nearer the spot than the logs keep apart,
		/// as it can be close in by the fire
		/// </summary>
		[Fact]
		public void LogTooNearTheSpotCrowds()
		{
			Vector3 middle = Vector3.zero;
			// 90 degrees around at 1m, 1.41m from the spot 1m out
			Vector3 log = new Vector3(1f, 0f, 0f);
			Assert.True(VillageSeats.Crowds(log, middle, Vector3.forward, 1f, 2f));
			Assert.False(VillageSeats.Crowds(log, middle, Vector3.forward, 1f, 1f));
		}
	}
}
