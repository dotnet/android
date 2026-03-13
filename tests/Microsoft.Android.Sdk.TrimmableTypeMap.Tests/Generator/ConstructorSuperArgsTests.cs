using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Tests for constructor super() argument matching.
/// The legacy pipeline selects super() arguments based on base registered ctors:
/// - Compatible params → forward all (super(p0, p1, ...))
/// - No compatible params, parameterless base exists → super()
/// - [Export(SuperArgumentsString="...")] → use custom string
/// </summary>
public class ConstructorSuperArgsTests : FixtureTestBase
{
	static string GenerateToString (JavaPeerInfo type)
	{
		var generator = new JcwJavaSourceGenerator ();
		using var writer = new StringWriter ();
		generator.Generate (type, writer);
		return writer.ToString ();
	}

	[Fact]
	public void MatchingBaseCtor_ForwardsAllParams ()
	{
		// UserActivity extends Activity which has [Register(".ctor","()V")]
		// UserActivity has activation ctor (IntPtr, JniHandleOwnership) — not a user ctor
		// The base ()V ctor should be inherited as a seed constructor
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		Assert.NotEmpty (peer.JavaConstructors);
		var ctor = peer.JavaConstructors.First (c => c.JniSignature == "()V");
		// null SuperArgumentsString means "forward all params"
		Assert.Null (ctor.SuperArgumentsString);
	}

	[Fact]
	public void CustomParamCtor_FallsBackToEmptySuper ()
	{
		// CustomParamActivity has ctor(string, int) which doesn't match Activity's ()V
		// But Activity has parameterless ctor → fallback to super()
		var peer = FindFixtureByJavaName ("my/app/CustomParamActivity");
		var customCtor = peer.JavaConstructors.FirstOrDefault (c => c.JniSignature != "()V");
		Assert.NotNull (customCtor);
		Assert.Equal ("", customCtor.SuperArgumentsString);
	}

	[Fact]
	public void CustomParamCtor_JcwEmitsEmptySuper ()
	{
		var peer = FindFixtureByJavaName ("my/app/CustomParamActivity");
		var java = GenerateToString (peer);
		// The custom-param ctor should call super() not super(p0, p1)
		Assert.Contains ("super ();", java);
	}

	[Fact]
	public void ExportSuperArgs_UsesCustomString ()
	{
		// From existing test fixtures — uses SuperArgumentsString
		var type = new JavaPeerInfo {
			JavaName = "my/app/ExportCtorTest",
			CompatJniName = "my/app/ExportCtorTest",
			ManagedTypeName = "MyApp.ExportCtorTest",
			ManagedTypeNamespace = "MyApp",
			ManagedTypeShortName = "ExportCtorTest",
			AssemblyName = "App",
			BaseJavaName = "android/app/Service",
			JavaConstructors = new System.Collections.Generic.List<JavaConstructorInfo> {
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
}
