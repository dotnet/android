using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class JcwJavaSourceGeneratorTests
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (JcwJavaSourceGeneratorTests).Assembly.Location)!;
			var fixtureAssembly = Path.Combine (testAssemblyDir, "TestFixtures.dll");
			Assert.True (File.Exists (fixtureAssembly),
				$"TestFixtures.dll not found at {fixtureAssembly}. Ensure the TestFixtures project builds.");
			return fixtureAssembly;
		}
	}

	static readonly Lazy<List<JavaPeerInfo>> _cachedFixtures = new (() => {
		using var scanner = new JavaPeerScanner ();
		return scanner.Scan (new [] { TestFixtureAssemblyPath });
	});

	static List<JavaPeerInfo> ScanFixtures () => _cachedFixtures.Value;

	static JavaPeerInfo FindByJavaName (List<JavaPeerInfo> peers, string javaName)
	{
		var peer = peers.FirstOrDefault (p => p.JavaName == javaName);
		Assert.NotNull (peer);
		return peer;
	}

	static string GenerateToString (JavaPeerInfo type)
	{
		var generator = new JcwJavaSourceGenerator ();
		using var writer = new StringWriter ();
		generator.Generate (type, writer);
		return writer.ToString ();
	}


	public class JniNameConversion
	{

		[Theory]
		[InlineData ("android/app/Activity", "android.app.Activity")]
		[InlineData ("java/lang/Object", "java.lang.Object")]
		[InlineData ("android/view/View$OnClickListener", "android.view.View$OnClickListener")]
		public void JniNameToJavaName_ConvertsCorrectly (string jniName, string expected)
		{
			Assert.Equal (expected, JcwJavaSourceGenerator.JniNameToJavaName (jniName));
		}

		[Theory]
		[InlineData ("com/example/MainActivity", "com.example")]
		[InlineData ("java/lang/Object", "java.lang")]
		[InlineData ("TopLevelClass", null)]
		public void GetJavaPackageName_ExtractsCorrectly (string jniName, string? expected)
		{
			Assert.Equal (expected, JcwJavaSourceGenerator.GetJavaPackageName (jniName));
		}

		[Theory]
		[InlineData ("com/example/MainActivity", "MainActivity")]
		[InlineData ("com/example/Outer$Inner", "Outer$Inner")]
		[InlineData ("TopLevelClass", "TopLevelClass")]
		public void GetJavaSimpleName_ExtractsCorrectly (string jniName, string expected)
		{
			Assert.Equal (expected, JcwJavaSourceGenerator.GetJavaSimpleName (jniName));
		}

		[Theory]
		[InlineData ("V", "void")]
		[InlineData ("Z", "boolean")]
		[InlineData ("B", "byte")]
		[InlineData ("I", "int")]
		[InlineData ("J", "long")]
		[InlineData ("F", "float")]
		[InlineData ("D", "double")]
		[InlineData ("Landroid/os/Bundle;", "android.os.Bundle")]
		[InlineData ("[I", "int[]")]
		[InlineData ("[Ljava/lang/String;", "java.lang.String[]")]
		public void JniTypeToJava_ConvertsCorrectly (string jniType, string expected)
		{
			Assert.Equal (expected, JcwJavaSourceGenerator.JniTypeToJava (jniType));
		}

	}

	public class Filtering
	{

		[Fact]
		public void Generate_SkipsMcwTypes ()
		{
			var peers = ScanFixtures ();
			var generator = new JcwJavaSourceGenerator ();
			var outputDir = Path.Combine (Path.GetTempPath (), $"jcw-test-{Guid.NewGuid ():N}");
			try {
				var files = generator.Generate (peers, outputDir);
				// MCW types like java/lang/Object, android/app/Activity should NOT be generated
				Assert.DoesNotContain (files, f => f.EndsWith ("java/lang/Object.java"));
				Assert.DoesNotContain (files, f => f.EndsWith ("android/app/Activity.java"));
				// User ACW types should be generated
				Assert.Contains (files, f => f.Replace ('\\', '/').Contains ("my/app/MainActivity.java"));
			} finally {
				if (Directory.Exists (outputDir)) {
					Directory.Delete (outputDir, true);
				}
			}
		}

	}

	public class PackageDeclaration
	{

		[Fact]
		public void Generate_MainActivity_HasPackageDeclaration ()
		{
			var peers = ScanFixtures ();
			var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
			var java = GenerateToString (mainActivity);
			Assert.StartsWith ("package my.app;\n", java);
		}

	}

	public class ClassDeclaration
	{

		[Fact]
		public void Generate_MainActivity_HasClassDeclaration ()
		{
			var peers = ScanFixtures ();
			var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
			var java = GenerateToString (mainActivity);
			Assert.Contains ("public class MainActivity\n", java);
			Assert.Contains ("\textends android.app.Activity\n", java);
			Assert.Contains ("\t\tmono.android.IGCUserPeer\n", java);
		}

		[Fact]
		public void Generate_AbstractType_HasAbstractModifier ()
		{
			var peers = ScanFixtures ();
			var abstractBase = FindByJavaName (peers, "my/app/AbstractBase");
			var java = GenerateToString (abstractBase);
			Assert.Contains ("public abstract class AbstractBase\n", java);
		}

		[Fact]
		public void Generate_TypeWithInterfaces_HasImplementsClause ()
		{
			var peers = ScanFixtures ();
			var multiView = FindByJavaName (peers, "my/app/MultiInterfaceView");
			var java = GenerateToString (multiView);
			Assert.Contains ("\timplements\n", java);
			Assert.Contains ("\t\tmono.android.IGCUserPeer", java);
			Assert.Contains ("android.view.View$OnClickListener", java);
			Assert.Contains ("android.view.View$OnLongClickListener", java);
		}

	}

	public class StaticInitializer
	{

		[Fact]
		public void Generate_AcwType_HasRegisterNativesStaticBlock ()
		{
			var peers = ScanFixtures ();
			var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
			var java = GenerateToString (mainActivity);
			Assert.Contains ("static {\n", java);
			Assert.Contains ("mono.android.Runtime.registerNatives (MainActivity.class);\n", java);
		}

	}

	public class Constructor
	{

		[Fact]
		public void Generate_CustomView_HasConstructors ()
		{
			var peers = ScanFixtures ();
			var customView = FindByJavaName (peers, "my/app/CustomView");
			var java = GenerateToString (customView);

			// Default constructor
			Assert.Contains ("public CustomView ()\n", java);
			Assert.Contains ("super ();\n", java);
			Assert.Contains ("nctor_0 ();\n", java);

			// Context constructor
			Assert.Contains ("public CustomView (android.content.Context p0)\n", java);
			Assert.Contains ("super (p0);\n", java);
			Assert.Contains ("nctor_1 (p0);\n", java);
		}

		[Fact]
		public void Generate_CustomView_HasNativeConstructorDeclarations ()
		{
			var peers = ScanFixtures ();
			var customView = FindByJavaName (peers, "my/app/CustomView");
			var java = GenerateToString (customView);
			Assert.Contains ("private native void nctor_0 ();\n", java);
			Assert.Contains ("private native void nctor_1 (android.content.Context p0);\n", java);
		}

		[Fact]
		public void Generate_Constructor_HasActivationGuard ()
		{
			var peers = ScanFixtures ();
			var customView = FindByJavaName (peers, "my/app/CustomView");
			var java = GenerateToString (customView);
			Assert.Contains ("if (getClass () == CustomView.class) nctor_0 ();\n", java);
		}

		[Fact]
		public void Generate_Constructor_WithSuperArgumentsString_UsesCustomSuperArgs ()
		{
			// [Export] constructors with SuperArgumentsString should use it in super() call
			var type = new JavaPeerInfo {
				JavaName = "my/app/CustomService",
				ManagedTypeName = "MyApp.CustomService",
				ManagedTypeNamespace = "MyApp",
				ManagedTypeShortName = "CustomService",
				AssemblyName = "App",
				BaseJavaName = "android/app/Service",
				JavaConstructors = new List<JavaConstructorInfo> {
					new JavaConstructorInfo {
						JniSignature = "(Landroid/content/Context;I)V",
						ConstructorIndex = 0,
						Parameters = new List<JniParameterInfo> {
							new JniParameterInfo { JniType = "Landroid/content/Context;" },
							new JniParameterInfo { JniType = "I" },
						},
						SuperArgumentsString = "p0",
					},
				},
			};

			var java = GenerateToString (type);
			// super() should use the custom args, not all parameters
			Assert.Contains ("super (p0);", java);
			Assert.DoesNotContain ("super (p0, p1);", java);
		}

		[Fact]
		public void Generate_Constructor_WithEmptySuperArgumentsString_EmptySuper ()
		{
			// Empty string means super() with no arguments
			var type = new JavaPeerInfo {
				JavaName = "my/app/MyWidget",
				ManagedTypeName = "MyApp.MyWidget",
				ManagedTypeNamespace = "MyApp",
				ManagedTypeShortName = "MyWidget",
				AssemblyName = "App",
				BaseJavaName = "android/appwidget/AppWidgetProvider",
				JavaConstructors = new List<JavaConstructorInfo> {
					new JavaConstructorInfo {
						JniSignature = "(Landroid/content/Context;)V",
						ConstructorIndex = 0,
						Parameters = new List<JniParameterInfo> {
							new JniParameterInfo { JniType = "Landroid/content/Context;" },
						},
						SuperArgumentsString = "",
					},
				},
			};

			var java = GenerateToString (type);
			Assert.Contains ("super ();", java);
			Assert.DoesNotContain ("super (p0);", java);
		}

		[Fact]
		public void Generate_Constructor_WithoutSuperArgumentsString_ForwardsAllParams ()
		{
			// null SuperArgumentsString means forward all params (default behavior)
			var type = new JavaPeerInfo {
				JavaName = "my/app/MyView",
				ManagedTypeName = "MyApp.MyView",
				ManagedTypeNamespace = "MyApp",
				ManagedTypeShortName = "MyView",
				AssemblyName = "App",
				BaseJavaName = "android/view/View",
				JavaConstructors = new List<JavaConstructorInfo> {
					new JavaConstructorInfo {
						JniSignature = "(Landroid/content/Context;Landroid/util/AttributeSet;)V",
						ConstructorIndex = 0,
						Parameters = new List<JniParameterInfo> {
							new JniParameterInfo { JniType = "Landroid/content/Context;" },
							new JniParameterInfo { JniType = "Landroid/util/AttributeSet;" },
						},
					},
				},
			};

			var java = GenerateToString (type);
			Assert.Contains ("super (p0, p1);", java);
		}

	}

	public class Method
	{

		[Fact]
		public void Generate_MarshalMethod_HasOverrideAndNativeDeclaration ()
		{
			var peers = ScanFixtures ();
			var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
			var java = GenerateToString (mainActivity);
			Assert.Contains ("@Override\n", java);
			Assert.Contains ("public void onCreate (android.os.Bundle p0)\n", java);
			Assert.Contains ("n_OnCreate (p0);\n", java);
			Assert.Contains ("public native void n_OnCreate (android.os.Bundle p0);\n", java);
		}

		[Fact]
		public void Generate_MethodWithReturnValue_HasReturnStatement ()
		{
			var peers = ScanFixtures ();
			var touchHandler = FindByJavaName (peers, "my/app/TouchHandler");
			var java = GenerateToString (touchHandler);
			Assert.Contains ("public boolean onTouch (android.view.View p0, int p1)\n", java);
			Assert.Contains ("return n_OnTouch (p0, p1);\n", java);
		}

		[Fact]
		public void Generate_MethodWithMultipleParams_HasAllParameters ()
		{
			var peers = ScanFixtures ();
			var touchHandler = FindByJavaName (peers, "my/app/TouchHandler");
			var java = GenerateToString (touchHandler);
			Assert.Contains ("public void onScroll (int p0, float p1, long p2, double p3)\n", java);
		}

		[Fact]
		public void Generate_MethodWithObjectReturnType_HasCorrectType ()
		{
			var peers = ScanFixtures ();
			var touchHandler = FindByJavaName (peers, "my/app/TouchHandler");
			var java = GenerateToString (touchHandler);
			Assert.Contains ("public java.lang.String getText ()\n", java);
			Assert.Contains ("return n_GetText ();\n", java);
		}

		[Fact]
		public void Generate_MethodWithArrayParam_HasCorrectType ()
		{
			var peers = ScanFixtures ();
			var touchHandler = FindByJavaName (peers, "my/app/TouchHandler");
			var java = GenerateToString (touchHandler);
			Assert.Contains ("public void setItems (java.lang.String[] p0)\n", java);
		}

	}

	public class NestedType
	{

		[Fact]
		public void Generate_NestedType_HasCorrectPackageAndClassName ()
		{
			var peers = ScanFixtures ();
			var inner = FindByJavaName (peers, "my/app/Outer$Inner");
			var java = GenerateToString (inner);
			Assert.Contains ("package my.app;\n", java);
			Assert.Contains ("public class Outer$Inner\n", java);
		}

	}

	public class OutputFilePath
	{

		[Fact]
		public void Generate_CreatesCorrectFileStructure ()
		{
			var peers = ScanFixtures ();
			var generator = new JcwJavaSourceGenerator ();
			var outputDir = Path.Combine (Path.GetTempPath (), $"jcw-test-{Guid.NewGuid ():N}");
			try {
				var files = generator.Generate (peers, outputDir);
				Assert.NotEmpty (files);

				// All files should be under the output directory
				foreach (var file in files) {
					Assert.StartsWith (outputDir, file);
					Assert.True (File.Exists (file), $"Generated file should exist: {file}");
					Assert.EndsWith (".java", file);
				}
			} finally {
				if (Directory.Exists (outputDir)) {
					Directory.Delete (outputDir, true);
				}
			}
		}

	}

	public class ExportWithThrowsClause
	{

		[Fact]
		public void Generate_ExportWithThrows_HasThrowsClause ()
		{
			var peers = ScanFixtures ();
			var peer = FindByJavaName (peers, "my/app/ExportWithThrows");
			var java = GenerateToString (peer);
			Assert.Contains ("throws java.io.IOException, java.lang.IllegalStateException\n", java);
		}

		[Fact]
		public void Generate_ExportWithoutThrows_HasNoThrowsClause ()
		{
			var peers = ScanFixtures ();
			var peer = FindByJavaName (peers, "my/app/ExportExample");
			var java = GenerateToString (peer);
			Assert.DoesNotContain ("throws", java);
		}
	}

	public class ExportConstructor
	{

		[Fact]
		public void Generate_ExportConstructors_UsesTypeManagerActivate ()
		{
			var peers = ScanFixtures ();
			var peer = FindByJavaName (peers, "my/app/ExportsConstructors");
			var java = GenerateToString (peer);

			// [Export] constructors should use TypeManager.Activate
			Assert.Contains ("mono.android.TypeManager.Activate (\"", java);

			// Should NOT have nctor_N native declarations
			Assert.DoesNotContain ("nctor_", java);
		}

		[Fact]
		public void Generate_ExportConstructors_ParameterlessCtorHasEmptySignature ()
		{
			var peers = ScanFixtures ();
			var peer = FindByJavaName (peers, "my/app/ExportsConstructors");
			var java = GenerateToString (peer);

			// Parameterless [Export] ctor should have empty managed param signature
			Assert.Contains ("mono.android.TypeManager.Activate (\"MyApp.ExportsConstructors, TestFixtures\", \"\", this, new java.lang.Object[] {  })", java);
		}

		[Fact]
		public void Generate_ExportConstructors_IntCtorHasIntSignature ()
		{
			var peers = ScanFixtures ();
			var peer = FindByJavaName (peers, "my/app/ExportsConstructors");
			var java = GenerateToString (peer);

			// int parameter [Export] ctor should have managed type signature
			Assert.Contains ("mono.android.TypeManager.Activate (\"MyApp.ExportsConstructors, TestFixtures\", \"System.Int32, System.Private.CoreLib\", this, new java.lang.Object[] { p0 })", java);
		}

		/// <summary>
		/// Full output comparison — ported from legacy GenerateConstructors.
		/// Verifies the complete JCW for [Export] constructors matches the
		/// TypeManager.Activate pattern with correct activation guard.
		/// </summary>
		[Fact]
		public void Generate_ExportConstructors_FullOutput ()
		{
			var peers = ScanFixtures ();
			var peer = FindByJavaName (peers, "my/app/ExportsConstructors");
			var java = GenerateToString (peer);

			// Parameterless ctor: super(), then TypeManager.Activate with empty sig
			Assert.Contains ("\tpublic ExportsConstructors ()\n\t{\n\t\tsuper ();\n\t\tif (getClass () == ExportsConstructors.class) mono.android.TypeManager.Activate (\"MyApp.ExportsConstructors, TestFixtures\", \"\", this, new java.lang.Object[] {  });\n\t}\n", java);

			// int ctor: super(p0), then TypeManager.Activate with int sig
			Assert.Contains ("\tpublic ExportsConstructors (int p0)\n\t{\n\t\tsuper (p0);\n\t\tif (getClass () == ExportsConstructors.class) mono.android.TypeManager.Activate (\"MyApp.ExportsConstructors, TestFixtures\", \"System.Int32, System.Private.CoreLib\", this, new java.lang.Object[] { p0 });\n\t}\n", java);

			// No nctor native declarations
			Assert.DoesNotContain ("private native void nctor_", java);
		}

		/// <summary>
		/// Full output comparison — ported from legacy GenerateConstructors_WithThrows.
		/// Verifies throws clauses appear on ctors that have ThrownNames.
		/// </summary>
		[Fact]
		public void Generate_ExportThrowsConstructors_FullOutput ()
		{
			var peers = ScanFixtures ();
			var peer = FindByJavaName (peers, "my/app/ExportsThrowsConstructors");
			var java = GenerateToString (peer);

			// Parameterless ctor with throws
			Assert.Contains ("\tpublic ExportsThrowsConstructors ()\n\t\tthrows java.lang.Throwable\n\t{\n\t\tsuper ();\n", java);

			// int ctor with throws
			Assert.Contains ("\tpublic ExportsThrowsConstructors (int p0)\n\t\tthrows java.lang.Throwable\n\t{\n\t\tsuper (p0);\n", java);

			// string ctor WITHOUT throws (empty ThrownNames in legacy means [Export] with no Throws)
			Assert.Contains ("\tpublic ExportsThrowsConstructors (java.lang.String p0)\n\t{\n\t\tsuper (p0);\n", java);

			// String ctor should use TypeManager.Activate with String sig
			Assert.Contains ("\"System.String, System.Private.CoreLib\"", java);

			// No nctor native declarations
			Assert.DoesNotContain ("private native void nctor_", java);
		}

		[Fact]
		public void Generate_ExportThrowsConstructors_HasThrowsClause ()
		{
			var peers = ScanFixtures ();
			var peer = FindByJavaName (peers, "my/app/ExportsThrowsConstructors");
			var java = GenerateToString (peer);

			// [Export] constructors with ThrownNames should have throws clause
			Assert.Contains ("throws java.lang.Throwable", java);
		}

		[Fact]
		public void Generate_MixedRegisterAndExportConstructors_HandledCorrectly ()
		{
			// A type with both [Register] and [Export] constructors
			var type = new JavaPeerInfo {
				JavaName = "my/app/MixedCtors",
				ManagedTypeName = "MyApp.MixedCtors",
				ManagedTypeNamespace = "MyApp",
				ManagedTypeShortName = "MixedCtors",
				AssemblyName = "App",
				BaseJavaName = "java/lang/Object",
				JavaConstructors = new List<JavaConstructorInfo> {
					new JavaConstructorInfo {
						JniSignature = "()V",
						ConstructorIndex = 0,
						Parameters = new List<JniParameterInfo> (),
						IsExport = false, // [Register]
					},
					new JavaConstructorInfo {
						JniSignature = "(I)V",
						ConstructorIndex = 1,
						Parameters = new List<JniParameterInfo> {
							new JniParameterInfo { JniType = "I", ManagedType = "System.Int32, System.Private.CoreLib" },
						},
						IsExport = true, // [Export]
					},
				},
			};

			var java = GenerateToString (type);

			// [Register] ctor should use nctor_0
			Assert.Contains ("nctor_0 ()", java);
			Assert.Contains ("private native void nctor_0 ()", java);

			// [Export] ctor should use TypeManager.Activate
			Assert.Contains ("mono.android.TypeManager.Activate (\"MyApp.MixedCtors, App\"", java);

			// Only nctor_0 declaration (not nctor_1 for [Export])
			Assert.DoesNotContain ("nctor_1", java);
		}

		[Fact]
		public void Generate_ExportCtorWithSuperArgs_UsesCustomSuperArgs ()
		{
			var peers = ScanFixtures ();
			var peer = FindByJavaName (peers, "my/app/ExportCtorWithSuperArgs");
			var java = GenerateToString (peer);

			// SuperArgumentsString = "" means super() with no args, not super(p0)
			Assert.Contains ("super ();", java);
			Assert.DoesNotContain ("super (p0);", java);

			// Should still use TypeManager.Activate
			Assert.Contains ("mono.android.TypeManager.Activate (\"", java);
		}

	}

	public class ExportMethodJcw
	{

		/// <summary>
		/// Ported from legacy GenerateExportedMembers — [Export] with name override.
		/// The Java method name should be the export name, not the C# method name.
		/// </summary>
		[Fact]
		public void Generate_ExportWithNameOverride_UsesExportName ()
		{
			var peers = ScanFixtures ();
			var peer = FindByJavaName (peers, "my/app/ExportMembersComprehensive");
			var java = GenerateToString (peer);

			// [Export("attributeOverridesNames")] on CompletelyDifferentName
			// Java method uses export name, native callback uses n_ + C# method name
			Assert.Contains ("public java.lang.String attributeOverridesNames (java.lang.String p0, int p1)", java);
			Assert.Contains ("n_CompletelyDifferentName (p0, p1)", java);
		}

		/// <summary>
		/// Ported from legacy GenerateExportedMembers — [Export] method keeps C# name.
		/// </summary>
		[Fact]
		public void Generate_ExportWithoutNameOverride_UsesMethodName ()
		{
			var peers = ScanFixtures ();
			var peer = FindByJavaName (peers, "my/app/ExportMembersComprehensive");
			var java = GenerateToString (peer);

			// [Export] without name arg uses the C# method name as-is
			Assert.Contains ("public void methodNamesNotMangled ()", java);
			Assert.Contains ("n_methodNamesNotMangled ()", java);
		}

		/// <summary>
		/// Ported from legacy GenerateExportedMembers — [Export] with throws.
		/// </summary>
		[Fact]
		public void Generate_ExportMethodWithThrows_HasThrowsClause ()
		{
			var peers = ScanFixtures ();
			var peer = FindByJavaName (peers, "my/app/ExportMembersComprehensive");
			var java = GenerateToString (peer);

			// throws clause appears on the line after the method signature
			Assert.Contains ("methodThatThrows ()\n\t\tthrows java.lang.Throwable\n", java);
		}

		/// <summary>
		/// Ported from legacy GenerateExportedMembers — [Export] with empty throws array.
		/// Should NOT generate a throws clause.
		/// </summary>
		[Fact]
		public void Generate_ExportMethodWithEmptyThrows_NoThrowsClause ()
		{
			var peers = ScanFixtures ();
			var peer = FindByJavaName (peers, "my/app/ExportMembersComprehensive");
			var java = GenerateToString (peer);

			// methodThatThrowsEmptyArray should NOT have throws clause
			// It should appear as a plain method declaration
			Assert.Contains ("public void methodThatThrowsEmptyArray ()", java);

			// Make sure the throws clause is NOT on this specific method
			// (it might be on methodThatThrows, but not on methodThatThrowsEmptyArray)
			var lines = java.Split ('\n');
			for (int i = 0; i < lines.Length; i++) {
				if (lines [i].Contains ("methodThatThrowsEmptyArray")) {
					// The line with the method should not have throws,
					// and neither should the next line
					Assert.DoesNotContain ("throws", lines [i]);
					if (i + 1 < lines.Length) {
						Assert.DoesNotContain ("throws", lines [i + 1]);
					}
					break;
				}
			}
		}

	}
}