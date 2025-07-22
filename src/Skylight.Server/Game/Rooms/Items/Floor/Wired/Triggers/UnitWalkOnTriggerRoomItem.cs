﻿using System.Text.Json;
using Skylight.API.Game.Furniture.Floor.Wired.Triggers;
using Skylight.API.Game.Rooms.Items;
using Skylight.API.Game.Rooms.Items.Floor.Wired.Triggers;
using Skylight.API.Game.Rooms.Items.Interactions.Wired.Triggers;
using Skylight.API.Game.Rooms.Private;
using Skylight.API.Game.Rooms.Units;
using Skylight.API.Game.Users;
using Skylight.API.Numerics;
using Skylight.API.Registry;
using Skylight.Protocol.Packets.Data.UserDefinedRoomEvents;
using Skylight.Protocol.Packets.Outgoing.UserDefinedRoomEvents;

namespace Skylight.Server.Game.Rooms.Items.Floor.Wired.Triggers;

internal sealed class UnitWalkOnTriggerRoomItem(IPrivateRoom room, IRegistryHolder registryHolder, RoomItemId id, IUserInfo owner, IUnitWalkOnTriggerFurniture furniture, Point3D position, int direction, IUnitWalkOnTriggerInteractionHandler interactionHandler,
	HashSet<IRoomItem>? selectedItems, JsonDocument? extraData)
	: WiredTriggerRoomItem<IUnitWalkOnTriggerFurniture>(room, id, owner, furniture, position, direction), IUnitWalkOnTriggerRoomItem
{
	// TODO: Support other domains
	private readonly IRoomItemDomain normalRoomItemDomain = RoomItemDomains.Normal.Get(registryHolder);

	private readonly IUnitWalkOnTriggerInteractionHandler interactionHandler = interactionHandler;

	private LazyRoomItemSetHolder selectedItems = selectedItems is null
		? new LazyRoomItemSetHolder(WiredUtils.GetSelectedItems(extraData))
		: new LazyRoomItemSetHolder(selectedItems);

	public new IUnitWalkOnTriggerFurniture Furniture => this.furniture;

	public IReadOnlySet<IRoomItem> SelectedItems
	{
		get => this.selectedItems.Get(this.Room.ItemManager, this.normalRoomItemDomain);
		set => this.selectedItems.Set([.. value]);
	}

	public override void OnPlace()
	{
		this.interactionHandler.OnPlace(this);
	}

	public override void OnRemove()
	{
		this.interactionHandler.OnRemove(this);
	}

	public override void Open(IUserRoomUnit unit)
	{
		unit.User.SendAsync(new WiredFurniTriggerOutgoingPacket<RoomItemId>(this.Id, this.Furniture.Id, TriggerType.UnitUseItem, 100, this.SelectedItems.Select(i => i.Id).ToList(), [], string.Empty));
	}

	public JsonDocument GetExtraData()
	{
		return JsonSerializer.SerializeToDocument(new
		{
			SelectedItems = this.SelectedItems.Select(i => i.StripId)
		});
	}
}
