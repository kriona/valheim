using System.Linq;
using SoftReferenceableAssets;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Building the valkyrie's prefab - a tameable, rideable Asksvin with its body swapped for the valkyrie that brings
	/// players to the world, its saddle moved onto her back, fighting like the Fallen Valkyrie (see ValkyrieCombat) -
	/// and adding it to each world's prefabs
	/// </summary>
	/// <remarks>
	/// The Asksvin brings taming, its saddle and riding. The Fallen Valkyrie's model goes in its place with its body
	/// taken off, leaving its skeleton and animator hidden, so the game's parameters, triggers and animation events -
	/// footsteps and attacks - have the controller its attacks expect. Her own model is posed by ValkyrieModel, from
	/// the intro valkyrie's fly and glide clips and the end credits valkyrie's landing and idle clips, and takes the
	/// hidden skeleton's pose as she attacks (see ValkyrieAttackPose)
	/// </remarks>
	internal static class ValkyriePrefab
	{
		/// <summary>
		/// The valkyrie's prefab name, which the game's spawn command takes
		/// </summary>
		public const string PrefabName = "VikingValkyrie";

		/// <summary>
		/// The name she goes by, and a zombie risen from her too, with Zombie Apocalypse's zombie word in front
		/// </summary>
		public const string DisplayName = "Valkyrie";

		/// <summary>
		/// The creature whose taming, saddle and riding she's built from
		/// </summary>
		private const string SourcePrefab = "Asksvin";

		/// <summary>
		/// The end credits valkyrie's prefab, whose landing and idle clips she uses - one of the world's prefabs, which
		/// the start temple's spawner places once the last boss is down
		/// </summary>
		private const string EndPrefab = "Valkyrie_End";

		/// <summary>
		/// The start temple, whose spawner holds the end credits valkyrie, and that spawner's name - where she's found
		/// when she isn't among the world's prefabs
		/// </summary>
		private const string TemplePath = "Assets/world/Locations/Meadows/StartTemple.prefab";
		private const string TempleSpawner = "StoneSpawner_Valkyrie_End";

		/// <summary>
		/// The child of the intro valkyrie with her animator, armature and body
		/// </summary>
		private const string ModelName = "valkyrie2";

		/// <summary>
		/// The bone in the middle of her back her saddle rides on
		/// </summary>
		public const string SaddleBone = "spine1";

		/// <summary>
		/// Her speeds on the ground - a slow waddle, even running
		/// </summary>
		private const float WalkSpeed = 1f;
		private const float GroundSpeed = 1.5f;
		private const float GroundRunSpeed = 2.5f;

		/// <summary>
		/// The name of the hidden Fallen Valkyrie skeleton
		/// </summary>
		private const string RigName = "FallenRig";

		/// <summary>
		/// The prefab, once it's built
		/// </summary>
		private static GameObject prefab;

		/// <summary>
		/// Her corpse's prefab, once it's built (see ValkyrieCorpse)
		/// </summary>
		private static GameObject corpse;

		/// <summary>
		/// How long her corpse lies before it vanishes in a burst of smoke, unless she's rising from it, in seconds
		/// </summary>
		private const float CorpseTime = 5f;

		/// <summary>
		/// The inactive object the prefab sits under, so none of its parts wake
		/// </summary>
		private static GameObject holder;

		/// <summary>
		/// The intro valkyrie, and the start temple when the end credits valkyrie had to be found in it, held loaded for
		/// good so the clips she uses from them stay loaded
		/// </summary>
		private static SoftReference<GameObject> introValkyrie;
		private static SoftReference<GameObject> temple;

		/// <summary>
		/// Adds the valkyrie's prefab, her corpse, her attacks and their effects to a world's prefabs, building them the
		/// first time
		/// </summary>
		/// <param name="scene">The world's ZNetScene, as it wakes</param>
		public static void Register(ZNetScene scene)
		{
			if (prefab == null)
			{
				prefab = Build(scene);
			}
			if (prefab == null)
			{
				return;
			}
			ValkyrieCombat.Register(scene);
			AddToScene(scene, prefab);
			if (corpse != null)
			{
				AddToScene(scene, corpse);
			}
		}

		/// <summary>
		/// Adds a prefab to a world's prefabs, unless it's in already
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <param name="built">The prefab</param>
		private static void AddToScene(ZNetScene scene, GameObject built)
		{
			int hash = built.name.GetStableHashCode();
			if (!scene.m_namedPrefabs.ContainsKey(hash))
			{
				scene.m_prefabs.Add(built);
				scene.m_namedPrefabs.Add(hash, built);
			}
		}

		/// <summary>
		/// Builds the valkyrie's prefab from the Asksvin's and the Fallen Valkyrie's, logging what's missing when it can't
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <returns>The prefab, or null when a part it's built from is missing</returns>
		private static GameObject Build(ZNetScene scene)
		{
			GameObject source = scene.GetPrefab(SourcePrefab);
			GameObject playerPrefab = scene.GetPrefab("Player");
			GameObject fallen = scene.GetPrefab(ValkyrieCombat.FallenPrefab);
			if (source == null || playerPrefab == null || source.GetComponent<Tameable>() == null || source.GetComponent<Tameable>().m_saddle == null)
			{
				Plugin.Log.LogWarning("Couldn't build the valkyrie - the Asksvin or Player prefab is missing");
				return(null);
			}
			Animator fallenAnimator = ((fallen != null) ? fallen.GetComponentInChildren<Animator>(true) : null);
			if (fallenAnimator == null || fallen.GetComponent<Humanoid>() == null || fallen.GetComponent<MonsterAI>() == null)
			{
				Plugin.Log.LogWarning("Couldn't build the valkyrie - the Fallen Valkyrie's prefab is missing");
				return(null);
			}
			Transform introModel = LoadIntroModel(playerPrefab);
			if (introModel == null)
			{
				Plugin.Log.LogWarning("Couldn't build the valkyrie - the intro valkyrie couldn't be loaded");
				return(null);
			}
			Animator endAnimator = LoadEndAnimator(scene);
			if (endAnimator == null)
			{
				Plugin.Log.LogWarning("Couldn't build the valkyrie - the end credits valkyrie couldn't be found");
				return(null);
			}
			AnimationClip fly = FindClip(introModel.GetComponent<Animator>(), "Fly");
			AnimationClip glide = FindClip(introModel.GetComponent<Animator>(), "Glide");
			AnimationClip idle = FindClip(endAnimator, "Idle");
			AnimationClip landing = FindClip(endAnimator, "Landing");
			if (fly == null || glide == null || idle == null || landing == null)
			{
				Plugin.Log.LogWarning("Couldn't build the valkyrie - the game's valkyrie clips are missing");
				return(null);
			}
			if (holder == null)
			{
				holder = new GameObject(Plugin.PluginName + " valkyrie");
				holder.SetActive(false);
				Object.DontDestroyOnLoad(holder);
			}
			GameObject valkyrie = Object.Instantiate(source, holder.transform, false);
			valkyrie.name = PrefabName;
			Humanoid body = valkyrie.GetComponent<Humanoid>();
			Transform visual = valkyrie.transform.Find("Visual");
			Animator oldAnimator = visual.GetComponentInChildren<Animator>();
			Tameable tameable = valkyrie.GetComponent<Tameable>();
			Sadle saddle = tameable.m_saddle;
			Quaternion saddleTurn = Quaternion.Inverse(visual.rotation) * saddle.transform.rotation;
			Vector3 seat = saddle.transform.InverseTransformPoint(saddle.m_attachPoint.position);
			float saddleSize = saddle.transform.lossyScale.x;
			saddle.transform.SetParent(visual, true);
			StripOldBody(visual, oldAnimator);
			MakeSaddleRigid(saddle, seat);
			// Her saddle is an unseen seat that's part of her (see ValkyrieSeat), so none is put on, taken off or dropped
			tameable.m_saddleItem = null;
			tameable.m_dropSaddleOnDeath = false;
			// The Asksvin's picture never stands for her on the mount panel - she has her own (see ValkyrieIcon)
			saddle.m_mountIcon = null;
			GameObject rig = Object.Instantiate(fallenAnimator.gameObject, visual, false);
			rig.name = RigName;
			// The game drives the first animator it finds under her, which has to be this one
			rig.transform.SetAsFirstSibling();
			GameObject model = Object.Instantiate(introModel.gameObject, visual, false);
			model.name = "Valkyrie";
			model.transform.localPosition = Vector3.zero;
			// Her model faces back along the way the game moves her, so it's turned around to face forward
			model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
			Animator animator = model.GetComponent<Animator>();
			animator.runtimeAnimatorController = null;
			animator.applyRootMotion = false;
			animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
			foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				// Her wings reach far past her body's bounds once they're spread, so it's drawn wherever it's seen
				renderer.updateWhenOffscreen = true;
			}
			Transform bone = Utils.FindChild(model.transform, SaddleBone);
			if (bone == null)
			{
				Plugin.Log.LogWarning("Couldn't build the valkyrie - her " + SaddleBone + " bone is missing");
				Object.Destroy(valkyrie);
				return(null);
			}
			saddle.transform.SetParent(bone, false);
			ValkyrieModel posing = model.AddComponent<ValkyrieModel>();
			posing.m_fly = fly;
			posing.m_glide = glide;
			posing.m_idle = idle;
			posing.m_landing = landing;
			posing.m_saddleTurn = saddleTurn;
			posing.m_saddleSize = saddleSize;
			if (!PairRig(model, rig))
			{
				Plugin.Log.LogWarning("Couldn't build the valkyrie - her skeleton doesn't match the Fallen Valkyrie's");
				Object.Destroy(valkyrie);
				return(null);
			}
			HideRig(rig);
			SetFeet(valkyrie, model.transform);
			SetCollider(valkyrie, endAnimator);
			SetBody(body);
			ValkyrieCombat.SetStats(body, fallen.GetComponent<Humanoid>());
			LeaveCorpse(body, scene, introModel.gameObject);
			ValkyrieCombat.SetAttacks(body, fallen.GetComponent<Humanoid>(), scene, holder.transform);
			ValkyrieCombat.SetAI(valkyrie.GetComponent<MonsterAI>(), fallen.GetComponent<MonsterAI>(), holder.transform);
			ValkyrieCombat.SetDrops(valkyrie, fallen);
			RemoveComponent<Procreation>(valkyrie);
			valkyrie.AddComponent<ValkyrieGrab>();
			valkyrie.AddComponent<ValkyrieBanish>();
			valkyrie.AddComponent<ValkyrieSkirmish>();
			valkyrie.AddComponent<ValkyrieTalk>();
			valkyrie.AddComponent<ValkyrieSighting>();
			valkyrie.AddComponent<ValkyrieCatch>();
			valkyrie.AddComponent<ValkyriePassThrough>();
			valkyrie.AddComponent<ValkyriePack>();
			valkyrie.AddComponent<ValkyrieFlight>();
			valkyrie.AddComponent<ValkyrieColor>();
			return(valkyrie);
		}

		/// <summary>
		/// Has her leave a corpse as she dies - a creature that leaves none can't rise as a zombie with Zombie
		/// Apocalypse (see ValkyrieCorpse) - with the Fallen Valkyrie's death cry, but its burst of smoke held back for
		/// her corpse to vanish in a few seconds later. Zombie Apocalypse keeps a corpse that's rising and takes it away
		/// itself, so one she rises from never bursts
		/// </summary>
		/// <param name="body">Her body, with the Fallen Valkyrie's death effects</param>
		/// <param name="scene">The world's ZNetScene</param>
		/// <param name="introModel">The intro valkyrie's model</param>
		private static void LeaveCorpse(Humanoid body, ZNetScene scene, GameObject introModel)
		{
			EffectList.EffectData[] deathEffects = body.m_deathEffects.m_effectPrefabs;
			if (corpse == null)
			{
				corpse = ValkyrieCorpse.Build(scene, introModel, holder.transform);
				if (corpse != null)
				{
					Ragdoll ragdoll = corpse.GetComponent<Ragdoll>();
					ragdoll.m_ttl = CorpseTime;
					ragdoll.m_removeEffect = new EffectList { m_effectPrefabs = deathEffects.Where(Bursts).ToArray() };
				}
			}
			if (corpse == null)
			{
				return;
			}
			EffectList.EffectData leaves = new EffectList.EffectData { m_prefab = corpse };
			body.m_deathEffects = new EffectList { m_effectPrefabs = deathEffects.Where((EffectList.EffectData effect) => !Bursts(effect)).Concat(new[] { leaves }).ToArray() };
		}

		/// <summary>
		/// Checks whether one of the Fallen Valkyrie's death effects is its burst of smoke rather than its cry
		/// </summary>
		/// <param name="effect">The effect</param>
		/// <returns>True when it's the burst</returns>
		private static bool Bursts(EffectList.EffectData effect)
		{
			return(effect.m_prefab != null && effect.m_prefab.name.StartsWith("fx_"));
		}

		/// <summary>
		/// Pairs her skeleton with the hidden Fallen Valkyrie's for showing its attacks on her (see ValkyrieAttackPose),
		/// while both still have the renderers that skin their bodies to them
		/// </summary>
		/// <param name="model">Her model</param>
		/// <param name="rig">The hidden Fallen Valkyrie's model</param>
		/// <returns>True when they paired</returns>
		private static bool PairRig(GameObject model, GameObject rig)
		{
			SkinnedMeshRenderer skin = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault((SkinnedMeshRenderer renderer) => renderer.bones.Any((Transform bone) => bone != null && bone.name == "pelvis"));
			SkinnedMeshRenderer rigSkin = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault((SkinnedMeshRenderer renderer) => renderer.bones.Any((Transform bone) => bone != null && bone.name == "hip"));
			if (skin == null || rigSkin == null)
			{
				return(false);
			}
			return(model.AddComponent<ValkyrieAttackPose>().Pair(model.transform, skin, rig.transform, rigSkin));
		}

		/// <summary>
		/// Takes the Fallen Valkyrie's body, its flowing cloth, hit capsules, smoke and lights off its hidden skeleton,
		/// and keeps its animator running though nothing of it is seen, since its attacks land on its animation events
		/// </summary>
		/// <param name="rig">The hidden Fallen Valkyrie's model</param>
		private static void HideRig(GameObject rig)
		{
			// The cloth needs its renderer, which can't be taken off while the cloth is there
			foreach (Cloth cloth in rig.GetComponentsInChildren<Cloth>(true))
			{
				Object.DestroyImmediate(cloth);
			}
			foreach (Renderer renderer in rig.GetComponentsInChildren<Renderer>(true))
			{
				if (!(renderer is ParticleSystemRenderer))
				{
					Object.DestroyImmediate(renderer);
				}
			}
			foreach (ParticleSystem particles in rig.GetComponentsInChildren<ParticleSystem>(true))
			{
				particles.gameObject.SetActive(false);
			}
			foreach (Collider collider in rig.GetComponentsInChildren<Collider>(true))
			{
				Object.DestroyImmediate(collider);
			}
			foreach (Light light in rig.GetComponentsInChildren<Light>(true))
			{
				light.enabled = false;
			}
			foreach (LODGroup group in rig.GetComponentsInChildren<LODGroup>(true))
			{
				Object.DestroyImmediate(group);
			}
			rig.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
		}

		/// <summary>
		/// Loads the intro valkyrie through the player's prefab and holds her loaded
		/// </summary>
		/// <param name="playerPrefab">The player's prefab</param>
		/// <returns>Her model's child, with its animator, armature and body, or null when she can't be loaded</returns>
		private static Transform LoadIntroModel(GameObject playerPrefab)
		{
			Player player = playerPrefab.GetComponent<Player>();
			if (player == null)
			{
				return(null);
			}
			introValkyrie = player.m_valkyrie;
			if (introValkyrie.Load() != LoadResult.Succeeded)
			{
				Plugin.Log.LogWarning("The intro valkyrie's asset didn't load");
				return(null);
			}
			GameObject intro = introValkyrie.Asset;
			return(((intro != null) ? intro.transform.Find(ModelName) : null));
		}

		/// <summary>
		/// Finds the end credits valkyrie among the world's prefabs, or failing that in the start temple's spawner, which
		/// is then held loaded
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <returns>Her animator, or null when she can't be found</returns>
		private static Animator LoadEndAnimator(ZNetScene scene)
		{
			GameObject end = scene.GetPrefab(EndPrefab);
			if (end == null && Runtime.GetAllAssetPathsInBundleMappedToAssetID().TryGetValue(TemplePath, out AssetID templeID))
			{
				temple = new SoftReference<GameObject>(templeID);
				if (temple.Load() == LoadResult.Succeeded && temple.Asset != null)
				{
					Transform spawner = Utils.FindChild(temple.Asset.transform, TempleSpawner);
					SpawnPrefab spawn = ((spawner != null) ? spawner.GetComponent<SpawnPrefab>() : null);
					end = ((spawn != null) ? spawn.m_prefab : null);
				}
			}
			return(((end != null) ? end.GetComponentInChildren<Animator>(true) : null));
		}

		/// <summary>
		/// Finds one of an animator's clips by name
		/// </summary>
		/// <param name="animator">The animator</param>
		/// <param name="name">The clip's name</param>
		/// <returns>The clip, or null when it has none by that name</returns>
		private static AnimationClip FindClip(Animator animator, string name)
		{
			if (animator == null || animator.runtimeAnimatorController == null)
			{
				return(null);
			}
			return(animator.runtimeAnimatorController.animationClips.FirstOrDefault((AnimationClip clip) => clip.name == name));
		}

		/// <summary>
		/// Takes the Asksvin's model out, with its animator, its extra hit capsule and its level of detail
		/// </summary>
		/// <param name="visual">The prefab's visual</param>
		/// <param name="oldAnimator">The Asksvin's animator</param>
		private static void StripOldBody(Transform visual, Animator oldAnimator)
		{
			Object.DestroyImmediate(oldAnimator.gameObject);
			Transform capsule = visual.Find("Capsule");
			if (capsule != null)
			{
				Object.DestroyImmediate(capsule.gameObject);
			}
			RemoveComponent<LODGroup>(visual.gameObject);
		}

		/// <summary>
		/// Takes away the saddle's skinned renderer, which followed the Asksvin's bones, leaving it an unseen seat that
		/// still takes its rider, and gives it a seat of its own where the Asksvin's rider sat on it
		/// </summary>
		/// <param name="saddle">The saddle</param>
		/// <param name="seat">Where the Asksvin's rider sat, in the saddle's own space</param>
		private static void MakeSaddleRigid(Sadle saddle, Vector3 seat)
		{
			SkinnedMeshRenderer skinned = saddle.GetComponent<SkinnedMeshRenderer>();
			if (skinned != null)
			{
				Object.DestroyImmediate(skinned);
			}
			GameObject attach = new GameObject("AttachPoint");
			attach.transform.SetParent(saddle.transform, false);
			attach.transform.localPosition = seat;
			saddle.m_attachPoint = attach.transform;
		}

		/// <summary>
		/// Has her footsteps sound at her own feet
		/// </summary>
		/// <param name="valkyrie">The prefab</param>
		/// <param name="model">Her model</param>
		private static void SetFeet(GameObject valkyrie, Transform model)
		{
			FootStep footStep = valkyrie.GetComponent<FootStep>();
			if (footStep == null)
			{
				return;
			}
			footStep.m_feet = new Transform[] { Utils.FindChild(model, "l_foot"), Utils.FindChild(model, "r_foot") }.Where((Transform foot) => foot != null).ToArray();
		}

		/// <summary>
		/// Sizes her body's capsule to the end credits valkyrie's, as wide and reaching as high, but standing on the
		/// ground where the game puts her - that capsule reaches below her feet into the stone she stands on - with her
		/// eyes near its top
		/// </summary>
		/// <param name="valkyrie">The prefab</param>
		/// <param name="endAnimator">The end credits valkyrie's animator, whose capsule fits her standing</param>
		private static void SetCollider(GameObject valkyrie, Animator endAnimator)
		{
			CapsuleCollider endCapsule = endAnimator.GetComponent<CapsuleCollider>();
			CapsuleCollider capsule = valkyrie.GetComponent<CapsuleCollider>();
			if (endCapsule == null || capsule == null)
			{
				return;
			}
			float top = endCapsule.center.y + endCapsule.height / 2f;
			capsule.direction = 1;
			capsule.radius = endCapsule.radius;
			capsule.height = Mathf.Max(top, endCapsule.radius * 2f);
			capsule.center = new Vector3(0f, capsule.height / 2f, 0f);
			Transform eye = valkyrie.transform.Find("EyePos");
			if (eye != null)
			{
				eye.localPosition = new Vector3(0f, capsule.height * 0.9f, endCapsule.radius * 0.5f);
			}
		}

		/// <summary>
		/// Names her, puts her with the dvergr - friendly to players until one hurts her, which provokes her the way it
		/// does them, since her AI is provokable (see ValkyrieCombat.SetAI) - starts her on the ground and sets her
		/// speeds there, with the game's tilting of her body off, since her model leans on its own (see ValkyrieModel)
		/// </summary>
		/// <param name="body">Her body</param>
		private static void SetBody(Humanoid body)
		{
			body.m_name = DisplayName;
			body.m_faction = Character.Faction.Dverger;
			body.m_walkSpeed = WalkSpeed;
			body.m_speed = GroundSpeed;
			body.m_runSpeed = GroundRunSpeed;
			body.m_flying = false;
			body.m_groundTilt = Character.GroundTiltType.None;
		}

		/// <summary>
		/// Removes a component from a prefab right away, when it has one
		/// </summary>
		/// <typeparam name="T">The component's type</typeparam>
		/// <param name="target">The prefab or child</param>
		private static void RemoveComponent<T>(GameObject target) where T : Component
		{
			T component = target.GetComponent<T>();
			if (component != null)
			{
				Object.DestroyImmediate(component);
			}
		}
	}
}
