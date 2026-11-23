using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how house blueprints are read and laid out, which way their doors face, and how the spots of a farm's
	/// blueprint houses are kept
	/// </summary>
	public class VillageBlueprintsTests
	{
		/// <summary>
		/// Checks that two points are the same, to within rounding
		/// </summary>
		/// <param name="expected">The point it should be</param>
		/// <param name="actual">The point it is</param>
		private static void AssertNear(Vector3 expected, Vector3 actual)
		{
			Assert.Equal(expected.x, actual.x, 4);
			Assert.Equal(expected.y, actual.y, 4);
			Assert.Equal(expected.z, actual.z, 4);
		}

		/// <summary>
		/// Makes a piece line the way PlanBuild saves one - prefab, category, position, rotation, its extra info and its
		/// scale - facing the way it was built
		/// </summary>
		/// <param name="prefab">The piece's prefab</param>
		/// <param name="x">Where it stands across</param>
		/// <param name="y">How high it stands</param>
		/// <param name="z">Where it stands deep</param>
		/// <returns>The line</returns>
		private static string Piece(string prefab, float x, float y, float z)
		{
			return(prefab + ";Building;" + x.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + ";" + y.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + ";" + z.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + ";0.000000;0.000000;0.000000;1.000000;;1.000000;1.000000;1.000000");
		}

		/// <summary>
		/// Checks that a piece line is read into its prefab, position and rotation
		/// </summary>
		[Fact]
		public void ReadsAPieceLine()
		{
			VillageBlueprints.Part part = VillageBlueprints.ReadPart("wood_door;BuildingWorkbench;-1.500000;0.250000;2.000000;0.000000;0.707107;0.000000;0.707107;;1.000000;1.000000;1.000000");
			Assert.NotNull(part);
			Assert.Equal("wood_door", part.Prefab);
			AssertNear(new Vector3(-1.5f, 0.25f, 2f), part.Position);
			Assert.Equal(0f, part.Rotation.x, 4);
			Assert.Equal(0.707107f, part.Rotation.y, 4);
			Assert.Equal(0f, part.Rotation.z, 4);
			Assert.Equal(0.707107f, part.Rotation.w, 4);
		}

		/// <summary>
		/// Checks that a line with only the fields up to the rotation is enough, and the prefab's spaces are trimmed
		/// </summary>
		[Fact]
		public void ReadsTheFewestFieldsAndTrimsThePrefab()
		{
			VillageBlueprints.Part part = VillageBlueprints.ReadPart("  bed  ;Furniture;1;2;3;0;0;0;1");
			Assert.NotNull(part);
			Assert.Equal("bed", part.Prefab);
			AssertNear(new Vector3(1f, 2f, 3f), part.Position);
		}

		/// <summary>
		/// Checks that a blueprint saved with commas for decimal points is read the same as one with points
		/// </summary>
		[Fact]
		public void ReadsCommasAsDecimalPoints()
		{
			VillageBlueprints.Part part = VillageBlueprints.ReadPart("wood_wall;Building;1,5;0,0;-2,25;0;0;0;1");
			Assert.NotNull(part);
			AssertNear(new Vector3(1.5f, 0f, -2.25f), part.Position);
		}

		/// <summary>
		/// Checks that a line it can't make a piece of is turned down - too few fields, no prefab, a field that isn't a
		/// number, or no rotation at all
		/// </summary>
		/// <param name="line">The line</param>
		[Theory]
		[InlineData("wood_wall;Building;1;2;3;0;0;0")]
		[InlineData("wood_wall")]
		[InlineData("")]
		[InlineData(";Building;1;2;3;0;0;0;1")]
		[InlineData("   ;Building;1;2;3;0;0;0;1")]
		[InlineData("wood_wall;Building;one;2;3;0;0;0;1")]
		[InlineData("wood_wall;Building;1;2;3;0;0;0;")]
		[InlineData("wood_wall;Building;1;2;3;0;0;0;0")]
		public void TurnsDownUnreadableLines(string line)
		{
			Assert.Null(VillageBlueprints.ReadPart(line));
		}

		/// <summary>
		/// Checks that a rotation saved a little off length 1 is brought back to it, keeping which way it turns
		/// </summary>
		[Fact]
		public void NormalizesTheRotation()
		{
			VillageBlueprints.Part part = VillageBlueprints.ReadPart("wood_wall;Building;0;0;0;0;3;0;4");
			Assert.NotNull(part);
			Assert.Equal(0f, part.Rotation.x, 4);
			Assert.Equal(0.6f, part.Rotation.y, 4);
			Assert.Equal(0f, part.Rotation.z, 4);
			Assert.Equal(0.8f, part.Rotation.w, 4);
			VillageBlueprints.Part doubled = VillageBlueprints.ReadPart("wood_wall;Building;0;0;0;0;0;0;2");
			Assert.Equal(1f, doubled.Rotation.w, 4);
		}

		/// <summary>
		/// Checks that only the lines after #Pieces are pieces, that the blueprint's own name is used over its file's,
		/// and that a file's name is used when the blueprint has none
		/// </summary>
		[Fact]
		public void ReadsOnlyThePiecesSection()
		{
			VillageBlueprints.Design design = VillageBlueprints.Parse("cottage.blueprint", new[]
			{
				"#Name:Little Cottage",
				"#Creator:Someone",
				"#Description:\"\"",
				Piece("wood_floor", 50f, 0f, 50f),
				"#Pieces",
				Piece("wood_floor", 0f, 0f, 0f),
				Piece("wood_door", 2f, 0f, 0f)
			});
			Assert.Equal("Little Cottage", design.Name);
			Assert.Equal("cottage.blueprint", design.File);
			Assert.Equal(2, design.Parts.Count);
			Assert.Empty(design.Problems);
			VillageBlueprints.Design unnamed = VillageBlueprints.Parse("cottage.blueprint", new[] { "#Name:", "#Pieces", Piece("wood_floor", 0f, 0f, 0f) });
			Assert.Equal("cottage", unnamed.Name);
		}

		/// <summary>
		/// Checks that the section after the pieces ends them, so its lines aren't read as pieces or counted as unreadable
		/// </summary>
		[Fact]
		public void SnapPointsEndThePieces()
		{
			VillageBlueprints.Design design = VillageBlueprints.Parse("hut.blueprint", new[]
			{
				"#Pieces",
				Piece("wood_floor", 0f, 0f, 0f),
				"#SnapPoints",
				"1.000000;0.000000;1.000000",
				"#Terrain",
				Piece("wood_floor", 9f, 0f, 9f)
			});
			Assert.Single(design.Parts);
			Assert.Empty(design.Problems);
		}

		/// <summary>
		/// Checks that PlanBuild's markers are left out of a design, whatever their case
		/// </summary>
		[Fact]
		public void LeavesOutMarkers()
		{
			VillageBlueprints.Design design = VillageBlueprints.Parse("hut.blueprint", new[]
			{
				"#Pieces",
				Piece("piece_bp_center", 0f, 0f, 0f),
				Piece("PIECE_BP_snappoint", 1f, 0f, 1f),
				Piece("wood_floor", 4f, 0f, 4f)
			});
			Assert.Single(design.Parts);
			Assert.Equal("wood_floor", design.Parts[0].Prefab);
		}

		/// <summary>
		/// Checks that a design is laid out around the middle of its pieces seen from above, with its lowest piece on
		/// the ground, and its footprint reaching a margin past them
		/// </summary>
		[Fact]
		public void CentersOnTheMiddleAndLowestPiece()
		{
			VillageBlueprints.Design design = VillageBlueprints.Parse("hall.blueprint", new[]
			{
				"#Pieces",
				Piece("wood_floor", 0f, 5f, 0f),
				Piece("wood_roof", 4f, 6f, 10f)
			});
			AssertNear(new Vector3(-2f, 0f, -5f), design.Parts[0].Position);
			AssertNear(new Vector3(2f, 1f, 5f), design.Parts[1].Position);
			Assert.Equal(3f, design.Half.x, 4);
			Assert.Equal(6f, design.Half.y, 4);
		}

		/// <summary>
		/// Checks that a design of one piece stands that piece at its middle, with a footprint of just the margin
		/// </summary>
		[Fact]
		public void SinglePieceGetsTheMargin()
		{
			VillageBlueprints.Design design = VillageBlueprints.Parse("shed.blueprint", new[] { "#Pieces", Piece("wood_floor", 7f, 2f, -3f) });
			AssertNear(Vector3.zero, design.Parts[0].Position);
			Assert.Equal(1f, design.Half.x, 4);
			Assert.Equal(1f, design.Half.y, 4);
		}

		/// <summary>
		/// Checks that lines it can't read are counted among the design's problems, and a design with no pieces says so
		/// </summary>
		[Fact]
		public void NotesUnreadableLinesAndNoPieces()
		{
			VillageBlueprints.Design design = VillageBlueprints.Parse("broken.blueprint", new[] { "#Pieces", "wood_floor;Building;x;0;0;0;0;0;1", "nonsense" });
			Assert.Empty(design.Parts);
			Assert.Equal(new List<string> { "2 piece lines it couldn't read", "no pieces" }, design.Problems);
			VillageBlueprints.Design empty = VillageBlueprints.Parse("empty.blueprint", new[] { "#Name:Empty", "#Pieces", "", "   " });
			Assert.Equal(new List<string> { "no pieces" }, empty.Problems);
		}

		/// <summary>
		/// Checks that every blueprint in the mod's Blueprints folder reads without problems - skipped while the folder
		/// has none
		/// </summary>
		[Fact]
		public void ReadsEveryBlueprintInTheMod()
		{
			string folder = Path.Combine(RepositoryFolder(), "ZombieApocalypse", "Blueprints");
			string[] files = (Directory.Exists(folder) ? Directory.GetFiles(folder, "*.blueprint", SearchOption.AllDirectories) : new string[0]);
			if (files.Length == 0)
			{
				Assert.Skip("No blueprints in " + folder);
			}
			foreach (string file in files)
			{
				VillageBlueprints.Design design = VillageBlueprints.Parse(file, File.ReadAllLines(file));
				Assert.True(design.Problems.Count == 0, Path.GetFileName(file) + ": " + string.Join(", ", design.Problems));
				Assert.NotEmpty(design.Parts);
			}
		}

		/// <summary>
		/// Finds the repository's folder - the one holding Valheim.slnx, above the folder the tests run from
		/// </summary>
		/// <returns>The folder</returns>
		private static string RepositoryFolder()
		{
			DirectoryInfo folder = new DirectoryInfo(AppContext.BaseDirectory);
			while (folder != null && !File.Exists(Path.Combine(folder.FullName, "Valheim.slnx")))
			{
				folder = folder.Parent;
			}
			Assert.NotNull(folder);
			return(folder.FullName);
		}

		/// <summary>
		/// Checks that doors spread evenly around a design cancel out, leaving it facing forward, as it does with none
		/// </summary>
		[Fact]
		public void DoorsThatCancelOutKeepForward()
		{
			AssertNear(Vector3.forward, VillageBlueprints.DoorDirection(new List<Vector3> { new Vector3(3f, 0f, 0f), new Vector3(-3f, 0f, 0f) }));
			AssertNear(Vector3.forward, VillageBlueprints.DoorDirection(new List<Vector3>()));
			AssertNear(Vector3.forward, VillageBlueprints.DoorDirection(new List<Vector3> { new Vector3(0.05f, 0f, 0.05f) }));
		}

		/// <summary>
		/// Checks that a design faces toward its one door, and toward where its doors are on the whole, seen from above
		/// </summary>
		[Fact]
		public void FacesTowardItsDoors()
		{
			AssertNear(new Vector3(-1f, 0f, 0f), VillageBlueprints.DoorDirection(new List<Vector3> { new Vector3(-4f, 0f, 0f) }));
			AssertNear(new Vector3(0.6f, 0f, -0.8f), VillageBlueprints.DoorDirection(new List<Vector3> { new Vector3(3f, 0f, 0f), new Vector3(0f, 0f, -4f) }));
		}

		/// <summary>
		/// Checks that how high a door stands doesn't turn the design
		/// </summary>
		[Fact]
		public void DoorHeightIsIgnored()
		{
			AssertNear(new Vector3(0f, 0f, -1f), VillageBlueprints.DoorDirection(new List<Vector3> { new Vector3(0f, 25f, -2f) }));
			AssertNear(Vector3.forward, VillageBlueprints.DoorDirection(new List<Vector3> { new Vector3(0f, 10f, 0f) }));
		}

		/// <summary>
		/// Checks that no points kept reads back as none, and an empty list packs and reads back empty
		/// </summary>
		[Fact]
		public void NoPointsReadBackEmpty()
		{
			Assert.Empty(VillageBlueprints.ReadPoints(null));
			Assert.Empty(VillageBlueprints.ReadPoints(VillageBlueprints.WritePoints(new List<Vector3>())));
		}

		/// <summary>
		/// Checks that points pack as a count then three floats each, and read back the same and in order
		/// </summary>
		[Fact]
		public void PointsRoundTrip()
		{
			List<Vector3> points = new List<Vector3> { new Vector3(1f, 2f, 3f), new Vector3(-10.5f, 0f, 7.25f), new Vector3(1000f, -3f, 0.125f) };
			byte[] bytes = VillageBlueprints.WritePoints(points);
			Assert.Equal(4 + 3 * 12, bytes.Length);
			Assert.Equal(points, VillageBlueprints.ReadPoints(bytes));
		}

		/// <summary>
		/// Checks that adding a point to those kept, as a farm notes another blueprint house, keeps the earlier ones
		/// </summary>
		[Fact]
		public void AddingAPointKeepsEarlierOnes()
		{
			byte[] bytes = VillageBlueprints.WritePoints(new List<Vector3> { new Vector3(1f, 0f, 1f) });
			List<Vector3> points = VillageBlueprints.ReadPoints(bytes);
			points.Add(new Vector3(5f, 1f, -5f));
			Assert.Equal(new List<Vector3> { new Vector3(1f, 0f, 1f), new Vector3(5f, 1f, -5f) }, VillageBlueprints.ReadPoints(VillageBlueprints.WritePoints(points)));
		}
	}
}
