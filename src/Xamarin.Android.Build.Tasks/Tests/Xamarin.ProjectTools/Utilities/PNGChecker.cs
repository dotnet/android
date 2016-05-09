using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Xamarin.ProjectTools
{
	public class PNGChecker {

		public class Chunk
		{
			public string Type {get; set;}
			public byte[] Data { get; set; }
			public uint Crc { get; set; }
		}

		public List<Chunk> Chunks = new List<Chunk> ();

		public PNGChecker (List<Chunk> chunks) {
			Chunks = chunks;
		}

		public bool Is9Patch {
			get { return Chunks.Any (x => x.Type == "npTc"); }
		}

		static Chunk ReadChunk(BinaryReader reader) {

			if (reader.PeekChar () == -1)
				return null;

			uint length = reader.ReadInt32().ToNetworkOrder();
			string type = Encoding.UTF8.GetString(reader.ReadBytes(4));
			byte[] data = reader.ReadBytes((int)length);
			uint crc = reader.ReadInt32().ToNetworkOrder();

			return new Chunk { Type = type, Data = data, Crc = crc };
		}

		static byte[] PNGHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

		public static PNGChecker LoadFromStream(Stream stream)
		{
			using (BinaryReader reader = new BinaryReader (stream, Encoding.UTF8, true)) {
				reader.ReadBytes (PNGHeader.Length);
				var chunks = new List<Chunk>();
				Chunk chunk;
				while ((chunk = ReadChunk(reader)) != null)
				{
					chunks.Add(chunk);
				}
				return new PNGChecker (chunks);
			}
		}

		public static PNGChecker LoadFromBytes (byte[] data)
		{
			using (var ms = new MemoryStream (data)) {
				ms.Position = 0;
				return LoadFromStream (ms);
			}
		}

		public static PNGChecker Load(string filename)
		{
			using (var fs = new FileStream (filename, FileMode.Open)) {
				return LoadFromStream (fs);
			}
		}
	}

	public static class IntExtensions {
		public static uint ToNetworkOrder (this int value)
		{
			return (uint)System.Net.IPAddress.NetworkToHostOrder (value);
		}
	}
}

