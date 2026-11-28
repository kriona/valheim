using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A valkyrie's corpse - a ragdoll of her model that falls from the pose she died in, which Zombie Apocalypse can
	/// raise as a Fallen Valkyrie (see Link.RisesAs), and which drops her loot as it fades, the way a creature's corpse
	/// does
	/// </summary>
	/// <remarks>
	/// The ragdoll is built from the Asksvin's for its settings, with her model in place of the Asksvin's and a body on
	/// each of her main bones. It takes her pose, size and facing as she dies, and keeps the pose in its ZDO for the
	/// other clients' copies. Its joints are only added once it's posed, since a joint takes the pose it's made in as
	/// its rest pose
	/// </remarks>
	internal class ValkyrieCorpse : MonoBehaviour
	{
		/// <summary>
		/// The corpse's prefab name
		/// </summary>
		public const string PrefabName = "VikingValkyrie_ragdoll";

		/// <summary>
		/// The ragdoll the corpse's settings come from
		/// </summary>
		private const string SourceRagdoll = "Ragdoll_Asksvin";

		/// <summary>
		/// The ZDO key holding the pose she died in, every bone's turn in order
		/// </summary>
		private static readonly int PoseKey = "kri_vv_valkyrie_corpse_pose".GetStableHashCode();

		/// <summary>
		/// The bones that get a body, each joined to the nearest one above it - her pelvis, spine, head, legs, wings and
		/// tail
		/// </summary>
		private static readonly string[] BodyBones = { "pelvis", "spine1", "spine2", "head", "l_leg0", "l_leg1", "l_leg2", "r_leg0", "r_leg1", "r_leg2", "l_arm0", "l_arm1", "l_arm2", "r_arm0", "r_arm1", "r_arm2", "tail" };

		/// <summary>
		/// Each body's mass, and how thick each bone's capsule is against its length
		/// </summary>
		private const float BodyMass = 20f;
		private const float Thickness = 0.2f;

		/// <summary>
		/// How far each joint bends and twists, in degrees
		/// </summary>
		private const float JointSwing = 40f;
		private const float JointTwist = 30f;

		/// <summary>
		/// Her model in the corpse, its bodies, and for each the index of the body its joint hangs from, or -1
		/// </summary>
		public Transform m_model;
		public Rigidbody[] m_bodies = new Rigidbody[0];
		public int[] m_jointTo = new int[0];

		private ZNetView nview;
		private Transform[] bones;

		/// <summary>
		/// Its model's renderers' own materials, for showing her color (see ValkyrieColors.Apply)
		/// </summary>
		private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();

		/// <summary>
		/// Builds the corpse's prefab from the Asksvin's ragdoll and the intro valkyrie's model, turned to face forward
		/// the way hers is (see ValkyriePrefab), on the Asksvin's ragdoll's layer
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <param name="model">The intro valkyrie's model</param>
		/// <param name="holder">The inactive object it's kept under</param>
		/// <returns>The corpse's prefab, or null when the Asksvin's ragdoll is missing</returns>
		public static GameObject Build(ZNetScene scene, GameObject model, Transform holder)
		{
			GameObject source = scene.GetPrefab(SourceRagdoll);
			Rigidbody sourceBody = ((source != null) ? source.GetComponentInChildren<Rigidbody>(true) : null);
			if (source == null || source.GetComponent<Ragdoll>() == null || sourceBody == null)
			{
				Plugin.Log.LogWarning("Couldn't build the valkyrie's corpse - the " + SourceRagdoll + " prefab is missing");
				return(null);
			}
			int layer = sourceBody.gameObject.layer;
			GameObject corpse = Object.Instantiate(source, holder, false);
			corpse.name = PrefabName;
			for (int i = corpse.transform.childCount - 1; i >= 0; i--)
			{
				Object.DestroyImmediate(corpse.transform.GetChild(i).gameObject);
			}
			GameObject body = Object.Instantiate(model, corpse.transform, false);
			body.name = model.name;
			body.transform.localPosition = Vector3.zero;
			body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
			foreach (MonoBehaviour behaviour in body.GetComponentsInChildren<MonoBehaviour>(true))
			{
				Object.DestroyImmediate(behaviour);
			}
			foreach (Animator animator in body.GetComponentsInChildren<Animator>(true))
			{
				Object.DestroyImmediate(animator);
			}
			Ragdoll ragdoll = corpse.GetComponent<Ragdoll>();
			ragdoll.m_mainModel = body.GetComponentInChildren<SkinnedMeshRenderer>(true);
			ValkyrieCorpse built = corpse.AddComponent<ValkyrieCorpse>();
			built.m_model = body.transform;
			built.AddBodies(body.transform);
			ragdoll.m_lootSpawnJoint = ((built.m_bodies.Length > 0) ? built.m_bodies[0].gameObject : null);
			foreach (Transform part in corpse.GetComponentsInChildren<Transform>(true))
			{
				part.gameObject.layer = layer;
			}
			return(corpse);
		}

		/// <summary>
		/// Gives each of her main bones a body and a capsule along it, out to the next bone down, and notes the body each
		/// one's joint will hang from
		/// </summary>
		/// <param name="model">Her model in the corpse</param>
		private void AddBodies(Transform model)
		{
			List<Rigidbody> bodies = new List<Rigidbody>();
			List<int> jointTo = new List<int>();
			foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
			{
				if (!BodyBones.Contains(bone.name))
				{
					continue;
				}
				Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
				body.mass = BodyMass;
				CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
				Vector3 along = ((bone.childCount > 0) ? bone.InverseTransformPoint(bone.GetChild(0).position) : Vector3.up * 0.005f);
				if (along.sqrMagnitude < 0.0000001f)
				{
					along = Vector3.up * 0.005f;
				}
				capsule.direction = LongestAxis(along);
				capsule.center = along / 2f;
				capsule.height = along.magnitude;
				capsule.radius = Mathf.Max(0.001f, along.magnitude * Thickness);
				int parent = -1;
				for (Transform above = bone.parent; above != null && above != model; above = above.parent)
				{
					int index = bodies.FindIndex((Rigidbody found) => found.transform == above);
					if (index >= 0)
					{
						parent = index;
						break;
					}
				}
				bodies.Add(body);
				jointTo.Add(parent);
			}
			m_bodies = bodies.ToArray();
			m_jointTo = jointTo.ToArray();
		}

		/// <summary>
		/// The axis a vector runs along most - 0 for x, 1 for y and 2 for z, as a capsule's direction takes
		/// </summary>
		/// <param name="along">The vector</param>
		/// <returns>The axis</returns>
		internal static int LongestAxis(Vector3 along)
		{
			float x = Mathf.Abs(along.x);
			float y = Mathf.Abs(along.y);
			float z = Mathf.Abs(along.z);
			if (x >= y && x >= z)
			{
				return(0);
			}
			return(((y >= z) ? 1 : 2));
		}

		/// <summary>
		/// Finds the corpse's parts, and on another client's copy, takes the pose she died in and her color from its ZDO
		/// </summary>
		private void Awake()
		{
			nview = GetComponent<ZNetView>();
			bones = ((m_model != null) ? m_model.GetComponentsInChildren<Transform>(true) : new Transform[0]);
			if (nview == null || !nview.IsValid() || nview.IsOwner())
			{
				return;
			}
			byte[] pose = nview.GetZDO().GetByteArray(PoseKey);
			if (pose != null)
			{
				ApplyPose(new ZPackage(pose));
			}
			int color = ValkyrieColor.Get(nview.GetZDO());
			if (color >= 0)
			{
				ValkyrieColors.Apply(m_model, originals, color);
			}
		}

		/// <summary>
		/// Joins its bodies together once it's posed
		/// </summary>
		private void Start()
		{
			for (int i = 0; i < m_bodies.Length; i++)
			{
				if (m_bodies[i] == null || m_jointTo[i] < 0 || m_bodies[m_jointTo[i]] == null)
				{
					continue;
				}
				CharacterJoint joint = m_bodies[i].gameObject.AddComponent<CharacterJoint>();
				joint.connectedBody = m_bodies[m_jointTo[i]];
				joint.enablePreprocessing = false;
				SoftJointLimit low = joint.lowTwistLimit;
				low.limit = -JointTwist;
				joint.lowTwistLimit = low;
				SoftJointLimit high = joint.highTwistLimit;
				high.limit = JointTwist;
				joint.highTwistLimit = high;
				SoftJointLimit swing = joint.swing1Limit;
				swing.limit = JointSwing;
				joint.swing1Limit = swing;
				joint.swing2Limit = swing;
			}
		}

		/// <summary>
		/// Poses the corpse as a valkyrie is as she dies - her size, facing, model's lean and every bone - and gives it her
		/// color, keeping both in its ZDO for the other clients' copies
		/// </summary>
		/// <param name="valkyrie">The dying valkyrie</param>
		public void TakePose(Character valkyrie)
		{
			ValkyrieModel model = valkyrie.GetComponentInChildren<ValkyrieModel>();
			if (model == null || m_model == null)
			{
				return;
			}
			int color = ((valkyrie.m_nview != null && valkyrie.m_nview.IsValid()) ? ValkyrieColor.Get(valkyrie.m_nview.GetZDO()) : -1);
			if (color >= 0)
			{
				ValkyrieColors.Apply(m_model, originals, color);
				if (nview != null && nview.IsValid())
				{
					nview.GetZDO().Set(ValkyrieColor.ColorKey, color);
				}
			}
			ZPackage pose = new ZPackage();
			pose.Write(valkyrie.transform.localScale.x);
			pose.Write(valkyrie.transform.rotation);
			pose.Write(model.transform.localPosition);
			pose.Write(model.transform.localRotation);
			Transform[] living = model.GetComponentsInChildren<Transform>(true);
			Dictionary<string, Transform> byPath = new Dictionary<string, Transform>();
			foreach (Transform bone in living)
			{
				byPath[PathFrom(model.transform, bone)] = bone;
			}
			pose.Write(bones.Length);
			foreach (Transform bone in bones)
			{
				Quaternion turn = (byPath.TryGetValue(PathFrom(m_model, bone), out Transform from) ? from.localRotation : bone.localRotation);
				pose.Write(turn);
			}
			ApplyPose(new ZPackage(pose.GetArray()));
			if (nview != null && nview.IsValid())
			{
				nview.GetZDO().Set(PoseKey, pose.GetArray());
			}
		}

		/// <summary>
		/// Poses the corpse from a kept pose - its size, facing, model's place and lean, and every bone's turn
		/// </summary>
		/// <param name="pose">The pose, read from the start</param>
		private void ApplyPose(ZPackage pose)
		{
			float size = pose.ReadSingle();
			transform.localScale = new Vector3(size, size, size);
			transform.rotation = pose.ReadQuaternion();
			m_model.localPosition = pose.ReadVector3();
			m_model.localRotation = pose.ReadQuaternion();
			int count = pose.ReadInt();
			for (int i = 0; i < count && i < bones.Length; i++)
			{
				bones[i].localRotation = pose.ReadQuaternion();
			}
		}

		/// <summary>
		/// A bone's path below a root, the names joined with /
		/// </summary>
		/// <param name="root">The root</param>
		/// <param name="bone">The bone</param>
		/// <returns>The path, empty for the root itself</returns>
		private static string PathFrom(Transform root, Transform bone)
		{
			string path = "";
			for (Transform part = bone; part != null && part != root; part = part.parent)
			{
				path = ((path.Length > 0) ? part.name + "/" + path : part.name);
			}
			return(path);
		}
	}
}
