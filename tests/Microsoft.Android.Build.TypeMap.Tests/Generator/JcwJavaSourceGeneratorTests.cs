using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Build.TypeMap.Tests;

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

	JavaPeerInfo FindByJavaName (List<JavaPeerInfo> peers, string javaName)
	{
		var peer = peers.FirstOrDefault (p => p.JavaName == javaName);
		Assert.NotNull (peer);
		return peer;
	}

	string GenerateToString (JavaPeerInfo type)
	{
		var generator = new JcwJavaSourceGenerator ();
		using var writer = new StringWriter ();
		generator.Generate (type, writer);
		return writer.ToString ();
	}

	// ---- JNI name conversion tests ----

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

	// ---- Filtering tests ----

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

	// ---- Package declaration tests ----

	[Fact]
	public void Generate_MainActivity_HasPackageDeclaration ()
	{
		var peers = ScanFixtures ();
		var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
		var java = GenerateToString (mainActivity);
		Assert.StartsWith ("package my.app;\n", java);
	}

	// ---- Class declaration tests ----

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

	// ---- Static initializer tests ----

	[Fact]
	public void Generate_AcwType_HasRegisterNativesStaticBlock ()
	{
		var peers = ScanFixtures ();
		var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
		var java = GenerateToString (mainActivity);
		Assert.Contains ("static {\n", java);
		Assert.Contains ("mono.android.Runtime.registerNatives (MainActivity.class);\n", java);
	}

	// ---- Constructor tests ----

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

	// ---- Method tests ----

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

	// ---- Nested type tests ----

	[Fact]
	public void Generate_NestedType_HasCorrectPackageAndClassName ()
	{
		var peers = ScanFixtures ();
		var inner = FindByJavaName (peers, "my/app/Outer$Inner");
		var java = GenerateToString (inner);
		Assert.Contains ("package my.app;\n", java);
		Assert.Contains ("public class Outer$Inner\n", java);
	}

	// ---- Output file path tests ----

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

	// ---- [Export] with throws clause ----

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
