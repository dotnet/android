#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using ELFSharp;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

using Microsoft.Android.Tasks;

namespace Xamarin.Android.Tasks.JniRemapping
{
	static class NativeAotJniRetention
	{
		public static HashSet<string> GetRequiredEntries (string objectFile, R8Mapping mapping)
		{
			var objectData = ReadElfData (() => {
				var sections = ReadObjectData (objectFile, out var payloads);
				return (Sections: sections, ClassPayloads: payloads);
			});
			var classes = new List<R8ClassMapping> (mapping.EnumerateClassMappings ());
			var classPatterns = new LiteralMatcher (requireClassBoundaries: true);
			foreach (var type in classes) {
				classPatterns.Add (type.OriginalJniName);
				classPatterns.Add (type.OriginalJniName.Replace ('/', '.'));
			}
			HashSet<string> retainedClasses = classPatterns.Match (objectData.ClassPayloads);

			var memberPatterns = new LiteralMatcher ();
			var candidateClasses = new List<R8ClassMapping> ();
			foreach (var type in classes) {
				if (!retainedClasses.Contains (type.OriginalJniName) &&
						!retainedClasses.Contains (type.OriginalJniName.Replace ('/', '.'))) {
					continue;
				}
				candidateClasses.Add (type);
				foreach (var method in type.Methods) {
					memberPatterns.Add (method.OriginalName);
					memberPatterns.Add (JniDescriptorText.JavaSourceTypesToMethodDescriptor (method.JavaParameterTypes, method.JavaReturnType));
				}
				foreach (var field in type.Fields) {
					memberPatterns.Add (field.OriginalName);
					memberPatterns.Add (JniDescriptorText.JavaSourceTypeToJniTypeToken (field.JavaFieldType));
				}
			}
			HashSet<string> retainedMembers = memberPatterns.Match (objectData.Sections);
			var required = new HashSet<string> (StringComparer.Ordinal);
			foreach (var type in candidateClasses) {
				required.Add (R8Mapping.BuildClassEntry (type.OriginalJniName));
				foreach (var method in type.Methods) {
					string descriptor = JniDescriptorText.JavaSourceTypesToMethodDescriptor (method.JavaParameterTypes, method.JavaReturnType);
					bool constructor = method.OriginalName == "<init>" || method.OriginalName == "<clinit>";
					if (retainedMembers.Contains (descriptor) && (constructor || retainedMembers.Contains (method.OriginalName))) {
						required.Add (R8Mapping.BuildMethodEntry (type.OriginalJniName,
							R8Mapping.BuildMethodKey (method.OriginalName, method.JavaParameterTypes, method.JavaReturnType)));
					}
				}
				foreach (var field in type.Fields) {
					string descriptor = JniDescriptorText.JavaSourceTypeToJniTypeToken (field.JavaFieldType);
					if (retainedMembers.Contains (field.OriginalName) && retainedMembers.Contains (descriptor)) {
						required.Add (R8Mapping.BuildFieldEntry (type.OriginalJniName, field.OriginalName));
					}
				}
			}
			return required;
		}

		readonly struct LiteralRegion
		{
			public ulong Offset { get; }
			public int Length { get; }
			public byte []? Bytes { get; }

			public LiteralRegion (ulong offset, int length, byte []? bytes = null)
			{
				Offset = offset;
				Length = length;
				Bytes = bytes;
			}
		}

		readonly struct LiteralPayload
		{
			public byte [] Bytes { get; }
			public bool Utf16 { get; }

			public LiteralPayload (byte [] bytes, bool utf16 = false)
			{
				Bytes = bytes;
				Utf16 = utf16;
			}
		}

		static List<byte []> ReadObjectData (string path, out List<LiteralPayload> classPayloads)
		{
			using var stream = File.OpenRead (path);
			using IELF elf = ReadElfData (() => ELFReader.Load (stream, shouldOwnStream: false));
			ulong fileSize = (ulong) stream.Length;
			if (elf.Type != FileType.Relocatable || elf.Endianess != Endianess.LittleEndian ||
					(elf.Class != Class.Bit64 && elf.Class != Class.Bit32)) {
				throw new InvalidDataException (Properties.Resources.XA4325_NativeAotObjectFormat);
			}
			var data = new List<byte []> ();
			classPayloads = new List<LiteralPayload> ();
			var sectionData = new Dictionary<ISection, byte []> ();
			bool hasManagedCode = false;
			bool hasData = false;
			foreach (ISection section in elf.Sections) {
				ulong offset;
				ulong size;
				if (section is Section<ulong> section64) {
					offset = section64.Offset;
					size = section64.Size;
				} else if (section is Section<uint> section32) {
					offset = section32.Offset;
					size = section32.Size;
				} else {
					throw new InvalidDataException (Properties.Resources.XA4325_NativeAotObjectFormat);
				}
				if (section.Type != SectionType.NoBits && (offset > fileSize || size > fileSize - offset)) {
					throw new InvalidDataException (Properties.Resources.XA4325_NativeAotInvalidSection);
				}
				if ((section.Flags & SectionFlags.Allocatable) == 0 || section.Type == SectionType.NoBits) {
					continue;
				}
				byte [] contents = ReadElfData (() => section.GetContents ());
				if ((ulong) contents.Length != size) {
					throw new InvalidDataException (Properties.Resources.XA4325_NativeAotTruncatedSection);
				}
				if (contents.Length == 0) {
					continue;
				}
				hasManagedCode |= section.Name == "__managedcode";
				hasData |= (section.Flags & SectionFlags.Executable) == 0;
				data.Add (contents);
				sectionData.Add (section, contents);
				if ((section.Flags & SectionFlags.Executable) == 0 &&
						((ulong) section.Flags & 0x30) == 0x30 && SectionEntrySize (section) == 1) {
					if (contents [contents.Length - 1] != 0) {
						throw InvalidDehydration ();
					}
					foreach (string text in new UTF8Encoding (false, true).GetString (contents).Split ('\0')) {
						classPayloads.Add (new LiteralPayload (Encoding.UTF8.GetBytes (text)));
					}
				}
			}
			if (!hasManagedCode || !hasData) {
				throw new InvalidDataException (Properties.Resources.XA4325_NativeAotMissingSections);
			}
			var sections = elf.Sections.ToArray ();
			var symbols = sections.OfType<ISymbolTable> ().SelectMany (table => table.Entries)
				.Where (symbol => !symbol.IsPointedIndexSpecial).ToArray ();
			var relocations = ReadRelocations (stream, sections, symbols, elf.Class == Class.Bit64);
			var hydrated = new Dictionary<ISection, List<LiteralRegion>> ();
			var decoded = new HashSet<(ISection Section, ulong Offset)> ();
			foreach (ISymbolEntry symbol in symbols) {
				if (!symbol.Name.EndsWith ("__dehydrated_data", StringComparison.Ordinal)) {
					continue;
				}
				ulong offset = SymbolValue (symbol);
				ISection target = symbol.PointedSection;
				if (!sectionData.TryGetValue (target, out byte []? contents) || offset > (ulong) contents.Length) {
					throw new InvalidDataException (Properties.Resources.XA4325_NativeAotInvalidSection);
				}
				if (offset == (ulong) contents.Length || !decoded.Add ((target, offset))) {
					continue;
				}
				if (!relocations.TryGetValue ((target, offset), out var destination) || destination.Symbol.IsPointedIndexSpecial) {
					throw InvalidDehydration ();
				}
				ISection hydratedSection = destination.Symbol.PointedSection;
				ulong hydratedStart = checked ((ulong) ((long) SymbolValue (destination.Symbol) + destination.Addend));
				if (!hydrated.TryGetValue (hydratedSection, out var regions)) {
					hydrated [hydratedSection] = regions = new List<LiteralRegion> ();
				}
				ReadDehydratedLiterals (contents, checked ((int) offset), SymbolSize (symbol),
					elf.Class == Class.Bit64 ? 8 : 4, hydratedStart, regions, data);
			}
			foreach (var regions in hydrated.Values) {
				regions.Sort ((left, right) => left.Offset.CompareTo (right.Offset));
				for (int i = 1; i < regions.Count; i++) {
					if (regions [i].Offset < regions [i - 1].Offset + (ulong) regions [i - 1].Length) {
						throw InvalidDehydration ();
					}
				}
			}
			const string frozenSegmentSuffix = "__FrozenSegmentStart";
			var frozenSegments = symbols.Where (symbol => symbol.Name.EndsWith (frozenSegmentSuffix, StringComparison.Ordinal)).ToArray ();
			foreach (var segment in frozenSegments) {
				if (SymbolValue (segment) > SectionSize (segment.PointedSection) ||
						SymbolSize (segment) > SectionSize (segment.PointedSection) - SymbolValue (segment)) {
					throw InvalidDehydration ();
				}
			}
			var frozenStringsBySection = frozenSegments.Select (segment => (
					Prefix: segment.Name.Substring (0, segment.Name.Length - frozenSegmentSuffix.Length) + "__Str_", Segment: segment))
				.ToLookup (entry => entry.Segment.PointedSection);
			foreach (var symbol in symbols) {
				ISection section = symbol.PointedSection;
				if ((section.Flags & SectionFlags.Executable) != 0) {
					continue;
				}
				// FrozenStringNode uses the same compilation-unit prefix as its containing
				// ArrayOfFrozenObjectsNode. A managed type name can contain "__Str_" too.
				var frozenSegment = frozenStringsBySection [section].FirstOrDefault (entry =>
					symbol.Name.StartsWith (entry.Prefix, StringComparison.Ordinal) &&
					SymbolValue (symbol) >= SymbolValue (entry.Segment) &&
					SymbolValue (symbol) - SymbolValue (entry.Segment) >= (elf.Class == Class.Bit64 ? 8UL : 4UL) &&
					SymbolValue (symbol) - SymbolValue (entry.Segment) < SymbolSize (entry.Segment)).Segment;
				if (frozenSegment != null) {
					// FrozenStringNode symbols point at the MethodTable, followed by Int32
					// length and exactly that many UTF-16 code units (then a terminator).
					ulong pointerSize = elf.Class == Class.Bit64 ? 8UL : 4UL;
					ulong remaining = SymbolSize (frozenSegment) - (SymbolValue (symbol) - SymbolValue (frozenSegment));
					if (remaining < pointerSize + 4) {
						throw InvalidDehydration ();
					}
					ulong offset = checked (SymbolValue (symbol) + pointerSize);
					byte [] header = ReadLiteral (section, offset, 4);
					uint length = ReadUInt32 (header, 0);
					if (length > int.MaxValue / 2 - 1 || ((ulong) length + 1) * 2 > remaining - pointerSize - 4) {
						throw InvalidDehydration ();
					}
					byte [] payload = ReadLiteral (section, offset + 4, checked (((int) length + 1) * 2));
					if (payload [payload.Length - 1] != 0 || payload [payload.Length - 2] != 0) {
						throw InvalidDehydration ();
					}
					Array.Resize (ref payload, payload.Length - 2);
					classPayloads.Add (new LiteralPayload (payload, utf16: true));
				} else if (symbol.Name.EndsWith ("__external_type_map__", StringComparison.Ordinal)) {
					byte [] blob = ReadLiteral (section, SymbolValue (symbol), checked ((int) SymbolSize (symbol)));
					var reader = new NativeAotTypeMapReader (blob);
					string prefix = symbol.Name.Substring (0, symbol.Name.Length - "__external_type_map__".Length);
					var fixups = symbols.SingleOrDefault (candidate => candidate.Name == prefix + "__external_CommonFixupsTable_references");
					if (fixups == null) {
						throw InvalidDehydration ();
					}
					var groups = new HashSet<uint> ();
					foreach (uint group in reader.ReadGroupTypeIndices ()) {
						if ((ulong) group * 4 + 4 > SymbolSize (fixups) ||
								!relocations.TryGetValue ((fixups.PointedSection, SymbolValue (fixups) + (ulong) group * 4), out var type) ||
								type.Addend != 0) {
							throw InvalidDehydration ();
						}
						if (TypeMapKey.IsJavaGroupSymbol (type.Symbol.Name)) {
							groups.Add (group);
						}
					}
					var keys = new HashSet<string> (StringComparer.Ordinal);
					reader.ReadKeys (keys, groups);
					foreach (string key in keys) {
						classPayloads.Add (new LiteralPayload (Encoding.UTF8.GetBytes (key)));
					}
				}
			}
			return data;

			byte [] ReadLiteral (ISection section, ulong offset, int count)
			{
				if (count < 0 || offset > SectionSize (section) || (ulong) count > SectionSize (section) - offset) {
					throw InvalidDehydration ();
				}
				if (!hydrated.TryGetValue (section, out var regions)) {
					if (sectionData.TryGetValue (section, out var bytes)) {
						var payload = new byte [count];
						Buffer.BlockCopy (bytes, checked ((int) offset), payload, 0, count);
						return payload;
					}
					throw InvalidDehydration ();
				}
				var result = new byte [count];
				ulong end = offset + (ulong) count;
				ulong position = offset;
				int low = 0;
				int high = regions.Count;
				while (low < high) {
					int middle = low + (high - low) / 2;
					if (regions [middle].Offset + (ulong) regions [middle].Length <= offset) {
						low = middle + 1;
					} else {
						high = middle;
					}
				}
				for (int i = low; i < regions.Count && position < end; i++) {
					var region = regions [i];
					ulong limit = region.Offset + (ulong) region.Length;
					if (limit <= position || region.Offset >= end) {
						continue;
					}
					if (region.Offset > position) {
						throw InvalidDehydration ();
					}
					int length = checked ((int) (Math.Min (end, limit) - position));
					if (region.Bytes != null) {
						Buffer.BlockCopy (region.Bytes, checked ((int) (position - region.Offset)), result, checked ((int) (position - offset)), length);
					}
					position += (ulong) length;
				}
				if (position != end) {
					throw InvalidDehydration ();
				}
				return result;
			}
		}

		// ILC's __dehydrated_data header contains a relative destination pointer and the
		// command-stream length. Evaluate literals without mistaking commands for UTF-16.
		// Format: dotnet/runtime src/coreclr/tools/Common/Internal/Runtime/DehydratedData.cs.
		static void ReadDehydratedLiterals (byte [] contents, int start, ulong symbolSize,
			int pointerSize, ulong destination, List<LiteralRegion> regions, List<byte []> data)
		{
			if (contents.Length - start < 8) {
				throw InvalidDehydration ();
			}
			uint length = ReadUInt32 (contents, start + 4);
			if (length < 8 || length > contents.Length - start) {
				throw InvalidDehydration ();
			}
			if (symbolSize != 0 && (symbolSize < length || symbolSize > (ulong) (contents.Length - start))) {
				throw InvalidDehydration ();
			}
			int end = start + (int) length;
			int position = start + 8;
			using var literal = new MemoryStream ();
			while (position < end) {
				byte instruction = contents [position++];
				int command = instruction & 7;
				int payload = instruction >> 3;
				int extra = payload - 28;
				if (extra > 0) {
					RequireBytes (extra);
					payload = 28;
					for (int i = 0; i < extra; i++) {
						payload += contents [position++] << (i * 8);
					}
				}
				switch (command) {
				case 0: // Copy
					if (payload == 0) {
						throw InvalidDehydration ();
					}
					RequireBytes (payload);
					var bytes = new byte [payload];
					Buffer.BlockCopy (contents, position, bytes, 0, payload);
					regions.Add (new LiteralRegion (destination, payload, bytes));
					destination = checked (destination + (ulong) payload);
					literal.Write (contents, position, payload);
					position += payload;
					break;
				case 1: // ZeroFill
					if (payload == 0) {
						throw InvalidDehydration ();
					}
					regions.Add (new LiteralRegion (destination, payload));
					destination = checked (destination + (ulong) payload);
					for (int i = 0; i < Math.Min (payload, 2); i++) {
						literal.WriteByte (0);
					}
					// A UTF-16 identifier can contain two adjacent zero bytes, but not three.
					// Preserve genuine edge zeros without expanding arbitrarily long padding.
					if (payload > 2) {
						FlushLiteral ();
						literal.WriteByte (0);
						literal.WriteByte (0);
					}
					break;
				case 2: // RelPtr32Reloc
				case 3: // PtrReloc
					if ((long) end + (long) payload * 4 + 4 > contents.Length) {
						throw InvalidDehydration ();
					}
					FlushLiteral ();
					destination = checked (destination + (command == 2 ? 4UL : (ulong) pointerSize));
					break;
				case 4: // InlineRelPtr32Reloc
				case 5: // InlinePtrReloc
					if (payload == 0) {
						throw InvalidDehydration ();
					}
					int relocationBytes = checked (payload * 4);
					RequireBytes (relocationBytes);
					position += relocationBytes;
					FlushLiteral ();
					destination = checked (destination + (ulong) payload * (command == 4 ? 4UL : (ulong) pointerSize));
					break;
				default:
					throw InvalidDehydration ();
				}
			}
			FlushLiteral ();
			// Neither command bytes nor relocation-table bytes are literal retention evidence.
			Array.Clear (contents, start, symbolSize == 0 ? (int) length : checked ((int) symbolSize));

			void RequireBytes (int count)
			{
				if (count < 0 || count > end - position) {
					throw InvalidDehydration ();
				}
			}

			void FlushLiteral ()
			{
				if (literal.Length > 0) {
					data.Add (literal.ToArray ());
				}
				literal.SetLength (0);
			}
		}

		static uint ReadUInt32 (byte [] bytes, int position)
			=> (uint) (bytes [position] | bytes [position + 1] << 8 |
				bytes [position + 2] << 16 | bytes [position + 3] << 24);

		static ulong SectionSize (ISection section) => section is Section<ulong> s64 ? s64.Size : ((Section<uint>) section).Size;
		static ulong SectionEntrySize (ISection section) => section is Section<ulong> s64 ? s64.EntrySize : ((Section<uint>) section).EntrySize;
		static ulong SymbolValue (ISymbolEntry symbol) => symbol is SymbolEntry<ulong> s64 ? s64.Value : ((SymbolEntry<uint>) symbol).Value;
		static ulong SymbolSize (ISymbolEntry symbol) => symbol is SymbolEntry<ulong> s64 ? s64.Size : ((SymbolEntry<uint>) symbol).Size;

		static Dictionary<(ISection, ulong), (ISymbolEntry Symbol, long Addend)> ReadRelocations (
			Stream stream, ISection [] sections, ISymbolEntry [] sourceSymbols, bool elf64)
		{
			var ranges = sourceSymbols.Where (symbol =>
					symbol.Name.EndsWith ("__dehydrated_data", StringComparison.Ordinal) ||
					symbol.Name.EndsWith ("__external_CommonFixupsTable_references", StringComparison.Ordinal))
				.GroupBy (symbol => symbol.PointedSection).ToDictionary (group => group.Key, group => group.ToArray ());
			using var reader = new BinaryReader (stream, Encoding.UTF8, leaveOpen: true);
			stream.Position = 18;
			uint relocationType = reader.ReadUInt16 () switch {
				183 => 261U, // R_AARCH64_PREL32
				40 => 3U, // R_ARM_REL32
				62 => 2U, // R_X86_64_PC32
				3 => 2U, // R_386_PC32
				_ => throw InvalidDehydration (),
			};
			stream.Position = elf64 ? 40 : 32;
			ulong headers = elf64 ? reader.ReadUInt64 () : reader.ReadUInt32 ();
			stream.Position = elf64 ? 58 : 46;
			int entrySize = reader.ReadUInt16 ();
			var result = new Dictionary<(ISection, ulong), (ISymbolEntry Symbol, long Addend)> ();
			for (int i = 0; i < sections.Length; i++) {
				var section = sections [i];
				bool addend = section.Type == SectionType.RelocationAddends;
				if (!addend && section.Type != SectionType.Relocation) {
					continue;
				}
				stream.Position = checked ((long) headers + i * entrySize + (elf64 ? 40 : 24));
				uint link = reader.ReadUInt32 ();
				uint info = reader.ReadUInt32 ();
				if (link >= sections.Length || info >= sections.Length || sections [link] is not ISymbolTable table) {
					throw InvalidDehydration ();
				}
				if (!ranges.TryGetValue (sections [info], out var needed)) {
					continue;
				}
				var symbols = table.Entries.ToArray ();
				byte [] contents = section.GetContents ();
				using var entries = new BinaryReader (new MemoryStream (contents));
				int size = elf64 ? (addend ? 24 : 16) : (addend ? 12 : 8);
				if (contents.Length % size != 0) {
					throw InvalidDehydration ();
				}
				while (entries.BaseStream.Position < contents.Length) {
					ulong offset = elf64 ? entries.ReadUInt64 () : entries.ReadUInt32 ();
					ulong relocation = elf64 ? entries.ReadUInt64 () : entries.ReadUInt32 ();
					long adjustment = addend ? (elf64 ? entries.ReadInt64 () : entries.ReadInt32 ()) : 0;
					if (!needed.Any (symbol => offset >= SymbolValue (symbol) &&
							offset - SymbolValue (symbol) < (symbol.Name.EndsWith ("__dehydrated_data", StringComparison.Ordinal) ? 4UL : SymbolSize (symbol)))) {
						continue;
					}
					ulong index = elf64 ? relocation >> 32 : relocation >> 8;
					uint type = elf64 ? (uint) relocation : (uint) relocation & 0xFF;
					if (type != relocationType || index >= (ulong) symbols.Length || offset > SectionSize (sections [info]) ||
							SectionSize (sections [info]) - offset < 4) {
						throw InvalidDehydration ();
					}
					if (!addend) {
						ulong fileOffset = sections [info] is Section<ulong> s64 ? s64.Offset : ((Section<uint>) sections [info]).Offset;
						stream.Position = checked ((long) (fileOffset + offset));
						adjustment = reader.ReadInt32 ();
					}
					result.Add ((sections [info], offset), (symbols [index], adjustment));
				}
			}
			return result;
		}

		static InvalidDataException InvalidDehydration ()
			=> new InvalidDataException (Properties.Resources.XA4325_NativeAotInvalidDehydration);

		static T ReadElfData<T> (Func<T> read)
		{
			try {
				return read ();
			} catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException ||
					ex is IndexOutOfRangeException || ex is OverflowException || ex is BadImageFormatException) {
				throw new InvalidDataException (ex.Message, ex);
			}
		}

		sealed class LiteralMatcher
		{
			readonly struct Pattern
			{
				public string Text { get; }
				public int Length { get; }
				public bool Utf16 { get; }
				public bool Descriptor { get; }

				public Pattern (string text, int length, bool utf16, bool descriptor)
				{
					Text = text;
					Length = length;
					Utf16 = utf16;
					Descriptor = descriptor;
				}
			}

			struct Node
			{
				public byte Value;
				public int Child;
				public int Sibling;
				public int Failure;
				public int Output;
				public List<Pattern>? Patterns;
			}

			Node [] nodes = new Node [256];
			int count = 1;
			readonly int [] root = new int [256];
			readonly HashSet<string> patterns = new HashSet<string> (StringComparer.Ordinal);
			readonly bool requireClassBoundaries;

			public LiteralMatcher (bool requireClassBoundaries = false)
			{
				this.requireClassBoundaries = requireClassBoundaries;
			}

			public void Add (string pattern)
			{
				if (pattern.Length == 0 || !patterns.Add (pattern)) {
					return;
				}
				AddEncoded (pattern, pattern, descriptor: false);
				if (requireClassBoundaries) {
					AddEncoded ("L" + pattern + ";", pattern, descriptor: true);
				}
			}

			void AddEncoded (string encoded, string pattern, bool descriptor)
			{
				byte [] utf8 = Encoding.UTF8.GetBytes (encoded);
				Add (utf8, new Pattern (pattern, utf8.Length, utf16: false, descriptor));
				byte [] utf16 = Encoding.Unicode.GetBytes (encoded);
				Add (utf16, new Pattern (pattern, utf16.Length, utf16: true, descriptor));
			}

			void Add (byte [] bytes, Pattern pattern)
			{
				int current = 0;
				foreach (byte value in bytes) {
					int next = Find (current, value);
					if (next == 0) {
						if (count == nodes.Length) {
							Array.Resize (ref nodes, checked (nodes.Length * 2));
						}
						next = count++;
						nodes [next].Value = value;
						nodes [next].Sibling = nodes [current].Child;
						nodes [current].Child = next;
						if (current == 0) {
							root [value] = next;
						}
					}
					current = next;
				}
				var terminalPatterns = nodes [current].Patterns;
				if (terminalPatterns == null) {
					nodes [current].Patterns = terminalPatterns = new List<Pattern> ();
				}
				terminalPatterns.Add (pattern);
			}

			int Find (int node, byte value)
			{
				if (node == 0) {
					return root [value];
				}
				for (int child = nodes [node].Child; child != 0; child = nodes [child].Sibling) {
					if (nodes [child].Value == value) {
						return child;
					}
				}
				return 0;
			}

			public HashSet<string> Match (List<byte []> sections)
				=> Match (sections.Select (section => new LiteralPayload (section)));

			public HashSet<string> Match (IEnumerable<LiteralPayload> sections)
			{
				var queue = new Queue<int> ();
				for (int child = nodes [0].Child; child != 0; child = nodes [child].Sibling) {
					queue.Enqueue (child);
				}
				while (queue.Count > 0) {
					int parent = queue.Dequeue ();
					for (int child = nodes [parent].Child; child != 0; child = nodes [child].Sibling) {
						int failure = nodes [parent].Failure;
						int next;
						while ((next = Find (failure, nodes [child].Value)) == 0 && failure != 0) {
							failure = nodes [failure].Failure;
						}
						nodes [child].Failure = next;
						nodes [child].Output = nodes [next].Patterns != null ? next : nodes [next].Output;
						queue.Enqueue (child);
					}
				}

				var found = new HashSet<string> (StringComparer.Ordinal);
				foreach (var payload in sections) {
					byte [] section = payload.Bytes;
					int current = 0;
					for (int position = 0; position < section.Length; position++) {
						byte value = section [position];
						int next;
						while ((next = Find (current, value)) == 0 && current != 0) {
							current = nodes [current].Failure;
						}
						current = next;
						for (int output = current; output != 0; output = nodes [output].Output) {
							var terminalPatterns = nodes [output].Patterns;
							if (terminalPatterns != null) {
								foreach (Pattern pattern in terminalPatterns) {
									if (!requireClassBoundaries || (pattern.Utf16 == payload.Utf16 &&
											(!pattern.Utf16 || (position - pattern.Length + 1) % 2 == 0) &&
											HasClassBoundaries (section, position, pattern))) {
										found.Add (pattern.Text);
									}
								}
							}
						}
					}
				}
				return found;
			}

			static bool HasClassBoundaries (byte [] section, int end, Pattern pattern)
			{
				int start = end - pattern.Length + 1;
				if (pattern.Descriptor) {
					return HasDescriptorPrefix (section, start + (pattern.Utf16 ? 2 : 1), pattern.Utf16);
				}
				// Bare names must occupy the whole literal; tokens within descriptors
				// (for example I in run.(I)V) are not independent class references.
				return start == 0 && end + 1 == section.Length;
			}

			static bool HasDescriptorPrefix (byte [] section, int start, bool utf16)
			{
				int width = utf16 ? 2 : 1;
				int position = start - width * 2;
				int value = utf16 ? ReadUtf16 (section, position) : ReadByte (section, position);
				if (!IsClassContinuation (value, utf16) || value == '[') {
					return true;
				}
				// Primitive parameters may precede an object token inside a method descriptor.
				while (value >= 0 && "BCDFIJSZ[".IndexOf ((char) value) >= 0) {
					position -= width;
					value = utf16 ? ReadUtf16 (section, position) : ReadByte (section, position);
				}
				return value == '(' || value == ';';
			}

			static int ReadByte (byte [] section, int position)
				=> position < 0 || position >= section.Length ? -1 : section [position];

			static int ReadUtf16 (byte [] section, int position)
				=> position < 0 || position + 1 >= section.Length ? -1 : section [position] | section [position + 1] << 8;

			static bool IsClassContinuation (int value, bool utf16)
			{
				if (value < 0) {
					return false;
				}
				if (!utf16 && value >= 0x80) {
					return value <= 0xF4;
				}
				char c = (char) value;
				return c == '/' || c == '.' || c == '$' || c == '_' || char.IsLetterOrDigit (c) ||
					char.IsSurrogate (c) || char.GetUnicodeCategory (c) is
						System.Globalization.UnicodeCategory.NonSpacingMark or
						System.Globalization.UnicodeCategory.SpacingCombiningMark or
						System.Globalization.UnicodeCategory.CurrencySymbol or
						System.Globalization.UnicodeCategory.ConnectorPunctuation or
						System.Globalization.UnicodeCategory.LetterNumber;
			}
		}
	}
}
