using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_cart console command, for trying out a village's cart - saying what it's up to, breaking it so its
	/// vikings build another, or moving it out of the village so they fetch it back
	/// </summary>
	internal static class CartCommand
	{
		/// <summary>
		/// The command's name and its words, and how far past the village's edge a cart is moved out to, in meters
		/// </summary>
		private const string Command = "kri_vv_cart";
		private const string BreakWord = "break";
		private const string AstrayWord = "astray";
		private const float AstrayDistance = 15f;

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand(Command, "[" + BreakWord + "|" + AstrayWord + "] - says what the cart of the village you're in is up to. " + BreakWord + " breaks it, dropping what it's made of and holds, so its vikings build another, and " + AstrayWord + " moves it " + AstrayDistance.ToString("F0") + " meters out of the village for them to fetch back", Run, isCheat: true, optionsFetcher: () => new List<string> { BreakWord, AstrayWord });
		}

		/// <summary>
		/// Runs the command
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Run(Terminal.ConsoleEventArgs args)
		{
			Player player = Player.m_localPlayer;
			Village village = ((player != null) ? Village.Find(player.transform.position) : null);
			if (village == null)
			{
				args.Context.AddString("Stand in a village first");
				return;
			}
			VillageCarts.Refresh(village);
			VillageCarts.Status status = VillageCarts.Of(village);
			string word = ((args.Length > 1) ? args[1].ToLowerInvariant() : "");
			if (word.Length == 0)
			{
				args.Context.AddString(VillageCarts.Describe(village).TrimEnd('\n'));
				if (status.State == CartRules.CartState.None)
				{
					args.Context.AddString("This village keeps no cart");
				}
				return;
			}
			if (status.Cart == null)
			{
				args.Context.AddString("The village's cart isn't loaded here");
				return;
			}
			ZNetView view = status.Cart.m_nview;
			view.ClaimOwnership();
			if (word == BreakWord)
			{
				WearNTear wear = status.Cart.GetComponent<WearNTear>();
				if (wear != null)
				{
					wear.Remove();
				}
				args.Context.AddString("Broke the village's cart");
				return;
			}
			if (word == AstrayWord)
			{
				Vector3 at = status.Cart.transform.position;
				Vector3 outward = at - village.Middle;
				outward.y = 0f;
				outward = ((outward.sqrMagnitude > 0.01f) ? outward.normalized : Vector3.forward);
				Vector3 spot = at;
				for (int i = 0; i < 200 && village.DistanceOutside(spot) < AstrayDistance; i++)
				{
					spot += outward;
				}
				spot.y = ZoneSystem.instance.GetGroundHeight(spot) + 0.3f;
				CartHitch.Unhitch(status.Cart);
				CartHitch.Move(status.Cart, spot, status.Cart.transform.rotation);
				args.Context.AddString("Moved the village's cart to " + spot.ToString("F0"));
				return;
			}
			args.Context.AddString("Give " + BreakWord + ", " + AstrayWord + " or nothing");
		}
	}
}
