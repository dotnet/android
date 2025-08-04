#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class MarshalMethodsNativeAssemblyGeneratorMonoVM : MarshalMethodsNativeAssemblyGenerator
{
	sealed class MarshalMethodNameDataProvider : NativeAssemblerStructContextDataProvider
	{
		public override string GetComment (object data, string fieldName)
		{
			var methodName = EnsureType<MarshalMethodName> (data);

			if (MonoAndroidHelper.StringEquals ("id", fieldName)) {
				return $" name: {methodName.name}";
			}

			return String.Empty;
		}
	}

	[NativeAssemblerStructContextDataProvider (typeof(MarshalMethodNameDataProvider))]
	sealed class MarshalMethodName
	{
		[NativeAssembler (Ignore = true)]
		public ulong Id32;

		[NativeAssembler (Ignore = true)]
		public ulong Id64;

		[NativeAssembler (UsesDataProvider = true, NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
		public ulong  id;
		public string name = "";
	}

	readonly int numberOfAssembliesInApk;
	StructureInfo? marshalMethodNameStructureInfo;

	/// <summary>
	/// Constructor to be used ONLY when marshal methods are DISABLED
	/// </summary>
	public MarshalMethodsNativeAssemblyGeneratorMonoVM (TaskLoggingHelper log, AndroidTargetArch targetArch, int numberOfAssembliesInApk, ICollection<string> uniqueAssemblyNames)
		: base (log, targetArch, uniqueAssemblyNames)
	{
		this.numberOfAssembliesInApk = numberOfAssembliesInApk;
	}

	public MarshalMethodsNativeAssemblyGeneratorMonoVM (TaskLoggingHelper log, int numberOfAssembliesInApk, ICollection<string> uniqueAssemblyNames, NativeCodeGenStateObject codeGenState, bool managedMarshalMethodsLookupEnabled)
		: base (log, uniqueAssemblyNames, codeGenState, managedMarshalMethodsLookupEnabled)
	{
		this.numberOfAssembliesInApk = numberOfAssembliesInApk;
	}

	protected override void AddMarshalMethodNames (LlvmIrModule module, AssemblyCacheState acs)
	{
		var uniqueMethods = new Dictionary<ulong, (MarshalMethodInfo mmi, ulong id32, ulong id64)> ();

		if (!GenerateEmptyCode && Methods != null) {
			foreach (MarshalMethodInfo mmi in Methods) {
				string asmName = Path.GetFileName (mmi.Method.NativeCallback.DeclaringType.Module.Assembly.MainModuleFileName);

				if (!acs.AsmNameToIndexData32.TryGetValue (asmName, out uint idx32)) {
					throw new InvalidOperationException ($"Internal error: failed to match assembly name '{asmName}' to 32-bit cache array index");
				}

				if (!acs.AsmNameToIndexData64.TryGetValue (asmName, out uint idx64)) {
					throw new InvalidOperationException ($"Internal error: failed to match assembly name '{asmName}' to 64-bit cache array index");
				}

				ulong methodToken = (ulong)mmi.Method.NativeCallback.MetadataToken;
				ulong id32 = ((ulong)idx32 << 32) | methodToken;
				if (uniqueMethods.ContainsKey (id32)) {
					continue;
				}

				ulong id64 = ((ulong)idx64 << 32) | methodToken;
				uniqueMethods.Add (id32, (mmi, id32, id64));
			}
		}

		MarshalMethodName name;
		var methodName = new StringBuilder ();
		var mm_method_names = new List<StructureInstance<MarshalMethodName>> ();
		foreach (var kvp in uniqueMethods) {
			ulong id = kvp.Key;
			(MarshalMethodInfo mmi, ulong id32, ulong id64) = kvp.Value;

			RenderMethodNameWithParams (mmi.Method.NativeCallback, methodName);
			name = new MarshalMethodName {
				Id32 = id32,
				Id64 = id64,

				// Tokens are unique per assembly
				id = 0,
				name = methodName.ToString (),
			};
			mm_method_names.Add (new StructureInstance<MarshalMethodName> (marshalMethodNameStructureInfo!, name));
		}

		// Must terminate with an "invalid" entry
		name = new MarshalMethodName {
			Id32 = 0,
			Id64 = 0,

			id = 0,
			name = String.Empty,
		};
		mm_method_names.Add (new StructureInstance<MarshalMethodName> (marshalMethodNameStructureInfo!, name));

		var mm_method_names_variable = new LlvmIrGlobalVariable (mm_method_names, "mm_method_names", LlvmIrVariableOptions.GlobalConstant) {
			BeforeWriteCallback = UpdateMarshalMethodNameIds,
			BeforeWriteCallbackCallerState = acs,
		};
		module.Add (mm_method_names_variable);

		void RenderMethodNameWithParams (MarshalMethodEntryMethodObject md, StringBuilder buffer)
		{
			buffer.Clear ();
			buffer.Append (md.Name);
			buffer.Append ('(');

			if (md.HasParameters) {
				bool first = true;
				foreach (MarshalMethodEntryMethodParameterObject pd in md.Parameters) {
					if (!first) {
						buffer.Append (',');
					} else {
						first = false;
					}

					buffer.Append (pd.ParameterTypeName);
				}
			}

			buffer.Append (')');
		}
	}

	void UpdateMarshalMethodNameIds (LlvmIrVariable variable, LlvmIrModuleTarget target, object? callerState)
	{
		var mm_method_names = (List<StructureInstance<MarshalMethodName>>)variable.Value!;
		bool is64Bit = target.Is64Bit;

		foreach (StructureInstance<MarshalMethodName> mmn in mm_method_names) {
			mmn.Instance!.id = is64Bit ? mmn.Instance.Id64 : mmn.Instance.Id32;
		}
	}
	protected override void AddClassNames (LlvmIrModule module)
	{
		// Marshal methods class names
		var mm_class_names = new List<string> ();
		foreach (StructureInstance<MarshalMethodsManagedClass> klass in classes) {
			if (klass.Instance == null) {
				throw new InvalidOperationException ("Internal error: null class instance found");
			}

			mm_class_names.Add (klass.Instance.ClassName);
		}
		module.AddGlobalVariable ("mm_class_names", mm_class_names, LlvmIrVariableOptions.GlobalConstant, comment: " Names of classes in which marshal methods reside");
	}

	protected override void AddClassCache (LlvmIrModule module)
	{
		module.AddGlobalVariable ("marshal_methods_number_of_classes", (uint)classes.Count, LlvmIrVariableOptions.GlobalConstant);
		module.AddGlobalVariable ("marshal_methods_class_cache", classes, LlvmIrVariableOptions.GlobalWritable);
	}

	protected override void AddAssemblyImageCache (LlvmIrModule module, AssemblyCacheState acs)
	{
		var assembly_image_cache = new LlvmIrGlobalVariable (typeof(List<IntPtr>), "assembly_image_cache", LlvmIrVariableOptions.GlobalWritable) {
			ZeroInitializeArray = true,
			ArrayItemCount = (ulong)numberOfAssembliesInApk,
		};
		module.Add (assembly_image_cache);

		var assembly_image_cache_hashes = new LlvmIrGlobalVariable (typeof(List<ulong>), "assembly_image_cache_hashes", LlvmIrVariableOptions.GlobalConstant) {
			Comment = " Each entry maps hash of an assembly name to an index into the `assembly_image_cache` array",
			BeforeWriteCallback = UpdateAssemblyImageCacheHashes,
			BeforeWriteCallbackCallerState = acs,
			GetArrayItemCommentCallback = GetAssemblyImageCacheItemComment,
			GetArrayItemCommentCallbackCallerState = acs,
			NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal,
		};
		module.Add (assembly_image_cache_hashes);

		var assembly_image_cache_indices = new LlvmIrGlobalVariable (typeof(List<uint>), "assembly_image_cache_indices", LlvmIrVariableOptions.GlobalConstant) {
			WriteOptions = LlvmIrVariableWriteOptions.ArrayWriteIndexComments | LlvmIrVariableWriteOptions.ArrayFormatInRows,
			BeforeWriteCallback = UpdateAssemblyImageCacheIndices,
			BeforeWriteCallbackCallerState = acs,
		};
		module.Add (assembly_image_cache_indices);
	}

	protected override void MapStructures (LlvmIrModule module)
	{
		base.MapStructures (module);
		marshalMethodNameStructureInfo = module.MapStructure<MarshalMethodName> ();
	}

	void UpdateAssemblyImageCacheHashes (LlvmIrVariable variable, LlvmIrModuleTarget target, object? callerState)
	{
		AssemblyCacheState acs = EnsureAssemblyCacheState (callerState);
		object value;
		Type type;

		if (target.Is64Bit) {
			value = acs.Keys64;
			type = typeof(List<ulong>);
		} else {
			value = acs.Keys32;
			type = typeof(List<uint>);
		}

		LlvmIrGlobalVariable gv = EnsureGlobalVariable (variable);
		gv.OverrideTypeAndValue (type, value);
	}

	string? GetAssemblyImageCacheItemComment (LlvmIrVariable v, LlvmIrModuleTarget target, ulong index, object? value, object? callerState)
	{
		AssemblyCacheState acs = EnsureAssemblyCacheState (callerState);

		string name;
		uint i;
		if (target.Is64Bit) {
			var v64 = (ulong)value!;
			name = acs.Hashes64[v64].name;
			i = acs.Hashes64[v64].index;
		} else {
			var v32 = (uint)value!;
			name = acs.Hashes32[v32].name;
			i = acs.Hashes32[v32].index;
		}

		return $" {index}: {name} => {i}";
	}

	void UpdateAssemblyImageCacheIndices (LlvmIrVariable variable, LlvmIrModuleTarget target, object? callerState)
	{
		AssemblyCacheState acs = EnsureAssemblyCacheState (callerState);
		object value;

		if (target.Is64Bit) {
			value = acs.Indices64;
		} else {
			value = acs.Indices32;
		}

		LlvmIrGlobalVariable gv = EnsureGlobalVariable (variable);
		gv.OverrideTypeAndValue (variable.Type, value);
	}

	AssemblyCacheState EnsureAssemblyCacheState (object? callerState)
	{
		var acs = callerState as AssemblyCacheState;
		if (acs == null) {
			throw new InvalidOperationException ("Internal error: construction state expected but not found");
		}

		return acs;
	}
}
