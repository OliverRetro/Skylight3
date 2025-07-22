﻿using Skylight.API.Game.Furniture.Wall;
using Skylight.API.Game.Rooms.Items;
using Skylight.API.Game.Rooms.Items.Wall;
using Skylight.API.Game.Rooms.Private;
using Skylight.API.Game.Users;
using Skylight.API.Numerics;

namespace Skylight.Server.Game.Rooms.Items.Wall;

internal sealed class StaticWallRoomItem(IPrivateRoom room, RoomItemId id, IUserInfo owner, IStaticWallFurniture furniture, Point2D location, Point2D position, int direction)
	: WallRoomItem<IStaticWallFurniture>(room, id, owner, furniture, location, position, direction), IStaticWallRoomItem
{
	public new IStaticWallFurniture Furniture => this.furniture;
}
