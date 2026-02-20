using System.Linq;
using Xamarin.Android.Tasks;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

public partial class ScannerComparisonTests
{
[Fact]
public void ExactTypeMap_MonoAndroid ()
{
var (legacy, _) = ScannerRunner.RunLegacy (MonoAndroidAssemblyPath);
var (newEntries, _) = ScannerRunner.RunNew (AllAssemblyPaths);
AssertTypeMapMatch (legacy, newEntries);
}

[Fact]
public void ExactMarshalMethods_MonoAndroid ()
{
var (_, legacyMethods) = ScannerRunner.RunLegacy (MonoAndroidAssemblyPath);
var (_, newMethods) = ScannerRunner.RunNew (AllAssemblyPaths);
var result = MarshalMethodDiffHelper.CompareMarshalMethods (legacyMethods, newMethods);

AssertNoDiffs ("MANAGED TYPES MISSING from new scanner", result.MissingTypes);
AssertNoDiffs ("MANAGED TYPES EXTRA in new scanner", result.ExtraTypes);
AssertNoDiffs ("METHODS MISSING from new scanner", result.MissingMethods);
AssertNoDiffs ("METHODS EXTRA in new scanner", result.ExtraMethods);
AssertNoDiffs ("CONNECTOR MISMATCHES", result.ConnectorMismatches);
}

[Fact]
public void ScannerDiagnostics_MonoAndroid ()
{
using var scanner = new JavaPeerScanner ();
var peers = scanner.Scan (new [] { MonoAndroidAssemblyPath });

var interfaces = peers.Count (p => p.IsInterface);
var totalMethods = peers.Sum (p => p.MarshalMethods.Count);
Assert.True (peers.Count > 3000, $"Expected >3000 types, got {peers.Count}");
Assert.True (interfaces > 500, $"Expected >500 interfaces, got {interfaces}");
Assert.True (totalMethods > 10000, $"Expected >10000 marshal methods, got {totalMethods}");
}

[Fact]
public void ExactBaseJavaNames_MonoAndroid ()
{
var (legacyData, _) = TypeDataBuilder.BuildLegacy (MonoAndroidAssemblyPath);
var newData = TypeDataBuilder.BuildNew (AllAssemblyPaths);
var mismatches = ComparisonDiffHelper.CompareBaseJavaNames (legacyData, newData);

AssertNoDiffs ("BASE JAVA NAME MISMATCHES", mismatches);
}

[Fact]
public void ExactImplementedInterfaces_MonoAndroid ()
{
var (legacyData, _) = TypeDataBuilder.BuildLegacy (MonoAndroidAssemblyPath);
var newData = TypeDataBuilder.BuildNew (AllAssemblyPaths);
var (missingInterfaces, extraInterfaces) = ComparisonDiffHelper.CompareImplementedInterfaces (legacyData, newData);

AssertNoDiffs ("INTERFACES MISSING from new scanner", missingInterfaces);
AssertNoDiffs ("INTERFACES EXTRA in new scanner", extraInterfaces);
}

[Fact]
public void ExactActivationCtors_MonoAndroid ()
{
var (legacyData, _) = TypeDataBuilder.BuildLegacy (MonoAndroidAssemblyPath);
var newData = TypeDataBuilder.BuildNew (AllAssemblyPaths);
var (presenceMismatches, declaringTypeMismatches, styleMismatches) = ComparisonDiffHelper.CompareActivationCtors (legacyData, newData);

AssertNoDiffs ("ACTIVATION CTOR PRESENCE MISMATCHES", presenceMismatches);
AssertNoDiffs ("ACTIVATION CTOR DECLARING TYPE MISMATCHES", declaringTypeMismatches);
AssertNoDiffs ("ACTIVATION CTOR STYLE MISMATCHES", styleMismatches);
}

[Fact]
public void ExactJavaConstructors_MonoAndroid ()
{
var (legacyData, _) = TypeDataBuilder.BuildLegacy (MonoAndroidAssemblyPath);
var newData = TypeDataBuilder.BuildNew (AllAssemblyPaths);
var (missingCtors, extraCtors) = ComparisonDiffHelper.CompareJavaConstructors (legacyData, newData);

AssertNoDiffs ("JAVA CONSTRUCTORS MISSING from new scanner", missingCtors);
AssertNoDiffs ("JAVA CONSTRUCTORS EXTRA in new scanner", extraCtors);
}

[Fact]
public void ExactTypeFlags_MonoAndroid ()
{
var (legacyData, _) = TypeDataBuilder.BuildLegacy (MonoAndroidAssemblyPath);
var newData = TypeDataBuilder.BuildNew (AllAssemblyPaths);
var (interfaceMismatches, abstractMismatches, genericMismatches, acwMismatches) = ComparisonDiffHelper.CompareTypeFlags (legacyData, newData);

AssertNoDiffs ("IsInterface MISMATCHES", interfaceMismatches);
AssertNoDiffs ("IsAbstract MISMATCHES", abstractMismatches);
AssertNoDiffs ("IsGenericDefinition MISMATCHES", genericMismatches);
AssertNoDiffs ("DoNotGenerateAcw MISMATCHES", acwMismatches);
}

[Fact]
public void ExactTypeMap_UserTypesFixture ()
{
var paths = AllUserTypesAssemblyPaths;
Assert.NotNull (paths);

var fixturePath = paths! [0];
var (legacy, _) = ScannerRunner.RunLegacy (fixturePath);
var (newEntries, _) = ScannerRunner.RunNew (paths);
var legacyNormalized = legacy.Select (e => e with { JavaName = NormalizeCrc64 (e.JavaName) }).ToList ();
var newNormalized = newEntries.Select (e => e with { JavaName = NormalizeCrc64 (e.JavaName) }).ToList ();

AssertTypeMapMatch (legacyNormalized, newNormalized);
}

[Fact]
public void ExactMarshalMethods_UserTypesFixture ()
{
var paths = AllUserTypesAssemblyPaths;
Assert.NotNull (paths);

var fixturePath = paths! [0];
var (_, legacyMethods) = ScannerRunner.RunLegacy (fixturePath);
var (_, newMethods) = ScannerRunner.RunNew (paths);

var legacyNormalized = legacyMethods
.ToDictionary (kvp => NormalizeCrc64 (kvp.Key), kvp => kvp.Value);
var newNormalized = newMethods
.ToDictionary (kvp => NormalizeCrc64 (kvp.Key), kvp => kvp.Value);

var result = MarshalMethodDiffHelper.CompareUserTypeMarshalMethods (legacyNormalized, newNormalized);
AssertNoDiffs ("MISSING from new scanner", result.Missing);
AssertNoDiffs ("METHOD MISMATCHES", result.MethodMismatches);
}
}
