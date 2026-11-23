using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a village's noted pieces are written and read, told apart from what stands, and how its houses are
	/// judged standing or in ruins
	/// </summary>
	public class VillageRecordsTests
	{
		/// <summary>
		/// One step of a written angle - a 65536th of a turn, in degrees
		/// </summary>
		private const float AngleStep = 360f / 65536f;

		/// <summary>
		/// Checks that angles are written as 65536ths of a turn, wrapping angles below zero or past a full turn
		/// </summary>
		/// <param name="degrees">The angle, in degrees</param>
		/// <param name="expected">The angle as written</param>
		[Theory]
		[InlineData(0f, 0)]
		[InlineData(90f, 16384)]
		[InlineData(180f, 32768)]
		[InlineData(270f, 49152)]
		[InlineData(360f, 0)]
		[InlineData(450f, 16384)]
		[InlineData(-90f, 49152)]
		[InlineData(-360f, 0)]
		public void AngleWritesFractionsOfATurn(float degrees, int expected)
		{
			Assert.Equal(expected, VillageRecords.Angle(degrees));
		}

		/// <summary>
		/// Checks that an angle a hair short of a full turn wraps round to zero rather than overflowing
		/// </summary>
		[Fact]
		public void AngleWrapsJustShortOfAFullTurn()
		{
			Assert.Equal(0, VillageRecords.Angle(359.999f));
		}

		/// <summary>
		/// Checks that pieces read back where they stood, to the centimeter, and facing the way they faced, to a step of
		/// the written angles, in the order they were written
		/// </summary>
		[Fact]
		public void PiecesReadBackWhereTheyStood()
		{
			Vector3 origin = new Vector3(1000f, 30f, -2000f);
			List<VillageRecords.WrittenPiece> pieces = new List<VillageRecords.WrittenPiece>
			{
				Piece(111, new Vector3(1012.343f, 31.502f, -1987.658f), new Vector3(0f, 90.5f, 0f), 4),
				Piece(222, new Vector3(990.001f, 29.997f, -2003.212f), new Vector3(10f, 271.25f, 359.5f), 7),
				Piece(111, new Vector3(1000f, 30f, -2000f), new Vector3(45f, 0f, 180f), 4)
			};
			List<VillageRecords.WrittenPiece> read = VillageRecords.Decode(VillageRecords.Encode(origin, pieces));
			Assert.Equal(pieces.Count, read.Count);
			for (int i = 0; i < pieces.Count; i++)
			{
				Assert.Equal(pieces[i].Prefab, read[i].Prefab);
				AssertNear(pieces[i].Position, read[i].Position, 0.005f);
				AssertSameAngles(pieces[i].Angles, read[i].Angles);
			}
		}

		/// <summary>
		/// Checks that each prefab's wood is written once, so every piece of a prefab reads back with the wood of the
		/// first of them written, and two prefabs keep a wood of their own
		/// </summary>
		[Fact]
		public void EachPrefabKeepsItsWood()
		{
			Vector3 origin = Vector3.zero;
			List<VillageRecords.WrittenPiece> pieces = new List<VillageRecords.WrittenPiece>
			{
				Piece(111, new Vector3(1f, 0f, 0f), Vector3.zero, 4),
				Piece(222, new Vector3(2f, 0f, 0f), Vector3.zero, 7),
				Piece(111, new Vector3(3f, 0f, 0f), Vector3.zero, 99)
			};
			List<VillageRecords.WrittenPiece> read = VillageRecords.Decode(VillageRecords.Encode(origin, pieces));
			Assert.Equal(new[] { 111, 222, 111 }, read.ConvertAll((VillageRecords.WrittenPiece piece) => piece.Prefab));
			Assert.Equal(new[] { 4, 7, 4 }, read.ConvertAll((VillageRecords.WrittenPiece piece) => piece.Wood));
		}

		/// <summary>
		/// Checks that pieces as far from the generator as a position can be written, either way on each axis, read back
		/// where they stood
		/// </summary>
		[Fact]
		public void PiecesAtTheEdgeOfReachReadBack()
		{
			Vector3 origin = new Vector3(500f, 0f, 500f);
			List<VillageRecords.WrittenPiece> pieces = new List<VillageRecords.WrittenPiece>
			{
				Piece(111, new Vector3(820f, -320f, 180f), Vector3.zero, 1),
				Piece(111, new Vector3(180f, 320f, 820f), Vector3.zero, 1)
			};
			List<VillageRecords.WrittenPiece> read = VillageRecords.Decode(VillageRecords.Encode(origin, pieces));
			Assert.Equal(2, read.Count);
			AssertNear(new Vector3(820f, -320f, 180f), read[0].Position, 0.005f);
			AssertNear(new Vector3(180f, 320f, 820f), read[1].Position, 0.005f);
		}

		/// <summary>
		/// Checks that a piece too far from the generator for its position to be written is left out, rather than read
		/// back somewhere else, and the pieces around it are kept
		/// </summary>
		[Fact]
		public void PiecesOutOfReachAreLeftOut()
		{
			Vector3 origin = new Vector3(500f, 0f, 500f);
			List<VillageRecords.WrittenPiece> pieces = new List<VillageRecords.WrittenPiece>
			{
				Piece(111, new Vector3(510f, 0f, 500f), Vector3.zero, 1),
				Piece(222, new Vector3(900f, 0f, 500f), Vector3.zero, 2),
				Piece(333, new Vector3(500f, 0f, 120f), Vector3.zero, 3),
				Piece(444, new Vector3(490f, 5f, 500f), Vector3.zero, 4)
			};
			List<VillageRecords.WrittenPiece> read = VillageRecords.Decode(VillageRecords.Encode(origin, pieces));
			Assert.Equal(new[] { 111, 444 }, read.ConvertAll((VillageRecords.WrittenPiece piece) => piece.Prefab));
			AssertNear(new Vector3(510f, 0f, 500f), read[0].Position, 0.005f);
			AssertNear(new Vector3(490f, 5f, 500f), read[1].Position, 0.005f);
		}

		/// <summary>
		/// Checks that a piece is in reach of the generator up to 320 meters from it on every axis, and out of reach past
		/// that on any of them
		/// </summary>
		/// <param name="x">How far east of the generator the piece stands, in meters</param>
		/// <param name="y">How far above it</param>
		/// <param name="z">How far north of it</param>
		/// <param name="expected">Whether it's in reach</param>
		[Theory]
		[InlineData(0f, 0f, 0f, true)]
		[InlineData(320f, 320f, 320f, true)]
		[InlineData(-320f, -320f, -320f, true)]
		[InlineData(320.5f, 0f, 0f, false)]
		[InlineData(0f, -320.5f, 0f, false)]
		[InlineData(0f, 0f, 400f, false)]
		public void InReachUpToMaxOffset(float x, float y, float z, bool expected)
		{
			Vector3 origin = new Vector3(100f, 20f, -100f);
			Assert.Equal(expected, VillageRecords.InReach(origin, origin + new Vector3(x, y, z)));
		}

		/// <summary>
		/// Checks that no pieces read back as none
		/// </summary>
		[Fact]
		public void NoPiecesReadBackAsNone()
		{
			Assert.Empty(VillageRecords.Decode(VillageRecords.Encode(Vector3.zero, new List<VillageRecords.WrittenPiece>())));
		}

		/// <summary>
		/// Checks that pieces written by another version read as none, so they're noted again
		/// </summary>
		[Fact]
		public void PiecesOfAnotherVersionReadAsNone()
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(2);
			pkg.Write(Vector3.zero);
			pkg.Write(1);
			pkg.Write(111);
			pkg.Write(4);
			pkg.Write(1);
			pkg.Write((ushort)0);
			pkg.Write((short)100);
			pkg.Write((short)0);
			pkg.Write((short)0);
			pkg.Write((ushort)0);
			pkg.Write((ushort)0);
			pkg.Write((ushort)0);
			Assert.Empty(VillageRecords.Decode(pkg.GetArray()));
		}

		/// <summary>
		/// Checks that a noted piece stands only when one of its prefab stands within half a meter of where it stood
		/// </summary>
		/// <param name="prefab">The prefab of the piece standing</param>
		/// <param name="x">How far east of where the noted piece stood it stands, in meters</param>
		/// <param name="expected">Whether the noted piece stands</param>
		[Theory]
		[InlineData(111, 0f, true)]
		[InlineData(111, 0.5f, true)]
		[InlineData(111, 0.51f, false)]
		[InlineData(222, 0f, false)]
		public void StandsWithinHalfAMeter(int prefab, float x, bool expected)
		{
			Dictionary<int, List<Vector3>> standing = new Dictionary<int, List<Vector3>>
			{
				{ prefab, new List<Vector3> { new Vector3(30f, 0f, 30f), new Vector3(10f + x, 2f, 10f) } }
			};
			PieceRecord record = new PieceRecord { Prefab = 111, Position = new Vector3(10f, 2f, 10f) };
			Assert.Equal(expected, VillageRecords.Stands(standing, record));
		}

		/// <summary>
		/// Checks that a noted piece doesn't stand when nothing of its prefab was found
		/// </summary>
		[Fact]
		public void DoesNotStandWithNoneOfItsPrefab()
		{
			Dictionary<int, List<Vector3>> standing = new Dictionary<int, List<Vector3>>();
			PieceRecord record = new PieceRecord { Prefab = 111, Position = new Vector3(10f, 2f, 10f) };
			Assert.False(VillageRecords.Stands(standing, record));
		}

		/// <summary>
		/// Checks that a house stands with half of its noted pieces standing or more, with none noted, or while not all of
		/// them have loaded
		/// </summary>
		/// <param name="noted">How many of the village's noted pieces are in the house</param>
		/// <param name="stands">How many of those stand</param>
		/// <param name="settled">Whether they've all loaded</param>
		/// <param name="expected">Whether the house stands</param>
		[Theory]
		[InlineData(4, 2, true, true)]
		[InlineData(4, 1, true, false)]
		[InlineData(3, 2, true, true)]
		[InlineData(3, 1, true, false)]
		[InlineData(1, 0, true, false)]
		[InlineData(1, 1, true, true)]
		[InlineData(0, 0, true, true)]
		[InlineData(4, 0, false, true)]
		public void HouseStandsWithHalfItsPieces(int noted, int stands, bool settled, bool expected)
		{
			Assert.Equal(expected, VillageRecords.HouseStands(noted, stands, settled));
		}

		/// <summary>
		/// Checks that a house is in ruins only with noted pieces, all of them loaded and none of them standing
		/// </summary>
		/// <param name="noted">How many of the village's noted pieces are in the house</param>
		/// <param name="stands">How many of those stand</param>
		/// <param name="settled">Whether they've all loaded</param>
		/// <param name="expected">Whether the house is in ruins</param>
		[Theory]
		[InlineData(3, 0, true, true)]
		[InlineData(1, 0, true, true)]
		[InlineData(3, 1, true, false)]
		[InlineData(0, 0, true, false)]
		[InlineData(3, 0, false, false)]
		public void RuinHasNothingStanding(int noted, int stands, bool settled, bool expected)
		{
			Assert.Equal(expected, VillageRecords.IsRuin(noted, stands, settled));
		}

		/// <summary>
		/// Makes a piece as it's written
		/// </summary>
		/// <param name="prefab">Its prefab's hash</param>
		/// <param name="position">Where it stands</param>
		/// <param name="angles">Its angles, in degrees</param>
		/// <param name="wood">The wood it takes to build</param>
		/// <returns>The piece</returns>
		private static VillageRecords.WrittenPiece Piece(int prefab, Vector3 position, Vector3 angles, int wood)
		{
			return(new VillageRecords.WrittenPiece { Prefab = prefab, Position = position, Angles = angles, Wood = wood });
		}

		/// <summary>
		/// Checks that two points are within a distance of each other on every axis
		/// </summary>
		/// <param name="expected">The point expected</param>
		/// <param name="actual">The point found</param>
		/// <param name="tolerance">How far apart they can be on each axis</param>
		private static void AssertNear(Vector3 expected, Vector3 actual, float tolerance)
		{
			Assert.Equal(expected.x, actual.x, tolerance);
			Assert.Equal(expected.y, actual.y, tolerance);
			Assert.Equal(expected.z, actual.z, tolerance);
		}

		/// <summary>
		/// Checks that two sets of angles face the same way, to a step of the written angles, whichever turn they're
		/// given in
		/// </summary>
		/// <param name="expected">The angles expected, in degrees</param>
		/// <param name="actual">The angles found, in degrees</param>
		private static void AssertSameAngles(Vector3 expected, Vector3 actual)
		{
			Assert.InRange(Mathf.Abs(Mathf.DeltaAngle(expected.x, actual.x)), 0f, AngleStep);
			Assert.InRange(Mathf.Abs(Mathf.DeltaAngle(expected.y, actual.y)), 0f, AngleStep);
			Assert.InRange(Mathf.Abs(Mathf.DeltaAngle(expected.z, actual.z)), 0f, AngleStep);
		}
	}
}
