using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The body of a viking who lives the way a player does - a PC or NPC viking - which works like a player's where the
	/// game only gives a player what it needs: what it eats, its rest and the weather getting to it, how much it carries,
	/// stalking prey, sitting down and putting its gear on and taking it off
	/// </summary>
	internal abstract class PlayerLikeVikingBody : LivingVikingBody
	{
		/// <summary>
		/// The most weight the viking carries, everything in its inventory together, before what its effects add (see
		/// GetMaxCarryWeight) - as much as a player can
		/// </summary>
		public const float MaxCarryWeight = 300f;

		/// <summary>
		/// The animator bool the player's equipping plays with, which the viking plays too
		/// </summary>
		private const string EquippingAnimation = "equipping";

		/// <summary>
		/// Putting on or taking off a piece of gear, which takes the piece's equip time the way it does a player - with
		/// a one-handed weapon and a shield as one change done together, the way a player's R key draws or sheathes both
		/// hands at once, taking as long as the slower of the two
		/// </summary>
		public class GearChange
		{
			public ItemDrop.ItemData Item;
			public ItemDrop.ItemData Partner;

			/// <summary>
			/// Item's type, and whether the change has a piece for the other hand joined to it (see Partner)
			/// </summary>
			public ItemDrop.ItemData.ItemType Type;
			public bool Paired;

			public bool PutOn;
			public bool Chosen;
			public bool Found;
			public float Start;
			public float DoneAt;
		}

		/// <summary>
		/// The viking's life in its village, found once VikingCore has added it
		/// </summary>
		private VillageLife life;

		/// <summary>
		/// What the viking has eaten, its rest and the weather getting to it, the way a player's work
		/// </summary>
		public VikingFood Food { get; private set; }
		public VikingRested Rest { get; private set; }
		public VikingWeather Weather { get; private set; }

		/// <summary>
		/// Whether the viking carries more than it can (see GetMaxCarryWeight), as noted on its owner (see
		/// UpdateEncumbered)
		/// </summary>
		private bool encumbered;

		/// <summary>
		/// Whether the viking lies on a bed or sits on a chair
		/// </summary>
		private bool seated;

		/// <summary>
		/// The gear the viking is putting on or taking off, one after another, and whether it's playing the equipping
		/// animation for them
		/// </summary>
		private readonly List<GearChange> gearChanges = new List<GearChange>();
		private bool equipping;

		/// <summary>
		/// Starts what the viking eats, its rest and the weather getting to it as the body wakes
		/// </summary>
		public override void Awake()
		{
			base.Awake();
			Food = new VikingFood(this);
			Rest = new VikingRested(this);
			Weather = new VikingWeather(this);
		}

		/// <summary>
		/// Gets the viking's life in its village
		/// </summary>
		/// <returns>Its life, or null before it has one</returns>
		public VillageLife Life()
		{
			if (life == null)
			{
				life = GetComponent<VillageLife>();
			}
			return(life);
		}

		/// <summary>
		/// Makes the viking's blow at an enemy its weapon's power attack when that's a smart move (see
		/// VillageLife.UsesPowerAttack), and otherwise the blow it was asked for
		/// </summary>
		/// <param name="target">What it attacks, or null</param>
		/// <param name="secondaryAttack">Whether it's the weapon's secondary attack</param>
		/// <returns>Whether the attack started</returns>
		public override bool StartAttack(Character target, bool secondaryAttack)
		{
			VillageLife own = Life();
			if (!secondaryAttack && own != null && own.UsesPowerAttack(target) && base.StartAttack(target, true))
			{
				own.PowerAttacked(target);
				return(true);
			}
			return(base.StartAttack(target, secondaryAttack));
		}

		/// <summary>
		/// Gets the stamina the viking's food gives it, on top of a player's base stamina
		/// </summary>
		/// <returns>The stamina</returns>
		public override float FoodStamina()
		{
			return((Food != null) ? Food.FoodStamina() : 0f);
		}

		/// <summary>
		/// Works out the most weight the viking carries the way a player's is - MaxCarryWeight, with what its effects
		/// add, like a strength belt it wears, scaled by the world's carry weight rate
		/// </summary>
		/// <returns>The weight</returns>
		public float GetMaxCarryWeight()
		{
			float limit = MaxCarryWeight;
			if (GetSEMan() != null)
			{
				GetSEMan().ModifyMaxCarryWeight(MaxCarryWeight, ref limit);
			}
			return(limit * Game.m_carryWeightRate);
		}

		/// <summary>
		/// Writes how much the viking carries and the most it can, the way the player's weight shows - flashing red while
		/// it carries more
		/// </summary>
		/// <returns>The text, like "120/300"</returns>
		public string WeightText()
		{
			int weight = Mathf.CeilToInt(GetInventory().GetTotalWeight());
			int max = Mathf.CeilToInt(GetMaxCarryWeight());
			return(VikingBodyRules.FormatWeight(weight, max, Mathf.Sin(Time.time * 10f) > 0f));
		}

		/// <summary>
		/// Notes whether the viking carries more than it can, sweating with the game's Encumbered effect the way a
		/// player does while it does, on its owner
		/// </summary>
		public void UpdateEncumbered()
		{
			bool over = (GetInventory().GetTotalWeight() > GetMaxCarryWeight());
			if (over == encumbered)
			{
				return;
			}
			encumbered = over;
			if (over)
			{
				GetSEMan().AddStatusEffect(SEMan.s_statusEffectEncumbered, false, 0, 0f, -1);
			}
			else
			{
				GetSEMan().RemoveStatusEffect(SEMan.s_statusEffectEncumbered);
			}
		}

		/// <summary>
		/// Counts the viking as encumbered while it carries more than it can, the way the game counts a player - slowed
		/// to a crouch-walk with the heavy walk, and kept from running, jumping and blocking
		/// </summary>
		/// <returns>True while it's encumbered</returns>
		public override bool IsEncumbered()
		{
			return(base.IsEncumbered() || encumbered);
		}

		/// <summary>
		/// Whether the viking is stalking prey, when it next notes how hard it is to see and raises its Sneak skill while
		/// it is, and the animator's crouching parameter, looked up the first time it's needed
		/// </summary>
		private bool stalking;
		private float nextStealthNote;
		private static int crouchingHash;

		/// <summary>
		/// Seconds between the viking's notes of how hard it is to see while it stalks, and how much each raises its Sneak
		/// skill, as the game gives it
		/// </summary>
		private const float StealthNoteInterval = 1f;
		private const float SneakRaise = 1f;

		/// <summary>
		/// Has the viking stalk prey, or stop - crouched in the player's sneaking pose, it moves at crouching speed and its
		/// steps make no sound for the prey to hear (see IsCrouching), and it's harder to see (see GetStealthFactor), its
		/// Sneak skill rising as it goes, the way a player's does
		/// </summary>
		/// <param name="on">Whether it stalks</param>
		public void SetStalking(bool on)
		{
			if (on == stalking)
			{
				if (on && Time.time >= nextStealthNote)
				{
					NoteStealth();
					viking.RaiseSkill(Skills.SkillType.Sneak, SneakRaise);
				}
				return;
			}
			stalking = on;
			if (crouchingHash == 0)
			{
				crouchingHash = ZSyncAnimation.GetHash("crouching");
			}
			m_zanim.SetBool(crouchingHash, on);
			NoteStealth();
		}

		/// <summary>
		/// Counts the viking as crouching while it stalks, as the game counts a sneaking player - moving at crouching
		/// speed with no sound from its steps
		/// </summary>
		/// <returns>True while it stalks</returns>
		public override bool IsCrouching()
		{
			return(stalking);
		}

		/// <summary>
		/// Gets how much of a creature's sight the viking can be seen within, the way a player's is worked out - all of
		/// it standing tall, and while it stalks, from half of it at no Sneak skill in the dark to a fifth at full skill,
		/// more of it in the light - read from its ZDO on any client but its owner's
		/// </summary>
		/// <returns>The share, from 0 to 1</returns>
		public override float GetStealthFactor()
		{
			if (m_nview == null || !m_nview.IsValid())
			{
				return(1f);
			}
			return(m_nview.IsOwner() ? Stealth() : m_nview.GetZDO().GetFloat(ZDOVars.s_stealth, 1f));
		}

		/// <summary>
		/// Works out how hard the viking is to see now (see GetStealthFactor)
		/// </summary>
		/// <returns>The share, from 0 to 1</returns>
		private float Stealth()
		{
			if (!stalking)
			{
				return(1f);
			}
			float skill = Mathf.Clamp01(viking.SkillLevel(Skills.SkillType.Sneak) / 100f);
			float light = ((StealthSystem.instance != null) ? StealthSystem.instance.GetLightFactor(GetCenterPoint()) : 1f);
			return(Mathf.Clamp01(Mathf.Lerp(0.5f + light * 0.5f, 0.2f + light * 0.4f, skill)));
		}

		/// <summary>
		/// Notes how hard the viking is to see in its ZDO, for the other clients' creatures to see it by
		/// </summary>
		private void NoteStealth()
		{
			nextStealthNote = Time.time + StealthNoteInterval;
			if (m_nview != null && m_nview.IsValid() && m_nview.IsOwner())
			{
				m_nview.GetZDO().Set(ZDOVars.s_stealth, Stealth());
			}
		}

		/// <summary>
		/// Notes whether the viking lies on a bed or sits on a chair, which the game counts as attached the way it does a
		/// player sitting down - so its collider isn't pushed around by what's near it, its body doesn't bend to look
		/// around and its model doesn't tilt to the slope under it
		/// </summary>
		/// <param name="value">Whether it's seated</param>
		public void SetSeated(bool value)
		{
			seated = value;
		}

		/// <summary>
		/// Counts the viking lying on a bed or sitting on a chair as attached, the way the game counts a player sitting
		/// down, so the game holds it still the same way
		/// </summary>
		/// <returns>True while it's attached</returns>
		public override bool IsAttached()
		{
			return(base.IsAttached() || seated);
		}

		/// <summary>
		/// Gives the viking the weather's effects once the game has healed it, adds the extra healing resting and being
		/// rested give to what the game healed, or takes back what being wet or cold slows it by, and heals it by its
		/// food, by as much as each does a player - a creature heals without reading its status effects, so the extra
		/// is in proportion to what the game healed (see VikingRegenPatch)
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="before">The viking's health before the game healed it</param>
		public void AfterRegeneration(float dt, float before)
		{
			Weather.Update();
			float multiplier = Rest.RegenMultiplier();
			float delta = VikingBodyRules.RegenDelta(GetHealth() - before, multiplier);
			if (delta > 0f)
			{
				Heal(delta, false);
			}
			else if (delta < 0f)
			{
				SetHealth(GetHealth() + delta);
			}
			Food.Update(dt, multiplier);
		}

		/// <summary>
		/// Has the viking take off a piece of gear it wears - equipped, or sheathed on its back - taking the piece's equip
		/// time the way a player does, after anything it's already putting on or taking off
		/// </summary>
		/// <param name="item">The piece</param>
		public void TakeOffGear(ItemDrop.ItemData item)
		{
			QueueGear(item, false, false);
		}

		/// <summary>
		/// Has the viking put on a piece of gear a player picked for it, whether or not it's better than its own (see
		/// VikingCore.Wear), taking the piece's equip time the way a player does, after anything it's already putting
		/// on or taking off
		/// </summary>
		/// <param name="item">The piece</param>
		public void PutOnGear(ItemDrop.ItemData item)
		{
			QueueGear(item, true, true);
		}

		/// <summary>
		/// Checks whether the viking is about to put on or take off a piece of gear
		/// </summary>
		/// <param name="item">The piece</param>
		/// <returns>True when it is</returns>
		public bool GearQueued(ItemDrop.ItemData item)
		{
			return(gearChanges.Exists((GearChange change) => change.Item == item || change.Partner == item));
		}

		/// <summary>
		/// Checks whether any piece a change is for is still in the viking's inventory
		/// </summary>
		/// <param name="change">The change</param>
		/// <returns>True when one is</returns>
		private bool StillHas(GearChange change)
		{
			Inventory inventory = GetInventory();
			return(inventory.ContainsItem(change.Item) || (change.Partner != null && inventory.ContainsItem(change.Partner)));
		}

		/// <summary>
		/// Queues putting on or taking off a piece of gear, taking the piece's equip time from when the last change
		/// queued is done - or does it at once for a piece with no equip time. A one-handed weapon or shield joins the
		/// last change queued when that's the same change to the other hand, so both are done together
		/// </summary>
		/// <param name="item">The piece</param>
		/// <param name="putOn">True to put it on, false to take it off</param>
		/// <param name="chosen">Whether a player picked it for the viking to put on</param>
		/// <param name="found">Whether the viking found it as loot or in a chest, so what it replaces goes to a chest,
		/// and so does the piece itself when it turns out not to be better after all</param>
		public void QueueGear(ItemDrop.ItemData item, bool putOn, bool chosen, bool found = false)
		{
			if (item == null || GearQueued(item))
			{
				return;
			}
			GearChange queued = VikingBodyRules.QueueChange(gearChanges, Time.time, item.m_shared.m_itemType, item.m_shared.m_equipDuration, putOn, chosen, found);
			if (queued.Paired)
			{
				queued.Partner = item;
			}
			else
			{
				queued.Item = item;
			}
		}

		/// <summary>
		/// Carries on putting on and taking off the gear queued, playing the player's equipping animation while it does,
		/// and making each change as its time is up - leaving out a piece that's no longer in its inventory - on its
		/// owner
		/// </summary>
		/// <param name="change">Makes a change to one of its pieces - its item, or the one in the other hand</param>
		public void UpdateGearChanges(System.Action<GearChange, ItemDrop.ItemData> change)
		{
			Inventory inventory = GetInventory();
			while (gearChanges.Count > 0 && (Time.time >= gearChanges[0].DoneAt || !StillHas(gearChanges[0])))
			{
				GearChange done = gearChanges[0];
				gearChanges.RemoveAt(0);
				if (inventory.ContainsItem(done.Item))
				{
					change(done, done.Item);
				}
				if (done.Partner != null && inventory.ContainsItem(done.Partner))
				{
					change(done, done.Partner);
				}
			}
			bool busy = (gearChanges.Count > 0);
			if (busy != equipping && m_zanim != null)
			{
				equipping = busy;
				m_zanim.SetBool(EquippingAnimation, busy);
			}
		}

		/// <summary>
		/// Puts the viking's name and the keys to talk to it, name it and open its inventory ahead of the game's hover
		/// text, when the local player isn't its enemy - only the key to talk to a traveler, which can't be named and
		/// keeps its pack to itself
		/// </summary>
		/// <param name="text">The game's hover text</param>
		/// <returns>The hover text</returns>
		protected override string HoverText(string text)
		{
			VillageLife own = Life();
			if (own == null || !own.CanTalkTo(Player.m_localPlayer))
			{
				return(text);
			}
			string talk = GetHoverName() + "\n" + Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>] Talk");
			if (!own.IsTraveler())
			{
				if (VikingNames.CanName())
				{
					string alt = ((ZInput.IsNonClassicFunctionality() && ZInput.IsGamepadActive()) ? "$KEY_AltKeys" : "$KEY_AltPlace");
					talk += "\n" + Localization.instance.Localize("[<color=yellow><b>" + alt + " + $KEY_Use</b></color>] Name");
				}
				talk += "\n[<color=yellow><b>" + VikingInventory.InventoryKey + "</b></color>] Inventory";
			}
			return(string.IsNullOrEmpty(text) ? talk : talk + "\n" + text);
		}
	}
}
