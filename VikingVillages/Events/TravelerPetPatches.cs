using HarmonyLib;
using Kriona.Vikings;

namespace VikingVillages
{
	/// <summary>
	/// Gives a traveler's animal its part as one as it loads on any client (see TravelerPet)
	/// </summary>
	[HarmonyPatch(typeof(Tameable), nameof(Tameable.Awake))]
	public static class TravelerPetLoadPatch
	{
		/// <summary>
		/// Adds the animal's part when its ZDO marks it as a traveler's
		/// </summary>
		/// <param name="__instance">The animal's Tameable</param>
		private static void Postfix(Tameable __instance)
		{
			ZNetView view = __instance.GetComponent<ZNetView>();
			if (view != null && view.IsValid() && TravelerPets.IsPet(view.GetZDO()) && __instance.GetComponent<TravelerPet>() == null)
			{
				__instance.gameObject.AddComponent<TravelerPet>();
			}
		}
	}

	/// <summary>
	/// Lets players pet a traveler's animal, but not tell it to follow or stay, or rename it, since it's the
	/// traveler's - until it's left to the village
	/// </summary>
	[HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
	public static class TravelerPetInteractPatch
	{
		/// <summary>
		/// Pets the animal the way the game pets one that can't be told what to do, and skips renaming it
		/// </summary>
		/// <param name="__instance">The animal's Tameable</param>
		/// <param name="user">The player</param>
		/// <param name="hold">Whether the key is held</param>
		/// <param name="alt">Whether the alternate key is down</param>
		/// <param name="__result">Whether the interaction did anything</param>
		/// <returns>False for a traveler's animal, to skip the game's interaction</returns>
		private static bool Prefix(Tameable __instance, Humanoid user, bool hold, bool alt, ref bool __result)
		{
			TravelerPet pet = TravelerPet.Of(__instance.m_character);
			// A valkyrie is talked to rather than petted (see ValkyrieTalkPatch)
			if (pet == null || pet.Orphaned() || __instance.GetComponent<ValkyrieTalk>() != null)
			{
				return(true);
			}
			__result = false;
			if (hold || alt || !__instance.IsTamed() || UnityEngine.Time.time - __instance.m_lastPetTime <= 1f)
			{
				return(false);
			}
			__instance.m_lastPetTime = UnityEngine.Time.time;
			__instance.m_petEffect.Create(__instance.transform.position, __instance.transform.rotation);
			string name = __instance.GetHoverName();
			user.Message(MessageHud.MessageType.Center, (__instance.m_nameBeforeText ? name + " " + __instance.m_tameText : __instance.m_tameText));
			__result = true;
			return(false);
		}
	}

	/// <summary>
	/// Shows a traveler's animal with only the prompt to pet it, until it's left to the village
	/// </summary>
	[HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
	public static class TravelerPetHoverPatch
	{
		/// <summary>
		/// Replaces the hover text of a traveler's animal with its name and the prompt to pet it
		/// </summary>
		/// <param name="__instance">The animal's Tameable</param>
		/// <param name="__result">Its hover text</param>
		private static void Postfix(Tameable __instance, ref string __result)
		{
			TravelerPet pet = TravelerPet.Of(__instance.m_character);
			// A valkyrie shows her own hover text (see ValkyrieHoverPatch)
			if (pet == null || pet.Orphaned() || !__instance.IsTamed() || __instance.GetComponent<ValkyrieTalk>() != null)
			{
				return;
			}
			__result = Localization.instance.Localize(__instance.GetHoverName() + "\n[<color=yellow><b>$KEY_Use</b></color>] $hud_pet");
		}
	}

	/// <summary>
	/// Keeps players from riding a traveler's saddled animal, since it's the traveler's - until it's left to the village.
	/// Using its saddle does what using the animal does instead - petting it, or talking to a valkyrie
	/// </summary>
	[HarmonyPatch(typeof(Sadle), nameof(Sadle.Interact))]
	public static class TravelerPetSaddlePatch
	{
		/// <summary>
		/// Uses a traveler's animal in place of getting on it, after the valkyrie's own seat rules (see
		/// ValkyrieSeatInteractPatch)
		/// </summary>
		/// <param name="__instance">The saddle</param>
		/// <param name="character">Who uses it</param>
		/// <param name="repeat">Whether the key is held</param>
		/// <param name="alt">Whether the alternate key is down</param>
		/// <param name="__result">Whether the interaction did anything</param>
		/// <returns>False for a traveler's animal, to skip the game's interaction</returns>
		[HarmonyPriority(Priority.Last)]
		private static bool Prefix(Sadle __instance, Humanoid character, bool repeat, bool alt, ref bool __result)
		{
			TravelerPet pet = TravelerPet.Of(__instance.m_character);
			if (pet == null || pet.Orphaned())
			{
				return(true);
			}
			Tameable tameable = __instance.m_character.GetComponent<Tameable>();
			__result = (tameable != null && tameable.Interact(character, repeat, alt));
			return(false);
		}
	}

	/// <summary>
	/// Shows a traveler's saddled animal's back with what its body shows - the prompt to pet it, or to talk to a
	/// valkyrie - rather than the prompts to ride it or take its saddle off, until it's left to the village
	/// </summary>
	[HarmonyPatch(typeof(Sadle), nameof(Sadle.GetHoverText))]
	public static class TravelerPetSaddleHoverPatch
	{
		/// <summary>
		/// Replaces a traveler's animal's saddle prompts with its body's, after the valkyrie's own seat prompt (see
		/// ValkyrieSeatHoverPatch)
		/// </summary>
		/// <param name="__instance">The saddle</param>
		/// <param name="__result">The hover text</param>
		[HarmonyPriority(Priority.Last)]
		private static void Postfix(Sadle __instance, ref string __result)
		{
			TravelerPet pet = TravelerPet.Of(__instance.m_character);
			if (pet == null || pet.Orphaned())
			{
				return;
			}
			Tameable tameable = __instance.m_character.GetComponent<Tameable>();
			__result = ((tameable != null) ? tameable.GetHoverText() : "");
		}
	}

	/// <summary>
	/// Keeps a traveler's valkyrie and her traveler from being hurt by a player who hasn't turned on PVP, the way the
	/// game keeps players from hurting each other (see TravelerPets.NeedsPVP)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
	public static class TravelerPVPPatch
	{
		/// <summary>
		/// Skips a hit from a player without PVP on, unless it ignores PVP the way the game's own checks allow
		/// </summary>
		/// <param name="__instance">The creature hit</param>
		/// <param name="hit">The hit</param>
		/// <returns>False to skip the hit</returns>
		private static bool Prefix(Character __instance, HitData hit)
		{
			if (hit == null || hit.m_ignorePVP || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
			{
				return(true);
			}
			Character attacker = hit.GetAttacker();
			if (attacker == null || !attacker.IsPlayer() || attacker.IsPVPEnabled())
			{
				return(true);
			}
			return(!TravelerPets.NeedsPVP(__instance));
		}
	}

	/// <summary>
	/// Keeps a traveler's animal at peace with the vikings - which the game would set it against while they're
	/// aggravated, being of the Dverger faction - and sets it against the players while its traveler is angry at them
	/// </summary>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IsEnemy), typeof(Character), typeof(Character))]
	public static class TravelerPetEnemyPatch
	{
		/// <summary>
		/// Settles whether a traveler's animal and another creature are enemies, after the mod's other rules (see
		/// HuntEnemyPatch)
		/// </summary>
		/// <param name="a">One creature</param>
		/// <param name="b">The other</param>
		/// <param name="__result">Whether they're enemies</param>
		[HarmonyPriority(Priority.Last)]
		private static void Postfix(Character a, Character b, ref bool __result)
		{
			TravelerPet first = TravelerPet.Of(a);
			TravelerPet second = TravelerPet.Of(b);
			if ((first == null) == (second == null))
			{
				return;
			}
			TravelerPet pet = (first ?? second);
			Character other = ((first != null) ? b : a);
			if (VikingCore.IsLivingViking(other))
			{
				__result = false;
			}
			else if (other.IsPlayer())
			{
				__result = pet.Angry();
			}
		}
	}

	/// <summary>
	/// Moves a traveler's animal where its AI wouldn't take it - carrying its traveler riding it, walking along a path
	/// to where it takes them, and heading off out of sight once its traveler has gone, the way the game's night monsters
	/// leave at dawn, gone once no player is near - or for a valkyrie, flying, and coming down to her traveler when they
	/// call her as they leave (see TravelerPet)
	/// </summary>
	/// <remarks>
	/// A valkyrie's own patch on the AI's update flies her wherever she's piloted (see ValkyrieAIFlightPatch), so the two
	/// work in either order
	/// </remarks>
	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
	public static class TravelerPetMovePatch
	{
		/// <summary>
		/// Moves an animal carrying its traveler, departing, or a valkyrie coming down to her traveler, in place of its AI
		/// </summary>
		/// <param name="__instance">The animal's AI</param>
		/// <param name="dt">Seconds since the last update</param>
		/// <param name="__result">Whether the AI's update ran, set to true</param>
		/// <returns>False for an animal carrying its traveler or departing, to skip the AI's own update</returns>
		private static bool Prefix(MonsterAI __instance, float dt, ref bool __result)
		{
			TravelerPet pet = TravelerPet.Of(__instance.m_character);
			if (pet == null || !__instance.m_nview.IsOwner())
			{
				return(true);
			}
			if (pet.Departing())
			{
				if (!pet.FlyOff())
				{
					__instance.MoveAwayAndDespawn(dt, false);
				}
			}
			else if (pet.Carrying(out UnityEngine.Vector3 to))
			{
				if (!pet.FlyTo(to) && __instance.MoveTo(dt, to, 1f, false))
				{
					__instance.StopMoving();
				}
			}
			else if (pet.Answering(out UnityEngine.Vector3 down))
			{
				pet.FlyDown(down);
			}
			else
			{
				return(true);
			}
			__result = true;
			return(false);
		}
	}
}
