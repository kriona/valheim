using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A valkyrie's pack - a small inventory of her own, apart from the one that holds her attacks, which a player she's
	/// befriended opens to give her food (see ValkyriePackWindow). Hungry, she eats from it rather than off the ground:
	/// anything a viking would eat (see VikingFood.IsFood), and raw meat that cooks into food. She's spawned fed, with
	/// StartMeat in it, and drops what's in it when she dies. Being fed never befriends her (see ValkyrieTamingPatch)
	/// </summary>
	/// <remarks>
	/// It's kept in her ZDO, so every client sees the same pack - read again whenever another client has changed it, and
	/// written only by the client that owns her
	/// </remarks>
	internal class ValkyriePack : MonoBehaviour
	{
		/// <summary>
		/// How many columns and rows her pack has
		/// </summary>
		public const int Columns = 5;
		public const int Rows = 2;

		/// <summary>
		/// What she's spawned with in her pack, and how many of each
		/// </summary>
		internal static readonly string[] StartMeat = { "LoxMeat", "BugMeat" };
		internal const int StartAmount = 5;

		/// <summary>
		/// How often she looks in her pack for something to eat when she's hungry, in seconds
		/// </summary>
		private const float EatInterval = 5f;

		/// <summary>
		/// The ZDO keys holding her pack, and noting she's been given her start
		/// </summary>
		private static readonly int PackKey = "kri_vv_valkyrie_pack".GetStableHashCode();
		private static readonly int StockedKey = "kri_vv_valkyrie_stocked".GetStableHashCode();

		/// <summary>
		/// The raw foods she eats that a viking wouldn't, by prefab name - whatever cooks into food (see RawFoods)
		/// </summary>
		private static HashSet<string> rawFoods;

		private ZNetView nview;
		private Tameable tameable;
		private Character character;
		private Inventory pack;
		private string loaded;
		private float nextEat;

		/// <summary>
		/// Finds her parts, makes her pack and has it dropped when she dies
		/// </summary>
		private void Awake()
		{
			nview = GetComponent<ZNetView>();
			tameable = GetComponent<Tameable>();
			character = GetComponent<Character>();
			Inventory playerInventory = ((Player.m_localPlayer != null) ? Player.m_localPlayer.GetInventory() : null);
			pack = new Inventory(ValkyriePrefab.DisplayName, ((playerInventory != null) ? playerInventory.GetBkg() : null), Columns, Rows);
			pack.m_onChanged += Changed;
			if (character != null)
			{
				character.m_onDeath = (System.Action)System.Delegate.Combine(character.m_onDeath, new System.Action(DropPack));
			}
		}

		/// <summary>
		/// Drops everything in her pack where she dies, on the client that owns her, scattered a little the way a
		/// creature's drops are, and empties it
		/// </summary>
		private void DropPack()
		{
			if (nview == null || !nview.IsValid() || !nview.IsOwner())
			{
				return;
			}
			Inventory inventory = Pack();
			Vector3 at = transform.position + Vector3.up * 0.5f;
			foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
			{
				if (item.m_dropPrefab == null)
				{
					continue;
				}
				Vector3 scatter = Random.insideUnitSphere * 0.5f;
				scatter.y = Mathf.Abs(scatter.y);
				ItemDrop.DropItem(item, item.m_stack, at + scatter, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
			}
			inventory.RemoveAll();
		}

		/// <summary>
		/// Gives her the meat she starts with and feeds her, the first time she's woken on the client that owns her
		/// </summary>
		private void Start()
		{
			if (nview == null || !nview.IsValid() || !nview.IsOwner() || nview.GetZDO().GetBool(StockedKey))
			{
				return;
			}
			nview.GetZDO().Set(StockedKey, true);
			Pack();
			foreach (string meat in StartMeat)
			{
				GameObject prefab = ((ObjectDB.instance != null) ? ObjectDB.instance.GetItemPrefab(meat) : null);
				if (prefab != null)
				{
					pack.AddItem(prefab, StartAmount);
				}
			}
			if (tameable != null)
			{
				tameable.ResetFeedingTimer();
			}
		}

		/// <summary>
		/// Has her eat from her pack when she's hungry, on the client that owns her
		/// </summary>
		private void Update()
		{
			if (Time.time < nextEat || nview == null || !nview.IsValid() || !nview.IsOwner() || tameable == null)
			{
				return;
			}
			nextEat = Time.time + EatInterval;
			if (!tameable.IsHungry())
			{
				return;
			}
			ItemDrop.ItemData food = FirstFood(Pack());
			if (food == null)
			{
				return;
			}
			pack.RemoveOneItem(food);
			// The game's own feeding of a tame creature - its soothed effect, and fed from now
			tameable.OnConsumedItem(null);
		}

		/// <summary>
		/// Gets her pack as her ZDO has it now
		/// </summary>
		/// <returns>Her pack</returns>
		public Inventory Pack()
		{
			if (nview == null || !nview.IsValid())
			{
				return(pack);
			}
			string saved = nview.GetZDO().GetString(PackKey);
			if (saved != loaded)
			{
				loaded = saved;
				pack.m_onChanged -= Changed;
				if (saved.Length > 0)
				{
					pack.Load(new ZPackage(saved));
				}
				else
				{
					pack.RemoveAll();
				}
				pack.m_onChanged += Changed;
			}
			return(pack);
		}

		/// <summary>
		/// Checks whether she's hungry
		/// </summary>
		/// <returns>True when she is</returns>
		public bool Hungry()
		{
			return(tameable != null && tameable.IsHungry());
		}

		/// <summary>
		/// Writes her pack to her ZDO as it changes, on the client that owns her
		/// </summary>
		private void Changed()
		{
			if (nview == null || !nview.IsValid() || !nview.IsOwner())
			{
				return;
			}
			ZPackage saved = new ZPackage();
			pack.Save(saved);
			loaded = saved.GetBase64();
			nview.GetZDO().Set(PackKey, loaded);
		}

		/// <summary>
		/// Finds the first thing in a pack she'd eat
		/// </summary>
		/// <param name="inventory">The pack</param>
		/// <returns>The item, or null when there's nothing</returns>
		private static ItemDrop.ItemData FirstFood(Inventory inventory)
		{
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				if (Eats(item))
				{
					return(item);
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether she'd eat an item - anything a viking would, or raw meat that cooks into food
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when she would</returns>
		public static bool Eats(ItemDrop.ItemData item)
		{
			if (VikingFood.IsFood(item))
			{
				return(true);
			}
			return(item != null && item.m_dropPrefab != null && RawFoods().Contains(item.m_dropPrefab.name));
		}

		/// <summary>
		/// Finds every raw food that cooks into food, by what the game's cooking stations turn into food
		/// </summary>
		/// <returns>Their prefab names</returns>
		private static HashSet<string> RawFoods()
		{
			if (rawFoods != null || ZNetScene.instance == null)
			{
				return((rawFoods ?? new HashSet<string>()));
			}
			rawFoods = new HashSet<string>();
			foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
			{
				CookingStation station = ((prefab != null) ? prefab.GetComponent<CookingStation>() : null);
				if (station == null || station.m_conversion == null)
				{
					continue;
				}
				foreach (CookingStation.ItemConversion conversion in station.m_conversion)
				{
					if (conversion.m_from != null && conversion.m_to != null && conversion.m_to.m_itemData.m_shared.m_food > 0f)
					{
						rawFoods.Add(conversion.m_from.gameObject.name);
					}
				}
			}
			return(rawFoods);
		}
	}
}
