using System;
using System.IO.Hashing;
using System.Text;

namespace Xamarin.Android.Tasks
{
	class AssemblyStoreIndexEntry
	{
		public string Name { get; }
		public uint StoreID { get; }
		public uint MappingIndex { get; }
		public uint LocalBlobIndex { get; }

		// Hash values must have the same type as they are inside a union in the native code
		public ulong NameHash64 { get; }
		public ulong NameHash32 { get; }

		public uint DataOffset { get; set; }
		public uint DataSize { get; set; }

		public uint DebugDataOffset { get; set; }
		public uint DebugDataSize { get; set; }

		public uint ConfigDataOffset { get; set; }
		public uint ConfigDataSize { get; set; }

		public AssemblyStoreIndexEntry (string name, uint blobID, uint mappingIndex, uint localBlobIndex)
		{
			if (String.IsNullOrEmpty (name)) {
				throw new ArgumentException ("must not be null or empty", nameof (name));
			}

			Name = name;
			StoreID = blobID;
			MappingIndex = mappingIndex;
			LocalBlobIndex = localBlobIndex;

			byte[] nameBytes = Encoding.UTF8.GetBytes (name);
			NameHash32 = XxHash32.HashToUInt32 (nameBytes);
			NameHash64 = XxHash3.HashToUInt64 (nameBytes);
		}
	}
}
