// Compatibility shim: NUnit 3.13.3's netstandard2.0 assembly does not include
// TestFixtureSetUpAttribute/TestFixtureTearDownAttribute, but the Java.Interop
// submodule's test code uses them. These are simple aliases for
// OneTimeSetUpAttribute/OneTimeTearDownAttribute.
namespace NUnit.Framework
{
	[System.AttributeUsage (System.AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
	[System.Obsolete ("Use OneTimeSetUpAttribute instead")]
	public class TestFixtureSetUpAttribute : OneTimeSetUpAttribute { }

	[System.AttributeUsage (System.AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
	[System.Obsolete ("Use OneTimeTearDownAttribute instead")]
	public class TestFixtureTearDownAttribute : OneTimeTearDownAttribute { }
}
