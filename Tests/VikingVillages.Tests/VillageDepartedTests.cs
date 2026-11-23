using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how the vikings that left a village are kept - each one's values, and the list of those kept at a bonfire
	/// or village
	/// </summary>
	public class VillageDepartedTests
	{
		/// <summary>
		/// Makes a viking's values with one or two of every type
		/// </summary>
		/// <returns>The values</returns>
		private static VillageDeparted.VikingValues EveryType()
		{
			VillageDeparted.VikingValues values = new VillageDeparted.VikingValues();
			values.Floats.Add(new KeyValuePair<int, float>(1, 2.5f));
			values.Floats.Add(new KeyValuePair<int, float>(-7, -0.25f));
			values.Vec3s.Add(new KeyValuePair<int, Vector3>(2, new Vector3(1f, -2f, 3.5f)));
			values.Quats.Add(new KeyValuePair<int, Quaternion>(3, new Quaternion(0.1f, 0.2f, 0.3f, 0.9f)));
			values.Ints.Add(new KeyValuePair<int, int>(4, -12345));
			values.Longs.Add(new KeyValuePair<int, long>(5, 638000000000000000L));
			values.Strings.Add(new KeyValuePair<int, string>(6, "Bjorn"));
			values.Strings.Add(new KeyValuePair<int, string>(8, ""));
			values.Bytes.Add(new KeyValuePair<int, byte[]>(7, new byte[] { 9, 8, 7 }));
			return(values);
		}

		/// <summary>
		/// Checks that every type of value reads back as it was written, by its hash
		/// </summary>
		[Fact]
		public void RoundTripsEveryType()
		{
			VillageDeparted.VikingValues read = VillageDeparted.Decode(VillageDeparted.Encode(EveryType(), new HashSet<int>()));
			Assert.NotNull(read);
			Assert.Equal(2, read.Floats.Count);
			Assert.Equal(1, read.Floats[0].Key);
			Assert.Equal(2.5f, read.Floats[0].Value);
			Assert.Equal(-7, read.Floats[1].Key);
			Assert.Equal(-0.25f, read.Floats[1].Value);
			Assert.Single(read.Vec3s);
			Assert.Equal(2, read.Vec3s[0].Key);
			Assert.Equal(1f, read.Vec3s[0].Value.x);
			Assert.Equal(-2f, read.Vec3s[0].Value.y);
			Assert.Equal(3.5f, read.Vec3s[0].Value.z);
			Assert.Single(read.Quats);
			Assert.Equal(3, read.Quats[0].Key);
			Assert.Equal(0.1f, read.Quats[0].Value.x);
			Assert.Equal(0.2f, read.Quats[0].Value.y);
			Assert.Equal(0.3f, read.Quats[0].Value.z);
			Assert.Equal(0.9f, read.Quats[0].Value.w);
			Assert.Equal(new List<KeyValuePair<int, int>> { new KeyValuePair<int, int>(4, -12345) }, read.Ints);
			Assert.Equal(new List<KeyValuePair<int, long>> { new KeyValuePair<int, long>(5, 638000000000000000L) }, read.Longs);
			Assert.Equal(new List<KeyValuePair<int, string>> { new KeyValuePair<int, string>(6, "Bjorn"), new KeyValuePair<int, string>(8, "") }, read.Strings);
			Assert.Single(read.Bytes);
			Assert.Equal(7, read.Bytes[0].Key);
			Assert.Equal(new byte[] { 9, 8, 7 }, read.Bytes[0].Value);
		}

		/// <summary>
		/// Checks that a viking with no values reads back with none
		/// </summary>
		[Fact]
		public void RoundTripsNoValues()
		{
			VillageDeparted.VikingValues read = VillageDeparted.Decode(VillageDeparted.Encode(new VillageDeparted.VikingValues(), new HashSet<int>()));
			Assert.NotNull(read);
			Assert.Empty(read.Floats);
			Assert.Empty(read.Vec3s);
			Assert.Empty(read.Quats);
			Assert.Empty(read.Ints);
			Assert.Empty(read.Longs);
			Assert.Empty(read.Strings);
			Assert.Empty(read.Bytes);
		}

		/// <summary>
		/// Checks that the values to skip are left out, whatever their type, and the rest are kept
		/// </summary>
		[Fact]
		public void LeavesOutSkippedValues()
		{
			VillageDeparted.VikingValues read = VillageDeparted.Decode(VillageDeparted.Encode(EveryType(), new HashSet<int> { 1, 3, 6, 7 }));
			Assert.Single(read.Floats);
			Assert.Equal(-7, read.Floats[0].Key);
			Assert.Single(read.Vec3s);
			Assert.Empty(read.Quats);
			Assert.Single(read.Ints);
			Assert.Single(read.Longs);
			Assert.Single(read.Strings);
			Assert.Equal(8, read.Strings[0].Key);
			Assert.Empty(read.Bytes);
		}

		/// <summary>
		/// Checks that a viking's values cut off anywhere before their end can't be read at all - including partway
		/// through the last byte array - so none of them is used
		/// </summary>
		[Fact]
		public void TruncatedValuesReadAsNothing()
		{
			byte[] bytes = VillageDeparted.Encode(EveryType(), new HashSet<int>());
			for (int length = 0; length < bytes.Length; length++)
			{
				Assert.Null(VillageDeparted.Decode(bytes[..length]));
			}
		}

		/// <summary>
		/// Checks that the vikings kept read back in the order they left
		/// </summary>
		[Fact]
		public void RoundTripsTheKept()
		{
			List<byte[]> kept = new List<byte[]> { new byte[] { 1 }, new byte[0], new byte[] { 2, 3 } };
			List<byte[]> read = VillageDeparted.DecodeKept(VillageDeparted.EncodeKept(kept));
			Assert.Equal(3, read.Count);
			Assert.Equal(new byte[] { 1 }, read[0]);
			Assert.Empty(read[1]);
			Assert.Equal(new byte[] { 2, 3 }, read[2]);
		}

		/// <summary>
		/// Checks that no vikings kept read back as none
		/// </summary>
		[Fact]
		public void RoundTripsNoneKept()
		{
			Assert.Empty(VillageDeparted.DecodeKept(VillageDeparted.EncodeKept(new List<byte[]>())));
		}

		/// <summary>
		/// Checks that of 70 vikings kept, the last 64 to leave are written, in the order they left
		/// </summary>
		[Fact]
		public void KeepsTheLast64()
		{
			List<byte[]> kept = new List<byte[]>();
			for (int i = 0; i < 70; i++)
			{
				kept.Add(new byte[] { (byte)i });
			}
			List<byte[]> read = VillageDeparted.DecodeKept(VillageDeparted.EncodeKept(kept));
			Assert.Equal(64, read.Count);
			Assert.Equal((byte)6, read[0][0]);
			Assert.Equal((byte)69, read[63][0]);
		}

		/// <summary>
		/// Checks that bytes too short to hold a count, or cut off partway through a viking, can't be read
		/// </summary>
		[Fact]
		public void CorruptKeptReadsAsNothing()
		{
			Assert.Null(VillageDeparted.DecodeKept(new byte[] { 1, 2 }));
			byte[] bytes = VillageDeparted.EncodeKept(new List<byte[]> { new byte[] { 1, 2, 3, 4 } });
			Assert.Null(VillageDeparted.DecodeKept(bytes[..(bytes.Length - 1)]));
		}
	}
}
