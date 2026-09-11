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
	/// <summary>
	/// A conservative bound for normal generated bindings with literal JNI identifiers.
	/// Unlike compiled method names, literals survive inlining, generic sharing and static initialization.
	/// Frozen strings are UTF-16; reflection metadata contains UTF-8 strings. Neither symbol
	/// names nor debug information are evidence that an identifier survived compilation.
	/// Arbitrary runtime-constructed names require explicit remapping or R8 keep rules.
	/// </summary>
	static class NativeAotJniRetention
	{
		public static HashSet<string> GetRequiredEntries (string objectFile, R8Mapping mapping)
		{
			var sections = ReadObjectData (objectFile);
			var classes = new List<R8ClassMapping> (mapping.EnumerateClassMappings ());
			var classPatterns = new LiteralMatcher ();
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
					// Generated constructor calls carry only the descriptor, not "<init>".
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
				throw new InvalidDataException (Properties.Resources.XA4327_NativeAotObjectFormat);
			}
			var data = new List<byte []> ();
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
					throw new InvalidDataException (Properties.Resources.XA4327_NativeAotObjectFormat);
				}
				if (section.Type != SectionType.NoBits && (offset > fileSize || size > fileSize - offset)) {
					throw new InvalidDataException (Properties.Resources.XA4327_NativeAotInvalidSection);
				}
				if ((section.Flags & SectionFlags.Allocatable) == 0 || section.Type == SectionType.NoBits) {
					continue;
				}
				byte [] contents = ReadElfData (() => section.GetContents ());
				if ((ulong) contents.Length != size) {
					throw new InvalidDataException (Properties.Resources.XA4327_NativeAotTruncatedSection);
				}
				if (contents.Length == 0) {
					continue;
				}
				hasManagedCode |= section.Name == "__managedcode";
				hasData |= (section.Flags & SectionFlags.Executable) == 0;
				data.Add (contents);
			}
			if (!hasManagedCode || !hasData) {
				throw new InvalidDataException (Properties.Resources.XA4327_NativeAotMissingSections);
			}
			return data;
		}

		static T ReadElfData<T> (Func<T> read)
		{
			try {
				return read ();
			} catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException ||
					ex is IndexOutOfRangeException || ex is OverflowException) {
				// ELFSharp uses these exceptions for malformed headers, string tables and section
				// indexes. Normalize only library reads, not failures in the retention matcher.
				throw new InvalidDataException (ex.Message, ex);
			}
		}

		// Match substrings deliberately: member IDs, registration blocks and descriptors contain
		// multiple JNI identifiers. Shared or coincidental matches can only retain extra entries.
		// A compact Aho-Corasick trie avoids scanning a large object once per mapping entry.
		sealed class LiteralMatcher
		{
			struct Node
			{
				public byte Value;
				public int Child;
				public int Sibling;
				public int Failure;
				public int Output;
				public List<string>? Patterns;
			}

			Node [] nodes = new Node [256];
			int count = 1;
			readonly int [] root = new int [256];
			readonly HashSet<string> patterns = new HashSet<string> (StringComparer.Ordinal);

			public void Add (string pattern)
			{
				if (pattern.Length == 0 || !patterns.Add (pattern)) {
					return;
				}
				Add (Encoding.UTF8.GetBytes (pattern), pattern);
				byte [] utf16 = Encoding.Unicode.GetBytes (pattern);
				// ILC dehydration replaces runs of >=4 zero bytes. A legal JNI identifier has
				// no NULs, so its interior is intact, but a boundary zero byte can join a run
				// in the string header, terminator or alignment padding. Do not require it.
				int start = utf16 [0] == 0 ? 1 : 0;
				int length = utf16.Length - start - (utf16 [utf16.Length - 1] == 0 ? 1 : 0);
				var payload = new byte [length];
				Buffer.BlockCopy (utf16, start, payload, 0, length);
				Add (payload, pattern);
			}

			void Add (byte [] bytes, string pattern)
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
					nodes [current].Patterns = terminalPatterns = new List<string> ();
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
					foreach (byte value in section) {
						int next;
						while ((next = Find (current, value)) == 0 && current != 0) {
							current = nodes [current].Failure;
						}
						current = next;
						for (int output = current; output != 0; output = nodes [output].Output) {
							var terminalPatterns = nodes [output].Patterns;
							if (terminalPatterns != null) {
								foreach (string pattern in terminalPatterns) {
									found.Add (pattern);
								}
							}
						}
					}
				}
				return found;
			}
		}
	}
}
