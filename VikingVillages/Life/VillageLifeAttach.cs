using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Holding a viking in place - facing a spot, sitting or lying attached to a chair or bed, and the hammer or prop
	/// it holds while it works
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Stops the viking and turns it to face a point
		/// </summary>
		/// <param name="point">The point</param>
		private void Face(Vector3 point)
		{
			ai.StopMoving();
			Vector3 look = point;
			look.y = humanoid.m_eye.position.y;
			ai.LookAt(look);
		}

		/// <summary>
		/// Lays the viking on a bed or sits it on a chair, ignoring the piece's colliders while it's there
		/// </summary>
		/// <param name="point">Where it lies or sits</param>
		/// <param name="piece">The bed or chair</param>
		private void Attach(Transform point, GameObject piece)
		{
			Detach();
			attachPoint = point;
			inBed = (piece.GetComponent<Bed>() != null);
			attachColliders = piece.GetComponentsInChildren<Collider>();
			foreach (Collider collider in attachColliders)
			{
				Physics.IgnoreCollision(humanoid.m_collider, collider, true);
			}
			humanoid.m_body.useGravity = false;
			body.SetSeated(true);
			HoldAttached();
		}

		/// <summary>
		/// Gets the viking off the bed or chair it's on, a little above it
		/// </summary>
		private void Detach()
		{
			if (attachPoint == null && attachColliders == null)
			{
				return;
			}
			DetachTo((attachPoint != null) ? attachPoint.position + Vector3.up * 0.5f : transform.position);
		}

		/// <summary>
		/// Gets the viking off the bed or chair it's on, standing at a spot
		/// </summary>
		/// <param name="standing">Where it stands</param>
		private void DetachTo(Vector3 standing)
		{
			if (attachPoint == null && attachColliders == null)
			{
				return;
			}
			if (attachColliders != null)
			{
				foreach (Collider collider in attachColliders)
				{
					if (collider != null && humanoid.m_collider != null)
					{
						Physics.IgnoreCollision(humanoid.m_collider, collider, false);
					}
				}
			}
			attachPoint = null;
			attachColliders = null;
			inBed = false;
			if (body != null)
			{
				body.SetSeated(false);
			}
			if (humanoid != null && humanoid.m_body != null)
			{
				humanoid.m_body.useGravity = true;
				humanoid.m_body.position = standing;
				transform.position = standing;
			}
		}

		/// <summary>
		/// Holds the viking where it lies or sits, still - with its gravity off, which the game's own motion update turns
		/// back on every physics update, the way it does for a player sitting down - moving with what it sits on when that
		/// moves, like a ship, so it keeps up with it between physics updates rather than jumping after it (see
		/// Player.UpdateAttach)
		/// </summary>
		private void HoldAttached()
		{
			transform.position = attachPoint.position;
			transform.rotation = attachPoint.rotation;
			humanoid.m_body.position = attachPoint.position;
			humanoid.m_body.rotation = attachPoint.rotation;
			humanoid.m_body.useGravity = false;
			Rigidbody carrier = attachPoint.GetComponentInParent<Rigidbody>();
			humanoid.m_body.linearVelocity = ((carrier != null) ? carrier.GetPointVelocity(attachPoint.position) : Vector3.zero);
			humanoid.m_body.angularVelocity = Vector3.zero;
			humanoid.m_maxAirAltitude = attachPoint.position.y;
			// Its seat's colliders are ignored, so it touches no ground - it's kept counted as on the ground, and still,
			// or its animation flips between falling, walking and sitting
			humanoid.m_lastGroundTouch = 0f;
			humanoid.m_currentVel = Vector3.zero;
			humanoid.SetMoveDir(Vector3.zero);
		}

		/// <summary>
		/// Holds the viking on the bed or chair it's on right after the game's motion update has moved it, so the physics
		/// that follows has nothing to move
		/// </summary>
		public void AfterMotion()
		{
			if (attachPoint != null && nview != null && nview.IsValid() && nview.IsOwner())
			{
				HoldAttached();
			}
		}

		/// <summary>
		/// Gives the viking a hammer to hold for show, or takes away whatever it holds for show
		/// </summary>
		/// <param name="hold">True to hold a hammer</param>
		private void HoldHammer(bool hold)
		{
			if (hold)
			{
				HoldProp(HammerPrefab);
				return;
			}
			if (humanoid == null)
			{
				return;
			}
			Inventory inventory = humanoid.GetInventory();
			ItemDrop.ItemData prop = inventory.GetAllItems().Find((ItemDrop.ItemData item) => VikingItems.IsProp(item));
			if (prop == null)
			{
				return;
			}
			if (humanoid.IsItemEquiped(prop))
			{
				humanoid.UnequipItem(prop, false);
			}
			inventory.RemoveItem(prop);
		}

		/// <summary>
		/// Gives the viking something to hold for show while it works, like a hammer or an axe, in place of any other it
		/// holds - taken away again when its job ends (see HoldHammer)
		/// </summary>
		/// <param name="prefab">The item's prefab name</param>
		private void HoldProp(string prefab)
		{
			if (humanoid == null || GearHeld())
			{
				return;
			}
			Inventory inventory = humanoid.GetInventory();
			ItemDrop.ItemData prop = inventory.GetAllItems().Find((ItemDrop.ItemData item) => VikingItems.IsProp(item));
			if (prop != null && (prop.m_dropPrefab == null || prop.m_dropPrefab.name != prefab))
			{
				HoldHammer(false);
				prop = null;
			}
			if (prop == null)
			{
				prop = inventory.AddItem(prefab, 1, 1, 0, 0L, "", false);
				if (prop == null)
				{
					return;
				}
				prop.m_customData[VikingItems.PropKey] = "1";
			}
			if (!humanoid.IsItemEquiped(prop))
			{
				humanoid.EquipItem(prop, false);
			}
		}
	}
}
