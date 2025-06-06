﻿using System.Buffers;
using System.Buffers.Text;
using System.Text;
using Net.Buffers;
using Net.Buffers.Extensions;
using Net.Communication.Incoming.Consumer;
using Net.Communication.Manager;
using Net.Communication.Outgoing;
using Net.Sockets.Pipeline.Handler;
using Net.Sockets.Pipeline.Handler.Incoming;
using Net.Sockets.Pipeline.Handler.Outgoing;
using Skylight.Protocol.Extensions;
using Skylight.Protocol.Packets.Manager;
using Skylight.Protocol.Packets.Outgoing;
using Skylight.Server.Net.Crypto;

namespace Skylight.Server.Net.Handlers;

internal sealed class FusePacketHeaderHandler<TLength> : IncomingBytesHandler, IOutgoingObjectHandler
{
	private uint currentPacketLength;

	private Func<PacketManager<string>> packetManagerGetter;

	private RC4? incomingMessageDecoder;
	private string cryptoKey;

	internal FusePacketHeaderHandler(Func<IGamePacketManager> packetManagerGetter, string cryptoKey)
	{
		this.packetManagerGetter = () => (PacketManager<string>)packetManagerGetter();
		this.cryptoKey = cryptoKey;
	}

	protected override void Decode(IPipelineHandlerContext context, ref PacketReader reader)
	{
		if (this.currentPacketLength == 0)
		{
			if (typeof(TLength) == typeof(string))
			{
				Span<byte> lengthBytes = stackalloc byte[4];
				if (!reader.TryReadBytes(lengthBytes))
				{
					return;
				}

				if (Utf8Parser.TryParse(lengthBytes, out uint length, out _))
				{
					this.currentPacketLength = length;
				}
				else
				{
					context.Socket.Disconnect("Invalid length");
					return;
				}
			}
			else if (typeof(TLength) == typeof(int))
			{
				if (this.currentPacketLength == 0)
				{
					if (reader.Remaining < 5)
					{
						return;
					}

					reader.Skip(2);

					this.currentPacketLength = reader.ReadBase128UInt32(3);
				}
			}
			else
			{
				throw new NotSupportedException();
			}
		}

		if (reader.Remaining < this.currentPacketLength)
		{
			return;
		}

		PacketReader readerSliced = reader.Slice(this.currentPacketLength);

		RC4? messageDecoder = this.incomingMessageDecoder;
		if (messageDecoder is not null)
		{
			readerSliced = messageDecoder.Read(ref readerSliced);
		}

		Console.WriteLine(Encoding.ASCII.GetString(readerSliced.UnreadSequence));

		if (!readerSliced.GetReaderRef().TryReadTo(out ReadOnlySequence<byte> headerBytes, (byte)' '))
		{
			headerBytes = readerSliced.ReadBytes(readerSliced.Remaining);
		}

		string header = Encoding.ASCII.GetString(headerBytes);

		this.Read(context, header, ref readerSliced);

		messageDecoder?.AdvanceReader(readerSliced.UnreadSequence.End);

		this.currentPacketLength = 0;
	}

	internal void SetSecretKey()
	{
		this.incomingMessageDecoder = new RC4Hex(this.cryptoKey);
	}

	private void Read(IPipelineHandlerContext context, string header, ref PacketReader reader)
	{
		if (this.packetManagerGetter().TryGetConsumer(header, out IIncomingPacketConsumer? consumer))
		{
			consumer.Read(context, ref reader);

			if (reader.Readable)
			{
				Console.WriteLine($"Packet has stuff left: {header} ({reader.Remaining})");
			}
		}
		else
		{
			Console.WriteLine($"Unknown packet: {header}");
		}
	}

	public void Handle<T>(IPipelineHandlerContext context, ref PacketWriter writer, in T packet)
	{
		if (this.packetManagerGetter().TryGetComposer<T>(out IOutgoingPacketComposer? composer, out string header))
		{
			writer.WriteBytes(Encoding.ASCII.GetBytes(header));
			if (composer is IGameOutgoingPacketComposer<T> gameComposer)
			{
				gameComposer.AppendHeader(ref writer, packet);
			}

			writer.WriteByte((byte)'\r');

			composer.Compose(ref writer, packet);

			writer.WriteBytes("##"u8);
		}
		else
		{
			Console.WriteLine($"Missing composer: {typeof(T)}");
		}
	}
}
