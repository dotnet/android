#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks.JniRemapping
{
	interface IJniNameMapping
	{
		bool TryMapClass (string className, out string mappedClassName);
		bool TryMapField (string owningClassName, string fieldName, out string mappedFieldName);
		bool TryMapMethod (string owningClassName, string methodName, IReadOnlyList<string> javaParameterTypes, string javaReturnType, out string mappedMethodName);
		bool TryMapMethodByNameOnly (string owningClassName, string methodName, out string mappedMethodName);
	}

	/// <summary>
	/// A parsed R8/ProGuard <c>mapping.txt</c> file, exposing the class, field, and method
	/// renames it describes using JNI-style ('/'-separated, '$' for nested classes) names.
	/// </summary>
	sealed class R8Mapping : IJniNameMapping
	{
		// Original JNI class name -> obfuscated JNI class name.
		readonly Dictionary<string, string> classes = new Dictionary<string, string> (StringComparer.Ordinal);

		// Obfuscated JNI class name -> original JNI class name.
		readonly Dictionary<string, string> originalClasses = new Dictionary<string, string> (StringComparer.Ordinal);

		// Original JNI class name -> (original field name -> obfuscated field name).
		readonly Dictionary<string, Dictionary<string, string>> fields = new Dictionary<string, Dictionary<string, string>> (StringComparer.Ordinal);

		// Original JNI class name -> ("name(javaParam,javaParam,...):javaReturn" -> obfuscated method name).
		readonly Dictionary<string, Dictionary<string, string>> methods = new Dictionary<string, Dictionary<string, string>> (StringComparer.Ordinal);

		// Reverse member indexes are scoped by original class. Fields retain every candidate because
		// R8 may reuse an obfuscated field name for fields with different JVM descriptors.
		readonly Dictionary<string, Dictionary<string, List<string>>> originalFields = new Dictionary<string, Dictionary<string, List<string>>> (StringComparer.Ordinal);
		readonly Dictionary<string, Dictionary<string, string>> originalMethods = new Dictionary<string, Dictionary<string, string>> (StringComparer.Ordinal);
		readonly HashSet<string> accessedEntries = new HashSet<string> (StringComparer.Ordinal);
		HashSet<string>? allowedReverseEntries;

		public IEnumerable<string> AccessedEntries => accessedEntries;

		bool IJniNameMapping.TryMapClass (string className, out string mappedClassName)
			=> TryGetRenamedClass (className, out mappedClassName);

		bool IJniNameMapping.TryMapField (string owningClassName, string fieldName, out string mappedFieldName)
			=> TryGetRenamedField (owningClassName, fieldName, out mappedFieldName);

		bool IJniNameMapping.TryMapMethod (string owningClassName, string methodName, IReadOnlyList<string> javaParameterTypes, string javaReturnType, out string mappedMethodName)
			=> TryGetRenamedMethod (owningClassName, methodName, javaParameterTypes, javaReturnType, out mappedMethodName);

		bool IJniNameMapping.TryMapMethodByNameOnly (string owningClassName, string methodName, out string mappedMethodName)
			=> TryGetRenamedMethodByNameOnly (owningClassName, methodName, out mappedMethodName);

		internal IJniNameMapping CreateReverseMapping () => new ReverseR8Mapping (this);

		internal void RestrictReverseLookupsTo (IEnumerable<string> manifestEntries)
			=> allowedReverseEntries = new HashSet<string> (manifestEntries, StringComparer.Ordinal);

		bool IsReverseEntryAllowed (string entry)
			=> allowedReverseEntries == null || allowedReverseEntries.Contains (entry);

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
					string currentObfuscatedClass = JavaNameToJni (obfuscatedClass);
					mapping.classes [currentOriginalClass] = currentObfuscatedClass;
					mapping.originalClasses [currentObfuscatedClass] = currentOriginalClass;
					continue;
				}

				if (currentOriginalClass == null) {
					throw new FormatException ($"mapping.txt:{lineNumber}: member mapping line found before any class mapping line: '{line}'.");
				}

				if (!TryParseMemberLine (trimmed, out string memberName, out string []? javaParameterTypes, out string? javaReturnType, out string obfuscatedName)) {
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
					string key = BuildMethodKey (memberName, javaParameterTypes, javaReturnType ?? "");
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

			mapping.BuildReverseMemberIndexes ();
			return mapping;
		}

		void BuildReverseMemberIndexes ()
		{
			foreach (var classEntry in fields) {
				var reverse = new Dictionary<string, List<string>> (StringComparer.Ordinal);
				originalFields [classEntry.Key] = reverse;
				foreach (var field in classEntry.Value) {
					if (!reverse.TryGetValue (field.Value, out var originalNames)) {
						reverse [field.Value] = originalNames = new List<string> ();
					}
					originalNames.Add (field.Key);
				}
			}

			foreach (var classEntry in methods) {
				var reverseMethods = new Dictionary<string, string> (StringComparer.Ordinal);
				originalMethods [classEntry.Key] = reverseMethods;
				foreach (var method in classEntry.Value) {
					if (method.Value.Length == 0) {
						continue;
					}
					int parameterStart = method.Key.IndexOf ('(');
					string originalName = parameterStart < 0 ? method.Key : method.Key.Substring (0, parameterStart);
					string signature = parameterStart < 0 ? "()" : method.Key.Substring (parameterStart);
					AddUnambiguousReverseEntry (reverseMethods, method.Value + signature, originalName);
				}
			}
		}

		static void AddUnambiguousReverseEntry (Dictionary<string, string> entries, string key, string value)
		{
			if (entries.TryGetValue (key, out string? existing) && !String.Equals (existing, value, StringComparison.Ordinal)) {
				entries [key] = "";
			} else if (existing == null) {
				entries [key] = value;
			}
		}

		/// <summary>
		/// Builds the lookup key used for methods: the JNI/Java member name plus its
		/// parameter types (in Java source form, e.g. "int", "android.os.Bundle", "java.lang.String[]").
		/// </summary>
		internal static string BuildMethodKey (string javaMethodName, IReadOnlyList<string> javaParameterTypes, string javaReturnType)
			=> javaMethodName + "(" + string.Join (",", javaParameterTypes) + "):" + javaReturnType;

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
				accessedEntries.Add (BuildClassEntry (originalJniClassName));
				return true;
			}
			obfuscatedJniClassName = "";
			return false;
		}

		public bool TryGetOriginalClass (string obfuscatedJniClassName, out string originalJniClassName)
		{
			if (originalClasses.TryGetValue (obfuscatedJniClassName, out string? original)) {
				originalJniClassName = original;
				return true;
			}
			originalJniClassName = "";
			return false;
		}

		public IEnumerable<string> GetOriginalMethodNames (string originalJniClassName, string obfuscatedMethodName)
		{
			if (!methods.TryGetValue (originalJniClassName, out var classMethods)) {
				yield break;
			}
			var seen = new HashSet<string> (StringComparer.Ordinal);
			foreach (var entry in classMethods) {
				if (!String.Equals (entry.Value, obfuscatedMethodName, StringComparison.Ordinal)) {
					continue;
				}
				int parameters = entry.Key.IndexOf ('(');
				string name = parameters < 0 ? entry.Key : entry.Key.Substring (0, parameters);
				if (seen.Add (name)) {
					yield return name;
				}
			}
		}

		public bool TryGetOriginalMethodName (string originalJniClassName, string obfuscatedMethodName, IReadOnlyList<string> originalJavaParameterTypes, string originalJavaReturnType, out string originalMethodName)
		{
			originalMethodName = "";
			return originalMethods.TryGetValue (originalJniClassName, out var classMethods) &&
				classMethods.TryGetValue (BuildMethodKey (obfuscatedMethodName, originalJavaParameterTypes, originalJavaReturnType), out originalMethodName) &&
				originalMethodName.Length != 0;
		}

		public bool TryGetOriginalFieldName (string originalJniClassName, string obfuscatedFieldName, out string originalFieldName)
		{
			originalFieldName = "";
			return originalFields.TryGetValue (originalJniClassName, out var classFields) &&
				classFields.TryGetValue (obfuscatedFieldName, out var originalNames) &&
				originalNames.Count == 1 &&
				(originalFieldName = originalNames [0]).Length != 0;
		}

		public bool TryGetRenamedField (string owningJniClassName, string originalFieldName, out string obfuscatedFieldName)
		{
			obfuscatedFieldName = "";
			if (!fields.TryGetValue (owningJniClassName, out var classFields) ||
					!classFields.TryGetValue (originalFieldName, out string? renamed)) {
				return false;
			}
			obfuscatedFieldName = renamed;
			accessedEntries.Add (BuildFieldEntry (owningJniClassName, originalFieldName));
			return true;
		}

		public bool TryGetRenamedMethod (string owningJniClassName, string javaMethodName, IReadOnlyList<string> javaParameterTypes, string javaReturnType, out string obfuscatedMethodName)
		{
			obfuscatedMethodName = "";
			if (!methods.TryGetValue (owningJniClassName, out var classMethods)) {
				return false;
			}
			string methodKey = BuildMethodKey (javaMethodName, javaParameterTypes, javaReturnType);
			if (!classMethods.TryGetValue (methodKey, out string? renamed) || renamed.Length == 0) {
				return false;
			}
			obfuscatedMethodName = renamed;
			accessedEntries.Add (BuildMethodEntry (owningJniClassName, methodKey));
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

			foreach (var kvp in classMethods) {
				if (kvp.Key.StartsWith (prefix, StringComparison.Ordinal) && kvp.Value == match) {
					accessedEntries.Add (BuildMethodEntry (owningJniClassName, kvp.Key));
				}
			}
			obfuscatedMethodName = match;
			return true;
		}

		/// <summary>
		/// Reports required mappings that are present in both this seed mapping and
		/// <paramref name="finalMapping"/>, but whose obfuscated names differ. Entries absent from the
		/// final mapping are intentionally ignored because ILLink, ILC, or final R8 shrinking may have
		/// removed them.
		/// </summary>
		public IEnumerable<string> GetCompatibilityConflicts (R8Mapping finalMapping, IEnumerable<string> requiredEntries)
		{
			foreach (string requiredEntry in requiredEntries) {
				string [] parts = requiredEntry.Split ('\t');
				switch (parts.Length > 0 ? parts [0] : "") {
				case "C" when parts.Length == 2:
					if (classes.TryGetValue (parts [1], out string? seedClassName) &&
							finalMapping.classes.TryGetValue (parts [1], out string? finalClassName) &&
							!IsRemovedClassName (finalClassName) &&
							!String.Equals (seedClassName, finalClassName, StringComparison.Ordinal)) {
						yield return $"class '{parts [1]}': seed name '{seedClassName}', final name '{finalClassName}'";
					}
					break;
				case "F" when parts.Length == 3:
					if (!finalMapping.IsRemovedClass (parts [1]) &&
							fields.TryGetValue (parts [1], out var seedFields) &&
							seedFields.TryGetValue (parts [2], out string? seedFieldName) &&
							finalMapping.fields.TryGetValue (parts [1], out var finalFields) &&
							finalFields.TryGetValue (parts [2], out string? finalFieldName) &&
							!String.Equals (seedFieldName, finalFieldName, StringComparison.Ordinal)) {
						yield return $"field '{parts [1]}.{parts [2]}': seed name '{seedFieldName}', final name '{finalFieldName}'";
					}
					break;
				case "M" when parts.Length == 3:
					if (!finalMapping.IsRemovedClass (parts [1]) &&
							methods.TryGetValue (parts [1], out var seedMethods) &&
							seedMethods.TryGetValue (parts [2], out string? seedMethodName) &&
							seedMethodName.Length != 0 &&
							finalMapping.methods.TryGetValue (parts [1], out var finalMethods) &&
							finalMethods.TryGetValue (parts [2], out string? finalMethodName) &&
							finalMethodName.Length != 0 &&
							!String.Equals (seedMethodName, finalMethodName, StringComparison.Ordinal)) {
						yield return $"method '{parts [1]}.{parts [2]}': seed name '{seedMethodName}', final name '{finalMethodName}'";
					}
					break;
				default:
					throw new FormatException ($"Invalid R8 JNI rewrite manifest entry '{requiredEntry}'.");
				}
			}
		}

		/// <summary>
		/// Reports entries that post-link analysis says must remain reachable but which final R8
		/// removed. A required member is only reported separately when its declaring class survived.
		/// </summary>
		public IEnumerable<string> GetReachabilityConflicts (R8Mapping finalMapping, IEnumerable<string> requiredEntries)
		{
			foreach (string requiredEntry in requiredEntries) {
				string [] parts = requiredEntry.Split ('\t');
				switch (parts.Length > 0 ? parts [0] : "") {
				case "C" when parts.Length == 2:
					if (!classes.ContainsKey (parts [1])) {
						throw new FormatException ($"R8 reachability manifest class '{parts [1]}' is absent from the seed mapping.");
					}
					if (!finalMapping.classes.TryGetValue (parts [1], out string? finalClassName) || IsRemovedClassName (finalClassName)) {
						yield return $"class '{parts [1]}'";
					}
					break;
				case "F" when parts.Length == 3:
					if (!fields.TryGetValue (parts [1], out var seedFields) || !seedFields.ContainsKey (parts [2])) {
						throw new FormatException ($"R8 reachability manifest field '{parts [1]}.{parts [2]}' is absent from the seed mapping.");
					}
					if (!finalMapping.IsLiveClass (parts [1])) {
						continue;
					}
					if (!finalMapping.fields.TryGetValue (parts [1], out var finalFields) || !finalFields.ContainsKey (parts [2])) {
						yield return $"field '{parts [1]}.{parts [2]}'";
					}
					break;
				case "M" when parts.Length == 3:
					if (!methods.TryGetValue (parts [1], out var seedMethods) || !seedMethods.ContainsKey (parts [2])) {
						throw new FormatException ($"R8 reachability manifest method '{parts [1]}.{parts [2]}' is absent from the seed mapping.");
					}
					if (!finalMapping.IsLiveClass (parts [1])) {
						continue;
					}
					if (!finalMapping.methods.TryGetValue (parts [1], out var finalMethods) ||
							!finalMethods.TryGetValue (parts [2], out string? finalMethodName) ||
							finalMethodName.Length == 0) {
						yield return $"method '{parts [1]}.{parts [2]}'";
					}
					break;
				default:
					throw new FormatException ($"Invalid R8 JNI reachability manifest entry '{requiredEntry}'.");
				}
			}
		}

		internal static string BuildClassEntry (string className) => $"C\t{className}";
		internal static string BuildFieldEntry (string className, string fieldName) => $"F\t{className}\t{fieldName}";
		internal static string BuildMethodEntry (string className, string methodKey) => $"M\t{className}\t{methodKey}";

		internal static string CreateManifestContent (IEnumerable<string> entries)
		{
			var sortedEntries = new List<string> (entries);
			sortedEntries.Sort (StringComparer.Ordinal);
			using var writer = new StringWriter {
				NewLine = "\n",
			};
			foreach (string entry in sortedEntries) {
				writer.WriteLine (entry);
			}
			return writer.ToString ();
		}

		sealed class ReverseR8Mapping : IJniNameMapping
		{
			readonly R8Mapping mapping;

			public ReverseR8Mapping (R8Mapping mapping)
			{
				this.mapping = mapping;
			}

			public bool TryMapClass (string className, out string mappedClassName)
			{
				if (!TryGetAllowedOriginalClass (className, out mappedClassName)) {
					return false;
				}
				mapping.accessedEntries.Add (BuildClassEntry (mappedClassName));
				return true;
			}

			public bool TryMapField (string owningClassName, string fieldName, out string mappedFieldName)
			{
				mappedFieldName = "";
				if (!TryGetAllowedOriginalClass (owningClassName, out string originalClassName) ||
						!mapping.originalFields.TryGetValue (originalClassName, out var classFields) ||
						!classFields.TryGetValue (fieldName, out var originalNames) ||
						originalNames.Count == 0) {
					return false;
				}
				string? firstOriginalName = null;
				string? allowedEntry = null;
				foreach (string originalName in originalNames) {
					string entry = BuildFieldEntry (originalClassName, originalName);
					if (!mapping.IsReverseEntryAllowed (entry)) {
						continue;
					}
					if (firstOriginalName != null) {
						return false;
					}
					firstOriginalName = originalName;
					allowedEntry = entry;
				}
				if (firstOriginalName == null || allowedEntry == null) {
					return false;
				}
				mapping.accessedEntries.Add (BuildClassEntry (originalClassName));
				mapping.accessedEntries.Add (allowedEntry);
				mappedFieldName = firstOriginalName;
				return true;
			}

			public bool TryMapMethod (string owningClassName, string methodName, IReadOnlyList<string> javaParameterTypes, string javaReturnType, out string mappedMethodName)
			{
				mappedMethodName = "";
				if (!TryGetAllowedOriginalClass (owningClassName, out string originalClassName)) {
					return false;
				}

				var originalParameterTypes = new List<string> (javaParameterTypes.Count);
				foreach (string parameterType in javaParameterTypes) {
					originalParameterTypes.Add (GetOriginalJavaType (parameterType));
				}
				string originalReturnType = GetOriginalJavaType (javaReturnType);
				if (!mapping.TryGetOriginalMethodName (originalClassName, methodName, originalParameterTypes, originalReturnType, out mappedMethodName)) {
					return false;
				}
				string entry = BuildMethodEntry (
					originalClassName,
					BuildMethodKey (mappedMethodName, originalParameterTypes, originalReturnType));
				if (!mapping.IsReverseEntryAllowed (entry)) {
					mappedMethodName = "";
					return false;
				}
				mapping.accessedEntries.Add (BuildClassEntry (originalClassName));
				mapping.accessedEntries.Add (entry);
				return true;
			}

			public bool TryMapMethodByNameOnly (string owningClassName, string methodName, out string mappedMethodName)
			{
				mappedMethodName = "";
				if (!TryGetAllowedOriginalClass (owningClassName, out string originalClassName) ||
						!mapping.methods.TryGetValue (originalClassName, out var classMethods)) {
					return false;
				}

				string? firstOriginalName = null;
				var allowedEntries = new List<string> ();
				foreach (var entry in classMethods) {
					if (!String.Equals (entry.Value, methodName, StringComparison.Ordinal)) {
						continue;
					}
					string manifestEntry = BuildMethodEntry (originalClassName, entry.Key);
					if (!mapping.IsReverseEntryAllowed (manifestEntry)) {
						continue;
					}
					int parameterStart = entry.Key.IndexOf ('(');
					string originalName = parameterStart < 0 ? entry.Key : entry.Key.Substring (0, parameterStart);
					if (firstOriginalName != null && !String.Equals (firstOriginalName, originalName, StringComparison.Ordinal)) {
						return false;
					}
					firstOriginalName = originalName;
					allowedEntries.Add (manifestEntry);
				}
				if (firstOriginalName == null) {
					return false;
				}
				mapping.accessedEntries.Add (BuildClassEntry (originalClassName));
				foreach (string entry in allowedEntries) {
					mapping.accessedEntries.Add (entry);
				}
				mappedMethodName = firstOriginalName;
				return true;
			}

			string GetOriginalJavaType (string javaType)
			{
				int suffixStart = javaType.IndexOf ('[');
				string suffix = suffixStart < 0 ? "" : javaType.Substring (suffixStart);
				string elementType = suffixStart < 0 ? javaType : javaType.Substring (0, suffixStart);
				string jniType = JavaNameToJni (elementType);
				return TryGetAllowedOriginalClass (jniType, out string originalJniType)
					? originalJniType.Replace ('/', '.') + suffix
					: javaType;
			}

			bool TryGetAllowedOriginalClass (string obfuscatedJniClassName, out string originalJniClassName)
			{
				if (!mapping.TryGetOriginalClass (obfuscatedJniClassName, out originalJniClassName) ||
						!mapping.IsReverseEntryAllowed (BuildClassEntry (originalJniClassName))) {
					originalJniClassName = "";
					return false;
				}
				return true;
			}
		}

		static bool IsRemovedClassName (string className)
			=> className.StartsWith ("R8$$REMOVED$$CLASS$$", StringComparison.Ordinal);

		bool IsRemovedClass (string originalClassName)
			=> classes.TryGetValue (originalClassName, out string? className) && IsRemovedClassName (className);

		bool IsLiveClass (string originalClassName)
			=> classes.TryGetValue (originalClassName, out string? className) && !IsRemovedClassName (className);

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

		static bool TryParseMemberLine (string trimmed, out string name, out string []? javaParameterTypes, out string? javaReturnType, out string obfuscatedName)
		{
			name = "";
			javaParameterTypes = null;
			javaReturnType = null;
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
				javaReturnType = beforeParen.Substring (0, lastSpace);
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
				javaReturnType = null;
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
