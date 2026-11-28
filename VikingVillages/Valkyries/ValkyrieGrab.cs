using System.Linq;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A valkyrie's grab - her AI snatches an enemy no bigger than a greydwarf brute in her talons, flies it up 20
	/// meters and drops it, on solid ground only
	/// </summary>
	/// <remarks>
	/// The grab is a strike of her own (see ValkyrieCombat), and when it lands on the enemy she went for, the client
	/// that owns the enemy is asked to hold it at her talon (see ValkyrieCarried) - that client decides, since a player
	/// rolling away isn't grabbed - and tells the client that owns her it has, which flies her up and has it let go.
	/// Each enemy notes when it was dropped in its ZDO, so no valkyrie grabs it again for a while
	/// </remarks>
	internal class ValkyrieGrab : MonoBehaviour
	{
		/// <summary>
		/// The name of her grab's item, which tells it apart among her attacks
		/// </summary>
		public const string GrabName = "ValkyrieGrab";

		/// <summary>
		/// The RPCs on a grabbed creature to grab and drop it, and on her that it's been grabbed
		/// </summary>
		public const string GrabRPC = "kri_vv_valkyrie_grab";
		public const string DropRPC = "kri_vv_valkyrie_drop";
		public const string GrabbedRPC = "kri_vv_valkyrie_grabbed";

		/// <summary>
		/// The ZDO key on a creature holding when a valkyrie last dropped it, in the game's ticks
		/// </summary>
		public static readonly int DroppedKey = "kri_vv_valkyrie_dropped".GetStableHashCode();

		/// <summary>
		/// The least time between her grabs, and before a creature she dropped can be grabbed again, in seconds
		/// </summary>
		internal const float Cooldown = 15f;
		internal const float TargetCooldown = 60f;

		/// <summary>
		/// How near an enemy has to be for her to grab it, across and up or down, in meters
		/// </summary>
		internal const float Reach = 4f;

		/// <summary>
		/// How long she flies up with what she grabbed before she drops it, in seconds - at her slow flying speed of 10
		/// meters a second, about 20 meters
		/// </summary>
		internal const float LiftTime = 2f;

		/// <summary>
		/// The tallest and widest a creature can be for her to grab it, in meters - as tall as a fenring, and a little
		/// wider than a greydwarf brute, since the capsules of four-legged creatures like deer lie along their bodies
		/// </summary>
		internal const float MaxHeight = 3f;
		internal const float MaxRadius = 0.85f;

		/// <summary>
		/// The cape that keeps a player from being grabbed
		/// </summary>
		private static readonly int FeatherCape = "CapeFeather".GetStableHashCode();

		/// <summary>
		/// The bone under her right foot her talon holds what she grabs by
		/// </summary>
		private const string TalonName = "Attach";

		private Humanoid body;
		private ValkyrieFlight flight;
		private ZNetView nview;
		private Transform talon;

		/// <summary>
		/// When she last grabbed, by the game's time, the enemy her current grab goes for, and whether it was asked to be
		/// held yet
		/// </summary>
		private float lastGrab = -100f;
		private Character aimedAt;
		private bool asked;

		/// <summary>
		/// What she's carrying up and until when, by the game's time, or below 0 when she isn't
		/// </summary>
		private ZDOID carried = ZDOID.None;
		private float liftUntil = -1f;

		/// <summary>
		/// What she last dropped and when, by the game's time, which she watches fall rather than strike at before it
		/// lands (see Watching)
		/// </summary>
		private ZDOID dropped = ZDOID.None;
		private float droppedAt = -100f;

		/// <summary>
		/// The longest she watches what she dropped fall, in seconds - a drop from 20 meters takes about 2 - and how long
		/// after the drop landing counts, so it isn't taken as landed as it's let go
		/// </summary>
		internal const float WatchTime = 3f;
		internal const float LandAfter = 0.3f;

		/// <summary>
		/// The bone her talon holds what she grabs by
		/// </summary>
		public Transform Talon
		{
			get
			{
				if (talon == null)
				{
					// Looked for under her own model, not the hidden Fallen Valkyrie skeleton beside it
					ValkyrieModel model = GetComponentInChildren<ValkyrieModel>(true);
					talon = ((model != null) ? Utils.FindChild(model.transform, TalonName) : null);
				}
				return((talon != null ? talon : transform));
			}
		}

		/// <summary>
		/// Finds her parts and listens for what she grabbed telling her it's held
		/// </summary>
		private void Awake()
		{
			body = GetComponent<Humanoid>();
			flight = GetComponent<ValkyrieFlight>();
			nview = GetComponent<ZNetView>();
			if (nview != null && nview.IsValid())
			{
				nview.Register<ZDOID>(GrabbedRPC, RPC_Grabbed);
			}
		}

		/// <summary>
		/// The creatures she never grabs whatever their size, by the start of their prefab names - ghosts, The Void among
		/// them, Shadows, the Riktig Fuling, and Hildir's Zil, Brenna and Geirrhafa and the Ashlands' Lord Reto, with the
		/// copies of them that carry no chest, and the roots and tendrils fixed in the ground - the Elder's, its Aspect's
		/// and the Mistlands'
		/// </summary>
		internal static readonly string[] NeverGrabbed = { "Ghost", "ShadowPerson", "Goblin_Gem", "GoblinShaman_Hildir", "Skeleton_Hildir", "Fenring_Cultist_Hildir", "Charred_Melee_Dyrnwyn", "TentaRoot", "Aspect_TentaRoot", "Tendril" };

		/// <summary>
		/// Checks whether a creature is one she can grab by its kind and size - none that fly, bosses, oozes, serpents,
		/// the training dummy or those she never grabs (see NeverGrabbed), and none taller or wider than she can carry
		/// </summary>
		/// <param name="prefabName">The creature's prefab name</param>
		/// <param name="height">Its capsule's height, in meters</param>
		/// <param name="radius">Its capsule's radius, in meters</param>
		/// <param name="flies">Whether it flies</param>
		/// <param name="boss">Whether it's a boss</param>
		/// <returns>True when she can grab it</returns>
		internal static bool Grabbable(string prefabName, float height, float radius, bool flies, bool boss)
		{
			if (flies || boss || prefabName.StartsWith("Blob") || prefabName.Contains("Serpent") || prefabName == "TrainingDummy")
			{
				return(false);
			}
			foreach (string never in NeverGrabbed)
			{
				if (prefabName.StartsWith(never))
				{
					return(false);
				}
			}
			return(height <= MaxHeight && radius <= MaxRadius);
		}

		/// <summary>
		/// The damage a fall does, the way the game works out a player's - none up to 4 meters, rising evenly to 100 at
		/// 20 meters and no more past it
		/// </summary>
		/// <param name="fall">How far it fell, in meters</param>
		/// <returns>The damage</returns>
		internal static float FallDamage(float fall)
		{
			return(Mathf.Clamp01((fall - 4f) / 16f) * 100f);
		}

		/// <summary>
		/// Checks whether a creature was dropped too lately to be grabbed again
		/// </summary>
		/// <param name="now">The game's time, in ticks</param>
		/// <param name="dropped">When it was last dropped, in ticks, or 0 when it never was</param>
		/// <returns>True while it can't be grabbed again</returns>
		internal static bool DroppedLately(long now, long dropped)
		{
			return(dropped > 0 && (now - dropped) / (double)System.TimeSpan.TicksPerSecond < TargetCooldown);
		}

		/// <summary>
		/// Grabs her target with her AI or carries up what she grabbed, on the client that owns her, before her AI's
		/// update - she grabs an enemy within reach she can grab and drop on solid ground (see CanGrab), at most every 15
		/// seconds. Once she's dropped it she hovers and watches it fall, striking at nothing, until it lands
		/// </summary>
		/// <param name="ai">Her AI</param>
		/// <returns>True when she's grabbing, carrying or watching, so her AI leaves her be this update</returns>
		public bool Think(MonsterAI ai)
		{
			if (liftUntil >= 0f)
			{
				Lift();
				return(true);
			}
			if (Watching())
			{
				body.SetMoveDir(transform.forward * 0.001f);
				body.SetRun(false);
				return(true);
			}
			if (Time.time - lastGrab < Cooldown || body.InAttack())
			{
				return(false);
			}
			Character target = ai.GetTargetCreature();
			if (target == null)
			{
				return(false);
			}
			Vector3 offset = target.transform.position - transform.position;
			if (new Vector2(offset.x, offset.z).magnitude > Reach || Mathf.Abs(offset.y) > Reach || !CanGrab(target))
			{
				return(false);
			}
			ItemDrop.ItemData grab = body.GetInventory().GetAllItems().FirstOrDefault((ItemDrop.ItemData item) => item.m_shared.m_name == GrabName);
			if (grab == null)
			{
				return(false);
			}
			offset.y = 0f;
			if (offset.sqrMagnitude > 0.0001f)
			{
				transform.rotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
				body.SetLookDir(offset.normalized);
			}
			if (!body.IsItemEquiped(grab))
			{
				body.EquipItem(grab, false);
			}
			if (!body.StartAttack(target, false))
			{
				return(false);
			}
			lastGrab = Time.time;
			aimedAt = target;
			asked = false;
			return(true);
		}

		/// <summary>
		/// Asks the enemy her grab went for to be held at her talon once her grab's strike lands on it - it still takes
		/// the strike's damage either way, and only the first hit asks
		/// </summary>
		/// <param name="hit">The creature her strike hit</param>
		public void Struck(Character hit)
		{
			if (asked || hit == null || hit != aimedAt || !CanGrab(hit))
			{
				return;
			}
			asked = true;
			hit.m_nview.InvokeRPC(ZNetView.Everybody, GrabRPC, nview.GetZDO().m_uid);
		}

		/// <summary>
		/// Checks whether she's still watching what she dropped fall - until it lands, as far as this client can tell,
		/// dies or is gone, or WatchTime seconds pass (see StillFalling)
		/// </summary>
		/// <returns>True while she is</returns>
		private bool Watching()
		{
			if (dropped.IsNone())
			{
				return(false);
			}
			GameObject fallingObject = ZNetScene.instance.FindInstance(dropped);
			Character falling = ((fallingObject != null) ? fallingObject.GetComponent<Character>() : null);
			bool landed = falling != null && falling.m_nview != null && falling.m_nview.IsValid() && falling.m_nview.IsOwner() && falling.IsOnGround();
			if (StillFalling(Time.time - droppedAt, falling == null || falling.IsDead(), landed))
			{
				return(true);
			}
			dropped = ZDOID.None;
			return(false);
		}

		/// <summary>
		/// Checks whether what she dropped is still falling, for her to watch it rather than strike at it
		/// </summary>
		/// <param name="sinceDrop">How long since she let go of it, in seconds</param>
		/// <param name="gone">Whether it's dead or gone</param>
		/// <param name="landed">Whether it's on the ground - only known on the client that owns it</param>
		/// <returns>True while it is</returns>
		internal static bool StillFalling(float sinceDrop, bool gone, bool landed)
		{
			if (gone || sinceDrop >= WatchTime)
			{
				return(false);
			}
			return(sinceDrop < LandAfter || !landed);
		}

		/// <summary>
		/// Checks whether her current attack is her grab
		/// </summary>
		/// <returns>True while it is</returns>
		public bool Grabbing()
		{
			Attack attack = body.m_currentAttack;
			return(attack != null && attack.m_weapon != null && attack.m_weapon.m_shared.m_name == GrabName);
		}

		/// <summary>
		/// Checks whether she can grab a creature - alive, of a kind and size she can (see Grabbable), not dropped in the
		/// last minute, standing over solid ground she can drop it on, and for a player, not wearing a feather cape
		/// </summary>
		/// <param name="target">The creature</param>
		/// <returns>True when she can</returns>
		private bool CanGrab(Character target)
		{
			if (target.IsDead() || target.m_nview == null || !target.m_nview.IsValid())
			{
				return(false);
			}
			ZDO zdo = target.m_nview.GetZDO();
			if (DroppedLately(ZNet.instance.GetTime().Ticks, zdo.GetLong(DroppedKey, 0L)))
			{
				return(false);
			}
			if (target.IsPlayer())
			{
				if (zdo.GetInt(ZDOVars.s_shoulderItem) == FeatherCape)
				{
					return(false);
				}
			}
			else
			{
				CapsuleCollider capsule = target.GetComponent<CapsuleCollider>();
				float scale = target.transform.lossyScale.x;
				float height = ((capsule != null) ? capsule.height * scale : 0f);
				float radius = ((capsule != null) ? capsule.radius * scale : 0f);
				if (!Grabbable(Utils.GetPrefabName(target.gameObject), height, radius, target.IsFlying(), target.IsBoss()))
				{
					return(false);
				}
			}
			return(SolidGround(target.transform.position));
		}

		/// <summary>
		/// Checks whether there's solid ground under a spot to drop something on - not water and not lava
		/// </summary>
		/// <param name="point">The spot</param>
		/// <returns>True when there is</returns>
		private static bool SolidGround(Vector3 point)
		{
			ZoneSystem zones = ZoneSystem.instance;
			if (zones == null || !zones.GetSolidHeight(point, out float height))
			{
				return(false);
			}
			point.y = height;
			return(height > zones.m_waterLevel + 0.5f && !zones.IsLava(point));
		}

		/// <summary>
		/// Starts carrying what she grabbed up, once its client tells her it's held - taking off first if she's on the
		/// ground
		/// </summary>
		/// <param name="sender">The client that holds it</param>
		/// <param name="grabbed">What she grabbed</param>
		private void RPC_Grabbed(long sender, ZDOID grabbed)
		{
			if (!nview.IsOwner())
			{
				return;
			}
			carried = grabbed;
			liftUntil = Time.time + LiftTime;
			if (!body.IsFlying())
			{
				flight.TakeOff();
			}
		}

		/// <summary>
		/// Flies her straight up with what she grabbed, and has it let go once she's carried it long enough
		/// </summary>
		private void Lift()
		{
			if (Time.time >= liftUntil)
			{
				liftUntil = -1f;
				GameObject held = ZNetScene.instance.FindInstance(carried);
				if (held != null && held.TryGetComponent(out ZNetView heldView) && heldView.IsValid())
				{
					heldView.InvokeRPC(ZNetView.Everybody, DropRPC);
				}
				dropped = carried;
				droppedAt = Time.time;
				carried = ZDOID.None;
				return;
			}
			body.SetMoveDir(transform.forward * 0.001f + Vector3.up);
			body.SetRun(false);
		}
	}
}
