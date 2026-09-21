#nullable disable

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Build.Utilities;

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
		public string TargetMethodSignature { get; }

		public bool TargetIsStatic { get; }

		public JniRemappingMethodReplacement (string sourceType, string sourceMethod, string sourceMethodSignature,
		                                      string targetType, string targetMethod, string targetMethodSignature, bool targetIsStatic)
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
		public string SourceFieldSignature { get; }

		public string TargetType { get; }
		public string TargetField { get; }
		public string TargetFieldSignature { get; }

		public JniRemappingFieldReplacement (string sourceType, string sourceField, string sourceFieldSignature,
		                                     string targetType, string targetField, string targetFieldSignature)
		{
			SourceType = sourceType;
			SourceField = sourceField;
			SourceFieldSignature = sourceFieldSignature;

			TargetType = targetType;
			TargetField = targetField;
			TargetFieldSignature = targetFieldSignature;
		}
	}

	class JniRemappingAssemblyGenerator : LlvmIrComposer
	{
		const string TypeReplacementsVariableName = "jni_remapping_type_replacements";
		const string ReverseTypeReplacementsVariableName = "jni_remapping_reverse_type_replacements";
		const string MethodReplacementIndexVariableName = "jni_remapping_method_replacement_index";
		const string FieldReplacementIndexVariableName = "jni_remapping_field_replacement_index";

		const string TypeReplacementCountVariableName = "jni_remapping_type_replacement_count";
		const string ReverseTypeReplacementCountVariableName = "jni_remapping_reverse_type_replacement_count";
		const string MethodReplacementIndexCountVariableName = "jni_remapping_method_replacement_index_count";
		const string FieldReplacementIndexCountVariableName = "jni_remapping_field_replacement_index_count";

		sealed class JniRemappingTypeReplacementEntryContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var entry = EnsureType<JniRemappingTypeReplacementEntry>(data);

				if (MonoAndroidHelper.StringEquals ("name", fieldName)) {
					return $" name: {entry.name.str}";
				}

				if (MonoAndroidHelper.StringEquals ("replacement", fieldName)) {
					return $" replacement: {entry.replacement}";
				}

				return String.Empty;
			}
		}

		sealed class JniRemappingIndexTypeEntryContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var entry = EnsureType<JniRemappingIndexTypeEntry> (data);

				if (MonoAndroidHelper.StringEquals ("name", fieldName)) {
					return $" name: {entry.name.str}";
				}

				return String.Empty;
			}

			public override string GetPointedToSymbolName (object data, string fieldName)
			{
				var entry = EnsureType<JniRemappingIndexTypeEntry> (data);

				if (MonoAndroidHelper.StringEquals ("methods", fieldName)) {
					return entry.MethodsArraySymbolName;
				}

				return base.GetPointedToSymbolName (data, fieldName);
			}

			public override ulong GetBufferSize (object data, string fieldName)
			{
				var entry = EnsureType<JniRemappingIndexTypeEntry> (data);
				if (MonoAndroidHelper.StringEquals ("methods", fieldName)) {
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

				if (MonoAndroidHelper.StringEquals ("name", fieldName)) {
					return $" name: {entry.name.str}";
				}

				if (MonoAndroidHelper.StringEquals ("replacement", fieldName)) {
					return $" replacement: {entry.replacement.target_type}.{entry.replacement.target_name}";
				}

				if (MonoAndroidHelper.StringEquals ("signature", fieldName)) {
					if (entry.signature.length == 0) {
						return String.Empty;
					}

					return $"signature: {entry.signature.str}";
				}

				return String.Empty;
			}
		}

		sealed class JniRemappingIndexFieldTypeEntryContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var entry = EnsureType<JniRemappingIndexFieldTypeEntry> (data);

				if (MonoAndroidHelper.StringEquals ("name", fieldName)) {
					return $" name: {entry.name.str}";
				}

				return String.Empty;
			}

			public override string GetPointedToSymbolName (object data, string fieldName)
			{
				var entry = EnsureType<JniRemappingIndexFieldTypeEntry> (data);

				if (MonoAndroidHelper.StringEquals ("fields", fieldName)) {
					return entry.FieldsArraySymbolName;
				}

				return base.GetPointedToSymbolName (data, fieldName);
			}

			public override ulong GetBufferSize (object data, string fieldName)
			{
				var entry = EnsureType<JniRemappingIndexFieldTypeEntry> (data);
				if (MonoAndroidHelper.StringEquals ("fields", fieldName)) {
					return (ulong)entry.TypeFields.Count;
				}

				return 0;
			}
		}

		sealed class JniRemappingIndexFieldEntryContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var entry = EnsureType<JniRemappingIndexFieldEntry> (data);

				if (MonoAndroidHelper.StringEquals ("name", fieldName)) {
					return $" name: {entry.name.str}";
				}

				if (MonoAndroidHelper.StringEquals ("replacement", fieldName)) {
					return $" replacement: {entry.replacement.target_type}.{entry.replacement.target_name}";
				}

				if (MonoAndroidHelper.StringEquals ("signature", fieldName)) {
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

			[NativeAssembler (Ignore = true)]
			public byte[] utf8;
		};

		sealed class JniRemappingReplacementMethod
		{
			public string  target_type;
			public string  target_name;
			public string  target_signature;
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
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value - populated during native code generation
			public JniRemappingIndexMethodEntry methods;
#pragma warning restore CS0649

			[NativeAssembler (Ignore = true)]
			public string MethodsArraySymbolName;

			[NativeAssembler (Ignore = true)]
			public List<StructureInstance<JniRemappingIndexMethodEntry>> TypeMethods;
		};

		sealed class JniRemappingReplacementField
		{
			public string target_type;
			public string target_name;
			public string target_signature;
		};

		[NativeAssemblerStructContextDataProvider (typeof(JniRemappingIndexFieldEntryContextDataProvider))]
		sealed class JniRemappingIndexFieldEntry
		{
			[NativeAssembler (UsesDataProvider = true)]
			public JniRemappingString           name;

			[NativeAssembler (UsesDataProvider = true)]
			public JniRemappingString           signature;

			[NativeAssembler (UsesDataProvider = true)]
			public JniRemappingReplacementField replacement;
		};

		[NativeAssemblerStructContextDataProvider (typeof(JniRemappingIndexFieldTypeEntryContextDataProvider))]
		sealed class JniRemappingIndexFieldTypeEntry
		{
			[NativeAssembler (UsesDataProvider = true)]
			public JniRemappingString          name;
			public uint                        field_count;

			[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToSymbol = "")]
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value - populated during native code generation
			public JniRemappingIndexFieldEntry fields;
#pragma warning restore CS0649

			[NativeAssembler (Ignore = true)]
			public string FieldsArraySymbolName;

			[NativeAssembler (Ignore = true)]
			public List<StructureInstance<JniRemappingIndexFieldEntry>> TypeFields;
		};

		[NativeAssemblerStructContextDataProvider (typeof(JniRemappingTypeReplacementEntryContextDataProvider))]
		sealed class JniRemappingTypeReplacementEntry
		{
			[NativeAssembler (UsesDataProvider = true)]
			public JniRemappingString name;

			[NativeAssembler (UsesDataProvider = true)]
			public string    replacement;
		};

		readonly List<JniRemappingTypeReplacement> typeReplacementsInput;
		readonly List<JniRemappingTypeReplacement> reverseTypeReplacementsInput;
		readonly List<JniRemappingMethodReplacement> methodReplacementsInput;
		readonly List<JniRemappingFieldReplacement> fieldReplacementsInput;

		StructureInfo jniRemappingStringStructureInfo;
		StructureInfo jniRemappingReplacementMethodStructureInfo;
		StructureInfo jniRemappingIndexMethodEntryStructureInfo;
		StructureInfo jniRemappingIndexTypeEntryStructureInfo;
		StructureInfo jniRemappingReplacementFieldStructureInfo;
		StructureInfo jniRemappingIndexFieldEntryStructureInfo;
		StructureInfo jniRemappingIndexFieldTypeEntryStructureInfo;
		StructureInfo jniRemappingTypeReplacementEntryStructureInfo;

		public int ReplacementTypeCount { get; private set; }
		public int ReverseReplacementTypeCount { get; private set; }
		public int ReplacementMethodIndexEntryCount { get; private set; } = 0;
		public int ReplacementFieldIndexEntryCount { get; private set; }

		public JniRemappingAssemblyGenerator (TaskLoggingHelper log)
			: this (
				log,
				new List<JniRemappingTypeReplacement> (),
				new List<JniRemappingTypeReplacement> (),
				new List<JniRemappingMethodReplacement> (),
				new List<JniRemappingFieldReplacement> ()
			)
		{}

		public JniRemappingAssemblyGenerator (
			TaskLoggingHelper log,
			List<JniRemappingTypeReplacement> typeReplacements,
			List<JniRemappingTypeReplacement> reverseTypeReplacements,
			List<JniRemappingMethodReplacement> methodReplacements,
			List<JniRemappingFieldReplacement> fieldReplacements)
			: base (log)
		{
			this.typeReplacementsInput = typeReplacements ?? throw new ArgumentNullException (nameof (typeReplacements));
			this.reverseTypeReplacementsInput = reverseTypeReplacements ?? throw new ArgumentNullException (nameof (reverseTypeReplacements));
			this.methodReplacementsInput = methodReplacements ?? throw new ArgumentNullException (nameof (methodReplacements));
			this.fieldReplacementsInput = fieldReplacements ?? throw new ArgumentNullException (nameof (fieldReplacements));
		}

		(
			List<StructureInstance<JniRemappingTypeReplacementEntry>> TypeReplacements,
			List<StructureInstance<JniRemappingTypeReplacementEntry>> ReverseTypeReplacements,
			List<StructureInstance<JniRemappingIndexTypeEntry>> MethodIndexTypes,
			List<StructureInstance<JniRemappingIndexFieldTypeEntry>> FieldIndexTypes
		) Init ()
		{
			var typeReplacements = MakeTypeReplacements (typeReplacementsInput);
			var reverseTypeReplacements = MakeTypeReplacements (reverseTypeReplacementsInput);

			var methodIndexTypes = new List<StructureInstance<JniRemappingIndexTypeEntry>> ();
			var methodTypes = new Dictionary<string, StructureInstance<JniRemappingIndexTypeEntry>> (StringComparer.Ordinal);

			foreach (JniRemappingMethodReplacement mmr in methodReplacementsInput) {
				if (!methodTypes.TryGetValue (mmr.SourceType, out StructureInstance<JniRemappingIndexTypeEntry> typeEntry)) {
					var entry = new JniRemappingIndexTypeEntry {
						name = MakeJniRemappingString (mmr.SourceType),
						MethodsArraySymbolName = $"mm_{MakeIdentifier (mmr.SourceType)}",
						TypeMethods = new List<StructureInstance<JniRemappingIndexMethodEntry>> (),
					};

					typeEntry = new StructureInstance<JniRemappingIndexTypeEntry> (jniRemappingIndexTypeEntryStructureInfo, entry);
					methodIndexTypes.Add (typeEntry);
					methodTypes.Add (mmr.SourceType, typeEntry);
				}

				var method = new JniRemappingIndexMethodEntry {
					name = MakeJniRemappingString (mmr.SourceMethod),
					signature = MakeJniRemappingString (mmr.SourceMethodSignature),
					replacement = new JniRemappingReplacementMethod {
						target_type = mmr.TargetType,
						target_name = mmr.TargetMethod,
						target_signature = mmr.TargetMethodSignature,
						is_static = mmr.TargetIsStatic,
					},
				};

				typeEntry.Instance.TypeMethods.Add (new StructureInstance<JniRemappingIndexMethodEntry> (jniRemappingIndexMethodEntryStructureInfo, method));
			}

			foreach (var entry in methodTypes.Values) {
				entry.Instance.method_count = (uint)entry.Instance.TypeMethods.Count;
				entry.Instance.TypeMethods.Sort ((l, r) => CompareUtf8 (l.Instance.name, r.Instance.name));
			}
			methodIndexTypes.Sort ((l, r) => CompareUtf8 (l.Instance.name, r.Instance.name));

			var fieldIndexTypes = new List<StructureInstance<JniRemappingIndexFieldTypeEntry>> ();
			var fieldTypes = new Dictionary<string, StructureInstance<JniRemappingIndexFieldTypeEntry>> (StringComparer.Ordinal);

			foreach (JniRemappingFieldReplacement mfr in fieldReplacementsInput) {
				if (!fieldTypes.TryGetValue (mfr.SourceType, out StructureInstance<JniRemappingIndexFieldTypeEntry> typeEntry)) {
					var entry = new JniRemappingIndexFieldTypeEntry {
						name = MakeJniRemappingString (mfr.SourceType),
						FieldsArraySymbolName = $"mf_{MakeIdentifier (mfr.SourceType)}",
						TypeFields = new List<StructureInstance<JniRemappingIndexFieldEntry>> (),
					};

					typeEntry = new StructureInstance<JniRemappingIndexFieldTypeEntry> (jniRemappingIndexFieldTypeEntryStructureInfo, entry);
					fieldIndexTypes.Add (typeEntry);
					fieldTypes.Add (mfr.SourceType, typeEntry);
				}

				var field = new JniRemappingIndexFieldEntry {
					name = MakeJniRemappingString (mfr.SourceField),
					signature = MakeJniRemappingString (mfr.SourceFieldSignature),
					replacement = new JniRemappingReplacementField {
						target_type = mfr.TargetType,
						target_name = mfr.TargetField,
						target_signature = mfr.TargetFieldSignature,
					},
				};

				typeEntry.Instance.TypeFields.Add (new StructureInstance<JniRemappingIndexFieldEntry> (jniRemappingIndexFieldEntryStructureInfo, field));
			}

			foreach (var entry in fieldTypes.Values) {
				entry.Instance.field_count = (uint)entry.Instance.TypeFields.Count;
				entry.Instance.TypeFields.Sort ((l, r) => CompareUtf8 (l.Instance.name, r.Instance.name));
			}
			fieldIndexTypes.Sort ((l, r) => CompareUtf8 (l.Instance.name, r.Instance.name));

			ReplacementTypeCount = typeReplacements.Count;
			ReverseReplacementTypeCount = reverseTypeReplacements.Count;
			ReplacementMethodIndexEntryCount = methodIndexTypes.Count;
			ReplacementFieldIndexEntryCount = fieldIndexTypes.Count;

			return (typeReplacements, reverseTypeReplacements, methodIndexTypes, fieldIndexTypes);

			List<StructureInstance<JniRemappingTypeReplacementEntry>> MakeTypeReplacements (List<JniRemappingTypeReplacement> input)
			{
				var replacements = new List<StructureInstance<JniRemappingTypeReplacementEntry>> ();
				foreach (JniRemappingTypeReplacement replacement in input) {
					var entry = new JniRemappingTypeReplacementEntry {
						name = MakeJniRemappingString (replacement.From),
						replacement = replacement.To,
					};

					replacements.Add (new StructureInstance<JniRemappingTypeReplacementEntry> (jniRemappingTypeReplacementEntryStructureInfo, entry));
				}
				replacements.Sort ((l, r) => CompareUtf8 (l.Instance.name, r.Instance.name));
				return replacements;
			}

			string MakeIdentifier (string typeName)
			{
				return typeName.Replace ('/', '_').Replace ('$', '_');
			}
		}

		static JniRemappingString MakeJniRemappingString (string str)
		{
			byte[] utf8 = String.IsNullOrEmpty (str) ? [] : Encoding.UTF8.GetBytes (str);
			return new JniRemappingString {
				length = (uint)utf8.Length,
				str = str,
				utf8 = utf8,
			};
		}

		static int CompareUtf8 (JniRemappingString left, JniRemappingString right)
		{
			int count = Math.Min (left.utf8.Length, right.utf8.Length);
			for (int i = 0; i < count; i++) {
				int result = left.utf8 [i].CompareTo (right.utf8 [i]);
				if (result != 0)
					return result;
			}
			return left.utf8.Length.CompareTo (right.utf8.Length);
		}

		protected override void Construct (LlvmIrModule module)
		{
			module.DefaultStringGroup = "jremap";

			MapStructures (module);
			var data = Init ();

			AddTable (
				module,
				TypeReplacementsVariableName,
				data.TypeReplacements,
				jniRemappingTypeReplacementEntryStructureInfo,
				new JniRemappingTypeReplacementEntry ()
			);
			AddTable (
				module,
				ReverseTypeReplacementsVariableName,
				data.ReverseTypeReplacements,
				jniRemappingTypeReplacementEntryStructureInfo,
				new JniRemappingTypeReplacementEntry ()
			);

			foreach (StructureInstance<JniRemappingIndexTypeEntry> entry in data.MethodIndexTypes) {
				module.AddGlobalVariable (entry.Instance.MethodsArraySymbolName, entry.Instance.TypeMethods, LlvmIrVariableOptions.LocalConstant);
			}
			AddTable (
				module,
				MethodReplacementIndexVariableName,
				data.MethodIndexTypes,
				jniRemappingIndexTypeEntryStructureInfo,
				new JniRemappingIndexTypeEntry ()
			);

			foreach (StructureInstance<JniRemappingIndexFieldTypeEntry> entry in data.FieldIndexTypes) {
				module.AddGlobalVariable (entry.Instance.FieldsArraySymbolName, entry.Instance.TypeFields, LlvmIrVariableOptions.LocalConstant);
			}
			AddTable (
				module,
				FieldReplacementIndexVariableName,
				data.FieldIndexTypes,
				jniRemappingIndexFieldTypeEntryStructureInfo,
				new JniRemappingIndexFieldTypeEntry ()
			);

			module.AddGlobalVariable (TypeReplacementCountVariableName, (uint)ReplacementTypeCount, LlvmIrVariableOptions.GlobalConstant);
			module.AddGlobalVariable (ReverseTypeReplacementCountVariableName, (uint)ReverseReplacementTypeCount, LlvmIrVariableOptions.GlobalConstant);
			module.AddGlobalVariable (MethodReplacementIndexCountVariableName, (uint)ReplacementMethodIndexEntryCount, LlvmIrVariableOptions.GlobalConstant);
			module.AddGlobalVariable (FieldReplacementIndexCountVariableName, (uint)ReplacementFieldIndexEntryCount, LlvmIrVariableOptions.GlobalConstant);
		}

		static void AddTable<T> (
			LlvmIrModule module,
			string variableName,
			List<StructureInstance<T>> entries,
			StructureInfo structureInfo,
			T emptyEntry)
			where T : class
		{
			if (entries.Count > 0) {
				module.AddGlobalVariable (variableName, entries, LlvmIrVariableOptions.GlobalConstant);
				return;
			}

			module.AddGlobalVariable (
				typeof(StructureInstance<T>),
				variableName,
				new StructureInstance<T> (structureInfo, emptyEntry) { IsZeroInitialized = true },
				LlvmIrVariableOptions.GlobalConstant
			);
		}

		void MapStructures (LlvmIrModule module)
		{
			jniRemappingStringStructureInfo = module.MapStructure<JniRemappingString> ();
			jniRemappingReplacementMethodStructureInfo = module.MapStructure<JniRemappingReplacementMethod> ();
			jniRemappingIndexMethodEntryStructureInfo = module.MapStructure<JniRemappingIndexMethodEntry> ();
			jniRemappingIndexTypeEntryStructureInfo = module.MapStructure<JniRemappingIndexTypeEntry> ();
			jniRemappingReplacementFieldStructureInfo = module.MapStructure<JniRemappingReplacementField> ();
			jniRemappingIndexFieldEntryStructureInfo = module.MapStructure<JniRemappingIndexFieldEntry> ();
			jniRemappingIndexFieldTypeEntryStructureInfo = module.MapStructure<JniRemappingIndexFieldTypeEntry> ();
			jniRemappingTypeReplacementEntryStructureInfo = module.MapStructure<JniRemappingTypeReplacementEntry> ();
		}
	}
}
