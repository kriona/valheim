using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// How a valkyrie fights - the Fallen Valkyrie's health, resistances, AI and attacks, with her own changes: no spit or
	/// taunt, her claws and spin dealing spirit damage, her spin's effect white, immune to spirit and stagger, no weaker to fire
	/// or poison than anything else and resistant to lightning, dropping its feathers but never its trophy
	/// </summary>
	internal static class ValkyrieCombat
	{
		/// <summary>
		/// The creature she fights like
		/// </summary>
		public const string FallenPrefab = "FallenValkyrie";

		/// <summary>
		/// The names her copies of the Fallen Valkyrie's attacks, and her spin's white effect, are registered by - each
		/// copy's item takes its name too, which tells her attacks apart, since her spin and feather volley play the same
		/// animation
		/// </summary>
		internal const string ClawsName = "ValkyrieClaws";
		internal const string AirClawsName = "ValkyrieAirClaws";
		internal const string SpinName = "ValkyrieSpin";
		internal const string SwoopName = "ValkyrieSwoop";
		internal const string VolleyName = "ValkyrieFeatherVolley";
		internal const string ScreechName = "ValkyrieScreech";
		private const string SpinEffectName = "fx_valkyrie_spin";

		/// <summary>
		/// The Fallen Valkyrie's attacks the game has but never gives it, which she uses in the air - its swoop, its
		/// spinning volley of feathers and its screech
		/// </summary>
		public const string FallenSwoopPrefab = "fallenvalkyrie_swoopattack";
		public const string FallenVolleyPrefab = "fallenvalkyrie_wingspin";
		public const string FallenScreechPrefab = "fallenvalkyrie_screech";

		/// <summary>
		/// How long her AI waits between swoops, in seconds - her rider can swoop as often as her claws strike
		/// </summary>
		private const float AISwoopInterval = 6f;

		/// <summary>
		/// Her feather volley - how many feathers, how high up her they're launched from, in meters at size 1, and how far
		/// she can be turned from her target to loose it, in degrees - the Fallen Valkyrie's flings 24 every which way.
		/// How each flies is ValkyrieVolley's
		/// </summary>
		private const int VolleyFeathers = 8;
		private const float VolleyHeight = 4f;
		private const float VolleyFacing = 45f;

		/// <summary>
		/// The dvergr mage whose frost nova her banish is made from, and the nova
		/// </summary>
		private const string NovaMage = "DvergerMageIce";
		private const string FrostNova = "DvergerStaffNova";

		/// <summary>
		/// Her banish's spirit damage and how much brighter its glow and lights are, as Zombie Apocalypse's dvergr mages'
		/// spirit nova, and how much brighter her spin's white effect is as she casts it
		/// </summary>
		private const float NovaDamage = 112f;
		private const float NovaGlow = 1.5f;
		private const float BanishBrightness = 2f;

		/// <summary>
		/// The names her banish's burst, its ring and its brighter spin effect are registered by
		/// </summary>
		private const string BanishBurstName = "ValkyrieBanish_aoe";
		private const string BanishRingName = "fx_valkyrie_banish_ring";
		private const string BanishSpinEffectName = "fx_valkyrie_banish_spin";

		/// <summary>
		/// Her grab's damage, how far below her its sweep starts and how wide it is, in meters
		/// </summary>
		private const float GrabSlash = 10f;
		private const float GrabSpirit = 10f;
		private const float GrabHeight = -1f;
		private const float GrabWidth = 1.5f;

		/// <summary>
		/// How her claws' damage differs from the Fallen Valkyrie's
		/// </summary>
		private const float ClawsSlash = -5f;
		private const float ClawsPierce = -5f;
		private const float ClawsSpirit = 20f;

		/// <summary>
		/// The chop damage her claws and swoop deal, which the Fallen Valkyrie's have none of, so they cut trees too, and
		/// the tool tier her claws, swoop and spin cut and mine with - a bronze axe's, which fells birches, oaks and the
		/// Swamp's ancient trees, where the Fallen Valkyrie's tier fells only what a stone axe does
		/// </summary>
		private const float ClawsChop = 50f;
		private const short TreeTier = 2;

		/// <summary>
		/// How far her claws reach from where she stands, how high above the ground their sweep starts, and how near an
		/// enemy has to be for her AI to strike, in meters - the Fallen Valkyrie's reach 6 meters, sweep from 2 meters
		/// below it and strike from 5
		/// </summary>
		private const float ClawsReach = 3.5f;
		private const float ClawsHeight = 1f;
		private const float ClawsAIRange = 3f;

		/// <summary>
		/// How far she wanders with nothing to do, in meters, and how often, in seconds - the Fallen Valkyrie roams 35
		/// to 50 meters every 25 to 37 seconds, and a tamed creature wanders around wherever it is, so she'd drift off
		/// for good
		/// </summary>
		private const float IdleMoveRange = 2f;
		private const float IdleMoveInterval = 30f;

		/// <summary>
		/// How far she sees, how far off she hears an enemy whose noise reaches her, and how near an enemy she sees has to
		/// be for her to be alerted to it, in meters - the Fallen Valkyrie sees 30, hears anything whose noise reaches it
		/// however far, and is alerted within 50, which had her fly off after enemies well away from her
		/// </summary>
		private const float SightRange = 15f;
		private const float HearRange = 10f;
		private const float AlertRange = 15f;

		/// <summary>
		/// How loud her idle calls are, as a share of the Fallen Valkyrie's
		/// </summary>
		private const float IdleVolume = 0.5f;

		/// <summary>
		/// The least height above the ground her AI flies at, in meters, so her strikes from the air reach what's on it -
		/// the Fallen Valkyrie skims it
		/// </summary>
		private const float FightAltitude = 3f;

		/// <summary>
		/// How her spin's damage differs from the Fallen Valkyrie's
		/// </summary>
		private const float SpinBlunt = -10f;
		private const float SpinPierce = -10f;
		private const float SpinSpirit = 40f;

		/// <summary>
		/// How bright the colors her spin's effect starts its particles with are at least once whitened, from 0 to 1
		/// </summary>
		private const float SpinStartBrightness = 0.9f;

		/// <summary>
		/// Her attacks and her spin's white effect, once they're built, which go in each world's prefabs with her
		/// </summary>
		private static readonly List<GameObject> built = new List<GameObject>();

		/// <summary>
		/// The white copies of the Fallen Valkyrie's effects made so far, by the effect they're copied from, so her spin
		/// and feather volley share one
		/// </summary>
		private static readonly Dictionary<GameObject, GameObject> whitened = new Dictionary<GameObject, GameObject>();

		/// <summary>
		/// Gives her the Fallen Valkyrie's health, resistances and effects, with her own changes
		/// </summary>
		/// <param name="body">Her body</param>
		/// <param name="fallen">The Fallen Valkyrie's body</param>
		public static void SetStats(Humanoid body, Humanoid fallen)
		{
			body.m_health = fallen.m_health;
			body.m_regenAllHPTime = fallen.m_regenAllHPTime;
			HitData.DamageModifiers modifiers = fallen.m_damageModifiers;
			modifiers.m_spirit = HitData.DamageModifier.Immune;
			modifiers.m_fire = HitData.DamageModifier.Normal;
			modifiers.m_poison = HitData.DamageModifier.Normal;
			modifiers.m_lightning = HitData.DamageModifier.Resistant;
			body.m_damageModifiers = modifiers;
			// The game never staggers a creature whose stagger factor isn't above 0
			body.m_staggerDamageFactor = 0f;
			body.m_staggerWhenBlocked = false;
			body.m_hitEffects = fallen.m_hitEffects;
			body.m_critHitEffects = fallen.m_critHitEffects;
			body.m_backstabHitEffects = fallen.m_backstabHitEffects;
			body.m_deathEffects = fallen.m_deathEffects;
			body.m_flySlowSpeed = fallen.m_flySlowSpeed;
			body.m_flyFastSpeed = fallen.m_flyFastSpeed;
			body.m_flyTurnSpeed = fallen.m_flyTurnSpeed;
		}

		/// <summary>
		/// Gives her the Fallen Valkyrie's AI, keeping how the Asksvin finds its way, never taking off on her own, mostly
		/// standing still with nothing to do rather than roaming, and only taking up enemies near her (see SightRange). She
		/// picks no food up off the ground the way other tame creatures do - she eats from her pack (see ValkyriePack). Her
		/// idle calls are half as loud as the Fallen Valkyrie's (see QuietIdleSound)
		/// </summary>
		/// <param name="ai">Her AI, still the Asksvin's</param>
		/// <param name="fallen">The Fallen Valkyrie's AI</param>
		/// <param name="holder">The inactive object her quieter idle sound is kept under</param>
		public static void SetAI(MonsterAI ai, MonsterAI fallen, Transform holder)
		{
			Pathfinding.AgentType agent = ai.m_pathAgentType;
			CopyFields(typeof(BaseAI), fallen, ai);
			CopyFields(typeof(MonsterAI), fallen, ai);
			ai.m_consumeItems = new List<ItemDrop>();
			ai.m_pathAgentType = agent;
			ai.m_randomFly = false;
			ai.m_avoidLand = false;
			ai.m_aggravatable = true;
			ai.m_randomMoveRange = IdleMoveRange;
			ai.m_randomMoveInterval = IdleMoveInterval;
			ai.m_viewRange = SightRange;
			ai.m_hearRange = HearRange;
			ai.m_alertRange = AlertRange;
			ai.m_flyAltitudeMin = FightAltitude;
			// The Fallen Valkyrie circles its target while its next attack's not ready, which has her loop around
			// beneath a big flyer like Moder, out of reach, rather than hovering by it ready to strike again
			ai.m_circulateWhileChargingFlying = false;
			ai.m_idleSound = QuietIdleSound(fallen.m_idleSound, holder);
		}

		/// <summary>
		/// Makes her idle sound from the Fallen Valkyrie's, its sounds copied at IdleVolume of their volume - each is a
		/// networked object, so its copy is registered as a prefab of its own, and the Fallen Valkyrie keeps its own
		/// </summary>
		/// <param name="fallen">The Fallen Valkyrie's idle sound</param>
		/// <param name="holder">The inactive object the copies are kept under</param>
		/// <returns>Her idle sound</returns>
		private static EffectList QuietIdleSound(EffectList fallen, Transform holder)
		{
			List<EffectList.EffectData> effects = new List<EffectList.EffectData>();
			foreach (EffectList.EffectData effect in fallen.m_effectPrefabs)
			{
				EffectList.EffectData copy = new EffectList.EffectData();
				CopyFields(typeof(EffectList.EffectData), effect, copy);
				if (effect.m_prefab != null && effect.m_prefab.GetComponent<ZSFX>() != null)
				{
					GameObject quiet = Object.Instantiate(effect.m_prefab, holder, false);
					quiet.name = effect.m_prefab.name.Replace("fallenvalkyrie", "valkyrie");
					if (quiet.name == effect.m_prefab.name)
					{
						quiet.name = "valkyrie_" + effect.m_prefab.name;
					}
					ZSFX sound = quiet.GetComponent<ZSFX>();
					sound.m_minVol *= IdleVolume;
					sound.m_maxVol *= IdleVolume;
					built.Add(quiet);
					copy.m_prefab = quiet;
				}
				effects.Add(copy);
			}
			return(new EffectList { m_effectPrefabs = effects.ToArray() });
		}

		/// <summary>
		/// Has her drop what the Fallen Valkyrie drops but its trophy
		/// </summary>
		/// <param name="valkyrie">Her prefab</param>
		/// <param name="fallen">The Fallen Valkyrie's prefab</param>
		public static void SetDrops(GameObject valkyrie, GameObject fallen)
		{
			CharacterDrop drop = valkyrie.GetComponent<CharacterDrop>();
			CharacterDrop fallenDrop = fallen.GetComponent<CharacterDrop>();
			if (drop == null || fallenDrop == null)
			{
				return;
			}
			drop.m_drops = fallenDrop.m_drops.Where((CharacterDrop.Drop item) => item.m_prefab != null && !item.m_prefab.name.StartsWith("Trophy")).ToList();
		}

		/// <summary>
		/// Gives her the Fallen Valkyrie's attacks but its spit and its taunt, with her own claws and spin - its taunt is
		/// the first thing it does once it spots an enemy, rearing up and screeching, and she goes straight for them -
		/// and the attacks she makes in the air: her claws as the Fallen Valkyrie rakes hovering, and those of its the
		/// game never gives it, its swoop, spinning volley of feathers and screech (see AirAttacks)
		/// </summary>
		/// <param name="body">Her body</param>
		/// <param name="fallen">The Fallen Valkyrie's body</param>
		/// <param name="scene">The world's ZNetScene, which has the Fallen Valkyrie's other attacks</param>
		/// <param name="holder">The inactive object her own attacks are kept under</param>
		public static void SetAttacks(Humanoid body, Humanoid fallen, ZNetScene scene, Transform holder)
		{
			List<GameObject> attacks = new List<GameObject>();
			foreach (GameObject item in fallen.m_defaultItems)
			{
				if (item == null || item.name.Contains("spit") || item.name.Contains("taunt"))
				{
					continue;
				}
				if (item.name.Contains("claws"))
				{
					GameObject claws = CopyAttack(item, ClawsName, holder, ClawsSlash, ClawsPierce, 0f, ClawsSpirit);
					ItemDrop.ItemData.SharedData shared = claws.GetComponent<ItemDrop>().m_itemData.m_shared;
					ShortenClaws(shared);
					CutTrees(shared, ClawsChop);
					shared.m_aiWhenFlying = false;
					attacks.Add(claws);
					attacks.AddRange(AirAttacks(item, shared, scene, holder));
					GameObject grab = GrabFrom(scene, holder);
					if (grab != null)
					{
						attacks.Add(grab);
					}
				}
				else if (item.name.Contains("spin"))
				{
					GameObject spin = CopyAttack(item, SpinName, holder, 0f, SpinPierce, SpinBlunt, SpinSpirit);
					CutTrees(spin.GetComponent<ItemDrop>().m_itemData.m_shared, 0f);
					SizeSpin(spin.GetComponent<ItemDrop>().m_itemData.m_shared);
					WhitenSpinEffect(spin.GetComponent<ItemDrop>().m_itemData.m_shared.m_attack, holder);
					attacks.Add(spin);
					GameObject banish = BanishFrom(scene, spin.GetComponent<ItemDrop>().m_itemData.m_shared.m_attack, holder);
					if (banish != null)
					{
						attacks.Add(banish);
					}
				}
				else
				{
					attacks.Add(item);
				}
			}
			body.m_defaultItems = attacks.ToArray();
		}

		/// <summary>
		/// Makes the attacks she uses only in the air - her claws as the Fallen Valkyrie rakes hovering, with its full
		/// reach from below her, since her own reach only as far as her talons get standing; its swoop, raking at once
		/// rather than rearing back first, with her claws' damage; and its spinning volley of feathers and its screech,
		/// as they are, the volley in white. Each one the game doesn't have is left out. All but the volley, which is
		/// for a distance, she uses however close she is - the Fallen Valkyrie keeps some way off for them, which with a
		/// target as big as Moder leaves her nothing to strike with up close but her spin
		/// </summary>
		/// <param name="fallenClaws">The Fallen Valkyrie's claws</param>
		/// <param name="claws">Her own claws' item data</param>
		/// <param name="scene">The world's ZNetScene</param>
		/// <param name="holder">The inactive object they're kept under</param>
		/// <returns>Her attacks in the air</returns>
		private static List<GameObject> AirAttacks(GameObject fallenClaws, ItemDrop.ItemData.SharedData claws, ZNetScene scene, Transform holder)
		{
			List<GameObject> attacks = new List<GameObject>();
			GameObject airClaws = CopyAttack(fallenClaws, AirClawsName, holder, ClawsSlash, ClawsPierce, 0f, ClawsSpirit);
			CutTrees(airClaws.GetComponent<ItemDrop>().m_itemData.m_shared, ClawsChop);
			OnlyInTheAir(airClaws, true);
			attacks.Add(airClaws);
			GameObject fallenSwoop = scene.GetPrefab(FallenSwoopPrefab);
			if (fallenSwoop != null && fallenSwoop.GetComponent<ItemDrop>() != null)
			{
				GameObject swoop = CopyAttack(fallenSwoop, SwoopName, holder, 0f, 0f, 0f, 0f);
				ItemDrop.ItemData.SharedData shared = swoop.GetComponent<ItemDrop>().m_itemData.m_shared;
				shared.m_damages = claws.m_damages;
				shared.m_toolTier = claws.m_toolTier;
				shared.m_aiAttackInterval = AISwoopInterval;
				OnlyInTheAir(swoop, true);
				attacks.Add(swoop);
			}
			GameObject fallenVolley = scene.GetPrefab(FallenVolleyPrefab);
			if (fallenVolley != null && fallenVolley.GetComponent<ItemDrop>() != null)
			{
				GameObject volley = CopyAttack(fallenVolley, VolleyName, holder, 0f, 0f, 0f, 0f);
				WhitenSpinEffect(volley.GetComponent<ItemDrop>().m_itemData.m_shared.m_attack, holder);
				AimVolley(volley.GetComponent<ItemDrop>().m_itemData.m_shared);
				OnlyInTheAir(volley, false);
				attacks.Add(volley);
			}
			GameObject fallenScreech = scene.GetPrefab(FallenScreechPrefab);
			if (fallenScreech != null && fallenScreech.GetComponent<ItemDrop>() != null)
			{
				GameObject screech = CopyAttack(fallenScreech, ScreechName, holder, 0f, 0f, 0f, 0f);
				OnlyInTheAir(screech, true);
				attacks.Add(screech);
			}
			return(attacks);
		}

		/// <summary>
		/// Makes her grab's strike from the Fallen Valkyrie's swoop, which rakes at once - 10 slash and 10 spirit damage,
		/// reaching from a little below her, so it catches what stands beside her on the ground and what's below her in
		/// the air. Her AI never picks it the way it picks her other attacks - she grabs on her own terms (see
		/// ValkyrieGrab)
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <param name="holder">The inactive object it's kept under</param>
		/// <returns>Her grab, or null when the game has no swoop</returns>
		private static GameObject GrabFrom(ZNetScene scene, Transform holder)
		{
			GameObject fallenSwoop = scene.GetPrefab(FallenSwoopPrefab);
			if (fallenSwoop == null || fallenSwoop.GetComponent<ItemDrop>() == null)
			{
				return(null);
			}
			GameObject grab = CopyAttack(fallenSwoop, ValkyrieGrab.GrabName, holder, 0f, 0f, 0f, 0f);
			ItemDrop.ItemData.SharedData shared = grab.GetComponent<ItemDrop>().m_itemData.m_shared;
			shared.m_damages = new HitData.DamageTypes { m_slash = GrabSlash, m_spirit = GrabSpirit };
			shared.m_attack.m_attackRange = ValkyrieGrab.Reach;
			shared.m_attack.m_attackHeight = GrabHeight;
			shared.m_attack.m_attackRayWidth = GrabWidth;
			shared.m_aiAttackInterval = ValkyrieGrab.Cooldown;
			shared.m_aiWhenFlying = false;
			shared.m_aiWhenWalking = false;
			shared.m_aiWhenSwiming = false;
			shared.m_aiPrioritized = false;
			return(grab);
		}

		/// <summary>
		/// Aims her feather volley at her target as a tight burst - the Fallen Valkyrie's flings its feathers every which
		/// way around it, level with its feet, which from where she hovers passes over what's on the ground - fewer
		/// feathers, launched from her chest toward her target, and only when she's turned toward it. Each is led at her
		/// target, flying straight at one speed, as it's launched (see ValkyrieVolley)
		/// </summary>
		/// <param name="shared">Her volley's item data</param>
		private static void AimVolley(ItemDrop.ItemData.SharedData shared)
		{
			Attack attack = shared.m_attack;
			attack.m_circularProjectileLaunch = false;
			attack.m_useCharacterFacing = false;
			attack.m_launchAngle = 0f;
			attack.m_projectiles = VolleyFeathers;
			attack.m_projectileAccuracy = 0f;
			attack.m_projectileAccuracyMin = 0f;
			attack.m_randomVelocity = false;
			attack.m_projectileVel = ValkyrieVolley.Speed;
			attack.m_projectileVelMin = ValkyrieVolley.Speed;
			attack.m_attackHeight = VolleyHeight;
			shared.m_aiAttackMaxAngle = VolleyFacing;
		}

		/// <summary>
		/// Makes her banish - the dvergr ice mage's frost nova as a spirit nova, the way Zombie Apocalypse gives it the
		/// dvergr mages, with the same spirit damage, in the same white, and as often, but bursting out 10 meters around
		/// her rather than 5, cast with her spin and its white effect twice as bright. Her AI never picks it the way it
		/// picks her other attacks - she banishes on her own terms (see ValkyrieBanish)
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <param name="spin">Her spin's attack, whose white effect it shows brighter</param>
		/// <param name="holder">The inactive object it's kept under</param>
		/// <returns>Her banish, or null when the game has no frost nova</returns>
		private static GameObject BanishFrom(ZNetScene scene, Attack spin, Transform holder)
		{
			GameObject mage = scene.GetPrefab(NovaMage);
			Humanoid mageBody = ((mage != null) ? mage.GetComponent<Humanoid>() : null);
			GameObject nova = ((mageBody != null) ? mageBody.m_defaultItems.FirstOrDefault((GameObject item) => item != null && item.name == FrostNova) : null);
			ItemDrop.ItemData.SharedData novaShared = ((nova != null && nova.GetComponent<ItemDrop>() != null) ? nova.GetComponent<ItemDrop>().m_itemData.m_shared : null);
			GameObject sourceBurst = novaShared?.m_attack.m_attackProjectile;
			if (sourceBurst == null || sourceBurst.GetComponent<Aoe>() == null)
			{
				Plugin.Log.LogWarning("Couldn't build the valkyrie's banish - the " + NovaMage + "'s " + FrostNova + " is missing");
				return(null);
			}
			GameObject burst = Object.Instantiate(sourceBurst, holder, false);
			burst.name = BanishBurstName;
			Aoe aoe = burst.GetComponent<Aoe>();
			float grow = ValkyrieBanish.Radius / Mathf.Max(0.1f, aoe.m_radius);
			aoe.m_damage = new HitData.DamageTypes { m_spirit = NovaDamage };
			aoe.m_radius = ValkyrieBanish.Radius;
			built.Add(burst);
			List<EffectList.EffectData> effects = new List<EffectList.EffectData>();
			GameObject spinEffect = spin.m_triggerEffect.m_effectPrefabs.Select((EffectList.EffectData effect) => effect.m_prefab).FirstOrDefault((GameObject prefab) => prefab != null && prefab.name == SpinEffectName);
			if (spinEffect != null)
			{
				GameObject bright = Object.Instantiate(spinEffect, holder, false);
				bright.name = BanishSpinEffectName;
				Brighten(bright, BanishBrightness);
				built.Add(bright);
				effects.Add(new EffectList.EffectData { m_prefab = bright });
			}
			GameObject sourceRing = novaShared.m_startEffect.m_effectPrefabs.Select((EffectList.EffectData effect) => effect.m_prefab).FirstOrDefault((GameObject prefab) => prefab != null);
			if (sourceRing != null)
			{
				GameObject ring = Object.Instantiate(sourceRing, holder, false);
				ring.name = BanishRingName;
				SpiritWhite.Whiten(ring, NovaGlow);
				Grow(ring, grow);
				built.Add(ring);
				effects.Add(new EffectList.EffectData { m_prefab = ring });
			}
			GameObject banish = CopyAttack(nova, ValkyrieBanish.BanishName, holder, 0f, 0f, 0f, 0f);
			ItemDrop.ItemData.SharedData shared = banish.GetComponent<ItemDrop>().m_itemData.m_shared;
			shared.m_damages = new HitData.DamageTypes { m_spirit = NovaDamage };
			shared.m_startEffect = new EffectList();
			shared.m_attack.m_attackAnimation = spin.m_attackAnimation;
			shared.m_attack.m_attackProjectile = burst;
			shared.m_attack.m_triggerEffect = new EffectList { m_effectPrefabs = effects.ToArray() };
			shared.m_aiWhenFlying = false;
			shared.m_aiWhenWalking = false;
			shared.m_aiWhenSwiming = false;
			shared.m_aiPrioritized = false;
			return(banish);
		}

		/// <summary>
		/// Makes an effect brighter - its particles' colors, the colors its glow is mapped from, and its lights
		/// </summary>
		/// <param name="effect">The effect</param>
		/// <param name="factor">How many times brighter</param>
		private static void Brighten(GameObject effect, float factor)
		{
			foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
			{
				ParticleSystem.MainModule main = particles.main;
				main.startColor = Brighten(main.startColor, factor);
				ParticleSystem.CustomDataModule custom = particles.customData;
				if (custom.enabled)
				{
					foreach (ParticleSystemCustomData stream in new[] { ParticleSystemCustomData.Custom1, ParticleSystemCustomData.Custom2 })
					{
						if (custom.GetMode(stream) == ParticleSystemCustomDataMode.Color)
						{
							custom.SetColor(stream, Brighten(custom.GetColor(stream), factor));
						}
					}
				}
			}
			foreach (Light light in effect.GetComponentsInChildren<Light>(true))
			{
				light.intensity *= factor;
			}
		}

		/// <summary>
		/// Makes every color in a particle color setting brighter, keeping their alpha
		/// </summary>
		/// <param name="value">The setting</param>
		/// <param name="factor">How many times brighter</param>
		/// <returns>The brighter setting, in the same mode</returns>
		private static ParticleSystem.MinMaxGradient Brighten(ParticleSystem.MinMaxGradient value, float factor)
		{
			switch (value.mode)
			{
				case ParticleSystemGradientMode.Color:
					return(new ParticleSystem.MinMaxGradient(Brighter(value.color, factor)));
				case ParticleSystemGradientMode.TwoColors:
					return(new ParticleSystem.MinMaxGradient(Brighter(value.colorMin, factor), Brighter(value.colorMax, factor)));
				case ParticleSystemGradientMode.TwoGradients:
					return(new ParticleSystem.MinMaxGradient(Brighter(value.gradientMin, factor), Brighter(value.gradientMax, factor)));
				default:
					ParticleSystem.MinMaxGradient brighter = new ParticleSystem.MinMaxGradient(Brighter(value.gradient, factor));
					brighter.mode = value.mode;
					return(brighter);
			}
		}

		/// <summary>
		/// Makes a color brighter, keeping its alpha
		/// </summary>
		/// <param name="color">The color</param>
		/// <param name="factor">How many times brighter</param>
		/// <returns>The brighter color</returns>
		internal static Color Brighter(Color color, float factor)
		{
			return(new Color(color.r * factor, color.g * factor, color.b * factor, color.a));
		}

		/// <summary>
		/// Makes every color key in a gradient brighter, keeping its alpha keys
		/// </summary>
		/// <param name="gradient">The gradient, or null</param>
		/// <param name="factor">How many times brighter</param>
		/// <returns>The brighter gradient, or null</returns>
		private static Gradient Brighter(Gradient gradient, float factor)
		{
			if (gradient == null)
			{
				return(null);
			}
			GradientColorKey[] keys = gradient.colorKeys;
			for (int i = 0; i < keys.Length; i++)
			{
				keys[i].color = Brighter(keys[i].color, factor);
			}
			Gradient brighter = new Gradient();
			brighter.mode = gradient.mode;
			brighter.SetKeys(keys, gradient.alphaKeys);
			return(brighter);
		}

		/// <summary>
		/// Grows an effect, its particles and lights with it - its particles are set to follow its size, which they
		/// don't all by themselves
		/// </summary>
		/// <param name="effect">The effect</param>
		/// <param name="factor">How many times bigger</param>
		private static void Grow(GameObject effect, float factor)
		{
			effect.transform.localScale *= factor;
			foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
			{
				ParticleSystem.MainModule main = particles.main;
				main.scalingMode = ParticleSystemScalingMode.Hierarchy;
			}
			foreach (Light light in effect.GetComponentsInChildren<Light>(true))
			{
				light.range *= factor;
			}
		}

		/// <summary>
		/// Has her AI use an attack only while she flies, however high she flies, and never first before her others
		/// </summary>
		/// <param name="attack">The attack</param>
		/// <param name="upClose">Whether she uses it however close her target is</param>
		private static void OnlyInTheAir(GameObject attack, bool upClose)
		{
			ItemDrop.ItemData.SharedData shared = attack.GetComponent<ItemDrop>().m_itemData.m_shared;
			shared.m_aiWhenFlying = true;
			shared.m_aiWhenWalking = false;
			shared.m_aiWhenSwiming = false;
			shared.m_aiPrioritized = false;
			shared.m_aiWhenFlyingAltitudeMin = 0f;
			if (upClose)
			{
				shared.m_aiAttackRangeMin = 0f;
			}
		}

		/// <summary>
		/// Has her attack for her rider, on the rider's client, which owns her while they ride - her claws, or her swoop
		/// in the air, or her spin for the rider's secondary attack - turned to face where the rider looks, once the
		/// attack's ready again after the wait her AI leaves between its uses, or her claws' wait for her swoop. She
		/// won't spin where it would hit buildings, saying so instead (see ValkyrieSpinGuard)
		/// </summary>
		/// <param name="body">Her body</param>
		/// <param name="spin">Whether it's her spin rather than her claws</param>
		/// <param name="look">Where the rider looks</param>
		public static void RiderAttack(Humanoid body, bool spin, Vector3 look)
		{
			if (body.InAttack() || body.IsDead())
			{
				return;
			}
			List<ItemDrop.ItemData> items = body.GetInventory().GetAllItems();
			string name = RiderAttackName(spin, body.IsFlying(), items.Any((ItemDrop.ItemData item) => item.m_shared.m_name == SwoopName));
			ItemDrop.ItemData attack = items.FirstOrDefault((ItemDrop.ItemData item) => item.m_shared.m_name == name);
			ItemDrop.ItemData claws = items.FirstOrDefault((ItemDrop.ItemData item) => item.m_shared.m_name == ClawsName);
			float interval = ((name == SwoopName && claws != null) ? claws.m_shared.m_aiAttackInterval : ((attack != null) ? attack.m_shared.m_aiAttackInterval : 0f));
			if (attack == null || !Ready(Time.time, attack.m_lastAttackTime, interval))
			{
				return;
			}
			look.y = 0f;
			if (look.sqrMagnitude > 0.0001f)
			{
				look.Normalize();
				body.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
				body.SetLookDir(look);
			}
			if (name == SpinName && ValkyrieSpinGuard.Holds(body, attack))
			{
				VikingSpeech.Say(body, ValkyrieLines.NotHere(Random.value), VikingSpeech.QuickTime, VikingSpeech.TalkCull);
				return;
			}
			if (!body.IsItemEquiped(attack))
			{
				body.EquipItem(attack, false);
			}
			body.StartAttack(null, false);
		}

		/// <summary>
		/// The name of the attack her rider's button has her make - her spin for the secondary attack, and for the
		/// attack her swoop in the air, when she has one, or her claws
		/// </summary>
		/// <param name="spin">Whether it's the rider's secondary attack</param>
		/// <param name="flying">Whether she flies</param>
		/// <param name="hasSwoop">Whether she has her swoop</param>
		/// <returns>The attack's name</returns>
		internal static string RiderAttackName(bool spin, bool flying, bool hasSwoop)
		{
			if (spin)
			{
				return(SpinName);
			}
			return(((flying && hasSwoop) ? SwoopName : ClawsName));
		}

		/// <summary>
		/// Checks whether an attack is ready again after the wait between its uses
		/// </summary>
		/// <param name="now">The game's time</param>
		/// <param name="last">When it was last used, by the game's time</param>
		/// <param name="interval">The wait between its uses, in seconds</param>
		/// <returns>True when it's ready</returns>
		internal static bool Ready(float now, float last, float interval)
		{
			return(now - last >= interval);
		}

		/// <summary>
		/// Adds her attacks and her spin's white effect to a world's prefabs, unless they're in already
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		public static void Register(ZNetScene scene)
		{
			foreach (GameObject prefab in built)
			{
				int hash = prefab.name.GetStableHashCode();
				if (!scene.m_namedPrefabs.ContainsKey(hash))
				{
					scene.m_prefabs.Add(prefab);
					scene.m_namedPrefabs.Add(hash, prefab);
				}
			}
		}

		/// <summary>
		/// Turns a color white, keeping how bright it is and its alpha, and raising it to at least a brightness
		/// </summary>
		/// <param name="color">The color</param>
		/// <param name="floor">The least brightness it's raised to, 0 to keep it as bright as it was</param>
		/// <returns>The white color, as bright as the color's brightest channel or the floor</returns>
		internal static Color Whiten(Color color, float floor)
		{
			float brightness = Mathf.Max(Mathf.Max(color.r, color.g), Mathf.Max(color.b, floor));
			return(new Color(brightness, brightness, brightness, color.a));
		}

		/// <summary>
		/// Copies one of the Fallen Valkyrie's attacks as her own, with its damage changed
		/// </summary>
		/// <param name="item">The Fallen Valkyrie's attack</param>
		/// <param name="name">Her copy's name</param>
		/// <param name="holder">The inactive object it's kept under</param>
		/// <param name="slash">Slash damage added</param>
		/// <param name="pierce">Pierce damage added</param>
		/// <param name="blunt">Blunt damage added</param>
		/// <param name="spirit">Spirit damage added</param>
		/// <returns>Her copy</returns>
		private static GameObject CopyAttack(GameObject item, string name, Transform holder, float slash, float pierce, float blunt, float spirit)
		{
			GameObject copy = Object.Instantiate(item, holder, false);
			copy.name = name;
			ItemDrop.ItemData.SharedData shared = copy.GetComponent<ItemDrop>().m_itemData.m_shared;
			shared.m_name = name;
			HitData.DamageTypes damages = shared.m_damages;
			damages.m_slash = Mathf.Max(0f, damages.m_slash + slash);
			damages.m_pierce = Mathf.Max(0f, damages.m_pierce + pierce);
			damages.m_blunt = Mathf.Max(0f, damages.m_blunt + blunt);
			damages.m_spirit = Mathf.Max(0f, damages.m_spirit + spirit);
			shared.m_damages = damages;
			built.Add(copy);
			return(copy);
		}

		/// <summary>
		/// How far her spin reaches from her feet, all the way around and up, and how much farther than that her AI spins
		/// at an enemy, in meters - the Fallen Valkyrie's reaches 5.67, with 6 for its AI
		/// </summary>
		private const float SpinRadius = 8f;
		private const float SpinAIMargin = 0.33f;

		/// <summary>
		/// Gives her spin its reach (see SpinRadius), and has her AI spin at an enemy within it and SpinAIMargin more
		/// </summary>
		/// <param name="shared">Her spin's item data</param>
		private static void SizeSpin(ItemDrop.ItemData.SharedData shared)
		{
			shared.m_attack.m_attackRayWidth = SpinRadius;
			shared.m_aiAttackRange = SpinRadius + SpinAIMargin;
		}

		/// <summary>
		/// Has one of her attacks cut trees - dealing at least some chop damage, at the tool tier that fells birches and
		/// oaks (see TreeTier)
		/// </summary>
		/// <param name="shared">The attack's item data</param>
		/// <param name="chop">The least chop damage it deals, or 0 to keep its own</param>
		private static void CutTrees(ItemDrop.ItemData.SharedData shared, float chop)
		{
			HitData.DamageTypes damages = shared.m_damages;
			damages.m_chop = Mathf.Max(damages.m_chop, chop);
			shared.m_damages = damages;
			shared.m_toolTier = TreeTier;
		}

		/// <summary>
		/// Shortens her claws' reach to where her talons get to - the Fallen Valkyrie rakes hovering, lunging its whole
		/// body, but she strikes standing and leans only part of the way (see ValkyrieAttackPose) - sweeping from a little
		/// above the ground she stands on rather than below it, and has her AI close in further before striking
		/// </summary>
		/// <param name="shared">Her claws' item data</param>
		private static void ShortenClaws(ItemDrop.ItemData.SharedData shared)
		{
			shared.m_attack.m_attackRange = ClawsReach;
			shared.m_attack.m_attackHeight = ClawsHeight;
			shared.m_aiAttackRange = ClawsAIRange;
			shared.m_aiAttackRangeMin = 0f;
		}

		/// <summary>
		/// Swaps a spinning attack's purple effect for a copy of it in white - its smoke, glow and feathers - which is a
		/// networked object, so it's registered as a prefab of its own, one copy shared by every attack using it
		/// </summary>
		/// <param name="attack">Her spinning attack</param>
		/// <param name="holder">The inactive object the copy is kept under</param>
		private static void WhitenSpinEffect(Attack attack, Transform holder)
		{
			EffectList.EffectData[] effects = attack.m_triggerEffect.m_effectPrefabs;
			for (int i = 0; i < effects.Length; i++)
			{
				GameObject original = effects[i].m_prefab;
				if (original == null || !original.name.Contains("spin"))
				{
					continue;
				}
				if (whitened.TryGetValue(original, out GameObject made))
				{
					effects[i].m_prefab = made;
					continue;
				}
				GameObject white = Object.Instantiate(original, holder, false);
				white.name = SpinEffectName;
				foreach (ParticleSystem particles in white.GetComponentsInChildren<ParticleSystem>(true))
				{
					ParticleSystem.MainModule main = particles.main;
					main.startColor = Whiten(main.startColor, SpinStartBrightness);
					ParticleSystem.CustomDataModule custom = particles.customData;
					if (custom.enabled)
					{
						WhitenCustom(custom, ParticleSystemCustomData.Custom1);
						WhitenCustom(custom, ParticleSystemCustomData.Custom2);
					}
				}
				effects[i].m_prefab = white;
				whitened[original] = white;
				built.Add(white);
			}
		}

		/// <summary>
		/// Whitens one of a particle system's custom data streams when it holds colors - the glow's shader maps its
		/// particles' colors from them
		/// </summary>
		/// <param name="custom">The particle system's custom data</param>
		/// <param name="stream">The stream</param>
		private static void WhitenCustom(ParticleSystem.CustomDataModule custom, ParticleSystemCustomData stream)
		{
			if (custom.GetMode(stream) == ParticleSystemCustomDataMode.Color)
			{
				custom.SetColor(stream, Whiten(custom.GetColor(stream), 0f));
			}
		}

		/// <summary>
		/// Turns a particle color setting white in every color it holds
		/// </summary>
		/// <param name="value">The setting</param>
		/// <param name="floor">The least brightness each color is raised to</param>
		/// <returns>The white setting, in the same mode</returns>
		private static ParticleSystem.MinMaxGradient Whiten(ParticleSystem.MinMaxGradient value, float floor)
		{
			switch (value.mode)
			{
				case ParticleSystemGradientMode.Color:
					return(new ParticleSystem.MinMaxGradient(Whiten(value.color, floor)));
				case ParticleSystemGradientMode.TwoColors:
					return(new ParticleSystem.MinMaxGradient(Whiten(value.colorMin, floor), Whiten(value.colorMax, floor)));
				case ParticleSystemGradientMode.TwoGradients:
					return(new ParticleSystem.MinMaxGradient(Whiten(value.gradientMin, floor), Whiten(value.gradientMax, floor)));
				default:
					ParticleSystem.MinMaxGradient whitened = new ParticleSystem.MinMaxGradient(Whiten(value.gradient, floor));
					whitened.mode = value.mode;
					return(whitened);
			}
		}

		/// <summary>
		/// Turns a gradient white in every color key, keeping its alpha keys
		/// </summary>
		/// <param name="gradient">The gradient</param>
		/// <param name="floor">The least brightness each color is raised to</param>
		/// <returns>The white gradient</returns>
		private static Gradient Whiten(Gradient gradient, float floor)
		{
			if (gradient == null)
			{
				return(null);
			}
			GradientColorKey[] keys = gradient.colorKeys;
			for (int i = 0; i < keys.Length; i++)
			{
				keys[i].color = Whiten(keys[i].color, floor);
			}
			Gradient whitened = new Gradient();
			whitened.mode = gradient.mode;
			whitened.SetKeys(keys, gradient.alphaKeys);
			return(whitened);
		}

		/// <summary>
		/// Copies the fields one type declares from one component to another
		/// </summary>
		/// <param name="type">The type whose own fields are copied</param>
		/// <param name="from">The component copied from</param>
		/// <param name="to">The component copied to</param>
		private static void CopyFields(System.Type type, object from, object to)
		{
			foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
			{
				field.SetValue(to, field.GetValue(from));
			}
		}
	}
}
