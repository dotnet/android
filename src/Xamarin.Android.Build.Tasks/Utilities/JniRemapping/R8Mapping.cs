#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// A parsed R8/ProGuard <c>mapping.txt</c> file, exposing the class, field, and method
	/// renames it describes using JNI-style ('/'-separated, '$' for nested classes) names.
	/// </summary>
	sealed class R8Mapping
	{
		// Original JNI class name -> obfuscated JNI class name.
		readonly Dictionary<string, string> classes = new Dictionary<string, string> (StringComparer.Ordinal);

		// Original JNI class name -> (original field name -> obfuscated field name).
		readonly Dictionary<string, Dictionary<string, string>> fields = new Dictionary<string, Dictionary<string, string>> (StringComparer.Ordinal);

		// Original JNI class name -> ("name(javaParam,javaParam,...)" -> obfuscated method name).
		readonly Dictionary<string, Dictionary<string, string>> methods = new Dictionary<string, Dictionary<string, string>> (StringComparer.Ordinal);

		public static R8Mapping Load (string path)
		{
			using var reader = new StreamReader (path);
			return Parse (reader);
		}

		public static R8Mapping Parse (TextReader reader)
		{
			var mapping = new R8Mapping ();
			string? currentOriginalClass = null;
			int lineNumber = 0;
			string? line;

			while ((line = reader.ReadLine ()) != null) {
				lineNumber++;

				if (line.Length == 0) {
					continue;
				}

				bool indented = line [0] == ' ' || line [0] == '\t';
				string trimmed = line.Trim ();
				if (trimmed.Length == 0 || trimmed [0] == '#') {
					// R8 emits indented "# {...}" metadata comments (e.g. inline source position
					// info) under a member line; these are not member mappings.
					continue;
				}

				if (!indented) {
					if (!TryParseClassLine (trimmed, out string originalClass, out string obfuscatedClass)) {
						throw new FormatException ($"mapping.txt:{lineNumber}: expected a class mapping line ('original -> obfuscated:'), got '{line}'.");
					}

					currentOriginalClass = JavaNameToJni (originalClass);
					mapping.classes [currentOriginalClass] = JavaNameToJni (obfuscatedClass);
					continue;
				}

				if (currentOriginalClass == null) {
					throw new FormatException ($"mapping.txt:{lineNumber}: member mapping line found before any class mapping line: '{line}'.");
				}

				if (!TryParseMemberLine (trimmed, out string memberName, out string []? javaParameterTypes, out string obfuscatedName)) {
					throw new FormatException ($"mapping.txt:{lineNumber}: could not parse member mapping line: '{line}'.");
				}

				if (javaParameterTypes == null) {
					// Field.
					if (!mapping.fields.TryGetValue (currentOriginalClass, out var classFields)) {
						mapping.fields [currentOriginalClass] = classFields = new Dictionary<string, string> (StringComparer.Ordinal);
					}
					classFields [memberName] = obfuscatedName;
				} else {
					// R8 emits fully-qualified source methods as inline call-frame records beneath
					// the destination method. They are retrace metadata, not member mappings for
					// the current class, and one source method may appear under many destinations.
					if (memberName.IndexOf ('.') >= 0) {
						continue;
					}

					// Method.
					string key = BuildMethodKey (memberName, javaParameterTypes);
					if (!mapping.methods.TryGetValue (currentOriginalClass, out var classMethods)) {
						mapping.methods [currentOriginalClass] = classMethods = new Dictionary<string, string> (StringComparer.Ordinal);
					}
					if (classMethods.TryGetValue (key, out string? existing) && existing != obfuscatedName) {
						// An optimized method can be inlined into several surviving methods. R8
						// then emits one retrace record per destination, so there is no single
						// runtime name to use for this source member.
						classMethods [key] = "";
						continue;
					}
					if (existing == null) {
						classMethods [key] = obfuscatedName;
					}
				}
			}

			return mapping;
		}

		/// <summary>
		/// Builds the lookup key used for methods: the JNI/Java member name plus its
		/// parameter types (in Java source form, e.g. "int", "android.os.Bundle", "java.lang.String[]").
		/// </summary>
		internal static string BuildMethodKey (string javaMethodName, IReadOnlyList<string> javaParameterTypes)
			=> javaMethodName + "(" + string.Join (",", javaParameterTypes) + ")";

		/// <summary>
		/// Translates a JNI member name (as it appears in a RegisterAttribute / encoded JniPeerMembers
		/// string) to the corresponding name used in a mapping.txt member line.
		/// </summary>
		internal static string JniMemberNameToMappingName (string jniMemberName)
			=> jniMemberName switch {
				".ctor" => "<init>",
				".cctor" => "<clinit>",
				_ => jniMemberName,
			};

		public bool TryGetRenamedClass (string originalJniClassName, out string obfuscatedJniClassName)
		{
			if (classes.TryGetValue (originalJniClassName, out string? renamed)) {
				obfuscatedJniClassName = renamed;
				return true;
			}
			obfuscatedJniClassName = "";
			return false;
		}

		public bool TryGetRenamedField (string owningJniClassName, string originalFieldName, out string obfuscatedFieldName)
		{
			obfuscatedFieldName = "";
			if (!fields.TryGetValue (owningJniClassName, out var classFields) ||
					!classFields.TryGetValue (originalFieldName, out string? renamed)) {
				return false;
			}
			obfuscatedFieldName = renamed;
			return true;
		}

		public bool TryGetRenamedMethod (string owningJniClassName, string javaMethodName, IReadOnlyList<string> javaParameterTypes, out string obfuscatedMethodName)
		{
			obfuscatedMethodName = "";
			if (!methods.TryGetValue (owningJniClassName, out var classMethods)) {
				return false;
			}
			if (!classMethods.TryGetValue (BuildMethodKey (javaMethodName, javaParameterTypes), out string? renamed) || renamed.Length == 0) {
				return false;
			}
			obfuscatedMethodName = renamed;
			return true;
		}

		/// <summary>
		/// Best-effort lookup used when only a member name (no parameter types) is available:
		/// succeeds only if the name is unambiguous (a single overload) within the class.
		/// </summary>
		public bool TryGetRenamedMethodByNameOnly (string owningJniClassName, string javaMethodName, out string obfuscatedMethodName)
		{
			obfuscatedMethodName = "";
			if (!methods.TryGetValue (owningJniClassName, out var classMethods)) {
				return false;
			}

			string? match = null;
			string prefix = javaMethodName + "(";
			foreach (var kvp in classMethods) {
				if (!kvp.Key.StartsWith (prefix, StringComparison.Ordinal)) {
					continue;
				}
				if (kvp.Value.Length == 0) {
					return false;
				}
				if (match != null && match != kvp.Value) {
					return false; // Ambiguous - multiple differently-renamed overloads.
				}
				match = kvp.Value;
			}

			if (match == null) {
				return false;
			}

			obfuscatedMethodName = match;
			return true;
		}

		static string JavaNameToJni (string javaBinaryName) => javaBinaryName.Replace ('.', '/');

		static bool TryParseClassLine (string trimmed, out string originalClass, out string obfuscatedClass)
		{
			originalClass = "";
			obfuscatedClass = "";

			if (!trimmed.EndsWith (":", StringComparison.Ordinal)) {
				return false;
			}

			const string arrow = " -> ";
			int arrowIndex = trimmed.IndexOf (arrow, StringComparison.Ordinal);
			if (arrowIndex < 0) {
				return false;
			}

			originalClass = trimmed.Substring (0, arrowIndex);
			obfuscatedClass = trimmed.Substring (arrowIndex + arrow.Length, trimmed.Length - arrowIndex - arrow.Length - 1);
			return originalClass.Length > 0 && obfuscatedClass.Length > 0;
		}

		static bool TryParseMemberLine (string trimmed, out string name, out string []? javaParameterTypes, out string obfuscatedName)
		{
			name = "";
			javaParameterTypes = null;
			obfuscatedName = "";

			const string arrow = " -> ";
			int arrowIndex = trimmed.LastIndexOf (arrow, StringComparison.Ordinal);
			if (arrowIndex < 0) {
				return false;
			}

			string left = trimmed.Substring (0, arrowIndex);
			obfuscatedName = trimmed.Substring (arrowIndex + arrow.Length).Trim ();
			if (obfuscatedName.Length == 0) {
				return false;
			}

			left = StripLeadingLineRange (left);
			left = StripTrailingLineRange (left);

			int parenOpen = left.IndexOf ('(');
			if (parenOpen >= 0 && left.EndsWith (")", StringComparison.Ordinal)) {
				string beforeParen = left.Substring (0, parenOpen);
				string paramList = left.Substring (parenOpen + 1, left.Length - parenOpen - 2);

				int lastSpace = beforeParen.LastIndexOf (' ');
				if (lastSpace < 0) {
					return false;
				}

				name = beforeParen.Substring (lastSpace + 1);
				javaParameterTypes = paramList.Length == 0
					? Array.Empty<string> ()
					: paramList.Split (',');
				return name.Length > 0;
			} else {
				int lastSpace = left.LastIndexOf (' ');
				if (lastSpace < 0) {
					return false;
				}

				name = left.Substring (lastSpace + 1);
				javaParameterTypes = null;
				return name.Length > 0;
			}
		}

		/// <summary>
		/// Strips a leading "startLine:endLine:" prefix used on some method mapping lines, e.g.
		/// "4:10:void onCreate(...)" -&gt; "void onCreate(...)".
		/// </summary>
		static string StripLeadingLineRange (string s)
		{
			int i = 0;
			while (i < s.Length && char.IsDigit (s [i])) {
				i++;
			}
			if (i == 0 || i >= s.Length || s [i] != ':') {
				return s;
			}

			int secondStart = i + 1;
			int j = secondStart;
			while (j < s.Length && char.IsDigit (s [j])) {
				j++;
			}
			if (j == secondStart || j >= s.Length || s [j] != ':') {
				return s;
			}

			return s.Substring (j + 1);
		}

		/// <summary>
		/// Strips a trailing ":originalStartLine[:originalEndLine]" suffix used on some method
		/// mapping lines, e.g. "void onCreate(...):23:29" -&gt; "void onCreate(...)" (two original
		/// line numbers) and "void run(...):2" -&gt; "void run(...)" (a single original line number,
		/// emitted when the original range collapses to one line).
		/// </summary>
		static string StripTrailingLineRange (string s)
		{
			if (s.Length == 0 || !char.IsDigit (s [s.Length - 1])) {
				return s;
			}

			int i = s.Length - 1;
			while (i >= 0 && char.IsDigit (s [i])) {
				i--;
			}
			if (i < 0 || s [i] != ':') {
				return s;
			}

			int lastColon = i;

			// Look for a second, earlier number - ":originalStartLine:originalEndLine".
			int j = i - 1;
			while (j >= 0 && char.IsDigit (s [j])) {
				j--;
			}
			if (j >= 0 && j != i - 1 && s [j] == ':') {
				return s.Substring (0, j);
			}

			// Only one trailing number - ":originalStartLine".
			return s.Substring (0, lastColon);
		}
	}
}
