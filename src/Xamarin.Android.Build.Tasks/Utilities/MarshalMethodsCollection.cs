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

	public MarshalMethodsCollection (MarshalMethodsClassifier classifier)
	{
		this.classifier = classifier;
		log = classifier.Log;
		resolver = classifier.Resolver;
	}

	/// <summary>
	/// Adds MarshalMethodEntry for each method that won't be returned by the JavaInterop type scanner, mostly
	/// used for hand-written methods (e.g. Java.Interop.TypeManager+JavaTypeManager::n_Activate)
	/// </summary>
	public void AddSpecialCaseMethods ()
	{
		AddTypeManagerSpecialCaseMethods ();
	}

	static void CheckMethod (MarshalMethodsCollection collection, TypeDefinition type, MethodDefinition registeredMethod, MethodDefinition implementedMethod, MarshalMethodsClassifier methodClassifier, TypeDefinitionCache cache)
	{
		foreach (RegisterAttribute attr in CecilExtensions.GetMethodRegistrationAttributes (registeredMethod)) {
			// Check for Kotlin-mangled methods that cannot be overridden
			if (attr.Name.Contains ("-impl") || (attr.Name.Length > 7 && attr.Name [attr.Name.Length - 8] == '-'))
				continue;

			collection.AddMethod (type, registeredMethod, implementedMethod, attr.OriginAttribute);
		}
	}

	public static MarshalMethodsCollection FromAssemblies (AndroidTargetArch arch, List<ITaskItem> assemblies, XAAssemblyResolver resolver, TaskLoggingHelper log)
	{
		var cache = new TypeDefinitionCache ();
		var classifier = new MarshalMethodsClassifier (cache, resolver, log);
		var collection = new MarshalMethodsCollection (classifier);
		var scanner = new XAJavaTypeScanner (arch, log, cache);

		var javaTypes = scanner.GetJavaTypes (assemblies, resolver, []);

		foreach (var type in javaTypes) {
			if (type.IsInterface || JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (type, cache))
				continue;

			ScanTypeForMarshalMethods (type, collection, resolver, cache, log, classifier);
		}

		return collection;
	}

	static void ScanTypeForMarshalMethods (TypeDefinition type, MarshalMethodsCollection collection, XAAssemblyResolver resolver, TypeDefinitionCache cache, TaskLoggingHelper log, MarshalMethodsClassifier classifier)
	{
		// Methods
		foreach (var minfo in type.Methods.Where (m => !m.IsConstructor)) {
			var baseRegisteredMethod = CecilExtensions.GetBaseRegisteredMethod (minfo, cache);

			if (baseRegisteredMethod is not null)
				CheckMethod (collection, type, baseRegisteredMethod, minfo, classifier, cache);
		}

		// Methods from interfaces
		foreach (InterfaceImplementation ifaceInfo in type.Interfaces) {
			var typeReference = ifaceInfo.InterfaceType;
			var typeDefinition = cache.Resolve (typeReference);

			if (typeDefinition is null) {
				Diagnostic.Error (4204,
					CecilExtensions.LookupSource (type),
					Java.Interop.Localization.Resources.JavaCallableWrappers_XA4204,
					typeReference.FullName);
			}

			if (!CecilExtensions.GetTypeRegistrationAttributes (typeDefinition).Any ())
				continue;

			foreach (MethodDefinition imethod in typeDefinition.Methods) {
				if (imethod.IsStatic)
					continue;

				CheckMethod (collection, type, imethod, imethod, classifier, cache);
			}
		}
	}

	public override bool ShouldBeDynamicallyRegistered (TypeDefinition topType, MethodDefinition registeredMethod, MethodDefinition implementedMethod, CustomAttribute? registerAttribute)
	{
		var method = AddMethod (topType, registeredMethod, implementedMethod, registerAttribute);

		return method is DynamicallyRegisteredMarshalMethodEntry;
	}

	public bool TypeHasDynamicallyRegisteredMethods (TypeDefinition type)
	{
		return typesWithDynamicallyRegisteredMarshalMethods.Contains (type);
	}

	MethodEntry AddMethod (TypeDefinition topType, MethodDefinition registeredMethod, MethodDefinition implementedMethod, CustomAttribute? registerAttribute)
	{
		var marshalMethod = classifier.ClassifyMethod (topType, registeredMethod, implementedMethod, registerAttribute);

		if (marshalMethod is DynamicallyRegisteredMarshalMethodEntry dynamicMethod) {
			DynamicallyRegisteredMarshalMethods.Add (dynamicMethod);
			typesWithDynamicallyRegisteredMarshalMethods.Add (topType);

			return dynamicMethod;
		}

		if (marshalMethod is ConvertedMarshalMethodEntry convertedMethod) {
			var key = convertedMethod.GetStoreMethodKey (classifier.TypeDefinitionCache);

			if (!ConvertedMarshalMethods.TryGetValue (key, out var list)) {
				list = new List<ConvertedMarshalMethodEntry> ();
				ConvertedMarshalMethods.Add (key, list);
			}

			list.Add (convertedMethod);

			return convertedMethod;
		}

		if (marshalMethod is MarshalMethodEntry marshalMethodEntry) {
			var key = marshalMethodEntry.GetStoreMethodKey (classifier.TypeDefinitionCache);

			if (!MarshalMethods.TryGetValue (key, out var list)) {
				list = new List<MarshalMethodEntry> ();
				MarshalMethods.Add (key, list);
			}

			list.Add (marshalMethodEntry);

			AssembliesWithMarshalMethods.Add (marshalMethodEntry.NativeCallback.Module.Assembly);

			if (marshalMethodEntry.Connector is not null)
				AssembliesWithMarshalMethods.Add (marshalMethodEntry.Connector.Module.Assembly);

			if (marshalMethodEntry.CallbackField is not null)
				AssembliesWithMarshalMethods.Add (marshalMethodEntry.CallbackField.Module.Assembly);
		}

		return marshalMethod;
	}

	void AddTypeManagerSpecialCaseMethods ()
	{
		const string FullTypeName = "Java.Interop.TypeManager+JavaTypeManager, Mono.Android";

		AssemblyDefinition? monoAndroid = resolver.Resolve ("Mono.Android");
		TypeDefinition? typeManager = monoAndroid?.MainModule.FindType ("Java.Interop.TypeManager");
		TypeDefinition? javaTypeManager = typeManager?.GetNestedType ("JavaTypeManager");

		if (javaTypeManager == null) {
			throw new InvalidOperationException ($"Internal error: unable to find the {FullTypeName} type in the Mono.Android assembly");
		}

		MethodDefinition? nActivate_mm = null;
		MethodDefinition? nActivate = null;

		foreach (MethodDefinition method in javaTypeManager.Methods) {
			if (nActivate_mm == null && IsMatchingMethod (method, "n_Activate_mm")) {
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

			if (nActivate_mm != null && nActivate != null) {
				break;
			}
		}

		if (nActivate_mm == null) {
			ThrowMissingMethod ("nActivate_mm");
		}

		if (nActivate == null) {
			ThrowMissingMethod ("nActivate");
		}

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

		var entry = new MarshalMethodEntry (javaTypeManager, nActivate_mm, jniTypeName!, jniMethodName!, jniSignature!);  // NRT- Guarded above
		MarshalMethods.Add (".:!SpEcIaL:Java.Interop.TypeManager+JavaTypeManager::n_Activate_mm", new List<MarshalMethodEntry> { entry });

		[DoesNotReturn]
		void ThrowMissingMethod (string name)
		{
			throw new InvalidOperationException ($"Internal error: unable to find the '{name}' method in the '{FullTypeName}' type");
		}

		bool IsMatchingMethod (MethodDefinition method, string name)
		{
			if (String.Compare (name, method.Name, StringComparison.Ordinal) != 0) {
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
