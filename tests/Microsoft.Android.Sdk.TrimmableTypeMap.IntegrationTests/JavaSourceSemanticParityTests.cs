using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.JavaCallableWrappers.Adapters;
using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;
using Xamarin.Android.Tools.Bytecode;
using Xunit;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

public partial class ScannerComparisonTests
{
	const string SemanticPeerManagedName = "UserApp.JavaSourceParity.SemanticPeer";
	const string SemanticPeerJavaName = "com/example/parity/SemanticPeer";

	sealed record JavaSemanticModel (
		string Name,
		string BaseName,
		ClassAccessFlags Modifiers,
		IReadOnlyList<string> Interfaces,
		IReadOnlyList<string> Constructors,
		IReadOnlyList<string> Methods,
		IReadOnlyList<string> Fields,
		IReadOnlyList<string> Annotations
	);

	[Fact]
	public void GeneratedJava_HasSemanticParityAndCompiles ()
	{
		var assemblyPaths = JavaSourceParityAssemblyPaths;

		var legacySource = GenerateLegacyJava (assemblyPaths [0]);
		var trimmableSource = GenerateTrimmableJava (assemblyPaths);

		var legacyModel = CompileAndReadSemanticModel ("legacy", legacySource);
		var trimmableModel = CompileAndReadSemanticModel ("trimmable", trimmableSource);

		AssertFixtureCoverage (legacyModel);
		AssertSemanticEquality (legacyModel, trimmableModel);
	}

	static string[] JavaSourceParityAssemblyPaths {
		get {
			var testDirectory = Path.GetDirectoryName (typeof (ScannerComparisonTests).Assembly.Location);
			if (testDirectory is null) {
				throw new InvalidOperationException ("Could not determine the integration test output directory.");
			}

			var paths = new List<string> {
				Path.Combine (testDirectory, "JavaSourceParityFixture.dll"),
				Path.Combine (testDirectory, "Mono.Android.dll"),
			};
			var javaInteropPath = Path.Combine (testDirectory, "Java.Interop.dll");
			if (File.Exists (javaInteropPath)) {
				paths.Add (javaInteropPath);
			}
			foreach (var path in paths) {
				if (!File.Exists (path)) {
					throw new InvalidOperationException ($"Required fixture dependency '{path}' was not found.");
				}
			}
			return paths.ToArray ();
		}
	}

	static string GenerateLegacyJava (string assemblyPath)
	{
		var cache = new TypeDefinitionCache ();
		var resolver = new DefaultAssemblyResolver ();
		var assemblyDirectory = Path.GetDirectoryName (assemblyPath);
		if (assemblyDirectory is null) {
			throw new InvalidOperationException ($"Could not determine the directory for '{assemblyPath}'.");
		}
		resolver.AddSearchDirectory (assemblyDirectory);

		var runtimeDirectory = Path.GetDirectoryName (typeof (object).Assembly.Location);
		if (runtimeDirectory is not null) {
			resolver.AddSearchDirectory (runtimeDirectory);
		}

		var readerParameters = new ReaderParameters { AssemblyResolver = resolver };
		using var assembly = CecilAssemblyDefinition.ReadAssembly (assemblyPath, readerParameters);
		var type = assembly.MainModule.GetType (SemanticPeerManagedName);
		if (type is null) {
			throw new InvalidOperationException ($"Could not find '{SemanticPeerManagedName}' in '{assemblyPath}'.");
		}

		var wrapper = CecilImporter.CreateType (type, cache);
		using var writer = new StringWriter ();
		wrapper.Generate (writer, new CallableWrapperWriterOptions {
			CodeGenerationTarget = JavaPeerStyle.XAJavaInterop1,
		});
		return writer.ToString ();
	}

	static string GenerateTrimmableJava (IReadOnlyList<string> assemblyPaths)
	{
		using var scanner = new JavaPeerScanner ();
		var peReaders = new List<PEReader> ();
		var assemblies = new List<(string Name, PEReader Reader)> ();
		List<JavaPeerInfo> peers;
		try {
			foreach (var path in assemblyPaths) {
				var peReader = new PEReader (File.OpenRead (path));
				peReaders.Add (peReader);
				var metadataReader = peReader.GetMetadataReader ();
				assemblies.Add ((metadataReader.GetString (metadataReader.GetAssemblyDefinition ().Name), peReader));
			}
			peers = scanner.Scan (assemblies);
		} finally {
			foreach (var peReader in peReaders) {
				peReader.Dispose ();
			}
		}

		var peer = peers.SingleOrDefault (p => p.ManagedTypeName == SemanticPeerManagedName);
		if (peer is null) {
			throw new InvalidOperationException ($"Could not scan '{SemanticPeerManagedName}'.");
		}

		using var writer = new StringWriter ();
		new JcwJavaSourceGenerator ().Generate (peer, writer);
		return writer.ToString ();
	}

	static JavaSemanticModel CompileAndReadSemanticModel (string name, string generatedSource)
	{
		var root = Path.Combine (Path.GetTempPath (), $"jcw-semantic-parity-{name}-{Guid.NewGuid ():N}");
		var sourceDirectory = Path.Combine (root, "src");
		var classesDirectory = Path.Combine (root, "classes");
		Directory.CreateDirectory (sourceDirectory);
		Directory.CreateDirectory (classesDirectory);

		try {
			var sourceFiles = WriteJavaSourceSet (sourceDirectory, generatedSource);
			CompileJavaSourceSet (name, sourceFiles, classesDirectory, generatedSource);

			var classPath = Path.Combine (classesDirectory, SemanticPeerJavaName + ".class");
			Assert.True (File.Exists (classPath), $"javac did not produce '{classPath}'.");
			using var stream = File.OpenRead (classPath);
			return CreateSemanticModel (new ClassFile (stream));
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	static List<string> WriteJavaSourceSet (string sourceDirectory, string generatedSource)
	{
		var sources = new Dictionary<string, string> {
			[SemanticPeerJavaName + ".java"] = generatedSource,
			["com/example/parity/Base.java"] = """
				package com.example.parity;

				public class Base {
					public Base () {}
					public Base (int value) {}
					public java.lang.String getValue () { return ""; }
				}
				""",
			["android/view/View.java"] = """
				package android.view;

				public class View {
					public interface OnClickListener {
						void onClick (View view);
					}
					public interface OnLongClickListener {
						boolean onLongClick (View view);
						boolean onLongClickUseDefaultHapticFeedback (View view);
					}
				}
				""",
			["mono/android/IGCUserPeer.java"] = """
				package mono.android;

				public interface IGCUserPeer {
					void monodroidAddReference (java.lang.Object value);
					void monodroidClearReferences ();
				}
				""",
			["mono/android/Runtime.java"] = """
				package mono.android;

				public final class Runtime {
					public static void register (java.lang.String managedName, java.lang.Class<?> javaType, java.lang.String methods) {}
					public static void registerNatives (java.lang.Class<?> javaType) {}
				}
				""",
			["mono/android/TypeManager.java"] = """
				package mono.android;

				public final class TypeManager {
					public static void Activate (java.lang.String managedName, java.lang.String signature, java.lang.Object instance, java.lang.Object[] arguments) {}
				}
				""",
		};

		var paths = new List<string> ();
		foreach (var source in sources) {
			var path = Path.Combine (sourceDirectory, source.Key);
			var directory = Path.GetDirectoryName (path);
			if (directory is null) {
				throw new InvalidOperationException ($"Could not determine the directory for '{path}'.");
			}
			Directory.CreateDirectory (directory);
			File.WriteAllText (path, source.Value);
			paths.Add (path);
		}
		return paths;
	}

	static void CompileJavaSourceSet (string name, IReadOnlyList<string> sourceFiles, string classesDirectory, string generatedSource)
	{
		var compilerPath = GetJavaCompilerPath ();
		var startInfo = new ProcessStartInfo {
			FileName = compilerPath,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		startInfo.ArgumentList.Add ("-d");
		startInfo.ArgumentList.Add (classesDirectory);
		foreach (var sourceFile in sourceFiles) {
			startInfo.ArgumentList.Add (sourceFile);
		}

		using var process = Process.Start (startInfo);
		if (process is null) {
			throw new InvalidOperationException ($"Could not start '{compilerPath}'.");
		}
		var standardOutput = process.StandardOutput.ReadToEndAsync ();
		var standardError = process.StandardError.ReadToEndAsync ();
		if (!process.WaitForExit ((int) TimeSpan.FromMinutes (1).TotalMilliseconds)) {
			process.Kill (entireProcessTree: true);
			process.WaitForExit ();
			var timeoutOutput = standardOutput.GetAwaiter ().GetResult ();
			var timeoutError = standardError.GetAwaiter ().GetResult ();
			throw new TimeoutException (
				$"javac timed out compiling the {name} Java source set.{Environment.NewLine}" +
				$"stdout:{Environment.NewLine}{timeoutOutput}{Environment.NewLine}" +
				$"stderr:{Environment.NewLine}{timeoutError}");
		}

		var output = standardOutput.GetAwaiter ().GetResult ();
		var error = standardError.GetAwaiter ().GetResult ();
		Assert.True (process.ExitCode == 0,
			$"javac failed compiling the {name} Java source set.{Environment.NewLine}" +
			$"stdout:{Environment.NewLine}{output}{Environment.NewLine}" +
			$"stderr:{Environment.NewLine}{error}{Environment.NewLine}" +
			$"generated source:{Environment.NewLine}{generatedSource}");
	}

	static string GetJavaCompilerPath ()
	{
		var attribute = typeof (ScannerComparisonTests).Assembly
			.GetCustomAttributes<AssemblyMetadataAttribute> ()
			.SingleOrDefault (a => a.Key == "JavaCPath");
		if (attribute is null || attribute.Value.IsNullOrEmpty ()) {
			throw new InvalidOperationException ("The JavaCPath assembly metadata value is missing.");
		}
		return attribute.Value;
	}

	static JavaSemanticModel CreateSemanticModel (ClassFile classFile)
	{
		// Classfiles preserve declaration semantics while discarding source formatting
		// and source-only annotations such as @Override, which javac validates above.
		var constructors = classFile.Methods
			.Where (method => method.IsConstructor)
			.Select (FormatMethod)
			.OrderBy (method => method, StringComparer.Ordinal)
			.ToArray ();
		var methods = classFile.Methods
			.Where (IsSemanticMethod)
			.Select (FormatMethod)
			.OrderBy (method => method, StringComparer.Ordinal)
			.ToArray ();
		var fields = classFile.Fields
			.Where (field => field.Name != "refList" && !field.Name.StartsWith ("__md_", StringComparison.Ordinal))
			.Select (FormatField)
			.OrderBy (field => field, StringComparer.Ordinal)
			.ToArray ();
		var interfaces = classFile.Interfaces
			.Select (iface => iface.Name.Value)
			.ToArray ();

		return new JavaSemanticModel (
			classFile.ThisClass.Name.Value,
			classFile.SuperClass.Name.Value,
			GetDeclarationModifiers (classFile.AccessFlags),
			interfaces,
			constructors,
			methods,
			fields,
			GetAnnotations (classFile.Attributes)
		);
	}

	static bool IsSemanticMethod (Xamarin.Android.Tools.Bytecode.MethodInfo method)
	{
		if (method.Name is "<init>" or "<clinit>" ||
		    method.AccessFlags.HasFlag (MethodAccessFlags.Native) ||
		    method.Name is "monodroidAddReference" or "monodroidClearReferences" ||
		    method.Name.StartsWith ("__md_", StringComparison.Ordinal)) {
			return false;
		}
		return true;
	}

	static string FormatMethod (Xamarin.Android.Tools.Bytecode.MethodInfo method)
	{
		var throws = method.GetThrows ()
			.Select (type => type.BinaryName)
			.OrderBy (type => type, StringComparer.Ordinal);
		var annotations = GetAnnotations (method.Attributes);
		return $"{GetVisibility (method.AccessFlags)}|" +
			$"static={method.AccessFlags.HasFlag (MethodAccessFlags.Static)}|" +
			$"abstract={method.AccessFlags.HasFlag (MethodAccessFlags.Abstract)}|" +
			$"bridge={method.AccessFlags.HasFlag (MethodAccessFlags.Bridge)}|" +
			$"synthetic={method.AccessFlags.HasFlag (MethodAccessFlags.Synthetic)}|" +
			$"{method.Name}|{method.Descriptor}|" +
			$"final={method.AccessFlags.HasFlag (MethodAccessFlags.Final)}|" +
			$"synchronized={method.AccessFlags.HasFlag (MethodAccessFlags.Synchronized)}|" +
			$"varargs={method.AccessFlags.HasFlag (MethodAccessFlags.Varargs)}|" +
			$"strict={method.AccessFlags.HasFlag (MethodAccessFlags.Strict)}|" +
			$"throws={string.Join (",", throws)}|" +
			$"annotations={string.Join (",", annotations)}";
	}

	static string FormatField (Xamarin.Android.Tools.Bytecode.FieldInfo field)
	{
		var annotations = GetAnnotations (field.Attributes);
		return $"{GetVisibility (field.AccessFlags)}|" +
			$"static={field.AccessFlags.HasFlag (FieldAccessFlags.Static)}|" +
			$"final={field.AccessFlags.HasFlag (FieldAccessFlags.Final)}|" +
			$"{field.Name}|{field.Descriptor}|" +
			$"volatile={field.AccessFlags.HasFlag (FieldAccessFlags.Volatile)}|" +
			$"transient={field.AccessFlags.HasFlag (FieldAccessFlags.Transient)}|" +
			$"synthetic={field.AccessFlags.HasFlag (FieldAccessFlags.Synthetic)}|" +
			$"enum={field.AccessFlags.HasFlag (FieldAccessFlags.Enum)}|" +
			$"annotations={string.Join (",", annotations)}";
	}

	static string[] GetAnnotations (AttributeCollection attributes)
	{
		return attributes
			.OfType<RuntimeVisibleAnnotationsAttribute> ()
			.SelectMany (attribute => attribute.Annotations)
			.Concat (attributes
				.OfType<RuntimeInvisibleAnnotationsAttribute> ()
				.SelectMany (attribute => attribute.Annotations))
			.Select (annotation => annotation.ToString ())
			.OrderBy (annotation => annotation, StringComparer.Ordinal)
			.ToArray ();
	}

	static ClassAccessFlags GetDeclarationModifiers (ClassAccessFlags flags)
	{
		return flags & (
			ClassAccessFlags.Public |
			ClassAccessFlags.Private |
			ClassAccessFlags.Protected |
			ClassAccessFlags.Static |
			ClassAccessFlags.Final |
			ClassAccessFlags.Interface |
			ClassAccessFlags.Abstract |
			ClassAccessFlags.Synthetic |
			ClassAccessFlags.Annotation |
			ClassAccessFlags.Enum
		);
	}

	static string GetVisibility (MethodAccessFlags flags)
	{
		if (flags.HasFlag (MethodAccessFlags.Public)) {
			return "public";
		}
		if (flags.HasFlag (MethodAccessFlags.Protected)) {
			return "protected";
		}
		if (flags.HasFlag (MethodAccessFlags.Private)) {
			return "private";
		}
		return "package";
	}

	static string GetVisibility (FieldAccessFlags flags)
	{
		if (flags.HasFlag (FieldAccessFlags.Public)) {
			return "public";
		}
		if (flags.HasFlag (FieldAccessFlags.Protected)) {
			return "protected";
		}
		if (flags.HasFlag (FieldAccessFlags.Private)) {
			return "private";
		}
		return "package";
	}

	static void AssertFixtureCoverage (JavaSemanticModel model)
	{
		Assert.Equal (SemanticPeerJavaName, model.Name);
		Assert.Equal ("com/example/parity/Base", model.BaseName);
		Assert.Equal (ClassAccessFlags.Public, model.Modifiers);
		Assert.Equal (new [] {
			"mono/android/IGCUserPeer",
			"android/view/View$OnClickListener",
			"android/view/View$OnLongClickListener",
		}, model.Interfaces);
		Assert.Contains ("public|static=False|abstract=False|bridge=False|synthetic=False|<init>|()V|final=False|synchronized=False|varargs=False|strict=False|throws=|annotations=", model.Constructors);
		Assert.Contains ("public|static=False|abstract=False|bridge=False|synthetic=False|<init>|(I)V|final=False|synchronized=False|varargs=False|strict=False|throws=|annotations=", model.Constructors);
		Assert.Contains (model.Methods, method => method.Contains ("public|static=False|abstract=False|bridge=False|synthetic=False|getValue|()Ljava/lang/String;", StringComparison.Ordinal));
		Assert.Contains (model.Methods, method => method.Contains ("public|static=False|abstract=False|bridge=False|synthetic=False|onClick|(Landroid/view/View;)V", StringComparison.Ordinal));
		Assert.Contains (model.Methods, method => method.Contains ("public|static=False|abstract=False|bridge=False|synthetic=False|onLongClick|(Landroid/view/View;)Z", StringComparison.Ordinal));
		Assert.Contains (model.Methods, method => method.Contains ("protected|static=False|abstract=False|bridge=False|synthetic=False|checkedExport|(Ljava/lang/String;)I|final=False|synchronized=False|varargs=False|strict=False|throws=java/io/IOException", StringComparison.Ordinal));
		Assert.Contains (model.Fields, field => field.Contains ("public|static=True|final=False|STATIC_LABEL|Ljava/lang/String;", StringComparison.Ordinal));
		Assert.Contains (model.Fields, field => field.Contains ("public|static=False|final=False|LABEL|Ljava/lang/String;", StringComparison.Ordinal));
	}

	static void AssertSemanticEquality (JavaSemanticModel expected, JavaSemanticModel actual)
	{
		Assert.Equal (expected.Name, actual.Name);
		Assert.Equal (expected.BaseName, actual.BaseName);
		Assert.Equal (expected.Modifiers, actual.Modifiers);
		Assert.Equal (expected.Interfaces, actual.Interfaces);
		Assert.Equal (expected.Constructors, actual.Constructors);
		Assert.Equal (expected.Methods, actual.Methods);
		Assert.Equal (expected.Fields, actual.Fields);
		Assert.Equal (expected.Annotations, actual.Annotations);
	}
}
