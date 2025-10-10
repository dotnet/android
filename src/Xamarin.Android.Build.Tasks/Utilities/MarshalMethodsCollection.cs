#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Android.Runtime;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Collects and manages marshal methods from .NET assemblies, organizing them by type and
/// determining which can be converted to native LLVM marshal methods versus those that
/// must be registered dynamically. This class extends <see cref="JavaCallableMethodClassifier"/>
/// to integrate with the Java callable wrapper generation pipeline.
/// </summary>
class MarshalMethodsCollection : JavaCallableMethodClassifier
{
	/// <summary>
	/// Assemblies that contain convertible marshal methods. These assemblies need to
	/// have the proper assembly references added to them.
	/// </summary>
	public HashSet<AssemblyDefinition> AssembliesWithMarshalMethods { get; } = [];

	/// <summary>
	/// Marshal methods that have already been rewritten as LLVM marshal methods.
	/// </summary>
	public IDictionary<string, IList<ConvertedMarshalMethodEntry>> ConvertedMarshalMethods { get; } = new Dictionary<string, IList<ConvertedMarshalMethodEntry>> (StringComparer.Ordinal);

	/// <summary>
	/// Marshal methods that cannot be rewritten and must be registered dynamically.
	/// </summary>
	public List<DynamicallyRegisteredMarshalMethodEntry> DynamicallyRegisteredMarshalMethods { get; } = [];

	/// <summary>
	/// Marshal methods that can be rewritten as LLVM marshal methods.
	/// </summary>
	public IDictionary<string, IList<MarshalMethodEntry>> MarshalMethods { get; } = new Dictionary<string, IList<MarshalMethodEntry>> (StringComparer.Ordinal);

	readonly MarshalMethodsClassifier classifier;
	readonly TaskLoggingHelper log;
	readonly IAssemblyResolver resolver;
	readonly HashSet<TypeDefinition> typesWithDynamicallyRegisteredMarshalMethods = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="MarshalMethodsCollection"/> class.
	/// </summary>
	/// <param name="classifier">The marshal methods classifier to use for method classification.</param>
	/// <exception cref="ArgumentNullException">Thrown when classifier is null.</exception>
	public MarshalMethodsCollection (MarshalMethodsClassifier classifier)
	{
		this.classifier = classifier;
		log = classifier.Log;
		resolver = classifier.Resolver;
	}

	/// <summary>
	/// Adds marshal method entries for special case methods that won't be detected by the 
	/// standard JavaInterop type scanner. This primarily handles hand-written methods
	/// like Java.Interop.TypeManager+JavaTypeManager::n_Activate.
	/// </summary>
	/// <remarks>
	/// Special case methods are typically internal framework methods that don't follow
	/// the standard Java callable wrapper patterns but still need marshal method generation.
	/// </remarks>
	public void AddSpecialCaseMethods ()
	{
		AddTypeManagerSpecialCaseMethods ();
	}

	/// <summary>
	/// Checks a method for marshal method compatibility and adds it to the collection if suitable.
	/// This method examines the [Register] attributes on the method and filters out Kotlin-mangled
	/// methods that cannot be properly overridden.
	/// </summary>
	/// <param name="collection">The collection to add the method to.</param>
	/// <param name="type">The top-level type that owns this method.</param>
	/// <param name="registeredMethod">The method that was registered with [Register] attributes.</param>
	/// <param name="implementedMethod">The actual method implementation.</param>
	/// <param name="methodClassifier">The classifier to use for method classification.</param>
	/// <param name="cache">The type definition cache for resolving types.</param>
	static void CheckMethod (MarshalMethodsCollection collection, TypeDefinition type, MethodDefinition registeredMethod, MethodDefinition implementedMethod, MarshalMethodsClassifier methodClassifier, TypeDefinitionCache cache)
	{
		foreach (RegisterAttribute attr in CecilExtensions.GetMethodRegistrationAttributes (registeredMethod)) {
			// Check for Kotlin-mangled methods that cannot be overridden
			// These methods have names ending with "-impl" or contain a hyphen 8 characters from the end
			if (attr.Name.Contains ("-impl") || (attr.Name.Length > 7 && attr.Name [attr.Name.Length - 8] == '-'))
				continue;

			collection.AddMethod (type, registeredMethod, implementedMethod, attr.OriginAttribute);
		}
	}

	/// <summary>
	/// Creates a <see cref="MarshalMethodsCollection"/> by scanning the specified assemblies for
	/// marshal method candidates. This is the main entry point for building a complete collection
	/// of marshal methods from a set of .NET assemblies.
	/// </summary>
	/// <param name="arch">The target Android architecture.</param>
	/// <param name="assemblies">The list of assemblies to scan for marshal methods.</param>
	/// <param name="resolver">The assembly resolver for loading referenced assemblies.</param>
	/// <param name="log">The logging helper for diagnostic messages.</param>
	/// <returns>A populated <see cref="MarshalMethodsCollection"/> containing all discovered marshal methods.</returns>
	public static MarshalMethodsCollection FromAssemblies (AndroidTargetArch arch, List<ITaskItem> assemblies, XAAssemblyResolver resolver, TaskLoggingHelper log)
	{
		var cache = new TypeDefinitionCache ();
		var classifier = new MarshalMethodsClassifier (cache, resolver, log);
		var collection = new MarshalMethodsCollection (classifier);
		var scanner = new XAJavaTypeScanner (arch, log, cache);

		// Get all Java-callable types from the assemblies
		var javaTypes = scanner.GetJavaTypes (assemblies, resolver, []);

		// Scan each type for marshal method candidates
		foreach (var type in javaTypes) {
			// Skip interfaces and types that shouldn't have Java callable wrappers
			if (type.IsInterface || JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (type, cache))
				continue;

			ScanTypeForMarshalMethods (type, collection, resolver, cache, log, classifier);
		}

		return collection;
	}

	/// <summary>
	/// Scans a specific type definition for marshal method candidates, including both
	/// direct methods and methods inherited from implemented interfaces.
	/// </summary>
	/// <param name="type">The type definition to scan.</param>
	/// <param name="collection">The collection to add discovered marshal methods to.</param>
	/// <param name="resolver">The assembly resolver for loading referenced types.</param>
	/// <param name="cache">The type definition cache for resolving types.</param>
	/// <param name="log">The logging helper for diagnostic messages.</param>
	/// <param name="classifier">The classifier to use for method classification.</param>
	static void ScanTypeForMarshalMethods (TypeDefinition type, MarshalMethodsCollection collection, XAAssemblyResolver resolver, TypeDefinitionCache cache, TaskLoggingHelper log, MarshalMethodsClassifier classifier)
	{
		// Scan direct methods (excluding constructors)
		foreach (var minfo in type.Methods.Where (m => !m.IsConstructor)) {
			var baseRegisteredMethod = CecilExtensions.GetBaseRegisteredMethod (minfo, cache);

			if (baseRegisteredMethod is not null)
				CheckMethod (collection, type, baseRegisteredMethod, minfo, classifier, cache);
		}

		// Scan methods from implemented interfaces
		foreach (InterfaceImplementation ifaceInfo in type.Interfaces) {
			var typeReference = ifaceInfo.InterfaceType;
			var typeDefinition = cache.Resolve (typeReference);

			if (typeDefinition is null) {
				Diagnostic.Error (4204,
					CecilExtensions.LookupSource (type),
					Java.Interop.Localization.Resources.JavaCallableWrappers_XA4204,
					typeReference.FullName);
			}

			// Only process interfaces that have [Register] attributes
			if (!CecilExtensions.GetTypeRegistrationAttributes (typeDefinition).Any ())
				continue;

			// Check all non-static methods in the interface
			foreach (MethodDefinition imethod in typeDefinition.Methods) {
				if (imethod.IsStatic)
					continue;

				CheckMethod (collection, type, imethod, imethod, classifier, cache);
			}
		}
	}

	/// <summary>
	/// Determines whether a method should be registered dynamically rather than converted to a marshal method.
	/// This method is called by the Java callable wrapper generation pipeline to make registration decisions.
	/// </summary>
	/// <param name="topType">The top-level type that owns this method.</param>
	/// <param name="registeredMethod">The method that was registered with the [Register] attribute.</param>
	/// <param name="implementedMethod">The actual method implementation.</param>
	/// <param name="registerAttribute">The [Register] attribute applied to the method.</param>
	/// <returns>
	/// true if the method should be registered dynamically; false if it can be converted to a marshal method.
	/// </returns>
	public override bool ShouldBeDynamicallyRegistered (TypeDefinition topType, MethodDefinition registeredMethod, MethodDefinition implementedMethod, CustomAttribute? registerAttribute)
	{
		var method = AddMethod (topType, registeredMethod, implementedMethod, registerAttribute);

		return method is DynamicallyRegisteredMarshalMethodEntry;
	}

	/// <summary>
	/// Determines whether a type has any methods that must be registered dynamically.
	/// This is used during code generation to decide whether to include dynamic registration logic.
	/// </summary>
	/// <param name="type">The type to check.</param>
	/// <returns>true if the type has dynamically registered methods; otherwise, false.</returns>
	public bool TypeHasDynamicallyRegisteredMethods (TypeDefinition type)
	{
		return typesWithDynamicallyRegisteredMarshalMethods.Contains (type);
	}

	/// <summary>
	/// Adds a method to the appropriate collection based on its classification result.
	/// This method coordinates with the classifier to determine the method's type and
	/// stores it in the correct collection with proper tracking of associated assemblies.
	/// </summary>
	/// <param name="topType">The top-level type that owns this method.</param>
	/// <param name="registeredMethod">The method that was registered with the [Register] attribute.</param>
	/// <param name="implementedMethod">The actual method implementation.</param>
	/// <param name="registerAttribute">The [Register] attribute applied to the method.</param>
	/// <returns>The method entry that was added to the collection.</returns>
	MethodEntry AddMethod (TypeDefinition topType, MethodDefinition registeredMethod, MethodDefinition implementedMethod, CustomAttribute? registerAttribute)
	{
		// Classify the method using the classifier
		var marshalMethod = classifier.ClassifyMethod (topType, registeredMethod, implementedMethod, registerAttribute);

		// Handle dynamically registered methods
		if (marshalMethod is DynamicallyRegisteredMarshalMethodEntry dynamicMethod) {
			DynamicallyRegisteredMarshalMethods.Add (dynamicMethod);
			typesWithDynamicallyRegisteredMarshalMethods.Add (topType);

			return dynamicMethod;
		}

		// Handle converted marshal methods (already processed)
		if (marshalMethod is ConvertedMarshalMethodEntry convertedMethod) {
			var key = convertedMethod.GetStoreMethodKey (classifier.TypeDefinitionCache);

			if (!ConvertedMarshalMethods.TryGetValue (key, out var list)) {
				list = new List<ConvertedMarshalMethodEntry> ();
				ConvertedMarshalMethods.Add (key, list);
			}

			list.Add (convertedMethod);

			return convertedMethod;
		}

		// Handle standard marshal methods
		if (marshalMethod is MarshalMethodEntry marshalMethodEntry) {
			var key = marshalMethodEntry.GetStoreMethodKey (classifier.TypeDefinitionCache);

			if (!MarshalMethods.TryGetValue (key, out var list)) {
				list = new List<MarshalMethodEntry> ();
				MarshalMethods.Add (key, list);
			}

			list.Add (marshalMethodEntry);

			// Track assemblies that contain marshal methods - these need special handling
			AssembliesWithMarshalMethods.Add (marshalMethodEntry.NativeCallback.Module.Assembly);

			if (marshalMethodEntry.Connector is not null)
				AssembliesWithMarshalMethods.Add (marshalMethodEntry.Connector.Module.Assembly);

			if (marshalMethodEntry.CallbackField is not null)
				AssembliesWithMarshalMethods.Add (marshalMethodEntry.CallbackField.Module.Assembly);
		}

		return marshalMethod;
	}

	/// <summary>
	/// Adds special case marshal methods for the TypeManager class. The TypeManager contains
	/// hand-written native callback methods that are essential for the Android runtime but
	/// don't follow the standard Java callable wrapper patterns.
	/// </summary>
	/// <remarks>
	/// The TypeManager's n_Activate method is critical for object activation in the Android runtime.
	/// This method manually creates marshal method entries for these special cases that would
	/// otherwise be missed by the standard type scanning process.
	/// </remarks>
	void AddTypeManagerSpecialCaseMethods ()
	{
		const string FullTypeName = "Java.Interop.TypeManager+JavaTypeManager, Mono.Android";

		// Resolve the TypeManager types from Mono.Android
		AssemblyDefinition? monoAndroid = resolver.Resolve ("Mono.Android");
		TypeDefinition? typeManager = monoAndroid?.MainModule.FindType ("Java.Interop.TypeManager");
		TypeDefinition? javaTypeManager = typeManager?.GetNestedType ("JavaTypeManager");

		if (javaTypeManager == null) {
			throw new InvalidOperationException ($"Internal error: unable to find the {FullTypeName} type in the Mono.Android assembly");
		}

		MethodDefinition? nActivate_mm = null;
		MethodDefinition? nActivate = null;

		// Find the n_Activate methods - both the marshal method and the original
		foreach (MethodDefinition method in javaTypeManager.Methods) {
			if (nActivate_mm == null && IsMatchingMethod (method, "n_Activate_mm")) {
				// Ensure the marshal method has the [UnmanagedCallersOnly] attribute
				if (method.GetCustomAttributes ("System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute").Any (cattr => cattr != null)) {
					nActivate_mm = method;
				} else {
					log.LogWarning ($"Method '{method.FullName}' isn't decorated with the UnmanagedCallersOnly attribute");
					continue;
				}
			}

			if (nActivate == null && IsMatchingMethod (method, "n_Activate")) {
				nActivate = method;
			}

			// Stop searching once we've found both methods
			if (nActivate_mm != null && nActivate != null) {
				break;
			}
		}

		// Ensure we found the required methods
		if (nActivate_mm == null) {
			ThrowMissingMethod ("nActivate_mm");
		}

		if (nActivate == null) {
			ThrowMissingMethod ("nActivate");
		}

		// Extract JNI information from [Register] attributes
		string? jniTypeName = null;
		foreach (CustomAttribute cattr in javaTypeManager.GetCustomAttributes ("Android.Runtime.RegisterAttribute")) {
			if (cattr.ConstructorArguments.Count != 1) {
				log.LogDebugMessage ($"[Register] attribute on type '{FullTypeName}' is expected to have 1 constructor argument, found {cattr.ConstructorArguments.Count}");
				continue;
			}

			jniTypeName = (string) cattr.ConstructorArguments [0].Value;
			if (!String.IsNullOrEmpty (jniTypeName)) {
				break;
			}
		}

		string? jniMethodName = null;
		string? jniSignature = null;
		foreach (CustomAttribute cattr in nActivate.GetCustomAttributes ("Android.Runtime.RegisterAttribute")) {
			if (cattr.ConstructorArguments.Count != 3) {
				log.LogDebugMessage ($"[Register] attribute on method '{nActivate.FullName}' is expected to have 3 constructor arguments, found {cattr.ConstructorArguments.Count}");
				continue;
			}

			jniMethodName = (string) cattr.ConstructorArguments [0].Value;
			jniSignature = (string) cattr.ConstructorArguments [1].Value;

			if (!String.IsNullOrEmpty (jniMethodName) && !String.IsNullOrEmpty (jniSignature)) {
				break;
			}
		}

		// Validate that we have all required JNI information
		bool missingInfo = false;
		if (String.IsNullOrEmpty (jniTypeName)) {
			missingInfo = true;
			log.LogDebugMessage ($"Failed to obtain Java type name from the [Register] attribute on type '{FullTypeName}'");
		}

		if (String.IsNullOrEmpty (jniMethodName)) {
			missingInfo = true;
			log.LogDebugMessage ($"Failed to obtain Java method name from the [Register] attribute on method '{nActivate.FullName}'");
		}

		if (String.IsNullOrEmpty (jniSignature)) {
			missingInfo = true;
			log.LogDebugMessage ($"Failed to obtain Java method signature from the [Register] attribute on method '{nActivate.FullName}'");
		}

		if (missingInfo) {
			throw new InvalidOperationException ($"Missing information while constructing marshal method for the '{nActivate_mm.FullName}' method");
		}

		// Create the special case marshal method entry
		var entry = new MarshalMethodEntry (javaTypeManager, nActivate_mm, jniTypeName!, jniMethodName!, jniSignature!);  // NRT- Guarded above
		MarshalMethods.Add (".:!SpEcIaL:Java.Interop.TypeManager+JavaTypeManager::n_Activate_mm", new List<MarshalMethodEntry> { entry });

		/// <summary>
		/// Throws an exception indicating that a required method was not found.
		/// This is a local function used for error handling within AddTypeManagerSpecialCaseMethods.
		/// </summary>
		/// <param name="name">The name of the missing method.</param>
		[DoesNotReturn]
		void ThrowMissingMethod (string name)
		{
			throw new InvalidOperationException ($"Internal error: unable to find the '{name}' method in the '{FullTypeName}' type");
		}

		/// <summary>
		/// Checks if a method matches the expected criteria for TypeManager special case methods.
		/// The method must have the correct name, be static, and be private.
		/// </summary>
		/// <param name="method">The method to check.</param>
		/// <param name="name">The expected method name.</param>
		/// <returns>true if the method matches all criteria; otherwise, false.</returns>
		bool IsMatchingMethod (MethodDefinition method, string name)
		{
			if (!MonoAndroidHelper.StringEquals (name, method.Name)) {
				return false;
			}

			if (!method.IsStatic) {
				log.LogWarning ($"Method '{method.FullName}' is not static");
				return false;
			}

			if (!method.IsPrivate) {
				log.LogWarning ($"Method '{method.FullName}' is not private");
				return false;
			}

			return true;
		}
	}
}
