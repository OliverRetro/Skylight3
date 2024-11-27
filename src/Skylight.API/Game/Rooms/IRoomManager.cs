﻿using System.Diagnostics.CodeAnalysis;
using Skylight.API.Collections.Cache;
using Skylight.API.Game.Rooms.Private;
using Skylight.API.Game.Rooms.Public;

namespace Skylight.API.Game.Rooms;

public interface IRoomManager
{
	public IEnumerable<IRoom> LoadedRooms { get; }
	public IEnumerable<IPrivateRoom> LoadedPrivateRooms { get; }

	public ValueTask<ICacheReference<IPrivateRoom>?> GetPrivateRoomAsync(int roomId, CancellationToken cancellationToken = default);

	public ValueTask<ICacheReference<IPublicRoomInstance>?> GetPublicRoomAsync(int instanceId, CancellationToken cancellationToken = default);
	public ValueTask<ICacheReference<IPublicRoom>?> GetPublicRoomAsync(int instanceId, int worldId, CancellationToken cancellationToken = default);

	public bool TryGetPrivateRoom(int roomId, [NotNullWhen(true)] out IPrivateRoom? room);
}
