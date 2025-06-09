using System;
using System.Collections.Generic;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class MarshalMethodsNativeAssemblyGeneratorMonoVM : MarshalMethodsNativeAssemblyGenerator
{
	readonly int numberOfAssembliesInApk;

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
