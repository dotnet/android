using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Build.Tests
{
	[Flags]
	enum ELFSectionFlags
	{
		None                   = 0,
		Allocatable            = 1 << 0,
		GnuMbind               = 1 << 1,
		Excluded               = 1 << 2,
		ReferencesOtherSection = 1 << 3,
		Writable               = 1 << 4,
		Executable             = 1 << 5,
		Mergeable              = 1 << 6,
		HasCStrings            = 1 << 7,
		GroupMember            = 1 << 8,
		ThreadLocalStorage     = 1 << 9,
		MemberOfPreviousGroup  = 1 << 10,
		Retained               = 1 << 11,
		Number                 = 1 << 12,
		Custom                 = 1 << 13,
	}

	enum ELFSectionType
	{
		None,
		Progbits, // @progbits
		Nobits, // @nobits
		Note, // @note
		InitArray, // @init_array
		FiniArray, // @fini_array
		PreInitArray, // @preinit_array
		Number, // @<number>
		Custom, // @<target specific>
	}

	enum ELFSymbolType
	{
		Function,
		GnuIndirectFunction,
		GnuUniqueObject,
		Object,
		TlsObject,
		Common,
		NoType,
	}

	class NativeAssemblyParser
	{
		public enum SymbolMetadataKind
		{
			Global,
			Local,
			Size,
			Type,
		}

		public sealed class AssemblerSection
		{
			public readonly ELFSectionType Type;
			public readonly string Name;
			public readonly ELFSectionFlags Flags;

			public AssemblerSection (string name, ELFSectionType type, ELFSectionFlags flags)
			{
				if (String.IsNullOrEmpty (name)) {
					throw new ArgumentException ("must not be null or empty", nameof (name));
				}

				Name = name;
				Type = type;
				Flags = flags;
			}
		}

		public sealed class AssemblerSymbolItem
		{
			public readonly string Contents;
			public readonly ulong LineNumber;

			public AssemblerSymbolItem (string contents, ulong lineNumber)
			{
				Contents = contents;
				LineNumber = lineNumber;
			}
		}

		public sealed class AssemblerSymbol
		{
			public readonly AssemblerSection Section;
			public readonly string Name;
			public readonly List<AssemblerSymbolItem> Contents;
			public readonly ulong LineNumber;

			public bool IsGlobal { get; private set; }
			public ulong Size { get; private set; }
			public ELFSymbolType Type { get; private set; }

			public AssemblerSymbol (string name, AssemblerSection section, ulong lineNumber)
			{
				Section = section ?? throw new ArgumentNullException (nameof (section));

				if (String.IsNullOrEmpty (name)) {
					throw new ArgumentException ("must not be null or empty", nameof (name));
				}

				Name = name;
				Contents = new List<AssemblerSymbolItem> ();
				LineNumber = lineNumber;
			}

			public void SetMetadata (List<SymbolMetadata> metadata)
			{
				IsGlobal = false;
				Size = 0;
				Type = ELFSymbolType.NoType;

				if (metadata == null || metadata.Count == 0) {
					return;
				}

				foreach (SymbolMetadata item in metadata) {
					switch (item.Kind) {
						case SymbolMetadataKind.Local:
							IsGlobal = false;
							break;

						case SymbolMetadataKind.Global:
							IsGlobal = true;
							break;

						case SymbolMetadataKind.Size:
							if (!UInt64.TryParse (item.Value, out ulong size)) {
								throw new InvalidOperationException ($"Unable to parse symbol size as ulong from '{item.Value}'");
							}
							Size = size;
							break;

						case SymbolMetadataKind.Type:
							if (String.Compare ("function", item.Value, StringComparison.OrdinalIgnoreCase) == 0) {
								Type = ELFSymbolType.Function;
							} else if (String.Compare ("gnu_indirect_function", item.Value, StringComparison.OrdinalIgnoreCase) == 0) {
								Type = ELFSymbolType.GnuIndirectFunction;
							} else if (String.Compare ("object", item.Value, StringComparison.OrdinalIgnoreCase) == 0) {
								Type = ELFSymbolType.Object;
							} else if (String.Compare ("tls_object", item.Value, StringComparison.OrdinalIgnoreCase) == 0) {
								Type = ELFSymbolType.TlsObject;
							} else if (String.Compare ("common", item.Value, StringComparison.OrdinalIgnoreCase) == 0) {
								Type = ELFSymbolType.Common;
							} else if (String.Compare ("notype", item.Value, StringComparison.OrdinalIgnoreCase) == 0) {
								Type = ELFSymbolType.NoType;
							} else if (String.Compare ("gnu_unique_object", item.Value, StringComparison.OrdinalIgnoreCase) == 0) {
								Type = ELFSymbolType.GnuUniqueObject;
							} else {
								throw new InvalidOperationException ($"Unsupported symbol type '{item.Value}'");
							}
							break;

						default:
							throw new InvalidOperationException ($"Unsupported symbol kind '{item.Kind}'");
					}
				}
			}
		}

		public sealed class SymbolMetadata
		{
			public readonly SymbolMetadataKind Kind;
			public readonly string Value;

			public SymbolMetadata (SymbolMetadataKind kind, string value = null)
			{
				Kind = kind;
				Value = value;
			}
		}

		static readonly HashSet<string> ignoredDirectives = new HashSet<string> (StringComparer.OrdinalIgnoreCase) {
			".arch",
			".eabi_attribute",
			".file",
			".fpu",
			".ident",
			".p2align",
			".syntax",
		};

		static readonly char[] splitOnWhitespace = new char[] { ' ', '\t' };
		static readonly char[] splitOnComma = new char[] { ',' };
		static readonly Regex assemblerLabelRegex = new Regex ("^[_.a-zA-Z0-9]+:", RegexOptions.Compiled);

		Dictionary<string, AssemblerSymbol> symbols = new Dictionary<string, AssemblerSymbol> (StringComparer.Ordinal);
		Dictionary<string, List<SymbolMetadata>> symbolMetadata = new Dictionary<string, List<SymbolMetadata>> (StringComparer.Ordinal);
		string abiCommentStart;

		public Dictionary<string, AssemblerSymbol> Symbols => symbols;

		public NativeAssemblyParser (string sourceFilePath, string abi)
		{
			if (String.IsNullOrEmpty (sourceFilePath)) {
				throw new ArgumentException ("must not be null or empty", nameof (sourceFilePath));
			}

			if (!File.Exists (sourceFilePath)) {
				throw new InvalidOperationException ($"File '{sourceFilePath}' does not exist");
			}

			switch (abi) {
				case "arm64-v8a":
					abiCommentStart = "//";
					break;

				case "armeabi-v7a":
					abiCommentStart = "@";
					break;

				case "x86":
				case "x86_64":
					abiCommentStart = "#";
					break;

				default:
					throw new InvalidOperationException ($"Unsupported ABI '{abi}'");
			}

			Load (sourceFilePath);
		}

		void Load (string sourceFilePath)
		{
			AssemblerSection currentSection = null;
			AssemblerSymbol currentSymbol = null;

			string symbolName;
			ulong lineNumber = 0;
			foreach (string l in File.ReadLines (sourceFilePath, Encoding.UTF8)) {
				lineNumber++;

				string line = TrimLine (l);
				if (String.IsNullOrEmpty (line)) {
					continue;
				}

				string[] parts = line.Split (splitOnWhitespace, 2, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0 || (ignoredDirectives.Contains (parts[0]))) {
					continue;
				}

				if (StartsNewSection (parts, ref currentSection)) {
					currentSymbol = null; // Symbols cannot cross sections
					continue;
				}

				if (IsSymbolMetadata (parts)) {
					continue;
				}

				if (assemblerLabelRegex.IsMatch (line)) {
					symbolName = GetSymbolName (line);
					currentSymbol = AddNewSymbol (symbolName);
					continue;
				}

				if (IsCommSymbol (parts, out symbolName)) {
					// .comm creates symbol in a single line, there won't be any "contents", so we add a new
					// symbol and close the current one
					AddNewSymbol (symbolName);
					currentSymbol = null;
					continue;
				}

				if (currentSymbol == null) {
					throw new InvalidOperationException ($"Found non-empty line, but there's no current symbol to append it to as contents. '{sourceFilePath}', line {lineNumber}");
				}

				currentSymbol.Contents.Add (new AssemblerSymbolItem (line, lineNumber));
			}

			foreach (var kvp in symbolMetadata) {
				symbolName = kvp.Key;
				List<SymbolMetadata> metadata = kvp.Value;

				if (!symbols.TryGetValue (symbolName, out AssemblerSymbol symbol)) {
					continue;
				}
				symbol?.SetMetadata (metadata);
			}

			string TrimLine (string line)
			{
				string ret = line.Trim ();
				int commentStart = ret.IndexOf (abiCommentStart, StringComparison.Ordinal);

				if (commentStart < 0) {
					return ret;
				}

				if (commentStart == 0) {
					return String.Empty;
				}

				return ret.Substring (0, commentStart).Trim ();
			}

			AssemblerSymbol AddNewSymbol (string symbolName)
			{
				var ret = new AssemblerSymbol (symbolName, currentSection, lineNumber);
				symbols.Add (symbolName, ret);
				return ret;
			}

			bool IsCommSymbol (string[] parts, out string symbolName)
			{
				if (parts.Length < 2 || String.Compare (".comm", parts[0], StringComparison.Ordinal) != 0) {
					symbolName = String.Empty;
					return false;
				}

				// .comm in ELF takes an optional 3rd parameter, which sets the desired alignment
				// We ignore it here as it doesn't really matter to us
				string[] commParts = GetSymbolMetadataParts (".comm", parts[1], 2);
				symbolName = commParts[0].Trim ();
				new SymbolMetadata (SymbolMetadataKind.Size, commParts[1].Trim ());

				return true;
			}

			bool IsSymbolMetadata (string[] parts)
			{
				if (parts.Length < 2) {
					return false;
				}

				string symbolName = null;
				SymbolMetadata metadata = null;
				if (String.Compare (".type", parts[0], StringComparison.Ordinal) == 0) {
					(symbolName, metadata) = GetSymbolType (parts[1]);
				} else if (String.Compare (".globl", parts[0], StringComparison.Ordinal) == 0 || String.Compare (".global", parts[0], StringComparison.Ordinal) == 0) {
					(symbolName, metadata) = GetGlobal (parts[0], parts[1]);
				} else if (String.Compare (".size", parts[0], StringComparison.Ordinal) == 0) {
					(symbolName, metadata) = GetSymbolSize (parts[1]);
					currentSymbol = null; // .size ends the symbol definition in the assembly generated by LLVM
				} else if (String.Compare (".local", parts[0], StringComparison.Ordinal) == 0) {
					// .local takes a comma-separated list of symbol names
					foreach (string name in parts[1].Split (splitOnComma, StringSplitOptions.RemoveEmptyEntries)) {
						StoreSymbolMetadata (name, new SymbolMetadata (SymbolMetadataKind.Local));
					}
					return true;
				}

				if (!String.IsNullOrEmpty (symbolName) && metadata != null) {
					StoreSymbolMetadata (symbolName, metadata);
					return true;
				}

				return false;
			}

			void StoreSymbolMetadata (string symbolName, SymbolMetadata metadata)
			{
				if (!symbolMetadata.TryGetValue (symbolName, out List<SymbolMetadata> metadataList)) {
					metadataList = new List<SymbolMetadata> ();
					symbolMetadata.Add (symbolName, metadataList);
				}
				metadataList.Add (metadata);
			}

			(string symbolName, SymbolMetadata metadata) GetSymbolSize (string directive)
			{
				string[] parts = GetSymbolMetadataParts (".size", directive, 2);
				return (parts[0].Trim (), new SymbolMetadata (SymbolMetadataKind.Size, parts[1].Trim ()));
			}

			(string symbolName, SymbolMetadata metadata) GetGlobal (string name, string directive)
			{
				string[] parts = GetSymbolMetadataParts (name, directive, 1);
				return (parts[0].Trim (), new SymbolMetadata (SymbolMetadataKind.Global));
			}

			(string symbolName, SymbolMetadata metadata) GetSymbolType (string directive)
			{
				string[] parts = GetSymbolMetadataParts (".type", directive, 2);
				return (parts[0].Trim (), new SymbolMetadata (SymbolMetadataKind.Type, parts[1].Trim ().Substring (1)));
			}

			string[] GetSymbolMetadataParts (string directiveName, string directiveValue, int requiredParts)
			{
				string[] parts = directiveValue.Split (splitOnComma, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length < requiredParts) {
					throw new InvalidOperationException ($"Invalid format of the '{directiveName}' directive in '{sourceFilePath}', line {lineNumber}. Expected {requiredParts}, got {parts.Length}");
				}

				return parts;
			}

			bool StartsNewSection (string[] parts, ref AssemblerSection newSection)
			{
				if (String.Compare (".text", parts[0], StringComparison.Ordinal) == 0) {
					newSection = new AssemblerSection (parts[0], ELFSectionType.Progbits, ELFSectionFlags.Allocatable | ELFSectionFlags.Executable);
					return true;
				}

				if (String.Compare (".data", parts[0], StringComparison.Ordinal) == 0) {
					newSection = new AssemblerSection (parts[0], ELFSectionType.Progbits, ELFSectionFlags.Allocatable | ELFSectionFlags.Writable);
					return true;
				}

				if (String.Compare (".bss", parts[0], StringComparison.Ordinal) == 0) {
					newSection = new AssemblerSection (parts[0], ELFSectionType.Nobits, ELFSectionFlags.Allocatable | ELFSectionFlags.Writable);
					return true;
				}

				if (String.Compare (".section", parts[0], StringComparison.Ordinal) != 0) {
					return false;
				}

				if (parts.Length < 2) {
					throw new InvalidOperationException ($"Invalid format of the '.section' directive in '{sourceFilePath}', line {lineNumber}");
				}

				string[] sectionParts = parts[1].Split (splitOnComma, StringSplitOptions.RemoveEmptyEntries);
				if (sectionParts.Length == 0) {
					throw new InvalidOperationException ($"Invalid format of the '.section' directive in '{sourceFilePath}', line {lineNumber}");
				}

				string name = sectionParts[0].Trim ();
				ELFSectionFlags flags = ELFSectionFlags.None;

				if (sectionParts.Length > 1) {
					flags = ParseSectionFlags (sectionParts[1].Trim ());
				}

				ELFSectionType type = ELFSectionType.None;
				if (sectionParts.Length > 2) {
					type = ParseSectionType (sectionParts[2].Trim ());
				}

				newSection = new AssemblerSection (parts[0], type, flags);
				return true;
			}

			ELFSectionType ParseSectionType (string type)
			{
				if (String.IsNullOrEmpty (type)) {
					return ELFSectionType.None;
				}

				if (type.Length < 2) {
					throw new InvalidOperationException ($"Invalid .section type '{type}' in '{sourceFilePath}', line {lineNumber}");
				}

				type = type.Substring (1); // ignore the arch-specific first character
				if (String.Compare (type, "progbits", StringComparison.OrdinalIgnoreCase) == 0) {
					return ELFSectionType.Progbits;
				}

				if (String.Compare (type, "nobits", StringComparison.OrdinalIgnoreCase) == 0) {
					return ELFSectionType.Nobits;
				}

				if (String.Compare (type, "note", StringComparison.OrdinalIgnoreCase) == 0) {
					return ELFSectionType.Note;
				}

				if (String.Compare (type, "init_array", StringComparison.OrdinalIgnoreCase) == 0) {
					return ELFSectionType.InitArray;
				}

				if (String.Compare (type, "fini_array", StringComparison.OrdinalIgnoreCase) == 0) {
					return ELFSectionType.FiniArray;
				}

				if (String.Compare (type, "preinit_array", StringComparison.OrdinalIgnoreCase) == 0) {
					return ELFSectionType.PreInitArray;
				}

				if (UInt64.TryParse (type, out _)) {
					return ELFSectionType.Number;
				}

				return ELFSectionType.Custom;
			}

			ELFSectionFlags ParseSectionFlags (string flags)
			{
				ELFSectionFlags ret = ELFSectionFlags.None;

				if (String.IsNullOrEmpty (flags)) {
					return ret;
				}

				foreach (char ch in flags) {
					switch (ch) {
						case 'a':
							ret |= ELFSectionFlags.Allocatable;
							break;

						case 'd':
							ret |= ELFSectionFlags.GnuMbind;
							break;

						case 'e':
							ret |= ELFSectionFlags.Excluded;
							break;

						case 'o':
							ret |= ELFSectionFlags.ReferencesOtherSection;
							break;

						case 'w':
							ret |= ELFSectionFlags.Writable;
							break;

						case 'x':
							ret |= ELFSectionFlags.Executable;
							break;

						case 'M':
							ret |= ELFSectionFlags.Mergeable;
							break;

						case 'S':
							ret |= ELFSectionFlags.HasCStrings;
							break;

						case 'G':
							ret |= ELFSectionFlags.GroupMember;
							break;

						case 'T':
							ret |= ELFSectionFlags.ThreadLocalStorage;
							break;

						case '?':
							ret |= ELFSectionFlags.MemberOfPreviousGroup;
							break;

						case 'R':
							ret |= ELFSectionFlags.Retained;
							break;

						case '"':
							break; // ignore

						default:
							throw new InvalidOperationException ($"Unknown .section flag '{ch}' in '{sourceFilePath}', line {lineNumber}");
					}
				}

				return ret;
			}
		}

		string GetSymbolName (string line)
		{
			int colon = line.IndexOf (':');
			return line.Substring (0, colon);
		}
	}
}
