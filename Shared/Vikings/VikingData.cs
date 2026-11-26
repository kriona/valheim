using System.Collections.Generic;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// Who a zombie viking was and how it looks and fights - for a tombstone viking, handed from the player's death to
	/// their corpse and on to the viking, and for a viking of the world, rolled when it first loads
	/// </summary>
	internal class VikingData
	{
		private const int Version = 3;

		public string Name = "";
		public long PlayerId;
		public int Model;
		public Vector3 SkinColor = Vector3.one;
		public Vector3 HairColor = Vector3.one;
		public string Beard = "";
		public string Hair = "";
		public float MaxHealth;
		public int Width = Humanoid.DefaultInventoryWidth;
		public int Height = Humanoid.DefaultInventoryHeight;

		/// <summary>
		/// Where the weapon the player was wielding sits in the viking's inventory, or -1, -1 when they had none
		/// </summary>
		public Vector2i WeaponSlot = new Vector2i(-1, -1);

		/// <summary>
		/// The name hash of the forsaken power the player could use when they died, or 0
		/// </summary>
		public int Power;

		/// <summary>
		/// The ground under the player's tombstone, or where they fell when it left none, which the viking guards
		/// </summary>
		public Vector3 Home;

		/// <summary>
		/// Whether Home is set, which it isn't in data written before vikings had a home
		/// </summary>
		public bool HasHome;

		/// <summary>
		/// Whether the viking is a player's, risen by their tombstone, rather than one of the world's
		/// </summary>
		public bool Tombstone = true;

		/// <summary>
		/// The index in VikingGear's tables of the biome the viking's gear, coins and fists come from
		/// </summary>
		public int Biome = -1;

		/// <summary>
		/// A viking of the world's level in every skill, from 0 to 100
		/// </summary>
		public float Skill;

		/// <summary>
		/// A tombstone viking's level in each of the player's skills when they died, by skill type
		/// </summary>
		public Dictionary<int, float> Skills = new Dictionary<int, float>();

		/// <summary>
		/// Checks whether the viking has a beard
		/// </summary>
		/// <returns>True when it has</returns>
		public bool HasBeard()
		{
			return(!string.IsNullOrEmpty(Beard) && Beard != VikingGear.NoBeard);
		}

		/// <summary>
		/// Writes the data to bytes for a ZDO
		/// </summary>
		/// <returns>The bytes</returns>
		public byte[] ToBytes()
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(Version);
			pkg.Write(Name);
			pkg.Write(PlayerId);
			pkg.Write(Model);
			pkg.Write(SkinColor);
			pkg.Write(HairColor);
			pkg.Write(Beard);
			pkg.Write(Hair);
			pkg.Write(MaxHealth);
			pkg.Write(Width);
			pkg.Write(Height);
			pkg.Write(WeaponSlot);
			pkg.Write(Power);
			pkg.Write(Home);
			pkg.Write(Tombstone);
			pkg.Write(Biome);
			pkg.Write(Skill);
			pkg.Write(Skills.Count);
			foreach (KeyValuePair<int, float> skill in Skills)
			{
				pkg.Write(skill.Key);
				pkg.Write(skill.Value);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads data written by ToBytes
		/// </summary>
		/// <param name="bytes">The bytes, or null</param>
		/// <returns>The data, or null when there were no bytes</returns>
		public static VikingData FromBytes(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0)
			{
				return(null);
			}
			ZPackage pkg = new ZPackage(bytes);
			int version = pkg.ReadInt();
			VikingData data = new VikingData();
			data.Name = pkg.ReadString();
			data.PlayerId = pkg.ReadLong();
			data.Model = pkg.ReadInt();
			data.SkinColor = pkg.ReadVector3();
			data.HairColor = pkg.ReadVector3();
			data.Beard = pkg.ReadString();
			data.Hair = pkg.ReadString();
			data.MaxHealth = pkg.ReadSingle();
			data.Width = pkg.ReadInt();
			data.Height = pkg.ReadInt();
			data.WeaponSlot = pkg.ReadVector2i();
			data.Power = pkg.ReadInt();
			if (version >= 2)
			{
				data.Home = pkg.ReadVector3();
				data.HasHome = true;
			}
			if (version >= 3)
			{
				data.Tombstone = pkg.ReadBool();
				data.Biome = pkg.ReadInt();
				data.Skill = pkg.ReadSingle();
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					int skill = pkg.ReadInt();
					data.Skills[skill] = pkg.ReadSingle();
				}
			}
			return(data);
		}
	}
}
