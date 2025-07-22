﻿using System.Drawing;
using System.Text.Json;
using Skylight.API.Game.Furniture.Wall;
using Skylight.API.Game.Rooms.Items;
using Skylight.API.Game.Rooms.Items.Interactions;
using Skylight.API.Game.Rooms.Items.Wall;
using Skylight.API.Game.Rooms.Private;
using Skylight.API.Game.Users;
using Skylight.API.Numerics;

namespace Skylight.Server.Game.Rooms.Items.Wall;

internal sealed class PostItRoomItem(IPrivateRoom room, RoomItemId id, IUserInfo owner, IStickyNoteFurniture furniture, Point2D location, Point2D position, int direction, Color color, string text, IStickyNoteInteractionHandler handler)
	: WallRoomItem<IStickyNoteFurniture>(room, id, owner, furniture, location, position, direction), IStickyNoteRoomItem
{
	public Color Color { get; set; } = color;
	public string Text { get; set; } = text;

	private readonly IStickyNoteInteractionHandler handler = handler;

	public new IStickyNoteFurniture Furniture => this.furniture;

	public override void OnPlace()
	{
		this.handler.OnPlace(this);
	}

	public override void OnRemove()
	{
		this.handler.OnRemove(this);
	}

	public JsonDocument GetExtraData() => JsonSerializer.SerializeToDocument(new { Color = this.Color.ToArgb(), this.Text });
}
