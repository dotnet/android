using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public partial class JavaPeerScannerTests
{
static string TestFixtureAssemblyPath {
get {
var testAssemblyDir = Path.GetDirectoryName (typeof (JavaPeerScannerTests).Assembly.Location)!;
var fixtureAssembly = Path.Combine (testAssemblyDir, "TestFixtures.dll");
Assert.True (File.Exists (fixtureAssembly),
$"TestFixtures.dll not found at {fixtureAssembly}. Ensure the TestFixtures project builds.");
return fixtureAssembly;
}
}

List<JavaPeerInfo> ScanFixtures ()
{
using var scanner = new JavaPeerScanner ();
return scanner.Scan (new [] { TestFixtureAssemblyPath });
}

JavaPeerInfo FindByJavaName (List<JavaPeerInfo> peers, string javaName)
{
var peer = peers.FirstOrDefault (p => p.JavaName == javaName);
Assert.NotNull (peer);
return peer;
}

JavaPeerInfo FindByManagedName (List<JavaPeerInfo> peers, string managedName)
{
var peer = peers.FirstOrDefault (p => p.ManagedTypeName == managedName);
Assert.NotNull (peer);
return peer;
}

[Fact]
public void Scan_FindsAllJavaPeerTypes ()
{
var peers = ScanFixtures ();
Assert.NotEmpty (peers);
Assert.Contains (peers, p => p.JavaName == "java/lang/Object");
Assert.Contains (peers, p => p.JavaName == "android/app/Activity");
Assert.Contains (peers, p => p.JavaName == "my/app/MainActivity");
Assert.Contains (peers, p => p.JavaName == "java/lang/Throwable");
Assert.Contains (peers, p => p.JavaName == "java/lang/Exception");
}

[Fact]
public void Scan_McwTypes_HaveDoNotGenerateAcw ()
{
var peers = ScanFixtures ();
Assert.True (FindByJavaName (peers, "android/app/Activity").DoNotGenerateAcw);
Assert.True (FindByJavaName (peers, "android/widget/Button").DoNotGenerateAcw);
Assert.True (FindByJavaName (peers, "android/content/Context").DoNotGenerateAcw);
}

[Fact]
public void Scan_UserTypes_DoNotGenerateAcwIsFalse ()
{
var peers = ScanFixtures ();
Assert.False (FindByJavaName (peers, "my/app/MainActivity").DoNotGenerateAcw);
}

[Theory]
[InlineData ("my/app/MainActivity")]
[InlineData ("my/app/MyService")]
[InlineData ("my/app/MyReceiver")]
[InlineData ("my/app/MyProvider")]
[InlineData ("my/app/MyApplication")]
[InlineData ("my/app/MyInstrumentation")]
public void Scan_ComponentTypes_AreUnconditional (string javaName)
{
var peers = ScanFixtures ();
Assert.True (FindByJavaName (peers, javaName).IsUnconditional);
}

[Fact]
public void Scan_TypeWithoutComponentAttribute_IsTrimmable ()
{
var peers = ScanFixtures ();
Assert.False (FindByJavaName (peers, "my/app/MyHelper").IsUnconditional);
}

[Fact]
public void Scan_McwBinding_IsTrimmable ()
{
var peers = ScanFixtures ();
Assert.False (FindByJavaName (peers, "android/app/Activity").IsUnconditional);
}

[Fact]
public void Scan_InterfaceType_IsMarkedAsInterface ()
{
var peers = ScanFixtures ();
Assert.True (FindByManagedName (peers, "Android.Views.IOnClickListener").IsInterface);
}

[Fact]
public void Scan_InvokerSharesJavaNameWithInterface ()
{
var peers = ScanFixtures ();
var clickListenerPeers = peers.Where (p => p.JavaName == "android/view/View$OnClickListener").ToList ();
Assert.Equal (2, clickListenerPeers.Count);
Assert.Contains (clickListenerPeers, p => p.IsInterface);
Assert.Contains (clickListenerPeers, p => p.DoNotGenerateAcw);
}

[Fact]
public void Scan_GenericType_IsGenericDefinition ()
{
var peers = ScanFixtures ();
var generic = FindByJavaName (peers, "my/app/GenericHolder");
Assert.True (generic.IsGenericDefinition);
Assert.Equal ("MyApp.Generic.GenericHolder`1", generic.ManagedTypeName);
}

[Fact]
public void Scan_AbstractType_IsMarkedAbstract ()
{
var peers = ScanFixtures ();
Assert.True (FindByJavaName (peers, "my/app/AbstractBase").IsAbstract);
}

[Fact]
public void Scan_AllTypes_HaveAssemblyName ()
{
var peers = ScanFixtures ();
Assert.All (peers, peer =>
Assert.False (string.IsNullOrEmpty (peer.AssemblyName),
$"Type {peer.ManagedTypeName} should have assembly name"));
}
}
