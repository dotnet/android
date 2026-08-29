using System;
using System.Buffers.Binary;
using System.Text;

namespace Xamarin.AndroidTools.Debugging.Java
{
	internal class DdmCommandPacket : CommandPacket
	{
		public DdmCommandPacket (string chunkType, ReadOnlyMemory<byte> chunkData)
		{
			if (chunkType == null)
				throw new ArgumentNullException (nameof (chunkType));
			if (chunkType.Length != 4)
				throw new ArgumentException ("DDM chunk types must contain exactly four ASCII characters.", nameof (chunkType));

			CommandSet = 0xc7;
			Command = 0x01;

			var data = new byte [8 + chunkData.Length];
			Encoding.ASCII.GetBytes (chunkType, 0, chunkType.Length, data, 0);
			BinaryPrimitives.WriteInt32BigEndian (data.AsSpan (4, 4), chunkData.Length);
			chunkData.CopyTo (data.AsMemory (8));
			Data = data;
		}
	}
}
