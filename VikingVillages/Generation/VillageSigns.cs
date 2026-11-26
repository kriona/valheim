using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The signposts of a village, each a sign with a name on it that players can write over
	/// </summary>
	/// <remarks>
	/// A farm's rooms come with blank signposts, a piece with no text on it. Whoever owns a village's generator swaps
	/// each one for the game's sign, which looks the same, whenever everything across the village has loaded. Each
	/// sign gets a name picked at random, and is noted among the village's pieces as a sign, so one the vikings build
	/// back is a sign with a name too, and the blank signposts are never noted, so they're never built back.
	///
	/// A blank signpost can stand with a wooden post through it, which would cut through the text, so each sign is
	/// slid along the way it faces until its text is clear of every building piece - to the side of the post it's
	/// mostly on, and turned around when that leaves the post in front of the text. The village's signs no player put
	/// up that were swapped before they were slid are slid the same way, once, keeping what's written on them.
	///
	/// The sign on top of a village's portal names the village on its other side (see VillagePortals), so it's never
	/// slid, renamed or counted as the sign that points to another village
	/// </remarks>
	internal static class VillageSigns
	{
		/// <summary>
		/// The blank signpost a farm comes with, and the sign it's swapped for
		/// </summary>
		private const string BlankPrefab = "sign_notext";
		private const string SignPrefab = "sign";
		private static readonly int BlankHash = BlankPrefab.GetStableHashCode();
		private static readonly int SignHash = SignPrefab.GetStableHashCode();

		/// <summary>
		/// On a farm's generator, that its village's signs have been slid clear of the posts through them
		/// </summary>
		private static readonly int SignsClearKey = "kriona.VikingVillages.villageSignsClear".GetStableHashCode();

		/// <summary>
		/// Half the size of a sign's text, around the sign's middle, in meters - its width and height, and how far in
		/// front of and behind the board it's kept clear, past the text that stands just off the board's face
		/// </summary>
		private static readonly Vector3 TextHalf = new Vector3(0.44f, 0.21f, 0.08f);

		/// <summary>
		/// How far a sign is slid at a time to clear what's through it, and the farthest it's slid, in meters
		/// </summary>
		private const float SlideStep = 0.02f;
		private const float SlideMax = 0.6f;

		/// <summary>
		/// How near a sign a blank signpost has to stand to be one left behind by the sign put up in its place, in
		/// meters - as far as a sign is slid, with some to spare
		/// </summary>
		private const float LeftoverReach = SlideMax + 0.5f;

		private static int pieceMask;

		/// <summary>
		/// The names a sign can have on it
		/// </summary>
		private static readonly string[] names =
		{
			"Valhalla",
			"Asgard",
			"Midgard",
			"Helheim",
			"Jotunheim",
			"Niflheim",
			"Bifrost",
			"Eikthyr",
			"Elder",
			"Black Forest",
			"Swamp",
			"Plains",
			"Mistlands",
			"Deep North",
			"Yggdrasil",
			"Mead Hall",
			"Burial Chambers",
			"Troll Cave"
		};

		/// <summary>
		/// Swaps a village's blank signposts for signs with names on them - taking away one left standing by a sign put
		/// up in its place instead - and slides the signs it already has clear of the posts through them, once. The
		/// blank signposts are taken out of the village's noted pieces, so its vikings never build them back
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with everything across it loaded</param>
		public static void TrySwap(Village village)
		{
			if (ZNetScene.instance == null)
			{
				return;
			}
			GameObject prefab = ZNetScene.instance.GetPrefab(SignPrefab);
			if (prefab == null)
			{
				return;
			}
			bool cleared = village.Zdo.GetBool(SignsClearKey);
			int unnoted = VillageRecords.RemoveAll(village, (int kind) => kind == BlankHash);
			List<ZNetView> found = new List<ZNetView>();
			List<Vector3> signs = new List<Vector3>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				int kind = instance.Key.GetPrefab();
				if ((kind != BlankHash && kind != SignHash) || instance.Value == null || instance.Key.GetLong(ZDOVars.s_creator, 0L) != 0L || VillagePortals.IsPortalSign(instance.Key) || !village.OwnsPiece(instance.Key.GetPosition()))
				{
					continue;
				}
				if (kind == SignHash)
				{
					signs.Add(instance.Key.GetPosition());
				}
				if (kind == BlankHash || !cleared)
				{
					found.Add(instance.Value);
				}
			}
			if (!cleared)
			{
				village.Zdo.Set(SignsClearKey, true);
			}
			int put = 0;
			int slid = 0;
			int leftovers = 0;
			foreach (ZNetView view in found)
			{
				bool blank = (view.GetZDO().GetPrefab() == BlankHash);
				Vector3 at = view.transform.position;
				if (blank && signs.Exists((Vector3 sign) => Vector3.Distance(sign, at) <= LeftoverReach))
				{
					view.ClaimOwnership();
					ZNetScene.instance.Destroy(view.gameObject);
					leftovers++;
					continue;
				}
				Vector3 from = view.transform.position;
				Vector3 position = from;
				Quaternion rotation = view.transform.rotation;
				bool moved = Clear(view, ref position, ref rotation);
				if (!blank && !moved)
				{
					continue;
				}
				ZDO old = view.GetZDO();
				string text = old.GetString(ZDOVars.s_text);
				string author = old.GetString(ZDOVars.s_author);
				string authorName = old.GetString(ZDOVars.s_authorDisplayName);
				int oldPrefab = old.GetPrefab();
				view.ClaimOwnership();
				ZNetScene.instance.Destroy(view.gameObject);
				GameObject instance = Object.Instantiate(prefab, position, rotation);
				VillageRecords.MarkBuilt(instance);
				if (blank || string.IsNullOrEmpty(text))
				{
					Name(instance, village);
					put += (blank ? 1 : 0);
				}
				else
				{
					Rewrite(instance, text, author, authorName);
				}
				if (moved)
				{
					slid++;
				}
				VillageRecords.Swap(village, oldPrefab, from, new PieceRecord { Prefab = SignHash, Position = position, Rotation = rotation, Wood = VillageRecords.WoodCost(SignHash) });
			}
			if (put > 0 || slid > 0 || leftovers > 0 || unnoted > 0)
			{
				Plugin.Log.LogInfo("Put up " + put + " signs in the village at " + village.Farm.ToString("F0") + ", sliding " + slid + " clear of the posts through them, taking away " + leftovers + " blank signposts left by signs and " + unnoted + " noted among its pieces");
			}
		}

		/// <summary>
		/// Works out where a sign stands clear of the building pieces through its text - slid along the way it faces to
		/// the side of them it's mostly on, and turned around when that leaves them in front of the text
		/// </summary>
		/// <param name="sign">The sign or signpost standing there, which is left out of what's in the way</param>
		/// <param name="position">Where it stands, set to where it's clear</param>
		/// <param name="rotation">Which way it faces, set to the way it faces once it's clear</param>
		/// <returns>False when nothing is through it, or it couldn't be slid clear within SlideMax, leaving it where it
		/// stands</returns>
		private static bool Clear(ZNetView sign, ref Vector3 position, ref Quaternion rotation)
		{
			Collider obstacle = InTheWay(sign, position, rotation);
			if (obstacle == null)
			{
				return(false);
			}
			Vector3 forward = rotation * Vector3.forward;
			float side = ((Vector3.Dot(obstacle.bounds.center - position, forward) > 0f) ? -1f : 1f);
			for (float slide = SlideStep; slide <= SlideMax + 0.001f; slide += SlideStep)
			{
				Vector3 moved = position + forward * (side * slide);
				if (InTheWay(sign, moved, rotation) == null)
				{
					position = moved;
					// The text is on the board's front, so a board slid behind the post is turned to face away from it
					if (side < 0f)
					{
						rotation = Quaternion.AngleAxis(180f, Vector3.up) * rotation;
					}
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Finds a building piece through a sign's text, other than the sign itself
		/// </summary>
		/// <param name="sign">The sign</param>
		/// <param name="position">Where its middle would be</param>
		/// <param name="rotation">Which way it would face</param>
		/// <returns>The piece's collider, or null when there's none</returns>
		private static Collider InTheWay(ZNetView sign, Vector3 position, Quaternion rotation)
		{
			if (pieceMask == 0)
			{
				pieceMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
			}
			foreach (Collider collider in Physics.OverlapBox(position, TextHalf, rotation, pieceMask, QueryTriggerInteraction.Ignore))
			{
				if (collider.GetComponentInParent<ZNetView>() != sign && collider.GetComponentInParent<WearNTear>() != null)
				{
					return(collider);
				}
			}
			return(null);
		}

		/// <summary>
		/// Writes what was on a sign onto the sign put up in its place, with who wrote it
		/// </summary>
		/// <param name="instance">The sign just put up</param>
		/// <param name="text">What was written on it</param>
		/// <param name="author">Who wrote it</param>
		/// <param name="authorName">The name shown for who wrote it</param>
		internal static void Rewrite(GameObject instance, string text, string author, string authorName)
		{
			Sign sign = instance.GetComponent<Sign>();
			ZNetView view = instance.GetComponent<ZNetView>();
			if (sign == null || view == null || !view.IsValid())
			{
				return;
			}
			ZDO zdo = view.GetZDO();
			zdo.Set(ZDOVars.s_text, text);
			zdo.Set(ZDOVars.s_author, author);
			zdo.Set(ZDOVars.s_authorDisplayName, authorName);
			sign.UpdateText();
		}

		/// <summary>
		/// Writes a name on a sign just placed, picked from the names a sign can have and the names the world's other
		/// villages go by - one of those written as by the player who gave it, so it's filtered for their words - though
		/// only one of a village's signs points to another village, so once one does, the rest pick from the names a
		/// sign can have. Anything but a sign is left as it is
		/// </summary>
		/// <param name="instance">The piece just placed</param>
		/// <param name="village">The village it's in, whose own name it leaves out</param>
		public static void Name(GameObject instance, Village village)
		{
			Sign sign = ((instance != null) ? instance.GetComponent<Sign>() : null);
			ZNetView view = ((sign != null) ? instance.GetComponent<ZNetView>() : null);
			if (view == null || !view.IsValid())
			{
				return;
			}
			List<VillageNameList.Entry> others = VillageNameList.Others(VillageNames.Typed(village));
			if (village != null && PointsToVillage(village, view, others))
			{
				others.Clear();
			}
			int pick = Random.Range(0, names.Length + others.Count);
			if (pick >= names.Length)
			{
				VillageNameList.Entry other = others[pick - names.Length];
				Rewrite(instance, other.Name, other.Author, "");
				return;
			}
			view.GetZDO().Set(ZDOVars.s_text, names[pick]);
			sign.UpdateText();
		}

		/// <summary>
		/// Checks whether another of a village's signs no player put up already points to another village - has one of
		/// the world's village names on it
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="own">The sign being named, left out</param>
		/// <param name="others">The names the world's other villages go by</param>
		/// <returns>True when one does</returns>
		private static bool PointsToVillage(Village village, ZNetView own, List<VillageNameList.Entry> others)
		{
			if (others.Count == 0)
			{
				return(false);
			}
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Value == own || instance.Key.GetPrefab() != SignHash || instance.Key.GetLong(ZDOVars.s_creator, 0L) != 0L || VillagePortals.IsPortalSign(instance.Key) || !village.OwnsPiece(instance.Key.GetPosition()))
				{
					continue;
				}
				if (VillageNameList.Has(others, instance.Key.GetString(ZDOVars.s_text)))
				{
					return(true);
				}
			}
			return(false);
		}
	}
}
