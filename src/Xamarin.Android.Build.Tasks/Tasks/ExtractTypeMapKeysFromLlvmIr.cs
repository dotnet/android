using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public class ExtractTypeMapKeysFromLlvmIr : AndroidTask
{
	static readonly UTF8Encoding Utf8 = new UTF8Encoding (false, true);

	public override string TaskPrefix => "ETMKLI";

	[Required]
	public ITaskItem [] LlvmIrFiles { get; set; } = [];

	[Required]
	public string OutputFile { get; set; } = "";

	public override bool RunTask ()
	{
		string source = "LLVM IR";
		try {
			if (LlvmIrFiles.Length == 0) {
				throw new InvalidDataException ("At least one LLVM IR type map file is required.");
			}
			if (OutputFile.IsNullOrWhiteSpace ()) {
				throw new InvalidDataException ("An output file is required.");
			}

			var keys = new SortedSet<string> (StringComparer.Ordinal);
			foreach (ITaskItem item in LlvmIrFiles) {
				source = item.ItemSpec;
				using var input = new StreamReader (source, Utf8, detectEncodingFromByteOrderMarks: false);
				new TypeMapBlobReader (input, keys).Read ();
			}

			source = OutputFile;
			string? directory = Path.GetDirectoryName (OutputFile);
			if (!directory.IsNullOrEmpty ()) {
				Directory.CreateDirectory (directory);
			}
			using var output = new StreamWriter (OutputFile, append: false, Utf8) { NewLine = "\n" };
			foreach (string key in keys) {
				output.WriteLine (key);
			}
			return !Log.HasLoggedErrors;
		} catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException) {
			Log.LogCodedError ("XA4327", Properties.Resources.XA4327, source, ex.Message);
			return false;
		}
	}

	// Only parse the two CoreCLR string-blob globals, not comments, managed names, or other LLVM strings.
	// Current writers use c"..." literals; older writers used multiline arrays of i8 u0xNN values.
	sealed class TypeMapBlobReader
	{
		readonly TextReader input;
		readonly SortedSet<string> keys;
		readonly List<byte> nameBytes = new List<byte> ();
		long expectedSize;
		long bytesRead;
		int line = 1;

		public TypeMapBlobReader (TextReader input, SortedSet<string> keys)
		{
			this.input = input;
			this.keys = keys;
		}

		public void Read ()
		{
			bool found = false;
			while (SkipTrivia () >= 0) {
				if (input.Peek () != '@') {
					SkipLine ();
					continue;
				}

				string symbol = ReadToken ();
				if (symbol != "@java_type_names" && symbol != "@type_map_java_type_names") {
					SkipLine ();
					continue;
				}
				if (found) {
					throw Invalid ("More than one CoreCLR Java type-name blob was found.");
				}

				found = true;
				ReadBlob ();
			}
			if (!found) {
				throw Invalid ("No supported CoreCLR Java type-name blob was found.");
			}
		}

		void ReadBlob ()
		{
			Expect ("=");
			string token = ReadToken ();
			if (token == "dso_local") {
				token = ReadToken ();
			}
			if (token == "local_unnamed_addr") {
				token = ReadToken ();
			}
			if (token != "constant") {
				throw Invalid ("Expected a constant Java type-name blob.");
			}
			Expect ("[");
			if (!Int64.TryParse (ReadToken (), NumberStyles.None, CultureInfo.InvariantCulture, out expectedSize)) {
				throw Invalid ("Invalid Java type-name blob size.");
			}
			Expect ("x");
			Expect ("i8");
			Expect ("]");

			int initializer = SkipTrivia ();
			if (initializer == 'c') {
				ReadByteString ();
			} else if (initializer == '[') {
				ReadByteArray ();
			} else {
				throw Invalid ("Unsupported Java type-name blob initializer.");
			}

			if (bytesRead != expectedSize || nameBytes.Count != 0) {
				throw Invalid ("Java type-name blob size does not match its data, or its last name is not NUL-terminated.");
			}

			Expect (",");
			Expect ("align");
			if (!UInt64.TryParse (ReadToken (), NumberStyles.None, CultureInfo.InvariantCulture, out ulong alignment) ||
				alignment == 0 || (alignment & (alignment - 1)) != 0) {
				throw Invalid ("Invalid Java type-name blob alignment.");
			}
			while (input.Peek () == ' ' || input.Peek () == '\t' || input.Peek () == '\r') {
				ReadChar ();
			}
			if (input.Peek () == ';') {
				SkipLine ();
			} else if (input.Peek () != '\n' && input.Peek () != -1) {
				throw Invalid ("Unexpected content after the Java type-name blob.");
			}
		}

		void ReadByteString ()
		{
			ReadChar (); // c
			if (ReadChar () != '"') {
				throw Invalid ("Expected a quoted LLVM byte string.");
			}

			int c;
			while ((c = ReadChar ()) != '"') {
				if (c == '\\') {
					int high = HexValue (ReadChar ());
					int low = HexValue (ReadChar ());
					if (high < 0 || low < 0) {
						throw Invalid ("Invalid hexadecimal escape in the Java type-name blob.");
					}
					AddByte ((byte)((high << 4) | low));
				} else {
					if (c < 32 || c >= 127) {
						throw Invalid ("Invalid or unterminated LLVM byte string.");
					}
					AddByte ((byte)c);
				}
			}
		}

		void ReadByteArray ()
		{
			Expect ("[");
			if (SkipTrivia () == ']') {
				ReadChar ();
				return;
			}
			while (true) {
				Expect ("i8");
				string value = ReadToken ();
				byte b;
				if (value.StartsWith ("u0x", StringComparison.Ordinal)) {
					if (value.Length != 5 || !Byte.TryParse (value.Substring (3), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out b)) {
						throw Invalid ("Invalid hexadecimal i8 value in the Java type-name blob.");
					}
				} else if (!Byte.TryParse (value, NumberStyles.None, CultureInfo.InvariantCulture, out b)) {
					throw Invalid ("Invalid i8 value in the Java type-name blob.");
				}
				AddByte (b);

				string separator = ReadToken ();
				if (separator == "]") {
					return;
				}
				if (separator != ",") {
					throw Invalid ("Expected a comma or the end of the Java type-name byte array.");
				}
			}
		}

		void AddByte (byte b)
		{
			if (bytesRead >= expectedSize) {
				throw Invalid ("Java type-name blob contains more bytes than its declared size.");
			}
			bytesRead++;
			if (b != 0) {
				nameBytes.Add (b);
				return;
			}
			string name = Utf8.GetString (nameBytes.ToArray ());
			nameBytes.Clear ();
			ValidateName (name);
			keys.Add (name);
		}

		void ValidateName (string name)
		{
			if (name.Length == 0 || name [0] == '/' || name [name.Length - 1] == '/' || name.Contains ("//")) {
				throw Invalid ("An empty Java class name or package segment was found.");
			}
			foreach (char c in name) {
				if (Char.IsWhiteSpace (c) || Char.IsControl (c) || ".;[]\\\"'*!?:,{}()#@<>%".IndexOf (c) >= 0) {
					throw Invalid ("A noncanonical Java class name was found in the blob.");
				}
			}
		}

		void Expect (string expected)
		{
			if (ReadToken () != expected) {
				throw Invalid ($"Expected '{expected}' in the Java type-name blob declaration.");
			}
		}

		string ReadToken ()
		{
			int c = SkipTrivia ();
			if (c < 0) {
				throw Invalid ("Unexpected end of LLVM IR.");
			}
			if (IsPunctuation (c)) {
				return ((char)ReadChar ()).ToString ();
			}
			var token = new StringBuilder ();
			while (c >= 0 && !Char.IsWhiteSpace ((char)c) && c != ';' && !IsPunctuation (c)) {
				token.Append ((char)ReadChar ());
				c = input.Peek ();
			}
			return token.ToString ();
		}

		static bool IsPunctuation (int c) => c == '[' || c == ']' || c == '=' || c == ',';

		int SkipTrivia ()
		{
			int c;
			while ((c = input.Peek ()) >= 0) {
				if (Char.IsWhiteSpace ((char)c) || c == '\uFEFF') {
					ReadChar ();
				} else if (c == ';') {
					SkipLine ();
				} else {
					break;
				}
			}
			return c;
		}

		void SkipLine ()
		{
			int c;
			do {
				c = ReadChar ();
			} while (c >= 0 && c != '\n');
		}

		int ReadChar ()
		{
			int c = input.Read ();
			if (c == '\n') {
				line++;
			}
			return c;
		}

		static int HexValue (int c)
		{
			if (c >= '0' && c <= '9') {
				return c - '0';
			}
			if (c >= 'a' && c <= 'f') {
				return c - 'a' + 10;
			}
			return c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
		}

		InvalidDataException Invalid (string message) => new InvalidDataException ($"Line {line}: {message}");
	}
}
