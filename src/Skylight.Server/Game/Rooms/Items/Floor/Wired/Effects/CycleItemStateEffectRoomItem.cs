﻿using System.Text.Json;
using Skylight.API.Game.Furniture.Floor.Wired.Effects;
using Skylight.API.Game.Rooms.Items;
using Skylight.API.Game.Rooms.Items.Floor.Wired.Effects;
using Skylight.API.Game.Rooms.Items.Interactions.Wired.Effects;
using Skylight.API.Game.Rooms.Private;
using Skylight.API.Game.Rooms.Units;
using Skylight.API.Game.Users;
using Skylight.API.Numerics;
using Skylight.API.Registry;
using Skylight.Protocol.Packets.Data.UserDefinedRoomEvents;
using Skylight.Protocol.Packets.Outgoing.UserDefinedRoomEvents;

namespace Skylight.Server.Game.Rooms.Items.Floor.Wired.Effects;

internal sealed class CycleItemStateEffectRoomItem(IRegistryHolder registryHolder, IPrivateRoom room, RoomItemId id, IUserInfo owner, ICycleItemStateEffectFurniture furniture, Point3D position, int direction, IWiredEffectInteractionHandler interactionHandler,
	HashSet<IRoomItem>? selectedItems, JsonDocument? extraData, int effectDelay)
	: WiredEffectRoomItem<ICycleItemStateEffectFurniture>(room, id, owner, furniture, position, direction, effectDelay), ICycleItemStateRoomItem
{
	// TODO: Support other domains
	private readonly IRoomItemDomain normalRoomItemDomain = RoomItemDomains.Normal.Get(registryHolder);

	private readonly IWiredEffectInteractionHandler interactionHandler = interactionHandler;

	private LazyRoomItemSetHolder selectedItems = selectedItems is null
		? new LazyRoomItemSetHolder(WiredUtils.GetSelectedItems(extraData))
		: new LazyRoomItemSetHolder(selectedItems);

	public new ICycleItemStateEffectFurniture Furniture => this.furniture;

	public IReadOnlySet<IRoomItem> SelectedItems
	{
		get => this.selectedItems.Get(this.Room.ItemManager, this.normalRoomItemDomain);
		set => this.selectedItems.Set([.. value]);
	}

	public override void OnPlace() => this.interactionHandler.OnPlace(this);
	public override void OnRemove() => this.interactionHandler.OnRemove(this);

	public override void Open(IUserRoomUnit unit)
	{
		unit.User.SendAsync(new WiredFurniActionOutgoingPacket<RoomItemId>(this.Id, this.Furniture.Id, ActionType.CycleItemState, 100, this.SelectedItems.Select(i => i.Id).ToList(), 0, [], string.Empty));
	}

	public override void Trigger(IUserRoomUnit? cause = null)
	{
		foreach (IRoomItem selectedItem in this.SelectedItems)
		{
			if (selectedItem is IInteractableRoomItem interactable)
			{
				interactable.Interact(null!, 0);
			}
		}
	}

	public JsonDocument GetExtraData()
	{
		return JsonSerializer.SerializeToDocument(new
		{
			SelectedItems = this.SelectedItems.Select(i => i.StripId)
		});
	}
}
