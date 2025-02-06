using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.Runtime;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;
using Java.Interop.Tools.JavaCallableWrappers.Utilities;
using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;

namespace Java.Interop.Tools.JavaCallableWrappers.Adapters;

public class CecilImporter
{
	// Don't expose internal "outerType" parameter to the public API
	public static CallableWrapperType CreateType (TypeDefinition type, IMetadataResolver resolver, CallableWrapperReaderOptions? options = null)
		=> CreateType (type, resolver, options, null);

	static CallableWrapperType CreateType (TypeDefinition type, IMetadataResolver resolver, CallableWrapperReaderOptions? options = null, string? outerType = null)
	{
		if (type.IsEnum || type.IsInterface || type.IsValueType)
			Diagnostic.Error (4200, CecilExtensions.LookupSource (type), Localization.Resources.JavaCallableWrappers_XA4200, type.FullName);

		var jniName = JavaNativeTypeManager.ToJniName (type, resolver);

		if (jniName is null)
			Diagnostic.Error (4201, CecilExtensions.LookupSource (type), Localization.Resources.JavaCallableWrappers_XA4201, type.FullName);

		if (outerType != null && !string.IsNullOrEmpty (outerType)) {
			jniName = jniName.Substring (outerType.Length + 1);
			ExtractJavaNames (outerType, out var p, out outerType);
		}

		ExtractJavaNames (jniName, out var package, out var name);

		options ??= new CallableWrapperReaderOptions ();

		if (string.IsNullOrEmpty (package) &&
				(type.IsSubclassOf ("Android.App.Activity", resolver) ||
				 type.IsSubclassOf ("Android.App.Application", resolver) ||
				 type.IsSubclassOf ("Android.App.Service", resolver) ||
				 type.IsSubclassOf ("Android.Content.BroadcastReceiver", resolver) ||
				 type.IsSubclassOf ("Android.Content.ContentProvider", resolver)))
			Diagnostic.Error (4203, CecilExtensions.LookupSource (type), Localization.Resources.JavaCallableWrappers_XA4203, jniName);

		var cwt = new CallableWrapperType (name, package, type.GetPartialAssemblyQualifiedName (resolver)) {
			IsApplication = JavaNativeTypeManager.IsApplication (type, resolver),
			IsInstrumentation = JavaNativeTypeManager.IsInstrumentation (type, resolver),
			IsAbstract = type.IsAbstract,
			ApplicationJavaClass = options.DefaultApplicationJavaClass,
			GenerateOnCreateOverrides = options.DefaultGenerateOnCreateOverrides,
			MonoRuntimeInitialization = options.DefaultMonoRuntimeInitialization,
		};

		// Type annotations
		cwt.Annotations.AddRange (CreateAnnotations (type, resolver));

		// Extends
		cwt.ExtendsType = GetJavaTypeName (type.BaseType, resolver);

		// Implemented interfaces
		foreach (var ifaceInfo in type.Interfaces) {
			var iface = resolver.Resolve (ifaceInfo.InterfaceType);

			if (!CecilExtensions.GetTypeRegistrationAttributes (iface).Any ())
				continue;

			cwt.ImplementedInterfaces.Add (GetJavaTypeName (iface, resolver));
		}

		// Application constructor
		if (CreateApplicationConstructor (cwt.Name, type, resolver) is CallableWrapperApplicationConstructor app_ctor)
			cwt.ApplicationConstructor = app_ctor;

		// Methods
		foreach (var minfo in type.Methods.Where (m => !m.IsConstructor)) {
			var baseRegisteredMethod = CecilExtensions.GetBaseRegisteredMethod (minfo, resolver);

			if (baseRegisteredMethod is not null)
				AddMethod (cwt, type, baseRegisteredMethod, minfo, options.MethodClassifier, resolver);
			else if (minfo.AnyCustomAttributes ("Java.Interop.JavaCallableAttribute")) {
				AddMethod (cwt, type, null, minfo, options.MethodClassifier, resolver);
				cwt.HasExport = true;
			} else if (minfo.AnyCustomAttributes ("Java.Interop.JavaCallableConstructorAttribute")) {
				AddMethod (cwt, type, null, minfo, options.MethodClassifier, resolver);
				cwt.HasExport = true;
			} else if (minfo.AnyCustomAttributes (typeof (ExportFieldAttribute))) {
				AddMethod (cwt, type, null, minfo, options.MethodClassifier, resolver);
				cwt.HasExport = true;
			} else if (minfo.AnyCustomAttributes (typeof (ExportAttribute))) {
				AddMethod (cwt, type, null, minfo, options.MethodClassifier, resolver);
				cwt.HasExport = true;
			}
		}

		// Methods from interfaces
		foreach (InterfaceImplementation ifaceInfo in type.Interfaces) {
			var typeReference = ifaceInfo.InterfaceType;
			var typeDefinition = resolver.Resolve (typeReference);

			if (typeDefinition is null) {
				Diagnostic.Error (4204,
					CecilExtensions.LookupSource (type),
					Localization.Resources.JavaCallableWrappers_XA4204,
					typeReference.FullName);
				continue;
			}

			if (!CecilExtensions.GetTypeRegistrationAttributes (typeDefinition).Any ())
				continue;

			foreach (MethodDefinition imethod in typeDefinition.Methods) {
				if (imethod.IsStatic)
					continue;

				AddMethod (cwt, type, imethod, imethod, options.MethodClassifier, resolver);
			}
		}

		// Constructors
		var ctorTypes = new List<TypeDefinition> () {
			type,
		};

		foreach (var bt in type.GetBaseTypes (resolver)) {
			ctorTypes.Add (bt);
			var rattr = CecilExtensions.GetTypeRegistrationAttributes (bt).FirstOrDefault ();

			if (rattr != null && rattr.DoNotGenerateAcw)
				break;
		}

		ctorTypes.Reverse ();

		var curCtors = new List<MethodDefinition> ();

		foreach (var minfo in type.Methods) {
			if (minfo.IsConstructor && minfo.AnyCustomAttributes (typeof (ExportAttribute))) {
				if (minfo.IsStatic) {
					// Diagnostic.Warning (log, "ExportAttribute does not work on static constructor");
				} else {
					if (CreateConstructor (cwt, minfo, ctorTypes [0], type, outerType, null, curCtors, false, true, resolver) is CallableWrapperConstructor c)
						cwt.Constructors.Add (c);

					cwt.HasExport = true;
				}
			}
		}

		AddConstructors (cwt, ctorTypes [0], type, outerType, null, curCtors, true, resolver);

		for (var i = 1; i < ctorTypes.Count; ++i) {
			var baseCtors = curCtors;
			curCtors = new List<MethodDefinition> ();
			AddConstructors (cwt, ctorTypes [i], type, outerType, baseCtors, curCtors, false, resolver);
		}

		AddNestedTypes (cwt, type, resolver, options);

		return cwt;
	}

	static CallableWrapperField CreateField (MethodDefinition method, string fieldName, IMetadataResolver resolver)
	{
		var visibility = GetJavaAccess (method.Attributes & MethodAttributes.MemberAccessMask);
		var type_name = JavaNativeTypeManager.ReturnTypeFromSignature (JavaNativeTypeManager.GetJniSignature (method, resolver))?.Type
			?? throw new ArgumentException ($"Could not get JNI signature for method `{method.Name}`", nameof (method));

		var field = new CallableWrapperField (fieldName, type_name, visibility, method.Name) {
			IsStatic = method.IsStatic,
		};

		field.Annotations.AddRange (CreateAnnotations (method, resolver));

		return field;
	}

	// Constructor with [Register] attribute
	static CallableWrapperConstructor CreateConstructor (MethodDefinition methodDefinition, CallableWrapperType type, RegisterAttribute register, string? managedParameters, string? outerType, IMetadataResolver cache, bool shouldBeDynamicallyRegistered = true)
	{
		var method = CreateConstructor (methodDefinition.Name, type, register.Signature, register.Connector, managedParameters, outerType, null);

		method.Annotations.AddRange (CreateAnnotations (methodDefinition, cache));
		method.IsDynamicallyRegistered = shouldBeDynamicallyRegistered;

		return method;
	}

	// Constructor with [Export] attribute
	static CallableWrapperConstructor CreateConstructor (MethodDefinition methodDefinition, CallableWrapperType type, ExportAttribute export, string? managedParameters, IMetadataResolver resolver)
	{
		var method = CreateConstructor (methodDefinition.Name, type, JavaNativeTypeManager.GetJniSignature (methodDefinition, resolver), "__export__", null, null, export.SuperArgumentsString);

		method.IsExport = true;
		method.IsStatic = methodDefinition.IsStatic;
		method.JavaAccess = GetJavaAccess (methodDefinition.Attributes & MethodAttributes.MemberAccessMask);
		method.ThrownTypeNames = export.ThrownNames;
		method.JavaNameOverride = export.Name;
		method.ManagedParameters = managedParameters;
		method.Annotations.AddRange (CreateAnnotations (methodDefinition, resolver));

		return method;
	}

	// Common constructor creation code
	static CallableWrapperConstructor CreateConstructor (string name, CallableWrapperType type, string? signature, string? connector, string? managedParameters, string? outerType, string? superCall)
	{
		signature = signature ?? throw new ArgumentNullException ("`connector` cannot be null.", nameof (connector));
		var method_name = "n_" + name + ":" + signature + ":" + connector;

		var method = new CallableWrapperConstructor (type, name, method_name, signature);

		PopulateMethod (method, signature, managedParameters, outerType, superCall);

		method.Name = type.Name;

		return method;
	}

	// Method with a [Register] attribute
	static CallableWrapperMethod CreateMethod (MethodDefinition methodDefinition, CallableWrapperType declaringType, RegisterAttribute register, string? managedParameters, string? outerType, IMetadataResolver resolver, bool shouldBeDynamicallyRegistered = true)
	{
		var method = CreateMethod (register.Name, declaringType, register.Signature, register.Connector, managedParameters, outerType, null);

		method.Annotations.AddRange (CreateAnnotations (methodDefinition, resolver));
		method.IsDynamicallyRegistered = shouldBeDynamicallyRegistered;

		return method;
	}

	// Method with an [Export] attribute
	static CallableWrapperMethod CreateMethod (MethodDefinition methodDefinition, CallableWrapperType declaringType, ExportAttribute export, string? managedParameters, IMetadataResolver resolver)
	{
		var method = CreateMethod (methodDefinition.Name, declaringType, JavaNativeTypeManager.GetJniSignature (methodDefinition, resolver), "__export__", null, null, export.SuperArgumentsString);

		method.IsExport = true;
		method.IsStatic = methodDefinition.IsStatic;
		method.JavaAccess = GetJavaAccess (methodDefinition.Attributes & MethodAttributes.MemberAccessMask);
		method.ThrownTypeNames = export.ThrownNames;
		method.JavaNameOverride = export.Name;
		method.ManagedParameters = managedParameters;
		method.Annotations.AddRange (CreateAnnotations (methodDefinition, resolver));

		return method;
	}

	// Method with an [ExportField] attribute
	static CallableWrapperMethod CreateMethod (MethodDefinition methodDefinition, CallableWrapperType declaringType, IMetadataResolver resolver)
	{
		var method = CreateMethod (methodDefinition.Name, declaringType, JavaNativeTypeManager.GetJniSignature (methodDefinition, resolver), "__export__", null, null, null);

		if (methodDefinition.HasParameters)
			Diagnostic.Error (4205, CecilExtensions.LookupSource (methodDefinition), Localization.Resources.JavaCallableWrappers_XA4205);
		if (methodDefinition.ReturnType.MetadataType == MetadataType.Void)
			Diagnostic.Error (4208, CecilExtensions.LookupSource (methodDefinition), Localization.Resources.JavaCallableWrappers_XA4208);

		method.IsExport = true;
		method.IsStatic = methodDefinition.IsStatic;
		method.JavaAccess = GetJavaAccess (methodDefinition.Attributes & MethodAttributes.MemberAccessMask);

		// Annotations are processed within CallableWrapperField, not the initializer method. So we don't generate them here.

		return method;
	}

	// Common method creation code
	static CallableWrapperMethod CreateMethod (string name, CallableWrapperType declaringType, string? signature, string? connector, string? managedParameters, string? outerType, string? superCall)
	{
		signature = signature ?? throw new ArgumentNullException ("`connector` cannot be null.", nameof (connector));
		var method_name = "n_" + name + ":" + signature + ":" + connector?.Replace ('/', '+');

		var method = new CallableWrapperMethod (declaringType, name, method_name, signature);

		PopulateMethod (method, signature, managedParameters, outerType, superCall);

		return method;
	}

	// This is done this way to allow sharing between CallableWrapperMethod and CallableWrapperConstructor
	static void PopulateMethod (CallableWrapperMethod method, string signature, string? managedParameters, string? outerType, string? superCall)
	{
		method.ManagedParameters = managedParameters;

		var jnisig = signature;
		var closer = jnisig.IndexOf (')');
		var ret = jnisig.Substring (closer + 1);

		method.Retval = JavaNativeTypeManager.Parse (ret)?.Type;

		var jniparms = jnisig.Substring (1, closer - 1);

		if (string.IsNullOrEmpty (jniparms) && string.IsNullOrEmpty (superCall))
			return;

		var parms = new StringBuilder ();
		var scall = new StringBuilder ();
		var acall = new StringBuilder ();
		var first = true;
		var i = 0;

		foreach (var jti in JavaNativeTypeManager.FromSignature (jniparms)) {
			if (outerType != null) {
				acall.Append (outerType).Append (".this");
				outerType = null;
				continue;
			}

			var parmType = jti.Type;

			if (!first) {
				parms.Append (", ");
				scall.Append (", ");
				acall.Append (", ");
			}

			first = false;
			parms.Append (parmType).Append (" p").Append (i);
			scall.Append ("p").Append (i);
			acall.Append ("p").Append (i);
			++i;
		}

		method.Params = parms.ToString ();
		method.SuperCall = superCall ?? scall.ToString ();
		method.ActivateCall = acall.ToString ();
	}

	static void AddConstructors (CallableWrapperType declaringType, TypeDefinition type, TypeDefinition rootType, string? outerType, List<MethodDefinition>? baseCtors, List<MethodDefinition> curCtors, bool onlyRegisteredOrExportedCtors, IMetadataResolver cache)
	{
		foreach (var ctor in type.Methods)
			if (ctor.IsConstructor && !ctor.IsStatic && !ctor.AnyCustomAttributes (typeof (ExportAttribute)))
				if (CreateConstructor (declaringType, ctor, type, rootType, outerType, baseCtors, curCtors, onlyRegisteredOrExportedCtors, false, cache) is CallableWrapperConstructor c)
					declaringType.Constructors.Add (c);
	}

	static CallableWrapperConstructor? CreateConstructor (CallableWrapperType declaringType, MethodDefinition ctor, TypeDefinition type, TypeDefinition rootType, string? outerType, List<MethodDefinition>? baseCtors, List<MethodDefinition> curCtors, bool onlyRegisteredOrExportedCtors, bool skipParameterCheck, IMetadataResolver cache)
	{
		// We create a parameter-less constructor for the application class, so don't use the imported one
		if (!ctor.HasParameters && JavaNativeTypeManager.IsApplication (rootType, cache))
			return null;

		var managedParameters = GetManagedParameters (ctor, outerType, type, cache);

		if (!skipParameterCheck && (managedParameters == null || declaringType.Constructors.Any (c => c.ManagedParameters == managedParameters)))
			return null;

		// Constructor with [Export] attribute
		var eattr = CecilExtensions.GetExportAttributes (ctor, cache).FirstOrDefault ();

		if (eattr != null) {
			if (!string.IsNullOrEmpty (eattr.Name)) {
				// Diagnostic.Warning (log, "Use of ExportAttribute.Name property is invalid on constructors");
			}

			curCtors.Add (ctor);
			return CreateConstructor (ctor, declaringType, eattr, managedParameters, cache);
		}

		// Constructor with [Register] attribute
		var rattr = CecilExtensions.GetMethodRegistrationAttributes (ctor).FirstOrDefault ();

		if (rattr != null) {
			if (declaringType.Constructors.Any (c => c.JniSignature == rattr.Signature))
				return null;

			curCtors.Add (ctor);
			return CreateConstructor (ctor, declaringType, rattr, managedParameters, outerType, cache);
		}

		if (onlyRegisteredOrExportedCtors)
			return null;

		// Constructors without [Export] or [Register] attributes
		var jniSignature = JavaNativeTypeManager.GetJniSignature (ctor, cache);

		if (jniSignature is null)
			return null;

		if (declaringType.Constructors.Any (c => c.JniSignature == jniSignature))
			return null;

		if (baseCtors is null)
			throw new InvalidOperationException ("`baseCtors` should not be null!");

		if (baseCtors.Any (m => m.Parameters.AreParametersCompatibleWith (ctor.Parameters, cache))) {
			curCtors.Add (ctor);
			return CreateConstructor (".ctor", declaringType, jniSignature, "", managedParameters, outerType, null);
		}

		if (baseCtors.Any (m => !m.HasParameters)) {
			curCtors.Add (ctor);
			return CreateConstructor (".ctor", declaringType, jniSignature, "", managedParameters, outerType, "");
		}

		return null;
	}

	static string GetManagedParameters (MethodDefinition ctor, string? outerType, TypeDefinition type, IMetadataResolver cache)
	{
		var sb = new StringBuilder ();

		foreach (var pdef in ctor.Parameters) {
			if (sb.Length > 0)
				sb.Append (':');
			if (outerType != null && sb.Length == 0)
				sb.Append (type.DeclaringType.GetPartialAssemblyQualifiedName (cache));
			else
				sb.Append (pdef.ParameterType.GetPartialAssemblyQualifiedName (cache));
		}

		return sb.ToString ();
	}

	static CallableWrapperApplicationConstructor? CreateApplicationConstructor (string name, TypeDefinition type, IMetadataResolver resolver)
	{
		if (!JavaNativeTypeManager.IsApplication (type, resolver))
			return null;

		return new CallableWrapperApplicationConstructor (name);
	}

	static void AddNestedTypes (CallableWrapperType declaringType, TypeDefinition type, IMetadataResolver cache, CallableWrapperReaderOptions? options)
	{
		if (!type.HasNestedTypes)
			return;

		foreach (var nt in type.NestedTypes) {
			if (!nt.HasJavaPeer (cache))
				continue;
			if (!JavaNativeTypeManager.IsNonStaticInnerClass (nt, cache))
				continue;

			declaringType.NestedTypes.Add (CreateType (nt, cache, options, JavaNativeTypeManager.ToJniName (type, cache)));
			AddNestedTypes (declaringType, nt, cache, options);
		}

		declaringType.HasExport |= declaringType.NestedTypes.Any (t => t.HasExport);
	}

	static void AddMethod (CallableWrapperType declaringType, TypeDefinition type, MethodDefinition? registeredMethod, MethodDefinition implementedMethod, JavaCallableMethodClassifier? methodClassifier, IMetadataResolver cache)
	{
		if (registeredMethod != null)
			foreach (RegisterAttribute attr in CecilExtensions.GetMethodRegistrationAttributes (registeredMethod)) {
				// Check for Kotlin-mangled methods that cannot be overridden
				if (attr.Name.Contains ("-impl") || (attr.Name.Length > 7 && attr.Name [attr.Name.Length - 8] == '-'))
					Diagnostic.Error (4217, CecilExtensions.LookupSource (implementedMethod), Localization.Resources.JavaCallableWrappers_XA4217, attr.Name);

				var shouldBeDynamicallyRegistered = methodClassifier?.ShouldBeDynamicallyRegistered (type, registeredMethod, implementedMethod, attr.OriginAttribute) ?? true;
				var method = CreateMethod (implementedMethod, declaringType, attr, null, null, cache, shouldBeDynamicallyRegistered);

				if (!registeredMethod.IsConstructor && !declaringType.Methods.Any (m => m.Name == method.Name && m.Params == method.Params))
					declaringType.Methods.Add (method);
			}
		foreach (ExportAttribute attr in CecilExtensions.GetExportAttributes (implementedMethod, cache)) {
			if (type.HasGenericParameters)
				Diagnostic.Error (4206, CecilExtensions.LookupSource (implementedMethod), Localization.Resources.JavaCallableWrappers_XA4206);

			var method = CreateMethod (implementedMethod, declaringType, attr, null, cache);

			if (!string.IsNullOrEmpty (attr.SuperArgumentsString)) {
				// Diagnostic.Warning (log, "Use of ExportAttribute.SuperArgumentsString property is invalid on methods");
			}

			if (!implementedMethod.IsConstructor && !declaringType.Methods.Any (m => m.Name == method.Name && m.Params == method.Params))
				declaringType.Methods.Add (method);
		}
		foreach (ExportFieldAttribute attr in CecilExtensions.GetExportFieldAttributes (implementedMethod)) {
			if (type.HasGenericParameters)
				Diagnostic.Error (4207, CecilExtensions.LookupSource (implementedMethod), Localization.Resources.JavaCallableWrappers_XA4207);

			var method = CreateMethod (implementedMethod, declaringType, cache);

			if (!implementedMethod.IsConstructor && !declaringType.Methods.Any (m => m.Name == method.Name && m.Params == method.Params)) {
				declaringType.Methods.Add (method);
				declaringType.Fields.Add (CreateField (implementedMethod, attr.Name, cache));
			}
		}
	}

	static void ExtractJavaNames (string jniName, out string package, out string type)
	{
		var i = jniName.LastIndexOf ('/');

		if (i < 0) {
			type = jniName;
			package = string.Empty;
		} else {
			type = jniName.Substring (i + 1);
			package = jniName.Substring (0, i).Replace ('/', '.');
		}
	}

	static string GetJavaTypeName (TypeReference r, IMetadataResolver cache)
	{
		var d = cache.Resolve (r);
		var jniName = JavaNativeTypeManager.ToJniName (d, cache);

		if (jniName is null) {
			Diagnostic.Error (4201, Localization.Resources.JavaCallableWrappers_XA4201, r.FullName);
			throw new InvalidOperationException ("--nrt:jniName-- Should not be reached");
		}

		return jniName.Replace ('/', '.').Replace ('$', '.');
	}

	static IEnumerable<CallableWrapperTypeAnnotation> CreateAnnotations (ICustomAttributeProvider type, IMetadataResolver resolver)
	{
		foreach (var ca in type.CustomAttributes) {
			var annotation = CreateAnnotation (ca, resolver);

			if (annotation is not null)
				yield return annotation;
		}
	}

	static CallableWrapperTypeAnnotation? CreateAnnotation (CustomAttribute ca, IMetadataResolver resolver)
	{
		var catype = resolver.Resolve (ca.AttributeType);
		var tca = catype.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "Android.Runtime.AnnotationAttribute");

		if (tca is null)
			return null;

		var name_object = tca.ConstructorArguments [0].Value;

		// Should never be hit
		if (name_object is not string name)
			throw new ArgumentException ($"Expected a string for the first argument of the {nameof (RegisterAttribute)} constructor.", nameof (ca));

		var annotation = new CallableWrapperTypeAnnotation (name);

		foreach (var p in ca.Properties) {
			var pd = catype.Properties.FirstOrDefault (pp => pp.Name == p.Name);
			var reg = pd?.CustomAttributes.FirstOrDefault (pdca => pdca.AttributeType.FullName == "Android.Runtime.RegisterAttribute");
			var key = reg != null ? (string) reg.ConstructorArguments [0].Value : p.Name;
			var value = ManagedValueToJavaSource (p.Argument.Value);

			annotation.Properties.Add (new KeyValuePair<string, string> (key, value));
		}

		return annotation;
	}

	// FIXME: this is hacky. Is there any existing code for value to source conversion?
	static string ManagedValueToJavaSource (object value)
	{
		if (value is string)
			return "\"" + value.ToString ()?.Replace ("\"", "\"\"") + '"';
		else if (value.GetType ().FullName == "Java.Lang.Class")
			return value.ToString () + ".class";
		else if (value is bool v)
			return v ? "true" : "false";
		else
			return value.ToString () ?? "";
	}

	static string GetJavaAccess (MethodAttributes access)
	{
		return access switch {
			MethodAttributes.Public => "public",
			MethodAttributes.FamORAssem => "protected",
			MethodAttributes.Family => "protected",
			_ => "private",
		};
	}
}
