using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The numbers behind how a valkyrie moves and is posed - which part of the end credits' landing clip her landing
	/// plays, where her pelvis is held while it does, how long her takeoff lasts, when she flaps or glides, when a rider
	/// brings her down, and her waddle on the ground
	/// </summary>
	/// <remarks>
	/// The game has no takeoff or walk for her. The landing clip is the end credits' whole swoop down to the stone
	/// circle - a dive from 100 meters up until 1.6 seconds in, her body swinging upright into a flare until 1.92, a
	/// hover with her wings beating until 2.56, touching down at 2.72 and settling until it ends - so her landing plays
	/// it from the flare on. Her takeoff blends from standing into the fly clip, tipping her forward as she beats her
	/// wings, since the landing clip backwards throws her back through the flare first. Her waddle is her standing idle
	/// with her body rocked and her legs swung on top
	/// </remarks>
	internal static class ValkyrieMotion
	{
		/// <summary>
		/// The landing clip's time where her body is level, the way it is when flying
		/// </summary>
		public const float LevelTime = 1.6f;

		/// <summary>
		/// The landing clip's time where her landing starts, with her body leaning back into the flare
		/// </summary>
		public const float FlareTime = 2.4f;

		/// <summary>
		/// The landing clip's time where her feet touch the ground
		/// </summary>
		public const float TouchdownTime = 2.72f;

		/// <summary>
		/// How long her takeoff lasts, in seconds - she climbs on her own while she blends from standing into flying
		/// </summary>
		public const float TakeoffDuration = 1f;

		/// <summary>
		/// Her pelvis's place under her armature as she flies, from the fly clip
		/// </summary>
		public static readonly Vector3 FlyingPelvis = new Vector3(0f, -0.002f, 0.002f);

		/// <summary>
		/// Her pelvis's place under her armature as she stands, from the idle clip
		/// </summary>
		public static readonly Vector3 StandingPelvis = new Vector3(0f, -0.006f, -0.013f);

		/// <summary>
		/// How high above the ground she is low enough for a rider holding crouch to bring her down, in meters
		/// </summary>
		public const float LandAltitude = 2f;

		/// <summary>
		/// How far she waddles in a full waddle, a step with each foot, in meters at size 1
		/// </summary>
		public const float WaddleLength = 1.6f;

		/// <summary>
		/// The ground speed at and above which she waddles fully, in meters a second
		/// </summary>
		public const float FullWaddleSpeed = 0.6f;

		/// <summary>
		/// How far her body rocks to each side as she waddles, in degrees
		/// </summary>
		public const float WaddleRoll = 7f;

		/// <summary>
		/// How far each leg swings forward and back as she waddles, in degrees
		/// </summary>
		public const float WaddleSwing = 18f;

		/// <summary>
		/// How far each knee bends as its leg swings forward, in degrees
		/// </summary>
		public const float WaddleKnee = 22f;

		/// <summary>
		/// How far off her target's edge has to be for her to fly to it rather than walk, in meters - a few steps past
		/// where her claws reach on the ground, since she only waddles
		/// </summary>
		public const float FarTarget = 7f;

		/// <summary>
		/// How often she thinks about taking to the air now and then in a fight on the ground, in seconds, the chance
		/// she does each time, and the least and most time she stays up when she does, in seconds
		/// </summary>
		public const float SortieCheck = 10f;
		public const float SortieChance = 0.15f;
		public const float SortieMin = 10f;
		public const float SortieMax = 15f;

		/// <summary>
		/// The least time she stays up once her AI takes off, and how long she keeps flying with no target before she
		/// lands, in seconds
		/// </summary>
		public const float MinAirTime = 4f;
		public const float LandDelay = 3f;

		/// <summary>
		/// How often she checks whether she can walk to her target, in seconds
		/// </summary>
		public const float ReachCheck = 2f;

		/// <summary>
		/// Checks whether her AI fights a target from the air - one that flies, one she can't walk to, one far off, one
		/// she fights hit and run since it can't strike at range (see ValkyrieSkirmish), or any while she's taken to the
		/// air now and then
		/// </summary>
		/// <param name="targetFlies">Whether her target flies</param>
		/// <param name="unreachable">Whether she can't walk to it</param>
		/// <param name="distance">How far off its edge is, in meters</param>
		/// <param name="sortie">Whether she's taken to the air now and then</param>
		/// <param name="hitAndRun">Whether she fights it hit and run</param>
		/// <returns>True when she fights it from the air</returns>
		public static bool FightsInTheAir(bool targetFlies, bool unreachable, float distance, bool sortie, bool hitAndRun)
		{
			return(targetFlies || unreachable || distance > FarTarget || sortie || hitAndRun);
		}

		/// <summary>
		/// Checks whether her AI brings her down from the air - once she's been up long enough, either to fight a target
		/// on the ground or after a while with no target
		/// </summary>
		/// <param name="hasTarget">Whether she has a target</param>
		/// <param name="fightsInTheAir">Whether she fights it from the air (see FightsInTheAir)</param>
		/// <param name="airTime">How long since she took off, in seconds</param>
		/// <param name="sinceTarget">How long since she last had a target, in seconds</param>
		/// <returns>True when she comes down</returns>
		public static bool AILands(bool hasTarget, bool fightsInTheAir, float airTime, float sinceTarget)
		{
			if (airTime < MinAirTime)
			{
				return(false);
			}
			if (hasTarget)
			{
				return(!fightsInTheAir);
			}
			return(sinceTarget > LandDelay);
		}

		/// <summary>
		/// How long she takes to spread her wings in a stretch, holds them wide and folds them again, in seconds
		/// </summary>
		public const float StretchOpen = 1f;
		public const float StretchHold = 1.5f;
		public const float StretchClose = 1.2f;

		/// <summary>
		/// The least and most time between her stretches while she stands about, in seconds
		/// </summary>
		public const float StretchGapMin = 20f;
		public const float StretchGapMax = 45f;

		/// <summary>
		/// How many slow beats a second her stretched wings make, how far each wing lifts and drops at the shoulder at
		/// most, in degrees, and how far behind and how much less the outer wing moves
		/// </summary>
		public const float StretchBeat = 0.8f;
		public const float StretchLift = 9f;
		public const float StretchLag = 0.7f;
		public const float StretchOuter = 0.6f;

		/// <summary>
		/// How far a stretched wing is lifted at a moment, by how wide it's spread - a slow, gentle beat, the outer wing
		/// following a little behind
		/// </summary>
		/// <param name="time">The game's time, in seconds</param>
		/// <param name="share">How wide her wings are spread, from 0 to 1</param>
		/// <param name="outer">Whether it's the outer wing rather than the wing at the shoulder</param>
		/// <returns>The lift, in degrees, below 0 dropped</returns>
		public static float StretchFlap(float time, float share, bool outer)
		{
			float phase = time * StretchBeat * Mathf.PI * 2f - (outer ? StretchLag : 0f);
			return(Mathf.Sin(phase) * StretchLift * (outer ? StretchOuter : 1f) * Mathf.Clamp01(share));
		}

		/// <summary>
		/// How far her wings are spread a while into a stretch - easing open, held wide, then easing closed
		/// </summary>
		/// <param name="elapsed">Seconds since the stretch began</param>
		/// <returns>How far they're spread, from 0 folded to 1 wide</returns>
		public static float StretchShare(float elapsed)
		{
			if (elapsed <= 0f || elapsed >= StretchOpen + StretchHold + StretchClose)
			{
				return(0f);
			}
			if (elapsed < StretchOpen)
			{
				return(Mathf.SmoothStep(0f, 1f, elapsed / StretchOpen));
			}
			if (elapsed < StretchOpen + StretchHold)
			{
				return(1f);
			}
			return(Mathf.SmoothStep(1f, 0f, (elapsed - StretchOpen - StretchHold) / StretchClose));
		}

		/// <summary>
		/// Rounds a turn to the nearest quarter turn
		/// </summary>
		/// <param name="degrees">The turn, in degrees</param>
		/// <returns>The nearest of -180, -90, 0, 90 and 180, or further whole quarters past them</returns>
		public static float NearestQuarter(float degrees)
		{
			return(Mathf.Round(degrees / 90f) * 90f);
		}

		/// <summary>
		/// How far up her model is moved to stand her lowest foot on the ground, by how much she stands rather than flies
		/// </summary>
		/// <param name="ground">The height of the ground she stands on, where the game puts her</param>
		/// <param name="lowestFoot">The height of her lowest foot or toe as her clips pose her, before she's moved</param>
		/// <param name="share">How much she stands, from 0 flying to 1 standing</param>
		/// <returns>How far up her model is moved, in meters, below 0 down</returns>
		public static float GroundShift(float ground, float lowestFoot, float share)
		{
			return((ground - lowestFoot) * Mathf.Clamp01(share));
		}

		/// <summary>
		/// The landing clip's time her landing shows, a while after it began
		/// </summary>
		/// <param name="elapsed">Seconds since she landed</param>
		/// <param name="clipLength">The landing clip's length, in seconds</param>
		/// <returns>The clip's time, from the flare to the clip's end</returns>
		public static float LandingTime(float elapsed, float clipLength)
		{
			return(Mathf.Min(clipLength, FlareTime + Mathf.Max(0f, elapsed)));
		}

		/// <summary>
		/// Where her pelvis is held under her armature at a time in the landing clip - the clip's own place moves her
		/// tens of meters, so until touching down it's held between where it is flying and standing instead, and the
		/// game's physics moves her up and down
		/// </summary>
		/// <param name="clipTime">The landing clip's time</param>
		/// <param name="held">Where her pelvis is held</param>
		/// <returns>True when it's held, false from touching down on, where the clip's own place is used</returns>
		public static bool HeldPelvis(float clipTime, out Vector3 held)
		{
			if (clipTime >= TouchdownTime)
			{
				held = StandingPelvis;
				return(false);
			}
			held = Vector3.Lerp(FlyingPelvis, StandingPelvis, Mathf.InverseLerp(LevelTime, TouchdownTime, clipTime));
			return(true);
		}

		/// <summary>
		/// How much of her flying shows the glide clip rather than the fly clip - she beats her wings to climb, hover
		/// or fly slowly, and glides as she comes down or flies fast
		/// </summary>
		/// <param name="horizontalSpeed">Her speed across the ground, in meters a second</param>
		/// <param name="verticalSpeed">Her speed up, below 0 going down, in meters a second</param>
		/// <param name="slowSpeed">Her slow flying speed, in meters a second</param>
		/// <returns>The glide clip's share, from 0 to 1</returns>
		public static float GlideShare(float horizontalSpeed, float verticalSpeed, float slowSpeed)
		{
			if (verticalSpeed > 0.5f)
			{
				return(0f);
			}
			if (verticalSpeed < -1f)
			{
				return(1f);
			}
			return(Mathf.Clamp01((horizontalSpeed - slowSpeed) / Mathf.Max(0.01f, slowSpeed)));
		}

		/// <summary>
		/// Which way a rider's held keys take her up or down - climbing wins when both are held
		/// </summary>
		/// <param name="climb">Whether jump is held</param>
		/// <param name="descend">Whether crouch is held</param>
		/// <returns>1 up, -1 down or 0</returns>
		public static float Vertical(bool climb, bool descend)
		{
			if (climb)
			{
				return(1f);
			}
			return((descend ? -1f : 0f));
		}

		/// <summary>
		/// The speeds a rider asks of a saddle, as the game numbers them - stop, walk, run, turn on the spot, and no change
		/// </summary>
		public const int RideStop = 0;
		public const int RideWalk = 1;
		public const int RideRun = 2;
		public const int RideTurn = 3;
		public const int RideNoChange = 4;

		/// <summary>
		/// How far a rider's look tips up or down, as the up part of the way they look, before holding block takes her up
		/// or down, and how far it tips for her to climb or come down at full speed
		/// </summary>
		public const float PitchDeadZone = 0.1f;
		public const float PitchFull = 0.5f;

		/// <summary>
		/// Which speed a rider asks of her - the game's riding, but for steering to the side, which walks her too, and on
		/// the ground stopping her as soon as nothing's held, where the game keeps her walking after a tap of forward. In
		/// the air she keeps going the way the game has her
		/// </summary>
		/// <param name="forward">The rider's forward input, -1 back to 1 forward</param>
		/// <param name="side">The rider's side input, -1 left to 1 right</param>
		/// <param name="run">Whether run is held</param>
		/// <param name="block">Whether block is held</param>
		/// <param name="grounded">Whether she's on the ground</param>
		/// <returns>The speed, one of the Ride constants</returns>
		public static int RideSpeed(float forward, float side, bool run, bool block, bool grounded)
		{
			if (run)
			{
				return(RideRun);
			}
			if (forward > 0.5f || Mathf.Abs(side) > 0.5f)
			{
				return(RideWalk);
			}
			if (forward < -0.5f)
			{
				return(RideStop);
			}
			if (block)
			{
				return(RideTurn);
			}
			return((grounded ? RideStop : RideNoChange));
		}

		/// <summary>
		/// Which way a rider has her go across the ground - the way they look, turned toward the side they steer to, a
		/// quarter turn for the side alone and an eighth with forward - or none when nothing that moves or turns her is
		/// held, so she keeps her way
		/// </summary>
		/// <param name="look">The way the rider looks</param>
		/// <param name="forward">The rider's forward input, -1 back to 1 forward</param>
		/// <param name="side">The rider's side input, -1 left to 1 right</param>
		/// <param name="run">Whether run is held</param>
		/// <param name="block">Whether block is held</param>
		/// <returns>The way, level and of length 1, or zero</returns>
		public static Vector3 RideDirection(Vector3 look, float forward, float side, bool run, bool block)
		{
			bool steering = Mathf.Abs(side) > 0.5f;
			Vector3 flat = new Vector3(look.x, 0f, look.z);
			if ((!steering && forward <= 0.5f && !run && !block) || flat.sqrMagnitude < 0.0001f)
			{
				return(Vector3.zero);
			}
			flat.Normalize();
			if (!steering)
			{
				return(flat);
			}
			float angle = Mathf.Sign(side) * ((forward > 0.5f) ? 45f : 90f) * Mathf.Deg2Rad;
			float cos = Mathf.Cos(angle);
			float sin = Mathf.Sin(angle);
			// Turned clockwise seen from above for a positive angle, as Unity turns about up
			return(new Vector3(flat.x * cos + flat.z * sin, 0f, flat.z * cos - flat.x * sin));
		}

		/// <summary>
		/// Which way holding block takes her up or down as she flies, by how far her rider looks up or down
		/// </summary>
		/// <param name="lookUp">The up part of the way the rider looks, -1 straight down to 1 straight up</param>
		/// <returns>From -1 down to 1 up</returns>
		public static float PitchVertical(float lookUp)
		{
			float tilt = Mathf.Abs(lookUp);
			if (tilt <= PitchDeadZone)
			{
				return(0f);
			}
			return(Mathf.Sign(lookUp) * Mathf.Clamp01((tilt - PitchDeadZone) / (PitchFull - PitchDeadZone)));
		}

		/// <summary>
		/// Checks whether she lands - going down with her feet on the ground, or low enough over it
		/// </summary>
		/// <param name="vertical">Which way she's going, from Vertical</param>
		/// <param name="altitude">How high she is above the ground, in meters</param>
		/// <param name="onGround">Whether she's touching the ground</param>
		/// <returns>True when she lands</returns>
		public static bool Lands(float vertical, float altitude, bool onGround)
		{
			return(vertical < 0f && (onGround || altitude < LandAltitude));
		}

		/// <summary>
		/// When she's flown by code (see ValkyrieFlight.Pilot) - how long after the last call she's still flown by it, in
		/// seconds, the height above the ground she cruises at, how near over her destination counts as there, how far
		/// off it she flies fast, in meters, and over how many meters off her cruising height she climbs or comes down at
		/// full speed
		/// </summary>
		public const float PilotLife = 0.5f;
		public const float CruiseHeight = 15f;
		public const float ArriveRadius = 3f;
		public const float FastDistance = 30f;
		public const float ClimbBlend = 4f;

		/// <summary>
		/// Which way she goes up or down to keep to her cruising height, flown by code - full speed when far off it,
		/// easing as she nears it
		/// </summary>
		/// <param name="altitude">How high she is above the ground, in meters</param>
		/// <returns>From -1 down to 1 up</returns>
		public static float CruiseVertical(float altitude)
		{
			return(Mathf.Clamp((CruiseHeight - altitude) / ClimbBlend, -1f, 1f));
		}

		/// <summary>
		/// Which way she flies, flown by code toward a destination - straight at it at her cruising height, or once she's
		/// over it, down onto it to land or hovering there. What's in her way she climbs over however she's flown (see
		/// Avoid)
		/// </summary>
		/// <param name="across">How far off her destination is across the ground, x and z, in meters</param>
		/// <param name="altitude">How high she is above the ground, in meters</param>
		/// <param name="land">Whether she lands at it</param>
		/// <returns>Her move direction, no longer than 1</returns>
		public static Vector3 PilotDirection(Vector2 across, float altitude, bool land)
		{
			float distance = across.magnitude;
			if (distance < ArriveRadius)
			{
				// Drifting onto it as she hovers or comes down
				Vector2 drift = ((distance > 0.01f) ? across / ArriveRadius : Vector2.zero);
				return(Vector3.ClampMagnitude(new Vector3(drift.x, (land ? -1f : CruiseVertical(altitude)), drift.y), 1f));
			}
			Vector2 way = across / distance;
			return(Vector3.ClampMagnitude(new Vector3(way.x, CruiseVertical(altitude), way.y), 1f));
		}

		/// <summary>
		/// How high above water her feet keep as she flies, in meters - swimming wins over flying in the game, so dipping
		/// in would have her swim, held at the surface, rather than fly
		/// </summary>
		public const float WaterClearance = 1.5f;

		/// <summary>
		/// Which way she goes up or down over water, however she's flown - she comes down no nearer it than
		/// WaterClearance, and nearer than that she's pushed up, the harder the nearer, at full speed from the surface down
		/// </summary>
		/// <param name="vertical">Which way she'd go, -1 down to 1 up</param>
		/// <param name="clearance">How far her feet are above the water, in meters, below 0 in it</param>
		/// <returns>Which way she goes</returns>
		public static float AboveWater(float vertical, float clearance)
		{
			if (clearance >= WaterClearance)
			{
				return(vertical);
			}
			return(Mathf.Max(vertical, Mathf.Clamp01((WaterClearance - clearance) / WaterClearance)));
		}

		/// <summary>
		/// How far ahead she looks for something in her way as she flies, in meters - as far as she flies in LookTime
		/// seconds and LookMargin more, from LookMin to LookMax - how near it has to be for her to stop pressing on
		/// toward it and only climb, and how much of her way across she keeps while she climbs over it
		/// </summary>
		public const float LookTime = 0.8f;
		public const float LookMargin = 2f;
		public const float LookMin = 3f;
		public const float LookMax = 12f;
		public const float StopDistance = 1.5f;
		public const float ClimbAcross = 0.3f;

		/// <summary>
		/// How far ahead she looks for something in her way, by how fast she's flying across
		/// </summary>
		/// <param name="speed">Her speed across the ground she's flying at, in meters a second</param>
		/// <returns>The distance, in meters</returns>
		public static float LookAhead(float speed)
		{
			return(Mathf.Clamp(speed * LookTime + LookMargin, LookMin, LookMax));
		}

		/// <summary>
		/// Turns the way she flies up over something in her way ahead - however she's flown, by a rider, her AI or code -
		/// climbing at full speed and keeping a little of her way across, or none when it's right in front of her. She
		/// doesn't when she's coming down, so she can still be brought down onto a slope, or isn't going anywhere across
		/// </summary>
		/// <param name="moveDir">The way she'd fly</param>
		/// <param name="hitDistance">How far ahead the thing in her way is, in meters, or below 0 when there's none</param>
		/// <returns>The way she flies</returns>
		public static Vector3 Avoid(Vector3 moveDir, float hitDistance)
		{
			Vector3 across = new Vector3(moveDir.x, 0f, moveDir.z);
			if (hitDistance < 0f || moveDir.y < 0f || across.sqrMagnitude < 0.01f)
			{
				return(moveDir);
			}
			float keep = ((hitDistance < StopDistance) ? 0f : ClimbAcross);
			Vector3 climb = across * keep + Vector3.up;
			if (keep <= 0f)
			{
				// A sliver ahead keeps her facing her way as she rises
				climb += across.normalized * 0.001f;
			}
			return(Vector3.ClampMagnitude(climb, 1f));
		}

		/// <summary>
		/// How far her waddle's cycle moves on, as a full turn of 2 pi for each full waddle
		/// </summary>
		/// <param name="speed">Her speed across the ground, in meters a second</param>
		/// <param name="size">Her size, 1 being the game's own valkyrie's</param>
		/// <param name="dt">Seconds since the last frame</param>
		/// <returns>How far the cycle moves on, in radians</returns>
		public static float WaddleStep(float speed, float size, float dt)
		{
			return(speed * dt / (WaddleLength * Mathf.Max(0.01f, size)) * Mathf.PI * 2f);
		}

		/// <summary>
		/// How much she waddles at a ground speed, from standing still to a full waddle
		/// </summary>
		/// <param name="speed">Her speed across the ground, in meters a second</param>
		/// <returns>The waddle's strength, from 0 to 1</returns>
		public static float WaddleStrength(float speed)
		{
			return(Mathf.Clamp01(speed / FullWaddleSpeed));
		}

		/// <summary>
		/// How far her body rocks at a point in her waddle - to the side of the foot she stands on
		/// </summary>
		/// <param name="phase">The waddle's cycle, in radians</param>
		/// <param name="strength">The waddle's strength, from 0 to 1</param>
		/// <returns>The roll, in degrees</returns>
		public static float Roll(float phase, float strength)
		{
			return(Mathf.Sin(phase) * WaddleRoll * strength);
		}

		/// <summary>
		/// How far a leg swings forward at a point in her waddle - the legs swing opposite ways
		/// </summary>
		/// <param name="phase">The waddle's cycle, in radians</param>
		/// <param name="strength">The waddle's strength, from 0 to 1</param>
		/// <param name="left">Whether it's the left leg</param>
		/// <returns>The swing forward, in degrees, below 0 back</returns>
		public static float Swing(float phase, float strength, bool left)
		{
			return(Mathf.Sin(phase) * WaddleSwing * strength * (left ? 1f : -1f));
		}

		/// <summary>
		/// How far a knee bends at a point in her waddle - only while its leg comes forward, lifting the foot
		/// </summary>
		/// <param name="phase">The waddle's cycle, in radians</param>
		/// <param name="strength">The waddle's strength, from 0 to 1</param>
		/// <param name="left">Whether it's the left leg</param>
		/// <returns>The bend, in degrees, never below 0</returns>
		public static float Knee(float phase, float strength, bool left)
		{
			return(Mathf.Max(0f, Mathf.Cos(phase) * (left ? 1f : -1f)) * WaddleKnee * strength);
		}
	}
}
