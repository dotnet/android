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
sealed class JcwJavaSourceGenerator
{
	/// <summary>
	/// Generates .java source files for all ACW types and writes them to the output directory.
	/// Returns the list of generated file paths.
	/// </summary>
	public IReadOnlyList<string> Generate (IReadOnlyList<JavaPeerInfo> types, string outputDirectory)
	{
		if (types is null) {
			throw new ArgumentNullException (nameof (types));
		}
		if (outputDirectory is null) {
			throw new ArgumentNullException (nameof (outputDirectory));
		}

		var generatedFiles = new List<string> ();

		foreach (var type in types) {
			if (type.DoNotGenerateAcw || type.IsInterface) {
				continue;
			}

			string filePath = GetOutputFilePath (type, outputDirectory);
			string? dir = Path.GetDirectoryName (filePath);
			if (dir != null) {
				Directory.CreateDirectory (dir);
			}

			using var writer = new StreamWriter (filePath);
			Generate (type, writer);
			generatedFiles.Add (filePath);
		}

		return generatedFiles;
	}

	/// <summary>
	/// Generates a single .java source file for the given type.
	/// </summary>
	internal void Generate (JavaPeerInfo type, TextWriter writer)
	{
		writer.NewLine = "\n";
		WritePackageDeclaration (type, writer);
		WriteClassDeclaration (type, writer);
		WriteStaticInitializer (type, writer);
		WriteConstructors (type, writer);
		WriteMethods (type, writer);
		WriteClassClose (writer);
	}

	static string GetOutputFilePath (JavaPeerInfo type, string outputDirectory)
	{
		JniSignatureHelper.ValidateJniName (type.JavaName);
		string relativePath = type.JavaName + ".java";
		return Path.Combine (outputDirectory, relativePath);
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
			string parameters = FormatParameterList (ctor.Parameters);
			string superArgs = ctor.SuperArgumentsString ?? FormatArgumentList (ctor.Parameters);
			string args = FormatArgumentList (ctor.Parameters);

			writer.Write ($$"""
	public {{simpleClassName}} ({{parameters}})
	{
		super ({{superArgs}});
		if (getClass () == {{simpleClassName}}.class) nctor_{{ctor.ConstructorIndex}} ({{args}});
	}


""");
		}

		// Write native constructor declarations
		foreach (var ctor in type.JavaConstructors) {
			string parameters = FormatParameterList (ctor.Parameters);
			writer.WriteLine ($"\tprivate native void nctor_{ctor.ConstructorIndex} ({parameters});");
		}

		if (type.JavaConstructors.Count > 0) {
			writer.WriteLine ();
		}
	}

	static void WriteMethods (JavaPeerInfo type, TextWriter writer)
	{
		foreach (var method in type.MarshalMethods) {
			if (method.IsConstructor) {
				continue;
			}

			string javaReturnType = JniSignatureHelper.JniTypeToJava (method.JniReturnType);
			bool isVoid = method.JniReturnType == "V";
			string parameters = FormatParameterList (method.Parameters);
			string args = FormatArgumentList (method.Parameters);
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
		{{returnPrefix}}{{method.NativeCallbackName}} ({{args}});
	}
	public native {{javaReturnType}} {{method.NativeCallbackName}} ({{parameters}});

""");
			} else {
				writer.Write ($$"""

	public {{javaReturnType}} {{method.JniName}} ({{parameters}}){{throwsClause}}
	{
		{{returnPrefix}}{{method.NativeCallbackName}} ({{args}});
	}
	public native {{javaReturnType}} {{method.NativeCallbackName}} ({{parameters}});

""");
			}
		}
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
