using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A valkyrie's flying - her rider takes her up with jump and brings her down with crouch, and with no rider her AI
	/// takes her up to fight what it can't on the ground, and brings her down again once it's done
	/// </summary>
	/// <remarks>
	/// The game keeps whether a creature flies only on the client that owns it, so it's kept in her ZDO too, for the
	/// others to pose her by and for a new owner, like a rider taking her reins, to carry on with
	/// </remarks>
	internal class ValkyrieFlight : MonoBehaviour
	{
		/// <summary>
		/// The ZDO key holding whether she flies
		/// </summary>
		private static readonly int FlyingKey = "kri_vv_valkyrie_flying".GetStableHashCode();

		/// <summary>
		/// How long a rider's held keys count for after they were last read, in seconds
		/// </summary>
		private const float InputLife = 0.25f;

		/// <summary>
		/// How far down the ground is looked for under her, in meters
		/// </summary>
		private const float AltitudeReach = 100f;

		private Character character;
		private ZNetView nview;
		private bool wasOwner;

		/// <summary>
		/// Whether her rider holds jump and crouch, and when that was last read
		/// </summary>
		private bool climbHeld;
		private bool descendHeld;
		private float inputTime = -1f;

		/// <summary>
		/// Whether her rider holds block, and the up part of the way they look, which takes her up or down as she flies
		/// while they do
		/// </summary>
		private bool pitchHeld;
		private float lookUp;

		/// <summary>
		/// When she last took off, by the game's time
		/// </summary>
		private float takeoffTime = -100f;

		/// <summary>
		/// When code last flew her, by the game's time, where to, and whether she lands there (see Pilot)
		/// </summary>
		private float pilotTime = -100f;
		private Vector3 pilotTo;
		private bool pilotLands;

		/// <summary>
		/// Her roosting in a village (see ValkyrieRoost) - whether she's to roost once she's had nothing to fight a while,
		/// whether she's flying to a roof to roost on and which, when she next looks for one, and whether she stands on a
		/// roof as last found and when that's next looked at
		/// </summary>
		private bool roostWanted;
		private bool roosting;
		private Vector3 roostSpot;
		private float nextRoostLook;
		private bool onRoof;
		private float nextRoofCheck;

		/// <summary>
		/// How long she's had nothing to fight before she flies up to roost, how long before she looks for a roof again
		/// when there wasn't one, in seconds, and how far down a roof under her feet is looked for, in meters
		/// </summary>
		private const float RoostIdle = 4f;
		private const float RoostLookInterval = 15f;
		private const float RoofBelow = 1.5f;

		/// <summary>
		/// The way she faces while flown by code (see FlyToward), and when that was last set, by the game's time - it holds
		/// for FacingLife seconds after, in seconds
		/// </summary>
		private Vector3 facing;
		private float facingAt = -100f;
		private const float FacingLife = 0.25f;

		/// <summary>
		/// What her AI goes by in the air, by the game's time - when she last had a target, when she next thinks about
		/// taking to the air now and then and until when she stays up when she does, and when she last checked whether
		/// she can walk to her target and what it found
		/// </summary>
		private float lastTargetTime = -100f;
		private float nextSortieCheck;
		private float sortieUntil = -100f;
		private float nextReachCheck;
		private bool unreachable;

		private Tameable tameable;
		private ValkyrieGrab grab;
		private ValkyrieBanish banish;
		private ValkyrieCatch catcher;
		private ValkyrieSkirmish skirmish;

		/// <summary>
		/// Her model's head bone, which sits where her neck meets her skull
		/// </summary>
		private Transform head;

		/// <summary>
		/// How far above her head bone the top of her head is, in meters at size 1
		/// </summary>
		private const float HeadTop = 0.6f;

		/// <summary>
		/// Finds her parts
		/// </summary>
		private void Awake()
		{
			character = GetComponent<Character>();
			nview = GetComponent<ZNetView>();
			tameable = GetComponent<Tameable>();
			grab = GetComponent<ValkyrieGrab>();
			banish = GetComponent<ValkyrieBanish>();
			catcher = GetComponent<ValkyrieCatch>();
			skirmish = GetComponent<ValkyrieSkirmish>();
			// The hidden Fallen Valkyrie skeleton has a head bone too, so it's looked for under her own model
			ValkyrieModel model = GetComponentInChildren<ValkyrieModel>(true);
			head = ((model != null) ? Utils.FindChild(model.transform, "head") : null);
		}

		/// <summary>
		/// Puts her seat on her once everything on her is awake, after the game has shown or hidden her saddle by her
		/// ZDO (see ValkyrieSeat)
		/// </summary>
		private void Start()
		{
			ValkyrieSeat.Seat(tameable, nview);
		}

		/// <summary>
		/// The top of her head as she's posed, where the game's health bar and other mods' markers go - her body's capsule
		/// reaches far above it
		/// </summary>
		/// <param name="capsuleTop">The top of her body's capsule, used when her head bone is missing</param>
		/// <returns>The point just above her head</returns>
		public Vector3 TopPoint(Vector3 capsuleTop)
		{
			if (head == null)
			{
				return(capsuleTop);
			}
			return(head.position + Vector3.up * (HeadTop * transform.lossyScale.y));
		}

		/// <summary>
		/// Takes her up to fight and brings her down again, on the client that owns her, before her AI's update moves
		/// her - with no rider, she carries down a rider she caught (see ValkyrieCatch), banishes the undead around her
		/// (see ValkyrieBanish), grabs and carries off what she
		/// can (see ValkyrieGrab), takes off to fight a target from the air (see ValkyrieMotion.FightsInTheAir), hit and
		/// run against one that can't strike at range (see ValkyrieSkirmish), and lands when she's done (see
		/// ValkyrieMotion.AILands), coming straight down - or in a village, onto a roof (see ValkyrieRoost). On the ground
		/// in a village after a fight, or asked to (see RoostSoon), she flies up to a roof too
		/// </summary>
		/// <param name="ai">Her AI</param>
		/// <returns>True to let her AI move her this update, false when she's banishing, grabbing, carrying or coming
		/// down</returns>
		public bool ThinkAI(MonsterAI ai)
		{
			// Carrying a rider she caught comes before anything else, even while her saddle still thinks she's ridden for a
			// moment after they got off - her AI doesn't run, so she neither picks a target nor attacks until they're down
			if (catcher != null && catcher.Think())
			{
				return(false);
			}
			if (Piloted())
			{
				FlyPiloted();
				return(false);
			}
			if (Ridden())
			{
				// Where her rider gets off is where she stays
				roostWanted = false;
				roosting = false;
				return(true);
			}
			if ((banish != null && banish.Think()) || (grab != null && grab.Think(ai)))
			{
				return(false);
			}
			float now = Time.time;
			Character target = ai.GetTargetCreature();
			StaticTarget targetStatic = ai.m_targetStatic;
			bool hasTarget = target != null || targetStatic != null;
			bool fightsInTheAir = false;
			if (hasTarget)
			{
				lastTargetTime = now;
				// Once the fight's done, she goes back up to a roof
				roostWanted = true;
				roosting = false;
				Vector3 at = ((target != null) ? target.transform.position : targetStatic.transform.position);
				if (now >= nextReachCheck)
				{
					nextReachCheck = now + ValkyrieMotion.ReachCheck;
					Vector3 under = transform.position + Vector3.down * Mathf.Min(Altitude(), AltitudeReach);
					unreachable = !Pathfinding.instance.HavePath(under, at, ai.m_pathAgentType);
				}
				if (!character.IsFlying() && now >= nextSortieCheck)
				{
					nextSortieCheck = now + ValkyrieMotion.SortieCheck;
					if (Random.value < ValkyrieMotion.SortieChance)
					{
						sortieUntil = now + Random.Range(ValkyrieMotion.SortieMin, ValkyrieMotion.SortieMax);
					}
				}
				float distance = Vector3.Distance(transform.position, at) - ((target != null) ? target.GetRadius() : 0f);
				fightsInTheAir = ValkyrieMotion.FightsInTheAir(target != null && target.IsFlying(), unreachable, distance, now < sortieUntil, ValkyrieSkirmish.HitAndRun(target));
			}
			if (!character.IsFlying())
			{
				roosting = false;
				if (fightsInTheAir)
				{
					TakeOff();
				}
				else if (!hasTarget && roostWanted && now - lastTargetTime > RoostIdle && LookForRoost(now))
				{
					TakeOff();
				}
				return(true);
			}
			if (!ValkyrieMotion.AILands(hasTarget, fightsInTheAir, now - takeoffTime, now - lastTargetTime))
			{
				return(skirmish == null || !skirmish.Think(ai));
			}
			sortieUntil = -100f;
			if (!hasTarget && (roosting || LookForRoost(now)))
			{
				FlyToward(roostSpot, true);
				return(false);
			}
			character.SetMoveDir(transform.forward * 0.001f + Vector3.down);
			character.SetRun(false);
			if (ValkyrieMotion.Lands(-1f, Altitude(), character.IsOnGround()))
			{
				Land();
			}
			return(false);
		}

		/// <summary>
		/// Looks for a roof to roost on, when she's in a village, isn't on a roof already and hasn't looked lately - the
		/// nearest she can stand on (see ValkyrieRoost.FindRoof)
		/// </summary>
		/// <param name="now">The game's time</param>
		/// <returns>True when she's found one to fly to</returns>
		private bool LookForRoost(float now)
		{
			if (now < nextRoostLook || OnRoof())
			{
				return(false);
			}
			nextRoostLook = now + RoostLookInterval;
			Village village = Village.Find(transform.position);
			if (village == null || !village.Contains(transform.position) || !ValkyrieRoost.FindRoof(transform.position, village, out Vector3 spot))
			{
				return(false);
			}
			roostSpot = spot;
			roosting = true;
			roostWanted = false;
			return(true);
		}

		/// <summary>
		/// Has her fly up to a roof to roost on once she's had nothing to fight for a moment, when she's on the ground in a
		/// village - for code that's done with her there, like a traveler she's carried in (see TravelerPet)
		/// </summary>
		public void RoostSoon()
		{
			roostWanted = true;
		}

		/// <summary>
		/// Checks whether she's roosting - standing on a roof with no rider and nothing to fight, which her AI leaves her
		/// standing on rather than wandering off (see ValkyrieAIFlightPatch)
		/// </summary>
		/// <param name="ai">Her AI</param>
		/// <returns>True when she is</returns>
		public bool Roosted(MonsterAI ai)
		{
			return(!character.IsFlying() && !Ridden() && ai.GetTargetCreature() == null && ai.m_targetStatic == null && OnRoof());
		}

		/// <summary>
		/// Checks whether she stands on something built, looked at again every half second
		/// </summary>
		/// <returns>True when she does</returns>
		private bool OnRoof()
		{
			if (Time.time < nextRoofCheck)
			{
				return(onRoof);
			}
			nextRoofCheck = Time.time + 0.5f;
			onRoof = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, RoofBelow + 0.5f, BaseAI.m_solidRayMask, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<WearNTear>() != null;
			return(onRoof);
		}

		/// <summary>
		/// Checks whether she has her rider on her and near, who steers her rather than her AI (see ValkyrieSeat)
		/// </summary>
		/// <returns>True when she's ridden</returns>
		private bool Ridden()
		{
			Sadle saddle = ((tameable != null) ? tameable.m_saddle : null);
			return(saddle != null && saddle.isActiveAndEnabled && saddle.HaveValidUser());
		}

		/// <summary>
		/// Flies her toward a destination, for code steering her rather than a player or her AI - like a viking riding
		/// her. Call it each update on the client that owns her, without anyone in her saddle: she takes off if she's on
		/// the ground, climbs to CruiseHeight above the ground, climbing over what's in her way ahead, and flies there,
		/// fast while it's far off - then hovers over it, or with land, comes down and lands on it and stays there. For
		/// half a second after each call her AI leaves her be, neither landing on its own nor flying off to fight, and
		/// a rider she caught comes first (see ValkyrieCatch). IsFlying says whether she's in the air
		/// </summary>
		/// <param name="destination">Where she flies to</param>
		/// <param name="land">Whether she lands once she's over it</param>
		public void Pilot(Vector3 destination, bool land)
		{
			if (nview == null || !nview.IsValid() || !nview.IsOwner() || character.IsDead())
			{
				return;
			}
			pilotTime = Time.time;
			pilotTo = destination;
			pilotLands = land;
		}

		/// <summary>
		/// Checks whether code is flying her (see Pilot)
		/// </summary>
		/// <returns>True while it has in the last ValkyrieMotion.PilotLife seconds</returns>
		public bool Piloted()
		{
			return(Time.time - pilotTime < ValkyrieMotion.PilotLife);
		}

		/// <summary>
		/// Checks whether she's free to go to someone's help (see ValkyrieHelp) - with no rider, not flown by code and not
		/// carrying down a rider she caught
		/// </summary>
		/// <returns>True when she is</returns>
		public bool Free()
		{
			return(!Ridden() && !Piloted() && (catcher == null || !catcher.Catching()));
		}

		/// <summary>
		/// Flies her toward where code is flying her, on the client that owns her, in place of her AI (see Pilot)
		/// </summary>
		private void FlyPiloted()
		{
			FlyToward(pilotTo, pilotLands);
		}

		/// <summary>
		/// Flies her toward a destination, on the client that owns her - taking off if she's on the ground, at her cruising
		/// height and over what's in her way, facing the way she flies, then hovering over it, or with land, coming down
		/// onto it and staying there
		/// </summary>
		/// <param name="to">Where she flies to</param>
		/// <param name="land">Whether she lands on it</param>
		private void FlyToward(Vector3 to, bool land)
		{
			Vector3 offset = to - transform.position;
			Vector2 across = new Vector2(offset.x, offset.z);
			bool over = across.magnitude < ValkyrieMotion.ArriveRadius;
			bool pilotLands = land;
			if (!character.IsFlying())
			{
				if (over && pilotLands)
				{
					character.SetMoveDir(Vector3.zero);
					return;
				}
				TakeOff();
				return;
			}
			// She faces the way she flies toward it, and keeps facing that way once she's over it (see KeepLevel)
			if (!over)
			{
				facing = new Vector3(across.x, 0f, across.y).normalized;
				character.SetLookDir(facing);
			}
			else if (Time.time - facingAt >= FacingLife)
			{
				// Over it from the start, she keeps facing the way she does
				facing = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
			}
			facingAt = Time.time;
			if (Time.time - takeoffTime < ValkyrieMotion.TakeoffDuration)
			{
				character.SetMoveDir(transform.forward * 0.001f + Vector3.up);
				character.SetRun(false);
				return;
			}
			float altitude = Altitude();
			Vector3 dir = ValkyrieMotion.PilotDirection(across, altitude, pilotLands);
			if (over && pilotLands && ValkyrieMotion.Lands(dir.y, altitude, character.IsOnGround()))
			{
				Land();
				return;
			}
			if (new Vector2(dir.x, dir.z).sqrMagnitude < 0.000001f)
			{
				dir += transform.forward * 0.001f;
			}
			character.SetMoveDir(dir);
			character.SetRun(!over && across.magnitude > ValkyrieMotion.FastDistance);
		}

		/// <summary>
		/// Turns the way she's about to fly up away from water below her (see ValkyrieMotion.AboveWater) and over
		/// something solid in her way ahead (see ValkyrieMotion.Avoid), on the client that owns her - for a rider, her AI
		/// and code alike. Carrying a rider she caught down, she's let be
		/// </summary>
		/// <param name="moveDir">The way she'd fly</param>
		/// <returns>The way she flies</returns>
		public Vector3 Steer(Vector3 moveDir)
		{
			if (catcher != null && catcher.Catching())
			{
				return(moveDir);
			}
			float water = WaterBelow();
			if (water > WaterNone)
			{
				moveDir.y = ValkyrieMotion.AboveWater(moveDir.y, transform.position.y - water);
			}
			Vector3 across = new Vector3(moveDir.x, 0f, moveDir.z);
			if (moveDir.y < 0f || across.sqrMagnitude < 0.01f)
			{
				return(moveDir);
			}
			float speed = (character.m_run ? character.m_flyFastSpeed : character.m_flySlowSpeed) * Mathf.Min(1f, across.magnitude);
			return(ValkyrieMotion.Avoid(moveDir, ObstacleAhead(across.normalized, ValkyrieMotion.LookAhead(speed))));
		}

		/// <summary>
		/// Below this, there's no water under her (see WaterBelow)
		/// </summary>
		private const float WaterNone = -1000f;

		/// <summary>
		/// Finds the surface of the water or tar under her, waves and all - looked for where she is and at the sea's
		/// height below her, since the water's volume may not reach up to where she flies
		/// </summary>
		/// <returns>The surface's height, or far below WaterNone when there's none</returns>
		private float WaterBelow()
		{
			Vector3 position = transform.position;
			float sea = ((ZoneSystem.instance != null) ? ZoneSystem.instance.m_waterLevel : position.y);
			float here = Floating.GetLiquidLevel(position);
			float below = Floating.GetLiquidLevel(new Vector3(position.x, Mathf.Min(position.y, sea), position.z));
			return(Mathf.Max(here, below));
		}

		/// <summary>
		/// Finds how far ahead something solid is in her way, sweeping her body's capsule along the way she's flying
		/// </summary>
		/// <param name="way">The way she's flying, across the ground, of length 1</param>
		/// <param name="distance">How far ahead to look, in meters</param>
		/// <returns>How far off it is, in meters, or -1 when nothing is</returns>
		internal float ObstacleAhead(Vector3 way, float distance)
		{
			CapsuleCollider capsule = GetComponent<CapsuleCollider>();
			if (capsule == null)
			{
				return((Physics.Raycast(transform.position + Vector3.up, way, out RaycastHit ray, distance, BaseAI.m_solidRayMask, QueryTriggerInteraction.Ignore) ? ray.distance : -1f));
			}
			float scale = transform.lossyScale.y;
			float radius = capsule.radius * scale * 0.9f;
			float half = Mathf.Max(0f, capsule.height * scale * 0.5f - radius);
			Vector3 center = transform.TransformPoint(capsule.center);
			if (Physics.CapsuleCast(center + Vector3.down * half, center + Vector3.up * half, radius, way, out RaycastHit hit, distance, BaseAI.m_solidRayMask, QueryTriggerInteraction.Ignore))
			{
				return(hit.distance);
			}
			return(-1f);
		}

		/// <summary>
		/// Checks whether she flies, by her ZDO, so every client agrees
		/// </summary>
		/// <returns>True when she flies</returns>
		public bool IsFlying()
		{
			if (nview == null || !nview.IsValid())
			{
				return(false);
			}
			return(nview.GetZDO().GetBool(FlyingKey));
		}

		/// <summary>
		/// On the client that owns her, carries on flying or not as her ZDO says once she becomes this client's, and keeps
		/// her ZDO up to date with it after
		/// </summary>
		private void FixedUpdate()
		{
			if (!nview.IsValid())
			{
				return;
			}
			bool owner = nview.IsOwner();
			if (owner && !wasOwner)
			{
				character.m_flying = IsFlying();
			}
			wasOwner = owner;
			if (owner && IsFlying() != character.m_flying)
			{
				nview.GetZDO().Set(FlyingKey, character.m_flying);
			}
		}

		/// <summary>
		/// Notes which of the keys that take her up and down her rider holds, on the rider's client, which owns her while
		/// they ride
		/// </summary>
		/// <param name="climb">Whether jump is held</param>
		/// <param name="descend">Whether crouch is held</param>
		public void SetRiderInput(bool climb, bool descend)
		{
			climbHeld = climb;
			descendHeld = descend;
			inputTime = Time.time;
		}

		/// <summary>
		/// Notes whether her rider holds block and how far up or down they look, on the rider's client, which owns her
		/// while they ride - holding it as she flies, she goes up or down the way they look (see ValkyrieMotion.PitchVertical)
		/// </summary>
		/// <param name="block">Whether block is held</param>
		/// <param name="look">The way the rider looks</param>
		public void SetRiderLook(bool block, Vector3 look)
		{
			pitchHeld = block;
			lookUp = look.normalized.y;
		}

		/// <summary>
		/// Moves her up and down after the saddle has moved her across, as the game's riding does each AI update on the
		/// client that owns her - a rider holding jump takes her up off the ground or higher, and holding crouch brings
		/// her down and lands her once she's low enough. With neither held, holding block as she flies takes her up or
		/// down the way they look. With no rider, her AI flies her (see ThinkAI)
		/// </summary>
		/// <param name="moving">Whether the saddle moved her this update</param>
		/// <param name="ridden">Whether she's saddled and tamed with her rider near</param>
		/// <returns>True when the saddle or her flying moved her, so her AI leaves her be this update</returns>
		public bool Ride(bool moving, bool ridden)
		{
			if (!ridden)
			{
				return(moving);
			}
			bool fresh = Time.time - inputTime < InputLife;
			float vertical = ValkyrieMotion.Vertical(fresh && climbHeld, fresh && descendHeld);
			// Jump and crouch win over where the rider looks
			if (vertical == 0f && fresh && pitchHeld && character.IsFlying())
			{
				vertical = ValkyrieMotion.PitchVertical(lookUp);
			}
			if (!character.IsFlying())
			{
				if (vertical > 0f)
				{
					TakeOff();
					return(true);
				}
				return(moving);
			}
			if (Time.time - takeoffTime < ValkyrieMotion.TakeoffDuration)
			{
				vertical = 1f;
			}
			if (ValkyrieMotion.Lands(vertical, Altitude(), character.IsOnGround()))
			{
				Land();
				return(moving);
			}
			Vector3 dir = (moving ? character.GetMoveDir() : transform.forward * 0.001f);
			dir.y = vertical;
			character.SetMoveDir(dir);
			if (!moving)
			{
				character.SetRun(false);
			}
			return(true);
		}

		/// <summary>
		/// Keeps her body level as she flies - the game turns a flying creature toward where it's moving, up and down
		/// too, which would tip her capsule, so she only turns side to side and her model leans instead (see
		/// ValkyrieModel). Flown by code, she's turned to the way she's flying toward where she's going instead, and
		/// keeps facing it as she comes down onto it - the game would turn her to whatever little way she drifts as she
		/// comes down, which could be back the way she came
		/// </summary>
		public void KeepLevel()
		{
			Vector3 forward = transform.forward;
			forward.y = 0f;
			if (Time.time - facingAt < FacingLife && facing.sqrMagnitude > 0.0001f)
			{
				Quaternion toward = Quaternion.LookRotation(facing, Vector3.up);
				Quaternion level = ((forward.sqrMagnitude > 0.0001f) ? Quaternion.LookRotation(forward.normalized, Vector3.up) : toward);
				transform.rotation = Quaternion.RotateTowards(level, toward, character.m_flyTurnSpeed * Time.fixedDeltaTime);
				return;
			}
			if (forward.sqrMagnitude > 0.0001f)
			{
				transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
			}
		}

		/// <summary>
		/// Takes her off the ground, the game's way for a flying creature
		/// </summary>
		internal void TakeOff()
		{
			takeoffTime = Time.time;
			character.TakeOff();
			nview.GetZDO().Set(FlyingKey, true);
		}

		/// <summary>
		/// Lands her, the game's way for a flying creature, dropping her the rest of the way
		/// </summary>
		private void Land()
		{
			character.Land();
			nview.GetZDO().Set(FlyingKey, false);
		}

		/// <summary>
		/// How high she is above the ground or whatever solid is under her
		/// </summary>
		/// <returns>Her height above it, in meters, or the reach looked down when there's nothing</returns>
		private float Altitude()
		{
			Vector3 start = transform.position + Vector3.up * 0.5f;
			if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, AltitudeReach + 0.5f, BaseAI.m_solidRayMask))
			{
				return(transform.position.y - hit.point.y);
			}
			return(AltitudeReach);
		}
	}
}
