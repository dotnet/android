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

	class JniRemappingAssemblyGenerator : LlvmIrComposer
	{
		const string TypeReplacementsVariableName = "jni_remapping_type_replacements";
		const string MethodReplacementIndexVariableName = "jni_remapping_method_replacement_index";
		const string RemappingDataVariableName = "jni_remapping_data";

		sealed class JniRemappingDataContextDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetPointedToSymbolName (object data, string fieldName)
			{
				return fieldName switch {
					nameof (JniRemappingData.type_replacements) => TypeReplacementsVariableName,
					nameof (JniRemappingData.method_replacement_index) => MethodReplacementIndexVariableName,
					_ => base.GetPointedToSymbolName (data, fieldName),
				};
			}
		}

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
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value - populated during native code generation
			public JniRemappingIndexMethodEntry methods;
#pragma warning restore CS0649

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

		[NativeAssemblerStructContextDataProvider (typeof(JniRemappingDataContextDataProvider))]
		sealed class JniRemappingData
		{
			[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToSymbol = "")]
#pragma warning disable CS0649 // Field is populated during native code generation
			public JniRemappingTypeReplacementEntry type_replacements;

			[NativeAssembler (UsesDataProvider = true), NativePointer (PointsToSymbol = "")]
			public JniRemappingIndexTypeEntry method_replacement_index;
#pragma warning restore CS0649

			public uint type_replacement_count;
			public uint method_replacement_index_count;
		}

		List<JniRemappingTypeReplacement> typeReplacementsInput;
		List<JniRemappingMethodReplacement> methodReplacementsInput;

		StructureInfo jniRemappingStringStructureInfo;
		StructureInfo jniRemappingReplacementMethodStructureInfo;
		StructureInfo jniRemappingIndexMethodEntryStructureInfo;
		StructureInfo jniRemappingIndexTypeEntryStructureInfo;
		StructureInfo jniRemappingTypeReplacementEntryStructureInfo;
		StructureInfo jniRemappingDataStructureInfo;
		readonly Dictionary<string, byte []> utf8SortKeys = new Dictionary<string, byte []> (StringComparer.Ordinal);

		public int ReplacementTypeCount { get; private set; } = 0;
		public int ReplacementMethodIndexEntryCount { get; private set; } = 0;

		int CompareUtf8 (string left, string right)
		{
			byte [] leftBytes = GetUtf8SortKey (left);
			byte [] rightBytes = GetUtf8SortKey (right);
			int min = Math.Min (leftBytes.Length, rightBytes.Length);
			for (int i = 0; i < min; i++) {
				if (leftBytes [i] != rightBytes [i]) {
					return leftBytes [i] < rightBytes [i] ? -1 : 1;
				}
			}
			return leftBytes.Length.CompareTo (rightBytes.Length);
		}

		byte [] GetUtf8SortKey (string value)
		{
			if (!utf8SortKeys.TryGetValue (value, out byte [] bytes)) {
				bytes = Encoding.UTF8.GetBytes (value);
				utf8SortKeys.Add (value, bytes);
			}
			return bytes;
		}

		public JniRemappingAssemblyGenerator (TaskLoggingHelper log)
			: base (log)
		{}

		public JniRemappingAssemblyGenerator (TaskLoggingHelper log, List<JniRemappingTypeReplacement> typeReplacements, List<JniRemappingMethodReplacement> methodReplacements)
			: base (log)
		{
			this.typeReplacementsInput = typeReplacements ?? throw new ArgumentNullException (nameof (typeReplacements));
			this.methodReplacementsInput = methodReplacements ?? throw new ArgumentNullException (nameof (methodReplacements));
		}

		(List<StructureInstance<JniRemappingTypeReplacementEntry>>? typeReplacements, List<StructureInstance<JniRemappingIndexTypeEntry>>? methodIndexTypes) Init ()
		{
			if (typeReplacementsInput == null) {
				return (null, null);
			}

			var typeReplacements = new List<StructureInstance<JniRemappingTypeReplacementEntry>> ();
			foreach (JniRemappingTypeReplacement mtr in typeReplacementsInput) {
				var entry = new JniRemappingTypeReplacementEntry {
					name = MakeJniRemappingString (mtr.From),
					replacement = mtr.To,
				};

				typeReplacements.Add (new StructureInstance<JniRemappingTypeReplacementEntry> (jniRemappingTypeReplacementEntryStructureInfo, entry));
			}
			typeReplacements.Sort ((StructureInstance<JniRemappingTypeReplacementEntry> l, StructureInstance<JniRemappingTypeReplacementEntry> r) => CompareUtf8 (l.Instance.name.str, r.Instance.name.str));
			ReplacementTypeCount = typeReplacements.Count;

			var methodIndexTypes = new List<StructureInstance<JniRemappingIndexTypeEntry>> ();
			var types = new Dictionary<string, StructureInstance<JniRemappingIndexTypeEntry>> (StringComparer.Ordinal);

			foreach (JniRemappingMethodReplacement mmr in methodReplacementsInput) {
				if (!types.TryGetValue (mmr.SourceType, out StructureInstance<JniRemappingIndexTypeEntry> typeEntry)) {
					var entry = new JniRemappingIndexTypeEntry {
						name = MakeJniRemappingString (mmr.SourceType),
						MethodsArraySymbolName = MakeMethodsArrayName (mmr.SourceType),
						TypeMethods = new List<StructureInstance<JniRemappingIndexMethodEntry>> (),
					};

					typeEntry = new StructureInstance<JniRemappingIndexTypeEntry> (jniRemappingIndexTypeEntryStructureInfo, entry);
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

				typeEntry.Instance.TypeMethods.Add (new StructureInstance<JniRemappingIndexMethodEntry> (jniRemappingIndexMethodEntryStructureInfo, method));
			}

			foreach (var kvp in types) {
				kvp.Value.Instance.method_count = (uint)kvp.Value.Instance.TypeMethods.Count;
				kvp.Value.Instance.TypeMethods.Sort ((StructureInstance<JniRemappingIndexMethodEntry> l, StructureInstance<JniRemappingIndexMethodEntry> r) => CompareUtf8 (l.Instance.name.str, r.Instance.name.str));
			}

			methodIndexTypes.Sort ((StructureInstance<JniRemappingIndexTypeEntry> l, StructureInstance<JniRemappingIndexTypeEntry> r) => CompareUtf8 (l.Instance.name.str, r.Instance.name.str));
			ReplacementMethodIndexEntryCount = methodIndexTypes.Count;

			return (typeReplacements, methodIndexTypes);

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

			uint GetLength (string str)
			{
				if (String.IsNullOrEmpty (str)) {
					return 0;
				}

				return (uint)Encoding.UTF8.GetBytes (str).Length;
			}
		}

		protected override void Construct (LlvmIrModule module)
		{
			module.DefaultStringGroup = "jremap";

			MapStructures (module);
			List<StructureInstance<JniRemappingTypeReplacementEntry>>? typeReplacements;
			List<StructureInstance<JniRemappingIndexTypeEntry>>? methodIndexTypes;

			(typeReplacements, methodIndexTypes) = Init ();

			if (typeReplacements == null) {
				module.AddGlobalVariable (
					typeof(StructureInstance<JniRemappingTypeReplacementEntry>),
					TypeReplacementsVariableName,
					new StructureInstance<JniRemappingTypeReplacementEntry> (jniRemappingTypeReplacementEntryStructureInfo, new JniRemappingTypeReplacementEntry ()) { IsZeroInitialized = true },
					LlvmIrVariableOptions.GlobalConstant
				);

				module.AddGlobalVariable (
					typeof(StructureInstance<JniRemappingIndexTypeEntry>),
					MethodReplacementIndexVariableName,
					new StructureInstance<JniRemappingIndexTypeEntry> (jniRemappingIndexTypeEntryStructureInfo, new JniRemappingIndexTypeEntry ()) { IsZeroInitialized = true },
					LlvmIrVariableOptions.GlobalConstant
				);
				AddData (module);
				return;
			}

			module.AddGlobalVariable (TypeReplacementsVariableName, typeReplacements, LlvmIrVariableOptions.GlobalConstant);

			foreach (StructureInstance<JniRemappingIndexTypeEntry> entry in methodIndexTypes) {
				module.AddGlobalVariable (entry.Instance.MethodsArraySymbolName, entry.Instance.TypeMethods, LlvmIrVariableOptions.LocalConstant);
			}

			module.AddGlobalVariable (MethodReplacementIndexVariableName, methodIndexTypes, LlvmIrVariableOptions.GlobalConstant);
			AddData (module);
		}

		void AddData (LlvmIrModule module)
		{
			var data = new JniRemappingData {
				type_replacement_count = (uint)ReplacementTypeCount,
				method_replacement_index_count = (uint)ReplacementMethodIndexEntryCount,
			};
			module.AddGlobalVariable (
				RemappingDataVariableName,
				new StructureInstance<JniRemappingData> (jniRemappingDataStructureInfo, data),
				LlvmIrVariableOptions.GlobalConstant
			);
		}

		void MapStructures (LlvmIrModule module)
		{
			jniRemappingStringStructureInfo = module.MapStructure<JniRemappingString> ();
			jniRemappingReplacementMethodStructureInfo = module.MapStructure<JniRemappingReplacementMethod> ();
			jniRemappingIndexMethodEntryStructureInfo = module.MapStructure<JniRemappingIndexMethodEntry> ();
			jniRemappingIndexTypeEntryStructureInfo = module.MapStructure<JniRemappingIndexTypeEntry> ();
			jniRemappingTypeReplacementEntryStructureInfo = module.MapStructure<JniRemappingTypeReplacementEntry> ();
			jniRemappingDataStructureInfo = module.MapStructure<JniRemappingData> ();
		}
	}
}
