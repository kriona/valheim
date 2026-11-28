using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A valkyrie catching her rider - a player who gets off her in the air, with nothing to soften their fall and a
	/// dangerous fall below them, is caught in her talons the way the valkyrie carries you in at the start of the game,
	/// and she flies down with them and lets go once they're safely near the ground (see ValkyrieCarried). Over deep
	/// water, she carries them to the nearest land she finds first (see NearestLand)
	/// </summary>
	/// <remarks>
	/// The client the rider plays on decides, since it owns them - it holds them at her talon, lets go of them, and
	/// tells the client that owns her, which flies her down until then
	/// </remarks>
	internal class ValkyrieCatch : MonoBehaviour
	{
		/// <summary>
		/// The RPCs on her that she's caught her rider, and that she's let go of them
		/// </summary>
		public const string CaughtRPC = "kri_vv_valkyrie_caught";
		public const string ReleasedRPC = "kri_vv_valkyrie_released";

		/// <summary>
		/// How far above the ground a fall has to start for her to catch her rider, in meters - the game's fall damage
		/// starts at 4 and kills at 20
		/// </summary>
		internal const float CatchHeight = 10f;

		/// <summary>
		/// How near the ground, or the water, she lets go of them, in meters
		/// </summary>
		internal const float ReleaseHeight = 1.5f;

		/// <summary>
		/// How much longer than flying them down from where she caught them takes she carries them at most, in seconds,
		/// in case they never get near the ground (see CatchLimit)
		/// </summary>
		internal const float CatchMargin = 10f;

		/// <summary>
		/// How fast she flies down with them near the ground, in meters a second - as fast as a player runs, holding the
		/// run key - and how fast higher up, so a long way down doesn't take long
		/// </summary>
		internal const float DescentSpeed = 7f;
		internal const float FastDescentSpeed = 20f;

		/// <summary>
		/// How high above the ground, or the water, she flies down at DescentSpeed, in meters, and over how many meters
		/// above that she slows from FastDescentSpeed to it
		/// </summary>
		internal const float SlowHeight = 30f;
		internal const float SlowBlend = 20f;

		/// <summary>
		/// How far off she looks for land to carry someone to over deep water, in meters, how far apart the rings she
		/// looks along are, how many ways around each ring she looks, how many times she halves the way between two rings
		/// to find the shore, and how far above the water land has to be to count
		/// </summary>
		internal const float LandReach = 300f;
		internal const float LandStep = 10f;
		internal const int LandDirections = 36;
		internal const int ShoreSteps = 5;
		internal const float LandAbove = 1f;

		/// <summary>
		/// How fast she flies someone across the water toward land, in meters a second, the least height above the water
		/// she comes down to on the way, in meters, over how many meters above it she stops coming down, and how near the
		/// shore she's flying for she has to be to come straight down there
		/// </summary>
		internal const float CrossSpeed = 15f;
		internal const float CrossHeight = 10f;
		internal const float CrossBlend = 3f;
		internal const float ArriveDistance = 3f;

		/// <summary>
		/// The ZDO key saying she's carrying her rider down, so every client spreads her wings wide (see ValkyrieModel)
		/// </summary>
		private static readonly int CatchingKey = "kri_vv_valkyrie_catching".GetStableHashCode();

		/// <summary>
		/// The cape that softens a player's fall
		/// </summary>
		private static readonly int FeatherCape = "CapeFeather".GetStableHashCode();

		private Character character;
		private ZNetView nview;

		/// <summary>
		/// Until when she carries her rider down, by the game's time, or below 0 when she isn't, the longest she carries
		/// them, in seconds, who she carries, and where she carries them to - the land she flies them to over water, or
		/// straight below where she caught them
		/// </summary>
		private float catchUntil = -1f;
		private float catchLimit;
		private GameObject caught;
		private Vector3 catchTarget;

		/// <summary>
		/// Has two bodies pass through each other or not - a rider she carries hangs right under her, and touching her
		/// would have her stand on them as on the ground
		/// </summary>
		/// <param name="a">One body</param>
		/// <param name="b">The other</param>
		/// <param name="ignore">True to pass through each other, false to collide again</param>
		public static void IgnoreCollisions(GameObject a, GameObject b, bool ignore)
		{
			if (a == null || b == null)
			{
				return;
			}
			Collider[] theirs = b.GetComponentsInChildren<Collider>(true);
			foreach (Collider mine in a.GetComponentsInChildren<Collider>(true))
			{
				foreach (Collider other in theirs)
				{
					Physics.IgnoreCollision(mine, other, ignore);
				}
			}
		}

		/// <summary>
		/// Finds her parts and listens for her rider being caught and let go - and on the client that owns her, clears a
		/// catch left over from before she was last loaded
		/// </summary>
		private void Awake()
		{
			character = GetComponent<Character>();
			nview = GetComponent<ZNetView>();
			if (nview != null && nview.IsValid())
			{
				nview.Register<ZDOID, Vector3>(CaughtRPC, RPC_Caught);
				nview.Register(ReleasedRPC, RPC_Released);
				if (nview.IsOwner() && Catching())
				{
					nview.GetZDO().Set(CatchingKey, false);
				}
			}
		}

		/// <summary>
		/// Checks whether a fall is one she catches her rider from - onto ground more than CatchHeight below them, and
		/// not into water deep enough to swim in, which breaks a fall
		/// </summary>
		/// <param name="feet">The height of their feet</param>
		/// <param name="ground">The height of the ground below them</param>
		/// <param name="water">The height of the water</param>
		/// <param name="swimDepth">How deep water has to be to swim in, in meters</param>
		/// <returns>True when she catches them</returns>
		internal static bool DangerousFall(float feet, float ground, float water, float swimDepth)
		{
			return(!OverDeepWater(ground, water, swimDepth) && feet - ground > CatchHeight);
		}

		/// <summary>
		/// Checks whether the water below someone is deep enough to swim in
		/// </summary>
		/// <param name="ground">The height of the ground below them</param>
		/// <param name="water">The height of the water</param>
		/// <param name="swimDepth">How deep water has to be to swim in, in meters</param>
		/// <returns>True when it is</returns>
		internal static bool OverDeepWater(float ground, float water, float swimDepth)
		{
			return(water - ground >= swimDepth);
		}

		/// <summary>
		/// Finds the nearest land to a point, looking along rings LandStep apart out to LandReach, LandDirections ways
		/// around each, and on the first ring that reaches land, finding the shore along each way that does by halving
		/// the way back to the ring before ShoreSteps times - the nearest of those shores
		/// </summary>
		/// <param name="from">The point, across the ground</param>
		/// <param name="height">The height of the world's ground at a point across it</param>
		/// <param name="water">The height of the water</param>
		/// <param name="land">The nearest land found, across the ground</param>
		/// <returns>True when there's land within LandReach</returns>
		internal static bool NearestLand(Vector2 from, System.Func<float, float, float> height, float water, out Vector2 land)
		{
			land = from;
			float nearest = float.MaxValue;
			for (int ring = 1; ring * LandStep <= LandReach; ring++)
			{
				float radius = ring * LandStep;
				for (int i = 0; i < LandDirections; i++)
				{
					float angle = i * Mathf.PI * 2f / LandDirections;
					Vector2 way = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
					if (!IsLand(from + way * radius, height, water))
					{
						continue;
					}
					float near = radius - LandStep;
					float far = radius;
					for (int step = 0; step < ShoreSteps; step++)
					{
						float middle = (near + far) / 2f;
						if (IsLand(from + way * middle, height, water))
						{
							far = middle;
						}
						else
						{
							near = middle;
						}
					}
					if (far < nearest)
					{
						nearest = far;
						land = from + way * far;
					}
				}
				if (nearest < float.MaxValue)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a point is land - its ground at least LandAbove above the water
		/// </summary>
		/// <param name="point">The point, across the ground</param>
		/// <param name="height">The height of the world's ground at a point across it</param>
		/// <param name="water">The height of the water</param>
		/// <returns>True when it is</returns>
		private static bool IsLand(Vector2 point, System.Func<float, float, float> height, float water)
		{
			return(height(point.x, point.y) >= water + LandAbove);
		}

		/// <summary>
		/// Works out how fast and which way she carries someone - over land, or once she's near the shore she's flying
		/// for, straight down (see DescentSpeedAt); otherwise across toward it at CrossSpeed, coming down the same way
		/// but no lower than CrossHeight above the water, or climbing over something in her way (see
		/// ValkyrieMotion.Avoid)
		/// </summary>
		/// <param name="toTarget">The way to where she carries them, across the ground, in meters</param>
		/// <param name="altitude">How high she is above the ground or the water, in meters</param>
		/// <param name="overLand">Whether there's land below her rather than water</param>
		/// <param name="hitDistance">How far ahead something's in her way, in meters, or below 0 when nothing is</param>
		/// <returns>Her velocity, in meters a second</returns>
		internal static Vector3 CarryVelocity(Vector2 toTarget, float altitude, bool overLand, float hitDistance)
		{
			if (overLand || toTarget.magnitude <= ArriveDistance)
			{
				return(Vector3.down * DescentSpeedAt(altitude));
			}
			Vector2 way = toTarget.normalized;
			Vector3 across = new Vector3(way.x, 0f, way.y);
			if (hitDistance >= 0f)
			{
				return(ValkyrieMotion.Avoid(across, hitDistance) * CrossSpeed);
			}
			float down = DescentSpeedAt(altitude) * Mathf.Clamp01((altitude - CrossHeight) / CrossBlend);
			return(across * CrossSpeed + Vector3.down * down);
		}

		/// <summary>
		/// Checks whether she's carried them near enough the ground, or the water below them, to let go
		/// </summary>
		/// <param name="feet">The height of their feet</param>
		/// <param name="ground">The height of the ground below them</param>
		/// <param name="water">The height of the water</param>
		/// <returns>True when she lets go</returns>
		internal static bool LetsGo(float feet, float ground, float water)
		{
			return(feet - Mathf.Max(ground, water) <= ReleaseHeight);
		}

		/// <summary>
		/// Works out how fast she flies down with someone at a height - DescentSpeed within SlowHeight of the ground or
		/// the water, FastDescentSpeed from SlowBlend above that, and in between, faster the higher she is
		/// </summary>
		/// <param name="altitude">How high she is above the ground or the water, in meters</param>
		/// <returns>Her speed down, in meters a second</returns>
		internal static float DescentSpeedAt(float altitude)
		{
			float blend = Mathf.Clamp01((altitude - SlowHeight) / SlowBlend);
			return(Mathf.Lerp(DescentSpeed, FastDescentSpeed, blend));
		}

		/// <summary>
		/// Works out how long she takes to fly someone down from a height to where she lets go of them (see LetsGo),
		/// at the speeds DescentSpeedAt gives - the slowing stretch taken by its speed rising evenly with height
		/// </summary>
		/// <param name="altitude">How high their feet are above the ground or the water, in meters</param>
		/// <returns>How long it takes, in seconds</returns>
		internal static float DescentTime(float altitude)
		{
			float slow = Mathf.Max(0f, Mathf.Min(altitude, SlowHeight) - ReleaseHeight) / DescentSpeed;
			float blendDrop = Mathf.Clamp(altitude - SlowHeight, 0f, SlowBlend);
			float blend = 0f;
			if (blendDrop > 0f)
			{
				// Speed rising evenly from DescentSpeed takes (SlowBlend / rise) * ln(speed reached / DescentSpeed)
				float rise = FastDescentSpeed - DescentSpeed;
				blend = (SlowBlend / rise) * Mathf.Log(DescentSpeedAt(SlowHeight + blendDrop) / DescentSpeed);
			}
			float fast = Mathf.Max(0f, altitude - SlowHeight - SlowBlend) / FastDescentSpeed;
			return(slow + blend + fast);
		}

		/// <summary>
		/// Works out the longest she carries someone - as long as flying them down from where she caught them takes
		/// (see DescentTime), as long as flying them across to land at CrossSpeed takes on top, and CatchMargin more
		/// </summary>
		/// <param name="feet">The height of their feet when she caught them</param>
		/// <param name="ground">The height of the ground below them</param>
		/// <param name="water">The height of the water</param>
		/// <param name="across">How far across the ground she carries them, in meters</param>
		/// <returns>The longest she carries them, in seconds</returns>
		internal static float CatchLimit(float feet, float ground, float water, float across)
		{
			return(DescentTime(feet - Mathf.Max(ground, water)) + across / CrossSpeed + CatchMargin);
		}

		/// <summary>
		/// Works out the longest she carries someone from a point to where she carries them (see CatchLimit)
		/// </summary>
		/// <param name="point">Where their feet are when she catches them</param>
		/// <param name="target">Where she carries them to</param>
		/// <returns>The longest she carries them, in seconds</returns>
		internal static float CatchLimitFor(Vector3 point, Vector3 target)
		{
			float across = new Vector2(target.x - point.x, target.z - point.z).magnitude;
			return(DescentTime(AltitudeOf(point, out bool _)) + across / CrossSpeed + CatchMargin);
		}

		/// <summary>
		/// Works out how high a point is above the ground or the water below it - over no ground found, above the water
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="overLand">Whether there's ground below it above the water</param>
		/// <returns>Its height above them, in meters</returns>
		internal static float AltitudeOf(Vector3 point, out bool overLand)
		{
			float water = WaterLevel();
			overLand = GroundBelow(point, out float ground) && ground >= water;
			if (!overLand)
			{
				ground = water;
			}
			return(point.y - Mathf.Max(ground, water));
		}

		/// <summary>
		/// The height of the ground right below a point - the first solid thing a ray down from it hits
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="ground">The height of the ground</param>
		/// <returns>True when there's ground below it</returns>
		internal static bool GroundBelow(Vector3 point, out float ground)
		{
			ground = 0f;
			return(ZoneSystem.instance != null && ZoneSystem.instance.GetSolidHeight(point + Vector3.up * 0.1f, out ground, 0));
		}

		/// <summary>
		/// The height of the sea
		/// </summary>
		/// <returns>The height, or far below anything when there's no world</returns>
		internal static float WaterLevel()
		{
			return(((ZoneSystem.instance != null) ? ZoneSystem.instance.m_waterLevel : -10000f));
		}

		/// <summary>
		/// Catches the local player as they get off a valkyrie, on their client, when she's alive and flying and nothing
		/// softens their fall - over a dangerous fall (see DangerousFall), to carry them down, or more than CatchHeight
		/// above deep water with land within LandReach, to carry them there (see NearestLand) - and she shouts so, the
		/// way vikings call out danger, for as long and as far off, with nothing she says covering it (see
		/// ValkyrieLines.Catch)
		/// </summary>
		/// <param name="saddle">Her saddle</param>
		/// <param name="player">The player getting off</param>
		public static void AfterDismount(Sadle saddle, Player player)
		{
			if (player == null || player != Player.m_localPlayer || player.IsDead() || saddle == null || saddle.m_character == null)
			{
				return;
			}
			Character valkyrie = saddle.m_character;
			if (valkyrie.IsDead() || !valkyrie.TryGetComponent(out ValkyrieCatch _) || !valkyrie.TryGetComponent(out ValkyrieFlight flight) || !flight.IsFlying())
			{
				return;
			}
			Vector3 feet = player.transform.position;
			float water = WaterLevel();
			if (Cushioned(player) || !GroundBelow(feet, out float ground))
			{
				return;
			}
			Vector3 target = feet;
			if (OverDeepWater(ground, water, player.m_swimDepth))
			{
				if (feet.y - water <= CatchHeight || WorldGenerator.instance == null || !NearestLand(new Vector2(feet.x, feet.z), WorldGenerator.instance.GetHeight, water, out Vector2 land))
				{
					return;
				}
				target = new Vector3(land.x, feet.y, land.y);
			}
			else if (!DangerousFall(feet.y, ground, water, player.m_swimDepth))
			{
				return;
			}
			if (ValkyrieCarried.Catch(player, valkyrie.GetZDOID(), target))
			{
				VikingSpeech.SayFirst(valkyrie, ValkyrieLines.Catch(Random.value), VikingSpeech.CallTime, VikingSpeech.CallCull);
			}
		}

		/// <summary>
		/// Checks whether something softens a player's fall - a feather cape, or anything else that takes away all of
		/// the game's fall damage
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>True when it does</returns>
		private static bool Cushioned(Player player)
		{
			if (player.m_nview != null && player.m_nview.IsValid() && player.m_nview.GetZDO().GetInt(ZDOVars.s_shoulderItem) == FeatherCape)
			{
				return(true);
			}
			float damage = 100f;
			player.GetSEMan().ModifyFallDamage(100f, ref damage);
			return(damage <= 0f);
		}

		/// <summary>
		/// Checks whether she's carrying her rider down, by her ZDO, so every client agrees
		/// </summary>
		/// <returns>True while she is</returns>
		public bool Catching()
		{
			return(nview != null && nview.IsValid() && nview.GetZDO().GetBool(CatchingKey));
		}

		/// <summary>
		/// Flies her down with her rider, on the client that owns her, before her AI's update, until they're let go or
		/// she's carried them as long as it takes to bring them down from where she caught them (see CatchLimit) - keeping
		/// her in the air until then, and her speed itself held after the game moves her (see Steady)
		/// </summary>
		/// <returns>True while she's carrying them down, so her AI leaves her be this update</returns>
		public bool Think()
		{
			if (catchUntil < 0f)
			{
				// A catch another client was carrying out when she became this one's
				if (Catching())
				{
					nview.GetZDO().Set(CatchingKey, false);
				}
				return(false);
			}
			if (Time.time >= catchUntil)
			{
				Stop("after " + catchLimit.ToString("0") + " seconds");
				return(false);
			}
			KeepFlying();
			Vector2 toTarget = new Vector2(catchTarget.x - transform.position.x, catchTarget.z - transform.position.z);
			if (toTarget.magnitude > ArriveDistance)
			{
				// Facing the way she carries them across, which the game turns her toward
				Vector2 way = toTarget.normalized;
				character.SetMoveDir(new Vector3(way.x, -0.5f, way.y));
			}
			else
			{
				character.SetMoveDir(transform.forward * 0.001f + Vector3.down);
			}
			character.SetRun(false);
			return(true);
		}

		/// <summary>
		/// Takes her up again if anything has landed her while she carries her rider
		/// </summary>
		private void KeepFlying()
		{
			if (!character.IsFlying() && TryGetComponent(out ValkyrieFlight flight))
			{
				flight.TakeOff();
			}
		}

		/// <summary>
		/// Holds her on her way while she carries her rider - straight down, fast high up and slowing near the ground,
		/// or over water across toward land (see CarryVelocity) - on the client that owns her, after the game has worked
		/// out how she flies, so nothing else her flying does, like her clips' own motion, changes her speed
		/// </summary>
		public void Steady()
		{
			if (catchUntil < 0f || character.m_body == null)
			{
				return;
			}
			Vector3 position = transform.position;
			float altitude = AltitudeOf(position, out bool overLand);
			Vector2 toTarget = new Vector2(catchTarget.x - position.x, catchTarget.z - position.z);
			float hit = -1f;
			if (!overLand && toTarget.magnitude > ArriveDistance && TryGetComponent(out ValkyrieFlight flight))
			{
				Vector2 way = toTarget.normalized;
				hit = flight.ObstacleAhead(new Vector3(way.x, 0f, way.y), ValkyrieMotion.LookAhead(CrossSpeed));
			}
			Vector3 velocity = CarryVelocity(toTarget, altitude, overLand, hit);
			character.m_currentVel = velocity;
			character.m_body.linearVelocity = velocity;
		}

		/// <summary>
		/// Starts carrying her rider down once their client has caught them
		/// </summary>
		/// <param name="sender">The client that holds them</param>
		/// <param name="rider">Who she caught</param>
		/// <param name="target">Where she carries them to</param>
		private void RPC_Caught(long sender, ZDOID rider, Vector3 target)
		{
			if (!nview.IsOwner())
			{
				return;
			}
			catchTarget = target;
			catchLimit = CatchLimitFor(transform.position, target);
			float across = new Vector2(target.x - transform.position.x, target.z - transform.position.z).magnitude;
			Plugin.Log.LogInfo("A valkyrie catches her rider at " + transform.position.y.ToString("0.0") + " m, flying " + character.IsFlying() + ", carrying them " + across.ToString("0") + " m across, for at most " + catchLimit.ToString("0") + " seconds");
			catchUntil = Time.time + catchLimit;
			nview.GetZDO().Set(CatchingKey, true);
			caught = ZNetScene.instance.FindInstance(rider);
			IgnoreCollisions(gameObject, caught, true);
			KeepFlying();
		}

		/// <summary>
		/// Stops carrying her rider down once their client has let go of them
		/// </summary>
		/// <param name="sender">The client that held them</param>
		private void RPC_Released(long sender)
		{
			if (nview.IsOwner() && catchUntil >= 0f)
			{
				Stop("as her rider is let go");
			}
		}

		/// <summary>
		/// Stops carrying her rider down, on the client that owns her
		/// </summary>
		/// <param name="why">Why, for the log</param>
		private void Stop(string why)
		{
			catchUntil = -1f;
			nview.GetZDO().Set(CatchingKey, false);
			ValkyriePassThrough.Settle(gameObject, caught);
			caught = null;
			Plugin.Log.LogInfo("A valkyrie stops carrying her rider down at " + transform.position.y.ToString("0.0") + " m, " + why);
		}
	}
}
