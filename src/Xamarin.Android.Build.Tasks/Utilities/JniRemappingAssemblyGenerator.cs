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
		public string To   { get; }

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
		public string SourceMethodSignature { get; }

		public string TargetType { get; }
		public string TargetMethod { get; }

		public bool TargetIsStatic { get; }

		public JniRemappingMethodReplacement (string sourceType, string sourceMethod, string sourceMethodSignature,
		                                      string targetType, string targetMethod, bool targetIsStatic)
		{
			SourceType = sourceType;
			SourceMethod = sourceMethod;
			SourceMethodSignature = sourceMethodSignature;

			TargetType = targetType;
			TargetMethod = targetMethod;
			TargetIsStatic = targetIsStatic;
		}
	}

	class JniRemappingAssemblyGenerator
	{
		const string TypeReplacementsVariableName = "jni_remapping_type_replacements";
		const string MethodReplacementIndexVariableName = "jni_remapping_method_replacement_index";

		// The structure declarations written by this class must be identical to the structures in
		// src/native/clr/include/xamarin-app.hh.  Data sizes are the sums of sizes of all the
		// non-pointer members (see LlvmIrTarget.GetAggregateAlignment), alignments don't include
		// the pointer members either (those are accounted for in GetAlignment below)
		const ulong JniRemappingIndexMethodEntryDataSize = 9;
		const ulong JniRemappingIndexTypeEntryDataSize = 8;
		const ulong JniRemappingTypeReplacementEntryDataSize = 4;
		const ulong NonPointerMemberAlignment = 4;

		sealed class JniRemappingString
		{
			public readonly uint length;
			public readonly string str;

			public JniRemappingString (string str)
			{
				this.str = str;
				length = String.IsNullOrEmpty (str) ? 0 : (uint)Encoding.UTF8.GetBytes (str).Length;
			}
		}

		sealed class JniRemappingIndexMethodEntry
		{
			public readonly JniRemappingString name;
			public readonly JniRemappingString signature;
			public readonly string target_type;
			public readonly string target_name;
			public readonly bool is_static;

			public JniRemappingIndexMethodEntry (JniRemappingMethodReplacement mmr)
			{
				name = new JniRemappingString (mmr.SourceMethod);
				signature = new JniRemappingString (mmr.SourceMethodSignature);
				target_type = mmr.TargetType;
				target_name = mmr.TargetMethod;
				is_static = mmr.TargetIsStatic;
			}
		}

		sealed class JniRemappingIndexTypeEntry
		{
			public readonly JniRemappingString name;
			public readonly string MethodsArraySymbolName;
			public readonly List<JniRemappingIndexMethodEntry> TypeMethods = new ();

			public JniRemappingIndexTypeEntry (string typeName)
			{
				name = new JniRemappingString (typeName);
				MethodsArraySymbolName = $"mm_{typeName.Replace ('/', '_')}";
			}
		}

		sealed class JniRemappingTypeReplacementEntry
		{
			public readonly JniRemappingString name;
			public readonly string replacement;

			public JniRemappingTypeReplacementEntry (JniRemappingTypeReplacement mtr)
			{
				name = new JniRemappingString (mtr.From);
				replacement = mtr.To;
			}
		}

		readonly List<JniRemappingTypeReplacementEntry>? typeReplacements;
		readonly List<JniRemappingIndexTypeEntry>? methodIndexTypes;

		public int ReplacementMethodIndexEntryCount { get; private set; } = 0;

		/// <summary>
		/// Whether to write descriptive comments into the generated LLVM IR.  Defaults to <c>false</c>.
		/// </summary>
		public bool EmitComments { get; set; }

		public JniRemappingAssemblyGenerator (TaskLoggingHelper log)
		{
			if (log == null) {
				throw new ArgumentNullException (nameof (log));
			}
		}

		public JniRemappingAssemblyGenerator (TaskLoggingHelper log, List<JniRemappingTypeReplacement> typeReplacements, List<JniRemappingMethodReplacement> methodReplacements)
			: this (log)
		{
			if (typeReplacements == null) {
				throw new ArgumentNullException (nameof (typeReplacements));
			}

			if (methodReplacements == null) {
				throw new ArgumentNullException (nameof (methodReplacements));
			}

			this.typeReplacements = new List<JniRemappingTypeReplacementEntry> ();
			foreach (JniRemappingTypeReplacement mtr in typeReplacements) {
				this.typeReplacements.Add (new JniRemappingTypeReplacementEntry (mtr));
			}
			this.typeReplacements.Sort ((JniRemappingTypeReplacementEntry l, JniRemappingTypeReplacementEntry r) => l.name.str.CompareTo (r.name.str));

			methodIndexTypes = new List<JniRemappingIndexTypeEntry> ();
			var types = new Dictionary<string, JniRemappingIndexTypeEntry> (StringComparer.Ordinal);

			foreach (JniRemappingMethodReplacement mmr in methodReplacements) {
				if (!types.TryGetValue (mmr.SourceType, out JniRemappingIndexTypeEntry? typeEntry)) {
					typeEntry = new JniRemappingIndexTypeEntry (mmr.SourceType);
					methodIndexTypes.Add (typeEntry);
					types.Add (mmr.SourceType, typeEntry);
				}

				typeEntry.TypeMethods.Add (new JniRemappingIndexMethodEntry (mmr));
			}

			foreach (var kvp in types) {
				kvp.Value.TypeMethods.Sort ((JniRemappingIndexMethodEntry l, JniRemappingIndexMethodEntry r) => l.name.str.CompareTo (r.name.str));
			}

			methodIndexTypes.Sort ((JniRemappingIndexTypeEntry l, JniRemappingIndexTypeEntry r) => l.name.str.CompareTo (r.name.str));
			ReplacementMethodIndexEntryCount = methodIndexTypes.Count;
		}

		public void Generate (AndroidTargetArch arch, TextWriter output, string fileName)
		{
			var w = new LlvmIrWriter (output, LlvmIrTarget.Get (arch), EmitComments);
			var strings = new LlvmIrStringPool ();
			ulong alignment = GetAlignment (w);

			w.WriteHeader (fileName);
			w.Write ($$"""

				%struct.JniRemappingIndexMethodEntry = type {
					%struct.JniRemappingString, {{w.Comment (" JniRemappingString name")}}
					%struct.JniRemappingString, {{w.Comment (" JniRemappingString signature")}}
					%struct.JniRemappingReplacementMethod {{w.Comment (" JniRemappingReplacementMethod replacement")}}
				}

				%struct.JniRemappingIndexTypeEntry = type {
					%struct.JniRemappingString, {{w.Comment (" JniRemappingString name")}}
					i32, {{w.Comment (" uint32_t method_count")}}
					ptr {{w.Comment (" JniRemappingIndexMethodEntry methods")}}
				}

				%struct.JniRemappingReplacementMethod = type {
					ptr, {{w.Comment (" char* target_type")}}
					ptr, {{w.Comment (" char* target_name")}}
					i1 {{w.Comment (" bool is_static")}}
				}

				%struct.JniRemappingString = type {
					i32, {{w.Comment (" uint32_t length")}}
					ptr {{w.Comment (" char* str")}}
				}

				%struct.JniRemappingTypeReplacementEntry = type {
					%struct.JniRemappingString, {{w.Comment (" JniRemappingString name")}}
					ptr {{w.Comment (" char* replacement")}}
				}

				""");

			if (typeReplacements == null || methodIndexTypes == null) {
				w.WriteGlobal (TypeReplacementsVariableName, LlvmIrWriter.GlobalConstant, "%struct.JniRemappingTypeReplacementEntry", "zeroinitializer", w.GetAggregateAlignment (alignment, JniRemappingTypeReplacementEntryDataSize));
				w.WriteGlobal (MethodReplacementIndexVariableName, LlvmIrWriter.GlobalConstant, "%struct.JniRemappingIndexTypeEntry", "zeroinitializer", w.GetAggregateAlignment (alignment, JniRemappingIndexTypeEntryDataSize));
			} else {
				var elements = new List<string> (typeReplacements.Count);
				foreach (JniRemappingTypeReplacementEntry entry in typeReplacements) {
					elements.Add ($$"""
							%struct.JniRemappingTypeReplacementEntry {
								{{RenderString (w, strings, entry.name)}}, {{w.Comment ($" name: {entry.name.str}")}}
								ptr {{strings.GetPointer (entry.replacement, "JniRemappingTypeReplacementEntry", "replacement")}}{{w.Comment ($" replacement: {entry.replacement}")}}
							}
						""");
				}
				WriteArray (w, TypeReplacementsVariableName, LlvmIrWriter.GlobalConstant, "JniRemappingTypeReplacementEntry", elements, alignment, JniRemappingTypeReplacementEntryDataSize);

				foreach (JniRemappingIndexTypeEntry type in methodIndexTypes) {
					elements = new List<string> (type.TypeMethods.Count);
					foreach (JniRemappingIndexMethodEntry method in type.TypeMethods) {
						string signatureComment = method.signature.length == 0 ? " JniRemappingString signature" : $"signature: {method.signature.str}";
						elements.Add ($$"""
								%struct.JniRemappingIndexMethodEntry {
									{{RenderString (w, strings, method.name)}}, {{w.Comment ($" name: {method.name.str}")}}
									{{RenderString (w, strings, method.signature)}}, {{w.Comment (signatureComment)}}
									%struct.JniRemappingReplacementMethod {
										ptr {{strings.GetPointer (method.target_type, "JniRemappingReplacementMethod", "target_type")}}, {{w.Comment (" char* target_type")}}
										ptr {{strings.GetPointer (method.target_name, "JniRemappingReplacementMethod", "target_name")}}, {{w.Comment (" char* target_name")}}
										i1 {{LlvmIrWriter.Bool (method.is_static)}}{{w.Comment (" bool is_static")}}
									}{{w.Comment ($" replacement: {method.target_type}.{method.target_name}")}}
								}
							""");
					}
					WriteArray (w, type.MethodsArraySymbolName, LlvmIrWriter.LocalConstant, "JniRemappingIndexMethodEntry", elements, alignment, JniRemappingIndexMethodEntryDataSize);
				}

				elements = new List<string> (methodIndexTypes.Count);
				foreach (JniRemappingIndexTypeEntry type in methodIndexTypes) {
					elements.Add ($$"""
							%struct.JniRemappingIndexTypeEntry {
								{{RenderString (w, strings, type.name)}}, {{w.Comment ($" name: {type.name.str}")}}
								i32 {{LlvmIrWriter.Number (type.TypeMethods.Count)}}, {{w.Comment (" uint32_t method_count")}}
								ptr @{{type.MethodsArraySymbolName}}{{w.Comment (" JniRemappingIndexMethodEntry* methods")}}
							}
						""");
				}
				WriteArray (w, MethodReplacementIndexVariableName, LlvmIrWriter.GlobalConstant, "JniRemappingIndexTypeEntry", elements, alignment, JniRemappingIndexTypeEntryDataSize);
			}

			strings.Write (w);
			w.WriteMetadata ();
			output.Flush ();
		}

		// All the structures written here either contain pointers or no members with alignment requirements higher than 4
		static ulong GetAlignment (LlvmIrWriter w) => Math.Max (w.Target.PointerSize, NonPointerMemberAlignment);

		// Renders a JniRemappingString structure embedded in another structure, at the second level of indentation
		static string RenderString (LlvmIrWriter w, LlvmIrStringPool strings, JniRemappingString s)
		{
			return $$"""
				%struct.JniRemappingString {
							i32 {{LlvmIrWriter.Number (s.length)}}, {{w.Comment (" uint32_t length")}}
							ptr {{strings.GetPointer (s.str, "JniRemappingString", "str")}}{{w.Comment (" char* str")}}
						}
				""";
		}

		static void WriteArray (LlvmIrWriter w, string name, string attributes, string structName, List<string> elements, ulong alignment, ulong dataSize)
		{
			w.WriteGlobal (
				name,
				attributes,
				$"[{LlvmIrWriter.Number (elements.Count)} x %struct.{structName}]",
				w.ArrayValue (elements, LlvmIrWriter.IndexComment),
				w.GetAggregateAlignment (alignment, (ulong)elements.Count * dataSize)
			);
		}
	}
}
