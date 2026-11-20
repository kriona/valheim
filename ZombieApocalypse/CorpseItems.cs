using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Keeps what a creature held in its hands on its corpse, like a troll's log, until the corpse rises or rots away
	/// </summary>
	/// <remarks>
	/// The game never puts held items on a corpse. The items and the names of the hand bones they hang from are kept in
	/// the corpse's ZDO, so every client shows them, and each one is copied onto the corpse's bone of the same name the
	/// way the game hangs an item from a living creature's hand. The copies are children of the corpse, so they take on
	/// its tint and hide along with it when its zombie stands
	/// </remarks>
	internal class CorpseItems : MonoBehaviour
	{
		/// <summary>
		/// On a corpse, the items in the creature's hands, the hand bones they hang from and the left item's variant
		/// </summary>
		private static readonly int RightItemKey = "kriona.ZombieApocalypse.corpseRightItem".GetStableHashCode();
		private static readonly int RightHandKey = "kriona.ZombieApocalypse.corpseRightHand".GetStableHashCode();
		private static readonly int LeftItemKey = "kriona.ZombieApocalypse.corpseLeftItem".GetStableHashCode();
		private static readonly int LeftHandKey = "kriona.ZombieApocalypse.corpseLeftHand".GetStableHashCode();
		private static readonly int LeftVariantKey = "kriona.ZombieApocalypse.corpseLeftVariant".GetStableHashCode();

		/// <summary>
		/// The child of an item's prefab that holds the model hung from a hand, and the offset some items hang at
		/// </summary>
		private const string AttachName = "attach";
		private const string OffsetName = "equipoffset";

		/// <summary>
		/// The child of an item's model that's only shown while the item is equipped
		/// </summary>
		private const string EquippedName = "equiped";

		/// <summary>
		/// Stores what a dying creature holds in its corpse's ZDO
		/// </summary>
		/// <param name="zdo">The corpse's ZDO</param>
		/// <param name="creature">The dying creature, or null</param>
		/// <returns>True when it held anything</returns>
		public static bool Store(ZDO zdo, Character creature)
		{
			Humanoid humanoid = creature as Humanoid;
			VisEquipment visual = ((humanoid != null) ? humanoid.m_visEquipment : null);
			if (visual == null)
			{
				return(false);
			}
			bool stored = false;
			ItemDrop.ItemData right = humanoid.m_rightItem;
			if (right != null && right.m_dropPrefab != null && visual.m_rightHand != null)
			{
				zdo.Set(RightItemKey, right.m_dropPrefab.name.GetStableHashCode());
				zdo.Set(RightHandKey, visual.m_rightHand.name);
				stored = true;
			}
			ItemDrop.ItemData left = humanoid.m_leftItem;
			if (left != null && left.m_dropPrefab != null && visual.m_leftHand != null)
			{
				zdo.Set(LeftItemKey, left.m_dropPrefab.name.GetStableHashCode());
				zdo.Set(LeftHandKey, visual.m_leftHand.name);
				zdo.Set(LeftVariantKey, left.m_variant);
				stored = true;
			}
			return(stored);
		}

		/// <summary>
		/// Checks whether a corpse's ZDO holds items for its hands
		/// </summary>
		/// <param name="zdo">The corpse's ZDO</param>
		/// <returns>True when it does</returns>
		public static bool Has(ZDO zdo)
		{
			return(zdo.GetInt(RightItemKey) != 0 || zdo.GetInt(LeftItemKey) != 0);
		}

		/// <summary>
		/// Hangs the items from the corpse's hands
		/// </summary>
		private void Start()
		{
			ZNetView nview = GetComponent<ZNetView>();
			if (nview == null || !nview.IsValid() || ObjectDB.instance == null)
			{
				return;
			}
			ZDO zdo = nview.GetZDO();
			Attach(zdo.GetInt(RightItemKey), zdo.GetString(RightHandKey), 0);
			Attach(zdo.GetInt(LeftItemKey), zdo.GetString(LeftHandKey), zdo.GetInt(LeftVariantKey));
		}

		/// <summary>
		/// Copies an item's hand model onto one of the corpse's bones
		/// </summary>
		/// <param name="itemHash">The item prefab's name hash, or 0 for none</param>
		/// <param name="boneName">The name of the hand bone it hangs from</param>
		/// <param name="variant">The item's variant</param>
		private void Attach(int itemHash, string boneName, int variant)
		{
			if (itemHash == 0 || string.IsNullOrEmpty(boneName))
			{
				return;
			}
			GameObject prefab = ObjectDB.instance.GetItemPrefab(itemHash);
			Transform model = ((prefab != null) ? prefab.transform.Find(AttachName) : null);
			Transform bone = Utils.FindChild(transform, boneName);
			if (model == null || bone == null)
			{
				return;
			}
			GameObject copy = Instantiate(model.gameObject);
			copy.SetActive(true);
			foreach (Collider collider in copy.GetComponentsInChildren<Collider>())
			{
				collider.enabled = false;
			}
			Transform equipped = copy.transform.Find(EquippedName);
			if (equipped != null)
			{
				equipped.gameObject.SetActive(true);
			}
			copy.transform.SetParent(bone);
			copy.transform.localPosition = Vector3.zero;
			copy.transform.localRotation = Quaternion.identity;
			Transform offset = prefab.transform.Find(OffsetName);
			if (offset != null)
			{
				copy.transform.localPosition += offset.position;
				copy.transform.localRotation *= offset.rotation;
			}
			copy.GetComponentInChildren<IEquipmentVisual>()?.Setup(variant);
		}
	}
}
