using System;
using System.Collections.Generic;
using System.Text;

using Xamarin.Android.Tasks.LLVMIR;

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

	partial class JniRemappingAssemblyGenerator : LlvmIrComposer
	{
		sealed class JniRemappingTypeReplacementEntryContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var entry = EnsureType<JniRemappingTypeReplacementEntry>(data);

				if (String.Compare ("name", fieldName, StringComparison.Ordinal) == 0) {
					return $"name: {entry.name.str}";
				}

				if (String.Compare ("replacement", fieldName, StringComparison.Ordinal) == 0) {
					return $"replacement: {entry.replacement}";
				}

				return String.Empty;
			}
		}

		sealed class JniRemappingIndexTypeEntryContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var entry = EnsureType<JniRemappingIndexTypeEntry> (data);

				if (String.Compare ("name", fieldName, StringComparison.Ordinal) == 0) {
					return $"name: {entry.name.str}";
				}

				return String.Empty;
			}

			public override string GetPointedToSymbolName (object data, string fieldName)
			{
				var entry = EnsureType<JniRemappingIndexTypeEntry> (data);

				if (String.Compare ("methods", fieldName, StringComparison.Ordinal) == 0) {
					return entry.MethodsArraySymbolName;
				}

				return base.GetPointedToSymbolName (data, fieldName);
			}

			public override ulong GetBufferSize (object data, string fieldName)
			{
				var entry = EnsureType<JniRemappingIndexTypeEntry> (data);
				if (String.Compare ("methods", fieldName, StringComparison.Ordinal) == 0) {
					return (ulong)entry.TypeMethods.Count;
				}

				return 0;
			}
		}

		sealed class JniRemappingIndexMethodEntryContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var entry = EnsureType<JniRemappingIndexMethodEntry> (data);

				if (String.Compare ("name", fieldName, StringComparison.Ordinal) == 0) {
					return $"name: {entry.name.str}";
				}

				if (String.Compare ("replacement", fieldName, StringComparison.Ordinal) == 0) {
					return $"replacement: {entry.replacement.target_type}.{entry.replacement.target_name}";
				}

				if (String.Compare ("signature", fieldName, StringComparison.Ordinal) == 0) {
					if (entry.signature.length == 0) {
						return String.Empty;
					}

					return $"signature: {entry.signature.str}";
				}

				return String.Empty;
			}
		}

		sealed class JniRemappingString
		{
			public uint   length;
			public string str;
		};

		sealed class JniRemappingReplacementMethod
		{
			public string  target_type;
			public string  target_name;
			public bool    is_static;
		};

		[NativeAssemblerStructContextDataProvider (typeof(JniRemappingIndexMethodEntryContextDataProvider))]
		sealed class JniRemappingIndexMethodEntry
		{
			[NativeAssembler (UsesDataProvider = true)]
			public JniRemappingString            name;

			[NativeAssembler (UsesDataProvider = true)]
			public JniRemappingString            signature;

			[NativeAssembler (UsesDataProvider = true)]
			public JniRemappingReplacementMethod replacement;
		};

		[NativeAssemblerStructContextDataProvider (typeof(JniRemappingIndexTypeEntryContextDataProvider))]
		sealed class JniRemappingIndexTypeEntry
		{
			[NativeAssembler (UsesDataProvider = true)]
			public JniRemappingString           name;
			public uint                method_count;

			[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToSymbol = "")]
			public JniRemappingIndexMethodEntry methods;

			[NativeAssembler (Ignore = true)]
			public string MethodsArraySymbolName;

			[NativeAssembler (Ignore = true)]
			public List<StructureInstance<JniRemappingIndexMethodEntry>> TypeMethods;
		};

		[NativeAssemblerStructContextDataProvider (typeof(JniRemappingTypeReplacementEntryContextDataProvider))]
		sealed class JniRemappingTypeReplacementEntry
		{
			[NativeAssembler (UsesDataProvider = true)]
			public JniRemappingString name;

			[NativeAssembler (UsesDataProvider = true)]
			public string    replacement;
		};

		List<JniRemappingTypeReplacement> typeReplacementsInput;
		List<JniRemappingMethodReplacement> methodReplacementsInput;

		StructureInfo<JniRemappingString> jniRemappingStringStructureInfo;
		StructureInfo<JniRemappingReplacementMethod> jniRemappingReplacementMethodStructureInfo;
		StructureInfo<JniRemappingIndexMethodEntry> jniRemappingIndexMethodEntryStructureInfo;
		StructureInfo<JniRemappingIndexTypeEntry> jniRemappingIndexTypeEntryStructureInfo;
		StructureInfo<JniRemappingTypeReplacementEntry> jniRemappingTypeReplacementEntryStructureInfo;

		List<StructureInstance<JniRemappingTypeReplacementEntry>> typeReplacements;
		List<StructureInstance<JniRemappingIndexTypeEntry>> methodIndexTypes;

		public int ReplacementMethodIndexEntryCount { get; private set; } = 0;

		public JniRemappingAssemblyGenerator ()
		{}

		public JniRemappingAssemblyGenerator (List<JniRemappingTypeReplacement> typeReplacements, List<JniRemappingMethodReplacement> methodReplacements)
		{
			this.typeReplacementsInput = typeReplacements ?? throw new ArgumentNullException (nameof (typeReplacements));
			this.methodReplacementsInput = methodReplacements ?? throw new ArgumentNullException (nameof (methodReplacements));
		}

		public override void Init ()
		{
			if (typeReplacementsInput == null) {
				return;
			}

			typeReplacements = new List<StructureInstance<JniRemappingTypeReplacementEntry>> ();
			foreach (JniRemappingTypeReplacement mtr in typeReplacementsInput) {
				var entry = new JniRemappingTypeReplacementEntry {
					name = MakeJniRemappingString (mtr.From),
					replacement = mtr.To,
				};

				typeReplacements.Add (new StructureInstance<JniRemappingTypeReplacementEntry> (entry));
			}
			typeReplacements.Sort ((StructureInstance<JniRemappingTypeReplacementEntry> l, StructureInstance<JniRemappingTypeReplacementEntry> r) => l.Obj.name.str.CompareTo (r.Obj.name.str));

			methodIndexTypes = new List<StructureInstance<JniRemappingIndexTypeEntry>> ();
			var types = new Dictionary<string, StructureInstance<JniRemappingIndexTypeEntry>> (StringComparer.Ordinal);

			foreach (JniRemappingMethodReplacement mmr in methodReplacementsInput) {
				if (!types.TryGetValue (mmr.SourceType, out StructureInstance<JniRemappingIndexTypeEntry> typeEntry)) {
					var entry = new JniRemappingIndexTypeEntry {
						name = MakeJniRemappingString (mmr.SourceType),
						MethodsArraySymbolName = MakeMethodsArrayName (mmr.SourceType),
						TypeMethods = new List<StructureInstance<JniRemappingIndexMethodEntry>> (),
					};

					typeEntry = new StructureInstance<JniRemappingIndexTypeEntry> (entry);
					methodIndexTypes.Add (typeEntry);
					types.Add (mmr.SourceType, typeEntry);
				}

				var method = new JniRemappingIndexMethodEntry {
					name = MakeJniRemappingString (mmr.SourceMethod),
					signature = MakeJniRemappingString (mmr.SourceMethodSignature),
					replacement = new JniRemappingReplacementMethod {
						target_type = mmr.TargetType,
						target_name = mmr.TargetMethod,
						is_static = mmr.TargetIsStatic,
					},
				};

				typeEntry.Obj.TypeMethods.Add (new StructureInstance<JniRemappingIndexMethodEntry> (method));
			}

			foreach (var kvp in types) {
				kvp.Value.Obj.method_count = (uint)kvp.Value.Obj.TypeMethods.Count;
				kvp.Value.Obj.TypeMethods.Sort ((StructureInstance<JniRemappingIndexMethodEntry> l, StructureInstance<JniRemappingIndexMethodEntry> r) => l.Obj.name.str.CompareTo (r.Obj.name.str));
			}

			methodIndexTypes.Sort ((StructureInstance<JniRemappingIndexTypeEntry> l, StructureInstance<JniRemappingIndexTypeEntry> r) => l.Obj.name.str.CompareTo (r.Obj.name.str));
			ReplacementMethodIndexEntryCount = methodIndexTypes.Count;

			string MakeMethodsArrayName (string typeName)
			{
				return $"mm_{typeName.Replace ('/', '_')}";
			}

			JniRemappingString MakeJniRemappingString (string str)
			{
				return new JniRemappingString {
					length = GetLength (str),
					str = str,
				};
			}
		}

		uint GetLength (string str)
		{
			if (String.IsNullOrEmpty (str)) {
				return 0;
			}

			return (uint)Encoding.UTF8.GetBytes (str).Length;
		}

		protected override void MapStructures (LlvmIrGenerator generator)
		{
			jniRemappingStringStructureInfo = generator.MapStructure<JniRemappingString> ();
			jniRemappingReplacementMethodStructureInfo = generator.MapStructure<JniRemappingReplacementMethod> ();
			jniRemappingIndexMethodEntryStructureInfo = generator.MapStructure<JniRemappingIndexMethodEntry> ();
			jniRemappingIndexTypeEntryStructureInfo = generator.MapStructure<JniRemappingIndexTypeEntry> ();
			jniRemappingTypeReplacementEntryStructureInfo = generator.MapStructure<JniRemappingTypeReplacementEntry> ();
		}

		void WriteNestedStructure (LlvmIrGenerator generator, LlvmIrGenerator.StructureBodyWriterOptions bodyWriterOptions, Type structureType, object fieldInstance)
		{
			if (fieldInstance == null) {
				return;
			}

			if (structureType == typeof (JniRemappingString)) {
				generator.WriteNestedStructure<JniRemappingString> (jniRemappingStringStructureInfo, new StructureInstance<JniRemappingString> ((JniRemappingString)fieldInstance), bodyWriterOptions);
				return;
			}

			if (structureType == typeof (JniRemappingReplacementMethod)) {
				generator.WriteNestedStructure<JniRemappingReplacementMethod> (jniRemappingReplacementMethodStructureInfo, new StructureInstance<JniRemappingReplacementMethod> ((JniRemappingReplacementMethod)fieldInstance), bodyWriterOptions);
				return;
			}

			if (structureType == typeof (JniRemappingIndexTypeEntry)) {
				generator.WriteNestedStructure<JniRemappingIndexTypeEntry> (jniRemappingIndexTypeEntryStructureInfo, new StructureInstance<JniRemappingIndexTypeEntry> ((JniRemappingIndexTypeEntry)fieldInstance), bodyWriterOptions);
			}

			if (structureType == typeof (JniRemappingIndexMethodEntry)) {
				generator.WriteNestedStructure<JniRemappingIndexMethodEntry> (jniRemappingIndexMethodEntryStructureInfo, new StructureInstance<JniRemappingIndexMethodEntry> ((JniRemappingIndexMethodEntry)fieldInstance), bodyWriterOptions);
			}

			throw new InvalidOperationException ($"Unsupported nested structure type {structureType}");
		}

		protected override void Write (LlvmIrGenerator generator)
		{
			generator.WriteEOL ();
			generator.WriteEOL ("JNI remapping data");

			if (typeReplacements == null) {
				generator.WriteStructureArray (
					jniRemappingTypeReplacementEntryStructureInfo,
					0,
					LlvmIrVariableOptions.GlobalConstant,
					"jni_remapping_type_replacements"
				);

				generator.WriteStructureArray (
					jniRemappingIndexTypeEntryStructureInfo,
					0,
					LlvmIrVariableOptions.GlobalConstant,
					"jni_remapping_method_replacement_index"
				);

				return;
			}

			generator.WriteStructureArray (
				jniRemappingTypeReplacementEntryStructureInfo,
				typeReplacements,
				LlvmIrVariableOptions.GlobalConstant,
				"jni_remapping_type_replacements",
				nestedStructureWriter: WriteNestedStructure
			);

			foreach (StructureInstance<JniRemappingIndexTypeEntry> entry in methodIndexTypes) {
				generator.WriteStructureArray (
					jniRemappingIndexMethodEntryStructureInfo,
					entry.Obj.TypeMethods,
					LlvmIrVariableOptions.LocalConstant,
					entry.Obj.MethodsArraySymbolName,
					nestedStructureWriter: WriteNestedStructure
				);
			}

			generator.WriteStructureArray (
				jniRemappingIndexTypeEntryStructureInfo,
				methodIndexTypes,
				LlvmIrVariableOptions.GlobalConstant,
				"jni_remapping_method_replacement_index",
				nestedStructureWriter: WriteNestedStructure
			);
		}
	}
}
