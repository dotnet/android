using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Generates JCW (Java Callable Wrapper) .java source files from scanned <see cref="JavaPeerInfo"/> records.
/// Only processes ACW types (where <see cref="JavaPeerInfo.DoNotGenerateAcw"/> is false).
/// </summary>
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
			if (type.DoNotGenerateAcw) {
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
		WritePackageDeclaration (type, writer);
		WriteClassDeclaration (type, writer);
		WriteStaticInitializer (type, writer);
		WriteConstructors (type, writer);
		WriteMethods (type, writer);
		WriteClassClose (writer);
	}

	static string GetOutputFilePath (JavaPeerInfo type, string outputDirectory)
	{
		// JNI name uses '/' as separator and '$' for nested types
		// e.g., "com/example/MainActivity" → "com/example/MainActivity.java"
		// Nested types: "com/example/Outer$Inner" → "com/example/Outer$Inner.java" (same file convention)
		string relativePath = type.JavaName + ".java";
		return Path.Combine (outputDirectory, relativePath);
	}

	static void WritePackageDeclaration (JavaPeerInfo type, TextWriter writer)
	{
		string? package = GetJavaPackageName (type.JavaName);
		if (package != null) {
			writer.Write ("package ");
			writer.Write (package);
			writer.WriteLine (';');
			writer.WriteLine ();
		}
	}

	static void WriteClassDeclaration (JavaPeerInfo type, TextWriter writer)
	{
		writer.Write ("public ");
		if (type.IsAbstract && !type.IsInterface) {
			writer.Write ("abstract ");
		}
		writer.Write ("class ");
		writer.WriteLine (GetJavaSimpleName (type.JavaName));

		// extends clause
		string? baseJavaType = type.BaseJavaName != null ? JniNameToJavaName (type.BaseJavaName) : null;
		if (baseJavaType != null) {
			writer.Write ("\textends ");
			writer.WriteLine (baseJavaType);
		}

		// implements clause — always includes IGCUserPeer, plus any implemented interfaces
		writer.Write ("\timplements");
		writer.Write ("\n\t\tmono.android.IGCUserPeer");

		foreach (var iface in type.ImplementedInterfaceJavaNames) {
			writer.Write (",\n\t\t");
			writer.Write (JniNameToJavaName (iface));
		}

		writer.WriteLine ();
		writer.WriteLine ('{');
	}

	static void WriteStaticInitializer (JavaPeerInfo type, TextWriter writer)
	{
		writer.Write ("\tstatic {\n");
		writer.Write ("\t\tmono.android.Runtime.registerNatives (");
		writer.Write (GetJavaSimpleName (type.JavaName));
		writer.Write (".class);\n");
		writer.Write ("\t}\n");
		writer.WriteLine ();
	}

	static void WriteConstructors (JavaPeerInfo type, TextWriter writer)
	{
		string simpleClassName = GetJavaSimpleName (type.JavaName);

		foreach (var ctor in type.JavaConstructors) {
			// Constructor signature
			writer.Write ("\tpublic ");
			writer.Write (simpleClassName);
			writer.Write (" (");
			WriteParameterList (ctor.Parameters, writer);
			writer.Write (')');

			if (ctor.IsExport && ctor.ThrownNames != null && ctor.ThrownNames.Count > 0) {
				writer.Write ("\n\t\tthrows ");
				for (int i = 0; i < ctor.ThrownNames.Count; i++) {
					if (i > 0) {
						writer.Write (", ");
					}
					writer.Write (ctor.ThrownNames [i]);
				}
			}

			writer.WriteLine ();
			writer.WriteLine ("\t{");

			// super() call — use SuperArgumentsString if provided ([Export] constructors),
			// otherwise forward all constructor parameters.
			writer.Write ("\t\tsuper (");
			if (ctor.SuperArgumentsString != null) {
				writer.Write (ctor.SuperArgumentsString);
			} else {
				WriteArgumentList (ctor.Parameters, writer);
			}
			writer.WriteLine (");");

			// Activation guard: only activate if this is the exact class
			writer.Write ("\t\tif (getClass () == ");
			writer.Write (simpleClassName);
			writer.Write (".class) ");

			if (ctor.IsExport) {
				// [Export] constructors use TypeManager.Activate
				WriteTypeManagerActivate (type, ctor.Parameters, writer);
			} else {
				// [Register] constructors use native nctor_N methods
				writer.Write ("nctor_");
				writer.Write (ctor.ConstructorIndex);
				writer.Write (" (");
				WriteArgumentList (ctor.Parameters, writer);
				writer.Write (')');
			}
			writer.WriteLine (";");

			writer.WriteLine ("\t}");
			writer.WriteLine ();
		}

		// Write native constructor declarations (only for [Register] constructors)
		foreach (var ctor in type.JavaConstructors) {
			if (ctor.IsExport) {
				continue;
			}
			writer.Write ("\tprivate native void nctor_");
			writer.Write (ctor.ConstructorIndex);
			writer.Write (" (");
			WriteParameterList (ctor.Parameters, writer);
			writer.WriteLine (");");
		}

		if (type.JavaConstructors.Count > 0) {
			writer.WriteLine ();
		}
	}

	/// <summary>
	/// Writes: mono.android.TypeManager.Activate ("ManagedType, Assembly", "param types", this, new java.lang.Object[] { p0, p1 })
	/// </summary>
	static void WriteTypeManagerActivate (JavaPeerInfo type, IReadOnlyList<JniParameterInfo> parameters, TextWriter writer)
	{
		writer.Write ("mono.android.TypeManager.Activate (\"");
		writer.Write (type.ManagedTypeName);
		writer.Write (", ");
		writer.Write (type.AssemblyName);
		writer.Write ("\", \"");

		// Managed parameter type signature
		for (int i = 0; i < parameters.Count; i++) {
			if (i > 0) {
				writer.Write (", ");
			}
			writer.Write (parameters [i].ManagedType);
		}

		writer.Write ("\", this, new java.lang.Object[] { ");
		WriteArgumentList (parameters, writer);
		writer.Write (" })");
	}

	static void WriteMethods (JavaPeerInfo type, TextWriter writer)
	{
		foreach (var method in type.MarshalMethods) {
			if (method.IsConstructor) {
				continue;
			}

			string javaReturnType = JniTypeToJava (method.JniReturnType);
			bool isVoid = method.JniReturnType == "V";

			// Public override wrapper
			writer.Write ("\t@Override\n");
			writer.Write ("\tpublic ");
			writer.Write (javaReturnType);
			writer.Write (' ');
			writer.Write (method.JniName);
			writer.Write (" (");
			WriteParameterList (method.Parameters, writer);
			writer.Write (")\n");

			// throws clause for [Export] methods
			if (method.ThrownNames != null && method.ThrownNames.Count > 0) {
				writer.Write ("\t\tthrows ");
				for (int i = 0; i < method.ThrownNames.Count; i++) {
					if (i > 0) {
						writer.Write (", ");
					}
					writer.Write (method.ThrownNames [i]);
				}
				writer.Write ('\n');
			}

			writer.Write ("\t{\n");

			// Delegate to native method
			writer.Write ("\t\t");
			if (!isVoid) {
				writer.Write ("return ");
			}
			writer.Write (method.NativeCallbackName);
			writer.Write (" (");
			WriteArgumentList (method.Parameters, writer);
			writer.Write (");\n");

			writer.Write ("\t}\n");

			// Native method declaration
			writer.Write ("\tpublic native ");
			writer.Write (javaReturnType);
			writer.Write (' ');
			writer.Write (method.NativeCallbackName);
			writer.Write (" (");
			WriteParameterList (method.Parameters, writer);
			writer.Write (");\n");

			writer.WriteLine ();
		}
	}

	static void WriteClassClose (TextWriter writer)
	{
		writer.WriteLine ('}');
	}

	static void WriteParameterList (IReadOnlyList<JniParameterInfo> parameters, TextWriter writer)
	{
		for (int i = 0; i < parameters.Count; i++) {
			if (i > 0) {
				writer.Write (", ");
			}
			writer.Write (JniTypeToJava (parameters [i].JniType));
			writer.Write (" p");
			writer.Write (i);
		}
	}

	static void WriteArgumentList (IReadOnlyList<JniParameterInfo> parameters, TextWriter writer)
	{
		for (int i = 0; i < parameters.Count; i++) {
			if (i > 0) {
				writer.Write (", ");
			}
			writer.Write ('p');
			writer.Write (i);
		}
	}

	/// <summary>
	/// Converts a JNI type name to a Java source type name.
	/// e.g., "android/app/Activity" → "android.app.Activity"
	/// </summary>
	internal static string JniNameToJavaName (string jniName)
	{
		return jniName.Replace ('/', '.');
	}

	/// <summary>
	/// Extracts the Java package name from a JNI type name.
	/// e.g., "com/example/MainActivity" → "com.example"
	/// Returns null for types without a package.
	/// </summary>
	internal static string? GetJavaPackageName (string jniName)
	{
		int lastSlash = jniName.LastIndexOf ('/');
		if (lastSlash < 0) {
			return null;
		}
		return jniName.Substring (0, lastSlash).Replace ('/', '.');
	}

	/// <summary>
	/// Extracts the simple Java class name from a JNI type name.
	/// e.g., "com/example/MainActivity" → "MainActivity"
	/// e.g., "com/example/Outer$Inner" → "Outer$Inner" (preserves nesting separator)
	/// </summary>
	internal static string GetJavaSimpleName (string jniName)
	{
		int lastSlash = jniName.LastIndexOf ('/');
		return lastSlash >= 0 ? jniName.Substring (lastSlash + 1) : jniName;
	}

	/// <summary>
	/// Converts a JNI type descriptor to a Java source type.
	/// e.g., "V" → "void", "I" → "int", "Landroid/os/Bundle;" → "android.os.Bundle"
	/// </summary>
	internal static string JniTypeToJava (string jniType)
	{
		if (jniType.Length == 1) {
			return jniType [0] switch {
				'V' => "void",
				'Z' => "boolean",
				'B' => "byte",
				'C' => "char",
				'S' => "short",
				'I' => "int",
				'J' => "long",
				'F' => "float",
				'D' => "double",
				_ => throw new ArgumentException ($"Unknown JNI primitive type: {jniType}"),
			};
		}

		// Array types: "[I" → "int[]", "[Ljava/lang/String;" → "java.lang.String[]"
		if (jniType [0] == '[') {
			return JniTypeToJava (jniType.Substring (1)) + "[]";
		}

		// Object types: "Landroid/os/Bundle;" → "android.os.Bundle"
		if (jniType [0] == 'L' && jniType [jniType.Length - 1] == ';') {
			return JniNameToJavaName (jniType.Substring (1, jniType.Length - 2));
		}

		throw new ArgumentException ($"Unknown JNI type descriptor: {jniType}");
	}
}
