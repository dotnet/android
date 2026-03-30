using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class AssemblyAttributeScanningTests : FixtureTestBase
{
	[Fact]
	public void UsesFeature_ConstructorArg ()
	{
		var info = ScanAssemblyManifestInfo ();
		var feature = info.UsesFeatures.FirstOrDefault (f => f.Name == "android.hardware.camera");
		Assert.NotNull (feature);
		Assert.True (feature.Required);
	}

	[Fact]
	public void UsesFeature_ConstructorArgWithNamedProperty ()
	{
		var info = ScanAssemblyManifestInfo ();
		var feature = info.UsesFeatures.FirstOrDefault (f => f.Name == "android.hardware.camera.autofocus");
		Assert.NotNull (feature);
		Assert.False (feature.Required);
	}

	[Fact]
	public void UsesFeature_GLESVersion ()
	{
		var info = ScanAssemblyManifestInfo ();
		var feature = info.UsesFeatures.FirstOrDefault (f => f.GLESVersion == 0x00020000);
		Assert.NotNull (feature);
		Assert.Null (feature.Name);
	}

	[Fact]
	public void UsesPermission_ConstructorArg ()
	{
		var info = ScanAssemblyManifestInfo ();
		var perm = info.UsesPermissions.FirstOrDefault (p => p.Name == "android.permission.INTERNET");
		Assert.NotNull (perm);
	}

	[Fact]
	public void UsesLibrary_ConstructorArg ()
	{
		var info = ScanAssemblyManifestInfo ();
		var lib = info.UsesLibraries.FirstOrDefault (l => l.Name == "org.apache.http.legacy");
		Assert.NotNull (lib);
		Assert.True (lib.Required);
	}

	[Fact]
	public void UsesLibrary_TwoArgConstructor ()
	{
		var info = ScanAssemblyManifestInfo ();
		var lib = info.UsesLibraries.FirstOrDefault (l => l.Name == "com.example.optional");
		Assert.NotNull (lib);
		Assert.False (lib.Required);
	}

	[Fact]
	public void MetaData_ConstructorArgAndNamedArg ()
	{
		var info = ScanAssemblyManifestInfo ();
		var meta = info.MetaData.FirstOrDefault (m => m.Name == "com.example.key");
		Assert.NotNull (meta);
		Assert.Equal ("test-value", meta.Value);
	}

	[Fact]
	public void UsesPermission_Flags ()
	{
		var info = ScanAssemblyManifestInfo ();
		var perm = info.UsesPermissions.FirstOrDefault (p => p.Name == "android.permission.POST_NOTIFICATIONS");
		Assert.NotNull (perm);
		Assert.Equal ("neverForLocation", perm.UsesPermissionFlags);
	}

	[Fact]
	public void SupportsGLTexture_ConstructorArg ()
	{
		var info = ScanAssemblyManifestInfo ();
		var gl = info.SupportsGLTextures.FirstOrDefault (g => g.Name == "GL_OES_compressed_ETC1_RGB8_texture");
		Assert.NotNull (gl);
	}
}
