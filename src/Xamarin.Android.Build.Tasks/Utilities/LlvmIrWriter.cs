#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Minimal helper used by the native assembly generators to write textual LLVM IR.  The generators
/// write the IR text themselves (mostly as interpolated raw string literals), this class merely
/// takes care of the parts shared by all of them: the module header and metadata footer, optional
/// comments, string literals and alignment of global symbols.
/// </summary>
sealed class LlvmIrWriter
{
	// Symbol attributes, see https://llvm.org/docs/LangRef.html#global-variables
	public const string GlobalConstant = "dso_local local_unnamed_addr constant";
	public const string GlobalWritable = "dso_local local_unnamed_addr global";
	public const string LocalConstant  = "internal dso_local constant";
	public const string LocalString    = "private unnamed_addr constant";

	readonly TextWriter output;

	public LlvmIrTarget Target { get; }

	/// <summary>
	/// Whether descriptive comments are written to the generated LLVM IR.  Comments make the
	/// output easier to read, but they can account for the majority of the file's size and
	/// have no effect on the code <c>llc</c> produces.
	/// </summary>
	public bool EmitComments { get; }

	public LlvmIrWriter (TextWriter output, LlvmIrTarget target, bool emitComments)
	{
		this.output = output ?? throw new ArgumentNullException (nameof (output));
		Target = target ?? throw new ArgumentNullException (nameof (target));
		EmitComments = emitComments;
	}

	/// <summary>
	/// Writes <paramref name="text"/> verbatim, except that line breaks (which may come from the
	/// C# source files the raw string literals live in) are written using the output's newline
	/// sequence.
	/// </summary>
	public void Write (string text)
	{
		string newLine = output.NewLine;
		if (text.IndexOf ('\n') < 0 || (MonoAndroidHelper.StringEquals ("\n", newLine) && text.IndexOf ('\r') < 0)) {
			output.Write (text);
			return;
		}

		int start = 0;
		while (start < text.Length) {
			int eol = text.IndexOf ('\n', start);
			if (eol < 0) {
				output.Write (text.Substring (start));
				break;
			}

			int end = eol > start && text [eol - 1] == '\r' ? eol - 1 : eol;
			output.Write (text.Substring (start, end - start));
			output.Write (newLine);
			start = eol + 1;
		}
	}

	public void WriteLine (string text = "")
	{
		Write (text);
		output.WriteLine ();
	}

	/// <summary>
	/// Returns <paramref name="text"/> as an LLVM IR comment (i.e. prefixed with <c>;</c>) if comments are
	/// enabled, an empty string otherwise.  The text is expected to contain any leading whitespace.
	/// </summary>
	public string Comment (string? text) => EmitComments && !text.IsNullOrEmpty () ? $";{SanitizeComment (text)}" : "";

	/// <summary>
	/// Returns a comment which follows a value on the same line (separated with a space), if comments are enabled.
	/// </summary>
	public string TrailingComment (string? text) => EmitComments && !text.IsNullOrEmpty () ? $" ;{SanitizeComment (text)}" : "";

	public void WriteCommentLine (string? text)
	{
		if (!EmitComments || text.IsNullOrEmpty ()) {
			return;
		}

		WriteLine ($";{SanitizeComment (text)}");
	}

	// Comments end at the end of line, a line break in e.g. an environment variable value would
	// turn the rest of the comment into (invalid) IR code
	static string SanitizeComment (string text)
	{
		if (text.IndexOf ('\n') < 0 && text.IndexOf ('\r') < 0) {
			return text;
		}

		return text.Replace ("\r", "\\r").Replace ("\n", "\\n");
	}

	public void WriteHeader (string fileName)
	{
		string name = Path.GetFileName (fileName);

		WriteCommentLine ($" ModuleID = '{name}'");
		Write ($$"""
			source_filename = "{{name}}"
			target datalayout = "{{Target.DataLayout}}"
			target triple = "{{Target.Triple}}"

			""");
	}

	/// <summary>
	/// Writes a global symbol definition.  <paramref name="value"/> may span several lines.
	/// </summary>
	public void WriteGlobal (string name, string attributes, string type, string value, ulong alignment, string? comment = null)
	{
		WriteLine ();
		WriteCommentLine (comment);
		WriteLine ($"@{name} = {attributes} {type} {value}, align {Number (alignment)}");
	}

	/// <summary>
	/// Renders an array initializer in which every element is written on its own line.  Elements must
	/// already be indented.  <paramref name="getElementComment"/> may return a comment which is
	/// placed after the element when comments are enabled.
	/// </summary>
	public string ArrayValue (IList<string> elements, Func<int, string?>? getElementComment = null)
	{
		if (elements.Count == 0) {
			return "zeroinitializer";
		}

		var sb = new StringBuilder ();
		sb.Append ("[\n");
		for (int i = 0; i < elements.Count; i++) {
			if (i > 0) {
				sb.Append (',');
				AppendCommentOrNewline (i - 1);
			}
			sb.Append (elements [i]);
		}
		AppendCommentOrNewline (elements.Count - 1);
		sb.Append (']');

		return sb.ToString ();

		void AppendCommentOrNewline (int index)
		{
			sb.Append (getElementComment != null ? TrailingComment (getElementComment (index)) : "");
			sb.Append ('\n');
		}
	}

	/// <summary>
	/// Returns the conventional array element comment, i.e. the element index.
	/// </summary>
	public static string IndexComment (int index) => $" {Number (index)}";

	/// <summary>
	/// Alignment of a structure or an array with elements of the given size and alignment.
	/// </summary>
	public ulong GetAggregateAlignment (ulong maxFieldAlignment, ulong dataSize) => Target.GetAggregateAlignment (maxFieldAlignment, dataSize);

	public ulong GetPointerArrayAlignment (int count) => GetAggregateAlignment (Target.PointerSize, (ulong)count * Target.PointerSize);

	/// <summary>
	/// Writes the module metadata (module flags, identification and the type based alias analysis
	/// descriptors), which ends every module.
	/// </summary>
	public void WriteMetadata ()
	{
		var flags = new StringBuilder ("!0, !1");
		var targetFlags = new StringBuilder ();
		for (int i = 0; i < Target.ModuleFlags.Length; i++) {
			int n = i + 7;
			flags.Append ($", !{Number (n)}");
			targetFlags.Append ($"!{Number (n)} = !{{{Target.ModuleFlags [i]}}}\n");
		}

		WriteLine ();
		WriteCommentLine (" Metadata");
		Write ($$"""
			!llvm.module.flags = !{{{flags}}}
			!0 = !{i32 1, !"wchar_size", i32 4}
			!1 = !{i32 7, !"PIC Level", i32 2}
			!llvm.ident = !{!2}
			!2 = !{!".NET for Android {{XABuildConfig.XamarinAndroidBranch}} @ {{XABuildConfig.XamarinAndroidCommitHash}}"}
			!3 = !{!4, !4, i64 0}
			!4 = !{!"any pointer", !5, i64 0}
			!5 = !{!"omnipotent char", !6, i64 0}
			!6 = !{!"Simple C++ TBAA"}

			""");
		Write (targetFlags.ToString ());
	}

	/// <summary>
	/// Writes declarations of external functions.  All of them use the same, dummy, signature since
	/// we only care about being able to take their address.
	/// </summary>
	public void WriteExternalFunctionDeclarations (ICollection<string> names)
	{
		if (names.Count == 0) {
			return;
		}

		var sorted = new List<string> (names);
		sorted.Sort ((string a, string b) => a.CompareTo (b));

		WriteLine ();
		Write (Comment (" External functions"));
		foreach (string name in sorted) {
			WriteLine ();
			Write ($"declare void @{name}() local_unnamed_addr");
		}
	}

	public static string Number (int value) => value.ToString (CultureInfo.InvariantCulture);
	public static string Number (uint value) => value.ToString (CultureInfo.InvariantCulture);
	public static string Number (ulong value) => value.ToString (CultureInfo.InvariantCulture);

	public static string Hex (uint value) => $"u0x{value.ToString ("x8", CultureInfo.InvariantCulture)}";

	public static string Bool (bool value) => value ? "true" : "false";

	/// <summary>
	/// Renders <paramref name="bytes"/> as an LLVM IR string constant (<c>c"..."</c>), escaping all the
	/// characters which aren't printable ASCII as well as <c>"</c> and <c>\</c>.
	/// </summary>
	public static string QuoteBytes (byte[] bytes, bool nullTerminated)
	{
		var sb = new StringBuilder (bytes.Length + 8);
		sb.Append ("c\"");
		AppendEscaped (sb, bytes);
		if (nullTerminated) {
			sb.Append ("\\00");
		}
		sb.Append ('"');

		return sb.ToString ();
	}

	public static void AppendEscaped (StringBuilder sb, byte[] bytes)
	{
		foreach (byte b in bytes) {
			if (b != (byte)'"' && b != (byte)'\\' && b >= 32 && b < 127) {
				sb.Append ((char)b);
				continue;
			}

			sb.Append ('\\');
			sb.Append (b.ToString ("X2", CultureInfo.InvariantCulture));
		}
	}
}

/// <summary>
/// Collects NUL-terminated UTF-8 strings which are written out as a single byte array, with the
/// strings referred to by their offset into the array.  Identical strings are stored only once.
/// </summary>
sealed class LlvmIrStringBlob
{
	readonly Dictionary<string, int> offsets = new (StringComparer.Ordinal);
	readonly List<byte[]> segments = new ();

	public ulong Size { get; private set; }

	public int Add (string s)
	{
		if (offsets.TryGetValue (s, out int offset)) {
			return offset;
		}

		byte[] bytes = MonoAndroidHelper.Utf8StringToBytes (s);
		offset = (int)Size;
		segments.Add (bytes);
		offsets.Add (s, offset);
		Size += (ulong)bytes.Length + 1;

		return offset;
	}

	public string Type => $"[{LlvmIrWriter.Number (Size)} x i8]";

	public string Value ()
	{
		var sb = new StringBuilder ((int)Size + 2);
		sb.Append ("c\"");
		foreach (byte[] bytes in segments) {
			LlvmIrWriter.AppendEscaped (sb, bytes);
			sb.Append ("\\00");
		}
		sb.Append ('"');

		return sb.ToString ();
	}

	public void Write (LlvmIrWriter writer, string name)
	{
		writer.WriteGlobal (name, LlvmIrWriter.GlobalConstant, Type, Value (), writer.GetAggregateAlignment (1, Size));
	}
}

/// <summary>
/// Private string literals referred to by pointers stored in global symbols.  Every unique string
/// is emitted once, into the group used when it was first requested.  Strings are written at the
/// end of the module, grouped, in the order in which they were first requested.
/// </summary>
sealed class LlvmIrStringPool
{
	sealed class Group
	{
		public readonly string SymbolPrefix;
		public readonly string? Comment;
		public readonly List<(string Symbol, string Value)> Strings = new ();

		public Group (string symbolPrefix, string? comment)
		{
			SymbolPrefix = symbolPrefix;
			Comment = comment;
		}
	}

	const string DefaultGroupName = "str";

	readonly Dictionary<string, string> symbols = new (StringComparer.Ordinal);
	readonly Dictionary<string, Group> groupsByName = new (StringComparer.Ordinal);
	readonly List<Group> groups = new ();

	public bool IsEmpty => symbols.Count == 0;

	/// <summary>
	/// Returns name of the symbol which contains <paramref name="value"/>.  Symbols in the default
	/// group are named <c>.str.N</c>, those in named groups <c>.{group}.N_{suffix}</c>.
	/// </summary>
	public string GetSymbol (string value, string? groupName = null, string? symbolSuffix = null)
	{
		if (symbols.TryGetValue (value, out string? existing)) {
			return existing;
		}

		if (groups.Count == 0) {
			AddGroup (DefaultGroupName, comment: null);
		}

		if (groupName.IsNullOrEmpty ()) {
			groupName = DefaultGroupName;
		}

		if (!groupsByName.TryGetValue (groupName, out Group? group)) {
			group = AddGroup (groupName, groupName);
		}

		string symbol = $"{group.SymbolPrefix}.{LlvmIrWriter.Number (group.Strings.Count)}";
		if (!symbolSuffix.IsNullOrEmpty ()) {
			symbol = $"{symbol}_{symbolSuffix}";
		}
		group.Strings.Add ((symbol, value));
		symbols.Add (value, symbol);

		return symbol;
	}

	/// <summary>
	/// Returns a pointer to the string, or <c>null</c> if <paramref name="value"/> is <c>null</c>.
	/// </summary>
	public string GetPointer (string? value, string? groupName = null, string? symbolSuffix = null)
	{
		if (value == null) {
			return "null";
		}

		return $"@{GetSymbol (value, groupName, symbolSuffix)}";
	}

	Group AddGroup (string name, string? comment)
	{
		var group = new Group ($".{name}", comment);
		groups.Add (group);
		groupsByName.Add (name, group);

		return group;
	}

	public void Write (LlvmIrWriter writer)
	{
		if (IsEmpty) {
			return;
		}

		writer.WriteLine ();
		writer.Write (writer.Comment (" Strings"));
		foreach (Group group in groups) {
			writer.WriteLine ();
			writer.WriteCommentLine (group.Comment);

			foreach ((string symbol, string value) in group.Strings) {
				byte[] bytes = MonoAndroidHelper.Utf8StringToBytes (value);
				ulong size = (ulong)bytes.Length + 1;
				writer.WriteLine ($"@{symbol} = {LlvmIrWriter.LocalString} [{LlvmIrWriter.Number (size)} x i8] {LlvmIrWriter.QuoteBytes (bytes, nullTerminated: true)}, align {LlvmIrWriter.Number (writer.GetAggregateAlignment (1, size))}");
			}
		}
	}
}
