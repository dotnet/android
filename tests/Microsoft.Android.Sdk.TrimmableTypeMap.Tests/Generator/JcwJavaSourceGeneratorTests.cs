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

	static void AssertContainsLine (string expected, string actual)
	{
		Assert.Contains (
			expected.ReplaceLineEndings ("\n"),
			actual.ReplaceLineEndings ("\n")
		);
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
		[InlineData ("android/view/View$OnClickListener", "android.view.View.OnClickListener")]
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
			var acwTypes = peers.Where (p => !p.DoNotGenerateAcw && !p.IsInterface).ToList ();
			Assert.DoesNotContain (acwTypes, p => p.JavaName == "java/lang/Object");
			Assert.DoesNotContain (acwTypes, p => p.JavaName == "android/app/Activity");
			Assert.Contains (acwTypes, p => p.JavaName == "my/app/MainActivity");
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
		public void Generate_MainActivity_HasIGCUserPeerMethods ()
		{
			var java = GenerateFixture ("my/app/MainActivity");
			Assert.Contains ("private java.util.ArrayList refList;", java);
			Assert.Contains ("public void monodroidAddReference (java.lang.Object obj)", java);
			Assert.Contains ("public void monodroidClearReferences ()", java);
		}

		[Fact]
		public void Generate_AbstractType_HasAbstractModifier ()
		{
			var java = GenerateFixture ("my/app/AbstractBase");
			Assert.Contains ("public abstract class AbstractBase\n", java);
		}

		[Fact]
		public void Generate_ClickableView_UsesDotsForNestedInterfaceName ()
		{
			var java = GenerateFixture ("my/app/ClickableView");
			Assert.Contains ("\t\tandroid.view.View.OnClickListener", java);
			Assert.DoesNotContain ("View$OnClickListener", java);
		}

	}

	public class StaticInitializer
	{

		[Fact]
		public void Generate_AcwType_HasRegisterNativesStaticBlock ()
		{
			var java = GenerateFixture ("my/app/MainActivity");
			AssertContainsLine ("static {\n", java);
			AssertContainsLine ("mono.android.Runtime.registerNatives (MainActivity.class);\n", java);
		}

		[Fact]
		public void Generate_ApplicationType_SkipsRegisterNatives ()
		{
			var java = GenerateFixture ("my/app/MyApplication");
			Assert.DoesNotContain ("registerNatives", java);
			Assert.DoesNotContain ("static {", java);
		}

		[Fact]
		public void Generate_InstrumentationType_SkipsRegisterNatives ()
		{
			var java = GenerateFixture ("my/app/MyInstrumentation");
			Assert.DoesNotContain ("registerNatives", java);
			Assert.DoesNotContain ("static {", java);
		}

		[Fact]
		public void Generate_DerivedApplication_SkipsNativeCtorActivation ()
		{
			var java = GenerateFixture ("my/app/DerivedApplication");
			Assert.DoesNotContain ("nctor_0", java);
		}

		[Fact]
		public void Generate_DerivedInstrumentation_SkipsNativeCtorActivation ()
		{
			var java = GenerateFixture ("my/app/DerivedInstrumentation");
			Assert.DoesNotContain ("nctor_0", java);
		}

	}

	public class Constructor
	{

		[Fact]
		public void Generate_CustomView_HasExpectedConstructorElements ()
		{
			var java = GenerateFixture ("my/app/CustomView");
			AssertContainsLine ("public CustomView ()\n", java);
			AssertContainsLine ("public CustomView (android.content.Context p0)\n", java);
			AssertContainsLine ("private native void nctor_0 ();\n", java);
			AssertContainsLine ("private native void nctor_1 (android.content.Context p0);\n", java);
			AssertContainsLine ("if (getClass () == CustomView.class) nctor_0 ();\n", java);
		}

		[Fact]
		public void Generate_Constructor_WithSuperArgumentsString_UsesCustomSuperArgs ()
		{
			// [Export] constructors with SuperArgumentsString should use it in super() call
			var type = new JavaPeerInfo {
				JavaName = "my/app/CustomService",
				CompatJniName = "my/app/CustomService",
				ManagedTypeName = "MyApp.CustomService",
				ManagedTypeNamespace = "MyApp",
				ManagedTypeShortName = "CustomService",
				AssemblyName = "App",
				BaseJavaName = "android/app/Service",
				JavaConstructors = new List<JavaConstructorInfo> {
					new JavaConstructorInfo {
						JniSignature = "(Landroid/content/Context;I)V",
						ConstructorIndex = 0,
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
				CompatJniName = "my/app/MyWidget",
				ManagedTypeName = "MyApp.MyWidget",
				ManagedTypeNamespace = "MyApp",
				ManagedTypeShortName = "MyWidget",
				AssemblyName = "App",
				BaseJavaName = "android/appwidget/AppWidgetProvider",
				JavaConstructors = new List<JavaConstructorInfo> {
					new JavaConstructorInfo {
						JniSignature = "(Landroid/content/Context;)V",
						ConstructorIndex = 0,
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
				CompatJniName = "my/app/MyView",
				ManagedTypeName = "MyApp.MyView",
				ManagedTypeNamespace = "MyApp",
				ManagedTypeShortName = "MyView",
				AssemblyName = "App",
				BaseJavaName = "android/view/View",
				JavaConstructors = new List<JavaConstructorInfo> {
					new JavaConstructorInfo {
						JniSignature = "(Landroid/content/Context;Landroid/util/AttributeSet;)V",
						ConstructorIndex = 0,
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
			AssertContainsLine ("@Override\n", java);
			AssertContainsLine ("public void onCreate (android.os.Bundle p0)\n", java);
			AssertContainsLine ("n_OnCreate_Landroid_os_Bundle_ (p0);\n", java);
			AssertContainsLine ("public native void n_OnCreate_Landroid_os_Bundle_ (android.os.Bundle p0);\n", java);
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

	public class JniNameValidation
	{

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
		public void ValidateJniName_InvalidName_Throws (string badJniName)
		{
			Assert.Throws<ArgumentException> (() => JniSignatureHelper.ValidateJniName (badJniName));
		}

		[Theory]
		[InlineData ("com/example/MainActivity")]
		[InlineData ("my/app/Outer$Inner")]
		[InlineData ("SingleSegment")]
		[InlineData ("com/example/_Private")]
		[InlineData ("com/example/$Generated")]
		public void ValidateJniName_ValidName_DoesNotThrow (string validJniName)
		{
			JniSignatureHelper.ValidateJniName (validJniName);
		}

	}

	public class ExportWithThrowsClause
	{

		[Fact]
		public void Generate_ExportWithThrows_HasThrowsClause ()
		{
			var java = GenerateFixture ("my/app/ExportWithThrows");
			AssertContainsLine ("throws java.io.IOException, java.lang.IllegalStateException\n", java);
		}

	}

	public class MethodReturnTypesAndParams
	{

		[Fact]
		public void Generate_TouchHandler_HasExpectedMethodSignatures ()
		{
			var java = GenerateFixture ("my/app/TouchHandler");
			AssertContainsLine ("public boolean onTouch (android.view.View p0, int p1)\n", java);
			AssertContainsLine ("public void onScroll (int p0, float p1, long p2, double p3)\n", java);
			AssertContainsLine ("public java.lang.String getText ()\n", java);
			AssertContainsLine ("public void setItems (java.lang.String[] p0)\n", java);
		}

	}
}
