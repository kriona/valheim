using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace VikingVillages
{
	/// <summary>
	/// Poses a valkyrie's model on every client - flying, gliding, taking off, landing, standing and waddling - leans her
	/// as she climbs, dives and turns in the air, keeps her saddle on her back and her rider sitting upright
	/// </summary>
	/// <remarks>
	/// Her clips are mixed by a playable graph of their own on her model's animator, apart from the Asksvin animator
	/// the game drives (see ValkyriePrefab). She's posed by whether her ZDO says she flies and by how she moves, both of
	/// which every client sees, and the landing clip's time is set by hand to play the parts of it she uses (see
	/// ValkyrieMotion)
	/// </remarks>
	internal class ValkyrieModel : MonoBehaviour
	{
		/// <summary>
		/// Her clips, set as the prefab's built
		/// </summary>
		public AnimationClip m_fly;
		public AnimationClip m_glide;
		public AnimationClip m_idle;
		public AnimationClip m_landing;

		/// <summary>
		/// How the saddle sat turned on the Asksvin, against the Asksvin's own facing, and how big it was
		/// </summary>
		public Quaternion m_saddleTurn = Quaternion.identity;
		public float m_saddleSize = 100f;

		/// <summary>
		/// The mixer's inputs for each clip
		/// </summary>
		private const int FlyInput = 0;
		private const int GlideInput = 1;
		private const int IdleInput = 2;
		private const int LandingInput = 3;
		private const int InputCount = 4;

		/// <summary>
		/// How long it takes to blend from one clip to another, in seconds
		/// </summary>
		private const float BlendTime = 0.3f;

		/// <summary>
		/// How long it takes her to blend into and out of an attack's pose, in seconds
		/// </summary>
		private const float AttackBlendTime = 0.15f;

		/// <summary>
		/// How far she leans forward and back as she dives and climbs, and to the side as she turns, at most, in degrees
		/// </summary>
		private const float MaxPitch = 30f;
		private const float MaxBank = 25f;

		/// <summary>
		/// How far she leans to the side for each degree a second she turns
		/// </summary>
		private const float BankPerTurn = 0.3f;

		/// <summary>
		/// The slowest speed across the ground her pitch is worked out against, so climbing or coming down on the spot
		/// doesn't stand her on end, in meters a second
		/// </summary>
		private const float PitchFloorSpeed = 4f;

		/// <summary>
		/// How quickly what's measured of her moving and what she leans follow their changes - the share of the gap
		/// closed each second, roughly
		/// </summary>
		private const float Smoothing = 6f;

		private Character character;
		private ValkyrieFlight flight;
		private ValkyrieCatch catcher;
		private ValkyrieAttackPose attackPose;

		/// <summary>
		/// How wide her wings are held spread as she carries a rider she caught down, from 0 to 1, and how long they take
		/// to spread, in seconds - at once, as she catches them (see ValkyrieCatch)
		/// </summary>
		private float catchShare;
		private const float CatchSpread = 0.25f;

		/// <summary>
		/// How much of the attack's pose she takes, from 0 to 1
		/// </summary>
		private float attackShare;

		/// <summary>
		/// Her feet and toes, the lowest of which is stood on the ground, and how much she's stood on it, from 0 flying
		/// to 1 standing
		/// </summary>
		private Transform[] feet = new Transform[0];
		private float groundShare;

		/// <summary>
		/// Her wing bones, how each is turned with her wings spread wide, from her glide clip, and when her current and
		/// next stretch begin, by the game's time
		/// </summary>
		private Transform[] wings = new Transform[0];
		private Quaternion[] spreadWings = new Quaternion[0];
		private float stretchStart = -100f;
		private float nextStretch;
		private Animator animator;
		private Transform pelvis;
		private Transform leftLeg;
		private Transform rightLeg;
		private Transform leftKnee;
		private Transform rightKnee;
		private Transform leftArm;
		private Transform rightArm;
		private Transform leftForearm;
		private Transform rightForearm;
		private Transform saddle;
		private Transform seat;

		private PlayableGraph graph;
		private AnimationMixerPlayable mixer;
		private AnimationClipPlayable flyPlayable;
		private AnimationClipPlayable glidePlayable;
		private AnimationClipPlayable landingPlayable;
		private readonly float[] weights = new float[InputCount];
		private readonly float[] targets = new float[InputCount];

		/// <summary>
		/// Whether she flew when last posed, and when that last changed, by the game's time
		/// </summary>
		private bool wasFlying;
		private float changeTime = -100f;

		/// <summary>
		/// The landing clip's time shown, or below 0 when it isn't
		/// </summary>
		private float landingTime = -1f;

		/// <summary>
		/// What's measured of her moving - where she was, which way she faced, how fast she moves and turns
		/// </summary>
		private Vector3 lastPosition;
		private float lastYaw;
		private Vector3 velocity;
		private float turnRate;

		private float waddlePhase;
		private float waddleStrength;
		private float pitch;
		private float bank;

		/// <summary>
		/// Where her rider sits from the middle of her back's spine, in meters at size 1 as she flies level - x to her
		/// right, y up off her back and z toward her head - and her size the saddle was last placed by
		/// </summary>
		private static readonly Vector3 SeatOffset = new Vector3(0f, 0.6f, 0f);
		private float placedSize = -1f;

		/// <summary>
		/// How much her size has to change by before her saddle is placed again - her lean leaves tiny rounding changes
		/// in a size measured through it
		/// </summary>
		private const float SizeTolerance = 0.001f;

		/// <summary>
		/// The bone her saddle rides on, as she flies level, in her body's space
		/// </summary>
		private Matrix4x4 flyingBone = Matrix4x4.identity;

		/// <summary>
		/// How her model is turned to face the way she moves, as the prefab set it, which her leaning goes on top of
		/// </summary>
		private Quaternion facing = Quaternion.identity;

		/// <summary>
		/// Finds her parts and bones and sets up the graph that mixes her clips
		/// </summary>
		private void Awake()
		{
			character = GetComponentInParent<Character>();
			flight = GetComponentInParent<ValkyrieFlight>();
			catcher = GetComponentInParent<ValkyrieCatch>();
			attackPose = GetComponent<ValkyrieAttackPose>();
			animator = GetComponent<Animator>();
			facing = transform.localRotation;
			pelvis =Utils.FindChild(transform, "pelvis");
			leftLeg = Utils.FindChild(transform, "l_leg0");
			rightLeg = Utils.FindChild(transform, "r_leg0");
			leftKnee = Utils.FindChild(transform, "l_leg1");
			rightKnee = Utils.FindChild(transform, "r_leg1");
			leftArm = Utils.FindChild(transform, "l_arm0");
			rightArm = Utils.FindChild(transform, "r_arm0");
			leftForearm = Utils.FindChild(transform, "l_arm1");
			rightForearm = Utils.FindChild(transform, "r_arm1");
			wings = GetComponentsInChildren<Transform>(true).Where((Transform bone) => bone.name.StartsWith("l_shoulder") || bone.name.StartsWith("r_shoulder") || bone.name.StartsWith("l_arm") || bone.name.StartsWith("r_arm")).ToArray();
			feet = GetComponentsInChildren<Transform>(true).Where((Transform bone) => (bone.name.StartsWith("l_foot") || bone.name.StartsWith("r_foot") || bone.name.StartsWith("l_toe") || bone.name.StartsWith("r_toe")) && !bone.name.EndsWith("_end")).ToArray();
			Tameable tameable = GetComponentInParent<Tameable>();
			if (tameable != null && tameable.m_saddle != null)
			{
				saddle = tameable.m_saddle.transform;
				seat = tameable.m_saddle.m_attachPoint;
			}
			graph = PlayableGraph.Create(name);
			graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
			mixer = AnimationMixerPlayable.Create(graph, InputCount);
			flyPlayable = Connect(m_fly, FlyInput);
			glidePlayable = Connect(m_glide, GlideInput);
			Connect(m_idle, IdleInput);
			landingPlayable = Connect(m_landing, LandingInput);
			landingPlayable.SetSpeed(0.0);
			AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, name, animator);
			output.SetSourcePlayable(mixer);
			graph.Play();
		}

		/// <summary>
		/// Starts her flying or standing as her ZDO says, and places her saddle
		/// </summary>
		private void Start()
		{
			wasFlying = flight.IsFlying();
			lastPosition = character.transform.position;
			lastYaw = character.transform.eulerAngles.y;
			weights[(wasFlying ? FlyInput : IdleInput)] = 1f;
			groundShare = (wasFlying ? 0f : 1f);
			ApplyWeights();
			MeasureSaddleBone();
			MeasureSpreadWings();
			PlaceSaddle();
			nextStretch = Time.time + Random.Range(ValkyrieMotion.StretchGapMin / 4f, ValkyrieMotion.StretchGapMax);
		}

		/// <summary>
		/// Notes how her wing bones are turned with her wings spread wide, posing her gliding for a moment as she spawns,
		/// for her stretches (see Stretch)
		/// </summary>
		private void MeasureSpreadWings()
		{
			for (int i = 0; i < InputCount; i++)
			{
				mixer.SetInputWeight(i, ((i == GlideInput) ? 1f : 0f));
			}
			graph.Evaluate(0f);
			spreadWings = wings.Select((Transform bone) => bone.localRotation).ToArray();
			ApplyWeights();
			graph.Evaluate(0f);
		}

		/// <summary>
		/// Starts a stretch now and then while she stands about on the ground - not moving, landing or attacking - and
		/// spreads her wings by it, easing them toward how they're spread gliding, each turned against its own parent so
		/// they spread out from her body however she stands, then beats them slowly while they're spread (see FlapWing).
		/// Carrying a rider she caught down, she holds them spread wide the same way, spreading them in CatchSpread
		/// seconds and folding them over the time a stretch takes to close
		/// </summary>
		/// <param name="ground">Her speed across the ground, in meters a second</param>
		private void Stretch(float ground)
		{
			bool catching = catcher != null && catcher.Catching();
			catchShare = Mathf.MoveTowards(catchShare, (catching ? 1f : 0f), Time.deltaTime / (catching ? CatchSpread : ValkyrieMotion.StretchClose));
			float elapsed = Time.time - stretchStart;
			bool stretching = elapsed < ValkyrieMotion.StretchOpen + ValkyrieMotion.StretchHold + ValkyrieMotion.StretchClose;
			bool idle = groundShare >= 1f && ground < 0.2f && attackShare <= 0f;
			if (!stretching && idle && Time.time >= nextStretch)
			{
				stretchStart = Time.time;
				elapsed = 0f;
				nextStretch = Time.time + Random.Range(ValkyrieMotion.StretchGapMin, ValkyrieMotion.StretchGapMax);
			}
			float share = Mathf.Max(ValkyrieMotion.StretchShare(elapsed) * groundShare, catchShare);
			if (share <= 0f || spreadWings.Length != wings.Length)
			{
				return;
			}
			for (int i = 0; i < wings.Length; i++)
			{
				wings[i].localRotation = Quaternion.Slerp(wings[i].localRotation, spreadWings[i], share);
			}
			FlapWing(leftArm, leftForearm, share);
			FlapWing(rightArm, rightForearm, share);
		}

		/// <summary>
		/// Lifts and drops a spread wing in a slow, gentle beat, at the shoulder and a little behind further out, by how
		/// wide it's spread (see ValkyrieMotion.StretchFlap) - up being found from where the wing points, so both wings
		/// rise together
		/// </summary>
		/// <param name="arm">The wing's bone at the shoulder</param>
		/// <param name="forearm">The wing's bone further out</param>
		/// <param name="share">How wide her wings are spread, from 0 to 1</param>
		private void FlapWing(Transform arm, Transform forearm, float share)
		{
			if (arm == null || forearm == null)
			{
				return;
			}
			Vector3 outward = forearm.position - arm.position;
			Vector3 axis = Vector3.Cross(outward, Vector3.up);
			if (axis.sqrMagnitude < 0.000001f)
			{
				return;
			}
			axis.Normalize();
			// Turning around this axis lifts the wing one way or the other, depending on its side
			if ((Quaternion.AngleAxis(1f, axis) * outward).y < outward.y)
			{
				axis = -axis;
			}
			arm.Rotate(axis, ValkyrieMotion.StretchFlap(Time.time, share, false), Space.World);
			forearm.Rotate(axis, ValkyrieMotion.StretchFlap(Time.time, share, true), Space.World);
		}

		/// <summary>
		/// Frees her graph
		/// </summary>
		private void OnDestroy()
		{
			if (graph.IsValid())
			{
				graph.Destroy();
			}
		}

		/// <summary>
		/// Measures how she moves, picks her clips and blends to them, and works out her waddle and lean
		/// </summary>
		private void Update()
		{
			float dt = Time.deltaTime;
			if (dt <= 0f)
			{
				return;
			}
			Measure(dt);
			bool flying = flight.IsFlying();
			if (flying != wasFlying)
			{
				wasFlying = flying;
				changeTime = Time.time;
			}
			float sinceChange = Time.time - changeTime;
			PickClips(flying, sinceChange);
			float step = dt / ((flying && sinceChange < ValkyrieMotion.TakeoffDuration) ? ValkyrieMotion.TakeoffDuration : BlendTime);
			for (int i = 0; i < InputCount; i++)
			{
				weights[i] = Mathf.MoveTowards(weights[i], targets[i], step);
			}
			ApplyWeights();
			float ground = new Vector2(velocity.x, velocity.z).magnitude;
			float size = transform.lossyScale.x;
			float blend = 1f - Mathf.Exp(-Smoothing * dt);
			waddleStrength = Mathf.Lerp(waddleStrength, (flying ? 0f : ValkyrieMotion.WaddleStrength(ground) * weights[IdleInput]), blend);
			waddlePhase = Mathf.Repeat(waddlePhase + ValkyrieMotion.WaddleStep(ground, size, dt), Mathf.PI * 2f);
			float pitchTarget = 0f;
			float bankTarget = 0f;
			if (flying)
			{
				pitchTarget = -Mathf.Clamp(Mathf.Atan2(velocity.y, Mathf.Max(ground, PitchFloorSpeed)) * Mathf.Rad2Deg, -MaxPitch, MaxPitch);
				bankTarget = Mathf.Clamp(-turnRate * BankPerTurn, -MaxBank, MaxBank);
			}
			pitch = Mathf.Lerp(pitch, pitchTarget, blend);
			bank = Mathf.Lerp(bank, bankTarget, blend);
			transform.localRotation = Quaternion.Euler(pitch, 0f, bank) * facing;
		}

		/// <summary>
		/// Places her saddle again when her size changes (see PlaceSaddle), then holds her pelvis while the landing clip shows, stretches her wings now and then as she stands about (see Stretch), rocks her body and swings her legs as she waddles, blends her
		/// into an attack's pose while the game has her attack (see ValkyrieAttackPose) and keeps her rider sitting
		/// upright, after her clips have posed her
		/// </summary>
		private void LateUpdate()
		{
			if (transform.parent != null && Mathf.Abs(transform.parent.lossyScale.x - placedSize) > SizeTolerance)
			{
				PlaceSaddle();
			}
			float landingShare = weights[LandingInput];
			if (pelvis != null && landingShare > 0f && landingTime >= 0f && ValkyrieMotion.HeldPelvis(landingTime, out Vector3 held))
			{
				pelvis.localPosition = Vector3.Lerp(pelvis.localPosition, held, landingShare);
			}
			Stretch(new Vector2(velocity.x, velocity.z).magnitude);
			if (waddleStrength > 0.001f)
			{
				Waddle();
			}
			if (attackPose != null && attackShare <= 0f)
			{
				attackPose.Calibrate(character.transform.forward);
			}
			bool attacking = character is Humanoid humanoid && humanoid.InAttack();
			attackShare = Mathf.MoveTowards(attackShare, (attacking ? 1f : 0f), Time.deltaTime / AttackBlendTime);
			if (attackPose != null && attackShare > 0f)
			{
				attackPose.Apply(attackShare);
			}
			StandOnGround();
			if (seat != null)
			{
				Vector3 forward = character.transform.forward;
				forward.y = 0f;
				if (forward.sqrMagnitude > 0.0001f)
				{
					seat.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
				}
			}
		}

		/// <summary>
		/// Moves her model up or down so her lowest foot or toe stands on the ground, as the game puts her there, while
		/// she's on the ground - from touching down on as she lands - easing in and out as she lands and takes off. Her
		/// clips pose her feet wherever they were made to, so this keeps her standing through all of them, her waddle and
		/// her attacks
		/// </summary>
		private void StandOnGround()
		{
			bool standing = !wasFlying && (landingTime < 0f || landingTime >= ValkyrieMotion.TouchdownTime);
			groundShare = Mathf.MoveTowards(groundShare, (standing ? 1f : 0f), Time.deltaTime / BlendTime);
			Transform body = transform.parent;
			if (feet.Length == 0 || body == null)
			{
				return;
			}
			float lowest = float.MaxValue;
			foreach (Transform foot in feet)
			{
				lowest = Mathf.Min(lowest, foot.position.y);
			}
			float moved = body.TransformVector(transform.localPosition).y;
			float shift = ValkyrieMotion.GroundShift(character.transform.position.y, lowest - moved, groundShare);
			transform.localPosition = body.InverseTransformVector(new Vector3(0f, shift, 0f));
		}

		/// <summary>
		/// Measures her velocity and how fast she turns from how she moved since the last frame, which works the same on
		/// every client
		/// </summary>
		/// <param name="dt">Seconds since the last frame</param>
		private void Measure(float dt)
		{
			Vector3 position = character.transform.position;
			float yaw = character.transform.eulerAngles.y;
			float blend = 1f - Mathf.Exp(-Smoothing * dt);
			velocity = Vector3.Lerp(velocity, (position - lastPosition) / dt, blend);
			turnRate = Mathf.Lerp(turnRate, Mathf.DeltaAngle(lastYaw, yaw) / dt, blend);
			lastPosition = position;
			lastYaw = yaw;
		}

		/// <summary>
		/// Picks the clips she blends to - beating her wings as she takes off, blended in over the whole takeoff (see
		/// Update), the landing clip as she lands, then flapping and gliding in the air or standing on the ground - and
		/// sets the landing clip's time
		/// </summary>
		/// <param name="flying">Whether she flies</param>
		/// <param name="sinceChange">Seconds since she took off or landed</param>
		private void PickClips(bool flying, float sinceChange)
		{
			for (int i = 0; i < InputCount; i++)
			{
				targets[i] = 0f;
			}
			float length = m_landing.length;
			landingTime = -1f;
			if (!flying && sinceChange < length - ValkyrieMotion.FlareTime)
			{
				landingTime = ValkyrieMotion.LandingTime(sinceChange, length);
			}
			if (landingTime >= 0f)
			{
				targets[LandingInput] = 1f;
				landingPlayable.SetTime(landingTime);
			}
			else if (flying && sinceChange < ValkyrieMotion.TakeoffDuration)
			{
				targets[FlyInput] = 1f;
			}
			else if (flying)
			{
				float glide = ValkyrieMotion.GlideShare(new Vector2(velocity.x, velocity.z).magnitude, velocity.y, character.m_flySlowSpeed);
				targets[GlideInput] = glide;
				targets[FlyInput] = 1f - glide;
			}
			else
			{
				targets[IdleInput] = 1f;
			}
		}

		/// <summary>
		/// Rocks her body to the side of the foot she stands on, swings her legs opposite ways and lifts the foot coming
		/// forward, on top of her standing clip
		/// </summary>
		private void Waddle()
		{
			Vector3 forward = character.transform.forward;
			Vector3 right = character.transform.right;
			if (pelvis != null)
			{
				pelvis.Rotate(forward, ValkyrieMotion.Roll(waddlePhase, waddleStrength), Space.World);
			}
			SwingLeg(leftLeg, leftKnee, right, true);
			SwingLeg(rightLeg, rightKnee, right, false);
		}

		/// <summary>
		/// Swings one leg forward or back at the hip and bends its knee, by her waddle
		/// </summary>
		/// <param name="leg">The leg's top bone</param>
		/// <param name="knee">The bone below it</param>
		/// <param name="right">Her right, which the leg swings around</param>
		/// <param name="left">Whether it's the left leg</param>
		private void SwingLeg(Transform leg, Transform knee, Vector3 right, bool left)
		{
			if (leg != null)
			{
				leg.Rotate(right, -ValkyrieMotion.Swing(waddlePhase, waddleStrength, left), Space.World);
			}
			if (knee != null)
			{
				knee.Rotate(right, ValkyrieMotion.Knee(waddlePhase, waddleStrength, left), Space.World);
			}
		}

		/// <summary>
		/// Notes where the bone her saddle rides on is as she flies level, against her body, posing her that way for a
		/// moment as she spawns, before her clips have played - her saddle is placed from it after (see PlaceSaddle)
		/// </summary>
		private void MeasureSaddleBone()
		{
			if (saddle == null || saddle.parent == null || transform.parent == null)
			{
				return;
			}
			transform.localRotation = facing;
			for (int i = 0; i < InputCount; i++)
			{
				mixer.SetInputWeight(i, ((i == FlyInput) ? 1f : 0f));
			}
			graph.Evaluate(0f);
			flyingBone = transform.parent.worldToLocalMatrix * saddle.parent.localToWorldMatrix;
			ApplyWeights();
			graph.Evaluate(0f);
		}

		/// <summary>
		/// Places her saddle - the unseen seat her rider sits on (see ValkyrieSeat) - on her back at SeatOffset, as it
		/// sits on the bone it rides when she flies level (see MeasureSaddleBone), so it sits on her the way the Asksvin's
		/// sits on it standing, at the Asksvin's size whatever hers - it rides her back's bone after, through every pose
		/// </summary>
		private void PlaceSaddle()
		{
			Transform body = transform.parent;
			placedSize = ((body != null) ? body.lossyScale.x : 1f);
			if (saddle == null || saddle.parent == null || body == null)
			{
				return;
			}
			Vector3 position = (Vector3)flyingBone.GetColumn(3) + SeatOffset;
			float scale = m_saddleSize / Mathf.Max(0.0001f, placedSize);
			Matrix4x4 onBone = flyingBone.inverse * Matrix4x4.TRS(position, m_saddleTurn, Vector3.one * scale);
			saddle.localPosition = onBone.GetColumn(3);
			saddle.localRotation = onBone.rotation;
			saddle.localScale = onBone.lossyScale;
		}

		/// <summary>
		/// Adds a clip to the mixer at one of its inputs
		/// </summary>
		/// <param name="clip">The clip</param>
		/// <param name="input">The mixer's input</param>
		/// <returns>The clip's playable</returns>
		private AnimationClipPlayable Connect(AnimationClip clip, int input)
		{
			AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
			graph.Connect(playable, 0, mixer, input);
			return(playable);
		}

		/// <summary>
		/// Gives the mixer the clips' weights, scaled to add up to 1, and pauses the fly and glide clips while they have
		/// none, so the fly clip's wing beat isn't heard while she stands
		/// </summary>
		private void ApplyWeights()
		{
			float total = 0f;
			for (int i = 0; i < InputCount; i++)
			{
				total += weights[i];
			}
			for (int i = 0; i < InputCount; i++)
			{
				mixer.SetInputWeight(i, ((total > 0f) ? weights[i] / total : ((i == IdleInput) ? 1f : 0f)));
			}
			flyPlayable.SetSpeed(((weights[FlyInput] > 0f) ? 1.0 : 0.0));
			glidePlayable.SetSpeed(((weights[GlideInput] > 0f) ? 1.0 : 0.0));
		}
	}
}
