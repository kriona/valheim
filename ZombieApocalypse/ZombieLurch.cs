using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Makes a zombie lurch as it walks - one on two legs hunched over with its head lolling to one side, leaning onto
	/// a stiff leg it drags and letting the arm on that side hang, and one on four legs limping on a stiff front leg
	/// with its head carried low and bobbing
	/// </summary>
	/// <remarks>
	/// The creature's own animations play as they are, and the lurch bends its bones on top of them once the animator
	/// has run, so it works on any rig with feet and hands - a four-legged creature's front feet carry the hands'
	/// names (e.g., a wolf's L Hand). The bones are found by their names (e.g., LeftFoot, l_foot or l.foot) and the
	/// shape of the skeleton, and whether the zombie walks on two legs or four is read from how it holds itself as it
	/// moves after getting up. The steps are read from the feet, so the lean lands as the stiff leg takes the
	/// creature's weight. Each zombie's stiff side, head tilt and lurch score come from its ZDOID, so every client sees
	/// the same lurch. The lean and hanging arm give way to attacks, blocks and staggers. The bones are put back at
	/// the end of each frame, so a frame the animator skips (e.g., while the zombie is out of sight) doesn't bend
	/// them twice
	/// </remarks>
	[DefaultExecutionOrder(10000)]
	internal class ZombieLurch : MonoBehaviour
	{
		/// <summary>
		/// Degrees the spine bends forward
		/// </summary>
		private const float HunchAngle = 14f;

		/// <summary>
		/// Degrees the spine leans toward the stiff leg while it carries the zombie's weight
		/// </summary>
		private const float LeanAngle = 9f;

		/// <summary>
		/// How far the spine leans the other way while the stiff leg swings, as a fraction of LeanAngle
		/// </summary>
		private const float BackLean = 0.35f;

		/// <summary>
		/// Degrees the spine dips further forward while the stiff leg carries the zombie's weight
		/// </summary>
		private const float DipAngle = 6f;

		/// <summary>
		/// Degrees a four-legged zombie's shoulders dip toward its stiff front leg as it lands
		/// </summary>
		private const float ShoulderDipAngle = 7f;

		/// <summary>
		/// Degrees a four-legged zombie carries its head low
		/// </summary>
		private const float HeadHangAngle = 15f;

		/// <summary>
		/// Degrees a four-legged zombie's head bobs up as its stiff front leg lands, and down as the other one does
		/// </summary>
		private const float HeadNodAngle = 8f;

		/// <summary>
		/// Degrees a four-legged zombie's head tilts to its side
		/// </summary>
		private const float FourLeggedHeadTiltAngle = 10f;

		/// <summary>
		/// Degrees the spine sways from side to side while the zombie stands still
		/// </summary>
		private const float IdleSwayAngle = 3f;

		/// <summary>
		/// How fast the spine sways while the zombie stands still, in radians a second
		/// </summary>
		private const float IdleSwaySpeed = 1.3f;

		/// <summary>
		/// Degrees the head tilts to its side
		/// </summary>
		private const float HeadTiltAngle = 16f;

		/// <summary>
		/// Degrees the head drops forward
		/// </summary>
		private const float HeadDropAngle = 8f;

		/// <summary>
		/// How much further the head rolls with the spine's lean, as a fraction of the lean
		/// </summary>
		private const float HeadFlop = 0.6f;

		/// <summary>
		/// How much of the way the arm on the stiff side turns from its animation to hanging down
		/// </summary>
		private const float ArmHang = 0.8f;

		/// <summary>
		/// How far out to the side the hanging arm points, against one down
		/// </summary>
		private const float ArmOutward = 0.2f;

		/// <summary>
		/// How far forward the hanging arm points, against one down
		/// </summary>
		private const float ArmForward = 0.1f;

		/// <summary>
		/// How far the hanging arm swings back as the stiff leg takes the zombie's weight, against one down
		/// </summary>
		private const float ArmSwing = 0.25f;

		/// <summary>
		/// How much the hanging arm's elbow bends forward, from 0 for straight to 1 for a right angle
		/// </summary>
		private const float ElbowBend = 0.15f;

		/// <summary>
		/// How much of the way the stiff leg's knee straightens as it swings
		/// </summary>
		private const float LegStiffness = 0.6f;

		/// <summary>
		/// How much stronger the lean and the stiff leg are at no health, as a fraction more
		/// </summary>
		private const float WoundedExtra = 0.5f;

		/// <summary>
		/// How high a lifted foot counts as a full step, as a fraction of the hips' height
		/// </summary>
		private const float StepHeight = 0.12f;

		/// <summary>
		/// How high the chest has to be above the hips, and the hands above the feet, for the zombie to count as
		/// standing on two legs, as fractions of the hips' height - a creature on four legs has its front feet, which
		/// carry the hands' names, on the ground
		/// </summary>
		private const float UprightChestHeight = 0.2f;
		private const float UprightHandHeight = 0.25f;

		/// <summary>
		/// Seconds the zombie has to walk on two legs, or on four, before it lurches, so a pose it passes through
		/// (e.g., in its wake-up animation) doesn't count
		/// </summary>
		private const float StanceTime = 1f;

		/// <summary>
		/// Seconds the zombie can spend walking without being seen on two legs or four before the lurch gives up on it
		/// </summary>
		private const float GiveUpTime = 10f;

		/// <summary>
		/// Meters a second the zombie has to move for its steps to lurch fully
		/// </summary>
		private const float WalkingSpeed = 0.6f;

		/// <summary>
		/// Meters a second above which a move counts as a teleport and is ignored
		/// </summary>
		private const float MaxSpeed = 30f;

		/// <summary>
		/// How quickly the speed and step follow the zombie's movement, per second
		/// </summary>
		private const float SpeedSmoothing = 6f;
		private const float StepSmoothing = 12f;

		/// <summary>
		/// Seconds the lurch takes to fade in once the zombie is standing
		/// </summary>
		private const float FadeTime = 1f;

		/// <summary>
		/// Seconds the lean and hanging arm take to give way to an attack, block or stagger, and to come back
		/// </summary>
		private const float ActionFadeTime = 0.2f;

		private Character character;
		private MonsterAI ai;

		/// <summary>
		/// The common parent of the feet, and of the hands
		/// </summary>
		private Transform hips;
		private Transform chest;

		/// <summary>
		/// The bones from the hips' child down to the chest
		/// </summary>
		private Transform[] spine;

		/// <summary>
		/// The head, or null when there's none under the spine
		/// </summary>
		private Transform head;

		/// <summary>
		/// The stiff side's arm, or null for the upper and lower arm when they can't be told apart, and the other
		/// arm's hand
		/// </summary>
		private Transform armUpper;
		private Transform armLower;
		private Transform hand;
		private Transform otherHand;

		/// <summary>
		/// The stiff leg, or null for the upper and lower leg when they can't be told apart, and the other leg's foot
		/// </summary>
		private Transform legUpper;
		private Transform legLower;
		private Transform foot;
		private Transform otherFoot;

		/// <summary>
		/// The top bone of each leg, just under the hips, where the legs join the body - the hips themselves can be
		/// the skeleton's root, down at the feet (e.g., a troll's)
		/// </summary>
		private Transform legTop;
		private Transform otherLegTop;

		/// <summary>
		/// Every bone the lurch turns, and its local rotation before the lurch turned it this frame
		/// </summary>
		private Transform[] touched;
		private Quaternion[] savedRotations;

		/// <summary>
		/// Whether the bones were turned this frame and need putting back
		/// </summary>
		private bool applied;

		/// <summary>
		/// The zombie's skinned meshes, inactive ones and every level of detail included, so the bones are only bent
		/// while one of them is drawn
		/// </summary>
		private SkinnedMeshRenderer[] bodies;

		/// <summary>
		/// 1 when the stiff leg and hanging arm are on the right, -1 on the left
		/// </summary>
		private int side;

		/// <summary>
		/// 1 when the head tilts to the right, -1 to the left
		/// </summary>
		private int headSide;

		/// <summary>
		/// How strongly this zombie lurches, from 1 to 2 times ZombieLurch
		/// </summary>
		private float lurchScore;

		/// <summary>
		/// Where in its sway this zombie starts, in radians
		/// </summary>
		private float swayPhase;

		/// <summary>
		/// How a zombie holds itself
		/// </summary>
		private enum Stance
		{
			Unknown,
			TwoLegs,
			FourLegs
		}

		/// <summary>
		/// How the zombie walks once it's been seen for long enough, the stance it was last seen in and for how long,
		/// and for how long it has been checked
		/// </summary>
		private Stance stance;
		private Stance seenStance;
		private float seenTime;
		private float checkTime;

		/// <summary>
		/// The height of the tops of the legs above the feet when the zombie was last checked
		/// </summary>
		private float hipHeight;

		/// <summary>
		/// Whether the zombie has finished getting up from its corpse
		/// </summary>
		private bool risen;

		/// <summary>
		/// How much of the lurch is shown, and how much of the lean and hanging arm, from 0 to 1
		/// </summary>
		private float weight;
		private float freeWeight = 1f;

		/// <summary>
		/// The zombie's smoothed speed across the ground and where it was last frame
		/// </summary>
		private float speed;
		private Vector3 lastPosition;

		/// <summary>
		/// The smoothed step, from 1 while the stiff leg carries the zombie's weight to -1 while it swings
		/// </summary>
		private float step;

		/// <summary>
		/// Finds the bones and works out this zombie's lurch, or removes the lurch from a creature without two feet
		/// and two hands, and on a dedicated server, which draws nothing
		/// </summary>
		private void Start()
		{
			character = GetComponent<Character>();
			if ((ZNet.instance != null && ZNet.instance.IsDedicated()) || character == null || character.m_animator == null)
			{
				Destroy(this);
				return;
			}
			ZDO zdo = (character.m_nview != null ? character.m_nview.GetZDO() : null);
			System.Random random = new System.Random((zdo != null ? zdo.m_uid.GetHashCode() : 0));
			side = (random.Next(2) == 0 ? -1 : 1);
			headSide = (random.Next(2) == 0 ? -1 : 1);
			lurchScore = 1f + (float)random.NextDouble();
			swayPhase = (float)random.NextDouble() * Mathf.PI * 2f;
			if (!FindBones())
			{
				Destroy(this);
				return;
			}
			ai = GetComponent<MonsterAI>();
			bodies = GetComponentsInChildren<SkinnedMeshRenderer>(true);
			lastPosition = transform.position;
			StartCoroutine(PutBack());
		}

		/// <summary>
		/// Bends the bones on top of the pose the animator gave them this frame
		/// </summary>
		private void LateUpdate()
		{
			float dt = Time.deltaTime;
			UpdateSpeed(dt);
			bool able = CanLurch();
			bool busy = (character.InAttack() || character.IsBlocking() || character.IsStaggering());
			float moving = Mathf.Clamp01(speed / WalkingSpeed);
			// Only judged while walking, which a pose it passes through standing still (e.g., getting up) isn't
			if (able && !busy && stance == Stance.Unknown && moving >= 1f)
			{
				Stance current = ReadStance();
				seenTime = (current == seenStance ? seenTime + dt : 0f);
				seenStance = current;
				if (current != Stance.Unknown && seenTime >= StanceTime)
				{
					stance = current;
				}
				checkTime += dt;
				if (stance == Stance.Unknown && checkTime >= GiveUpTime)
				{
					Destroy(this);
					return;
				}
			}
			weight = Mathf.MoveTowards(weight, (able && stance != Stance.Unknown ? 1f : 0f), dt / FadeTime);
			freeWeight = Mathf.MoveTowards(freeWeight, (busy ? 0f : 1f), dt / ActionFadeTime);
			float amount = weight * lurchScore * Plugin.ZombieLurch.Value / 100f;
			if (amount <= 0f || !IsDrawn())
			{
				return;
			}
			bool fourLegs = (stance == Stance.FourLegs);
			float wounded = 1f + WoundedExtra * (1f - HealthFraction());
			// The stiff leg is a four-legged zombie's front one. The other leg is lifted while the stiff one carries
			// the zombie's weight, read before the stiff one is bent
			Transform stiffFoot = (fourLegs ? hand : foot);
			Transform otherStiffFoot = (fourLegs ? otherHand : otherFoot);
			float lift = Vector3.Dot(otherStiffFoot.position - stiffFoot.position, transform.up) / (hipHeight * StepHeight);
			step = Mathf.Lerp(step, Mathf.Clamp(lift, -1f, 1f) * moving, 1f - Mathf.Exp(-StepSmoothing * dt));
			for (int i = 0; i < touched.Length; i++)
			{
				savedRotations[i] = touched[i].localRotation;
			}
			applied = true;
			float sway = IdleSwayAngle * Mathf.Sin(Time.time * IdleSwaySpeed + swayPhase) * (1f - moving);
			// Degrees toward the stiff side
			float lean = ((fourLegs ? ShoulderDipAngle : LeanAngle) * (step > 0f ? step : step * BackLean) * wounded + sway) * freeWeight;
			if (fourLegs)
			{
				LurchOnFourLegs(amount, wounded, lean);
			}
			else
			{
				LurchOnTwoLegs(amount, wounded, lean);
			}
		}

		/// <summary>
		/// Hunches a zombie on two legs over, lolls its head, hangs the arm on its stiff side and drags its stiff leg
		/// </summary>
		/// <param name="amount">How much of the lurch is shown, around 1</param>
		/// <param name="wounded">How much stronger the lean and stiff leg are for the zombie's wounds, from 1</param>
		/// <param name="lean">Degrees the spine leans toward the stiff side</param>
		private void LurchOnTwoLegs(float amount, float wounded, float lean)
		{
			Vector3 up = transform.up;
			Vector3 forward = transform.forward;
			Vector3 right = transform.right;
			float hunch = HunchAngle + DipAngle * Mathf.Max(step, 0f) * wounded * freeWeight;
			BendSpine(hunch * amount, lean * amount);
			if (head != null)
			{
				float tilt = HeadTiltAngle * headSide + HeadFlop * side * lean;
				TurnHead(HeadDropAngle * amount, tilt * amount);
			}
			float hang = Mathf.Clamp01(ArmHang * amount) * freeWeight;
			if (armLower != null && hang > 0f)
			{
				Vector3 down = (-up + right * (side * ArmOutward) + forward * (ArmForward - ArmSwing * step)).normalized;
				Turn(armUpper, armLower.position - armUpper.position, down, hang);
				Vector3 upperDirection = (armLower.position - armUpper.position).normalized;
				Turn(armLower, hand.position - armLower.position, Vector3.Slerp(upperDirection, forward, ElbowBend), hang);
			}
			if (legLower != null)
			{
				Stiffen(legUpper, legLower, foot, amount, wounded);
			}
		}

		/// <summary>
		/// Dips a zombie on four legs onto its stiff front leg as it lands, hangs and bobs its head and drags the stiff
		/// front leg
		/// </summary>
		/// <param name="amount">How much of the lurch is shown, around 1</param>
		/// <param name="wounded">How much stronger the dip and stiff leg are for the zombie's wounds, from 1</param>
		/// <param name="lean">Degrees the shoulders dip toward the stiff side</param>
		private void LurchOnFourLegs(float amount, float wounded, float lean)
		{
			BendSpine(0f, lean * amount);
			if (head != null)
			{
				// A limping animal's head comes up as the sore leg lands, taking weight off it
				float drop = (HeadHangAngle - HeadNodAngle * step * wounded) * (0.5f + 0.5f * freeWeight);
				float tilt = FourLeggedHeadTiltAngle * headSide + HeadFlop * side * lean;
				TurnHead(drop * amount, tilt * amount);
			}
			if (armLower != null)
			{
				Stiffen(armUpper, armLower, hand, amount, wounded);
			}
		}

		/// <summary>
		/// Bends the spine forward and leans it to the side, spread evenly along its bones
		/// </summary>
		/// <param name="hunch">Degrees forward</param>
		/// <param name="lean">Degrees toward the stiff side</param>
		private void BendSpine(float hunch, float lean)
		{
			// Turning about the forward axis by a negative angle tips a bone's top to the right
			Quaternion bend = Quaternion.AngleAxis(hunch / spine.Length, transform.right) * Quaternion.AngleAxis(-side * lean / spine.Length, transform.forward);
			foreach (Transform bone in spine)
			{
				bone.rotation = bend * bone.rotation;
			}
		}

		/// <summary>
		/// Drops the head forward and tilts it to the side
		/// </summary>
		/// <param name="drop">Degrees forward</param>
		/// <param name="tilt">Degrees to the right</param>
		private void TurnHead(float drop, float tilt)
		{
			head.rotation = Quaternion.AngleAxis(drop, transform.right) * Quaternion.AngleAxis(-tilt, transform.forward) * head.rotation;
		}

		/// <summary>
		/// Straightens the stiff leg's knee as it swings, so it drags, and leaves it bent while it carries the
		/// zombie's weight, so the foot doesn't sink into the ground
		/// </summary>
		/// <param name="upper">The leg's upper bone</param>
		/// <param name="lower">The leg's lower bone</param>
		/// <param name="end">The leg's foot, which keeps its turn so it doesn't tip</param>
		/// <param name="amount">How much of the lurch is shown, around 1</param>
		/// <param name="wounded">How much stiffer the leg is for the zombie's wounds, from 1</param>
		private void Stiffen(Transform upper, Transform lower, Transform end, float amount, float wounded)
		{
			float stiff = Mathf.Clamp01(LegStiffness * amount * wounded) * Mathf.Clamp01(0.5f - 0.5f * step);
			if (stiff <= 0f)
			{
				return;
			}
			Quaternion endRotation = end.rotation;
			Turn(lower, end.position - lower.position, lower.position - upper.position, stiff);
			end.rotation = endRotation;
		}

		/// <summary>
		/// Checks whether the zombie is up and about - standing after getting up from its corpse, alive, awake and not
		/// swimming
		/// </summary>
		/// <returns>True when it can lurch</returns>
		private bool CanLurch()
		{
			if (!risen)
			{
				risen = (GetComponent<RiseBlend>() == null);
			}
			return(risen && !character.IsDead() && !character.IsSwimming() && (ai == null || !ai.IsSleeping()));
		}

		/// <summary>
		/// Follows how fast the zombie moves across the ground, from where it is each frame so it's the same on clients
		/// that don't own it
		/// </summary>
		/// <param name="dt">Seconds since last frame</param>
		private void UpdateSpeed(float dt)
		{
			Vector3 move = transform.position - lastPosition;
			lastPosition = transform.position;
			move.y = 0f;
			if (dt <= 0f)
			{
				return;
			}
			float current = move.magnitude / dt;
			if (current > MaxSpeed)
			{
				return;
			}
			speed = Mathf.Lerp(speed, current, 1f - Mathf.Exp(-SpeedSmoothing * dt));
		}

		/// <summary>
		/// Works out how much health the zombie has left
		/// </summary>
		/// <returns>Its health as a fraction of its max, or 1 when it has no max</returns>
		private float HealthFraction()
		{
			float maxHealth = character.GetMaxHealth();
			return((maxHealth > 0f ? Mathf.Clamp01(character.GetHealth() / maxHealth) : 1f));
		}

		/// <summary>
		/// Checks whether any camera draws one of the zombie's skinned meshes, its shadow included
		/// </summary>
		/// <returns>True when one is drawn, or when the zombie has none left to check</returns>
		private bool IsDrawn()
		{
			if (bodies == null)
			{
				return(true);
			}
			bool any = false;
			foreach (SkinnedMeshRenderer body in bodies)
			{
				if (body == null)
				{
					continue;
				}
				if (body.isVisible)
				{
					return(true);
				}
				any = true;
			}
			return(!any);
		}

		/// <summary>
		/// Turns a bone part of the way so a direction from it points another way
		/// </summary>
		/// <param name="bone">The bone</param>
		/// <param name="from">The direction as it points now, in world space</param>
		/// <param name="to">The direction to point it, in world space</param>
		/// <param name="t">How far to turn it, from 0 to 1</param>
		private static void Turn(Transform bone, Vector3 from, Vector3 to, float t)
		{
			if (from.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f)
			{
				return;
			}
			bone.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(from, to), t) * bone.rotation;
		}

		/// <summary>
		/// Reads how the zombie holds itself from its animated pose - on two legs with the tops of its legs above its
		/// feet, its chest above the tops of its legs and its hands off the ground, or on four with its hands (its
		/// front feet) down on the ground
		/// </summary>
		/// <returns>The stance, or Unknown when it's neither</returns>
		private Stance ReadStance()
		{
			Vector3 up = transform.up;
			float feet = (Vector3.Dot(foot.position, up) + Vector3.Dot(otherFoot.position, up)) / 2f;
			float legTops = (Vector3.Dot(legTop.position, up) + Vector3.Dot(otherLegTop.position, up)) / 2f;
			hipHeight = legTops - feet;
			float chestHeight = Vector3.Dot(chest.position, up) - legTops;
			float handHeight = (Vector3.Dot(hand.position, up) + Vector3.Dot(otherHand.position, up)) / 2f - feet;
			if (hipHeight <= 0.05f)
			{
				return(Stance.Unknown);
			}
			else if (handHeight < UprightHandHeight * hipHeight)
			{
				return(Stance.FourLegs);
			}
			else if (chestHeight > UprightChestHeight * hipHeight)
			{
				return(Stance.TwoLegs);
			}
			return(Stance.Unknown);
		}

		/// <summary>
		/// Finds the hips, spine, head and the stiff side's arm and leg
		/// </summary>
		/// <returns>True when the skeleton has two feet and two hands, with the hands' common parent under the feet's</returns>
		private bool FindBones()
		{
			Transform root = character.m_animator.transform;
			Transform[] all = root.GetComponentsInChildren<Transform>(true);
			Transform leftFoot = FindBone(all, root, -1, "foot", false);
			Transform rightFoot = FindBone(all, root, 1, "foot", false);
			Transform leftHand = FindBone(all, root, -1, "hand", false);
			Transform rightHand = FindBone(all, root, 1, "hand", false);
			if (leftFoot == null || rightFoot == null || leftHand == null || rightHand == null)
			{
				return(false);
			}
			hips = CommonParent(leftFoot, rightFoot);
			chest = CommonParent(leftHand, rightHand);
			if (hips == null || chest == null || chest == hips || !chest.IsChildOf(hips))
			{
				return(false);
			}
			List<Transform> chain = new List<Transform>();
			for (Transform bone = chest; bone != hips; bone = bone.parent)
			{
				chain.Insert(0, bone);
			}
			spine = chain.ToArray();
			head = FindBone(all, root, 0, "head", true);
			if (head != null && !head.IsChildOf(spine[0]))
			{
				head = null;
			}
			foot = (side < 0 ? leftFoot : rightFoot);
			otherFoot = (side < 0 ? rightFoot : leftFoot);
			hand = (side < 0 ? leftHand : rightHand);
			otherHand = (side < 0 ? rightHand : leftHand);
			legTop = ChildToward(foot, hips);
			otherLegTop = ChildToward(otherFoot, hips);
			// A leg or arm needs an upper and lower bone of its own to bend, but the zombie hunches and leans without
			legLower = foot.parent;
			legUpper = legLower.parent;
			if (legLower == hips || legUpper == hips || !legUpper.IsChildOf(hips))
			{
				legLower = legUpper = null;
			}
			armLower = hand.parent;
			armUpper = armLower.parent;
			if (armLower == chest || armUpper == chest || !armUpper.IsChildOf(chest))
			{
				armLower = armUpper = null;
			}
			List<Transform> bones = new List<Transform>(spine);
			foreach (Transform bone in new Transform[] { head, armUpper, armLower, hand, legLower, foot })
			{
				if (bone != null)
				{
					bones.Add(bone);
				}
			}
			touched = bones.ToArray();
			savedRotations = new Quaternion[touched.Length];
			return(true);
		}

		/// <summary>
		/// Finds the bone highest in the skeleton with a part in its name on one side
		/// </summary>
		/// <param name="all">Every transform under the root</param>
		/// <param name="root">The animator's transform</param>
		/// <param name="wantSide">-1 for the left, 1 for the right, or 0 for either</param>
		/// <param name="part">The part, in lower case (e.g., "foot")</param>
		/// <param name="exact">Whether the name has to be just the part</param>
		/// <returns>The bone, or null when there's none</returns>
		private static Transform FindBone(Transform[] all, Transform root, int wantSide, string part, bool exact)
		{
			Transform found = null;
			int foundDepth = int.MaxValue;
			foreach (Transform bone in all)
			{
				string name = bone.name.ToLowerInvariant();
				if ((exact ? name != part : name.IndexOf(part, System.StringComparison.Ordinal) < 0) || (wantSide != 0 && Side(name) != wantSide))
				{
					continue;
				}
				int depth = Depth(bone, root);
				if (depth < foundDepth)
				{
					found = bone;
					foundDepth = depth;
				}
			}
			return(found);
		}

		/// <summary>
		/// Works out which side a bone is on from its name - Left or l_ / l. at the start, or _l / .l at the end
		/// </summary>
		/// <param name="name">The bone's name, in lower case</param>
		/// <returns>-1 for the left, 1 for the right, or 0 when its name doesn't say</returns>
		internal static int Side(string name)
		{
			if (name.StartsWith("left"))
			{
				return(-1);
			}
			else if (name.StartsWith("right"))
			{
				return(1);
			}
			int length = name.Length;
			if (length < 2)
			{
				return(0);
			}
			if (IsSeparator(name[1]))
			{
				if (name[0] == 'l')
				{
					return(-1);
				}
				else if (name[0] == 'r')
				{
					return(1);
				}
			}
			if (IsSeparator(name[length - 2]))
			{
				if (name[length - 1] == 'l')
				{
					return(-1);
				}
				else if (name[length - 1] == 'r')
				{
					return(1);
				}
			}
			return(0);
		}

		/// <summary>
		/// Checks whether a character separates a side's letter from the rest of a bone's name
		/// </summary>
		/// <param name="c">The character</param>
		/// <returns>True for a dot, underscore, space or hyphen</returns>
		private static bool IsSeparator(char c)
		{
			return(c == '.' || c == '_' || c == ' ' || c == '-');
		}

		/// <summary>
		/// Counts how many parents a bone has below the root
		/// </summary>
		/// <param name="bone">The bone</param>
		/// <param name="root">The animator's transform</param>
		/// <returns>The count</returns>
		private static int Depth(Transform bone, Transform root)
		{
			int depth = 0;
			for (Transform parent = bone; parent != null && parent != root; parent = parent.parent)
			{
				depth++;
			}
			return(depth);
		}

		/// <summary>
		/// Finds the lowest bone both bones are under
		/// </summary>
		/// <param name="a">One bone</param>
		/// <param name="b">The other bone</param>
		/// <returns>The common parent, or null when they share none</returns>
		private static Transform CommonParent(Transform a, Transform b)
		{
			HashSet<Transform> parents = new HashSet<Transform>();
			for (Transform parent = a.parent; parent != null; parent = parent.parent)
			{
				parents.Add(parent);
			}
			for (Transform parent = b.parent; parent != null; parent = parent.parent)
			{
				if (parents.Contains(parent))
				{
					return(parent);
				}
			}
			return(null);
		}

		/// <summary>
		/// Finds the child of a parent that a bone under it hangs from
		/// </summary>
		/// <param name="bone">The bone</param>
		/// <param name="parent">One of the bone's parents</param>
		/// <returns>The parent's child on the way down to the bone, or the bone itself when it's a direct child</returns>
		private static Transform ChildToward(Transform bone, Transform parent)
		{
			while (bone.parent != parent)
			{
				bone = bone.parent;
			}
			return(bone);
		}

		/// <summary>
		/// Puts back each bone the lurch turned at the end of every frame, once it's been drawn
		/// </summary>
		/// <returns>The coroutine</returns>
		private IEnumerator PutBack()
		{
			WaitForEndOfFrame wait = new WaitForEndOfFrame();
			while (true)
			{
				yield return wait;
				if (applied)
				{
					applied = false;
					for (int i = 0; i < touched.Length; i++)
					{
						if (touched[i] != null)
						{
							touched[i].localRotation = savedRotations[i];
						}
					}
				}
			}
		}
	}
}
