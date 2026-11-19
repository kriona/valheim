using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// A corpse that gets up as a zombie
	/// </summary>
	/// <remarks>
	/// The corpse takes on the zombie's tint while it lies there. When its time comes the owner spawns the zombie,
	/// hands it the corpse's loot and stores the zombie's ZDOID in the corpse's ZDO. Each client then hands its own
	/// copy of the corpse's pose to the zombie and hides the corpse. A corpse built on a different skeleton from its
	/// creature can't hand its pose over, so it gets up on its own instead - its bones turn from where they lie into
	/// the standing pose of the corpse's prefab, standing where the zombie stands - and hides once the zombie takes
	/// its place. The owner removes the corpse once the zombie is standing, and the game's own removal of the corpse
	/// is held off while this is on it
	/// </remarks>
	internal class RisingCorpse : MonoBehaviour
	{
		private Ragdoll ragdoll;
		private ZNetView nview;
		private ZombieLook look;
		private bool handedOver;

		/// <summary>
		/// Whether the corpse is getting up on its own
		/// </summary>
		private bool standing;

		private long standStart;
		private long standEnd;

		/// <summary>
		/// The corpse's top bone, which is moved and turned in world space while the others turn in local space
		/// </summary>
		private Transform hips;

		private Vector3 hipsFromPosition;
		private Quaternion hipsFromRotation;
		private Vector3 hipsToPosition;
		private Quaternion hipsToRotation;

		/// <summary>
		/// The bones below the hips, with their local pose where they lay and in the prefab's standing pose
		/// </summary>
		private Transform[] standBones;
		private Quaternion[] fromLocalRotations;
		private Quaternion[] toLocalRotations;

		/// <summary>
		/// Finds the corpse's parts and starts it on no tint
		/// </summary>
		private void Awake()
		{
			ragdoll = GetComponent<Ragdoll>();
			nview = GetComponent<ZNetView>();
			look = GetComponent<ZombieLook>();
			if (look == null)
			{
				look = gameObject.AddComponent<ZombieLook>();
			}
			look.Amount = 0f;
		}

		/// <summary>
		/// Tints the corpse, raises the zombie when it's time, hands the corpse's pose over and removes the corpse
		/// </summary>
		private void Update()
		{
			if (nview == null || !nview.IsValid() || ZNetScene.instance == null)
			{
				return;
			}
			ZDO zdo = nview.GetZDO();
			long now = Zombies.Now();
			long diedAt = zdo.GetLong(Zombies.DiedAtKey);
			long riseAt = zdo.GetLong(Zombies.RiseAtKey);
			if (!handedOver)
			{
				look.Amount = ((riseAt > diedAt) ? Mathf.Clamp01((float)(now - diedAt) / (riseAt - diedAt)) : 1f);
			}
			ZDOID zombieId = zdo.GetZDOID(Zombies.ZombieIdKey);
			if (zombieId.IsNone())
			{
				if (nview.IsOwner() && now >= riseAt)
				{
					Rise(zdo, now);
				}
				return;
			}
			if (!handedOver)
			{
				GameObject zombie = ZNetScene.instance.FindInstance(zombieId);
				if (zombie != null)
				{
					RiseBlend blend = zombie.GetComponent<RiseBlend>();
					if (blend == null || blend.Begin(transform) || !StartStanding(zombie))
					{
						Hide();
					}
					handedOver = true;
				}
			}
			if (standing && now >= standEnd)
			{
				standing = false;
				Hide();
			}
			if (nview.IsOwner() && now >= zdo.GetLong(Zombies.RemoveAtKey))
			{
				ZNetScene.instance.Destroy(gameObject);
			}
		}

		/// <summary>
		/// Spawns the zombie where the corpse lies, handing it the corpse's loot, or drops the loot and removes the
		/// corpse when the zombie can't be spawned
		/// </summary>
		/// <param name="zdo">The corpse's ZDO, owned by this client</param>
		/// <param name="now">The current ZNet time in ticks</param>
		private void Rise(ZDO zdo, long now)
		{
			Vector3 middle = ragdoll.GetAverageBodyPosition();
			PendingRise rise = new PendingRise();
			rise.PrefabHash = zdo.GetInt(Zombies.RisePrefabKey);
			rise.Level = zdo.GetInt(ZDOVars.s_level, 1);
			rise.Loot = Loot.Read(zdo, Loot.CorpseKeys);
			rise.LootCheated = zdo.IsCheated();
			rise.KillerId = zdo.GetZDOID(Zombies.KillerIdKey);
			long riseEnd = now + Zombies.Ticks(Plugin.RiseDuration.Value);
			Quaternion facing = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
			Character zombie = Zombies.Raise(rise, Zombies.GroundUnder(middle), facing, now, riseEnd);
			if (zombie == null)
			{
				ragdoll.SpawnLoot((ragdoll.m_lootSpawnJoint != null) ? ragdoll.m_lootSpawnJoint.transform.position : middle);
				ZombieViking.DropGrave(zdo, middle, facing);
				ZNetScene.instance.Destroy(gameObject);
				return;
			}
			// A player's corpse holds the things their viking carries
			ZombieViking.HandOver(zdo, zombie);
			Loot.Clear(zdo, Loot.CorpseKeys);
			zdo.Set(Zombies.ZombieIdKey, zombie.GetZDOID());
			// Kept a moment past the blend so a client that hears about the zombie late still has the pose
			zdo.Set(Zombies.RemoveAtKey, riseEnd + Zombies.Ticks(1f));
		}

		/// <summary>
		/// Turns the corpse's bones toward its prefab's standing pose while it gets up on its own
		/// </summary>
		private void LateUpdate()
		{
			if (!standing)
			{
				return;
			}
			long now = Zombies.Now();
			float t = ((standEnd > standStart) ? Mathf.Clamp01((float)(now - standStart) / (standEnd - standStart)) : 1f);
			t = t * t * (3f - 2f * t);
			hips.position = Vector3.Lerp(hipsFromPosition, hipsToPosition, t);
			hips.rotation = Quaternion.Slerp(hipsFromRotation, hipsToRotation, t);
			for (int i = 0; i < standBones.Length; i++)
			{
				standBones[i].localRotation = Quaternion.Slerp(fromLocalRotations[i], toLocalRotations[i], t);
			}
		}

		/// <summary>
		/// Starts the corpse getting up on its own, ending in its prefab's standing pose where the zombie stands
		/// </summary>
		/// <param name="zombie">The zombie rising from the corpse</param>
		/// <returns>True when it started, false when the corpse has no prefab or bones to stand up with</returns>
		private bool StartStanding(GameObject zombie)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(Utils.GetPrefabName(gameObject));
			Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
			if (prefab == null || bodies.Length == 0)
			{
				return(false);
			}
			// GetComponentsInChildren lists parents first, so the first body is the top one
			hips = bodies[0].transform;
			Transform prefabRoot = prefab.transform;
			Transform prefabHips = prefabRoot.Find(PathTo(hips));
			if (prefabHips == null)
			{
				return(false);
			}
			StopPhysics();
			ZDO zombieZdo = zombie.GetComponent<ZNetView>().GetZDO();
			standStart = zombieZdo.GetLong(Zombies.RiseStartKey);
			standEnd = zombieZdo.GetLong(Zombies.RiseEndKey);
			hipsFromPosition = hips.position;
			hipsFromRotation = hips.rotation;
			// Where the hips sit in the standing prefab, placed on the zombie at the corpse's own scale
			Quaternion facing = zombie.transform.rotation;
			Vector3 restOffset = Vector3.Scale(prefabRoot.InverseTransformPoint(prefabHips.position), transform.localScale);
			hipsToPosition = zombie.transform.position + facing * restOffset;
			hipsToRotation = facing * (Quaternion.Inverse(prefabRoot.rotation) * prefabHips.rotation);
			List<Transform> found = new List<Transform>();
			List<Quaternion> fromRotations = new List<Quaternion>();
			List<Quaternion> toRotations = new List<Quaternion>();
			foreach (Transform bone in hips.GetComponentsInChildren<Transform>(true))
			{
				if (bone == hips)
				{
					continue;
				}
				// Bones added to the corpse after it spawned, like equipment, aren't in the prefab and stay as they are
				Transform prefabBone = prefabRoot.Find(PathTo(bone));
				if (prefabBone != null)
				{
					found.Add(bone);
					fromRotations.Add(bone.localRotation);
					toRotations.Add(prefabBone.localRotation);
				}
			}
			standBones = found.ToArray();
			fromLocalRotations = fromRotations.ToArray();
			toLocalRotations = toRotations.ToArray();
			standing = true;
			// The zombie stays hidden until the corpse is up, so the corpse carries the flies while it stands
			if (GetComponent<ZombieFlies>() == null)
			{
				gameObject.AddComponent<ZombieFlies>().Creature = zombie.GetComponent<Character>();
			}
			return(true);
		}

		/// <summary>
		/// Builds a bone's path from the corpse's root, the same as Transform.Find takes
		/// </summary>
		/// <param name="bone">A bone of the corpse</param>
		/// <returns>The names from the root's child down to the bone, joined with /</returns>
		private string PathTo(Transform bone)
		{
			string path = bone.name;
			for (Transform parent = bone.parent; parent != null && parent != transform; parent = parent.parent)
			{
				path = parent.name + "/" + path;
			}
			return(path);
		}

		/// <summary>
		/// Stops the corpse's bodies moving and colliding, so its bones only move where they're put
		/// </summary>
		private void StopPhysics()
		{
			foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
			{
				body.isKinematic = true;
			}
			foreach (Collider collider in GetComponentsInChildren<Collider>(true))
			{
				collider.enabled = false;
			}
		}

		/// <summary>
		/// Hides the corpse and stops it moving, leaving it in place for the owner to remove
		/// </summary>
		private void Hide()
		{
			foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
			{
				renderer.forceRenderingOff = true;
			}
			StopPhysics();
		}
	}
}
