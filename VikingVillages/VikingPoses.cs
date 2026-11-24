using System.Collections;
using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A living viking's everyday body language - a morning stretch, the head moving as it speaks or listens, the
	/// player's conversational emotes and emotes of grief, sitting on the floor, lying in bed, sitting in a chair, a roar with a shout, a
	/// hammer swing and the player's short reach to use something
	/// </summary>
	/// <remarks>
	/// The viking's owner, which runs its AI, starts each of them through the static calls, which keep the state in the
	/// viking's ZDO so every client plays it, including one that loads the viking later. The emotes are driven the way
	/// the player's are - each client watches an emote counter in the ZDO and sets the emote's trigger or bool on its own
	/// animator - while the bed and chair poses are the animator bools the player's ZSyncAnimation already syncs. The
	/// stretch and the talking head are bent onto the bones on every client once the animator has run, and put back at
	/// the end of the frame like ZombieLurch's. A client that becomes the viking's owner starts it standing, since its AI
	/// starts afresh
	/// </remarks>
	[DefaultExecutionOrder(10000)]
	internal class VikingPoses : MonoBehaviour
	{
		/// <summary>
		/// How the viking's head moves in a conversation
		/// </summary>
		public enum TalkMode
		{
			None,
			Speaking,
			Listening
		}

		/// <summary>
		/// How long the stretch lasts once the viking is standing, in seconds
		/// </summary>
		public const float StretchDuration = 2.4f;

		/// <summary>
		/// On a living viking, when its stretch started in ZNet time ticks, or 0
		/// </summary>
		private static readonly int StretchKey = "kriona.VikingVillages.vikingStretch".GetStableHashCode();

		/// <summary>
		/// On a living viking, its TalkMode
		/// </summary>
		private static readonly int TalkKey = "kriona.VikingVillages.vikingTalk".GetStableHashCode();

		/// <summary>
		/// On a living viking, its emote - its name without "emote_", or "" for none - whether it plays once, when it
		/// started in ZNet time ticks, and a counter that goes up each time it changes
		/// </summary>
		private static readonly int EmoteKey = "kriona.VikingVillages.vikingEmote".GetStableHashCode();
		private static readonly int EmoteOneshotKey = "kriona.VikingVillages.vikingEmoteOneshot".GetStableHashCode();
		private static readonly int EmoteTimeKey = "kriona.VikingVillages.vikingEmoteTime".GetStableHashCode();
		private static readonly int EmoteIdKey = "kriona.VikingVillages.vikingEmoteId".GetStableHashCode();

		/// <summary>
		/// What ZSyncAnimation adds to an animator parameter's hash for the ZDO key it syncs the parameter in
		/// </summary>
		private const int AnimationSalt = 438569;

		/// <summary>
		/// The player's animator bools for lying in a bed and sitting in a chair, which the player's ZSyncAnimation syncs
		/// </summary>
		private const string BedBool = "attach_bed";
		private const string ChairBool = "attach_chair";

		/// <summary>
		/// The player's animator int for working at a crafting station, set to the station's animation while crafting
		/// </summary>
		public const string CraftingInt = "crafting";
		private static readonly int BedHash = ZSyncAnimation.GetHash(BedBool);
		private static readonly int ChairHash = ZSyncAnimation.GetHash(ChairBool);

		/// <summary>
		/// The player's emotes the viking uses - the looping floor sit, the roar, the wave and the conversational gestures - and
		/// the trigger that ends an emote
		/// </summary>
		private const string SitEmote = "sit";
		private const string RoarEmote = "roar";
		private const string WaveEmote = "wave";
		private static readonly string[] Gestures = new string[] { "point", "shrug", "laugh", "thumbsup", "nonono", "comehere" };

		/// <summary>
		/// The player's emotes a viking mourns with - kneeling, crying, despairing and bowing its head
		/// </summary>
		private static readonly string[] Griefs = new string[] { "kneel", "cry", "despair", "bow" };
		private const string EmotePrefix = "emote_";
		private const string EmoteStop = "emote_stop";

		/// <summary>
		/// The player's trigger for reaching out to pick up or use something
		/// </summary>
		private const string InteractTrigger = "interact";

		/// <summary>
		/// The animator float for how fast the viking walks forward, which ZSyncAnimation syncs
		/// </summary>
		private static readonly int ForwardSpeedHash = ZSyncAnimation.GetHash("forward_speed");

		/// <summary>
		/// The item whose attack animation is the swing the player builds and repairs with, and that swing's trigger
		/// when the item can't be read
		/// </summary>
		private const string HammerPrefab = "Hammer";
		private const string HammerSwingFallback = "swing_hammer";

		/// <summary>
		/// The fuling berserker's shout, a man's scream played deep, pitched up to a viking's war cry, and the dvergr's
		/// battle cry when it isn't loaded
		/// </summary>
		private const string ShoutSound = "sfx_goblinbrute_shout";
		private const string FallbackShoutSound = "sfx_dverger_vo_attack";
		private const float ShoutPitchMin = 0.75f;
		private const float ShoutPitchMax = 0.85f;

		/// <summary>
		/// Seconds into the roar emote the shout starts
		/// </summary>
		private const float ShoutDelay = 0.2f;

		/// <summary>
		/// Seconds a shout is kept before it's removed, since it isn't networked
		/// </summary>
		private const float ShoutLifetime = 6f;

		/// <summary>
		/// How late a client can see a one-shot emote or a stretch start and still play it, in seconds
		/// </summary>
		internal const float LateLimit = 3f;

		/// <summary>
		/// How long a stretch waits for the viking to get up from a bed, chair or the floor before it's dropped, in
		/// seconds, and how high its hips have to be above its lower foot for it to count as standing, in meters
		/// </summary>
		private const float StretchWaitMax = 2f;
		internal const float StandingHipHeight = 0.7f;

		/// <summary>
		/// Seconds the stretch takes to ease in and out, and to fade away when it's cut short
		/// </summary>
		internal const float StretchIn = 0.7f;
		internal const float StretchOut = 0.8f;
		private const float StretchCancelTime = 0.3f;

		/// <summary>
		/// How fast the viking can walk forward before a stretch fades away, in meters a second
		/// </summary>
		private const float StretchMaxSpeed = 0.5f;

		/// <summary>
		/// Degrees the back arches and the head tilts back at the height of the stretch
		/// </summary>
		private const float StretchArchAngle = 16f;
		private const float StretchHeadAngle = 22f;

		/// <summary>
		/// How the arms reach overhead - how far out to the side and back the upper arms point and how far in the
		/// forearms lean, against straight up, and how far the shoulders lift
		/// </summary>
		private const float StretchArmOutward = 0.2f;
		private const float StretchArmBack = 0.1f;
		private const float StretchForearmInward = 0.08f;
		private const float StretchShrug = 0.3f;

		/// <summary>
		/// How much of a turn of the head the neck takes
		/// </summary>
		private const float NeckShare = 0.35f;

		/// <summary>
		/// Seconds the talking head takes to fade in and out
		/// </summary>
		private const float TalkFadeTime = 0.4f;

		/// <summary>
		/// The speaker's small, steady head motion - degrees of nod, turn and tilt, and how fast it wanders
		/// </summary>
		private const float SpeakNoisePitch = 2.5f;
		private const float SpeakNoiseYaw = 5f;
		private const float SpeakNoiseRoll = 3f;
		private const float SpeakNoiseSpeed = 0.8f;

		/// <summary>
		/// The speaker's emphasis - seconds between them and how long each takes, how likely each is to be a nod rather
		/// than a tilt, and the degrees of each
		/// </summary>
		private const float SpeakBeatIntervalMin = 0.7f;
		private const float SpeakBeatIntervalMax = 2.2f;
		private const float SpeakBeatTimeMin = 0.35f;
		private const float SpeakBeatTimeMax = 0.6f;
		private const float SpeakNodChance = 0.6f;
		private const float SpeakNodMin = 5f;
		private const float SpeakNodMax = 9f;
		private const float SpeakTiltMin = 4f;
		private const float SpeakTiltMax = 8f;

		/// <summary>
		/// The listener's head motion - degrees of drift and how fast it wanders, and the degrees of the tilt it holds
		/// </summary>
		private const float ListenNoisePitch = 1.5f;
		private const float ListenNoiseYaw = 2.5f;
		private const float ListenNoiseSpeed = 0.4f;
		private const float ListenTiltMin = 2f;
		private const float ListenTiltMax = 5f;

		/// <summary>
		/// The listener's slow nods - seconds between them and how long each takes, how likely one is to be a double
		/// nod, and its degrees
		/// </summary>
		private const float ListenNodIntervalMin = 2.5f;
		private const float ListenNodIntervalMax = 6f;
		private const float ListenNodTimeMin = 0.9f;
		private const float ListenNodTimeMax = 1.4f;
		private const float ListenDoubleChance = 0.3f;
		private const float ListenNodMin = 5f;
		private const float ListenNodMax = 8f;

		/// <summary>
		/// The hammer's swing, once it has been read
		/// </summary>
		private static string swingAnimation;

		private static bool loggedShoutFallback;

		private Humanoid humanoid;
		private ZNetView nview;
		private Animator animator;

		/// <summary>
		/// Whether this client draws nothing, being a dedicated server
		/// </summary>
		private bool dedicated;

		/// <summary>
		/// Whether this client has cleared what an earlier owner left in the ZDO since it became the owner
		/// </summary>
		private bool claimed;

		/// <summary>
		/// The bones from the hips up - null where the skeleton has none, with the arm bones left then right
		/// </summary>
		private Transform hips;
		private Transform leftFoot;
		private Transform rightFoot;
		private Transform[] spine;
		private Transform neck;
		private Transform head;
		private Transform[] shoulders = new Transform[2];
		private Transform[] arms = new Transform[2];
		private Transform[] foreArms = new Transform[2];
		private Transform[] hands = new Transform[2];

		/// <summary>
		/// Every bone the poses turn, and its local rotation before they turned it this frame
		/// </summary>
		private Transform[] touched;
		private Quaternion[] savedRotations;

		/// <summary>
		/// Whether the bones were turned this frame and need putting back
		/// </summary>
		private bool applied;

		/// <summary>
		/// The emote counter this client has played up to, whether it has read it yet, and the looping emote it has on
		/// </summary>
		private int emoteId;
		private bool emoteSeen;
		private string emoteState = "";

		/// <summary>
		/// The stretch start this client has seen, whether it's waiting for the viking to stand, since when, and when it
		/// started on this client, or a negative number while it isn't stretching
		/// </summary>
		private long stretchSeen;
		private bool stretchPending;
		private float stretchWaitStart;
		private float stretchStart = -1f;

		/// <summary>
		/// How much of the stretch is shown, from 0 to 1
		/// </summary>
		private float stretchWeight;

		/// <summary>
		/// The talk mode in the ZDO, the last one other than None, which the head keeps moving with as it fades out,
		/// and how much of it is shown, from 0 to 1
		/// </summary>
		private TalkMode talkMode;
		private TalkMode talkShape;
		private float talkWeight;

		/// <summary>
		/// The nod or tilt the head is making - when it started, how long it takes, its degrees of nod and tilt, whether
		/// it's a double nod - and when the next one starts
		/// </summary>
		private float beatStart;
		private float beatTime = 1f;
		private float beatPitch;
		private float beatRoll;
		private bool beatDouble;
		private float nextBeat;

		/// <summary>
		/// The degrees the listener holds its head tilted
		/// </summary>
		private float listenTilt;

		/// <summary>
		/// Where this viking's head motion starts in the noise, so vikings side by side don't move together
		/// </summary>
		private float noiseSeed;

		/// <summary>
		/// Gets a living viking's prefab ready for the poses, making sure its ZSyncAnimation syncs the bed and chair
		/// bools and the crafting int, and adds them
		/// </summary>
		/// <param name="prefab">The living viking's prefab, before its parts wake</param>
		public static void AddTo(GameObject prefab)
		{
			ZSyncAnimation sync = prefab.GetComponent<ZSyncAnimation>();
			if (sync != null)
			{
				foreach (string name in new string[] { BedBool, ChairBool })
				{
					if (!sync.m_syncBools.Contains(name))
					{
						sync.m_syncBools.Add(name);
					}
				}
				// A viking crafting at a workbench works at it the way a player does, seen by every client
				if (!sync.m_syncInts.Contains(CraftingInt))
				{
					sync.m_syncInts.Add(CraftingInt);
				}
			}
			if (prefab.GetComponent<VikingPoses>() == null)
			{
				prefab.AddComponent<VikingPoses>();
			}
		}

		/// <summary>
		/// Starts a morning stretch - the arms reach overhead, the back arches and the head tilts back - once the viking
		/// is standing
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		public static void Stretch(Character viking)
		{
			VikingPoses poses = Owned(viking);
			if (poses == null || poses.IsPosed())
			{
				return;
			}
			poses.nview.GetZDO().Set(StretchKey, NetTime.Now());
		}

		/// <summary>
		/// Sets how the viking's head moves in a conversation, until it's set back to None
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		/// <param name="mode">Speaking, listening or neither</param>
		public static void SetTalk(Character viking, TalkMode mode)
		{
			VikingPoses poses = Owned(viking);
			if (poses == null)
			{
				return;
			}
			ZDO zdo = poses.nview.GetZDO();
			if (zdo.GetInt(TalkKey) != (int)mode)
			{
				zdo.Set(TalkKey, (int)mode);
			}
		}

		/// <summary>
		/// Plays one of the player's conversational emotes at random - point, shrug, laugh, thumbs up, no no no or come
		/// here - unless the viking is sitting, lying or in a chair
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		public static void Gesture(Character viking)
		{
			VikingPoses poses = Owned(viking);
			if (poses == null || poses.IsPosed())
			{
				return;
			}
			poses.EndStretch();
			poses.SetEmote(Gestures[Random.Range(0, Gestures.Length)], true);
		}

		/// <summary>
		/// Plays one of the player's emotes of grief at random - kneel, cry, despair or bow - unless the viking is
		/// sitting, lying or in a chair
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		public static void Grieve(Character viking)
		{
			VikingPoses poses = Owned(viking);
			if (poses == null || poses.IsPosed())
			{
				return;
			}
			poses.EndStretch();
			poses.SetEmote(Griefs[Random.Range(0, Griefs.Length)], true);
		}

		/// <summary>
		/// Checks whether a viking lies on a bed, or sits on a chair or the floor, as every client sees it
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <param name="lying">Set to whether it lies on a bed</param>
		/// <returns>True when it lies or sits</returns>
		public static bool SettledDown(Character viking, out bool lying)
		{
			lying = false;
			ZDO zdo = ((viking != null && viking.m_nview != null && viking.m_nview.IsValid()) ? viking.m_nview.GetZDO() : null);
			if (zdo == null)
			{
				return(false);
			}
			lying = (zdo.GetInt(AnimationSalt + BedHash) != 0);
			return(lying || zdo.GetInt(AnimationSalt + ChairHash) != 0 || zdo.GetString(EmoteKey) == SitEmote);
		}

		/// <summary>
		/// Sits the viking on the floor with the player's sit emote, or stands it back up
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		/// <param name="sitting">Whether it sits</param>
		public static void SetSitting(Character viking, bool sitting)
		{
			VikingPoses poses = Owned(viking);
			if (poses == null)
			{
				return;
			}
			if (!sitting)
			{
				poses.EndSitting();
				return;
			}
			poses.EndStretch();
			poses.SetPoseBool(BedHash, false);
			poses.SetPoseBool(ChairHash, false);
			if (!poses.IsSitting())
			{
				poses.SetEmote(SitEmote, false);
			}
		}

		/// <summary>
		/// Lays the viking down in the player's bed pose, or gets it back up - its position on the bed is the caller's
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		/// <param name="lying">Whether it lies down</param>
		public static void SetLying(Character viking, bool lying)
		{
			SetAttachPose(viking, BedHash, ChairHash, lying);
		}

		/// <summary>
		/// Sits the viking in the player's chair pose, or gets it back up - its position on the chair is the caller's
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		/// <param name="seated">Whether it sits</param>
		public static void SetChair(Character viking, bool seated)
		{
			SetAttachPose(viking, ChairHash, BedHash, seated);
		}

		/// <summary>
		/// Has the viking wave with the player's wave emote, unless it's sitting, lying or in a chair
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		public static void Wave(Character viking)
		{
			VikingPoses poses = Owned(viking);
			if (poses == null || poses.IsPosed())
			{
				return;
			}
			poses.EndStretch();
			poses.SetEmote(WaveEmote, true);
		}

		/// <summary>
		/// Has the viking throw its head back in the player's roar emote with a war cry every client nearby hears,
		/// unless it's sitting, lying or in a chair
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		public static void Roar(Character viking)
		{
			VikingPoses poses = Owned(viking);
			if (poses == null || poses.IsPosed())
			{
				return;
			}
			poses.EndStretch();
			poses.SetEmote(RoarEmote, true);
		}

		/// <summary>
		/// Swings the viking's arm once the way the player swings a hammer to build and repair, unless it's sitting,
		/// lying or in a chair
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		public static void Swing(Character viking)
		{
			VikingPoses poses = Owned(viking);
			if (poses == null || poses.IsPosed())
			{
				return;
			}
			poses.EndStretch();
			poses.humanoid.m_zanim.SetTrigger(SwingAnimation());
		}

		/// <summary>
		/// Swings a tool the viking holds once the way the player swings it, like a hoe making a path - or the way the
		/// player swings a hammer, for a tool with no swing of its own - unless it's sitting, lying or in a chair
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		/// <param name="tool">The tool</param>
		public static void Swing(Character viking, ItemDrop.ItemData tool)
		{
			VikingPoses poses = Owned(viking);
			if (poses == null || poses.IsPosed())
			{
				return;
			}
			poses.EndStretch();
			Attack attack = ((tool != null) ? tool.m_shared.m_attack : null);
			bool own = (attack != null && !string.IsNullOrEmpty(attack.m_attackAnimation));
			poses.humanoid.m_zanim.SetTrigger((own ? attack.m_attackAnimation : SwingAnimation()));
		}

		/// <summary>
		/// Has the viking reach out the way the player does to pick something up or use it, unless it's sitting, lying
		/// or in a chair
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		public static void Interact(Character viking)
		{
			VikingPoses poses = Owned(viking);
			if (poses == null || poses.IsPosed())
			{
				return;
			}
			poses.EndStretch();
			poses.humanoid.m_zanim.SetTrigger(InteractTrigger);
		}

		/// <summary>
		/// Ends at once whatever pose, emote, talk or stretch the viking is in, standing it up
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		public static void StopAll(Character viking)
		{
			VikingPoses poses = Owned(viking);
			if (poses != null)
			{
				poses.ClearAll();
			}
		}

		/// <summary>
		/// Finds a living or dvergr viking's poses when this client owns it and it's alive, clearing what an earlier owner
		/// left first
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>Its poses, or null</returns>
		private static VikingPoses Owned(Character viking)
		{
			if (viking == null || !VikingCore.IsLivingOrDvergr(viking) || !viking.m_nview.IsOwner() || viking.IsDead())
			{
				return(null);
			}
			VikingPoses poses = viking.GetComponent<VikingPoses>();
			if (poses == null || poses.nview == null || poses.humanoid == null)
			{
				return(null);
			}
			poses.Claim();
			return(poses);
		}

		/// <summary>
		/// Turns the bed or chair pose on or off, taking the viking out of the other pose, the floor sit and a stretch
		/// as it goes into one
		/// </summary>
		/// <param name="viking">The living viking, owned by this client</param>
		/// <param name="hash">The pose's animator bool</param>
		/// <param name="otherHash">The other attach pose's animator bool</param>
		/// <param name="on">Whether the pose is on</param>
		private static void SetAttachPose(Character viking, int hash, int otherHash, bool on)
		{
			VikingPoses poses = Owned(viking);
			if (poses == null)
			{
				return;
			}
			if (on)
			{
				poses.EndStretch();
				poses.EndSitting();
				poses.SetPoseBool(otherHash, false);
			}
			poses.SetPoseBool(hash, on);
		}

		/// <summary>
		/// Reads the hammer's swing from the item database, falling back to the player's hammer swing trigger until
		/// it's loaded
		/// </summary>
		/// <returns>The swing's animator trigger</returns>
		private static string SwingAnimation()
		{
			if (swingAnimation != null)
			{
				return(swingAnimation);
			}
			GameObject hammer = ((ObjectDB.instance != null) ? ObjectDB.instance.GetItemPrefab(HammerPrefab) : null);
			ItemDrop item = ((hammer != null) ? hammer.GetComponent<ItemDrop>() : null);
			Attack attack = ((item != null) ? item.m_itemData.m_shared.m_attack : null);
			if (attack == null || string.IsNullOrEmpty(attack.m_attackAnimation))
			{
				return(HammerSwingFallback);
			}
			swingAnimation = attack.m_attackAnimation;
			return(swingAnimation);
		}

		/// <summary>
		/// Finds the viking's parts
		/// </summary>
		private void Awake()
		{
			humanoid = GetComponent<Humanoid>();
			nview = GetComponent<ZNetView>();
			noiseSeed = Random.Range(0f, 100f);
		}

		/// <summary>
		/// Finds the bones, and starts putting them back at the end of each frame, except on a dedicated server
		/// </summary>
		private void Start()
		{
			animator = ((humanoid != null) ? humanoid.m_animator : null);
			dedicated = (ZNet.instance != null && ZNet.instance.IsDedicated());
			if (dedicated || animator == null)
			{
				return;
			}
			FindBones();
			if (touched != null)
			{
				StartCoroutine(PutBack());
			}
		}

		/// <summary>
		/// On the owner, clears what an earlier owner left and stands the viking up when it moves, attacks or is
		/// staggered, and on every client, plays the emotes and starts the stretches in the ZDO
		/// </summary>
		private void Update()
		{
			if (nview == null || !nview.IsValid() || humanoid == null)
			{
				return;
			}
			ZDO zdo = nview.GetZDO();
			if (nview.IsOwner())
			{
				Claim();
				UpdateOwner(zdo);
			}
			else
			{
				claimed = false;
			}
			UpdateEmote(zdo);
			UpdateStretchStart(zdo);
			talkMode = (TalkMode)zdo.GetInt(TalkKey);
		}

		/// <summary>
		/// Clears what an earlier owner left in the ZDO, once each time this client becomes the owner, since the
		/// viking's AI starts afresh with it
		/// </summary>
		private void Claim()
		{
			if (claimed || nview == null || !nview.IsValid() || !nview.IsOwner())
			{
				return;
			}
			claimed = true;
			ClearAll();
		}

		/// <summary>
		/// Gets the viking up off the floor as it moves, as a player does, and out of any pose and stretch as it attacks
		/// or is staggered
		/// </summary>
		/// <param name="zdo">The viking's ZDO, owned by this client</param>
		private void UpdateOwner(ZDO zdo)
		{
			if (humanoid.IsDead())
			{
				return;
			}
			if (humanoid.m_moveDir.sqrMagnitude > 0.01f)
			{
				EndSitting();
			}
			if (humanoid.InAttack() || humanoid.IsStaggering())
			{
				EndStretch();
				EndSitting();
				SetPoseBool(BedHash, false);
				SetPoseBool(ChairHash, false);
			}
		}

		/// <summary>
		/// Ends every pose, emote, talk and stretch, writing only what isn't already cleared
		/// </summary>
		private void ClearAll()
		{
			ZDO zdo = nview.GetZDO();
			if (zdo.GetString(EmoteKey) != "")
			{
				SetEmote("", false);
			}
			if (zdo.GetInt(TalkKey) != 0)
			{
				zdo.Set(TalkKey, 0);
			}
			EndStretch();
			SetPoseBool(BedHash, false);
			SetPoseBool(ChairHash, false);
		}

		/// <summary>
		/// Checks whether the viking is sitting on the floor, lying in bed or sitting in a chair
		/// </summary>
		/// <returns>True when it is</returns>
		private bool IsPosed()
		{
			ZDO zdo = nview.GetZDO();
			return(IsSitting() || zdo.GetInt(AnimationSalt + BedHash) != 0 || zdo.GetInt(AnimationSalt + ChairHash) != 0);
		}

		/// <summary>
		/// Checks whether the viking is sitting on the floor
		/// </summary>
		/// <returns>True when its emote is the sit</returns>
		private bool IsSitting()
		{
			return(nview.GetZDO().GetString(EmoteKey) == SitEmote);
		}

		/// <summary>
		/// Gets the viking up off the floor when it's sitting there
		/// </summary>
		private void EndSitting()
		{
			if (IsSitting())
			{
				SetEmote("", false);
			}
		}

		/// <summary>
		/// Ends the viking's stretch when it has one
		/// </summary>
		private void EndStretch()
		{
			ZDO zdo = nview.GetZDO();
			if (zdo.GetLong(StretchKey) != 0L)
			{
				zdo.Set(StretchKey, 0L);
			}
		}

		/// <summary>
		/// Starts an emote for every client, or ends the current one
		/// </summary>
		/// <param name="emote">The emote's name without "emote_", or "" to end it</param>
		/// <param name="oneshot">Whether it plays once rather than looping until it's ended</param>
		private void SetEmote(string emote, bool oneshot)
		{
			ZDO zdo = nview.GetZDO();
			zdo.Set(EmoteKey, emote);
			zdo.Set(EmoteOneshotKey, oneshot);
			zdo.Set(EmoteTimeKey, NetTime.Now());
			zdo.Set(EmoteIdKey, zdo.GetInt(EmoteIdKey) + 1);
		}

		/// <summary>
		/// Sets one of the synced pose bools, writing it to the ZDO even when this client's animator already has it,
		/// since a viking loaded from the world keeps the last value an earlier owner wrote
		/// </summary>
		/// <param name="hash">The animator bool's hash</param>
		/// <param name="value">Its value</param>
		private void SetPoseBool(int hash, bool value)
		{
			if (humanoid.m_zanim != null)
			{
				humanoid.m_zanim.SetBool(hash, value);
			}
			ZDO zdo = nview.GetZDO();
			int stored = (value ? 1 : 0);
			if (zdo.GetInt(AnimationSalt + hash) != stored)
			{
				zdo.Set(AnimationSalt + hash, stored);
			}
		}

		/// <summary>
		/// Plays the emote in the ZDO on this client's animator when it has changed, the way the player's emotes are
		/// played - a one-shot only while it's fresh, and a looping one whenever it's on
		/// </summary>
		/// <param name="zdo">The viking's ZDO</param>
		private void UpdateEmote(ZDO zdo)
		{
			int id = zdo.GetInt(EmoteIdKey);
			if (emoteSeen && id == emoteId)
			{
				return;
			}
			bool first = !emoteSeen;
			emoteSeen = true;
			emoteId = id;
			if (animator == null)
			{
				return;
			}
			if (emoteState != "")
			{
				animator.SetBool(EmotePrefix + emoteState, false);
			}
			emoteState = "";
			string emote = zdo.GetString(EmoteKey);
			PoseCurves.EmoteStep step = PoseCurves.EmoteAction(first, emote, zdo.GetBool(EmoteOneshotKey), zdo.GetLong(EmoteTimeKey), NetTime.Now());
			if (step == PoseCurves.EmoteStep.None)
			{
				return;
			}
			if (step == PoseCurves.EmoteStep.Stop)
			{
				animator.SetTrigger(EmoteStop);
				return;
			}
			animator.ResetTrigger(EmoteStop);
			if (step == PoseCurves.EmoteStep.Loop)
			{
				emoteState = emote;
				animator.SetBool(EmotePrefix + emote, true);
				return;
			}
			animator.SetTrigger(EmotePrefix + emote);
			if (emote == RoarEmote)
			{
				Shout();
			}
		}

		/// <summary>
		/// Notices a new stretch in the ZDO, which waits for the viking to stand when it's fresh enough to play, and
		/// starts a waiting one once the viking is up, or drops it when it waits too long
		/// </summary>
		/// <param name="zdo">The viking's ZDO</param>
		private void UpdateStretchStart(ZDO zdo)
		{
			long started = zdo.GetLong(StretchKey);
			if (started != stretchSeen)
			{
				stretchSeen = started;
				stretchStart = -1f;
				stretchPending = PoseCurves.StretchFresh(started, NetTime.Now());
				stretchWaitStart = Time.time;
			}
			if (!stretchPending)
			{
				return;
			}
			if (CanStretch())
			{
				stretchPending = false;
				stretchStart = Time.time;
			}
			else if (Time.time - stretchWaitStart > StretchWaitMax)
			{
				stretchPending = false;
			}
		}

		/// <summary>
		/// Checks whether the viking can stretch - alive, standing still on its feet and doing nothing else
		/// </summary>
		/// <returns>True when it can, and always false on a client that doesn't draw the viking</returns>
		private bool CanStretch()
		{
			if (touched == null || !IsFree() || humanoid.IsSwimming() || emoteState != "")
			{
				return(false);
			}
			if (animator.GetBool(BedHash) || animator.GetBool(ChairHash) || animator.IsInTransition(0))
			{
				return(false);
			}
			if (animator.GetFloat(ForwardSpeedHash) > StretchMaxSpeed)
			{
				return(false);
			}
			return(IsStanding());
		}

		/// <summary>
		/// Checks whether the viking is alive and neither attacking nor staggering
		/// </summary>
		/// <returns>True when it is</returns>
		private bool IsFree()
		{
			return(!humanoid.IsDead() && !humanoid.InAttack() && !humanoid.IsStaggering());
		}

		/// <summary>
		/// Checks whether the viking's animated pose has it up on its feet, from how high its hips are above its lower
		/// foot
		/// </summary>
		/// <returns>True when it's standing, or when the skeleton has no hips and feet to tell by</returns>
		private bool IsStanding()
		{
			if (hips == null || leftFoot == null || rightFoot == null)
			{
				return(true);
			}
			return(PoseCurves.IsStanding(hips.position, leftFoot.position, rightFoot.position, transform.up, transform.lossyScale.y));
		}

		/// <summary>
		/// Plays the war cry at the viking's head, on this client only, since every client plays the roar
		/// </summary>
		private void Shout()
		{
			if (dedicated)
			{
				return;
			}
			GameObject prefab = GamePrefabs.Find(ShoutSound);
			bool fallback = (prefab == null);
			if (fallback)
			{
				if (!loggedShoutFallback)
				{
					loggedShoutFallback = true;
					Plugin.Log.LogInfo(ShoutSound + " isn't loaded, so living vikings shout with " + FallbackShoutSound);
				}
				prefab = GamePrefabs.Find(FallbackShoutSound);
				if (prefab == null)
				{
					return;
				}
			}
			Vector3 position = ((humanoid.m_eye != null) ? humanoid.m_eye.position : humanoid.GetCenterPoint());
			GameObject sound = GamePrefabs.SpawnLocal(prefab, position, Quaternion.identity);
			ZSFX sfx = sound.GetComponent<ZSFX>();
			if (sfx != null)
			{
				// Read when the sound plays, after its Awake
				sfx.m_delay = ShoutDelay;
				sfx.m_closedCaptionToken = humanoid.GetHoverName();
				if (!fallback)
				{
					sfx.m_minPitch = ShoutPitchMin;
					sfx.m_maxPitch = ShoutPitchMax;
				}
			}
			Destroy(sound, ShoutLifetime);
		}

		/// <summary>
		/// Bends the stretch and the talking head onto the bones, on top of the pose the animator gave them this frame
		/// </summary>
		private void LateUpdate()
		{
			if (touched == null)
			{
				return;
			}
			float dt = Time.deltaTime;
			bool free = IsFree();
			float stretchTime = ((stretchStart >= 0f) ? Time.time - stretchStart : -1f);
			if (stretchStart >= 0f && (stretchTime >= StretchDuration || !free || humanoid.IsSwimming() || animator.GetFloat(ForwardSpeedHash) > StretchMaxSpeed || !IsStanding()))
			{
				stretchStart = -1f;
			}
			if (stretchStart >= 0f)
			{
				stretchWeight = PoseCurves.StretchCurve(stretchTime);
			}
			else
			{
				stretchWeight = Mathf.MoveTowards(stretchWeight, 0f, dt / StretchCancelTime);
			}
			UpdateTalkShape();
			talkWeight = Mathf.MoveTowards(talkWeight, ((free && talkMode != TalkMode.None) ? 1f : 0f), dt / TalkFadeTime);
			if (stretchWeight <= 0f && talkWeight <= 0f)
			{
				return;
			}
			for (int i = 0; i < touched.Length; i++)
			{
				savedRotations[i] = touched[i].localRotation;
			}
			applied = true;
			if (stretchWeight > 0f)
			{
				BendStretch(stretchWeight, Mathf.Max(stretchTime, 0f));
			}
			if (talkWeight > 0f)
			{
				TurnTalkingHead(talkWeight);
			}
		}

		/// <summary>
		/// Arches the back, tilts the head back and reaches the arms overhead, reaching a little further through the
		/// middle of the stretch
		/// </summary>
		/// <param name="weight">How much of the stretch is shown, from 0 to 1</param>
		/// <param name="time">Seconds since it started</param>
		private void BendStretch(float weight, float time)
		{
			Vector3 up = transform.up;
			Vector3 right = transform.right;
			Vector3 forward = transform.forward;
			float reach = 0.85f + 0.15f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(time / StretchDuration));
			float arch = StretchArchAngle * weight * reach;
			if (spine.Length > 0)
			{
				// Turning about the right axis by a negative angle tips a bone's top backward
				Quaternion bend = Quaternion.AngleAxis(-arch / spine.Length, right);
				foreach (Transform bone in spine)
				{
					bone.rotation = bend * bone.rotation;
				}
			}
			Vector3 bodyUp = Quaternion.AngleAxis(-arch, right) * up;
			for (int i = 0; i < 2; i++)
			{
				if (arms[i] == null || foreArms[i] == null)
				{
					continue;
				}
				float side = ((i == 0) ? -1f : 1f);
				if (shoulders[i] != null)
				{
					Vector3 toArm = arms[i].position - shoulders[i].position;
					Turn(shoulders[i], toArm, toArm.normalized + bodyUp * StretchShrug, weight);
				}
				Vector3 upper = bodyUp + right * (side * StretchArmOutward) - forward * StretchArmBack;
				Turn(arms[i], foreArms[i].position - arms[i].position, upper, weight);
				if (hands[i] != null)
				{
					Vector3 lower = bodyUp - right * (side * StretchForearmInward) - forward * StretchArmBack;
					Turn(foreArms[i], hands[i].position - foreArms[i].position, lower, weight);
				}
			}
			float tilt = StretchHeadAngle * weight * reach;
			TurnHead(-tilt, 0f, 0f);
		}

		/// <summary>
		/// Keeps the talk mode the head moves with, picking a new listener's tilt and the timing of the first nod as a
		/// new mode starts
		/// </summary>
		private void UpdateTalkShape()
		{
			if (talkMode == TalkMode.None || talkMode == talkShape)
			{
				return;
			}
			talkShape = talkMode;
			listenTilt = Random.Range(ListenTiltMin, ListenTiltMax) * ((Random.value < 0.5f) ? -1f : 1f);
			beatStart = -1f;
			nextBeat = Time.time + ((talkShape == TalkMode.Speaking) ? Random.Range(0.2f, SpeakBeatIntervalMin) : Random.Range(0.5f, ListenNodIntervalMin));
		}

		/// <summary>
		/// Moves the head as the viking speaks - small wandering turns with a nod or tilt every second or two - or as it
		/// listens - a held tilt with a slow nod now and then
		/// </summary>
		/// <param name="weight">How much of the motion is shown, from 0 to 1</param>
		private void TurnTalkingHead(float weight)
		{
			float now = Time.time;
			if (now >= nextBeat)
			{
				StartBeat(now);
			}
			float pitch;
			float yaw;
			float roll;
			if (talkShape == TalkMode.Speaking)
			{
				float x = now * SpeakNoiseSpeed;
				pitch = SpeakNoisePitch * Noise(x, 0);
				yaw = SpeakNoiseYaw * Noise(x, 1);
				roll = SpeakNoiseRoll * Noise(x, 2);
			}
			else
			{
				float x = now * ListenNoiseSpeed;
				pitch = ListenNoisePitch * Noise(x, 0);
				yaw = ListenNoiseYaw * Noise(x, 1);
				roll = listenTilt;
			}
			float beat = PoseCurves.BeatShape(now, beatStart, beatTime, beatDouble);
			pitch += beatPitch * beat;
			roll += beatRoll * beat;
			TurnHead(pitch * weight, yaw * weight, roll * weight);
		}

		/// <summary>
		/// Starts the head's next nod or tilt and picks when the one after it starts
		/// </summary>
		/// <param name="now">The time</param>
		private void StartBeat(float now)
		{
			beatStart = now;
			if (talkShape == TalkMode.Speaking)
			{
				beatDouble = false;
				beatTime = Random.Range(SpeakBeatTimeMin, SpeakBeatTimeMax);
				if (Random.value < SpeakNodChance)
				{
					beatPitch = Random.Range(SpeakNodMin, SpeakNodMax);
					beatRoll = Random.Range(-2f, 2f);
				}
				else
				{
					beatPitch = Random.Range(-2f, 3f);
					beatRoll = Random.Range(SpeakTiltMin, SpeakTiltMax) * ((Random.value < 0.5f) ? -1f : 1f);
				}
				nextBeat = now + beatTime + Random.Range(SpeakBeatIntervalMin, SpeakBeatIntervalMax);
				return;
			}
			beatDouble = (Random.value < ListenDoubleChance);
			beatTime = Random.Range(ListenNodTimeMin, ListenNodTimeMax) * (beatDouble ? 1.5f : 1f);
			beatPitch = Random.Range(ListenNodMin, ListenNodMax);
			beatRoll = 0f;
			nextBeat = now + beatTime + Random.Range(ListenNodIntervalMin, ListenNodIntervalMax);
		}

		/// <summary>
		/// Reads this viking's smooth noise on one channel
		/// </summary>
		/// <param name="x">Where along the noise, which wanders as it grows</param>
		/// <param name="channel">Which of the head's turns it's for</param>
		/// <returns>From -1 to 1</returns>
		private float Noise(float x, int channel)
		{
			return((Mathf.PerlinNoise(x, noiseSeed + channel * 17.3f) - 0.5f) * 2f);
		}

		/// <summary>
		/// Nods, turns and tilts the head, shared between the neck and the head
		/// </summary>
		/// <param name="pitch">Degrees forward</param>
		/// <param name="yaw">Degrees to the right</param>
		/// <param name="roll">Degrees tilted to the right</param>
		private void TurnHead(float pitch, float yaw, float roll)
		{
			Vector3 up = transform.up;
			Vector3 right = transform.right;
			Vector3 forward = transform.forward;
			if (neck != null)
			{
				neck.rotation = TurnAbout(pitch * NeckShare, yaw * NeckShare, roll * NeckShare, up, right, forward) * neck.rotation;
			}
			if (head != null)
			{
				float share = ((neck != null) ? 1f - NeckShare : 1f);
				head.rotation = TurnAbout(pitch * share, yaw * share, roll * share, up, right, forward) * head.rotation;
			}
		}

		/// <summary>
		/// Builds a turn about the viking's own axes
		/// </summary>
		/// <param name="pitch">Degrees forward, about its right</param>
		/// <param name="yaw">Degrees to the right, about its up</param>
		/// <param name="roll">Degrees tilted to the right, about its forward</param>
		/// <param name="up">Its up</param>
		/// <param name="right">Its right</param>
		/// <param name="forward">Its forward</param>
		/// <returns>The turn, in world space</returns>
		private static Quaternion TurnAbout(float pitch, float yaw, float roll, Vector3 up, Vector3 right, Vector3 forward)
		{
			// Turning about the forward axis by a negative angle tips a bone's top to the right
			return(Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(pitch, right) * Quaternion.AngleAxis(-roll, forward));
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
		/// Finds the player skeleton's hips, feet, spine, neck, head and arms by their names, leaving the poses off when
		/// there's no spine
		/// </summary>
		private void FindBones()
		{
			hips = Utils.FindChild(animator.transform, "Hips");
			Transform lowSpine = ((hips != null) ? hips.Find("Spine") : null);
			Transform midSpine = ((lowSpine != null) ? lowSpine.Find("Spine1") : null);
			Transform chest = ((midSpine != null) ? midSpine.Find("Spine2") : null);
			if (chest == null)
			{
				return;
			}
			spine = new Transform[] { lowSpine, midSpine, chest };
			leftFoot = Utils.FindChild(hips, "LeftFoot");
			rightFoot = Utils.FindChild(hips, "RightFoot");
			neck = chest.Find("Neck");
			head = ((neck != null) ? neck.Find("Head") : chest.Find("Head"));
			string[] sides = new string[] { "Left", "Right" };
			for (int i = 0; i < 2; i++)
			{
				shoulders[i] = chest.Find(sides[i] + "Shoulder");
				arms[i] = ((shoulders[i] != null) ? shoulders[i].Find(sides[i] + "Arm") : null);
				foreArms[i] = ((arms[i] != null) ? arms[i].Find(sides[i] + "ForeArm") : null);
				hands[i] = ((foreArms[i] != null) ? foreArms[i].Find(sides[i] + "Hand") : null);
			}
			List<Transform> bones = new List<Transform>(spine);
			foreach (Transform bone in new Transform[] { neck, head, shoulders[0], arms[0], foreArms[0], shoulders[1], arms[1], foreArms[1] })
			{
				if (bone != null)
				{
					bones.Add(bone);
				}
			}
			touched = bones.ToArray();
			savedRotations = new Quaternion[touched.Length];
		}

		/// <summary>
		/// Puts back each bone the poses turned at the end of every frame, once it's been drawn, so a frame the animator
		/// skips doesn't turn them twice
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

	/// <summary>
	/// The timing and shape of a living viking's poses - how its stretch eases in and out, how its head nods, what a
	/// client does with the emote in the ZDO, when a stretch is fresh enough to play, and when it's on its feet
	/// </summary>
	internal static class PoseCurves
	{
		/// <summary>
		/// What a client does with its animator when the emote in the ZDO changes
		/// </summary>
		internal enum EmoteStep
		{
			/// <summary>
			/// Nothing
			/// </summary>
			None,

			/// <summary>
			/// Ends the emote playing
			/// </summary>
			Stop,

			/// <summary>
			/// Turns on the emote's looping bool
			/// </summary>
			Loop,

			/// <summary>
			/// Sets the emote's trigger to play it once
			/// </summary>
			Trigger
		}

		/// <summary>
		/// Works out how much of the stretch is shown at a moment in it, easing in and out
		/// </summary>
		/// <param name="time">Seconds since it started</param>
		/// <returns>The weight, from 0 to 1</returns>
		internal static float StretchCurve(float time)
		{
			float easeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / VikingPoses.StretchIn));
			float easeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((VikingPoses.StretchDuration - time) / VikingPoses.StretchOut));
			return(Mathf.Min(easeIn, easeOut));
		}

		/// <summary>
		/// Works out how far through its nod or tilt the head is, rising and falling smoothly, twice for a double nod
		/// </summary>
		/// <param name="now">The time</param>
		/// <param name="start">When the nod or tilt started, or a negative number when there's none</param>
		/// <param name="time">How long it takes, in seconds</param>
		/// <param name="isDouble">Whether it's a double nod</param>
		/// <returns>From 0 at rest to 1 at the deepest</returns>
		internal static float BeatShape(float now, float start, float time, bool isDouble)
		{
			if (start < 0f)
			{
				return(0f);
			}
			float progress = (now - start) / time;
			if (progress < 0f || progress > 1f)
			{
				return(0f);
			}
			float wave = Mathf.Sin(Mathf.PI * progress * (isDouble ? 2f : 1f));
			return(wave * wave);
		}

		/// <summary>
		/// Checks whether something that started at a moment is recent enough for a client that sees it now to play it -
		/// no more than LateLimit ago
		/// </summary>
		/// <param name="started">When it started, in ZNet time ticks</param>
		/// <param name="now">The time, in ZNet time ticks</param>
		/// <returns>True when it is</returns>
		internal static bool IsFresh(long started, long now)
		{
			return(now - started <= NetTime.Ticks(VikingPoses.LateLimit));
		}

		/// <summary>
		/// Works out what a client does with its animator when the emote in the ZDO changes, the way the player's emotes
		/// are played - a one-shot only while it's fresh, a looping one whenever it's on, and otherwise the emote playing
		/// ends, unless the client has only just read the ZDO, when nothing was playing
		/// </summary>
		/// <param name="first">Whether this is the first time the client reads the emote</param>
		/// <param name="emote">The emote's name without "emote_", or "" for none</param>
		/// <param name="oneshot">Whether it plays once</param>
		/// <param name="started">When it started, in ZNet time ticks</param>
		/// <param name="now">The time, in ZNet time ticks</param>
		/// <returns>What to do</returns>
		internal static EmoteStep EmoteAction(bool first, string emote, bool oneshot, long started, long now)
		{
			if (emote == "" || (oneshot && !IsFresh(started, now)))
			{
				return((first ? EmoteStep.None : EmoteStep.Stop));
			}
			return((oneshot ? EmoteStep.Trigger : EmoteStep.Loop));
		}

		/// <summary>
		/// Checks whether a stretch in the ZDO is one to play - there is one, and it's fresh
		/// </summary>
		/// <param name="started">When it started, in ZNet time ticks, or 0 for none</param>
		/// <param name="now">The time, in ZNet time ticks</param>
		/// <returns>True when it is</returns>
		internal static bool StretchFresh(long started, long now)
		{
			return(started != 0L && IsFresh(started, now));
		}

		/// <summary>
		/// Checks whether a pose has the viking up on its feet, from how high its hips are above its lower foot
		/// </summary>
		/// <param name="hips">Where its hips are</param>
		/// <param name="leftFoot">Where its left foot is</param>
		/// <param name="rightFoot">Where its right foot is</param>
		/// <param name="up">The viking's up, of length 1</param>
		/// <param name="scale">How much the viking is scaled up along its up</param>
		/// <returns>True when its hips are StandingHipHeight, scaled, above its lower foot or higher</returns>
		internal static bool IsStanding(Vector3 hips, Vector3 leftFoot, Vector3 rightFoot, Vector3 up, float scale)
		{
			float foot = Mathf.Min(Vector3.Dot(leftFoot, up), Vector3.Dot(rightFoot, up));
			return(Vector3.Dot(hips, up) - foot >= VikingPoses.StandingHipHeight * scale);
		}
	}
}
