#nullable enable

using System;
using System.Collections.Generic;

using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Manages lookup information for managed marshal methods that can be resolved at runtime.
/// This class builds hierarchical indexes (Assembly -> Class -> Method) to efficiently
/// locate native callback wrappers and their associated metadata. Used when 
/// EnableManagedMarshalMethodsLookup is enabled to support dynamic marshal method resolution.
/// </summary>
class ManagedMarshalMethodsLookupInfo (TaskLoggingHelper log)
{
	readonly TaskLoggingHelper _log = log;

	/// <summary>
	/// Gets the top-level lookup dictionary organized by assembly name.
	/// Each assembly contains classes, which in turn contain methods with their lookup information.
	/// </summary>
	public Dictionary<string, AssemblyLookupInfo> AssemblyLookup { get; } = new (StringComparer.Ordinal);

	/// <summary>
	/// Retrieves the hierarchical indexes (assembly, class, method) for a given native callback wrapper method.
	/// These indexes can be used at runtime to efficiently locate the method without string comparisons.
	/// </summary>
	/// <param name="nativeCallbackWrapper">The native callback wrapper method to get indexes for.</param>
	/// <returns>A tuple containing the assembly index, class index, and method index.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the assembly, class, or method is not found in the lookup indexes, or when
	/// the indexes have invalid values.
	/// </exception>
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

	/// <summary>
	/// Adds a native callback wrapper method to the lookup hierarchy.
	/// Creates the necessary assembly, class, and method entries if they don't exist.
	/// This method is called during the marshal method generation process to build the lookup tables.
	/// </summary>
	/// <param name="nativeCallbackWrapper">The native callback wrapper method to add.</param>
	public void AddNativeCallbackWrapper (MethodDefinition nativeCallbackWrapper)
	{
		var (assemblyName, className, methodName) = GetNames (nativeCallbackWrapper);

		// Get or create assembly info
		if (!AssemblyLookup.TryGetValue (assemblyName, out var assemblyInfo)) {
			AssemblyLookup [assemblyName] = assemblyInfo = new AssemblyLookupInfo ();
		}

		// Get or create class info
		if (!assemblyInfo.ClassLookup.TryGetValue (className, out var classInfo)) {
			assemblyInfo.ClassLookup [className] = classInfo = new ClassLookupInfo ();
		}

		// Get or create method info
		if (!classInfo.MethodLookup.TryGetValue (methodName, out var methodInfo)) {
			classInfo.MethodLookup [methodName] = methodInfo = new MethodLookupInfo ();
		} else {
			// Method already exists - this shouldn't normally happen
			_log.LogDebugMessage ($"Method '{assemblyName}'/'{className}'/'{methodName}' already has an associated UnmanagedCallersOnly method.");
			return;
		}

		// Populate the lookup info with the actual Cecil objects
		assemblyInfo.Assembly = nativeCallbackWrapper.DeclaringType.Module.Assembly;
		classInfo.DeclaringType = nativeCallbackWrapper.DeclaringType;
		methodInfo.NativeCallbackWrapper = nativeCallbackWrapper;
	}

	/// <summary>
	/// Extracts the assembly name, class name, and method name from a method definition.
	/// These names are used as keys in the hierarchical lookup structure.
	/// </summary>
	/// <param name="nativeCallback">The method definition to extract names from.</param>
	/// <returns>A tuple containing the assembly name, class name, and method name.</returns>
	private static (string, string, string) GetNames (MethodDefinition nativeCallback)
	{
		var type = nativeCallback.DeclaringType;
		var assemblyName = type.Module.Assembly.Name.Name;
		var className = type.FullName;
		var methodName = nativeCallback.Name;

		return (assemblyName, className, methodName);
	}

	/// <summary>
	/// Contains lookup information for an assembly, including its assigned index
	/// and the classes it contains.
	/// </summary>
	internal sealed class AssemblyLookupInfo
	{
		/// <summary>
		/// Gets or sets the assigned index for this assembly in the lookup tables.
		/// Initialized to uint.MaxValue to detect unassigned indexes.
		/// </summary>
		public uint Index { get; set; } = uint.MaxValue;
		
		/// <summary>
		/// Gets or sets the assembly definition associated with this lookup info.
		/// </summary>
		public AssemblyDefinition Assembly { get; set; } = null!;
		
		/// <summary>
		/// Gets or sets the method used to get function pointers for methods in this assembly.
		/// This may be null if the assembly doesn't have such a method.
		/// </summary>
		public MethodDefinition? GetFunctionPointerMethod { get; set; }
		
		/// <summary>
		/// Gets the lookup dictionary for classes within this assembly.
		/// </summary>
		public Dictionary<string, ClassLookupInfo> ClassLookup { get; } = new (StringComparer.Ordinal);
	}

	/// <summary>
	/// Contains lookup information for a class, including its assigned index
	/// and the methods it contains.
	/// </summary>
	internal sealed class ClassLookupInfo
	{
		/// <summary>
		/// Gets or sets the assigned index for this class in the lookup tables.
		/// Initialized to uint.MaxValue to detect unassigned indexes.
		/// </summary>
		public uint Index { get; set; } = uint.MaxValue;
		
		/// <summary>
		/// Gets or sets the type definition associated with this lookup info.
		/// </summary>
		public TypeDefinition DeclaringType { get; set; } = null!;
		
		/// <summary>
		/// Gets or sets the method used to get function pointers for methods in this class.
		/// This may be null if the class doesn't have such a method.
		/// </summary>
		public MethodDefinition? GetFunctionPointerMethod { get; set; }
		
		/// <summary>
		/// Gets the lookup dictionary for methods within this class.
		/// </summary>
		public Dictionary<string, MethodLookupInfo> MethodLookup { get; } = new (StringComparer.Ordinal);
	}

	/// <summary>
	/// Contains lookup information for a method, including its assigned index
	/// and the native callback wrapper method definition.
	/// </summary>
	internal sealed class MethodLookupInfo
	{
		/// <summary>
		/// Gets or sets the assigned index for this method in the lookup tables.
		/// Initialized to uint.MaxValue to detect unassigned indexes.
		/// </summary>
		public uint Index { get; set; } = uint.MaxValue;
		
		/// <summary>
		/// Gets or sets the native callback wrapper method definition.
		/// </summary>
		public MethodDefinition NativeCallbackWrapper { get; set; } = null!;
	}
}
