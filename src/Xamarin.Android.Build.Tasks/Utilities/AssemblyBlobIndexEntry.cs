using System;
using System.Text;

using K4os.Hash.xxHash;

namespace Xamarin.Android.Tasks
{
	class AssemblyBlobIndexEntry
	{
		static uint globalAssemblyIndex = 0;

		public string Name { get; }
		public uint BlobID { get; }
		public uint Index { get; }

		// Hash values must have the same type as they are inside a union in the native code
		public ulong NameHash64 { get; }
		public ulong NameHash32 { get; }

		public uint DataOffset { get; set; }
		public uint DataSize { get; set; }

		public uint DebugDataOffset { get; set; }
		public uint DebugDataSize { get; set; }

		public uint ConfigDataOffset { get; set; }
		public uint ConfigDataSize { get; set; }

		public AssemblyBlobIndexEntry (string name, uint blobID)
		{
			if (String.IsNullOrEmpty (name)) {
				throw new ArgumentException ("must not be null or empty", nameof (name));
			}

			Name = name;
			BlobID = blobID;

			// NOTE: NOT thread safe, if we ever have parallel runs of BuildApk this operation must either be atomic or protected with a lock
			Index = globalAssemblyIndex++;

			byte[] nameBytes = Encoding.UTF8.GetBytes (name);
			NameHash32 = XXH32.DigestOf (nameBytes, 0, nameBytes.Length);
			NameHash64 = XXH64.DigestOf (nameBytes, 0, nameBytes.Length);
		}
	}
}
