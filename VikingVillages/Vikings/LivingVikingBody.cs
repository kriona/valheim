using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A living viking's body, which works like a player's where the game only gives a player what a living viking
	/// needs - what it eats, its rest and the weather getting to it, how much it carries, sitting down and putting its
	/// gear on and taking it off
	/// </summary>
	internal class LivingVikingBody : VikingBody
	{
		/// <summary>
		/// The color the viking's job shows in under its name in debug mode, so it stands apart from the name - in the
		/// hover text, over the health bar and on other mods' markers, which all show the name as rich text
		/// </summary>
		private const string JobColor = "#C0C0C0";

		/// <summary>
		/// What shows after the name of a viking that doesn't know the local player, in JobColor
		/// </summary>
		private const string StrangerMark = "(?)";

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
		/// The forsaken powers the viking knows and uses
		/// </summary>
		public VikingPowers Powers { get; private set; }

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
		/// The viking's own frost modifier from its prefab, the name it was last checked for frost immunity by and
		/// whether that name makes it immune (see FrostImmune)
		/// </summary>
		private HitData.DamageModifier ownFrost;
		private string frostName;
		private bool frostImmune;

		/// <summary>
		/// Starts what the viking eats, its rest, the weather getting to it and its forsaken powers as the body wakes
		/// </summary>
		public override void Awake()
		{
			base.Awake();
			ownFrost = m_damageModifiers.m_frost;
			Food = new VikingFood(this);
			Rest = new VikingRested(this);
			Weather = new VikingWeather(this);
			Powers = new VikingPowers(this);
		}

		/// <summary>
		/// Checks whether the viking is immune to frost by its name (see LivingVikingBodyRules.FrostImmune) - frost
		/// damage, the frost effect a hit gives and the weather's cold and freezing - keeping its own frost modifier Immune
		/// while it is, since that's the one the game checks before a hit gives it the frost effect
		/// </summary>
		/// <returns>True when it is</returns>
		public bool FrostImmune()
		{
			if (m_nview == null || !m_nview.IsValid())
			{
				return(false);
			}
			VikingNames.ReadRaw(m_nview.GetZDO(), out string name, out string _);
			if (name != frostName)
			{
				frostName = name;
				frostImmune = LivingVikingBodyRules.FrostImmune(name);
				m_damageModifiers.m_frost = (frostImmune ? HitData.DamageModifier.Immune : ownFrost);
			}
			return(frostImmune);
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
		/// Speeds the viking's run by its Run skill the way a player's is (see VikingCore.RunSkillBonus), and by what its
		/// gear does to its speed half again as much, as a player's run is (see GetEquipmentMovementModifier)
		/// </summary>
		/// <returns>Its run speed factor</returns>
		public override float GetRunSpeedFactor()
		{
			float factor = base.GetRunSpeedFactor();
			float level = ((viking != null) ? viking.SkillLevel(Skills.SkillType.Run) : 0f);
			if (level > 0f)
			{
				factor *= VikingCore.SkillBoost(level, VikingCore.RunSkillBonus);
			}
			return(factor * (1f + GetEquipmentMovementModifier() * 1.5f));
		}

		/// <summary>
		/// Speeds or slows the viking's jog by what its gear does to its speed, the way a player's is (see
		/// GetEquipmentMovementModifier)
		/// </summary>
		/// <returns>Its jog speed factor</returns>
		public override float GetJogSpeedFactor()
		{
			return(1f + GetEquipmentMovementModifier());
		}

		/// <summary>
		/// Adds up what the gear the viking wears and holds does to its speed, the way the game adds up a player's - each
		/// equipped piece's movement modifier, like a Fenris piece's bonus or iron armor's, a heavy weapon's or a tower
		/// shield's penalty. A weapon it has put away counts for nothing, the same as one on a player's back. The game
		/// also goes by it for how far the viking is pushed and what a jump costs it
		/// </summary>
		/// <returns>The modifier - 0.03 for 3% faster, -0.05 for 5% slower</returns>
		public override float GetEquipmentMovementModifier()
		{
			return(Movement(m_rightItem) + Movement(m_leftItem) + Movement(m_chestItem) + Movement(m_legItem) + Movement(m_helmetItem) + Movement(m_shoulderItem) + Movement(m_utilityItem) + Movement(m_trinketItem));
		}

		/// <summary>
		/// Gets what an equipped piece does to its wearer's speed
		/// </summary>
		/// <param name="item">The piece, or null for an empty slot</param>
		/// <returns>Its movement modifier, or 0 for an empty slot</returns>
		private static float Movement(ItemDrop.ItemData item)
		{
			return((item != null) ? item.m_shared.m_movementModifier : 0f);
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
			return(LivingVikingBodyRules.FormatWeight(weight, max, Mathf.Sin(Time.time * 10f) > 0f));
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
		/// Checks whether the viking takes fall damage the way a player does
		/// </summary>
		/// <returns>True when it does</returns>
		public virtual bool TakesFallDamage()
		{
			return(true);
		}

		/// <summary>
		/// Keeps the height the viking falls from at where it stands while another client owns it, since the game only
		/// follows it on the client that owns it - so a viking handed back to this client doesn't land from where it was
		/// when this client last had it
		/// </summary>
		/// <param name="fixedDeltaTime">Seconds since the last physics update</param>
		public override void CustomFixedUpdate(float fixedDeltaTime)
		{
			base.CustomFixedUpdate(fixedDeltaTime);
			if (m_nview != null && m_nview.IsValid() && !m_nview.IsOwner())
			{
				m_maxAirAltitude = transform.position.y;
			}
			else if (m_nview != null && m_nview.IsValid())
			{
				RegainStamina(fixedDeltaTime);
			}
		}

		/// <summary>
		/// The viking's stamina, which it only uses swimming, or a negative number while it's full - kept on its owner
		/// </summary>
		private float stamina = -1f;

		/// <summary>
		/// When the viking last used stamina, in Time.time seconds, and how long it has been drowning since it last took
		/// damage for it
		/// </summary>
		private float staminaUsedAt;
		private float drownTimer;

		/// <summary>
		/// The most stamina the viking can have, as last worked out, and when it's next worked out, in Time.time seconds
		/// </summary>
		private float maxStamina;
		private float nextMaxStamina;

		/// <summary>
		/// Seconds between workings out of how much stamina the viking can have, which its food changes slowly
		/// </summary>
		private const float MaxStaminaInterval = 1f;

		/// <summary>
		/// Gets the most stamina the viking can have - a player's base stamina and what its food gives, the way a
		/// player's is (see Player.GetMaxStamina)
		/// </summary>
		/// <returns>The stamina</returns>
		public float MaxStamina()
		{
			if (Time.time >= nextMaxStamina)
			{
				nextMaxStamina = Time.time + MaxStaminaInterval;
				maxStamina = BoatRules.BaseStamina + ((Food != null) ? Food.FoodStamina() : 0f);
			}
			return(maxStamina);
		}

		/// <summary>
		/// Gets the stamina the viking has left
		/// </summary>
		/// <returns>The stamina</returns>
		public float Stamina()
		{
			return((stamina < 0f) ? MaxStamina() : Mathf.Min(stamina, MaxStamina()));
		}

		/// <summary>
		/// Checks whether the viking is drowning - in the water with no stamina left
		/// </summary>
		/// <returns>True while it is</returns>
		public bool Drowning()
		{
			return(IsSwimming() && Stamina() <= 0f);
		}

		/// <summary>
		/// Uses the viking's stamina as it swims and drowns it once it has none left, the way the game does a player's
		/// (see Player.OnSwimming) - stamina drains only while it swims somewhere, by its Swim skill and effects at the
		/// world's stamina rate, and with none left it loses a share of its max health every second (see
		/// BoatRules.DrownDamage)
		/// </summary>
		/// <param name="targetVel">Where it's swimming to</param>
		/// <param name="dt">Seconds since the last physics update</param>
		public override void OnSwimming(Vector3 targetVel, float dt)
		{
			base.OnSwimming(targetVel, dt);
			if (targetVel.magnitude > 0.1f)
			{
				float drain = BoatRules.SwimDrain(GetSkillFactor(Skills.SkillType.Swim));
				m_seman.ModifySwimStaminaUsage(drain, ref drain);
				stamina = Mathf.Max(0f, Stamina() - dt * drain * Game.m_moveStaminaRate);
				staminaUsedAt = Time.time;
			}
			if (Stamina() > 0f)
			{
				return;
			}
			drownTimer += dt;
			if (drownTimer <= 1f)
			{
				return;
			}
			drownTimer = 0f;
			HitData hit = new HitData();
			hit.m_damage.m_damage = BoatRules.DrownDamage(GetMaxHealth());
			hit.m_point = GetCenterPoint();
			hit.m_dir = Vector3.down;
			hit.m_pushForce = 10f;
			hit.m_hitType = HitData.HitType.Drowning;
			Damage(hit);
		}

		/// <summary>
		/// Gives the viking its stamina back the way a player gets theirs (see Player.UpdateStats) - not while it's in the
		/// water off the ground or carrying more than it can, and only StaminaRegenDelay seconds after it last used any,
		/// by its effects and the world's stamina regen rate
		/// </summary>
		/// <param name="dt">Seconds since the last physics update</param>
		private void RegainStamina(float dt)
		{
			if (stamina < 0f || (IsSwimming() && !IsOnGround()) || IsEncumbered() || Time.time - staminaUsedAt < BoatRules.StaminaRegenDelay)
			{
				return;
			}
			float max = MaxStamina();
			float multiplier = 1f;
			m_seman.ModifyStaminaRegen(ref multiplier);
			stamina += BoatRules.StaminaRegain(stamina, max) * multiplier * dt * Game.m_staminaRegenRate;
			if (stamina >= max)
			{
				stamina = -1f;
			}
		}

		/// <summary>
		/// Gets how far the viking has fallen as it touches the ground, measured the way the game measures a player's fall
		/// (see Character.UpdateGroundContact), before the game sets the height it fell from back to where it stands
		/// </summary>
		/// <returns>The height, in meters, or 0 when it isn't touching the ground or takes no fall damage</returns>
		public float LandingHeight()
		{
			if (!m_groundContact || !TakesFallDamage())
			{
				return(0f);
			}
			return(Mathf.Max(0f, m_maxAirAltitude - transform.position.y));
		}

		/// <summary>
		/// Hurts the viking for a fall the way the game hurts a player - by the fall's height, less what its effects take
		/// off it, like a feather cape's (see Character.UpdateGroundContact) - through its armor (see VikingArmorPatch)
		/// </summary>
		/// <param name="height">How far it fell, in meters</param>
		public void Land(float height)
		{
			float damage = LivingVikingBodyRules.FallDamage(height);
			if (damage <= 0f)
			{
				return;
			}
			m_seman.ModifyFallDamage(damage, ref damage);
			if (damage <= 0f)
			{
				return;
			}
			HitData hit = new HitData();
			hit.m_damage.m_damage = damage;
			hit.m_point = m_lastGroundPoint;
			hit.m_dir = m_lastGroundNormal;
			hit.m_hitType = HitData.HitType.Fall;
			Damage(hit);
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
			float delta = LivingVikingBodyRules.RegenDelta(GetHealth() - before, multiplier);
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
			GearChange queued = LivingVikingBodyRules.QueueChange(gearChanges, Time.time, item.m_shared.m_itemType, item.m_shared.m_equipDuration, putOn, chosen, found);
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
		/// Shows the viking's equipment, with the weapon and shield it has sheathed on its back, which the game only shows
		/// for a player
		/// </summary>
		/// <param name="visEq">Its equipment visuals</param>
		/// <param name="isRagdoll">Whether they're its corpse's</param>
		public override void SetupVisEquipment(VisEquipment visEq, bool isRagdoll)
		{
			base.SetupVisEquipment(visEq, isRagdoll);
			if (isRagdoll || visEq == null)
			{
				return;
			}
			ItemDrop.ItemData left = m_hiddenLeftItem;
			ItemDrop.ItemData right = m_hiddenRightItem;
			visEq.SetLeftBackItem((left != null && left.m_dropPrefab != null) ? left.m_dropPrefab.name.GetStableHashCode() : 0, ((left != null) ? left.m_variant : 0), ((left != null) ? left.m_quality : 0));
			visEq.SetRightBackItem((right != null && right.m_dropPrefab != null) ? right.m_dropPrefab.name.GetStableHashCode() : 0, ((right != null) ? right.m_quality : 0));
		}

		/// <summary>
		/// Gets the viking's name as the local player knows it - the name a player gave it once it knows them, or else
		/// plain Viking with "(?)" after it while it doesn't, until it greets them or they talk to it - with what it's
		/// doing on a line under it in debug mode, wherever its name shows: its hover text, the health bar over it (see
		/// VikingHudName), and other mods' markers that read its name
		/// </summary>
		/// <returns>Its name</returns>
		public override string GetHoverName()
		{
			string name = KnownName();
			if (!Player.m_debugMode || viking == null)
			{
				return(name);
			}
			string job = viking.DebugJob();
			return(string.IsNullOrEmpty(job) ? name : name + "\n<color=" + JobColor + ">" + job + "</color>");
		}

		/// <summary>
		/// Gets the viking's name as the local player knows it - the name a player gave it once it knows them, or else
		/// plain Viking with "(?)" after it while it doesn't - without what it's doing, which shows under it in debug mode
		/// (see GetHoverName)
		/// </summary>
		/// <returns>Its name</returns>
		public string KnownName()
		{
			string given = ShownName();
			if (given.Length > 0)
			{
				return(given);
			}
			string name = base.GetHoverName();
			return(Stranger() ? name + " <color=" + JobColor + ">" + StrangerMark + "</color>" : name);
		}

		/// <summary>
		/// Checks whether the viking doesn't know the local player yet - hasn't greeted them, been talked to or been named
		/// by them
		/// </summary>
		/// <returns>True when it doesn't, false when it does or there's no local player</returns>
		public bool Stranger()
		{
			Player player = Player.m_localPlayer;
			return(player != null && !VikingSpeech.Knows(this, player.GetPlayerID()));
		}

		/// <summary>
		/// Gets the name a player gave the viking, as the local player may see it - only once the viking knows them
		/// </summary>
		/// <returns>The name, or "" when it has none or doesn't know the local player</returns>
		public string ShownName()
		{
			return(Stranger() ? "" : VikingNames.Get(this));
		}

		/// <summary>
		/// Puts the viking's name and the keys to talk to it, name it and open its inventory ahead of the game's hover
		/// text, when the local player isn't its enemy
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
			if (VikingNames.CanName())
			{
				string alt = ((ZInput.IsNonClassicFunctionality() && ZInput.IsGamepadActive()) ? "$KEY_AltKeys" : "$KEY_AltPlace");
				talk += "\n" + Localization.instance.Localize("[<color=yellow><b>" + alt + " + $KEY_Use</b></color>] Name");
			}
			talk += "\n[<color=yellow><b>" + VikingInventory.InventoryKey + "</b></color>] Inventory";
			return(string.IsNullOrEmpty(text) ? talk : talk + "\n" + text);
		}
	}

	/// <summary>
	/// What a living viking's body works out without its body - when its gear changes start and are done, how much its
	/// rest changes what the game heals it by, how much a fall hurts it, how its weight shows and whose name makes it
	/// immune to frost
	/// </summary>
	internal static class LivingVikingBodyRules
	{
		/// <summary>
		/// Checks whether a viking's name makes it immune to frost - Elsa, whatever its case and byname (see
		/// VikingNames.IsElsa)
		/// </summary>
		/// <param name="name">The viking's name, or null</param>
		/// <returns>True when it does</returns>
		internal static bool FrostImmune(string name)
		{
			return(VikingNames.IsElsa(name));
		}

		/// <summary>
		/// Queues putting on or taking off a piece of gear, taking its equip time from when the last change queued is
		/// done, or from now when nothing is queued. A one-handed weapon or shield joins the last change queued when
		/// that's the same change to the other hand, with nothing joined to it yet, and the change is done when the slower
		/// of the two is
		/// </summary>
		/// <param name="changes">The changes queued, in order, which a new change goes on the end of</param>
		/// <param name="now">The time now, in seconds</param>
		/// <param name="type">The piece's item type</param>
		/// <param name="equipDuration">The piece's equip time, in seconds, where below 0 counts as 0</param>
		/// <param name="putOn">True to put it on, false to take it off</param>
		/// <param name="chosen">Whether a player picked it for the viking to put on</param>
		/// <param name="found">Whether the viking found it as loot or in a chest</param>
		/// <returns>The change the piece is part of - the last one, Paired, when it joined it as the other hand's piece,
		/// or else a new one for it</returns>
		internal static LivingVikingBody.GearChange QueueChange(List<LivingVikingBody.GearChange> changes, float now, ItemDrop.ItemData.ItemType type, float equipDuration, bool putOn, bool chosen, bool found)
		{
			float duration = Mathf.Max(0f, equipDuration);
			LivingVikingBody.GearChange last = ((changes.Count > 0) ? changes[changes.Count - 1] : null);
			if (last != null && !last.Paired && last.PutOn == putOn && last.Chosen == chosen && last.Found == found && HandPair(last.Type, type))
			{
				last.Paired = true;
				last.DoneAt = Mathf.Max(last.DoneAt, last.Start + duration);
				return(last);
			}
			float start = ((last != null) ? last.DoneAt : now);
			LivingVikingBody.GearChange change = new LivingVikingBody.GearChange { Type = type, PutOn = putOn, Chosen = chosen, Found = found, Start = start, DoneAt = start + duration };
			changes.Add(change);
			return(change);
		}

		/// <summary>
		/// Checks whether two pieces of gear go in a hand each - a one-handed weapon and a shield - so changing them is
		/// one change, done together
		/// </summary>
		/// <param name="first">One piece's item type</param>
		/// <param name="second">The other's</param>
		/// <returns>True when they do</returns>
		internal static bool HandPair(ItemDrop.ItemData.ItemType first, ItemDrop.ItemData.ItemType second)
		{
			return((first == ItemDrop.ItemData.ItemType.OneHandedWeapon && second == ItemDrop.ItemData.ItemType.Shield) || (first == ItemDrop.ItemData.ItemType.Shield && second == ItemDrop.ItemData.ItemType.OneHandedWeapon));
		}

		/// <summary>
		/// Works out how much more or less the viking heals than the game healed it, by its rest's regen multiplier -
		/// more while resting or rested, less while wet or cold
		/// </summary>
		/// <param name="healed">How much the game healed it</param>
		/// <param name="multiplier">Its regen multiplier, 1 for no change</param>
		/// <returns>The health to add, below 0 for health to take back, or 0 when the game didn't heal it</returns>
		internal static float RegenDelta(float healed, float multiplier)
		{
			return((healed > 0f) ? healed * (multiplier - 1f) : 0f);
		}

		/// <summary>
		/// How far past Character.c_MinFallHeight a fall's damage rises to Character.c_MaxFallDamage, in meters - the
		/// game's own number for a player (see Character.UpdateGroundContact)
		/// </summary>
		private const float FallSpread = 16f;

		/// <summary>
		/// Works out how much a fall hurts the viking, the same as a player - nothing up to Character.c_MinFallHeight,
		/// rising evenly to Character.c_MaxFallDamage at FallSpread meters past it
		/// </summary>
		/// <param name="height">How far it fell, in meters</param>
		/// <returns>The damage, before what its effects take off it</returns>
		internal static float FallDamage(float height)
		{
			if (height <= Character.c_MinFallHeight)
			{
				return(0f);
			}
			return(Mathf.Clamp01((height - Character.c_MinFallHeight) / FallSpread) * Character.c_MaxFallDamage);
		}

		/// <summary>
		/// Writes how much the viking carries and the most it can, like "120/300", with the weight in red while it
		/// carries more than it can and the flash is on
		/// </summary>
		/// <param name="weight">The weight it carries</param>
		/// <param name="max">The most it can carry</param>
		/// <param name="flash">Whether the flash is on right now, as it blinks</param>
		/// <returns>The text</returns>
		internal static string FormatWeight(int weight, int max, bool flash)
		{
			bool red = (flash && weight > max);
			return((red ? "<color=red>" + weight + "</color>" : weight.ToString()) + "/" + max);
		}
	}
}
