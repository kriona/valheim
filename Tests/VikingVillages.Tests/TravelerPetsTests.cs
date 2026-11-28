using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the animals traveling vikings bring - which kind comes from each biome, and the names they're given
	/// </summary>
	public class TravelerPetsTests
	{
		/// <summary>
		/// Checks that each biome a traveler can come from has its own animal - a wolf from the Mountains, which keeps by
		/// its traveler with no saddle, and a saddled lox, valkyrie, asksvin and moose, which wait at the village's edge
		/// </summary>
		[Fact]
		public void EachHardBiomeHasItsAnimal()
		{
			Assert.Equal(5, TravelerPets.Kinds.Count);
			TravelerPets.Kind wolf = TravelerPets.Kinds[Heightmap.Biome.Mountain];
			Assert.Equal("Wolf", wolf.Prefab);
			Assert.True(wolf.Follows);
			Assert.False(wolf.Saddled);
			Dictionary<Heightmap.Biome, string> mounts = new Dictionary<Heightmap.Biome, string>
			{
				{ Heightmap.Biome.Plains, "Lox" },
				{ Heightmap.Biome.Mistlands, "VikingValkyrie" },
				{ Heightmap.Biome.AshLands, "Asksvin" },
				{ Heightmap.Biome.DeepNorth, "Moose" }
			};
			foreach (KeyValuePair<Heightmap.Biome, string> mount in mounts)
			{
				TravelerPets.Kind kind = TravelerPets.Kinds[mount.Key];
				Assert.Equal(mount.Value, kind.Prefab);
				Assert.True(kind.Saddled);
				Assert.False(kind.Follows);
			}
		}

		/// <summary>
		/// Checks that the lox, asksvin, moose and valkyrie are ridden and the wolf isn't, and that only the valkyrie flies
		/// </summary>
		[Fact]
		public void MountsAreRidden()
		{
			Assert.False(TravelerPets.Kinds[Heightmap.Biome.Mountain].Ridden);
			Assert.True(TravelerPets.Kinds[Heightmap.Biome.Plains].Ridden);
			Assert.True(TravelerPets.Kinds[Heightmap.Biome.Mistlands].Ridden);
			Assert.True(TravelerPets.Kinds[Heightmap.Biome.AshLands].Ridden);
			Assert.True(TravelerPets.Kinds[Heightmap.Biome.DeepNorth].Ridden);
			foreach (KeyValuePair<Heightmap.Biome, TravelerPets.Kind> pair in TravelerPets.Kinds)
			{
				Assert.Equal(pair.Key == Heightmap.Biome.Mistlands, pair.Value.Flies);
			}
		}

		/// <summary>
		/// Checks that every kind has names, none of them twice
		/// </summary>
		[Fact]
		public void EveryKindHasNamesOnce()
		{
			foreach (TravelerPets.Kind kind in TravelerPets.Kinds.Values)
			{
				Assert.NotEmpty(kind.Names);
				Assert.Equal(kind.Names.Length, new HashSet<string>(kind.Names).Count);
			}
		}

		/// <summary>
		/// Checks that an animal is never given its owner's first name, whatever its case or byname
		/// </summary>
		/// <param name="owner">The owner's name</param>
		[Theory]
		[InlineData("Sven")]
		[InlineData("sven")]
		[InlineData("Sven the Red")]
		public void PickNameSkipsTheOwnersName(string owner)
		{
			string[] names = { "Kristoff", "Sven" };
			Assert.Equal("Kristoff", TravelerPets.PickName(names, owner, 0f));
			Assert.Equal("Kristoff", TravelerPets.PickName(names, owner, 0.99f));
		}

		/// <summary>
		/// Checks that each name is as likely, from the first to the last
		/// </summary>
		/// <param name="roll">The roll</param>
		/// <param name="expected">The name</param>
		[Theory]
		[InlineData(0f, "Kristoff")]
		[InlineData(0.49f, "Kristoff")]
		[InlineData(0.5f, "Sven")]
		[InlineData(1f, "Sven")]
		public void PickNameSpreadsEvenly(float roll, string expected)
		{
			Assert.Equal(expected, TravelerPets.PickName(new[] { "Kristoff", "Sven" }, "Astrid", roll));
		}

		/// <summary>
		/// Checks that no name is left when the only one is the owner's
		/// </summary>
		[Fact]
		public void PickNameWithNoneLeft()
		{
			Assert.Equal("", TravelerPets.PickName(new[] { "Sven" }, "Sven", 0.5f));
			Assert.Equal("Sven", TravelerPets.PickName(new[] { "Sven" }, "", 0.5f));
		}

		/// <summary>
		/// Checks that half the travelers bring an animal, with at most two stars
		/// </summary>
		[Fact]
		public void HalfBringOneWithUpToTwoStars()
		{
			Assert.Equal(0.5f, TravelerPets.PetChance);
			Assert.Equal(2, TravelerPets.MaxStars);
		}
	}
}
