using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the rules of the boats vikings build - how many a group takes, who sits where, how they steer, and how long
	/// a viking swims before it drowns
	/// </summary>
	public class BoatRulesTests
	{
		[Theory]
		[InlineData(0, 4, 0)]
		[InlineData(1, 4, 1)]
		[InlineData(4, 4, 1)]
		[InlineData(5, 4, 2)]
		[InlineData(9, 2, 5)]
		public void GroupTakesAsManyBoatsAsItFills(int vikings, int crew, int expected)
		{
			Assert.Equal(expected, BoatRules.BoatsFor(vikings, crew));
		}

		[Fact]
		public void CrewsAreSharedOutEvenly()
		{
			List<string> ids = new List<string> { "e", "a", "d", "b", "c" };
			List<List<string>> crews = BoatRules.Crews(ids, 4);
			Assert.Equal(2, crews.Count);
			Assert.Equal(new List<string> { "a", "b", "c" }, crews[0]);
			Assert.Equal(new List<string> { "d", "e" }, crews[1]);
		}

		[Fact]
		public void OneBoatTakesTheWholeGroupWhenItFits()
		{
			List<List<string>> crews = BoatRules.Crews(new List<string> { "b", "a" }, 8);
			Assert.Single(crews);
			Assert.Equal("a", crews[0][0]);
		}

		[Fact]
		public void TheHardestBiomeBuildsTheBoats()
		{
			Assert.Equal(1, BoatRules.Builder(new List<string> { "a", "b", "c" }, new List<int> { 1, 3, 2 }));
			// A tie goes to the lowest ID
			Assert.Equal(2, BoatRules.Builder(new List<string> { "c", "b", "a" }, new List<int> { 3, 1, 3 }));
			Assert.Equal(-1, BoatRules.Builder(new List<string>(), new List<int>()));
		}

		[Fact]
		public void RudderTurnsTowardTheWaypoint()
		{
			Assert.Equal(0f, BoatRules.Rudder(Vector3.forward, Vector3.forward), 3);
			Assert.Equal(1f, BoatRules.Rudder(Vector3.forward, Vector3.right), 3);
			Assert.Equal(-1f, BoatRules.Rudder(Vector3.forward, Vector3.left), 3);
			Assert.Equal(0.5f, BoatRules.Rudder(Vector3.forward, new Vector3(Mathf.Tan(22.5f * Mathf.Deg2Rad), 0f, 1f)), 2);
			Assert.Equal(0f, BoatRules.Rudder(Vector3.forward, Vector3.zero), 3);
		}

		[Fact]
		public void SwimmingLastsAsLongAsAPlayersStamina()
		{
			// 75 base stamina at 5 a second with no skill
			Assert.Equal(15f, BoatRules.SwimSeconds(0f, 0f, 1f), 3);
			// More with food and skill
			Assert.Equal(125f / 2f, BoatRules.SwimSeconds(50f, 1f, 1f), 3);
			// A world that uses stamina at double the rate halves it
			Assert.Equal(7.5f, BoatRules.SwimSeconds(0f, 0f, 2f), 3);
		}

		[Fact]
		public void SwimmingDrainsLessStaminaWithMoreSkill()
		{
			Assert.Equal(5f, BoatRules.SwimDrain(0f), 3);
			Assert.Equal(3.5f, BoatRules.SwimDrain(0.5f), 3);
			Assert.Equal(2f, BoatRules.SwimDrain(1f), 3);
			Assert.Equal(2f, BoatRules.SwimDrain(2f), 3);
		}

		[Fact]
		public void StaminaComesBackFasterTheLessIsLeft()
		{
			// A player's regen - 5 a second when full, twice that when empty
			Assert.Equal(10f, BoatRules.StaminaRegain(0f, 100f), 3);
			Assert.Equal(7.5f, BoatRules.StaminaRegain(50f, 100f), 3);
			Assert.Equal(5f, BoatRules.StaminaRegain(100f, 100f), 3);
			Assert.Equal(0f, BoatRules.StaminaRegain(0f, 0f), 3);
		}

		[Fact]
		public void SwimDistanceIsSwimTimeAtSwimSpeed()
		{
			// 15 seconds at 2 meters a second
			Assert.Equal(30f, BoatRules.SwimMeters(0f, 0f, 1f, 2f), 3);
			Assert.Equal(125f / 2f * 2.5f, BoatRules.SwimMeters(50f, 1f, 1f, 2.5f), 3);
		}

		[Theory]
		[InlineData(100f, 5f)]
		[InlineData(110f, 6f)]
		[InlineData(0f, 0f)]
		public void DrowningTakesAShareOfMaxHealth(float maxHealth, float expected)
		{
			Assert.Equal(expected, BoatRules.DrownDamage(maxHealth), 3);
		}

		[Fact]
		public void SeaStretchEndsAtTheFirstWaypointAshore()
		{
			TravelRoad road = new TravelRoad();
			road.Add(Vector3.zero, TravelLeg.Foot);
			road.Add(new Vector3(10f, 0f, 0f), TravelLeg.Foot);
			road.Add(new Vector3(70f, 0f, 0f), TravelLeg.Sea);
			road.Add(new Vector3(130f, 0f, 0f), TravelLeg.Sea);
			road.Add(new Vector3(150f, 0f, 0f), TravelLeg.Foot);
			Assert.Equal(4, BoatRules.Ashore(road, 2, out int lastSea));
			Assert.Equal(3, lastSea);
			road = new TravelRoad();
			road.Add(Vector3.zero, TravelLeg.Foot);
			road.Add(new Vector3(70f, 0f, 0f), TravelLeg.Sea);
			Assert.Equal(2, BoatRules.Ashore(road, 1, out lastSea));
			Assert.Equal(1, lastSea);
		}

		[Theory]
		[InlineData(0, 6)]
		[InlineData(1, 10)]
		[InlineData(3, 25)]
		[InlineData(6, 44)]
		public void BiggerBoatsCarryMore(int biome, int expected)
		{
			Assert.Equal(expected, TravelTime.BoatCrew(biome));
		}

		[Fact]
		public void TheDrakkarsStepsAreSatOnEvenlySpaced()
		{
			foreach (string bench in new[] { "sit_bench", "sit_bench (4)" })
			{
				List<float> across = new List<BoatSeats.Plan>(BoatSeats.For("VikingShip_Ashlands")).FindAll((BoatSeats.Plan seat) => seat.Beside && seat.Chair == bench).ConvertAll((BoatSeats.Plan seat) => seat.X);
				across.Sort();
				Assert.Equal(4, across.Count);
				Assert.Equal(-across[0], across[across.Count - 1], 3);
				for (int i = 2; i < across.Count; i++)
				{
					Assert.Equal(across[1] - across[0], across[i] - across[i - 1], 3);
				}
			}
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(3)]
		[InlineData(6)]
		public void EveryBoatHasASeatForEachOfItsCrewTheHelmFirst(int biome)
		{
			BoatSeats.Plan[] plan = BoatSeats.For(TravelTime.BoatPrefab(biome));
			Assert.NotNull(plan);
			Assert.Equal(TravelTime.BoatCrew(biome), plan.Length);
			Assert.Equal(BoatSeats.Helm, plan[0].Chair);
			Assert.Single(plan, (BoatSeats.Plan seat) => seat.Chair == BoatSeats.Helm);
			Assert.Single(plan, (BoatSeats.Plan seat) => seat.Chair == "mast");
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(3)]
		[InlineData(6)]
		public void SpotsOnTheDeckKeepApart(int biome)
		{
			List<BoatSeats.Plan> deck = new List<BoatSeats.Plan>(BoatSeats.For(TravelTime.BoatPrefab(biome))).FindAll((BoatSeats.Plan seat) => seat.OnDeck);
			for (int i = 0; i < deck.Count; i++)
			{
				for (int j = i + 1; j < deck.Count; j++)
				{
					float apart = Mathf.Sqrt((deck[i].X - deck[j].X) * (deck[i].X - deck[j].X) + (deck[i].Z - deck[j].Z) * (deck[i].Z - deck[j].Z));
					Assert.True(apart >= 0.9f, "Two spots on the deck are only " + apart + " m apart");
				}
			}
		}

		[Fact]
		public void BoatsAreFoundByName()
		{
			Assert.Equal(0, TravelTime.BiomeOfBoat("raft"));
			Assert.Equal(1, TravelTime.BiomeOfBoat("Karve"));
			Assert.Equal(3, TravelTime.BiomeOfBoat("LONGSHIP"));
			Assert.Equal(6, TravelTime.BiomeOfBoat("drakkar"));
			Assert.Equal(-1, TravelTime.BiomeOfBoat("canoe"));
			Assert.Equal(3, TravelTime.BiomeOfBoat("VikingShip"));
			Assert.Equal(6, TravelTime.BiomeOfBoat("vikingship_ashlands"));
			Assert.Equal(new[] { "Raft", "Karve", "VikingShip", "VikingShip_Ashlands" }, TravelTime.BoatPrefabNames());
			Assert.Equal(new[] { "raft", "karve", "longship", "drakkar" }, TravelTime.BoatNames());
		}

		[Theory]
		[InlineData(6, 1, false, 1)]
		[InlineData(3, 1, false, 1)]
		[InlineData(1, 1, false, 1)]
		[InlineData(0, 1, false, 0)]
		[InlineData(6, 1, true, 6)]
		[InlineData(6, 2, false, 6)]
		[InlineData(3, 4, false, 3)]
		public void OneCrossingAloneTakesAKarveUnlessTheSeaBoils(int best, int crew, bool burning, int expected)
		{
			Assert.Equal(expected, TravelTime.CrossingBoatBiome(best, crew, burning));
		}

		[Fact]
		public void SwimLeftGoesByTheStaminaLeft()
		{
			// Unskilled, a second takes 5 stamina, so 50 lasts 10 seconds - 20 meters at 2 a second
			Assert.Equal(20f, BoatRules.SwimMetersLeft(50f, 0f, 1f, 2f), 3);
			Assert.Equal(0f, BoatRules.SwimMetersLeft(0f, 0f, 1f, 2f), 3);
			Assert.True(BoatRules.SwimMetersLeft(50f, 1f, 1f, 2f) > BoatRules.SwimMetersLeft(50f, 0f, 1f, 2f));
		}

		[Fact]
		public void AVikingOverboardSwimsOnlyForABoatWellWithinReach()
		{
			Assert.True(BoatRules.SwimsForBoat(40f, 20f));
			Assert.True(BoatRules.SwimsForBoat(40f, 40f * BoatRules.SwimMargin));
			Assert.False(BoatRules.SwimsForBoat(40f, 35f));
			Assert.False(BoatRules.SwimsForBoat(0f, 3f));
		}

		/// <summary>
		/// Makes a road from points, each leg on foot or by sea
		/// </summary>
		/// <param name="legs">Each point's x, along the road, and whether the leg to it is by sea - the first's ignored</param>
		/// <returns>The road</returns>
		private static TravelRoad RoadOf(params (float X, bool Sea)[] legs)
		{
			TravelRoad road = new TravelRoad();
			foreach ((float X, bool Sea) leg in legs)
			{
				road.Add(new Vector3(leg.X, 30f, 0f), (leg.Sea ? TravelLeg.Sea : TravelLeg.Foot));
			}
			return(road);
		}

		[Fact]
		public void AQuestSailsWhenItsRoadTakesToTheSeaByTheVillage()
		{
			TravelRoad road = RoadOf((0f, false), (50f, false), (110f, true), (300f, true), (450f, false), (500f, false));
			TravelRoad sea = BoatRules.QuestSea(road, Vector3.zero);
			Assert.NotNull(sea);
			Assert.Equal(new Vector3(50f, 30f, 0f), sea.Points[0]);
			Assert.Equal(new Vector3(450f, 30f, 0f), sea.Points[sea.Count - 1]);
			Assert.True(sea.SeaLeg(1));
			Assert.True(sea.SeaLeg(2));
			Assert.False(sea.SeaLeg(3));
		}

		[Fact]
		public void AQuestWalksWhenTheSeaIsFarOffShortOrPastAPortal()
		{
			// The shore 200 meters off the village
			Assert.Null(BoatRules.QuestSea(RoadOf((0f, false), (200f, false), (500f, true), (700f, false)), Vector3.zero));
			// Only 100 meters of sea
			Assert.Null(BoatRules.QuestSea(RoadOf((0f, false), (50f, false), (150f, true), (200f, false)), Vector3.zero));
			// No sea at all
			Assert.Null(BoatRules.QuestSea(RoadOf((0f, false), (500f, false)), Vector3.zero));
			TravelRoad portal = RoadOf((0f, false), (10f, false));
			portal.Add(new Vector3(20f, 30f, 0f), TravelLeg.Portal);
			portal.Add(new Vector3(400f, 30f, 0f), TravelLeg.Sea);
			Assert.Null(BoatRules.QuestSea(portal, Vector3.zero));
		}

		[Fact]
		public void TheVillagesOwnBoatsFillUpFirst()
		{
			List<string> ids = new List<string> { "a", "b", "c", "d", "e", "f", "g" };
			List<List<string>> built = BoatRules.QuestCrews(ids, new List<int> { 4 }, 6, out List<List<string>> owned);
			Assert.Single(owned);
			Assert.Equal(new List<string> { "a", "b", "c", "d" }, owned[0]);
			Assert.Single(built);
			Assert.Equal(3, built[0].Count);
			// The village's boats hold them all, so none is built, and a boat not needed isn't taken
			built = BoatRules.QuestCrews(new List<string> { "a", "b" }, new List<int> { 10, 6 }, 6, out owned);
			Assert.Single(owned);
			Assert.Empty(built);
			// No boat of the village's, so all are built for
			built = BoatRules.QuestCrews(new List<string> { "a", "b", "c" }, new List<int>(), 2, out owned);
			Assert.Empty(owned);
			Assert.Equal(2, built.Count);
		}

		[Fact]
		public void AQuestsBoatsComeBackFromOutAtSea()
		{
			TravelRoad sea = RoadOf((0f, false), (100f, true), (300f, true), (450f, false));
			Vector3 start = BoatRules.SailInFrom(sea, out TravelRoad home);
			Assert.Equal(BoatRules.QuestSailIn, start.x, 2);
			Assert.Equal(start, home.Points[0]);
			Assert.Equal(new Vector3(100f, 30f, 0f), home.Points[home.Count - 1]);
			Assert.True(home.SeaLeg(home.Count - 1));
			// A sea shorter than that, they come back from its far end
			TravelRoad short1 = RoadOf((0f, false), (80f, true), (120f, false));
			Assert.Equal(80f, BoatRules.SailInFrom(short1, out TravelRoad _).x, 2);
		}

		[Fact]
		public void ABoatComesInUntilItsNearShallowOrAground()
		{
			Assert.False(BoatRules.Beached(25f, 6f, 0f));
			Assert.True(BoatRules.Beached(BoatRules.ShoreNear, 6f, 0f));
			Assert.True(BoatRules.Beached(25f, 1f, 0f));
			Assert.True(BoatRules.Beached(25f, -0.5f, 0f));
			Assert.True(BoatRules.Beached(25f, 6f, BoatRules.BeachStall));
			Assert.False(BoatRules.Beached(25f, BoatRules.ShoalDepth, BoatRules.BeachStall - 1f));
		}

		[Fact]
		public void ABoatSlowsAndStopsForSomeoneOverboard()
		{
			Assert.Equal(Ship.Speed.Half, BoatRules.RescueSpeed(100f));
			Assert.Equal(Ship.Speed.Slow, BoatRules.RescueSpeed(BoatRules.RescueSlow));
			Assert.Equal(Ship.Speed.Stop, BoatRules.RescueSpeed(BoatRules.RescueStop));
			Assert.Equal(Ship.Speed.Stop, BoatRules.RescueSpeed(2f));
		}
	}
}
