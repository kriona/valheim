using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Hooks carts up to vikings the way a player hooks one up - the game's own joint pulls it along - and sets a cart
	/// down behind its viking when it has snagged on something or come loose, so it's hooked up again
	/// </summary>
	/// <remarks>
	/// The game unhooks every cart on a client as any cart is hooked up, since a player only pulls one, so hooking one up
	/// only unhooks the carts hooked to the same puller (see CartHitchPatches) - each viking pulls its own. A cart is
	/// pulled by the client that owns it, so the viking's client takes it over first
	/// </remarks>
	internal static class CartHitch
	{
		/// <summary>
		/// The puller a cart is being hooked up to right now, so only its own carts come loose (see CartHitchPatches)
		/// </summary>
		internal static GameObject Attaching;

		/// <summary>
		/// How far above the ground a cart set down is lifted, so its wheels don't start under it, in meters
		/// </summary>
		private const float SetDownLift = 0.2f;

		/// <summary>
		/// The least a cart has to stand up for the game to let it be hooked up (see Vagon.CanAttach)
		/// </summary>
		private const float UprightMin = 0.1f;

		/// <summary>
		/// Hooks a cart up to a viking - taking it over for this client, setting it down behind the viking first when
		/// its handle is too far from the viking for the game to hook it up, or it has tipped over
		/// </summary>
		/// <param name="cart">The cart</param>
		/// <param name="puller">The viking, run by this client</param>
		/// <param name="behind">Which way is behind the viking, along the way it came, seen from above</param>
		/// <returns>True when it's hooked up</returns>
		public static bool Hitch(Vagon cart, Humanoid puller, Vector3 behind)
		{
			if (cart == null || puller == null || cart.m_nview == null || !cart.m_nview.IsValid())
			{
				return(false);
			}
			if (!cart.m_nview.IsOwner())
			{
				cart.m_nview.ClaimOwnership();
			}
			Vector3 handle = puller.transform.position + cart.m_attachOffset;
			if (cart.transform.up.y < UprightMin || Vector3.Distance(handle, cart.m_attachPoint.position) >= cart.m_detachDistance * 0.8f)
			{
				SetDown(cart, puller, behind);
			}
			cart.AttachTo(puller.gameObject);
			return(IsHitched(cart, puller));
		}

		/// <summary>
		/// Unhooks a cart from whoever pulls it on this client
		/// </summary>
		/// <param name="cart">The cart</param>
		public static void Unhitch(Vagon cart)
		{
			if (cart != null && cart.m_attachJoin != null)
			{
				cart.Detach();
			}
		}

		/// <summary>
		/// Checks whether a cart is hooked up to a viking on this client
		/// </summary>
		/// <param name="cart">The cart</param>
		/// <param name="puller">The viking</param>
		/// <returns>True when it is</returns>
		public static bool IsHitched(Vagon cart, Character puller)
		{
			return(cart != null && puller != null && cart.m_attachJoin != null && cart.m_attachJoin.connectedBody != null && cart.m_attachedObject == puller.gameObject);
		}

		/// <summary>
		/// Sets a cart down behind its viking, its handle at the viking's back, standing upright on the ground and still
		/// </summary>
		/// <param name="cart">The cart, owned by this client</param>
		/// <param name="puller">The viking</param>
		/// <param name="behind">Which way is behind the viking, seen from above</param>
		public static void SetDown(Vagon cart, Character puller, Vector3 behind)
		{
			behind.y = 0f;
			if (behind.sqrMagnitude < 0.0001f)
			{
				behind = -puller.transform.forward;
				behind.y = 0f;
			}
			behind.Normalize();
			// The handle points from the cart toward the viking, so the cart faces along the way it's pulled
			Vector3 local = cart.transform.InverseTransformPoint(cart.m_attachPoint.position);
			Vector3 handleWay = new Vector3(local.x, 0f, local.z);
			float yaw = Mathf.Atan2(-behind.x, -behind.z) * Mathf.Rad2Deg;
			float handleYaw = ((handleWay.sqrMagnitude > 0.0001f) ? Mathf.Atan2(handleWay.x, handleWay.z) * Mathf.Rad2Deg : 0f);
			Quaternion rotation = Quaternion.Euler(0f, yaw - handleYaw, 0f);
			Vector3 handle = puller.transform.position + cart.m_attachOffset;
			Vector3 origin = handle - rotation * local;
			float ground = ((ZoneSystem.instance != null) ? ZoneSystem.instance.GetGroundHeight(origin) : origin.y);
			origin.y = Mathf.Max(ground, puller.transform.position.y - 0.5f) + SetDownLift;
			Move(cart, origin, rotation);
		}

		/// <summary>
		/// Moves a cart and its wheels to a spot, standing still
		/// </summary>
		/// <param name="cart">The cart, owned by this client</param>
		/// <param name="position">Where it stands</param>
		/// <param name="rotation">Which way it faces</param>
		public static void Move(Vagon cart, Vector3 position, Quaternion rotation)
		{
			// The wheels are bodies of their own, so each keeps its place on the cart as it moves
			List<KeyValuePair<Rigidbody, Vector3>> offsets = new List<KeyValuePair<Rigidbody, Vector3>>();
			List<Quaternion> turns = new List<Quaternion>();
			foreach (Rigidbody body in cart.m_bodies)
			{
				if (body != null)
				{
					offsets.Add(new KeyValuePair<Rigidbody, Vector3>(body, cart.transform.InverseTransformPoint(body.position)));
					turns.Add(Quaternion.Inverse(cart.transform.rotation) * body.rotation);
				}
			}
			cart.transform.SetPositionAndRotation(position, rotation);
			for (int i = 0; i < offsets.Count; i++)
			{
				Rigidbody body = offsets[i].Key;
				body.position = cart.transform.TransformPoint(offsets[i].Value);
				body.rotation = rotation * turns[i];
				body.linearVelocity = Vector3.zero;
				body.angularVelocity = Vector3.zero;
			}
			Physics.SyncTransforms();
		}
	}
}
