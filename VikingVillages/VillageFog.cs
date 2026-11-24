using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Pushes back the fog around a village's burning bonfire or campfire - the nearer the camera is to it, the thinner
	/// the weather's fog, down to ClearShare of it within ClearReach and back to all of it by FadeReach - fading in and
	/// out as a fire lights or goes out. A fire in the Mistlands leaves the mist as it is
	/// </summary>
	/// <remarks>
	/// The game's fog is one density for everything in view, set from the weather every update (see EnvMan.SetEnv), so
	/// it's thinned for the whole view by how near the fire the camera is rather than in a patch around the fire
	/// </remarks>
	internal static class VillageFog
	{
		/// <summary>
		/// How much of the weather's fog is left at the fire, how near it the fog is pushed back that far, and how far from
		/// it the fog is back to all of it, in meters
		/// </summary>
		private const float ClearShare = 0f;
		private const float ClearReach = 15f;
		private const float FadeReach = 45f;

		/// <summary>
		/// How fast the fog thins or thickens as a fire lights or goes out, as a share of the weather's fog a second, and
		/// how often the villages' fires are looked at, in seconds
		/// </summary>
		private const float FadeRate = 0.4f;
		private const float FireCheckInterval = 1f;

		/// <summary>
		/// Where the loaded villages' burning fires are, as last looked at, when they're next looked at, and the share of
		/// the weather's fog left now
		/// </summary>
		private static readonly List<Vector3> fires = new List<Vector3>();
		private static float nextFireCheck;
		private static float share = 1f;

		/// <summary>
		/// The weather's own fog density at the last update, before it was thinned
		/// </summary>
		private static float weatherDensity;

		/// <summary>
		/// Thins the fog the game has just set by how near the camera is to a village's burning fire - call it once the
		/// game has set the fog for the update
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		public static void Apply(float dt)
		{
			if (Time.time >= nextFireCheck)
			{
				nextFireCheck = Time.time + FireCheckInterval;
				FindFires();
			}
			Camera camera = Utils.GetMainCamera();
			float target = ((camera != null) ? Target(camera.transform.position, fires) : 1f);
			share = Mathf.MoveTowards(share, target, FadeRate * dt);
			weatherDensity = RenderSettings.fogDensity;
			if (share < 1f)
			{
				RenderSettings.fogDensity *= share;
			}
		}

		/// <summary>
		/// Describes the fog for the debug hover text of a village's fire - the weather, its fog's density, and how much
		/// of it the village fires leave
		/// </summary>
		/// <returns>The line, ending in a line break</returns>
		public static string Describe()
		{
			EnvSetup environment = ((EnvMan.instance != null) ? EnvMan.instance.GetCurrentEnvironment() : null);
			string weather = ((environment != null) ? environment.m_name : "unknown weather");
			return("Fog: " + weather + ", density " + weatherDensity.ToString("0.0000") + ", " + Mathf.RoundToInt(share * 100f) + "% of it left, " + fires.Count + " burning village fires loaded\n");
		}

		/// <summary>
		/// Works out how much of the weather's fog is left at a spot, by how near the nearest burning village fire is
		/// </summary>
		/// <param name="point">The spot</param>
		/// <param name="burning">Where the burning village fires are</param>
		/// <returns>The share, from ClearShare to 1, or 1 with no fires</returns>
		internal static float Target(Vector3 point, List<Vector3> burning)
		{
			float nearest = float.MaxValue;
			foreach (Vector3 fire in burning)
			{
				nearest = Mathf.Min(nearest, Utils.DistanceXZ(fire, point));
			}
			float far = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((nearest - ClearReach) / (FadeReach - ClearReach)));
			return(Mathf.Lerp(ClearShare, 1f, far));
		}

		/// <summary>
		/// Notes where the loaded villages' fires are that are burning, while villages are on - but for those in the
		/// Mistlands, whose mist a fire doesn't push back, which their wisp lights do (see VillageWisps)
		/// </summary>
		private static void FindFires()
		{
			fires.Clear();
			if (!ServerCheck.Active)
			{
				return;
			}
			foreach (Village village in Village.Loaded())
			{
				Fireplace fire = village.Fire();
				if (fire != null && fire.m_nview != null && fire.m_nview.IsValid() && fire.IsBurning() && !InMistlands(fire.transform.position))
				{
					fires.Add(fire.transform.position);
				}
			}
		}

		/// <summary>
		/// Checks whether a spot is in the Mistlands
		/// </summary>
		/// <param name="point">The spot</param>
		/// <returns>True when it is</returns>
		private static bool InMistlands(Vector3 point)
		{
			return(WorldGenerator.instance != null && WorldGenerator.instance.GetBiome(point) == Heightmap.Biome.Mistlands);
		}
	}
}
