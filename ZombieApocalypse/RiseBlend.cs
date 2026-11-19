using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Makes a zombie get up from its corpse by blending its bones from the corpse's pose into its animated pose
	/// </summary>
	/// <remarks>
	/// Most corpses share their creature's skeleton and bone names, so each of the creature's bones starts
	/// where the corpse's bone of that name lay and turns into the pose the animator gives it - idle, or asleep for a
	/// creature that wakes up with its own animation. The blend runs from RiseStartKey to RiseEndKey on every client,
	/// each from its own copy of the corpse, and the zombie stays hidden until its corpse hands over. Its AI is held
	/// still until the end, when the owner wakes it and sets it on its enemies, and the bones then ease from the
	/// blend's last pose into whatever the animator does next, so a change of stance as the AI takes over doesn't snap. It runs late so it has the last say
	/// on the bones after the animator and anything else that moves them
	/// </remarks>
	[DefaultExecutionOrder(10000)]
	internal class RiseBlend : MonoBehaviour
	{
		/// <summary>
		/// The AIs of zombies still getting up, which don't think until they're standing
		/// </summary>
		private static readonly HashSet<BaseAI> heldAIs = new HashSet<BaseAI>();

		private Character character;
		private BaseAI ai;
		private long riseStart;
		private long riseEnd;

		/// <summary>
		/// The creature's bones that match a corpse bone, parents before children
		/// </summary>
		private Transform[] bones;

		/// <summary>
		/// Each bone's corpse position and rotation in world space
		/// </summary>
		private Vector3[] fromPositions;
		private Quaternion[] fromRotations;

		/// <summary>
		/// Whether a bone's position is blended as well as its rotation - when its parent isn't blended, or when its
		/// corpse bone is the top one physics moves, which can lie away from bones above it that physics leaves
		/// standing (e.g., a fenring's Root above its Hips)
		/// </summary>
		private bool[] blendPositions;

		/// <summary>
		/// Whether a bone's corpse bone is left standing where the creature died, since neither it nor any bone above it
		/// is moved by physics (e.g., a fuling shaman's Root and Root2). Its starting pose is worked out each frame
		/// from its animated pose instead, carried along with the hips to where the corpse lies, so mesh weighted to it
		/// doesn't stretch back to where the creature stood
		/// </summary>
		private bool[] frozen;

		/// <summary>
		/// The bone whose corpse bone is the top one physics moves, which the frozen bones are carried along with, or
		/// -1 when there's none
		/// </summary>
		private int hipsIndex = -1;

		/// <summary>
		/// Each bone's local position and rotation before the blend moved it this frame, put back before the animator
		/// runs so a bone the animator doesn't move starts from its own pose again
		/// </summary>
		private Vector3[] animatedLocalPositions;
		private Quaternion[] animatedLocalRotations;

		private Vector3[] animatedPositions;
		private Quaternion[] animatedRotations;
		private bool posed;

		/// <summary>
		/// Whether the animated pose has been read at least once, so there's a pose to put back
		/// </summary>
		private bool captured;

		private bool hidden;

		/// <summary>
		/// The zombie's simulated tails, paused during the blend
		/// </summary>
		private Tail[] tails;

		/// <summary>
		/// The animator's culling mode before the blend kept it animating
		/// </summary>
		private AnimatorCullingMode cullingMode;

		/// <summary>
		/// Whether the zombie is standing and easing from the blend's last pose into whatever the animator does next
		/// </summary>
		private bool settling;

		private float settleStart;
		private Vector3[] settlePositions;
		private Quaternion[] settleRotations;

		/// <summary>
		/// Seconds the zombie takes to ease from the blend's last pose into the animator's
		/// </summary>
		private const float SettleTime = 0.25f;

		/// <summary>
		/// Seconds the zombie sticks to its killer before it looks for the nearest enemy again
		/// </summary>
		private const float KillerFocusTime = 10f;

		/// <summary>
		/// Checks whether an AI belongs to a zombie that's still getting up
		/// </summary>
		/// <param name="ai">The AI</param>
		/// <returns>True when it should be held still</returns>
		public static bool IsHeld(BaseAI ai)
		{
			return(heldAIs.Contains(ai));
		}

		/// <summary>
		/// Reads when the zombie gets up, holds its AI, keeps its animator animating and hides it until its corpse
		/// hands over
		/// </summary>
		/// <remarks>
		/// A creature's animator stops moving its bones while it can't be seen, which the zombie can't while it's
		/// hidden, leaving the bones in the model's bind pose (e.g., a fuling shaman's arms spread wide). The blend
		/// would then turn the zombie into that pose instead of its idle one, so the animator animates throughout
		/// </remarks>
		private void Awake()
		{
			character = GetComponent<Character>();
			ai = GetComponent<BaseAI>();
			ZDO zdo = character.m_nview.GetZDO();
			riseStart = zdo.GetLong(Zombies.RiseStartKey);
			riseEnd = zdo.GetLong(Zombies.RiseEndKey);
			if (ai != null)
			{
				heldAIs.Add(ai);
				// Alerted from the start, quietly, so the idle the blend heads for is the alerted one - an unalerted
				// creature picks a random idle (e.g., a fuling shaman's arms-out ritual) and would switch to its alerted
				// stance with a jump once standing. The alert sound plays when it's standing
				ai.m_alerted = true;
				if (character.m_nview.IsOwner())
				{
					zdo.Set(ZDOVars.s_alert, true);
					if (ai.m_animator != null)
					{
						ai.m_animator.SetBool("alert", true);
					}
				}
			}
			cullingMode = character.m_animator.cullingMode;
			character.m_animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
			SetHidden(true);
		}

		/// <summary>
		/// Releases the AI and puts back the animator's culling when the zombie is destroyed mid-blend
		/// </summary>
		private void OnDestroy()
		{
			if (ai != null)
			{
				heldAIs.Remove(ai);
			}
			if (character != null && character.m_animator != null)
			{
				character.m_animator.cullingMode = cullingMode;
			}
		}

		/// <summary>
		/// Starts the blend from a corpse's pose and shows the zombie, when the corpse's skeleton matches the
		/// creature's
		/// </summary>
		/// <remarks>
		/// A few corpses (e.g., the greydwarf's) are built on a different skeleton from their creature. When fewer than
		/// half the corpse's physics bones are found in the creature, the zombie stays hidden until it's standing and the
		/// corpse gets up on its own instead
		/// </remarks>
		/// <param name="corpse">The root of the corpse the zombie rises from</param>
		/// <returns>True when the blend started, false when the corpse should get up on its own</returns>
		public bool Begin(Transform corpse)
		{
			if (posed)
			{
				return(true);
			}
			// Only bones from the corpse's and creature's own prefabs are matched, leaving out equipment, which can bring
			// a copy of the skeleton with the same bone names (e.g., a fuling shaman's corpse is dressed in its gear)
			HashSet<string> corpsePaths = PrefabPaths(corpse.gameObject);
			HashSet<string> creaturePaths = PrefabPaths(character.gameObject);
			Dictionary<string, Transform> corpseBones = new Dictionary<string, Transform>();
			foreach (Transform bone in corpse.GetComponentsInChildren<Transform>(true))
			{
				if (corpsePaths != null && !corpsePaths.Contains(PathFrom(corpse, bone)))
				{
					continue;
				}
				// A name used twice can't be matched, so it's kept as null
				corpseBones[bone.name] = (corpseBones.ContainsKey(bone.name) ? null : bone);
			}
			HashSet<Transform> skinBones = new HashSet<Transform>();
			foreach (SkinnedMeshRenderer skin in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				foreach (Transform bone in skin.bones)
				{
					if (bone != null)
					{
						skinBones.Add(bone);
					}
				}
				if (skin.rootBone != null)
				{
					skinBones.Add(skin.rootBone);
				}
			}
			List<Transform> matched = new List<Transform>();
			List<Transform> sources = new List<Transform>();
			HashSet<Transform> matchedSet = new HashSet<Transform>();
			// GetComponentsInChildren lists parents before their children, which the blend relies on
			foreach (Transform bone in character.m_animator.GetComponentsInChildren<Transform>(true))
			{
				if (creaturePaths != null && !creaturePaths.Contains(PathFrom(transform, bone)))
				{
					continue;
				}
				if (skinBones.Contains(bone) && corpseBones.TryGetValue(bone.name, out Transform source) && source != null && matchedSet.Add(bone))
				{
					matched.Add(bone);
					sources.Add(source);
				}
			}
			int count = matched.Count;
			// Judged by the corpse's physics bones, which carry its pose, rather than all the zombie's skinned bones,
			// since equipment can bring skinned bones of its own that no corpse has
			HashSet<Transform> matchedSources = new HashSet<Transform>(sources);
			int bodies = 0;
			int matchedBodies = 0;
			foreach (Rigidbody body in corpse.GetComponentsInChildren<Rigidbody>(true))
			{
				bodies++;
				if (matchedSources.Contains(body.transform))
				{
					matchedBodies++;
				}
			}
			if (count == 0 || matchedBodies * 2 < bodies)
			{
				return(false);
			}
			bones = matched.ToArray();
			fromPositions = new Vector3[count];
			fromRotations = new Quaternion[count];
			blendPositions = new bool[count];
			frozen = new bool[count];
			animatedLocalPositions = new Vector3[count];
			animatedLocalRotations = new Quaternion[count];
			animatedPositions = new Vector3[count];
			animatedRotations = new Quaternion[count];
			for (int i = 0; i < count; i++)
			{
				fromPositions[i] = sources[i].position;
				fromRotations[i] = sources[i].rotation;
				bool topBody = IsTopBody(sources[i], corpse);
				if (topBody && hipsIndex < 0)
				{
					hipsIndex = i;
				}
				frozen[i] = !IsMovedByPhysics(sources[i], corpse);
				blendPositions[i] = (!matchedSet.Contains(bones[i].parent) || topBody || frozen[i]);
			}
			// A tail (e.g., a neck's) is simulated after the animator and would drag behind the bones the blend moves,
			// stretching it out, so it's paused until the zombie is standing
			tails = GetComponentsInChildren<Tail>();
			foreach (Tail tail in tails)
			{
				tail.enabled = false;
			}
			posed = true;
			StartCoroutine(PutBackAnimatedPose());
			EquipWeapon();
			SetHidden(false);
			return(true);
		}

		/// <summary>
		/// Lists the path of every object in an instance's prefab, to tell its own bones from ones added since
		/// </summary>
		/// <param name="instance">The spawned object</param>
		/// <returns>The paths from the prefab's root, or null when its prefab can't be found</returns>
		private static HashSet<string> PrefabPaths(GameObject instance)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(Utils.GetPrefabName(instance));
			if (prefab == null)
			{
				return(null);
			}
			HashSet<string> paths = new HashSet<string>();
			foreach (Transform part in prefab.GetComponentsInChildren<Transform>(true))
			{
				paths.Add(PathFrom(prefab.transform, part));
			}
			return(paths);
		}

		/// <summary>
		/// Builds an object's path from a root, the same as Transform.Find takes
		/// </summary>
		/// <param name="root">The root</param>
		/// <param name="part">An object under the root, or the root itself</param>
		/// <returns>The names from the root's child down to the object, joined with /, or empty for the root</returns>
		private static string PathFrom(Transform root, Transform part)
		{
			string path = "";
			for (Transform current = part; current != null && current != root; current = current.parent)
			{
				path = ((path.Length > 0) ? current.name + "/" + path : current.name);
			}
			return(path);
		}

		/// <summary>
		/// Checks whether physics moves a corpse bone, either itself or through a bone above it
		/// </summary>
		/// <param name="bone">The corpse bone</param>
		/// <param name="corpse">The corpse's root</param>
		/// <returns>True when the bone or one of its parents up to the root has a rigidbody</returns>
		private static bool IsMovedByPhysics(Transform bone, Transform corpse)
		{
			for (Transform part = bone; part != null && part != corpse; part = part.parent)
			{
				if (part.GetComponent<Rigidbody>() != null)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a corpse bone is moved by physics with no bone above it that is
		/// </summary>
		/// <param name="bone">The corpse bone</param>
		/// <param name="corpse">The corpse's root</param>
		/// <returns>True when the bone has a rigidbody and none of its parents up to the root do</returns>
		private static bool IsTopBody(Transform bone, Transform corpse)
		{
			if (bone.GetComponent<Rigidbody>() == null)
			{
				return(false);
			}
			for (Transform parent = bone.parent; parent != null && parent != corpse; parent = parent.parent)
			{
				if (parent.GetComponent<Rigidbody>() != null)
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Puts back the pose the animator gave each bone at the end of every frame, once it's been drawn, so the next
		/// frame starts from the animator's pose rather than the blend's
		/// </summary>
		/// <remarks>
		/// Without this, a bone the animator doesn't move would keep the blend's pose and be blended again from there.
		/// It's done at the end of the frame rather than in Update, since an animator that animates with physics
		/// (e.g., a fenring's) runs in FixedUpdate, before Update, and putting the pose back after it would undo its work
		/// - leaving the blend heading for the bind pose, arms out like a cross
		/// </remarks>
		/// <returns>The coroutine's steps</returns>
		private IEnumerator PutBackAnimatedPose()
		{
			WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
			while (true)
			{
				yield return endOfFrame;
				if (!captured)
				{
					continue;
				}
				for (int i = 0; i < bones.Length; i++)
				{
					if (bones[i] != null)
					{
						bones[i].localPosition = animatedLocalPositions[i];
						bones[i].localRotation = animatedLocalRotations[i];
					}
				}
			}
		}

		/// <summary>
		/// Blends the bones toward the animated pose, then once the zombie is standing eases them into whatever the
		/// animator does next
		/// </summary>
		private void LateUpdate()
		{
			long now = Zombies.Now();
			if (!posed)
			{
				// Equipment can be added after spawning, so it's hidden again until the corpse hands over
				SetHidden(true);
				if (now >= riseEnd)
				{
					Finish();
					Done();
				}
				return;
			}
			for (int i = 0; i < bones.Length; i++)
			{
				Transform bone = bones[i];
				if (bone == null)
				{
					continue;
				}
				animatedLocalPositions[i] = bone.localPosition;
				animatedLocalRotations[i] = bone.localRotation;
				animatedPositions[i] = bone.position;
				animatedRotations[i] = bone.rotation;
			}
			captured = true;
			if (settling)
			{
				float s = Mathf.Clamp01((Time.time - settleStart) / SettleTime);
				SetPose(settlePositions, settleRotations, s * s * (3f - 2f * s));
				if (s >= 1f)
				{
					Done();
				}
				return;
			}
			float t = ((riseEnd > riseStart) ? Mathf.Clamp01((float)(now - riseStart) / (riseEnd - riseStart)) : 1f);
			CarryFrozenBones();
			SetPose(fromPositions, fromRotations, t * t * (3f - 2f * t));
			if (now >= riseEnd)
			{
				Finish();
				settlePositions = new Vector3[bones.Length];
				settleRotations = new Quaternion[bones.Length];
				for (int i = 0; i < bones.Length; i++)
				{
					if (bones[i] != null)
					{
						settlePositions[i] = bones[i].position;
						settleRotations[i] = bones[i].rotation;
					}
				}
				settleStart = Time.time;
				settling = true;
			}
		}

		/// <summary>
		/// Works out the starting pose of each frozen bone - its animated pose, moved and turned the way the corpse's
		/// hips are from the animated hips
		/// </summary>
		private void CarryFrozenBones()
		{
			if (hipsIndex < 0)
			{
				return;
			}
			Quaternion turn = fromRotations[hipsIndex] * Quaternion.Inverse(animatedRotations[hipsIndex]);
			for (int i = 0; i < bones.Length; i++)
			{
				if (frozen[i])
				{
					fromPositions[i] = fromPositions[hipsIndex] + turn * (animatedPositions[i] - animatedPositions[hipsIndex]);
					fromRotations[i] = turn * animatedRotations[i];
				}
			}
		}

		/// <summary>
		/// Puts each bone part way between a starting pose and the animated pose read this frame
		/// </summary>
		/// <param name="positions">The starting world position of each bone, used for the bones in blendPositions</param>
		/// <param name="rotations">The starting world rotation of each bone</param>
		/// <param name="t">How far toward the animated pose, from 0 to 1</param>
		private void SetPose(Vector3[] positions, Quaternion[] rotations, float t)
		{
			for (int i = 0; i < bones.Length; i++)
			{
				Transform bone = bones[i];
				if (bone == null)
				{
					continue;
				}
				if (blendPositions[i])
				{
					bone.position = Vector3.Lerp(positions[i], animatedPositions[i], t);
				}
				bone.rotation = Quaternion.Slerp(rotations[i], animatedRotations[i], t);
			}
		}

		/// <summary>
		/// Starts the zombie's tails again, puts back its animator's culling and removes the blend
		/// </summary>
		private void Done()
		{
			if (tails != null)
			{
				foreach (Tail tail in tails)
				{
					if (tail != null)
					{
						// Starts the tail's simulation again from the standing pose, rather than from where it was paused
						tail.m_positions.Clear();
						tail.Awake();
						tail.enabled = true;
					}
				}
			}
			character.m_animator.cullingMode = cullingMode;
			Destroy(this);
		}

		/// <summary>
		/// Shows the zombie, lets its AI think and, on the owner, wakes it and sets it on its enemies
		/// </summary>
		private void Finish()
		{
			SetHidden(false);
			MonsterAI sleeper = ai as MonsterAI;
			if (sleeper != null && sleeper.IsSleeping() && WakeRoar.Roars(character) && GetComponent<WakeRoar>() == null)
			{
				gameObject.AddComponent<WakeRoar>();
			}
			if (ai != null)
			{
				heldAIs.Remove(ai);
				ZNetView nview = character.m_nview;
				if (nview != null && nview.IsValid() && nview.IsOwner())
				{
					MonsterAI monster = ai as MonsterAI;
					if (monster != null)
					{
						monster.Wakeup();
					}
					SkipTaunts();
					TargetKiller();
					// The zombie was alerted quietly as it started to rise, and alerting only plays the creature's alert
					// sound (e.g., a troll's growl) when it wasn't alerted already, so it's played here as it stands
					if (ai.IsAlerted())
					{
						ai.m_alertedEffects.Create(transform.position, Quaternion.identity);
					}
					else
					{
						ai.SetAlerted(true);
					}
				}
			}
		}

		/// <summary>
		/// Equips the zombie's best weapon on the owner when it has none, so the stance it blends into is its armed one
		/// </summary>
		/// <remarks>
		/// A creature equips a weapon as it spawns, but only when its default items are already in its inventory, which
		/// they may not be yet, and otherwise not until its AI's next update - which the blend holds off. Unarmed, some
		/// creatures stand differently (e.g., a fuling shaman with its arms out), and would jump into their armed stance
		/// once the AI took over. Other clients see the weapon through the game's own equipment syncing
		/// </remarks>
		private void EquipWeapon()
		{
			Humanoid humanoid = character as Humanoid;
			ZNetView nview = character.m_nview;
			if (humanoid == null || nview == null || !nview.IsValid() || !nview.IsOwner() || humanoid.GetCurrentWeapon() != null)
			{
				return;
			}
			humanoid.EquipBestWeapon(null, null, null, null);
		}

		/// <summary>
		/// Sets the zombie on whoever dealt its creature's killing blow, on the owner, when they're still nearby, alive
		/// and its enemy
		/// </summary>
		/// <remarks>
		/// A monster's AI looks for the nearest enemy every couple of seconds, which would soon swap the killer for
		/// someone closer, so that's held off for KillerFocusTime
		/// </remarks>
		private void TargetKiller()
		{
			ZDOID killerId = character.m_nview.GetZDO().GetZDOID(Zombies.KillerIdKey);
			if (killerId.IsNone())
			{
				return;
			}
			GameObject killerObject = ZNetScene.instance.FindInstance(killerId);
			Character killer = ((killerObject != null) ? killerObject.GetComponent<Character>() : null);
			if (killer == null || killer.IsDead() || !BaseAI.IsEnemy(character, killer))
			{
				return;
			}
			MonsterAI monster = ai as MonsterAI;
			if (monster != null)
			{
				monster.m_targetCreature = killer;
				monster.m_targetStatic = null;
				monster.m_lastKnownTargetPos = killer.transform.position;
				monster.m_beenAtLastPos = false;
				monster.m_updateTargetTimer = KillerFocusTime;
				return;
			}
			ZombieCharge charge = GetComponent<ZombieCharge>();
			if (charge != null)
			{
				charge.SetTarget(killer, KillerFocusTime);
			}
		}

		/// <summary>
		/// Puts the zombie's taunts (e.g., a fuling berserker's arms-out roar) on cooldown, as if it had just used them
		/// </summary>
		/// <remarks>
		/// A creature with a taunt uses it as an attack when it first targets an enemy, which a zombie does as soon as
		/// it's standing, so getting up would run straight into the taunt. It can still taunt once the cooldown is over
		/// </remarks>
		private void SkipTaunts()
		{
			Humanoid humanoid = character as Humanoid;
			if (humanoid == null)
			{
				return;
			}
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				string animation = item.m_shared.m_attack.m_attackAnimation;
				if (!string.IsNullOrEmpty(animation) && animation.IndexOf("taunt", System.StringComparison.OrdinalIgnoreCase) >= 0)
				{
					item.m_lastAttackTime = Time.time;
				}
			}
		}

		/// <summary>
		/// Hides or shows the zombie's renderers
		/// </summary>
		/// <param name="hide">True to hide them</param>
		private void SetHidden(bool hide)
		{
			if (!hide && !hidden)
			{
				return;
			}
			hidden = hide;
			foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
			{
				renderer.forceRenderingOff = hide;
			}
		}
	}
}
