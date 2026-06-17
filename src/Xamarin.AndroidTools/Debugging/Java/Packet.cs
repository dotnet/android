using System;
using System.Buffers.Binary;

namespace Xamarin.AndroidTools.Debugging.Java
{
	internal abstract class Packet
	{
		static int id = 100;

		public int Id { get; set; } = id++;

		public byte Flags { get; set; } = 0x00;

		public ReadOnlyMemory<byte> Data { get; set; } = new byte[0];

		public bool IsReply => (Flags & 0x80) == 0x80;

		public abstract ReadOnlyMemory<byte> ToMemory();
	}
}
