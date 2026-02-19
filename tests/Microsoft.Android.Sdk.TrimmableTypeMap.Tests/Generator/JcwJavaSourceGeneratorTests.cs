using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class JcwJavaSourceGeneratorTests : FixtureTestBase
{
	static string GenerateToString (JavaPeerInfo type)
	{
		var generator = new JcwJavaSourceGenerator ();
		using var writer = new StringWriter ();
		generator.Generate (type, writer);
		return writer.ToString ();
	}

	static string GenerateFixture (string javaName)
	{
		var peer = FindFixtureByJavaName (javaName);
		return GenerateToString (peer);
	}


	public class JniNameConversion
	{

		[Theory]
		[InlineData ("android/app/Activity", "android.app.Activity")]
		[InlineData ("java/lang/Object", "java.lang.Object")]
		[InlineData ("android/view/View$OnClickListener", "android.view.View$OnClickListener")]
		public void JniNameToJavaName_ConvertsCorrectly (string jniName, string expected)
		{
			Assert.Equal (expected, JniSignatureHelper.JniNameToJavaName (jniName));
		}

		[Theory]
		[InlineData ("com/example/MainActivity", "com.example")]
		[InlineData ("java/lang/Object", "java.lang")]
		[InlineData ("TopLevelClass", null)]
		public void GetJavaPackageName_ExtractsCorrectly (string jniName, string? expected)
		{
			Assert.Equal (expected, JniSignatureHelper.GetJavaPackageName (jniName));
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
			Assert.Equal (expected, JniSignatureHelper.JniTypeToJava (jniType));
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
				Assert.DoesNotContain (files, f => f.EndsWith ("java/lang/Object.java"));
				Assert.DoesNotContain (files, f => f.EndsWith ("android/app/Activity.java"));
				Assert.Contains (files, f => f.Replace ('\\', '/').Contains ("my/app/MainActivity.java"));
			} finally {
				if (Directory.Exists (outputDir)) {
					Directory.Delete (outputDir, true);
				}
			}
		}

	}

	public class ClassDeclaration
	{

		[Fact]
		public void Generate_MainActivity_HasClassDeclaration ()
		{
			var java = GenerateFixture ("my/app/MainActivity");
			Assert.Contains ("public class MainActivity\n", java);
			Assert.Contains ("\textends android.app.Activity\n", java);
			Assert.Contains ("\t\tmono.android.IGCUserPeer\n", java);
		}

		[Fact]
		public void Generate_AbstractType_HasAbstractModifier ()
		{
			var java = GenerateFixture ("my/app/AbstractBase");
			Assert.Contains ("public abstract class AbstractBase\n", java);
		}

	}

	public class StaticInitializer
	{

		[Fact]
		public void Generate_AcwType_HasRegisterNativesStaticBlock ()
		{
			var java = GenerateFixture ("my/app/MainActivity");
			Assert.Contains ("static {\n", java);
			Assert.Contains ("mono.android.Runtime.registerNatives (MainActivity.class);\n", java);
		}

	}

	public class Constructor
	{

		[Fact]
		public void Generate_CustomView_HasExpectedConstructorElements ()
		{
			var java = GenerateFixture ("my/app/CustomView");
			Assert.Contains ("public CustomView ()\n", java);
			Assert.Contains ("public CustomView (android.content.Context p0)\n", java);
			Assert.Contains ("private native void nctor_0 ();\n", java);
			Assert.Contains ("private native void nctor_1 (android.content.Context p0);\n", java);
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
			var java = GenerateFixture ("my/app/MainActivity");
			Assert.Contains ("@Override\n", java);
			Assert.Contains ("public void onCreate (android.os.Bundle p0)\n", java);
			Assert.Contains ("n_OnCreate (p0);\n", java);
			Assert.Contains ("public native void n_OnCreate (android.os.Bundle p0);\n", java);
		}

	}

	public class NestedType
	{

		[Fact]
		public void Generate_NestedType_HasCorrectPackageAndClassName ()
		{
			var java = GenerateFixture ("my/app/Outer$Inner");
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

		[Theory]
		[InlineData ("")]
		[InlineData ("com//Example")]
		[InlineData ("/com/Example")]
		[InlineData ("com/Example/")]
		[InlineData ("com/1Invalid")]
		[InlineData ("com/../etc/passwd")]
		[InlineData ("com\\..\\.\\secret")]
		[InlineData ("C:\\Windows\\System32")]
		[InlineData ("com/Ex:ample")]
		[InlineData ("/absolute/path")]
		public void Generate_InvalidJniName_Throws (string badJniName)
		{
			var peer = MakeAcwPeer (badJniName, "Test.Bad", "TestApp");
			var generator = new JcwJavaSourceGenerator ();
			var outputDir = Path.Combine (Path.GetTempPath (), $"jcw-test-{Guid.NewGuid ():N}");
			Assert.Throws<ArgumentException> (() => generator.Generate (new [] { peer }, outputDir));
		}

		[Theory]
		[InlineData ("com/example/MainActivity")]
		[InlineData ("my/app/Outer$Inner")]
		[InlineData ("SingleSegment")]
		[InlineData ("com/example/_Private")]
		[InlineData ("com/example/$Generated")]
		public void Generate_ValidJniName_DoesNotThrow (string validJniName)
		{
			var peer = MakeAcwPeer (validJniName, "Test.Valid", "TestApp");
			var generator = new JcwJavaSourceGenerator ();
			var outputDir = Path.Combine (Path.GetTempPath (), $"jcw-test-{Guid.NewGuid ():N}");
			try {
				generator.Generate (new [] { peer }, outputDir);
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
			var java = GenerateFixture ("my/app/ExportWithThrows");
			Assert.Contains ("throws java.io.IOException, java.lang.IllegalStateException\n", java);
		}

	}

	public class MethodReturnTypesAndParams
	{

		[Fact]
		public void Generate_TouchHandler_HasExpectedMethodSignatures ()
		{
			var java = GenerateFixture ("my/app/TouchHandler");
			Assert.Contains ("public boolean onTouch (android.view.View p0, int p1)\n", java);
			Assert.Contains ("public void onScroll (int p0, float p1, long p2, double p3)\n", java);
			Assert.Contains ("public java.lang.String getText ()\n", java);
			Assert.Contains ("public void setItems (java.lang.String[] p0)\n", java);
		}

	}
}