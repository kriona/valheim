using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how the village a player is in is picked, how far around it everything is run, and which zones around a
	/// player count as loaded
	/// </summary>
	public class VillageSimulationTests
	{
		/// <summary>
		/// Checks that a player in no village's circle is in none
		/// </summary>
		[Fact]
		public void PickCircleFindsNoneOutside()
		{
			List<KeyValuePair<Vector3, float>> circles = new List<KeyValuePair<Vector3, float>>
			{
				new KeyValuePair<Vector3, float>(new Vector3(0f, 0f, 0f), 50f)
			};
			Assert.Equal(-1, VillageSimulation.PickCircle(circles, new Vector3(60f, 0f, 0f)));
			Assert.Equal(-1, VillageSimulation.PickCircle(new List<KeyValuePair<Vector3, float>>(), Vector3.zero));
		}

		/// <summary>
		/// Checks that a player on a village's edge is in it, leaving out height
		/// </summary>
		[Fact]
		public void PickCircleCountsTheEdgeAndIgnoresHeight()
		{
			List<KeyValuePair<Vector3, float>> circles = new List<KeyValuePair<Vector3, float>>
			{
				new KeyValuePair<Vector3, float>(new Vector3(0f, 0f, 0f), 50f)
			};
			Assert.Equal(0, VillageSimulation.PickCircle(circles, new Vector3(30f, 200f, 40f)));
		}

		/// <summary>
		/// Checks that a player in two villages' circles is in the one whose middle is nearer
		/// </summary>
		[Fact]
		public void PickCirclePrefersTheNearerMiddle()
		{
			List<KeyValuePair<Vector3, float>> circles = new List<KeyValuePair<Vector3, float>>
			{
				new KeyValuePair<Vector3, float>(new Vector3(0f, 0f, 0f), 100f),
				new KeyValuePair<Vector3, float>(new Vector3(120f, 0f, 0f), 100f)
			};
			Assert.Equal(1, VillageSimulation.PickCircle(circles, new Vector3(70f, 0f, 0f)));
			Assert.Equal(0, VillageSimulation.PickCircle(circles, new Vector3(50f, 0f, 0f)));
		}

		/// <summary>
		/// Checks that the last Meadows run's frozen vikings fall within the village's radius and margin - the village's
		/// circle reached 101m from (-358, -483)
		/// </summary>
		[Fact]
		public void InReachCoversTheFrozenOutposts()
		{
			Vector3 middle = new Vector3(-358f, 51f, -483f);
			float reach = 101f + VillageSimulation.CoverMargin;
			Assert.True(VillageSimulation.InReach(middle, reach, new Vector3(-418f, 43f, -503f)));
			Assert.True(VillageSimulation.InReach(middle, reach, new Vector3(-372f, 57f, -413f)));
			Assert.False(VillageSimulation.InReach(middle, reach, new Vector3(-358f, 51f, -283f)));
		}

		/// <summary>
		/// Checks that zones count as within a distance of another either way along the ground, up to it and not past it
		/// </summary>
		[Fact]
		public void ZonesWithinCountsEitherWay()
		{
			Vector2s center = new Vector2s(-5, -8);
			Assert.True(VillageSimulation.ZonesWithin(center, new Vector2s(-5, -8), 2));
			Assert.True(VillageSimulation.ZonesWithin(center, new Vector2s(-7, -6), 2));
			Assert.True(VillageSimulation.ZonesWithin(center, new Vector2s(-3, -10), 2));
			Assert.False(VillageSimulation.ZonesWithin(center, new Vector2s(-8, -8), 2));
			Assert.False(VillageSimulation.ZonesWithin(center, new Vector2s(-5, -5), 2));
		}
	}
}
