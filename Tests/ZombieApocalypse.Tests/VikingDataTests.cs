using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests the record of who a viking is and how it looks, saved on its ZDO - reading back what's written, and
	/// reading data written by older versions with defaults for what they didn't have
	/// </summary>
	public class VikingDataTests
	{
		/// <summary>
		/// Makes a viking's data with every field set away from its default
		/// </summary>
		/// <param name="skills">Its levels in each skill, by skill type</param>
		/// <returns>The data</returns>
		private static VikingData Full(Dictionary<int, float> skills)
		{
			return(new VikingData
			{
				Name = "Ragnhild",
				PlayerId = 1234567890123L,
				Model = 1,
				SkinColor = new Vector3(0.9f, 0.7f, 0.6f),
				HairColor = new Vector3(0.2f, 0.1f, 0.05f),
				Beard = "Beard3",
				Hair = "Hair12",
				MaxHealth = 87.5f,
				Width = 8,
				Height = 5,
				WeaponSlot = new Vector2i(3, 1),
				Power = 98765,
				Home = new Vector3(-1234.5f, 42.25f, 678.75f),
				HasHome = true,
				Tombstone = false,
				Biome = 4,
				Skill = 37.5f,
				Skills = skills
			});
		}

		/// <summary>
		/// Writes the fields every version has, the way the first version did
		/// </summary>
		/// <param name="pkg">The package to write to</param>
		/// <param name="version">The version to write at its start</param>
		private static void WriteFirstFields(ZPackage pkg, int version)
		{
			pkg.Write(version);
			pkg.Write("Torvald");
			pkg.Write(42L);
			pkg.Write(0);
			pkg.Write(new Vector3(0.5f, 0.4f, 0.3f));
			pkg.Write(new Vector3(0.1f, 0.2f, 0.3f));
			pkg.Write("Beard5");
			pkg.Write("Hair3");
			pkg.Write(120f);
			pkg.Write(8);
			pkg.Write(4);
			pkg.Write(new Vector2i(2, 0));
			pkg.Write(555);
		}

		/// <summary>
		/// Checks that the fields every version has read back as written
		/// </summary>
		/// <param name="data">The data read</param>
		private static void AssertFirstFields(VikingData data)
		{
			Assert.NotNull(data);
			Assert.Equal("Torvald", data.Name);
			Assert.Equal(42L, data.PlayerId);
			Assert.Equal(0, data.Model);
			Assert.Equal(new Vector3(0.5f, 0.4f, 0.3f), data.SkinColor);
			Assert.Equal(new Vector3(0.1f, 0.2f, 0.3f), data.HairColor);
			Assert.Equal("Beard5", data.Beard);
			Assert.Equal("Hair3", data.Hair);
			Assert.Equal(120f, data.MaxHealth);
			Assert.Equal(8, data.Width);
			Assert.Equal(4, data.Height);
			Assert.Equal(2, data.WeaponSlot.x);
			Assert.Equal(0, data.WeaponSlot.y);
			Assert.Equal(555, data.Power);
		}

		/// <summary>
		/// Checks that every field reads back as written, with no skills, one skill and several
		/// </summary>
		/// <param name="skillCount">How many skills the viking has levels in</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(4)]
		public void ReadsBackEveryField(int skillCount)
		{
			Dictionary<int, float> skills = new Dictionary<int, float>();
			for (int i = 0; i < skillCount; i++)
			{
				skills[i + 1] = 10.25f * (i + 1);
			}
			VikingData written = Full(skills);
			VikingData read = VikingData.FromBytes(written.ToBytes());
			Assert.NotNull(read);
			Assert.Equal(written.Name, read.Name);
			Assert.Equal(written.PlayerId, read.PlayerId);
			Assert.Equal(written.Model, read.Model);
			Assert.Equal(written.SkinColor, read.SkinColor);
			Assert.Equal(written.HairColor, read.HairColor);
			Assert.Equal(written.Beard, read.Beard);
			Assert.Equal(written.Hair, read.Hair);
			Assert.Equal(written.MaxHealth, read.MaxHealth);
			Assert.Equal(written.Width, read.Width);
			Assert.Equal(written.Height, read.Height);
			Assert.Equal(3, read.WeaponSlot.x);
			Assert.Equal(1, read.WeaponSlot.y);
			Assert.Equal(written.Power, read.Power);
			Assert.Equal(written.Home, read.Home);
			Assert.True(read.HasHome);
			Assert.False(read.Tombstone);
			Assert.Equal(4, read.Biome);
			Assert.Equal(37.5f, read.Skill);
			Assert.Equal(skillCount, read.Skills.Count);
			foreach (KeyValuePair<int, float> skill in skills)
			{
				Assert.Equal(skill.Value, read.Skills[skill.Key]);
			}
		}

		/// <summary>
		/// Checks that a weapon slot of -1, -1 for no weapon reads back
		/// </summary>
		[Fact]
		public void ReadsBackNoWeapon()
		{
			VikingData read = VikingData.FromBytes(new VikingData().ToBytes());
			Assert.Equal(-1, read.WeaponSlot.x);
			Assert.Equal(-1, read.WeaponSlot.y);
			Assert.True(read.Tombstone);
			Assert.Equal(-1, read.Biome);
		}

		/// <summary>
		/// Checks that data from the first version reads with no home, as a tombstone viking with no biome or skills
		/// </summary>
		[Fact]
		public void ReadsTheFirstVersion()
		{
			ZPackage pkg = new ZPackage();
			WriteFirstFields(pkg, 1);
			VikingData data = VikingData.FromBytes(pkg.GetArray());
			AssertFirstFields(data);
			Assert.False(data.HasHome);
			Assert.Equal(Vector3.zero, data.Home);
			Assert.True(data.Tombstone);
			Assert.Equal(-1, data.Biome);
			Assert.Equal(0f, data.Skill);
			Assert.Empty(data.Skills);
		}

		/// <summary>
		/// Checks that data from the second version reads its home, as a tombstone viking with no biome or skills
		/// </summary>
		[Fact]
		public void ReadsTheSecondVersion()
		{
			ZPackage pkg = new ZPackage();
			WriteFirstFields(pkg, 2);
			pkg.Write(new Vector3(10f, 20f, 30f));
			VikingData data = VikingData.FromBytes(pkg.GetArray());
			AssertFirstFields(data);
			Assert.True(data.HasHome);
			Assert.Equal(new Vector3(10f, 20f, 30f), data.Home);
			Assert.True(data.Tombstone);
			Assert.Equal(-1, data.Biome);
			Assert.Equal(0f, data.Skill);
			Assert.Empty(data.Skills);
		}

		/// <summary>
		/// Checks that no bytes, or none at all, read as no data
		/// </summary>
		[Fact]
		public void ReadsNoBytesAsNoData()
		{
			Assert.Null(VikingData.FromBytes(null));
			Assert.Null(VikingData.FromBytes(new byte[0]));
		}

		/// <summary>
		/// Checks that a viking has a beard only when it has one other than the game's beardless one
		/// </summary>
		/// <param name="beard">The beard's prefab name</param>
		/// <param name="expected">Whether it has a beard</param>
		[Theory]
		[InlineData("Beard3", true)]
		[InlineData("BeardNone", false)]
		[InlineData("", false)]
		[InlineData(null, false)]
		public void HasABeardUnlessBeardless(string beard, bool expected)
		{
			Assert.Equal(expected, new VikingData { Beard = beard }.HasBeard());
		}
	}
}
