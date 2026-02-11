using System;
using System.Collections.Generic;

namespace Microsoft.Android.Build.TypeMap;

/// <summary>
/// Represents a Java peer type discovered during assembly scanning.
/// Contains all data needed by downstream generators (TypeMap IL, UCO wrappers, JCW Java sources).
/// Generators consume this data model — they never touch PEReader/MetadataReader.
/// </summary>
sealed class JavaPeerInfo
{
	/// <summary>
	/// JNI type name, e.g., "android/app/Activity".
	/// Extracted from the [Register] attribute.
	/// </summary>
	public string JavaName { get; }

	/// <summary>
	/// Full managed type name, e.g., "Android.App.Activity".
	/// </summary>
	public string ManagedTypeName { get; }

	/// <summary>
	/// Managed type namespace, e.g., "Android.App".
	/// </summary>
	public string ManagedTypeNamespace { get; }

	/// <summary>
	/// Managed type short name (without namespace), e.g., "Activity".
	/// </summary>
	public string ManagedTypeShortName { get; }

	/// <summary>
	/// Assembly name the type belongs to, e.g., "Mono.Android".
	/// </summary>
	public string AssemblyName { get; }

	/// <summary>
	/// JNI name of the base Java type, e.g., "android/app/Activity" for a type
	/// that extends Activity. Null for java/lang/Object or types without a Java base.
	/// Needed by JCW Java source generation ("extends" clause).
	/// </summary>
	public string? BaseJavaName { get; }

	/// <summary>
	/// JNI names of Java interfaces this type implements, e.g., ["android/view/View$OnClickListener"].
	/// Needed by JCW Java source generation ("implements" clause).
	/// </summary>
	public IReadOnlyList<string> ImplementedInterfaceJavaNames { get; }

	public bool IsInterface { get; }
	public bool IsAbstract { get; }

	/// <summary>
	/// If true, this is a Managed Callable Wrapper (MCW) binding type.
	/// No JCW or RegisterNatives will be generated for it.
	/// </summary>
	public bool DoNotGenerateAcw { get; }

	/// <summary>
	/// Types with component attributes ([Activity], [Service], etc.),
	/// custom views from layout XML, or manifest-declared components
	/// are unconditionally preserved (not trimmable).
	/// </summary>
	public bool IsUnconditional { get; set; }

	/// <summary>
	/// Marshal methods: methods with [Register(name, sig, connector)] or [Export].
	/// Ordered — the index in this list is the method's ordinal for RegisterNatives.
	/// </summary>
	public IReadOnlyList<MarshalMethodInfo> MarshalMethods { get; }

	/// <summary>
	/// Java constructors to emit in the JCW .java file.
	/// Each has a JNI signature and an ordinal index for the nctor_N native method.
	/// </summary>
	public IReadOnlyList<JavaConstructorInfo> JavaConstructors { get; }

	/// <summary>
	/// Information about the activation constructor for this type.
	/// May reference a base type's constructor if the type doesn't define its own.
	/// </summary>
	public ActivationCtorInfo? ActivationCtor { get; }

	/// <summary>
	/// For interfaces and abstract types, the name of the invoker type
	/// used to instantiate instances from Java.
	/// </summary>
	public string? InvokerTypeName { get; }

	/// <summary>
	/// True if this is an open generic type definition.
	/// Generic types get TypeMap entries but CreateInstance throws NotSupportedException.
	/// </summary>
	public bool IsGenericDefinition { get; }

	public JavaPeerInfo (
		string javaName,
		string managedTypeName,
		string managedTypeNamespace,
		string managedTypeShortName,
		string assemblyName,
		string? baseJavaName,
		IReadOnlyList<string> implementedInterfaceJavaNames,
		bool isInterface,
		bool isAbstract,
		bool doNotGenerateAcw,
		bool isUnconditional,
		IReadOnlyList<MarshalMethodInfo> marshalMethods,
		IReadOnlyList<JavaConstructorInfo> javaConstructors,
		ActivationCtorInfo? activationCtor,
		string? invokerTypeName,
		bool isGenericDefinition)
	{
		JavaName = javaName;
		ManagedTypeName = managedTypeName;
		ManagedTypeNamespace = managedTypeNamespace;
		ManagedTypeShortName = managedTypeShortName;
		AssemblyName = assemblyName;
		BaseJavaName = baseJavaName;
		ImplementedInterfaceJavaNames = implementedInterfaceJavaNames;
		IsInterface = isInterface;
		IsAbstract = isAbstract;
		DoNotGenerateAcw = doNotGenerateAcw;
		IsUnconditional = isUnconditional;
		MarshalMethods = marshalMethods;
		JavaConstructors = javaConstructors;
		ActivationCtor = activationCtor;
		InvokerTypeName = invokerTypeName;
		IsGenericDefinition = isGenericDefinition;
	}
}

/// <summary>
/// Describes a marshal method (a method with [Register] or [Export]) on a Java peer type.
/// Contains all data needed to generate a UCO wrapper, a JCW native declaration,
/// and a RegisterNatives call.
/// </summary>
sealed class MarshalMethodInfo
{
	/// <summary>
	/// JNI method name, e.g., "onCreate".
	/// This is the Java method name (without n_ prefix).
	/// </summary>
	public string JniName { get; }

	/// <summary>
	/// JNI method signature, e.g., "(Landroid/os/Bundle;)V".
	/// </summary>
	public string JniSignature { get; }

	/// <summary>
	/// The connector string from [Register], e.g., "GetOnCreate_Landroid_os_Bundle_Handler".
	/// Null for [Export] methods.
	/// </summary>
	public string? Connector { get; }

	/// <summary>
	/// Full name of the managed method this marshal method maps to.
	/// </summary>
	public string ManagedMethodName { get; }

	/// <summary>
	/// Full name of the type that declares the managed method (may be a base type).
	/// </summary>
	public string DeclaringTypeName { get; }

	/// <summary>
	/// Assembly name of the type that declares the managed method.
	/// Needed for cross-assembly UCO wrapper generation.
	/// </summary>
	public string DeclaringAssemblyName { get; }

	/// <summary>
	/// The native callback method name, e.g., "n_onCreate".
	/// This is the actual method the UCO wrapper delegates to.
	/// </summary>
	public string NativeCallbackName { get; }

	/// <summary>
	/// JNI parameter types for UCO generation.
	/// </summary>
	public IReadOnlyList<JniParameterInfo> Parameters { get; }

	/// <summary>
	/// JNI return type descriptor, e.g., "V", "Landroid/os/Bundle;".
	/// </summary>
	public string JniReturnType { get; }

	/// <summary>
	/// True if this is a constructor registration.
	/// Constructor registrations use nctor_N naming and ActivateInstance.
	/// </summary>
	public bool IsConstructor { get; }

	/// <summary>
	/// For [Export] methods: Java exception types that the method declares it can throw.
	/// Null for [Register] methods.
	/// </summary>
	public IReadOnlyList<string>? ThrownNames { get; }

	/// <summary>
	/// For [Export] methods: super constructor arguments string.
	/// Null for [Register] methods.
	/// </summary>
	public string? SuperArgumentsString { get; }

	public MarshalMethodInfo (
		string jniName,
		string jniSignature,
		string? connector,
		string managedMethodName,
		string declaringTypeName,
		string declaringAssemblyName,
		string nativeCallbackName,
		IReadOnlyList<JniParameterInfo> parameters,
		string jniReturnType,
		bool isConstructor,
		IReadOnlyList<string>? thrownNames = null,
		string? superArgumentsString = null)
	{
		JniName = jniName;
		JniSignature = jniSignature;
		Connector = connector;
		ManagedMethodName = managedMethodName;
		DeclaringTypeName = declaringTypeName;
		DeclaringAssemblyName = declaringAssemblyName;
		NativeCallbackName = nativeCallbackName;
		Parameters = parameters;
		JniReturnType = jniReturnType;
		IsConstructor = isConstructor;
		ThrownNames = thrownNames;
		SuperArgumentsString = superArgumentsString;
	}
}

/// <summary>
/// Describes a JNI parameter for UCO method generation.
/// </summary>
sealed class JniParameterInfo
{
	/// <summary>
	/// JNI type descriptor, e.g., "Landroid/os/Bundle;", "I", "Z".
	/// </summary>
	public string JniType { get; }

	/// <summary>
	/// Managed parameter type name, e.g., "Android.OS.Bundle", "System.Int32".
	/// </summary>
	public string ManagedType { get; }

	public JniParameterInfo (string jniType, string managedType)
	{
		JniType = jniType;
		ManagedType = managedType;
	}
}

/// <summary>
/// Describes a Java constructor to emit in the JCW .java source file.
/// </summary>
sealed class JavaConstructorInfo
{
	/// <summary>
	/// JNI constructor signature, e.g., "(Landroid/content/Context;)V".
	/// </summary>
	public string JniSignature { get; }

	/// <summary>
	/// Ordinal index for the native constructor method (nctor_0, nctor_1, ...).
	/// </summary>
	public int ConstructorIndex { get; }

	/// <summary>
	/// JNI parameter types parsed from the signature.
	/// Used to generate the Java constructor parameter list.
	/// </summary>
	public IReadOnlyList<JniParameterInfo> Parameters { get; }

	public JavaConstructorInfo (string jniSignature, int constructorIndex, IReadOnlyList<JniParameterInfo> parameters)
	{
		JniSignature = jniSignature;
		ConstructorIndex = constructorIndex;
		Parameters = parameters;
	}
}

/// <summary>
/// Describes how to call the activation constructor for a Java peer type.
/// </summary>
sealed class ActivationCtorInfo
{
	/// <summary>
	/// The type that declares the activation constructor.
	/// May be the type itself or a base type.
	/// </summary>
	public string DeclaringTypeName { get; }

	/// <summary>
	/// The assembly containing the declaring type.
	/// </summary>
	public string DeclaringAssemblyName { get; }

	/// <summary>
	/// The style of activation constructor found.
	/// </summary>
	public ActivationCtorStyle Style { get; }

	public ActivationCtorInfo (string declaringTypeName, string declaringAssemblyName, ActivationCtorStyle style)
	{
		DeclaringTypeName = declaringTypeName;
		DeclaringAssemblyName = declaringAssemblyName;
		Style = style;
	}
}

enum ActivationCtorStyle
{
	/// <summary>
	/// Xamarin.Android style: (IntPtr handle, JniHandleOwnership transfer)
	/// </summary>
	XamarinAndroid,

	/// <summary>
	/// Java.Interop style: (ref JniObjectReference reference, JniObjectReferenceOptions options)
	/// </summary>
	JavaInterop,
}
