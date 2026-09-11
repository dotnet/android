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

		/// <summary>
		/// The JNI method descriptor to use on the target type, or <c>null</c> when the source
		/// signature is used unchanged. Remapping inputs which predate this attribute (for example
		/// the Intune/MAM mapping) leave it unset.
		/// </summary>
		public string TargetMethodSignature { get; }

		public bool TargetIsStatic { get; }

		public JniRemappingMethodReplacement (string sourceType, string sourceMethod, string sourceMethodSignature,
		                                      string targetType, string targetMethod, string targetMethodSignature,
		                                      bool targetIsStatic)
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

		// The runtime reads the table sizes from these symbols instead of `application_config`, so
		// that the same lookup implementation works in the NativeAOT build, which has no
		// application config at all.
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
		};

		sealed class JniRemappingReplacementMethod
		{
			public string  target_type;
			public string  target_name;
			public string  target_signature;
			public bool    is_static;
		};

		sealed class JniRemappingReplacementField
		{
			public string  target_type;
			public string  target_name;
			public string  target_signature;
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
			public uint               field_count;

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

		sealed class GeneratedTables
		{
			public List<StructureInstance<JniRemappingTypeReplacementEntry>> TypeReplacements;
			public List<StructureInstance<JniRemappingTypeReplacementEntry>> ReverseTypeReplacements;
			public List<StructureInstance<JniRemappingIndexTypeEntry>>       MethodIndexTypes;
			public List<StructureInstance<JniRemappingIndexFieldTypeEntry>>  FieldIndexTypes;
		}

		List<JniRemappingTypeReplacement> typeReplacementsInput;
		List<JniRemappingTypeReplacement> reverseTypeReplacementsInput;
		List<JniRemappingMethodReplacement> methodReplacementsInput;
		List<JniRemappingFieldReplacement> fieldReplacementsInput;

		StructureInfo jniRemappingStringStructureInfo;
		StructureInfo jniRemappingReplacementMethodStructureInfo;
		StructureInfo jniRemappingReplacementFieldStructureInfo;
		StructureInfo jniRemappingIndexMethodEntryStructureInfo;
		StructureInfo jniRemappingIndexTypeEntryStructureInfo;
		StructureInfo jniRemappingIndexFieldEntryStructureInfo;
		StructureInfo jniRemappingIndexFieldTypeEntryStructureInfo;
		StructureInfo jniRemappingTypeReplacementEntryStructureInfo;

		public int ReplacementTypeCount { get; private set; } = 0;
		public int ReverseTypeCount { get; private set; } = 0;
		public int ReplacementMethodIndexEntryCount { get; private set; } = 0;
		public int ReplacementFieldIndexEntryCount { get; private set; } = 0;

		public JniRemappingAssemblyGenerator (TaskLoggingHelper log)
			: base (log)
		{}

		public JniRemappingAssemblyGenerator (TaskLoggingHelper log,
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

		/// <summary>
		/// Orders UTF-8 encoded names exactly the way the native lookup's <c>memcmp</c>-based
		/// comparison does, so the runtime can binary-search the emitted tables.
		/// </summary>
		internal static int CompareUtf8 (byte [] left, byte [] right)
		{
			int min = Math.Min (left.Length, right.Length);
			for (int i = 0; i < min; i++) {
				if (left [i] != right [i]) {
					return left [i] < right [i] ? -1 : 1;
				}
			}

			if (left.Length == right.Length) {
				return 0;
			}

			return left.Length < right.Length ? -1 : 1;
		}

		static byte [] Utf8 (string str) => String.IsNullOrEmpty (str) ? Array.Empty<byte> () : Encoding.UTF8.GetBytes (str);

		GeneratedTables Init ()
		{
			if (typeReplacementsInput == null) {
				return null;
			}

			var ret = new GeneratedTables {
				TypeReplacements = MakeTypeReplacements (typeReplacementsInput),
				ReverseTypeReplacements = MakeTypeReplacements (reverseTypeReplacementsInput),
				MethodIndexTypes = MakeMethodIndex (),
				FieldIndexTypes = MakeFieldIndex (),
			};

			ReplacementTypeCount = ret.TypeReplacements.Count;
			ReverseTypeCount = ret.ReverseTypeReplacements.Count;
			ReplacementMethodIndexEntryCount = ret.MethodIndexTypes.Count;
			ReplacementFieldIndexEntryCount = ret.FieldIndexTypes.Count;

			return ret;
		}

		List<StructureInstance<JniRemappingTypeReplacementEntry>> MakeTypeReplacements (List<JniRemappingTypeReplacement> input)
		{
			var sorted = new List<(byte [] key, JniRemappingTypeReplacement replacement)> (input.Count);
			foreach (JniRemappingTypeReplacement tr in input) {
				sorted.Add ((Utf8 (tr.From), tr));
			}
			sorted.Sort ((l, r) => CompareUtf8 (l.key, r.key));

			var ret = new List<StructureInstance<JniRemappingTypeReplacementEntry>> (sorted.Count);
			foreach ((byte [] key, JniRemappingTypeReplacement tr) in sorted) {
				var entry = new JniRemappingTypeReplacementEntry {
					name = MakeJniRemappingString (tr.From, key),
					replacement = tr.To,
				};

				ret.Add (new StructureInstance<JniRemappingTypeReplacementEntry> (jniRemappingTypeReplacementEntryStructureInfo, entry));
			}

			return ret;
		}

		List<StructureInstance<JniRemappingIndexTypeEntry>> MakeMethodIndex ()
		{
			var types = new Dictionary<string, (byte [] key, List<(byte [] nameKey, byte [] signatureKey, JniRemappingMethodReplacement method)> methods)> (StringComparer.Ordinal);

			foreach (JniRemappingMethodReplacement mmr in methodReplacementsInput) {
				if (!types.TryGetValue (mmr.SourceType, out var typeEntry)) {
					typeEntry = (Utf8 (mmr.SourceType), new List<(byte [], byte [], JniRemappingMethodReplacement)> ());
					types.Add (mmr.SourceType, typeEntry);
				}

				typeEntry.methods.Add ((Utf8 (mmr.SourceMethod), Utf8 (mmr.SourceMethodSignature), mmr));
			}

			var sortedTypes = new List<KeyValuePair<string, (byte [] key, List<(byte [] nameKey, byte [] signatureKey, JniRemappingMethodReplacement method)> methods)>> (types);
			sortedTypes.Sort ((l, r) => CompareUtf8 (l.Value.key, r.Value.key));

			var ret = new List<StructureInstance<JniRemappingIndexTypeEntry>> (sortedTypes.Count);
			foreach (var kvp in sortedTypes) {
				var methods = kvp.Value.methods;
				// Overloads share a name, so the native lookup binary-searches the name and then
				// scans the equal-name run for a matching signature. Keep both keys in the sort.
				methods.Sort ((l, r) => {
					int cmp = CompareUtf8 (l.nameKey, r.nameKey);
					return cmp != 0 ? cmp : CompareUtf8 (l.signatureKey, r.signatureKey);
				});

				var typeMethods = new List<StructureInstance<JniRemappingIndexMethodEntry>> (methods.Count);
				foreach ((byte [] nameKey, byte [] signatureKey, JniRemappingMethodReplacement mmr) in methods) {
					var method = new JniRemappingIndexMethodEntry {
						name = MakeJniRemappingString (mmr.SourceMethod, nameKey),
						signature = MakeJniRemappingString (mmr.SourceMethodSignature, signatureKey),
						replacement = new JniRemappingReplacementMethod {
							target_type = mmr.TargetType,
							target_name = mmr.TargetMethod,
							target_signature = mmr.TargetMethodSignature,
							is_static = mmr.TargetIsStatic,
						},
					};

					typeMethods.Add (new StructureInstance<JniRemappingIndexMethodEntry> (jniRemappingIndexMethodEntryStructureInfo, method));
				}

				var entry = new JniRemappingIndexTypeEntry {
					name = MakeJniRemappingString (kvp.Key, kvp.Value.key),
					method_count = (uint)typeMethods.Count,
					MethodsArraySymbolName = MakeMembersArrayName ("mm", kvp.Key),
					TypeMethods = typeMethods,
				};

				ret.Add (new StructureInstance<JniRemappingIndexTypeEntry> (jniRemappingIndexTypeEntryStructureInfo, entry));
			}

			return ret;
		}

		List<StructureInstance<JniRemappingIndexFieldTypeEntry>> MakeFieldIndex ()
		{
			var types = new Dictionary<string, (byte [] key, List<(byte [] nameKey, byte [] signatureKey, JniRemappingFieldReplacement field)> fields)> (StringComparer.Ordinal);

			foreach (JniRemappingFieldReplacement mfr in fieldReplacementsInput) {
				if (!types.TryGetValue (mfr.SourceType, out var typeEntry)) {
					typeEntry = (Utf8 (mfr.SourceType), new List<(byte [], byte [], JniRemappingFieldReplacement)> ());
					types.Add (mfr.SourceType, typeEntry);
				}

				typeEntry.fields.Add ((Utf8 (mfr.SourceField), Utf8 (mfr.SourceFieldSignature), mfr));
			}

			var sortedTypes = new List<KeyValuePair<string, (byte [] key, List<(byte [] nameKey, byte [] signatureKey, JniRemappingFieldReplacement field)> fields)>> (types);
			sortedTypes.Sort ((l, r) => CompareUtf8 (l.Value.key, r.Value.key));

			var ret = new List<StructureInstance<JniRemappingIndexFieldTypeEntry>> (sortedTypes.Count);
			foreach (var kvp in sortedTypes) {
				var fields = kvp.Value.fields;
				fields.Sort ((l, r) => {
					int cmp = CompareUtf8 (l.nameKey, r.nameKey);
					return cmp != 0 ? cmp : CompareUtf8 (l.signatureKey, r.signatureKey);
				});

				var typeFields = new List<StructureInstance<JniRemappingIndexFieldEntry>> (fields.Count);
				foreach ((byte [] nameKey, byte [] signatureKey, JniRemappingFieldReplacement mfr) in fields) {
					var field = new JniRemappingIndexFieldEntry {
						name = MakeJniRemappingString (mfr.SourceField, nameKey),
						signature = MakeJniRemappingString (mfr.SourceFieldSignature, signatureKey),
						replacement = new JniRemappingReplacementField {
							target_type = mfr.TargetType,
							target_name = mfr.TargetField,
							target_signature = mfr.TargetFieldSignature,
						},
					};

					typeFields.Add (new StructureInstance<JniRemappingIndexFieldEntry> (jniRemappingIndexFieldEntryStructureInfo, field));
				}

				var entry = new JniRemappingIndexFieldTypeEntry {
					name = MakeJniRemappingString (kvp.Key, kvp.Value.key),
					field_count = (uint)typeFields.Count,
					FieldsArraySymbolName = MakeMembersArrayName ("mf", kvp.Key),
					TypeFields = typeFields,
				};

				ret.Add (new StructureInstance<JniRemappingIndexFieldTypeEntry> (jniRemappingIndexFieldTypeEntryStructureInfo, entry));
			}

			return ret;
		}

		static string MakeMembersArrayName (string prefix, string typeName)
		{
			return $"{prefix}_{typeName.Replace ('/', '_')}";
		}

		static JniRemappingString MakeJniRemappingString (string str, byte [] utf8)
		{
			return new JniRemappingString {
				length = (uint)utf8.Length,
				str = str,
			};
		}

		protected override void Construct (LlvmIrModule module)
		{
			module.DefaultStringGroup = "jremap";

			MapStructures (module);

			GeneratedTables tables = Init ();

			if (tables == null) {
				module.AddGlobalVariable (
					typeof(StructureInstance<JniRemappingTypeReplacementEntry>),
					TypeReplacementsVariableName,
					new StructureInstance<JniRemappingTypeReplacementEntry> (jniRemappingTypeReplacementEntryStructureInfo, new JniRemappingTypeReplacementEntry ()) { IsZeroInitialized = true },
					LlvmIrVariableOptions.GlobalConstant
				);

				module.AddGlobalVariable (
					typeof(StructureInstance<JniRemappingTypeReplacementEntry>),
					ReverseTypeReplacementsVariableName,
					new StructureInstance<JniRemappingTypeReplacementEntry> (jniRemappingTypeReplacementEntryStructureInfo, new JniRemappingTypeReplacementEntry ()) { IsZeroInitialized = true },
					LlvmIrVariableOptions.GlobalConstant
				);

				module.AddGlobalVariable (
					typeof(StructureInstance<JniRemappingIndexTypeEntry>),
					MethodReplacementIndexVariableName,
					new StructureInstance<JniRemappingIndexTypeEntry> (jniRemappingIndexTypeEntryStructureInfo, new JniRemappingIndexTypeEntry ()) { IsZeroInitialized = true },
					LlvmIrVariableOptions.GlobalConstant
				);

				module.AddGlobalVariable (
					typeof(StructureInstance<JniRemappingIndexFieldTypeEntry>),
					FieldReplacementIndexVariableName,
					new StructureInstance<JniRemappingIndexFieldTypeEntry> (jniRemappingIndexFieldTypeEntryStructureInfo, new JniRemappingIndexFieldTypeEntry ()) { IsZeroInitialized = true },
					LlvmIrVariableOptions.GlobalConstant
				);

				AddCounts (module);
				return;
			}

			module.AddGlobalVariable (TypeReplacementsVariableName, tables.TypeReplacements, LlvmIrVariableOptions.GlobalConstant);
			module.AddGlobalVariable (ReverseTypeReplacementsVariableName, tables.ReverseTypeReplacements, LlvmIrVariableOptions.GlobalConstant);

			foreach (StructureInstance<JniRemappingIndexTypeEntry> entry in tables.MethodIndexTypes) {
				module.AddGlobalVariable (entry.Instance.MethodsArraySymbolName, entry.Instance.TypeMethods, LlvmIrVariableOptions.LocalConstant);
			}

			module.AddGlobalVariable (MethodReplacementIndexVariableName, tables.MethodIndexTypes, LlvmIrVariableOptions.GlobalConstant);

			foreach (StructureInstance<JniRemappingIndexFieldTypeEntry> entry in tables.FieldIndexTypes) {
				module.AddGlobalVariable (entry.Instance.FieldsArraySymbolName, entry.Instance.TypeFields, LlvmIrVariableOptions.LocalConstant);
			}

			module.AddGlobalVariable (FieldReplacementIndexVariableName, tables.FieldIndexTypes, LlvmIrVariableOptions.GlobalConstant);

			AddCounts (module);
		}

		void AddCounts (LlvmIrModule module)
		{
			module.AddGlobalVariable (TypeReplacementCountVariableName, (uint)ReplacementTypeCount);
			module.AddGlobalVariable (ReverseTypeReplacementCountVariableName, (uint)ReverseTypeCount);
			module.AddGlobalVariable (MethodReplacementIndexCountVariableName, (uint)ReplacementMethodIndexEntryCount);
			module.AddGlobalVariable (FieldReplacementIndexCountVariableName, (uint)ReplacementFieldIndexEntryCount);
		}

		void MapStructures (LlvmIrModule module)
		{
			jniRemappingStringStructureInfo = module.MapStructure<JniRemappingString> ();
			jniRemappingReplacementMethodStructureInfo = module.MapStructure<JniRemappingReplacementMethod> ();
			jniRemappingReplacementFieldStructureInfo = module.MapStructure<JniRemappingReplacementField> ();
			jniRemappingIndexMethodEntryStructureInfo = module.MapStructure<JniRemappingIndexMethodEntry> ();
			jniRemappingIndexTypeEntryStructureInfo = module.MapStructure<JniRemappingIndexTypeEntry> ();
			jniRemappingIndexFieldEntryStructureInfo = module.MapStructure<JniRemappingIndexFieldEntry> ();
			jniRemappingIndexFieldTypeEntryStructureInfo = module.MapStructure<JniRemappingIndexFieldTypeEntry> ();
			jniRemappingTypeReplacementEntryStructureInfo = module.MapStructure<JniRemappingTypeReplacementEntry> ();
		}
	}
}
