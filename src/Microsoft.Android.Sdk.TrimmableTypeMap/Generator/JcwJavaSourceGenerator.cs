using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Generates JCW (Java Callable Wrapper) .java source files from scanned <see cref="JavaPeerInfo"/> records.
/// Only processes ACW types (where <see cref="JavaPeerInfo.DoNotGenerateAcw"/> is false).
/// </summary>
/// <remarks>
/// <para>Each generated .java file looks like this (pseudo-Java):</para>
/// <code>
/// package com.example;
///
/// public class MainActivity
///     extends android.app.Activity
///     implements
///         mono.android.IGCUserPeer,
///         android.view.View.OnClickListener
/// {
///     static {
///         mono.android.Runtime.registerNatives (MainActivity.class);
///     }
///
///     public MainActivity (android.content.Context p0)
///     {
///         super (p0);
///         if (getClass () == MainActivity.class) nctor_0 (p0);
///     }
///     private native void nctor_0 (android.content.Context p0);
///
///     @Override
///     public void onCreate (android.os.Bundle p0)
///     {
///         n_onCreate (p0);
///     }
///     public native void n_onCreate (android.os.Bundle p0);
/// }
/// </code>
/// </remarks>
public sealed class JcwJavaSourceGenerator
{
	/// <summary>
	/// Generates .java source content for all ACW types and returns them as in-memory
	/// (relativePath, content) pairs. No filesystem IO is performed.
	/// </summary>
	public IReadOnlyList<GeneratedJavaSource> GenerateContent (IReadOnlyList<JavaPeerInfo> types)
	{
		if (types is null) throw new ArgumentNullException (nameof (types));
		var results = new List<GeneratedJavaSource> ();
		foreach (var type in types) {
			if (type.DoNotGenerateAcw || type.IsInterface) continue;
			using var writer = new StringWriter ();
			Generate (type, writer);
			results.Add (new GeneratedJavaSource (GetRelativePath (type), writer.ToString ()));
		}
		return results;
	}

	/// <summary>
	/// Generates a single .java source file for the given type.
	/// </summary>
	public void Generate (JavaPeerInfo type, TextWriter writer)
	{
		writer.NewLine = "\n";
		WritePackageDeclaration (type, writer);
		WriteClassDeclaration (type, writer);
		WriteStaticInitializer (type, writer);
		WriteConstructors (type, writer);
		WriteFields (type, writer);
		WriteMethods (type, writer);
		WriteGCUserPeerMethods (writer);
		WriteClassClose (writer);
	}

	static string GetRelativePath (JavaPeerInfo type)
	{
		JniSignatureHelper.ValidateJniName (type.JavaName);
		return type.JavaName + ".java";
	}


	/// <summary>
	/// Validates that the JNI name is well-formed: non-empty, each segment separated by '/'
	/// contains only valid Java identifier characters (letters, digits, '_', '$').
	/// This also prevents path traversal (e.g., ".." segments, rooted paths, backslashes).
	/// </summary>
	static void WritePackageDeclaration (JavaPeerInfo type, TextWriter writer)
	{
		string? package = JniSignatureHelper.GetJavaPackageName (type.JavaName);
		if (package != null) {
			writer.Write ("package ");
			writer.Write (package);
			writer.WriteLine (';');
			writer.WriteLine ();
		}
	}

	static void WriteClassDeclaration (JavaPeerInfo type, TextWriter writer)
	{
		string abstractModifier = type.IsAbstract && !type.IsInterface ? "abstract " : "";
		string className = JniSignatureHelper.GetJavaSimpleName (type.JavaName);

		writer.Write ($"public {abstractModifier}class {className}\n");

		// extends clause
		if (type.BaseJavaName != null) {
			writer.WriteLine ($"\textends {JniSignatureHelper.JniNameToJavaName (type.BaseJavaName)}");
		}

		// implements clause — always includes IGCUserPeer, plus any implemented interfaces
		writer.Write ("\timplements\n\t\tmono.android.IGCUserPeer");

		foreach (var iface in type.ImplementedInterfaceJavaNames) {
			writer.Write ($",\n\t\t{JniSignatureHelper.JniNameToJavaName (iface)}");
		}

		writer.WriteLine ();
		writer.WriteLine ('{');
	}

	static void WriteStaticInitializer (JavaPeerInfo type, TextWriter writer)
	{
		string className = JniSignatureHelper.GetJavaSimpleName (type.JavaName);

		// Application and Instrumentation types cannot call registerNatives in their
		// static initializer — the runtime isn't ready yet at that point. Emit a
		// lazy one-time helper instead so the first managed callback can register
		// the class just before invoking its native method.
		if (type.CannotRegisterInStaticConstructor) {
			writer.Write ($$"""
	private static boolean __md_natives_registered;
	private static synchronized void __md_registerNatives ()
	{
		if (!__md_natives_registered) {
			mono.android.Runtime.registerNatives ({{className}}.class);
			__md_natives_registered = true;
		}
	}


""");
			return;
		}

		writer.Write ($$"""
	static {
		mono.android.Runtime.registerNatives ({{className}}.class);
	}


""");
	}

	static void WriteConstructors (JavaPeerInfo type, TextWriter writer)
	{
		string simpleClassName = JniSignatureHelper.GetJavaSimpleName (type.JavaName);

		foreach (var ctor in type.JavaConstructors) {
			var ctorParams = JniSignatureHelper.ParseParameters (ctor.JniSignature);
			string parameters = FormatParameterList (ctorParams);
			string superArgs = ctor.SuperArgumentsString ?? FormatArgumentList (ctorParams);
			string args = FormatArgumentList (ctorParams);

			writer.Write ($$"""
	public {{simpleClassName}} ({{parameters}})
	{
		super ({{superArgs}});

""");

			if (!type.CannotRegisterInStaticConstructor) {
				writer.Write ($$"""
		if (getClass () == {{simpleClassName}}.class) nctor_{{ctor.ConstructorIndex}} ({{args}});

""");
			}

			writer.Write ($$"""
	}


""");
		}

		// Write native constructor declarations
		foreach (var ctor in type.JavaConstructors) {
			var nativeCtorParams = JniSignatureHelper.ParseParameters (ctor.JniSignature);
			string parameters = FormatParameterList (nativeCtorParams);
			writer.WriteLine ($"\tprivate native void nctor_{ctor.ConstructorIndex} ({parameters});");
		}

		if (type.JavaConstructors.Count > 0) {
			writer.WriteLine ();
		}
	}

	static void WriteFields (JavaPeerInfo type, TextWriter writer)
	{
		foreach (var field in type.JavaFields) {
			writer.Write ('\t');
			writer.Write (field.Visibility);
			writer.Write (' ');
			if (field.IsStatic) {
				writer.Write ("static ");
			}
			writer.Write (field.JavaTypeName);
			writer.Write (' ');
			writer.Write (field.FieldName);
			writer.Write (" = ");
			writer.Write (field.InitializerMethodName);
			writer.WriteLine (" ();");
		}

		if (type.JavaFields.Count > 0) {
			writer.WriteLine ();
		}
	}

	static void WriteMethods (JavaPeerInfo type, TextWriter writer)
	{
		string registerNativesLine = type.CannotRegisterInStaticConstructor
			? "\t\t__md_registerNatives ();\n"
			: "";

		foreach (var method in type.MarshalMethods) {
			if (method.IsConstructor) {
				continue;
			}

			string jniReturnType = JniSignatureHelper.ParseReturnTypeString (method.JniSignature);
			string javaReturnType = JniSignatureHelper.JniTypeToJava (jniReturnType);
			bool isVoid = jniReturnType == "V";
			var methodParams = JniSignatureHelper.ParseParameters (method.JniSignature);
			string parameters = FormatParameterList (methodParams);
			string args = FormatArgumentList (methodParams);
			string returnPrefix = isVoid ? "" : "return ";

			// throws clause for [Export] methods
			string throwsClause = "";
			if (method.ThrownNames != null && method.ThrownNames.Count > 0) {
				throwsClause = $"\n\t\tthrows {string.Join (", ", method.ThrownNames)}";
			}

			if (method.Connector != null) {
				writer.Write ($$"""

	@Override
	public {{javaReturnType}} {{method.JniName}} ({{parameters}}){{throwsClause}}
	{
{{registerNativesLine}}		{{returnPrefix}}{{method.NativeCallbackName}} ({{args}});
	}
	public native {{javaReturnType}} {{method.NativeCallbackName}} ({{parameters}});

""");
			} else {
				string access = method.IsExport && method.JavaAccess != null ? method.JavaAccess : "public";
				writer.Write ($$"""

	{{access}} {{javaReturnType}} {{method.JniName}} ({{parameters}}){{throwsClause}}
	{
{{registerNativesLine}}		{{returnPrefix}}{{method.NativeCallbackName}} ({{args}});
	}
	{{access}} native {{javaReturnType}} {{method.NativeCallbackName}} ({{parameters}});

""");
			}
		}
	}

	static void WriteGCUserPeerMethods (TextWriter writer)
	{
		writer.Write ("""

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}

""");
	}

	static void WriteClassClose (TextWriter writer)
	{
		writer.WriteLine ('}');
	}

	static string FormatParameterList (IReadOnlyList<JniParameterInfo> parameters)
	{
		if (parameters.Count == 0) {
			return "";
		}

		var sb = new System.Text.StringBuilder ();
		for (int i = 0; i < parameters.Count; i++) {
			if (i > 0) {
				sb.Append (", ");
			}
			sb.Append (JniSignatureHelper.JniTypeToJava (parameters [i].JniType));
			sb.Append (" p");
			sb.Append (i);
		}
		return sb.ToString ();
	}

	static string FormatArgumentList (IReadOnlyList<JniParameterInfo> parameters)
	{
		if (parameters.Count == 0) {
			return "";
		}

		var sb = new System.Text.StringBuilder ();
		for (int i = 0; i < parameters.Count; i++) {
			if (i > 0) {
				sb.Append (", ");
			}
			sb.Append ('p');
			sb.Append (i);
		}
		return sb.ToString ();
	}

}
