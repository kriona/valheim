using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Shows a valkyrie's attacks on her model - the Fallen Valkyrie's animator moves a hidden copy of its skeleton
	/// with her, the one the game drives, and while it plays an attack its pose is copied onto her bones
	/// </summary>
	/// <remarks>
	/// The Fallen Valkyrie's model is a reworked copy of hers, so the skeletons have the same bones by the same names
	/// but a few - its hip is her pelvis and its tail0 to tail2 her tail, tail.001 and tail.002 - and the same rest
	/// pose, but not always the same roll along each bone. So each bone is turned by how far the hidden one turned from
	/// its own rest pose, rather than copied as it is, and her pelvis is moved by where its hip moved, scaled to her
	/// height. The rest poses are each skeleton's bind pose, measured as her prefab's built, and both are measured from
	/// each skeleton's armature - the Fallen Valkyrie's clips turn its armature around from its bind pose to face the
	/// other way, which would turn her around with it
	/// </remarks>
	internal class ValkyrieAttackPose : MonoBehaviour
	{
		/// <summary>
		/// The name of the bone each skeleton's bones hang from, which the rest poses are measured from
		/// </summary>
		private const string ArmatureName = "Armature";

		/// <summary>
		/// The hidden skeleton's armature and hers, the hidden skeleton's bones, and hers they're copied onto, parents
		/// before children - the first of each is the hip and her pelvis
		/// </summary>
		public Transform m_rigFrame;
		public Transform m_ownFrame;
		public Transform[] m_from = new Transform[0];
		public Transform[] m_to = new Transform[0];

		/// <summary>
		/// Each bone's rest turn in its own skeleton's armature's space
		/// </summary>
		public Quaternion[] m_fromRest = new Quaternion[0];
		public Quaternion[] m_toRest = new Quaternion[0];

		/// <summary>
		/// Her pelvis's rest distance from her armature over the hidden skeleton's hip's from its own
		/// </summary>
		public float m_heightRatio = 1f;

		/// <summary>
		/// The way the hidden skeleton faces in its bind pose, in its armature's space
		/// </summary>
		public Vector3 m_rigFacing = Vector3.back;

		/// <summary>
		/// The hidden skeleton's animator, whose clip tells her claws apart from her other attacks, and which part of
		/// her each of her paired bones is in (see PartOf)
		/// </summary>
		public Animator m_rigAnimator;
		public int[] m_parts = new int[0];

		/// <summary>
		/// For each of her paired bones, the index of its parent among them, or -1 when its parent isn't one
		/// </summary>
		public int[] m_parents = new int[0];

		/// <summary>
		/// Where the hidden skeleton's pose turns each of her paired bones, reused each frame
		/// </summary>
		private Quaternion[] turnedBuffer = new Quaternion[0];

		/// <summary>
		/// The parts of her a bone can be in - her wings, and anything else not named, take an attack's pose in full
		/// </summary>
		internal const int WingsPart = 0;
		internal const int BodyPart = 1;
		internal const int LegsPart = 2;

		/// <summary>
		/// The Fallen Valkyrie's claws clip, which it plays hovering - rearing back with its legs swung forward, then
		/// raking down - so on her, standing, her legs keep standing and her body and neck lean only part of the way
		/// </summary>
		private const string ClawsClip = "Attack Claws";

		/// <summary>
		/// How much of the claws' lean her body and neck take
		/// </summary>
		internal const float ClawsBodyShare = 0.5f;

		/// <summary>
		/// How far the hidden skeleton's pose is turned around to face her way, in degrees (see Calibrate)
		/// </summary>
		private float facingFix;

		/// <summary>
		/// Which part of her a bone is in - her pelvis, spine, neck, head and tail are her body, her legs, feet and toes
		/// her legs, and the rest her wings
		/// </summary>
		/// <param name="name">Her bone's name</param>
		/// <returns>WingsPart, BodyPart or LegsPart</returns>
		internal static int PartOf(string name)
		{
			if (name == "pelvis" || name.StartsWith("spine") || name == "neck" || name == "head" || name.StartsWith("tail"))
			{
				return(BodyPart);
			}
			if (name.StartsWith("l_leg") || name.StartsWith("r_leg") || name.StartsWith("l_foot") || name.StartsWith("r_foot") || name.StartsWith("l_toe") || name.StartsWith("r_toe"))
			{
				return(LegsPart);
			}
			return(WingsPart);
		}

		/// <summary>
		/// How much of an attack's pose a part of her takes, by how much she takes of the attack's pose overall
		/// </summary>
		/// <param name="part">The part, from PartOf</param>
		/// <param name="share">How much of the attack's pose she takes, from 0 to 1</param>
		/// <param name="claws">Whether the attack is her claws, which she takes only part of</param>
		/// <returns>The part's share, from 0 to 1</returns>
		internal static float PartShare(int part, float share, bool claws)
		{
			if (!claws || part == WingsPart)
			{
				return(share);
			}
			return(((part == BodyPart) ? share * ClawsBodyShare : 0f));
		}

		/// <summary>
		/// Checks whether the hidden skeleton plays the claws clip, or is blending into it - every client's copy of it
		/// plays the same clips
		/// </summary>
		/// <returns>True while it does</returns>
		private bool PlaysClaws()
		{
			if (m_rigAnimator == null)
			{
				return(false);
			}
			foreach (AnimatorClipInfo info in m_rigAnimator.GetCurrentAnimatorClipInfo(0))
			{
				if (info.clip != null && info.clip.name == ClawsClip)
				{
					return(true);
				}
			}
			foreach (AnimatorClipInfo info in m_rigAnimator.GetNextAnimatorClipInfo(0))
			{
				if (info.clip != null && info.clip.name == ClawsClip)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// The hidden skeleton's bones named differently from hers, by her name
		/// </summary>
		private static readonly Dictionary<string, string> Renamed = new Dictionary<string, string>
		{
			{ "pelvis", "hip" },
			{ "tail", "tail0" },
			{ "tail.001", "tail1" },
			{ "tail.002", "tail2" }
		};

		/// <summary>
		/// The name a bone of hers has in the hidden skeleton
		/// </summary>
		/// <param name="name">Her bone's name</param>
		/// <returns>The hidden skeleton's name for it</returns>
		internal static string RigName(string name)
		{
			return((Renamed.TryGetValue(name, out string renamed) ? renamed : name));
		}

		/// <summary>
		/// Pairs her bones with the hidden skeleton's and notes their rest poses, for each bone both skeletons skin their
		/// bodies with, parents before children
		/// </summary>
		/// <param name="model">Her model's root</param>
		/// <param name="skin">Her body's renderer</param>
		/// <param name="rigRoot">The hidden skeleton's root</param>
		/// <param name="rigSkin">The renderer that skinned the Fallen Valkyrie's body to the hidden skeleton</param>
		/// <returns>True when both armatures were found and her pelvis was paired, which the rest goes by</returns>
		public bool Pair(Transform model, SkinnedMeshRenderer skin, Transform rigRoot, SkinnedMeshRenderer rigSkin)
		{
			m_ownFrame = Utils.FindChild(model, ArmatureName);
			m_rigFrame = Utils.FindChild(rigRoot, ArmatureName);
			if (m_ownFrame == null || m_rigFrame == null)
			{
				return(false);
			}
			Dictionary<string, int> rigBones = new Dictionary<string, int>();
			for (int i = 0; i < rigSkin.bones.Length; i++)
			{
				if (rigSkin.bones[i] != null)
				{
					rigBones[rigSkin.bones[i].name] = i;
				}
			}
			Dictionary<Transform, int> ownBones = new Dictionary<Transform, int>();
			for (int i = 0; i < skin.bones.Length; i++)
			{
				if (skin.bones[i] != null)
				{
					ownBones[skin.bones[i]] = i;
				}
			}
			List<Transform> from = new List<Transform>();
			List<Transform> to = new List<Transform>();
			List<Quaternion> fromRest = new List<Quaternion>();
			List<Quaternion> toRest = new List<Quaternion>();
			Vector3 pelvisRest = Vector3.zero;
			Vector3 hipRest = Vector3.zero;
			foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
			{
				if (!ownBones.TryGetValue(bone, out int own) || !rigBones.TryGetValue(RigName(bone.name), out int rig))
				{
					continue;
				}
				Matrix4x4 ownRest = m_ownFrame.worldToLocalMatrix * skin.transform.localToWorldMatrix * skin.sharedMesh.bindposes[own].inverse;
				Matrix4x4 rigRest = m_rigFrame.worldToLocalMatrix * rigSkin.transform.localToWorldMatrix * rigSkin.sharedMesh.bindposes[rig].inverse;
				if (bone.name == "pelvis")
				{
					from.Insert(0, rigSkin.bones[rig]);
					to.Insert(0, bone);
					fromRest.Insert(0, rigRest.rotation);
					toRest.Insert(0, ownRest.rotation);
					pelvisRest = ownRest.GetColumn(3);
					hipRest = rigRest.GetColumn(3);
					continue;
				}
				from.Add(rigSkin.bones[rig]);
				to.Add(bone);
				fromRest.Add(rigRest.rotation);
				toRest.Add(ownRest.rotation);
			}
			m_from = from.ToArray();
			m_to = to.ToArray();
			m_parts = to.Select((Transform bone) => PartOf(bone.name)).ToArray();
			m_parents = to.Select((Transform bone) => to.IndexOf(bone.parent)).ToArray();
			m_rigAnimator = rigRoot.GetComponent<Animator>();
			m_fromRest = fromRest.ToArray();
			m_toRest = toRest.ToArray();
			m_heightRatio = ((hipRest.magnitude > 0.000001f) ? pelvisRest.magnitude / hipRest.magnitude : 1f);
			// The Fallen Valkyrie's model faces back along its own forward in its bind pose, as hers does
			m_rigFacing = m_rigFrame.InverseTransformDirection(rigRoot.TransformDirection(Vector3.back)).normalized;
			return(m_to.Length > 0 && m_to[0].name == "pelvis");
		}

		/// <summary>
		/// Measures which way the hidden skeleton's pose would turn her, while she isn't attacking, and notes the turn
		/// to the nearest quarter that brings it back to face the way she faces - Unity can leave the turn the Fallen
		/// Valkyrie's clips give its armature off the hidden skeleton, which would have her face backwards, and this
		/// takes it out either way
		/// </summary>
		/// <param name="forward">The way she faces</param>
		public void Calibrate(Vector3 forward)
		{
			if (m_rigFrame == null || m_ownFrame == null || m_from.Length == 0 || m_from[0] == null)
			{
				return;
			}
			Quaternion turn = Quaternion.Inverse(m_rigFrame.rotation) * m_from[0].rotation * Quaternion.Inverse(m_fromRest[0]);
			Vector3 facing = m_ownFrame.rotation * (turn * m_rigFacing);
			facing.y = 0f;
			forward.y = 0f;
			if (facing.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f)
			{
				return;
			}
			float fix = ValkyrieMotion.NearestQuarter(Vector3.SignedAngle(facing, forward, Vector3.up));
			if (fix != facingFix)
			{
				facingFix = fix;
				Plugin.Log.LogInfo("Valkyrie: her attacks' poses are turned " + fix + " degrees to face her way");
			}
		}

		/// <summary>
		/// Turns her bones toward the hidden skeleton's pose and moves her pelvis toward where its hip is, by a share -
		/// each bone turned from its rest pose, in her armature's space, as far as the hidden one turned from its own
		/// in its armature's space, then the whole pose turned to face her way (see Calibrate). Her claws she takes only
		/// part of, with her legs standing and her pelvis where it is (see PartShare)
		/// </summary>
		/// <param name="share">How much of its pose she takes, from 0 to 1</param>
		public void Apply(float share)
		{
			if (m_rigFrame == null || m_ownFrame == null || m_to.Length == 0)
			{
				return;
			}
			bool claws = PlaysClaws();
			Quaternion fix = Quaternion.AngleAxis(facingFix, Vector3.up);
			Quaternion ownToRig = m_ownFrame.rotation * Quaternion.Inverse(m_rigFrame.rotation);
			Vector3 pivot = transform.position;
			if (turnedBuffer.Length != m_to.Length)
			{
				turnedBuffer = new Quaternion[m_to.Length];
			}
			for (int i = 0; i < m_to.Length; i++)
			{
				if (m_from[i] != null)
				{
					turnedBuffer[i] = fix * ownToRig * m_from[i].rotation * Quaternion.Inverse(m_fromRest[i]) * m_toRest[i];
				}
			}
			for (int i = 0; i < m_to.Length; i++)
			{
				if (m_from[i] == null || m_to[i] == null)
				{
					continue;
				}
				float partShare = PartShare(((i < m_parts.Length) ? m_parts[i] : WingsPart), share, claws);
				if (partShare <= 0f)
				{
					continue;
				}
				// Blended against its parent, so a chain of bones each taking part of a turn adds up to that part
				int parent = ((i < m_parents.Length) ? m_parents[i] : -1);
				Quaternion parentTurned = ((parent >= 0) ? turnedBuffer[parent] : m_to[i].parent.rotation);
				Quaternion local = Quaternion.Inverse(parentTurned) * turnedBuffer[i];
				m_to[i].localRotation = Quaternion.Slerp(m_to[i].localRotation, local, partShare);
				if (i == 0 && !claws)
				{
					Vector3 hip = m_ownFrame.TransformPoint(m_rigFrame.InverseTransformPoint(m_from[0].position) * m_heightRatio);
					hip = pivot + fix * (hip - pivot);
					m_to[0].position = Vector3.Lerp(m_to[0].position, hip, share);
				}
			}
		}
	}
}
