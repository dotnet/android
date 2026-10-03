#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ELFSharp;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

namespace Xamarin.Android.Tasks.JniRemapping
{
	static class NativeAotJniRetention
	{
		public static HashSet<string> GetRequiredEntries (string objectFile, R8Mapping mapping)
		{
			var sections = ReadObjectData (objectFile);
			var classes = new List<R8ClassMapping> (mapping.EnumerateClassMappings ());
			var classPatterns = new LiteralMatcher (requireClassBoundaries: true);
			foreach (var type in classes) {
				classPatterns.Add (type.OriginalJniName);
				classPatterns.Add (type.OriginalJniName.Replace ('/', '.'));
			}
			HashSet<string> retainedClasses = classPatterns.Match (sections);

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
			HashSet<string> retainedMembers = memberPatterns.Match (sections);
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

		static List<byte []> ReadObjectData (string path)
		{
			using var stream = File.OpenRead (path);
			using IELF elf = ReadElfData (() => ELFReader.Load (stream, shouldOwnStream: false));
			ulong fileSize = (ulong) stream.Length;
			if (elf.Type != FileType.Relocatable || elf.Endianess != Endianess.LittleEndian ||
					(elf.Class != Class.Bit64 && elf.Class != Class.Bit32)) {
				throw new InvalidDataException (Properties.Resources.XA4325_NativeAotObjectFormat);
			}
			var data = new List<byte []> ();
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
			}
			if (!hasManagedCode || !hasData) {
				throw new InvalidDataException (Properties.Resources.XA4325_NativeAotMissingSections);
			}
			var decoded = new HashSet<(ISection Section, ulong Offset)> ();
			foreach (ISection section in elf.Sections) {
				if (section is not ISymbolTable symbols) {
					continue;
				}
				foreach (ISymbolEntry symbol in symbols.Entries) {
					if (!symbol.Name.EndsWith ("__dehydrated_data", StringComparison.Ordinal) || symbol.IsPointedIndexSpecial) {
						continue;
					}
					ulong offset = symbol switch {
						SymbolEntry<ulong> symbol64 => symbol64.Value,
						SymbolEntry<uint> symbol32 => symbol32.Value,
						_ => throw new InvalidDataException (Properties.Resources.XA4325_NativeAotObjectFormat),
					};
					ISection target = ReadElfData (() => symbol.PointedSection);
					if (!sectionData.TryGetValue (target, out byte []? contents) || offset > (ulong) contents.Length) {
						throw new InvalidDataException (Properties.Resources.XA4325_NativeAotInvalidSection);
					}
					if (offset == (ulong) contents.Length || !decoded.Add ((target, offset))) {
						continue;
					}
					ulong symbolSize = symbol switch {
						SymbolEntry<ulong> symbol64 => symbol64.Size,
						SymbolEntry<uint> symbol32 => symbol32.Size,
						_ => 0,
					};
					ReadDehydratedLiterals (contents, checked ((int) offset), symbolSize, data);
				}
			}
			return data;
		}

		// ILC's __dehydrated_data header contains a relative destination pointer and the
		// command-stream length. Evaluate literals without mistaking commands for UTF-16.
		// Format: dotnet/runtime src/coreclr/tools/Common/Internal/Runtime/DehydratedData.cs.
		static void ReadDehydratedLiterals (byte [] contents, int start, ulong symbolSize, List<byte []> data)
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
			literal.WriteByte (0);
			literal.WriteByte (0);
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
					literal.Write (contents, position, payload);
					position += payload;
					break;
				case 1: // ZeroFill; legal JNI identifiers contain no NULs.
					if (payload == 0) {
						throw InvalidDehydration ();
					}
					FlushLiteral ();
					break;
				case 2: // RelPtr32Reloc
				case 3: // PtrReloc
					if ((long) end + (long) payload * 4 + 4 > contents.Length) {
						throw InvalidDehydration ();
					}
					FlushLiteral ();
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
				if (literal.Length > 2) {
					literal.WriteByte (0);
					literal.WriteByte (0);
					data.Add (literal.ToArray ());
				}
				literal.SetLength (0);
				literal.WriteByte (0);
				literal.WriteByte (0);
			}
		}

		static uint ReadUInt32 (byte [] bytes, int position)
			=> (uint) (bytes [position] | bytes [position + 1] << 8 |
				bytes [position + 2] << 16 | bytes [position + 3] << 24);

		static InvalidDataException InvalidDehydration ()
			=> new InvalidDataException (Properties.Resources.XA4325_NativeAotInvalidDehydration);

		static T ReadElfData<T> (Func<T> read)
		{
			try {
				return read ();
			} catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException ||
					ex is IndexOutOfRangeException || ex is OverflowException) {
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
				public bool TrimmedLeadingZero { get; }
				public bool TrimmedTrailingZero { get; }

				public Pattern (string text, int length, bool utf16, bool descriptor,
					bool trimmedLeadingZero = false, bool trimmedTrailingZero = false)
				{
					Text = text;
					Length = length;
					Utf16 = utf16;
					Descriptor = descriptor;
					TrimmedLeadingZero = trimmedLeadingZero;
					TrimmedTrailingZero = trimmedTrailingZero;
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
				int start = utf16 [0] == 0 ? 1 : 0;
				int length = utf16.Length - start - (utf16 [utf16.Length - 1] == 0 ? 1 : 0);
				var payload = new byte [length];
				Buffer.BlockCopy (utf16, start, payload, 0, length);
				Add (payload, new Pattern (pattern, payload.Length, utf16: true, descriptor,
					trimmedLeadingZero: start != 0,
					trimmedTrailingZero: utf16 [utf16.Length - 1] == 0));
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
				foreach (byte [] section in sections) {
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
									if (!requireClassBoundaries || HasClassBoundaries (section, position, pattern)) {
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
				if (pattern.Utf16 && pattern.TrimmedLeadingZero && start > 0 && section [start - 1] == 0) {
					start--;
				}
				if (pattern.Descriptor) {
					return HasDescriptorPrefix (section, start + (pattern.Utf16 ? 2 : 1), pattern.Utf16);
				}
				int after = end + 1;
				if (pattern.Utf16 && pattern.TrimmedTrailingZero && after < section.Length && section [after] == 0) {
					after++;
				}
				int beforeValue = pattern.Utf16 ? ReadUtf16 (section, start - 2) : ReadByte (section, start - 1);
				int afterValue = pattern.Utf16 ? ReadUtf16 (section, after) : ReadByte (section, after);

				return !IsClassContinuation (beforeValue, pattern.Utf16) &&
					!IsClassContinuation (afterValue, pattern.Utf16);
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
