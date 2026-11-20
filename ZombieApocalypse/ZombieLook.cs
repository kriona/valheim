using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Gives a zombie or corpse its rotten look - ZombieColor, ZombieSaturation and dark patches from Mottling
	/// </summary>
	/// <remarks>
	/// The look goes in each renderer's property blocks on top of its materials' own color and texture, so a starred
	/// creature keeps its level's colors underneath. Each material's texture is swapped for its washed-out, mottled
	/// copy, which a corpse turns through in steps while it lies there. It's applied again every second to catch
	/// equipment and level materials added after the zombie spawned, and changes to the settings
	/// </remarks>
	internal class ZombieLook : MonoBehaviour
	{
		private const float RefreshInterval = 1f;

		private static readonly int ColorId = Shader.PropertyToID("_Color");
		private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

		/// <summary>
		/// How much of the look is shown, from 0 for none to 1 for all of it
		/// </summary>
		public float Amount = 1f;

		private MaterialPropertyBlock block;
		private float appliedAmount = -1f;
		private float nextRefresh;

		/// <summary>
		/// Applies the look when Amount has changed or a second has passed
		/// </summary>
		private void Update()
		{
			if (Amount != appliedAmount || Time.time >= nextRefresh)
			{
				Refresh();
			}
		}

		/// <summary>
		/// Applies the look to every mesh renderer under the object by Amount
		/// </summary>
		private void Refresh()
		{
			if (block == null)
			{
				block = new MaterialPropertyBlock();
			}
			appliedAmount = Amount;
			nextRefresh = Time.time + RefreshInterval;
			Color tint = Color.Lerp(Color.white, Plugin.ZombieColor.Value, Amount);
			int step = Mathf.CeilToInt(Amount * Mottling.Steps);
			foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
			{
				if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))
				{
					continue;
				}
				Material[] materials = renderer.sharedMaterials;
				for (int i = 0; i < materials.Length; i++)
				{
					Material material = materials[i];
					// Eyes are colored by ZombieEyes, and graying them would hide it
					if (material == null || ZombieEyes.IsEyeMaterial(material))
					{
						continue;
					}
					bool hasColor = material.HasProperty(ColorId);
					Texture texture = (material.HasProperty(MainTexId) ? material.GetTexture(MainTexId) : null);
					if (!hasColor && texture == null)
					{
						continue;
					}
					renderer.GetPropertyBlock(block, i);
					if (hasColor)
					{
						block.SetColor(ColorId, material.GetColor(ColorId) * tint);
					}
					if (texture != null)
					{
						block.SetTexture(MainTexId, Mottling.Get(texture, step));
					}
					renderer.SetPropertyBlock(block, i);
				}
			}
		}
	}
}
