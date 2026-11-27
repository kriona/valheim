using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The body of a viking that's alive - a PC, NPC or dvergr viking - which moves, swims, falls and shows its name the
	/// way a player does, knows the forsaken powers it was given and can be immune to frost by its name
	/// </summary>
	internal abstract class LivingVikingBody : VikingBody
	{
		/// <summary>
		/// The color the viking's job shows in under its name in debug mode, so it stands apart from the name - in the
		/// hover text, over the health bar and on other mods' markers, which all show the name as rich text
		/// </summary>
		protected const string JobColor = "#C0C0C0";

		/// <summary>
		/// What shows after the name of a viking that doesn't know the local player, in JobColor
		/// </summary>
		private const string StrangerMark = "(?)";

		/// <summary>
		/// What shows after an NPC viking's name in debug mode, and its color
		/// </summary>
		private const string NPCMark = "(NPC)";
		private const string NPCColor = "#33CCFF";

		/// <summary>
		/// What shows after a traveler's name in debug mode, and its color
		/// </summary>
		private const string TravelerMark = "(Traveler)";
		private const string TravelerColor = "#FF66CC";

		/// <summary>
		/// The forsaken powers the viking knows and uses
		/// </summary>
		public VikingPowers Powers { get; private set; }

		/// <summary>
		/// The viking's own frost modifier from its prefab, the name it was last checked for frost immunity by and
		/// whether that name makes it immune (see FrostImmune)
		/// </summary>
		private HitData.DamageModifier ownFrost;
		private string frostName;
		private bool frostImmune;

		/// <summary>
		/// Starts the viking's forsaken powers as the body wakes, and notes it as lost when a console command just made it
		/// (see VillageTravel.NoteSpawned)
		/// </summary>
		public override void Awake()
		{
			base.Awake();
			if (m_nview != null && m_nview.GetZDO() != null)
			{
				VillageTravel.NoteSpawned(m_nview.GetZDO());
			}
			ownFrost = m_damageModifiers.m_frost;
			Powers = new VikingPowers(this);
		}

		/// <summary>
		/// Checks whether the viking is immune to frost by its name (see VikingBodyRules.FrostImmune) - frost damage, the
		/// frost effect a hit gives and the weather's cold and freezing - keeping its own frost modifier Immune while it
		/// is, since that's the one the game checks before a hit gives it the frost effect
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
				frostImmune = VikingBodyRules.FrostImmune(name);
				m_damageModifiers.m_frost = (frostImmune ? HitData.DamageModifier.Immune : ownFrost);
			}
			return(frostImmune);
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
		/// Gets the stamina the viking's food gives it, on top of a player's base stamina
		/// </summary>
		/// <returns>The stamina - none for a viking that doesn't eat</returns>
		public virtual float FoodStamina()
		{
			return(0f);
		}

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
				maxStamina = BoatRules.BaseStamina + FoodStamina();
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
			float damage = VikingBodyRules.FallDamage(height);
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
		/// plain Viking with "(?)" after it while it doesn't, until it greets them or they talk to it - with, in debug
		/// mode, "(NPC)" after an NPC viking's name in NPCColor, "(Traveler)" after a traveler's in TravelerColor, and what it's doing on a line under it, wherever its name
		/// shows: its hover text, the health bar over it (see VikingHudName), and other mods' markers that read its name
		/// </summary>
		/// <returns>Its name</returns>
		public override string GetHoverName()
		{
			string name = KnownName();
			if (!Player.m_debugMode || viking == null)
			{
				return(name);
			}
			if (VikingCore.IsNPCViking(this))
			{
				name += " <color=" + NPCColor + ">" + NPCMark + "</color>";
			}
			if (Traveler.Is(this))
			{
				name += " <color=" + TravelerColor + ">" + TravelerMark + "</color>";
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
	}
}
