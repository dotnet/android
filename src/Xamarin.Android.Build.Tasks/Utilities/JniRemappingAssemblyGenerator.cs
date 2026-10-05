#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	sealed class JniRemappingTypeReplacement
	{
		public string From { get; }
		public string To { get; }

		public JniRemappingTypeReplacement (string from, string to)
		{
			From = from;
			To = to;
		}
	}

	sealed class JniRemappingMethodReplacement
	{
		public string SourceType { get; }
		public string SourceMethod { get; }
		public string? SourceMethodSignature { get; }
		public string TargetType { get; }
		public string TargetMethod { get; }
		public string? TargetMethodSignature { get; }
		public bool TargetIsStatic { get; }

		public JniRemappingMethodReplacement (string sourceType, string sourceMethod, string? sourceMethodSignature,
		                                      string targetType, string targetMethod, string? targetMethodSignature, bool targetIsStatic)
		{
			SourceType = sourceType;
			SourceMethod = sourceMethod;
			SourceMethodSignature = sourceMethodSignature;
			TargetType = targetType;
			TargetMethod = targetMethod;
			TargetMethodSignature = targetMethodSignature;
			TargetIsStatic = targetIsStatic;
		}
	}

	sealed class JniRemappingFieldReplacement
	{
		public string SourceType { get; }
		public string SourceField { get; }
		public string? SourceFieldSignature { get; }
		public string TargetType { get; }
		public string TargetField { get; }
		public string? TargetFieldSignature { get; }

		public JniRemappingFieldReplacement (string sourceType, string sourceField, string? sourceFieldSignature,
		                                     string targetType, string targetField, string? targetFieldSignature)
		{
			SourceType = sourceType;
			SourceField = sourceField;
			SourceFieldSignature = sourceFieldSignature;
			TargetType = targetType;
			TargetField = targetField;
			TargetFieldSignature = targetFieldSignature;
		}
	}

	class JniRemappingAssemblyGenerator
	{
		const string TypeReplacementsVariableName = "jni_remapping_type_replacements";
		const string ReverseTypeReplacementsVariableName = "jni_remapping_reverse_type_replacements";
		const string MethodReplacementIndexVariableName = "jni_remapping_method_replacement_index";
		const string FieldReplacementIndexVariableName = "jni_remapping_field_replacement_index";

		// Structures must match src/native/clr/include/xamarin-app.hh and
		// src/native/common/include/managed-interface.hh. Sizes exclude pointer fields;
		// see LlvmIrTarget.GetAggregateAlignment.
		const ulong MethodEntryDataSize = 9;
		const ulong TypeEntryDataSize = 8;
		const ulong FieldEntryDataSize = 8;
		const ulong RemappingDataSize = 16;

		sealed class JniRemappingString
		{
			public string? Value { get; }
			public uint Length { get; }

			public JniRemappingString (string? value)
			{
				Value = value;
				Length = (uint)Utf8 (value).Length;
			}
		}

		sealed class MethodType
		{
			public string Name { get; }
			public string Symbol { get; }
			public List<JniRemappingMethodReplacement> Methods { get; }

			public MethodType (string name, int index, List<JniRemappingMethodReplacement> methods)
			{
				Name = name;
				Symbol = $"mm_{index}";
				Methods = methods;
			}
		}

		sealed class FieldType
		{
			public string Name { get; }
			public string Symbol { get; }
			public List<JniRemappingFieldReplacement> Fields { get; }

			public FieldType (string name, int index, List<JniRemappingFieldReplacement> fields)
			{
				Name = name;
				Symbol = $"mf_{index}";
				Fields = fields;
			}
		}

		readonly List<JniRemappingTypeReplacement>? typeReplacements;
		readonly List<JniRemappingTypeReplacement>? reverseTypeReplacements;
		readonly List<MethodType>? methodTypes;
		readonly List<FieldType>? fieldTypes;

		public int ReplacementTypeCount => typeReplacements?.Count ?? 0;
		public int ReverseTypeCount => reverseTypeReplacements?.Count ?? 0;
		public int ReplacementMethodIndexEntryCount => methodTypes?.Count ?? 0;
		public int ReplacementFieldIndexEntryCount => fieldTypes?.Count ?? 0;

		public JniRemappingAssemblyGenerator (TaskLoggingHelper log)
		{
			if (log == null) {
				throw new ArgumentNullException (nameof (log));
			}
		}

		public JniRemappingAssemblyGenerator (TaskLoggingHelper log, List<JniRemappingTypeReplacement> types,
		                                      List<JniRemappingTypeReplacement> reverseTypes,
		                                      List<JniRemappingMethodReplacement> methods, List<JniRemappingFieldReplacement> fields)
			: this (log)
		{
			typeReplacements = SortTypes (types ?? throw new ArgumentNullException (nameof (types)));
			reverseTypeReplacements = SortTypes (reverseTypes ?? throw new ArgumentNullException (nameof (reverseTypes)));
			methodTypes = SortMethods (methods ?? throw new ArgumentNullException (nameof (methods)));
			fieldTypes = SortFields (fields ?? throw new ArgumentNullException (nameof (fields)));
		}

		internal static int CompareUtf8 (byte[] left, byte[] right)
		{
			int min = Math.Min (left.Length, right.Length);
			for (int i = 0; i < min; i++) {
				if (left [i] != right [i]) {
					return left [i] < right [i] ? -1 : 1;
				}
			}
			return left.Length.CompareTo (right.Length);
		}

		static byte[] Utf8 (string? s) => string.IsNullOrEmpty (s) ? [] : Encoding.UTF8.GetBytes (s);
		static int CompareNames (string? left, string? right) => CompareUtf8 (Utf8 (left), Utf8 (right));

		static List<JniRemappingTypeReplacement> SortTypes (List<JniRemappingTypeReplacement> input)
		{
			var result = new List<JniRemappingTypeReplacement> (input);
			result.Sort ((left, right) => CompareNames (left.From, right.From));
			return result;
		}

		static List<MethodType> SortMethods (List<JniRemappingMethodReplacement> input)
		{
			var groups = new Dictionary<string, List<JniRemappingMethodReplacement>> (StringComparer.Ordinal);
			foreach (var method in input) {
				if (!groups.TryGetValue (method.SourceType, out var methods)) {
					methods = new ();
					groups.Add (method.SourceType, methods);
				}
				methods.Add (method);
			}

			var names = new List<string> (groups.Keys);
			names.Sort (CompareNames);
			var result = new List<MethodType> (names.Count);
			foreach (string name in names) {
				var methods = groups [name];
				methods.Sort ((left, right) => {
					int cmp = CompareNames (left.SourceMethod, right.SourceMethod);
					if (cmp != 0) {
						return cmp;
					}
					cmp = SignatureSpecificity (left.SourceMethodSignature).CompareTo (SignatureSpecificity (right.SourceMethodSignature));
					return cmp != 0 ? cmp : CompareNames (left.SourceMethodSignature, right.SourceMethodSignature);
				});
				result.Add (new MethodType (name, result.Count, methods));
			}
			return result;
		}

		static int SignatureSpecificity (string? signature) =>
			string.IsNullOrEmpty (signature) ? 2 : signature [signature.Length - 1] == ')' ? 1 : 0;

		static List<FieldType> SortFields (List<JniRemappingFieldReplacement> input)
		{
			var groups = new Dictionary<string, List<JniRemappingFieldReplacement>> (StringComparer.Ordinal);
			foreach (var field in input) {
				if (!groups.TryGetValue (field.SourceType, out var fields)) {
					fields = new ();
					groups.Add (field.SourceType, fields);
				}
				fields.Add (field);
			}

			var names = new List<string> (groups.Keys);
			names.Sort (CompareNames);
			var result = new List<FieldType> (names.Count);
			foreach (string name in names) {
				var fields = groups [name];
				fields.Sort ((left, right) => {
					int cmp = CompareNames (left.SourceField, right.SourceField);
					return cmp != 0 ? cmp : CompareNames (left.SourceFieldSignature, right.SourceFieldSignature);
				});
				result.Add (new FieldType (name, result.Count, fields));
			}
			return result;
		}

		public void Generate (AndroidTargetArch arch, TextWriter output, string fileName)
		{
			using var w = new LlvmIrWriter (output, LlvmIrTarget.Get (arch));
			var strings = new LlvmIrStringPool ();
			ulong alignment = Math.Max (w.Target.PointerSize, 4);

			w.WriteHeader (fileName);
			w.Write ("""

				%struct.JniRemappingString = type { i32, ptr }
				%struct.JniRemappingTypeReplacementEntry = type { %struct.JniRemappingString, ptr }
				%struct.JniRemappingReplacementMethod = type { ptr, ptr, ptr, i1 }
				%struct.JniRemappingIndexMethodEntry = type { %struct.JniRemappingString, %struct.JniRemappingString, %struct.JniRemappingReplacementMethod }
				%struct.JniRemappingIndexTypeEntry = type { %struct.JniRemappingString, i32, ptr }
				%struct.JniRemappingReplacementField = type { ptr, ptr, ptr }
				%struct.JniRemappingIndexFieldEntry = type { %struct.JniRemappingString, %struct.JniRemappingString, %struct.JniRemappingReplacementField }
				%struct.JniRemappingIndexFieldTypeEntry = type { %struct.JniRemappingString, i32, ptr }
				%struct.JniRemappingData = type { ptr, ptr, ptr, ptr, i32, i32, i32, i32 }

				""");

			WriteTypes (TypeReplacementsVariableName, typeReplacements);
			WriteTypes (ReverseTypeReplacementsVariableName, reverseTypeReplacements);
			WriteMethods ();
			WriteFields ();

			w.WriteGlobal ("jni_remapping_data", LlvmIrWriter.GlobalConstant, "%struct.JniRemappingData", $$"""
				{
					ptr @{{TypeReplacementsVariableName}},
					ptr @{{ReverseTypeReplacementsVariableName}},
					ptr @{{MethodReplacementIndexVariableName}},
					ptr @{{FieldReplacementIndexVariableName}},
					i32 {{ReplacementTypeCount}},
					i32 {{ReverseTypeCount}},
					i32 {{ReplacementMethodIndexEntryCount}},
					i32 {{ReplacementFieldIndexEntryCount}}
				}
				""", w.GetAggregateAlignment (alignment, RemappingDataSize));

			strings.Write (w);
			w.WriteMetadata ();
			output.Flush ();

			void WriteTypes (string symbol, List<JniRemappingTypeReplacement>? replacements)
			{
				if (replacements == null) {
					w.WriteGlobal (symbol, LlvmIrWriter.GlobalConstant, "%struct.JniRemappingTypeReplacementEntry", "zeroinitializer", alignment);
					return;
				}

				var elements = new List<string> (replacements.Count);
				foreach (var replacement in replacements) {
					elements.Add ($$"""
							%struct.JniRemappingTypeReplacementEntry {
								{{RenderString (strings, new JniRemappingString (replacement.From))}}, {{w.Comment ($" name: {replacement.From}")}}
								ptr {{strings.GetPointer (replacement.To, "JniRemappingTypeReplacementEntry", "replacement")}}{{w.Comment ($" replacement: {replacement.To}")}}
							}
						""");
				}
				WriteArray (w, symbol, LlvmIrWriter.GlobalConstant, "JniRemappingTypeReplacementEntry", elements, alignment, 4);
			}

			void WriteMethods ()
			{
				if (methodTypes == null) {
					w.WriteGlobal (MethodReplacementIndexVariableName, LlvmIrWriter.GlobalConstant, "%struct.JniRemappingIndexTypeEntry", "zeroinitializer", alignment);
					return;
				}

				foreach (var type in methodTypes) {
					var elements = new List<string> (type.Methods.Count);
					foreach (var method in type.Methods) {
						var signature = new JniRemappingString (method.SourceMethodSignature);
						elements.Add ($$"""
								%struct.JniRemappingIndexMethodEntry {
									{{RenderString (strings, new JniRemappingString (method.SourceMethod))}}, {{w.Comment ($" name: {method.SourceMethod}")}}
									{{RenderString (strings, signature)}}, {{w.Comment (signature.Length == 0 ? " JniRemappingString signature" : $"signature: {signature.Value}")}}
									%struct.JniRemappingReplacementMethod {
										ptr {{strings.GetPointer (method.TargetType, "JniRemappingReplacementMethod", "target_type")}},
										ptr {{strings.GetPointer (method.TargetMethod, "JniRemappingReplacementMethod", "target_name")}},
										ptr {{strings.GetPointer (method.TargetMethodSignature, "JniRemappingReplacementMethod", "target_signature")}},
										i1 {{(method.TargetIsStatic ? "true" : "false")}}
									}{{w.Comment ($" replacement: {method.TargetType}.{method.TargetMethod}")}}
								}
							""");
					}
					WriteArray (w, type.Symbol, LlvmIrWriter.LocalConstant, "JniRemappingIndexMethodEntry", elements, alignment, MethodEntryDataSize);
				}

				var types = new List<string> (methodTypes.Count);
				foreach (var type in methodTypes) {
					types.Add ($$"""
							%struct.JniRemappingIndexTypeEntry {
								{{RenderString (strings, new JniRemappingString (type.Name))}}, {{w.Comment ($" name: {type.Name}")}}
								i32 {{type.Methods.Count}},
								ptr @{{type.Symbol}}
							}
						""");
				}
				WriteArray (w, MethodReplacementIndexVariableName, LlvmIrWriter.GlobalConstant, "JniRemappingIndexTypeEntry", types, alignment, TypeEntryDataSize);
			}

			void WriteFields ()
			{
				if (fieldTypes == null) {
					w.WriteGlobal (FieldReplacementIndexVariableName, LlvmIrWriter.GlobalConstant, "%struct.JniRemappingIndexFieldTypeEntry", "zeroinitializer", alignment);
					return;
				}

				foreach (var type in fieldTypes) {
					var elements = new List<string> (type.Fields.Count);
					foreach (var field in type.Fields) {
						var signature = new JniRemappingString (field.SourceFieldSignature);
						elements.Add ($$"""
								%struct.JniRemappingIndexFieldEntry {
									{{RenderString (strings, new JniRemappingString (field.SourceField))}}, {{w.Comment ($" name: {field.SourceField}")}}
									{{RenderString (strings, signature)}}, {{w.Comment (signature.Length == 0 ? " JniRemappingString signature" : $"signature: {signature.Value}")}}
									%struct.JniRemappingReplacementField {
										ptr {{strings.GetPointer (field.TargetType, "JniRemappingReplacementField", "target_type")}},
										ptr {{strings.GetPointer (field.TargetField, "JniRemappingReplacementField", "target_name")}},
										ptr {{strings.GetPointer (field.TargetFieldSignature, "JniRemappingReplacementField", "target_signature")}}
									}{{w.Comment ($" replacement: {field.TargetType}.{field.TargetField}")}}
								}
							""");
					}
					WriteArray (w, type.Symbol, LlvmIrWriter.LocalConstant, "JniRemappingIndexFieldEntry", elements, alignment, FieldEntryDataSize);
				}

				var types = new List<string> (fieldTypes.Count);
				foreach (var type in fieldTypes) {
					types.Add ($$"""
							%struct.JniRemappingIndexFieldTypeEntry {
								{{RenderString (strings, new JniRemappingString (type.Name))}}, {{w.Comment ($" name: {type.Name}")}}
								i32 {{type.Fields.Count}},
								ptr @{{type.Symbol}}
							}
						""");
				}
				WriteArray (w, FieldReplacementIndexVariableName, LlvmIrWriter.GlobalConstant, "JniRemappingIndexFieldTypeEntry", types, alignment, TypeEntryDataSize);
			}
		}

		static string RenderString (LlvmIrStringPool strings, JniRemappingString s) => $$"""
			%struct.JniRemappingString {
						i32 {{s.Length}},
						ptr {{strings.GetPointer (s.Value, "JniRemappingString", "str")}}
					}
			""";

		static void WriteArray (LlvmIrWriter w, string name, string attributes, string structName, List<string> elements, ulong alignment, ulong dataSize)
		{
			w.WriteGlobal (name, attributes, $"[{elements.Count} x %struct.{structName}]",
				w.ArrayValue (elements, i => $" {i}"), w.GetAggregateAlignment (alignment, (ulong)elements.Count * dataSize));
		}
	}
}
