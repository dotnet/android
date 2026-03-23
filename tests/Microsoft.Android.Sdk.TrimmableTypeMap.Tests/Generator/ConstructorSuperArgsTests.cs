using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Tests for constructor super() argument matching.
/// </summary>
public class ConstructorSuperArgsTests : FixtureTestBase
{
	[Fact]
	public void MatchingBaseCtor_ForwardsAllParams ()
	{
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		Assert.NotEmpty (peer.JavaConstructors);
		var ctor = peer.JavaConstructors.First (c => c.JniSignature == "()V");
		Assert.Null (ctor.SuperArgumentsString);
	}

	[Fact]
	public void CustomParamCtor_FallsBackToEmptySuper ()
	{
		var peer = FindFixtureByJavaName ("my/app/CustomParamActivity");
		var customCtor = peer.JavaConstructors.FirstOrDefault (c => c.JniSignature != "()V");
		Assert.NotNull (customCtor);
		Assert.Equal ("", customCtor.SuperArgumentsString);

		var generator = new JcwJavaSourceGenerator ();
		using var writer = new StringWriter ();
		generator.Generate (peer, writer);
		Assert.Contains ("super ();", writer.ToString ());
	}

	[Fact]
	public void ExportSuperArgs_UsesCustomString ()
	{
		var type = new JavaPeerInfo {
			JavaName = "my/app/ExportCtorTest",
			CompatJniName = "my/app/ExportCtorTest",
			ManagedTypeName = "MyApp.ExportCtorTest",
			ManagedTypeNamespace = "MyApp",
			ManagedTypeShortName = "ExportCtorTest",
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

		var generator = new JcwJavaSourceGenerator ();
		using var writer = new StringWriter ();
		generator.Generate (type, writer);
		var java = writer.ToString ();
		Assert.Contains ("super (p0);", java);
		Assert.DoesNotContain ("super (p0, p1);", java);
	}
}
