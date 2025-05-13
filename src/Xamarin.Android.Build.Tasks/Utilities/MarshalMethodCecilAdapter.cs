using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class MarshalMethodCecilAdapter
{
	public static NativeCodeGenStateCollection? GetNativeCodeGenStateCollection (TaskLoggingHelper log, ConcurrentDictionary<AndroidTargetArch, NativeCodeGenState>? nativeCodeGenStates)
	{
		if (nativeCodeGenStates is null) {
			log.LogDebugMessage ($"No {nameof (NativeCodeGenState)} found");
			return null;
		}

		var collection = new NativeCodeGenStateCollection ();

		// Convert each architecture
		foreach (var kvp in nativeCodeGenStates) {
			var arch = kvp.Key;
			var state = kvp.Value;
			var obj = CreateNativeCodeGenState (arch, state);
			obj.PinvokeInfos = state.PinvokeInfos;
			obj.TargetArch = state.TargetArch;
			collection.States.Add (arch, obj);
		}

		return collection;
	}

	static NativeCodeGenStateObject CreateNativeCodeGenState (AndroidTargetArch arch, NativeCodeGenState state)
	{
		var obj = new NativeCodeGenStateObject ();

		foreach (var type in state.JavaTypesForJCW) {
			if (JavaNativeTypeManager.IsApplication (type, state.TypeCache) || JavaNativeTypeManager.IsInstrumentation (type, state.TypeCache)) {
				if (state.Classifier != null && !state.Classifier.TypeHasDynamicallyRegisteredMethods (type)) {
					continue;
				}

				var jniName = JavaNativeTypeManager.ToJniName (type, state.TypeCache).Replace ('/', '.');
				var assemblyQualifiedName = type.GetAssemblyQualifiedName (state.TypeCache);

				obj.ApplicationsAndInstrumentationsToRegister.Add ((jniName, assemblyQualifiedName));
			}
		}

		if (state.Classifier is null)
			return obj;

		foreach (var group in state.Classifier.MarshalMethods) {
			var methods = new List<MarshalMethodEntryObject> (group.Value.Count);

			foreach (var method in group.Value) {
				var entry = CreateEntry (method, state.ManagedMarshalMethodsLookupInfo);
				methods.Add (entry);
			}

			obj.MarshalMethods.Add (group.Key, methods);
		}

		return obj;
	}

	static MarshalMethodEntryObject CreateEntry (MarshalMethodEntry entry, ManagedMarshalMethodsLookupInfo? info)
	{
		var obj = new MarshalMethodEntryObject (
			declaringType: CreateDeclaringType (entry.DeclaringType),
			implementedMethod: CreateMethod (entry.ImplementedMethod),
			isSpecial: entry.IsSpecial,
			jniTypeName: entry.JniTypeName,
			jniMethodName: entry.JniMethodName,
			jniMethodSignature: entry.JniMethodSignature,
			nativeCallback: CreateMethod (entry.NativeCallback),
			registeredMethod: CreateMethodBase (entry.RegisteredMethod)
		);

		if (info is not null) {
			(uint assemblyIndex, uint classIndex, uint methodIndex) = info.GetIndex (entry.NativeCallback);

			obj.NativeCallback.AssemblyIndex = assemblyIndex;
			obj.NativeCallback.ClassIndex = classIndex;
			obj.NativeCallback.MethodIndex = methodIndex;
		}

		return obj;
	}

	static MarshalMethodEntryTypeObject CreateDeclaringType (TypeDefinition type)
	{
		var cecilModule = type.Module;
		var cecilAssembly = cecilModule.Assembly;

		var assembly = new MarshalMethodEntryAssemblyObject (
			fullName: cecilAssembly.FullName,
			nameFullName: cecilAssembly.Name.FullName,
			mainModuleFileName: cecilAssembly.MainModule.FileName,
			nameName: cecilAssembly.Name.Name
		);

		var module = new MarshalMethodEntryModuleObject (
			assembly: assembly
		);

		return new MarshalMethodEntryTypeObject (
			fullName: type.FullName,
			metadataToken: type.MetadataToken.ToUInt32 (),
			module: module);
	}

	[return:NotNullIfNotNull (nameof (method))]
	static MarshalMethodEntryMethodObject? CreateMethod (MethodDefinition? method)
	{
		if (method is null)
			return null;

		var parameters = new List<MarshalMethodEntryMethodParameterObject> (method.Parameters.Count);

		foreach (var parameter in method.Parameters) {
			parameters.Add (new MarshalMethodEntryMethodParameterObject (
				name: parameter.Name,
				parameterTypeName: parameter.ParameterType.Name
			));
		}

		return new MarshalMethodEntryMethodObject (
			name: method.Name,
			fullName: method.FullName,
			declaringType: CreateDeclaringType (method.DeclaringType),
			metadataToken: method.MetadataToken.ToUInt32 (),
			parameters: parameters
		);
	}

	static MarshalMethodEntryMethodBaseObject? CreateMethodBase (MethodDefinition? method)
	{
		if (method is null)
			return null;
		return new MarshalMethodEntryMethodBaseObject (
			fullName: method.FullName
		);
	}
}

class NativeCodeGenStateCollection
{
	public Dictionary<AndroidTargetArch, NativeCodeGenStateObject> States { get; } = [];
}

class NativeCodeGenStateObject
{
	public Dictionary<string, IList<MarshalMethodEntryObject>> MarshalMethods { get; } = [];
	public List<PinvokeScanner.PinvokeEntryInfo>? PinvokeInfos                { get; set; }
	public AndroidTargetArch TargetArch                                       { get; set; } = AndroidTargetArch.None;
	public List<(string JniName, string AssemblyQualifiedName)> ApplicationsAndInstrumentationsToRegister { get; } = [];
}

class MarshalMethodEntryObject
{
	public MarshalMethodEntryTypeObject DeclaringType { get; }
	public MarshalMethodEntryMethodObject? ImplementedMethod { get; }
	public bool IsSpecial { get; }
	public string JniTypeName { get; }
	public string JniMethodName { get; }
	public string JniMethodSignature { get; }
	public MarshalMethodEntryMethodObject NativeCallback { get; }
	public MarshalMethodEntryMethodBaseObject? RegisteredMethod { get; }

	public MarshalMethodEntryObject (MarshalMethodEntryTypeObject declaringType, MarshalMethodEntryMethodObject? implementedMethod, bool isSpecial, string jniTypeName, string jniMethodName, string jniMethodSignature, MarshalMethodEntryMethodObject nativeCallback, MarshalMethodEntryMethodBaseObject? registeredMethod)
	{
		DeclaringType = declaringType;
		ImplementedMethod = implementedMethod;
		IsSpecial = isSpecial;
		JniTypeName = jniTypeName;
		JniMethodName = jniMethodName;
		JniMethodSignature = jniMethodSignature;
		NativeCallback = nativeCallback;
		RegisteredMethod = registeredMethod;
	}
}

class MarshalMethodEntryAssemblyObject
{
	public string FullName { get; }
	public string NameFullName { get; }  // Cecil's Assembly.Name.FullName
	public string MainModuleFileName { get; }  // Cecil's Assembly.MainModule.FileName
	public string NameName { get; } // Cecil's Module.Name.Name

	public MarshalMethodEntryAssemblyObject (string fullName, string nameFullName, string mainModuleFileName, string nameName)
	{
		FullName = fullName;
		NameFullName = nameFullName;
		MainModuleFileName = mainModuleFileName;
		NameName = nameName;
	}
}

class MarshalMethodEntryModuleObject
{
	public MarshalMethodEntryAssemblyObject Assembly { get; }

	public MarshalMethodEntryModuleObject (MarshalMethodEntryAssemblyObject assembly)
	{
		Assembly = assembly;
	}
}

class MarshalMethodEntryTypeObject
{
	public string FullName { get; }
	public uint MetadataToken { get; }
	public MarshalMethodEntryModuleObject Module { get; }

	public MarshalMethodEntryTypeObject (string fullName, uint metadataToken, MarshalMethodEntryModuleObject module)
	{
		FullName = fullName;
		MetadataToken = metadataToken;
		Module = module;
	}
}

class MarshalMethodEntryMethodBaseObject
{
	public string FullName { get; }

	public MarshalMethodEntryMethodBaseObject (string fullName)
	{
		FullName = fullName;
	}
}

class MarshalMethodEntryMethodObject : MarshalMethodEntryMethodBaseObject
{
	public string Name { get; }
	public MarshalMethodEntryTypeObject DeclaringType { get; }
	public uint MetadataToken { get; }
	public List<MarshalMethodEntryMethodParameterObject> Parameters { get; }

	public uint? AssemblyIndex { get; set; }
	public uint? ClassIndex { get; set; }
	public uint? MethodIndex { get; set; }

	public bool HasParameters => Parameters.Count > 0;

	public MarshalMethodEntryMethodObject (string name, string fullName, MarshalMethodEntryTypeObject declaringType, uint metadataToken, List<MarshalMethodEntryMethodParameterObject> parameters)
		: base (fullName)
	{
		Name = name;
		DeclaringType = declaringType;
		MetadataToken = metadataToken;
		Parameters = parameters;
	}
}

class MarshalMethodEntryMethodParameterObject
{
	public string Name { get; }
	public string ParameterTypeName { get; }  // Cecil's ParameterDefinition.ParameterType.Name

	public MarshalMethodEntryMethodParameterObject (string name, string parameterTypeName)
	{
		Name = name;
		ParameterTypeName = parameterTypeName;
	}
}
