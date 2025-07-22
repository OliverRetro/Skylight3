﻿using Skylight.API.Game.Furniture.Floor.Wired.Triggers;
using Skylight.API.Game.Rooms.Items;
using Skylight.API.Game.Rooms.Items.Floor.Wired.Triggers;
using Skylight.API.Game.Rooms.Private;
using Skylight.API.Game.Rooms.Units;
using Skylight.API.Game.Users;
using Skylight.API.Numerics;

namespace Skylight.Server.Game.Rooms.Items.Floor.Wired.Triggers;

internal abstract class WiredTriggerRoomItem<T>(IPrivateRoom room, RoomItemId id, IUserInfo owner, T furniture, Point3D position, int direction)
	: FloorRoomItem<T>(room, id, owner, furniture, position, direction), IWiredTriggerRoomItem
	where T : IWiredTriggerFurniture
{
	public new IWiredTriggerFurniture Furniture => this.furniture;

	public bool Interact(IUserRoomUnit unit, int state)
	{
		if (!this.Room.IsOwner(unit.User))
		{
			return false;
		}

		this.Open(unit);

		return true;
	}

	public abstract void Open(IUserRoomUnit unit);
}
