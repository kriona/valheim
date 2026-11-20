using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieApocalypse
{
	/// <summary>
	/// Makes a zombie's eyes glow ZombieEyeColor
	/// </summary>
	/// <remarks>
	/// A creature with eyes of their own material (e.g., a greydwarf's) has that material recolored. A creature whose
	/// eyes glow through its skin's emission map (e.g., a dvergr's) has the glow recolored at the same brightness,
	/// which recolors anything else the map lights up too. A creature with its eyes painted into its skin but a bone
	/// for each eye (e.g., a boar, wolf or troll) gets a small glowing eyeball on each of those bones, made from the
	/// greydwarf brute's red eye material so it glows like the game's own eyes. A creature with none of these keeps its
	/// eyes as they are. On a zombie's corpse the eyes stay ZombieEyeColor but stop glowing, and glowing skin goes
	/// dark. The color is applied again every second so changes to the settings show
	/// </remarks>
	internal class ZombieEyes : MonoBehaviour
	{
		private const float RefreshInterval = 1f;

		/// <summary>
		/// How bright the eyes glow, as a multiple of their color
		/// </summary>
		private const float GlowIntensity = 3f;

		/// <summary>
		/// An added eyeball's size as a fraction of the distance between the eyes
		/// </summary>
		private const float EyeSize = 0.3f;

		/// <summary>
		/// The prefab and material the added eyeballs take their glowing material from
		/// </summary>
		private const string EyeMaterialPrefab = "Greydwarf_Elite";
		private const string EyeMaterialName = "eye_red";

		private static readonly int ColorId = Shader.PropertyToID("_Color");
		private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
		private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

		/// <summary>
		/// The brightest an emission color's channel can be and still count as not glowing
		/// </summary>
		private const float NoGlow = 0.1f;

		/// <summary>
		/// Parts of bone names that mark a bone near the eye that isn't an eye
		/// </summary>
		private static readonly string[] NotEyes = new string[] { "brow", "end", "pos", "lid", "lash" };

		private static Mesh sphereMesh;
		private static Material eyeMaterial;
		private static bool lookedForMaterial;

		private readonly List<Renderer> eyeRenderers = new List<Renderer>();
		private readonly List<Renderer> glowRenderers = new List<Renderer>();
		private readonly List<GameObject> addedEyes = new List<GameObject>();
		private MaterialPropertyBlock block;
		private float nextRefresh;

		/// <summary>
		/// Whether the eyes are on a corpse, which doesn't glow
		/// </summary>
		private bool dead;

		/// <summary>
		/// Checks whether a material is an eye's, which the zombie's skin look leaves alone
		/// </summary>
		/// <param name="material">The material</param>
		/// <returns>True when its name has "eye" in it</returns>
		public static bool IsEyeMaterial(Material material)
		{
			return(material.name.IndexOf("eye", System.StringComparison.OrdinalIgnoreCase) >= 0);
		}

		/// <summary>
		/// Checks whether a material glows through an emission map, the way a dvergr's eyes do
		/// </summary>
		/// <param name="material">The material</param>
		/// <returns>True when it has an emission map and a bright enough emission color</returns>
		private static bool IsGlowingSkin(Material material)
		{
			if (IsEyeMaterial(material) || !material.HasProperty(EmissionMapId) || !material.HasProperty(EmissionColorId) || material.GetTexture(EmissionMapId) == null)
			{
				return(false);
			}
			return(material.GetColor(EmissionColorId).maxColorComponent > NoGlow);
		}

		/// <summary>
		/// Finds the creature's eye materials and glowing skin, or adds eyeballs to its eye bones when it has neither
		/// </summary>
		private void Start()
		{
			dead = (GetComponent<Ragdoll>() != null);
			foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
			{
				bool eye = false;
				bool glow = false;
				foreach (Material material in renderer.sharedMaterials)
				{
					if (material != null)
					{
						eye |= IsEyeMaterial(material);
						glow |= IsGlowingSkin(material);
					}
				}
				if (eye)
				{
					eyeRenderers.Add(renderer);
				}
				if (glow)
				{
					glowRenderers.Add(renderer);
				}
			}
			if (eyeRenderers.Count == 0 && glowRenderers.Count == 0)
			{
				AddEyeballs();
			}
			Refresh();
		}

		/// <summary>
		/// Applies the eye color again every second
		/// </summary>
		private void Update()
		{
			if (Time.time >= nextRefresh)
			{
				Refresh();
			}
		}

		/// <summary>
		/// Colors the eye materials, glowing skin and added eyeballs, without a glow on a corpse, or puts them back when
		/// ZombieEyes is off
		/// </summary>
		private void Refresh()
		{
			nextRefresh = Time.time + RefreshInterval;
			if (block == null)
			{
				block = new MaterialPropertyBlock();
			}
			bool on = Plugin.ZombieEyes.Value;
			Color color = Plugin.ZombieEyeColor.Value;
			Color glow = (dead ? Color.black : color * GlowIntensity);
			foreach (Renderer renderer in eyeRenderers)
			{
				if (renderer == null)
				{
					continue;
				}
				Material[] materials = renderer.sharedMaterials;
				for (int i = 0; i < materials.Length; i++)
				{
					Material material = materials[i];
					if (material == null || !IsEyeMaterial(material))
					{
						continue;
					}
					renderer.GetPropertyBlock(block, i);
					if (material.HasProperty(ColorId))
					{
						block.SetColor(ColorId, (on ? color : material.GetColor(ColorId)));
					}
					if (material.HasProperty(EmissionColorId))
					{
						block.SetColor(EmissionColorId, (on ? glow : material.GetColor(EmissionColorId)));
					}
					renderer.SetPropertyBlock(block, i);
				}
			}
			foreach (Renderer renderer in glowRenderers)
			{
				if (renderer == null)
				{
					continue;
				}
				Material[] materials = renderer.sharedMaterials;
				for (int i = 0; i < materials.Length; i++)
				{
					Material material = materials[i];
					if (material == null || !IsGlowingSkin(material))
					{
						continue;
					}
					// The glow keeps the brightness of the creature's own
					Color original = material.GetColor(EmissionColorId);
					Color recolored = (dead ? Color.black : color * original.maxColorComponent);
					renderer.GetPropertyBlock(block, i);
					block.SetColor(EmissionColorId, (on ? recolored : original));
					renderer.SetPropertyBlock(block, i);
				}
			}
			foreach (GameObject eye in addedEyes)
			{
				if (eye == null)
				{
					continue;
				}
				eye.SetActive(on);
				Renderer renderer = eye.GetComponent<Renderer>();
				renderer.GetPropertyBlock(block);
				block.SetColor(ColorId, color);
				block.SetColor(EmissionColorId, glow);
				renderer.SetPropertyBlock(block);
			}
		}

		/// <summary>
		/// Puts a small glowing eyeball on each of the creature's eye bones, when it has at least two
		/// </summary>
		private void AddEyeballs()
		{
			List<Transform> eyeBones = new List<Transform>();
			Character character = GetComponent<Character>();
			Transform skeleton = ((character != null && character.m_animator != null) ? character.m_animator.transform : transform);
			foreach (Transform bone in skeleton.GetComponentsInChildren<Transform>(true))
			{
				if (IsEyeBone(bone.name))
				{
					eyeBones.Add(bone);
				}
			}
			// A single bone for both eyes can't place them apart
			if (eyeBones.Count < 2 || !FindEyeAssets())
			{
				return;
			}
			float size = Vector3.Distance(eyeBones[0].position, eyeBones[1].position) * EyeSize;
			foreach (Transform bone in eyeBones)
			{
				GameObject eye = new GameObject("ZombieEye");
				eye.layer = bone.gameObject.layer;
				eye.transform.SetParent(bone, false);
				float scale = bone.lossyScale.x;
				eye.transform.localScale = Vector3.one * ((scale > 0f) ? size / scale : size);
				eye.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
				MeshRenderer renderer = eye.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = eyeMaterial;
				renderer.shadowCastingMode = ShadowCastingMode.Off;
				renderer.receiveShadows = false;
				addedEyes.Add(eye);
			}
		}

		/// <summary>
		/// Checks whether a bone's name marks it as an eye
		/// </summary>
		/// <param name="name">The bone's name</param>
		/// <returns>True for names like "Eye L", "LeftEye" or "DEF-eye.R", but not "Eye L_end" or "LeftEyeBrow"</returns>
		private static bool IsEyeBone(string name)
		{
			string lower = name.ToLowerInvariant();
			if (!lower.Contains("eye"))
			{
				return(false);
			}
			foreach (string part in NotEyes)
			{
				if (lower.Contains(part))
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Finds the sphere mesh and glowing eye material the added eyeballs use, once
		/// </summary>
		/// <returns>True when both were found</returns>
		private static bool FindEyeAssets()
		{
			if (sphereMesh == null)
			{
				GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				sphereMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
				DestroyImmediate(sphere);
			}
			if (!lookedForMaterial && ZNetScene.instance != null)
			{
				lookedForMaterial = true;
				GameObject prefab = ZNetScene.instance.GetPrefab(EyeMaterialPrefab);
				if (prefab != null)
				{
					foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
					{
						foreach (Material material in renderer.sharedMaterials)
						{
							if (material != null && material.name == EyeMaterialName)
							{
								eyeMaterial = material;
							}
						}
					}
				}
			}
			return(sphereMesh != null && eyeMaterial != null);
		}
	}
}
