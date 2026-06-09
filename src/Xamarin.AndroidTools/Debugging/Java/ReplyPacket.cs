using System;
using System.Buffers.Binary;
using System.Text;

namespace Xamarin.AndroidTools.Debugging.Java
{
	internal class ReplyPacket : Packet
	{
		public ReplyPacket()
		{
			Flags &= 0x80;
		}

		public short ErrorCode { get; set; } = 0;

		public override ReadOnlyMemory<byte> ToMemory()
		{
			const uint headerLength = 11;

			var dataLength = Data.Length;

			Memory<byte> headerSpan = new byte[headerLength + dataLength];

			var l = headerLength + ((uint)Math.Max(0, dataLength));

			// Length
			BinaryPrimitives.WriteUInt32BigEndian(headerSpan.Slice(0).Span, l);

			// Id
			BinaryPrimitives.WriteInt32BigEndian(headerSpan.Slice(4).Span, Id);

			// Flags
			headerSpan.Span[8] = Flags;

			// Error code
			BinaryPrimitives.WriteInt16BigEndian(headerSpan.Slice(9, 2).Span, ErrorCode);

			// Data body if there is one
			if (dataLength > 0)
				Data.Span.CopyTo(headerSpan.Slice(11, dataLength).Span);

			return headerSpan;
		}

		public virtual void FromMemory(ReadOnlyMemory<byte> header, ReadOnlyMemory<byte> data)
		{
			Id = BinaryPrimitives.ReadInt32BigEndian(header.Slice(4, 4).Span);
			Flags = header.Span[8];
			ErrorCode = BinaryPrimitives.ReadInt16BigEndian(header.Slice(9, 2).Span);
			Data = data;
		}
	}
}