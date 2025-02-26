using System;
using System.Collections.Generic;

using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

class ManagedMarshalMethodsLookupInfo (TaskLoggingHelper log)
{
	readonly TaskLoggingHelper _log = log;

	public Dictionary<string, AssemblyLookupInfo> AssemblyLookup { get; } = new (StringComparer.Ordinal);

	public (uint AssemblyIndex, uint ClassIndex, uint MethodIndex) GetIndex (MethodDefinition nativeCallbackWrapper)
	{
		var (assemblyName, className, methodName) = GetNames (nativeCallbackWrapper);

		if (!AssemblyLookup.TryGetValue (assemblyName, out var assemblyInfo)) {
			throw new InvalidOperationException ($"Assembly '{assemblyName}' not found in the lookup indexes.");
		}

		if (!assemblyInfo.ClassLookup.TryGetValue (className, out var classInfo)) {
			throw new InvalidOperationException ($"Class '{className}' not found in the lookup indexes.");
		}

		if (!classInfo.MethodLookup.TryGetValue (methodName, out var methodLookup)) {
			throw new InvalidOperationException ($"Method '{methodName}' not found in the lookup indexes.");
		}

		if (assemblyInfo.Index < 0 || classInfo.Index < 0 || methodLookup.Index < 0) {
			throw new InvalidOperationException ($"Invalid index values for {assemblyName}/{className}/{methodName}: {assemblyInfo.Index}, {classInfo.Index}, {methodLookup.Index}");
		}

		return (assemblyInfo.Index, classInfo.Index, methodLookup.Index);
	}

	public void AddNativeCallbackWrapper (MethodDefinition nativeCallbackWrapper)
	{
		var (assemblyName, className, methodName) = GetNames (nativeCallbackWrapper);

		if (!AssemblyLookup.TryGetValue (assemblyName, out var assemblyInfo)) {
			AssemblyLookup [assemblyName] = assemblyInfo = new AssemblyLookupInfo ();
		}

		if (!assemblyInfo.ClassLookup.TryGetValue (className, out var classInfo)) {
			assemblyInfo.ClassLookup [className] = classInfo = new ClassLookupInfo ();
		}

		if (!classInfo.MethodLookup.TryGetValue (methodName, out var methodInfo)) {
			classInfo.MethodLookup [methodName] = methodInfo = new MethodLookupInfo ();
		} else {
			_log.LogDebugMessage ($"Method '{assemblyName}'/'{className}'/'{methodName}' already has an associated UnmanagedCallersOnly method.");
			return;
		}

		assemblyInfo.Assembly = nativeCallbackWrapper.DeclaringType.Module.Assembly;
		classInfo.DeclaringType = nativeCallbackWrapper.DeclaringType;
		methodInfo.NativeCallbackWrapper = nativeCallbackWrapper;
	}

	private static (string, string, string) GetNames (MethodDefinition nativeCallback)
	{
		var type = nativeCallback.DeclaringType;
		var assemblyName = type.Module.Assembly.Name.Name;
		var className = type.FullName;
		var methodName = nativeCallback.Name;

		return (assemblyName, className, methodName);
	}

	internal sealed class AssemblyLookupInfo
	{
		public uint Index { get; set; } = uint.MaxValue;
		public AssemblyDefinition Assembly { get; set; }
		public MethodDefinition? GetFunctionPointerMethod { get; set; }
		public Dictionary<string, ClassLookupInfo> ClassLookup { get; } = new (StringComparer.Ordinal);
	}

	internal sealed class ClassLookupInfo
	{
		public uint Index { get; set; } = uint.MaxValue;
		public TypeDefinition DeclaringType { get; set; }
		public MethodDefinition? GetFunctionPointerMethod { get; set; }
		public Dictionary<string, MethodLookupInfo> MethodLookup { get; } = new (StringComparer.Ordinal);
	}

	internal sealed class MethodLookupInfo
	{
		public uint Index { get; set; } = uint.MaxValue;
		public MethodDefinition NativeCallbackWrapper { get; set; }
	}
}
