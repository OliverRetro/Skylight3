﻿using System.Globalization;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Net.Buffers;
using Net.Communication.Incoming.Consumer;
using Net.Communication.Manager;
using Net.Communication.Outgoing;
using Net.Sockets.Pipeline.Handler;
using Net.Sockets.Pipeline.Handler.Incoming;
using Net.Sockets.Pipeline.Handler.Outgoing;
using Skylight.Protocol.Extensions;
using Skylight.Protocol.Packets.Manager;
using Skylight.Protocol.Packets.Outgoing.Handshake;
using Skylight.Server.Net.Crypto;

namespace Skylight.Server.Net.Handlers;

internal sealed class Base64PacketHeaderHandler : IncomingBytesHandler, IOutgoingObjectHandler
{
	private readonly ILogger<Base64PacketHeaderHandler> logger;

	private readonly Func<PacketManager<uint>> packetManager;

	private uint currentPacketLength;

	private RC4? incomingHeaderDecoder;
	private RC4? incomingMessageDecoder;

	private Pipe? outgoingEncoderPipe;
	private RC4? outgoingHeaderEncoder;
	private RC4? outgoingMessageEncoder;

	private int incomingPaddingDecoder;
	private int outgoingPaddingEncoder;

	internal BigInteger CryptoPrime { get; }
	internal BigInteger CryptoGenerator { get; }
	internal string CryptoKey { get; }
	internal string CryptoPremix { get; }

	internal Base64PacketHeaderHandler(ILogger<Base64PacketHeaderHandler> logger, Func<IGamePacketManager> packetManager, BigInteger cryptoPrime, BigInteger cryptoGenerator, string cryptoKey, string cryptoPremix)
	{
		this.logger = logger;
		this.packetManager = () => (PacketManager<uint>)packetManager();

		this.CryptoPrime = cryptoPrime;
		this.CryptoGenerator = cryptoGenerator;
		this.CryptoKey = cryptoKey;
		this.CryptoPremix = cryptoPremix;
	}

	protected override void Decode(IPipelineHandlerContext context, ref PacketReader reader)
	{
		if (this.incomingHeaderDecoder is not null)
		{
			if (this.currentPacketLength == 0)
			{
				if (reader.Remaining < 6)
				{
					return;
				}

				PacketReader headerSliced = reader.Slice(6);
				PacketReader headerReader = this.incomingHeaderDecoder.Read(ref headerSliced);

				if (this.incomingHeaderDecoder != this.incomingMessageDecoder)
				{
					headerReader.Skip(1); //Random, nice one
				}

				headerReader.TryReadBase64UInt32(3, out this.currentPacketLength);

				if (this.incomingHeaderDecoder == this.incomingMessageDecoder)
				{
					this.currentPacketLength *= 2;
				}

				this.incomingHeaderDecoder.AdvanceReader(headerReader.UnreadSequence.End);
			}
		}
		else
		{
			//We haven't read the next packet length, wait for it
			if (this.currentPacketLength == 0 && !reader.TryReadBase64UInt32(3, out this.currentPacketLength))
			{
				return;
			}
		}

		if (reader.Remaining < this.currentPacketLength)
		{
			return;
		}

		PacketReader readerSliced = reader.Slice(this.currentPacketLength);

		this.Read(context, ref readerSliced);

		this.currentPacketLength = 0;
	}

	public void Read(IPipelineHandlerContext context, ref PacketReader reader)
	{
		RC4? messageDecoder = this.incomingMessageDecoder;
		if (messageDecoder is not null)
		{
			reader = messageDecoder.Read(ref reader);

			if (this.incomingHeaderDecoder != this.incomingMessageDecoder)
			{
				reader.Skip(this.IterateTokenRandom(ref this.incomingPaddingDecoder));
			}
		}

		uint header = reader.ReadBase64UInt32(2);

		if (this.packetManager().TryGetConsumer(header, out IIncomingPacketConsumer? consumer))
		{
			this.logger.LogDebug("Incoming: " + consumer.GetType().GetGenericArguments()[0]);

			consumer.Read(context, ref reader);

			if (reader.Readable)
			{
				this.logger.LogDebug($"Packet has stuff left: {header} ({reader.Remaining})");
			}
		}
		else
		{
			this.logger.LogDebug($"Unknown packet: {header}");
		}

		messageDecoder?.AdvanceReader(reader.UnreadSequence.End);
	}

	public void Handle<T>(IPipelineHandlerContext context, ref PacketWriter writer, in T packet)
	{
		if (this.packetManager().TryGetComposer<T>(out IOutgoingPacketComposer? composer, out uint header))
		{
			this.logger.LogDebug("Outgoing: " + typeof(T));

			if (this.outgoingEncoderPipe is not null && typeof(T) != typeof(CompleteDiffieHandshakeOutgoingPacket))
			{
				//Reserve space for the header
				PacketWriter headerSlice = writer.ReservedFixedSlice(6);

				int offset = writer.Length;

				{
					ReadOnlySpan<byte> padding = [0, 0, 0, 0, 0];

					PacketWriter packetWriter = new(this.outgoingEncoderPipe.Writer);
					packetWriter.WriteBytes(padding.Slice(0, this.IterateTokenRandom(ref this.outgoingPaddingEncoder)));
					WritePacket(ref packetWriter, composer, header, packet);
					packetWriter.Dispose();

					this.outgoingEncoderPipe.Reader.TryRead(out ReadResult readResult);

					foreach (ReadOnlyMemory<byte> readOnlyMemory in readResult.Buffer)
					{
						this.outgoingMessageEncoder!.Write(readOnlyMemory.Span, ref writer);
					}

					this.outgoingEncoderPipe.Reader.AdvanceTo(readResult.Buffer.End);
				}

				{
					PacketWriter packetWriter = new(this.outgoingEncoderPipe.Writer);
					packetWriter.WriteByte(0); //Ignored byte
					packetWriter.WriteBase64UInt32(3, (uint)(writer.Length - offset));
					packetWriter.Dispose();

					this.outgoingEncoderPipe.Reader.TryRead(out ReadResult readResult);

					foreach (ReadOnlyMemory<byte> readOnlyMemory in readResult.Buffer)
					{
						this.outgoingHeaderEncoder!.Write(readOnlyMemory.Span, ref headerSlice);
					}

					this.outgoingEncoderPipe.Reader.AdvanceTo(readResult.Buffer.End);
				}
			}
			else
			{
				WritePacket(ref writer, composer, header, packet);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			static void WritePacket(ref PacketWriter writer, IOutgoingPacketComposer composer, uint header, in T packet)
			{
				writer.WriteBase64UInt32(2, header);

				composer.Compose(ref writer, packet);

				writer.WriteByte(1);
			}
		}
		else
		{
			this.logger.LogDebug($"Missing composer: {typeof(T)}");
		}
	}

	internal void EnableEncryption(BigInteger sharedKey, bool incomingOnly)
	{
		byte[] rc4Table = sharedKey.ToByteArray(isUnsigned: true, isBigEndian: true);

		this.incomingHeaderDecoder = new RC4Base64(rc4Table, this.CryptoKey, this.CryptoPremix);
		this.incomingMessageDecoder = new RC4Base64(rc4Table, this.CryptoKey, this.CryptoPremix);

		if (!incomingOnly)
		{
			this.outgoingEncoderPipe = new Pipe();
			this.outgoingHeaderEncoder = new RC4Base64(rc4Table, this.CryptoKey, this.CryptoPremix);
			this.outgoingMessageEncoder = new RC4Base64(rc4Table, this.CryptoKey, this.CryptoPremix);
		}
	}

	internal void SetSecretKey()
	{
		this.incomingHeaderDecoder = this.incomingMessageDecoder = new RC4Hex(this.CryptoKey);
	}

	internal void SetToken(BigInteger integer)
	{
		Span<char> chars = stackalloc char[64];

		if (integer.TryFormat(chars, out int writtenChars, "X"))
		{
			this.incomingPaddingDecoder = int.Parse(chars.Slice(Math.Max(0, writtenChars - 4), writtenChars), NumberStyles.AllowHexSpecifier);
			this.outgoingPaddingEncoder = int.Parse(chars.Slice(0, Math.Min(4, writtenChars)), NumberStyles.AllowHexSpecifier);
		}
		else
		{
			throw new FormatException();
		}
	}

	private int IterateTokenRandom(ref int token)
	{
		token = ((19979 * token) + 5) % (ushort.MaxValue + 1);

		return token % 5;
	}

	internal bool CheckVersionBased => !this.packetManager().TryGetComposer<SessionParametersOutgoingPacket>(out _, out _);
}
