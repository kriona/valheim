using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The village's cooking stations - two over a bonfire and one over a campfire, placed once the fire is lit - and
	/// what its vikings do with them: put raw meat on, see what's cooking and take off what's done
	/// </summary>
	/// <remarks>
	/// A cooking station only cooks while each of its fire check points is in a fire's flames, so the stations are
	/// placed where they are for this village's fire, side by side across a bonfire's middle, or over a campfire. Whoever
	/// owns a village's generator places them, notes it so they're only placed once, and notes them among the village's
	/// pieces so its vikings build them back. A station only cooks on the client that owns it, so a viking takes it over
	/// as it cooks there
	/// </remarks>
	internal static class VillageCooking
	{
		/// <summary>
		/// The cooking stations the vikings place and cook at - the wooden one, and the iron one in a village of the
		/// Swamp or a harder biome (see StationFor)
		/// </summary>
		public const string StationPrefab = "piece_cookingstation";
		public static readonly int StationHash = StationPrefab.GetStableHashCode();
		public const string IronStationPrefab = "piece_cookingstation_iron";
		public static readonly int IronStationHash = IronStationPrefab.GetStableHashCode();

		/// <summary>
		/// The cauldron a village of the Black Forest or a harder biome gets over its fire, and the extensions next to it
		/// that raise its level, one more for each biome after the Black Forest, in the order they're given - the spice
		/// rack for the Swamp, then the butcher's table, the pots and pans, the mortar and pestle, the rolling pins and
		/// the smoker for the Deep North
		/// </summary>
		public const string CauldronPrefab = "piece_cauldron";
		public static readonly int CauldronHash = CauldronPrefab.GetStableHashCode();
		private static readonly string[] CauldronExtensions =
		{
			"cauldron_ext1_spice", "cauldron_ext3_butchertable", "cauldron_ext4_pots", "cauldron_ext5_mortarandpestle",
			"cauldron_ext6_rollingpins", "cauldron_ext7_smoker"
		};

		/// <summary>
		/// On a farm's generator or a started village's anchor, that its village has been given its cauldron
		/// </summary>
		private static readonly int CauldronKey = "kriona.VikingVillages.villageCauldron".GetStableHashCode();

		/// <summary>
		/// Where a cauldron's extensions stand around it, on rings from ExtensionNearest out to the extension's own reach
		/// of the cauldron less ExtensionReachSlack - the nearest past where a viking or a player stands to use the
		/// cauldron, so an extension never stands in the way of it - ExtensionRingStep apart, with the spots on each ring
		/// ExtensionAngleStep apart, and how far every part of an extension keeps out of the fire's flames, in meters and
		/// degrees
		/// </summary>
		private const float ExtensionNearest = 2.5f;
		private const float ExtensionReachSlack = 0.5f;
		private const float ExtensionRingStep = 0.5f;
		private const float ExtensionAngleStep = 20f;
		private const float ExtensionFlameClearance = 0.5f;

		/// <summary>
		/// The loaded villages' burning bonfires as last found, and how often they're looked for again, in seconds
		/// </summary>
		private static readonly List<Fireplace> villageFires = new List<Fireplace>();
		private static float nextVillageFireCheck;
		private const float VillageFireCheckInterval = 2f;

		/// <summary>
		/// How far from a village's bonfire a cooking station's middle can be for the bonfire to run it, in meters
		/// </summary>
		private const float VillageFireReach = 5f;

		/// <summary>
		/// Checks whether a fire is a bonfire, rather than a campfire or the like
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>True when it is</returns>
		private static bool IsBonfire(Fireplace fire)
		{
			return(Utils.GetPrefabName(fire.gameObject).StartsWith("bonfire", System.StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Checks whether a cooking station sits on a village's burning bonfire - any one of its fire check points, or
		/// its middle when it has none, in the bonfire's flames - so the bonfire runs it, even a station whose check
		/// points the game would need a hearth or several campfires under all at once. A village's campfire doesn't
		/// </summary>
		/// <param name="station">The station</param>
		/// <returns>True when it does</returns>
		public static bool OnVillageFire(CookingStation station)
		{
			if (station == null || !station.m_requireFire || !ServerCheck.Active)
			{
				return(false);
			}
			if (Time.time >= nextVillageFireCheck)
			{
				nextVillageFireCheck = Time.time + VillageFireCheckInterval;
				FindVillageFires();
			}
			Vector3 middle = station.transform.position;
			float radius = (station.m_cheapFireCheck ? 0.25f : station.m_fireCheckRadius);
			foreach (Fireplace fire in villageFires)
			{
				if (fire == null || !fire.IsBurning() || Utils.DistanceXZ(fire.transform.position, middle) > VillageFireReach)
				{
					continue;
				}
				if (station.m_fireCheckPoints == null || station.m_fireCheckPoints.Length == 0)
				{
					if (InFlames(fire, middle, radius))
					{
						return(true);
					}
					continue;
				}
				foreach (Transform point in station.m_fireCheckPoints)
				{
					if (point != null && InFlames(fire, point.position, radius))
					{
						return(true);
					}
				}
			}
			return(false);
		}

		/// <summary>
		/// Notes the loaded villages' bonfires that are burning
		/// </summary>
		private static void FindVillageFires()
		{
			villageFires.Clear();
			foreach (Village village in Village.Loaded())
			{
				Fireplace fire = village.Fire();
				if (fire != null && fire.m_nview != null && fire.m_nview.IsValid() && fire.IsBurning() && IsBonfire(fire))
				{
					villageFires.Add(fire);
				}
			}
		}

		/// <summary>
		/// Checks whether a point is in a fire's own flames - within a radius of one of its burning effect areas, which
		/// are only active while it burns
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <param name="point">The point</param>
		/// <param name="radius">How near the flames it has to be, in meters</param>
		/// <returns>True when it is</returns>
		private static bool InFlames(Fireplace fire, Vector3 point, float radius)
		{
			foreach (EffectArea area in fire.GetComponentsInChildren<EffectArea>())
			{
				if ((area.m_type & EffectArea.Type.Burning) == 0)
				{
					continue;
				}
				Collider flames = area.GetComponent<Collider>();
				if (flames != null && flames.enabled && Vector3.Distance(flames.ClosestPoint(point), point) <= radius)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a prefab is one of the cooking stations the vikings cook at
		/// </summary>
		/// <param name="prefab">The prefab's hash</param>
		/// <returns>True when it is</returns>
		public static bool IsStation(int prefab)
		{
			return(prefab == StationHash || prefab == IronStationHash);
		}

		/// <summary>
		/// Works out a village's biome, the one everything about it goes by - its vikings (see VikingCore.Generate), how
		/// it's built and stocked, and its quests - the hardest within VillageFarmhouses.HardBiomeVikingReach of its farm,
		/// or the farm's own, by its index into VikingGear's tables
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The index, from 0 for the Meadows</returns>
		public static int VillageBiome(Village village)
		{
			return(BiomeAt(village.Farm));
		}

		/// <summary>
		/// Works out the biome of the village whose farm stands at a spot (see VillageBiome) - for working one out before
		/// the village is loaded, like a farm a portal could lead to, or while its ground is laid
		/// </summary>
		/// <param name="farm">Where the farm stands</param>
		/// <returns>The index, from 0 for the Meadows</returns>
		public static int BiomeAt(Vector3 farm)
		{
			if (WorldGenerator.instance == null)
			{
				return(0);
			}
			int own = VikingGear.IndexOf(WorldGenerator.instance.GetBiome(farm));
			return(Mathf.Max(own, VillageFarmhouses.HardestBiomeNear(farm, VillageFarmhouses.HardBiomeVikingReach)));
		}

		/// <summary>
		/// Gets the biome of the village whose farm stands at a spot (see BiomeAt) as the game's biome
		/// </summary>
		/// <param name="farm">Where the farm stands</param>
		/// <returns>The biome, or None before the world is generated</returns>
		public static Heightmap.Biome GameBiomeAt(Vector3 farm)
		{
			return((WorldGenerator.instance != null) ? VikingGear.Get(BiomeAt(farm)).Biome : Heightmap.Biome.None);
		}

		/// <summary>
		/// Picks the cooking station a village gets over its fire - the iron one from the Swamp on, the wooden one before
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The station's prefab name</returns>
		private static string StationFor(Village village)
		{
			return((VillageBiome(village) >= VikingGear.IndexOf(Heightmap.Biome.Swamp)) ? IronStationPrefab : StationPrefab);
		}

		/// <summary>
		/// On a farm's generator, that its village has been given its cooking stations
		/// </summary>
		public static readonly int CookingKey = "kriona.VikingVillages.villageCooking".GetStableHashCode();

		/// <summary>
		/// On a farm's generator, where the village's fire stood before it took up another, until its cooking stations
		/// have moved over the new one, and zero after
		/// </summary>
		private static readonly int OldFireKey = "kriona.VikingVillages.villageOldFire".GetStableHashCode();

		/// <summary>
		/// How many cooking stations a bonfire and a campfire get
		/// </summary>
		private const int BonfireStations = 2;
		private const int CampfireStations = 1;

		/// <summary>
		/// How far from a fire's middle a station's middle can be, how far apart the spots tried are, in meters and
		/// degrees, how far apart two stations side by side keep, and how near a fire a station already standing counts
		/// as its own, in meters
		/// </summary>
		private const float MaxOffset = 3f;
		private const float OffsetStep = 0.1f;
		private const float AngleStep = 15f;
		private const float Spacing = 0.05f;
		private const float StationReach = 3.5f;

		/// <summary>
		/// How far above a fire's middle its flames are looked for, in meters, to tell whether it's lit
		/// </summary>
		private const float FlameHeight = 0.3f;

		/// <summary>
		/// The items the cooking station cooks, by prefab name
		/// </summary>
		private static HashSet<string> raws;

		/// <summary>
		/// Has a village's cooking stations move over the fire it took up in place of its destroyed one, once that fire
		/// is lit (see TryPlace)
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="oldFire">Where its destroyed fire stood</param>
		public static void FireMoved(Village village, Vector3 oldFire)
		{
			if (!village.Zdo.GetBool(CookingKey))
			{
				return;
			}
			village.Zdo.Set(OldFireKey, oldFire);
			village.Zdo.Set(CookingKey, false);
		}

		/// <summary>
		/// Gives a village its cooking stations over its fire, once, when the fire is loaded and lit - as many as it gets
		/// for its kind of fire, less those already standing over it and cooking. When the village took up another fire
		/// in place of its destroyed one, the stations it was given over the old one that don't cook over the new one go
		/// first (see Unhome)
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with everything across it loaded</param>
		public static void TryPlace(Village village)
		{
			if (village.Zdo.GetBool(CookingKey) || ZNetScene.instance == null)
			{
				return;
			}
			Fireplace fire = village.Fire();
			if (fire == null)
			{
				return;
			}
			Physics.SyncTransforms();
			// An unlit fire has no flames for a station to be placed over, so it's tried again once the vikings light it
			if (EffectArea.IsPointInsideArea(fire.transform.position + Vector3.up * FlameHeight, EffectArea.Type.Burning, 0.25f) == null)
			{
				return;
			}
			village.Zdo.Set(CookingKey, true);
			Vector3 oldFire = village.Zdo.GetVec3(OldFireKey, Vector3.zero);
			if (oldFire != Vector3.zero)
			{
				Unhome(village, oldFire);
				village.Zdo.Set(OldFireKey, Vector3.zero);
			}
			bool bonfire = IsBonfire(fire);
			int wanted = (bonfire ? BonfireStations : CampfireStations) - StationsNear(fire.transform.position);
			if (wanted <= 0)
			{
				return;
			}
			string station = StationFor(village);
			List<PieceRecord> built = Place(fire, wanted, station);
			// A campfire's flames may not reach all of the longer iron station's check points, so the wooden one stands
			// over it then
			if (built.Count == 0 && station == IronStationPrefab)
			{
				station = StationPrefab;
				built = Place(fire, wanted, station);
			}
			VillageRecords.Add(village, built);
			Plugin.Log.LogInfo("Placed " + built.Count + " " + station + " over the fire of the village at " + village.Farm.ToString("F0"));
		}

		/// <summary>
		/// Gives a village of the Black Forest or a harder biome a cauldron over its fire once its fire is lit - where
		/// there's room for it in the flames beside the cooking stations, as far out from the fire's middle as it fits -
		/// with the extensions its biome calls for around it, out of the flames (see CauldronExtensions). It's placed
		/// once, noted so it isn't placed again, and noted among the village's pieces so its vikings build it back. A
		/// village with a cauldron already standing over its fire just gets the extensions
		/// </summary>
		/// <param name="village">The village, whose generator or anchor this client owns, with everything across it
		/// loaded</param>
		public static void TryPlaceCauldron(Village village)
		{
			if (village.Zdo.GetBool(CauldronKey) || ZNetScene.instance == null)
			{
				return;
			}
			int biome = VillageBiome(village);
			int forest = VikingGear.IndexOf(Heightmap.Biome.BlackForest);
			if (biome < forest)
			{
				village.Zdo.Set(CauldronKey, true);
				return;
			}
			Fireplace fire = village.Fire();
			if (fire == null)
			{
				return;
			}
			Physics.SyncTransforms();
			if (EffectArea.IsPointInsideArea(fire.transform.position + Vector3.up * FlameHeight, EffectArea.Type.Burning, 0.25f) == null)
			{
				return;
			}
			village.Zdo.Set(CauldronKey, true);
			GameObject prefab = ZNetScene.instance.GetPrefab(CauldronPrefab);
			if (prefab == null)
			{
				Plugin.Log.LogWarning("Couldn't find the " + CauldronPrefab + " prefab to give the village at " + village.Farm.ToString("F0"));
				return;
			}
			List<PieceRecord> built = new List<PieceRecord>();
			if (!Standing(CauldronHash, fire.transform.position, StationReach, out Vector3 cauldron))
			{
				if (!PlaceCauldron(fire, prefab, built, out cauldron))
				{
					Plugin.Log.LogWarning("Found no room for a cauldron in the flames of the fire of the village at " + village.Farm.ToString("F0") + " beside its cooking stations");
					return;
				}
			}
			int wanted = Mathf.Min(biome - forest, CauldronExtensions.Length);
			int placed = PlaceExtensions(village, cauldron, wanted, built);
			VillageRecords.Add(village, built);
			Plugin.Log.LogInfo("Gave the village at " + village.Farm.ToString("F0") + " a cauldron with " + placed + " of the " + wanted + " extensions its biome calls for, " + built.Count + " pieces in all");
		}

		/// <summary>
		/// Finds a piece of a kind standing near a point
		/// </summary>
		/// <param name="prefab">The piece's prefab hash</param>
		/// <param name="point">The point</param>
		/// <param name="reach">How near it, in meters</param>
		/// <param name="found">Set to where it stands</param>
		/// <returns>False when none stands that near</returns>
		private static bool Standing(int prefab, Vector3 point, float reach, out Vector3 found)
		{
			found = point;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Key.GetPrefab() == prefab && instance.Value != null && Utils.DistanceXZ(instance.Key.GetPosition(), point) <= reach)
				{
					found = instance.Key.GetPosition();
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Places a cauldron in a fire's flames - its middle inside them, where the game also counts it as over the fire -
		/// as far out from the fire's middle as it has room, clear of the cooking stations and anything else but the fire
		/// itself
		/// </summary>
		/// <remarks>
		/// The game's cauldron is used from within 1.9 meters of its middle, and a bonfire's flames reach 1.8 meters, so
		/// one at the fire's middle could only be used from inside the flames - at their edge a viking or a player works
		/// it from outside them
		/// </remarks>
		/// <param name="fire">The fire, lit</param>
		/// <param name="prefab">The cauldron's prefab</param>
		/// <param name="built">The pieces placed, as they'd be noted among the village's pieces, added to</param>
		/// <param name="spot">Set to where it stands</param>
		/// <returns>False when there's no room for it</returns>
		private static bool PlaceCauldron(Fireplace fire, GameObject prefab, List<PieceRecord> built, out Vector3 spot)
		{
			Vector3 middle = fire.transform.position;
			Bounds size = (VillageFurnishing.Footprint(CauldronPrefab, out Bounds measured) ? measured : new Bounds(Vector3.up * 0.5f, Vector3.one));
			float flames = Mathf.Min(VillageLife.AreaReach(fire, EffectArea.Type.Burning), MaxOffset);
			// Stepping in by OffsetStep can come up a hair short of the middle, so the last step lands on it
			for (float step = flames; step > -OffsetStep / 2f; step -= OffsetStep)
			{
				float offset = Mathf.Max(0f, step);
				for (float angle = 0f; angle < 360f; angle += AngleStep)
				{
					Vector3 outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
					spot = OnGround(middle + outward * offset);
					Quaternion facing = Quaternion.LookRotation(outward);
					if (InFlames(fire, spot, 0f) && EffectArea.IsPointPlus025InsideBurningArea(spot) && Clear(spot, facing, size, fire.gameObject))
					{
						built.Add(Build(prefab, spot, facing));
						return(true);
					}
					if (offset <= 0f)
					{
						break;
					}
				}
			}
			spot = middle;
			return(false);
		}

		/// <summary>
		/// Places a cauldron's extensions around it, facing it, at the nearest spot within their reach of it, past where
		/// it's used from (see ExtensionNearest), with every part of them out of the fire's flames and clear of everything
		/// already standing there - writing to the log why the spots tried for each that has no room didn't fit
		/// </summary>
		/// <param name="village">The village, for the log</param>
		/// <param name="cauldron">Where the cauldron stands</param>
		/// <param name="count">How many of CauldronExtensions it gets, the first ones first</param>
		/// <param name="built">The pieces placed, as they'd be noted among the village's pieces, added to</param>
		/// <returns>How many were placed</returns>
		private static int PlaceExtensions(Village village, Vector3 cauldron, int count, List<PieceRecord> built)
		{
			int placed = 0;
			for (int i = 0; i < count; i++)
			{
				string name = CauldronExtensions[i];
				GameObject prefab = ZNetScene.instance.GetPrefab(name);
				StationExtension extension = ((prefab != null) ? prefab.GetComponent<StationExtension>() : null);
				if (extension == null)
				{
					Plugin.Log.LogWarning("Couldn't find the " + name + " prefab to give the cauldron of the village at " + village.Farm.ToString("F0"));
					continue;
				}
				Bounds size = (VillageFurnishing.Footprint(name, out Bounds measured) ? measured : new Bounds(Vector3.up * 0.5f, Vector3.one));
				// The spot's check reaches as far as the piece's farthest corner from its origin, so none of it is in the flames
				float spread = new Vector2(Mathf.Max(Mathf.Abs(size.min.x), Mathf.Abs(size.max.x)), Mathf.Max(Mathf.Abs(size.min.z), Mathf.Abs(size.max.z))).magnitude;
				float reach = extension.m_maxStationDistance - ExtensionReachSlack;
				int tried = 0;
				int burning = 0;
				int far = 0;
				int blocked = 0;
				bool fits = false;
				foreach (float distance in RingDistances(ExtensionNearest, reach, ExtensionRingStep))
				{
					for (float angle = 0f; angle < 360f && !fits; angle += ExtensionAngleStep)
					{
						tried++;
						Vector3 outward = Quaternion.Euler(0f, angle + i * 47f, 0f) * Vector3.forward;
						Vector3 spot = OnGround(cauldron + outward * distance);
						Quaternion facing = Quaternion.LookRotation(-outward);
						if (EffectArea.IsPointInsideArea(spot, EffectArea.Type.Burning, ExtensionFlameClearance + spread) != null)
						{
							burning++;
							continue;
						}
						// The game counts an extension's reach in three dimensions, so ground that rises or falls can put it out
						if (Vector3.Distance(spot, cauldron) > reach)
						{
							far++;
							continue;
						}
						if (!Clear(spot, facing, size, null))
						{
							blocked++;
							continue;
						}
						built.Add(Build(prefab, spot, facing));
						Physics.SyncTransforms();
						fits = true;
					}
					if (fits)
					{
						break;
					}
				}
				if (fits)
				{
					placed++;
				}
				else
				{
					Plugin.Log.LogWarning("Found no room for a " + name + " within " + reach.ToString("F1") + "m of the cauldron at " + cauldron.ToString("F1") + " in the village at " + village.Farm.ToString("F0") + ": " + tried + " spots tried - " + burning + " in the fire's flames, " + far + " out of its reach where the ground rises or falls, " + blocked + " with something already standing in the way");
				}
			}
			return(placed);
		}

		/// <summary>
		/// Lists the distances of the rings a piece is tried on around a point, nearest first, a step apart from the
		/// nearest out to the farthest, which is always tried even when the step doesn't land on it
		/// </summary>
		/// <param name="nearest">The nearest ring's distance</param>
		/// <param name="farthest">The farthest ring's distance</param>
		/// <param name="step">How far apart the rings are</param>
		/// <returns>The distances, none when the farthest is nearer than the nearest</returns>
		internal static List<float> RingDistances(float nearest, float farthest, float step)
		{
			List<float> distances = new List<float>();
			if (farthest < nearest || step <= 0f)
			{
				return(distances);
			}
			// Stepping out by a float step can land a hair past the farthest, so that's left to the last ring
			for (float distance = nearest; distance < farthest - step / 2f; distance += step)
			{
				distances.Add(distance);
			}
			distances.Add(farthest);
			return(distances);
		}

		/// <summary>
		/// Checks whether a piece has room at a spot - nothing solid in the space it takes but what's left out, counting
		/// the colliders a cooking station, cauldron or the like keeps on the game's non-solid piece and blocker layers
		/// </summary>
		/// <param name="spot">Where its origin would be, on the ground</param>
		/// <param name="facing">Which way it would face</param>
		/// <param name="size">Its size around its origin</param>
		/// <param name="ignore">An object whose colliders don't count, like the fire a cauldron stands over, or null</param>
		/// <returns>True when it has room</returns>
		private static bool Clear(Vector3 spot, Quaternion facing, Bounds size, GameObject ignore)
		{
			if (solidMask == 0)
			{
				solidMask = LayerMask.GetMask("piece", "piece_nonsolid", "blocker", "Default", "static_solid", "Default_small");
			}
			Vector3 center = spot + facing * size.center + Vector3.up * 0.05f;
			foreach (Collider hit in Physics.OverlapBox(center, size.extents * 0.9f, facing, solidMask, QueryTriggerInteraction.Ignore))
			{
				if (hit != null && (ignore == null || !hit.transform.IsChildOf(ignore.transform)))
				{
					return(false);
				}
			}
			return(true);
		}

		private static int solidMask;

		/// <summary>
		/// Counts the cooking stations already standing over a fire and cooking over it
		/// </summary>
		/// <param name="fire">Where the fire stands, lit</param>
		/// <returns>How many</returns>
		private static int StationsNear(Vector3 fire)
		{
			int count = 0;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (!IsStation(instance.Key.GetPrefab()) || instance.Value == null || Utils.DistanceXZ(instance.Key.GetPosition(), fire) > StationReach)
				{
					continue;
				}
				CookingStation station = instance.Value.GetComponent<CookingStation>();
				if (station != null && station.IsFireLit())
				{
					count++;
				}
			}
			return(count);
		}

		/// <summary>
		/// Stops a village building back the cooking stations it was given over the fire it had before - those noted
		/// within StationReach of where that fire stood, unless one still stands there cooking over the new fire - and
		/// takes down the ones still standing there that it built, don't cook and hold nothing
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with its new fire lit</param>
		/// <param name="oldFire">Where its old fire stood</param>
		private static void Unhome(Village village, Vector3 oldFire)
		{
			List<Vector3> cooking = new List<Vector3>();
			List<ZNetView> idle = new List<ZNetView>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (!IsStation(instance.Key.GetPrefab()) || instance.Value == null || !instance.Value.IsValid() || Utils.DistanceXZ(instance.Key.GetPosition(), oldFire) > StationReach)
				{
					continue;
				}
				CookingStation station = instance.Value.GetComponent<CookingStation>();
				if (station == null)
				{
					continue;
				}
				if (station.IsFireLit())
				{
					cooking.Add(instance.Key.GetPosition());
				}
				else if (instance.Key.GetLong(ZDOVars.s_creator, 0L) == 0L && FreeSlots(station) == station.m_slots.Length)
				{
					idle.Add(instance.Value);
				}
			}
			int forgotten = VillageRecords.Remove(village, (PieceRecord record) => IsStation(record.Prefab) && Utils.DistanceXZ(record.Position, oldFire) <= StationReach && !cooking.Exists((Vector3 standing) => Vector3.Distance(standing, record.Position) <= 0.5f));
			foreach (ZNetView view in idle)
			{
				if (!view.IsOwner())
				{
					view.ClaimOwnership();
				}
				ZNetScene.instance.Destroy(view.gameObject);
			}
			Plugin.Log.LogInfo("Moved the cooking stations of the village at " + village.Farm.ToString("F0") + " to its new fire - forgot " + forgotten + ", took down " + idle.Count);
		}

		/// <summary>
		/// Places cooking stations over a fire - two back to back across its middle, each facing out, when it has room
		/// for two in its flames, or else one - each as far out from its middle as it can stand and still cook
		/// </summary>
		/// <param name="fire">The fire, lit</param>
		/// <param name="count">How many it wants</param>
		/// <param name="stationPrefab">The station's prefab name (see StationFor)</param>
		/// <returns>The stations placed, as they'd be noted among the village's pieces</returns>
		private static List<PieceRecord> Place(Fireplace fire, int count, string stationPrefab)
		{
			List<PieceRecord> built = new List<PieceRecord>();
			GameObject prefab = ZNetScene.instance.GetPrefab(stationPrefab);
			CookingStation station = ((prefab != null) ? prefab.GetComponent<CookingStation>() : null);
			if (station == null)
			{
				Plugin.Log.LogWarning("Couldn't find the cooking station to give the village's fire");
				return(built);
			}
			List<Vector3> checks = new List<Vector3>();
			if (station.m_fireCheckPoints != null)
			{
				foreach (Transform point in station.m_fireCheckPoints)
				{
					checks.Add(prefab.transform.InverseTransformPoint(point.position));
				}
			}
			if (checks.Count == 0)
			{
				checks.Add(Vector3.zero);
			}
			float depth = (VillageFurnishing.Footprint(stationPrefab, out Bounds size) ? size.extents.z : 0.3f);
			Vector3 middle = fire.transform.position;
			// The spots are tried from the farthest out in, so a station stands as far out of the fire as it can while
			// still cooking, rather than buried in its middle
			if (count >= 2)
			{
				// Two stations stand back to back across the fire's middle, each at least its depth out from it
				for (float offset = MaxOffset; offset >= depth + Spacing; offset -= OffsetStep)
				{
					for (float angle = 0f; angle < 180f; angle += AngleStep)
					{
						Vector3 out1 = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
						Vector3 first = OnGround(middle + out1 * offset);
						Vector3 second = OnGround(middle - out1 * offset);
						Quaternion firstFacing = Quaternion.LookRotation(out1);
						Quaternion secondFacing = Quaternion.LookRotation(-out1);
						if (OverFire(first, firstFacing, checks, station.m_fireCheckRadius) && OverFire(second, secondFacing, checks, station.m_fireCheckRadius))
						{
							built.Add(Build(prefab, first, firstFacing));
							built.Add(Build(prefab, second, secondFacing));
							return(built);
						}
					}
				}
			}
			for (float offset = MaxOffset; offset >= 0f; offset -= OffsetStep)
			{
				for (float angle = 0f; angle < 360f; angle += AngleStep)
				{
					Vector3 outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
					Vector3 spot = OnGround(middle + outward * offset);
					Quaternion facing = Quaternion.LookRotation(outward);
					if (OverFire(spot, facing, checks, station.m_fireCheckRadius))
					{
						built.Add(Build(prefab, spot, facing));
						return(built);
					}
				}
			}
			// A station too long for the fire's flames to reach all its check points still cooks over a village's
			// bonfire with one of them in its flames (see OnVillageFire) - two back to back when there's room, as far out
			// as that still lets them stand
			if (!IsBonfire(fire))
			{
				Plugin.Log.LogWarning("Found no spot over the fire at " + middle.ToString("F0") + " where a " + stationPrefab + " would cook");
				return(built);
			}
			float radius = (station.m_cheapFireCheck ? 0.25f : station.m_fireCheckRadius);
			if (count >= 2)
			{
				for (float offset = MaxOffset; offset >= depth + Spacing; offset -= OffsetStep)
				{
					for (float angle = 0f; angle < 180f; angle += AngleStep)
					{
						Vector3 out1 = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
						Vector3 first = OnGround(middle + out1 * offset);
						Vector3 second = OnGround(middle - out1 * offset);
						Quaternion firstFacing = Quaternion.LookRotation(out1);
						Quaternion secondFacing = Quaternion.LookRotation(-out1);
						if (PartlyInFlames(fire, first, firstFacing, checks, radius) && PartlyInFlames(fire, second, secondFacing, checks, radius))
						{
							built.Add(Build(prefab, first, firstFacing));
							built.Add(Build(prefab, second, secondFacing));
							return(built);
						}
					}
				}
			}
			for (float offset = MaxOffset; offset >= 0f; offset -= OffsetStep)
			{
				for (float angle = 0f; angle < 360f; angle += AngleStep)
				{
					Vector3 outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
					Vector3 spot = OnGround(middle + outward * offset);
					Quaternion facing = Quaternion.LookRotation(outward);
					if (PartlyInFlames(fire, spot, facing, checks, radius))
					{
						built.Add(Build(prefab, spot, facing));
						return(built);
					}
				}
			}
			Plugin.Log.LogWarning("Found no spot over the fire at " + middle.ToString("F0") + " where a cooking station would cook");
			return(built);
		}

		/// <summary>
		/// Puts a point on the ground
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>The point at the ground's height</returns>
		private static Vector3 OnGround(Vector3 point)
		{
			point.y = ZoneSystem.instance.GetGroundHeight(point);
			return(point);
		}

		/// <summary>
		/// Checks whether a cooking station placed at a spot would cook - every one of its fire check points in a fire's
		/// flames
		/// </summary>
		/// <param name="spot">Where it would stand</param>
		/// <param name="facing">Which way it would face</param>
		/// <param name="checks">Its fire check points, around its own origin</param>
		/// <param name="radius">How near the flames each point has to be</param>
		/// <returns>True when it would</returns>
		private static bool OverFire(Vector3 spot, Quaternion facing, List<Vector3> checks, float radius)
		{
			foreach (Vector3 check in checks)
			{
				if (EffectArea.IsPointInsideArea(spot + facing * check, EffectArea.Type.Burning, radius) == null)
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Checks whether a cooking station placed at a spot would have any one of its fire check points in a fire's own
		/// flames, so the village's fire runs it (see OnVillageFire)
		/// </summary>
		/// <param name="fire">The village's fire</param>
		/// <param name="spot">Where it would stand</param>
		/// <param name="facing">Which way it would face</param>
		/// <param name="checks">Its fire check points, around its own origin</param>
		/// <param name="radius">How near the flames a point has to be</param>
		/// <returns>True when it would</returns>
		private static bool PartlyInFlames(Fireplace fire, Vector3 spot, Quaternion facing, List<Vector3> checks, float radius)
		{
			foreach (Vector3 check in checks)
			{
				if (InFlames(fire, spot + facing * check, radius))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Builds a cooking station, as a piece no player built
		/// </summary>
		/// <param name="prefab">The station's prefab</param>
		/// <param name="spot">Where it stands</param>
		/// <param name="facing">Which way it faces</param>
		/// <returns>The station, as it's noted among the village's pieces</returns>
		private static PieceRecord Build(GameObject prefab, Vector3 spot, Quaternion facing)
		{
			VillageRecords.MarkBuilt(Object.Instantiate(prefab, spot, facing));
			int hash = prefab.name.GetStableHashCode();
			return(new PieceRecord { Prefab = hash, Position = spot, Rotation = facing, Wood = VillageRecords.WoodCost(hash) });
		}

		/// <summary>
		/// Lists a village's loaded cooking stations, nearest a point first
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="near">The point</param>
		/// <returns>The stations</returns>
		public static List<CookingStation> Stations(Village village, Vector3 near)
		{
			List<CookingStation> stations = new List<CookingStation>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (!IsStation(instance.Key.GetPrefab()) || instance.Value == null || !instance.Value.IsValid() || !village.OwnsPiece(instance.Value.transform.position))
				{
					continue;
				}
				CookingStation station = instance.Value.GetComponent<CookingStation>();
				if (station != null)
				{
					stations.Add(station);
				}
			}
			stations.Sort((CookingStation a, CookingStation b) => Vector3.Distance(a.transform.position, near).CompareTo(Vector3.Distance(b.transform.position, near)));
			return(stations);
		}

		/// <summary>
		/// Checks whether an item is raw food the cooking station cooks, like raw meat
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		public static bool IsRaw(ItemDrop.ItemData item)
		{
			return(item != null && item.m_dropPrefab != null && IsRaw(item.m_dropPrefab.name));
		}

		/// <summary>
		/// Checks whether an item is raw food the cooking station cooks, like raw meat, by its prefab name
		/// </summary>
		/// <param name="name">The item's prefab name</param>
		/// <returns>True when it is</returns>
		public static bool IsRaw(string name)
		{
			// What either station cooks - the iron one cooks the wooden one's and more
			if (raws == null)
			{
				if (ZNetScene.instance == null)
				{
					return(false);
				}
				raws = new HashSet<string>();
				foreach (string stationPrefab in new[] { StationPrefab, IronStationPrefab })
				{
					GameObject prefab = ZNetScene.instance.GetPrefab(stationPrefab);
					CookingStation station = ((prefab != null) ? prefab.GetComponent<CookingStation>() : null);
					if (station == null)
					{
						continue;
					}
					foreach (CookingStation.ItemConversion conversion in station.m_conversion)
					{
						if (conversion.m_from != null)
						{
							raws.Add(conversion.m_from.gameObject.name);
						}
					}
				}
			}
			return(raws.Contains(name));
		}

		/// <summary>
		/// The items the cauldron's recipes take, by prefab name
		/// </summary>
		private static HashSet<string> cauldronIngredients;

		/// <summary>
		/// Checks whether an item is something one of the cauldron's recipes takes, like thistle for sausages
		/// </summary>
		/// <param name="name">The item's prefab name</param>
		/// <returns>True when it is</returns>
		public static bool IsCauldronIngredient(string name)
		{
			if (cauldronIngredients == null)
			{
				if (ObjectDB.instance == null || ObjectDB.instance.m_recipes.Count == 0)
				{
					return(false);
				}
				cauldronIngredients = new HashSet<string>();
				foreach (Recipe recipe in ObjectDB.instance.m_recipes)
				{
					if (recipe == null || recipe.m_craftingStation == null || recipe.m_craftingStation.gameObject.name != CauldronPrefab)
					{
						continue;
					}
					foreach (Piece.Requirement requirement in recipe.m_resources)
					{
						if (requirement.m_resItem != null)
						{
							cauldronIngredients.Add(requirement.m_resItem.gameObject.name);
						}
					}
				}
			}
			return(cauldronIngredients.Contains(name));
		}

		/// <summary>
		/// Checks whether a cooking station cooks an item
		/// </summary>
		/// <param name="station">The station</param>
		/// <param name="item">The item</param>
		/// <returns>True when it does</returns>
		public static bool Cooks(CookingStation station, ItemDrop.ItemData item)
		{
			return(item != null && item.m_dropPrefab != null && station.IsItemAllowed(item.m_dropPrefab.name));
		}

		/// <summary>
		/// Checks whether a cooking station's fire is lit, so it cooks
		/// </summary>
		/// <param name="station">The station</param>
		/// <returns>True when it is</returns>
		public static bool IsLit(CookingStation station)
		{
			return(station != null && station.m_nview != null && station.m_nview.IsValid() && station.IsFireLit());
		}

		/// <summary>
		/// Counts a cooking station's empty slots
		/// </summary>
		/// <param name="station">The station</param>
		/// <returns>How many</returns>
		public static int FreeSlots(CookingStation station)
		{
			int free = 0;
			for (int i = 0; i < station.m_slots.Length; i++)
			{
				station.GetSlot(i, out string name, out float _, out CookingStation.Status _, out bool _);
				if (name == "")
				{
					free++;
				}
			}
			return(free);
		}

		/// <summary>
		/// Puts raw food on a cooking station's next empty slot, the way a player does
		/// </summary>
		/// <param name="station">The station</param>
		/// <param name="prefabName">The food's prefab</param>
		public static void Add(CookingStation station, string prefabName)
		{
			if (!station.m_nview.IsOwner())
			{
				station.m_nview.ClaimOwnership();
			}
			station.m_nview.InvokeRPC("RPC_AddItem", prefabName, false);
		}

		/// <summary>
		/// Takes everything done off a cooking station, taking the station over first so it can be changed
		/// </summary>
		/// <param name="station">The station</param>
		/// <returns>The prefabs of what came off</returns>
		public static List<string> TakeDone(CookingStation station)
		{
			List<string> taken = new List<string>();
			if (!station.m_nview.IsOwner())
			{
				station.m_nview.ClaimOwnership();
			}
			for (int i = 0; i < station.m_slots.Length; i++)
			{
				station.GetSlot(i, out string name, out float _, out CookingStation.Status _, out bool _);
				if (name == "" || !station.IsItemDone(name))
				{
					continue;
				}
				taken.Add(name);
				station.SetSlot(i, "", 0f, CookingStation.Status.NotDone, false, 0L);
				station.m_nview.InvokeRPC(ZNetView.Everybody, "RPC_SetSlotVisual", i, "");
			}
			return(taken);
		}
	}
}
