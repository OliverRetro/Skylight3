﻿using Skylight.API.Game.Furniture;
using Skylight.API.Game.Inventory.Items.Wall;

namespace Skylight.Server.Game.Inventory.Items.Wall;

internal abstract class WallInventoryItemBuilder<TFurniture, TTarget, TBuilder> : FurnitureInventoryItemBuilder<TFurniture, TTarget, TBuilder>, IWallInventoryItemBuilder<TFurniture, TTarget, TBuilder>
	where TFurniture : IFurniture
	where TTarget : IWallInventoryItem, IFurnitureItem<TFurniture>
	where TBuilder : WallInventoryItemBuilder<TFurniture, TTarget, TBuilder>;
