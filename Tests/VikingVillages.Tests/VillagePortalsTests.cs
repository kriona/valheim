using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the portals between villages - which biomes' villages get one and which they can lead to, how a portal is
	/// named apart from the world's other portals, and which way it faces
	/// </summary>
	public class VillagePortalsTests
	{
		/// <summary>
		/// Checks that only the Ashlands' and the Deep North's villages get a portal
		/// </summary>
		/// <param name="biome">The village's biome</param>
		/// <param name="expected">Whether it gets one</param>
		[Theory]
		[InlineData(Heightmap.Biome.AshLands, true)]
		[InlineData(Heightmap.Biome.DeepNorth, true)]
		[InlineData(Heightmap.Biome.Meadows, false)]
		[InlineData(Heightmap.Biome.Plains, false)]
		[InlineData(Heightmap.Biome.Mistlands, false)]
		[InlineData(Heightmap.Biome.Ocean, false)]
		[InlineData(Heightmap.Biome.None, false)]
		[InlineData(Heightmap.Biome.AshLands | Heightmap.Biome.Meadows, false)]
		public void GivesAPortalToTheHardestBiomes(Heightmap.Biome biome, bool expected)
		{
			Assert.Equal(expected, VillagePortals.GetsPortal(biome));
		}

		/// <summary>
		/// Checks that a portal leads to a village of the Plains or an easier biome, and nowhere else
		/// </summary>
		/// <param name="biome">The village's biome</param>
		/// <param name="expected">Whether a portal can lead there</param>
		[Theory]
		[InlineData(Heightmap.Biome.Meadows, true)]
		[InlineData(Heightmap.Biome.BlackForest, true)]
		[InlineData(Heightmap.Biome.Swamp, true)]
		[InlineData(Heightmap.Biome.Mountain, true)]
		[InlineData(Heightmap.Biome.Plains, true)]
		[InlineData(Heightmap.Biome.Mistlands, false)]
		[InlineData(Heightmap.Biome.AshLands, false)]
		[InlineData(Heightmap.Biome.DeepNorth, false)]
		[InlineData(Heightmap.Biome.Ocean, false)]
		[InlineData(Heightmap.Biome.None, false)]
		[InlineData(Heightmap.Biome.Plains | Heightmap.Biome.Mistlands, false)]
		public void LeadsToThePlainsOrEasier(Heightmap.Biome biome, bool expected)
		{
			Assert.Equal(expected, VillagePortals.IsDestination(biome));
		}

		/// <summary>
		/// Checks that a portal takes its village's name when no portal has it
		/// </summary>
		[Fact]
		public void NamesAPortalAfterItsVillage()
		{
			Assert.Equal("Hedeby", VillagePortals.UniqueTag("Hedeby", new List<string> { "Kaupang", "", "Hedeby #2" }));
		}

		/// <summary>
		/// Checks that a portal whose village's name another portal has gets the first number after it no portal has,
		/// whatever case the other portals' names are in
		/// </summary>
		[Fact]
		public void NumbersAPortalWhoseNameIsTaken()
		{
			Assert.Equal("Hedeby #2", VillagePortals.UniqueTag("Hedeby", new List<string> { "Hedeby" }));
			Assert.Equal("Hedeby #3", VillagePortals.UniqueTag("Hedeby", new List<string> { "hedeby", "HEDEBY #2", "Hedeby #4" }));
		}

		/// <summary>
		/// Checks that a long village name is cut short to fit a portal's name, number and all, with no space left before
		/// the number
		/// </summary>
		[Fact]
		public void CutsALongNameShortToFit()
		{
			Assert.Equal("Gamla Upps", VillagePortals.UniqueTag("Gamla Uppsala", new List<string>()));
			Assert.Equal("Gamla U #2", VillagePortals.UniqueTag("Gamla Uppsala", new List<string> { "Gamla Upps" }));
			Assert.Equal("Hedeby #2", VillagePortals.UniqueTag("Hedeby Mor", new List<string> { "Hedeby Mor" }));
			Assert.Equal("Frost", VillagePortals.FitTag("Frost   ", ""));
		}

		/// <summary>
		/// Checks that a portal's name never runs past what the box for naming a portal takes, however many portals have
		/// its village's name
		/// </summary>
		[Fact]
		public void KeepsEveryNameWithinTheLimit()
		{
			List<string> taken = new List<string>();
			for (int i = 0; i < 120; i++)
			{
				string tag = VillagePortals.UniqueTag("Njardarheimr", taken);
				Assert.True(tag.Length <= VillagePortals.TagMax, "\"" + tag + "\" is too long");
				Assert.DoesNotContain(tag, taken);
				taken.Add(tag);
			}
		}

		/// <summary>
		/// Checks that there's no nearest point among none
		/// </summary>
		[Fact]
		public void FindsNoNearestPointAmongNone()
		{
			Assert.False(VillagePortals.NearestPoint(new List<Vector3>(), Vector3.zero, out Vector3 _));
		}

		/// <summary>
		/// Checks that the nearest point is found across the ground, whatever its height
		/// </summary>
		[Fact]
		public void FindsTheNearestPointAcrossTheGround()
		{
			List<Vector3> points = new List<Vector3> { new Vector3(10f, 0f, 0f), new Vector3(3f, 50f, 4f), new Vector3(-6f, 0f, 0f) };
			Assert.True(VillagePortals.NearestPoint(points, Vector3.zero, out Vector3 nearest));
			Assert.Equal(new Vector3(3f, 50f, 4f), nearest);
		}

		/// <summary>
		/// Checks that a portal faces where its path heads, flat across the ground
		/// </summary>
		[Fact]
		public void FacesWhereItsPathHeads()
		{
			Vector3 facing = VillagePortals.Facing(new Vector3(1f, 2f, 1f), new Vector3(4f, 10f, 5f), Vector3.zero);
			Assert.Equal(0.6f, facing.x, 3);
			Assert.Equal(0f, facing.y, 3);
			Assert.Equal(0.8f, facing.z, 3);
		}

		/// <summary>
		/// Checks that a portal whose path heads to where it stands faces the fallback point, or north when that's where
		/// it stands too
		/// </summary>
		[Fact]
		public void FacesTheFallbackWhenItsPathHeadsNowhere()
		{
			Vector3 facing = VillagePortals.Facing(new Vector3(1f, 0f, 1f), new Vector3(1f, 5f, 1f), new Vector3(-3f, 0f, 1f));
			Assert.Equal(-1f, facing.x, 3);
			Assert.Equal(0f, facing.z, 3);
			Assert.Equal(Vector3.forward, VillagePortals.Facing(Vector3.one, Vector3.one, Vector3.one));
		}
	}
}
