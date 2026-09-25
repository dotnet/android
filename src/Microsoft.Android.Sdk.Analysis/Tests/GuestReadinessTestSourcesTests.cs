using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Xamarin.ProjectTools;

[TestFixture]
[NonParallelizable]
public class GuestReadinessTestSourcesTests
{
	string savedOptIn;
	string savedConfig;
	string directory;

	[SetUp]
	public void SetUp ()
	{
		savedOptIn = Environment.GetEnvironmentVariable ("ANDROID_GUEST_READINESS_TEST_ACQUISITION");
		savedConfig = Environment.GetEnvironmentVariable ("RESTORECONFIGFILE");
		directory = Path.Combine (TestContext.CurrentContext.WorkDirectory, "guest readiness sources", Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (directory);
	}

	[TearDown]
	public void TearDown ()
	{
		Environment.SetEnvironmentVariable ("ANDROID_GUEST_READINESS_TEST_ACQUISITION", savedOptIn);
		Environment.SetEnvironmentVariable ("RESTORECONFIGFILE", savedConfig);
	}

	[Test]
	public void ExplicitWindowsGate ()
	{
		Assert.IsTrue (GuestReadinessTestSources.IsEnabled (PlatformID.Win32NT, "1"));
		Assert.IsFalse (GuestReadinessTestSources.IsEnabled (PlatformID.Win32NT, null));
		Assert.IsFalse (GuestReadinessTestSources.IsEnabled (PlatformID.Win32NT, ""));
		foreach (var invalid in new [] { "0", "true", " 1", "1 ", "2" })
			Assert.Throws<InvalidOperationException> (() => GuestReadinessTestSources.IsEnabled (PlatformID.Win32NT, invalid));
		foreach (var host in new [] { PlatformID.Unix, PlatformID.MacOSX }) {
			Assert.IsFalse (GuestReadinessTestSources.IsEnabled (host, "1"));
			Assert.IsFalse (GuestReadinessTestSources.IsEnabled (host, "invalid"));
		}
	}

	[Test]
	public void ExactMavenOriginAndSuffix ()
	{
		const string source = "https://repo1.maven.org/maven2/com/balysv/material-menu/1.1.0/material-menu-1.1.0.aar";
		const string mirror = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public-maven/maven/v1/com/balysv/material-menu/1.1.0/material-menu-1.1.0.aar";
		Environment.SetEnvironmentVariable ("ANDROID_GUEST_READINESS_TEST_ACQUISITION", null);
		Assert.AreEqual (source, GuestReadinessTestSources.GetDownloadUrl (source));
		Environment.SetEnvironmentVariable ("ANDROID_GUEST_READINESS_TEST_ACQUISITION", "1");
		Assert.AreEqual (Environment.OSVersion.Platform == PlatformID.Win32NT ? mirror : source, GuestReadinessTestSources.GetDownloadUrl (source));
		foreach (var other in new [] {
			"https://repo1.maven.org.evil/maven2/a.jar", "http://repo1.maven.org/maven2/a.jar",
			"https://repo1.maven.org:444/maven2/a.jar", "https://user@repo1.maven.org/maven2/a.jar",
			"https://repo1.maven.org/maven2/a.jar?query=1", "https://repo1.maven.org/maven2/a.jar#fragment",
			"https://repo1.maven.org/maven2/../a.jar", "https://repo1.maven.org/maven2/%2e%2e/a.jar",
			"https://repo1.maven.org/maven2//a.jar", "https://repo1.maven.org/maven2/", mirror,
		})
			Assert.AreEqual (other, GuestReadinessTestSources.GetDownloadUrl (other));
	}

	[Test]
	public void ConfigAndAllVerifierConstructors ()
	{
		var config = Path.Combine (directory, "NuGet.config");
		File.Copy (Path.Combine (TestContext.CurrentContext.TestDirectory, "guest-readiness-root-NuGet.config"), config);
		Environment.SetEnvironmentVariable ("RESTORECONFIGFILE", config);
		Environment.SetEnvironmentVariable ("ANDROID_GUEST_READINESS_TEST_ACQUISITION", null);
		var original = ReferenceAssemblies.Default;
		Assert.AreSame (original, CSharpVerifierHelper.ConfigureReferenceAssemblies (original));
		Environment.SetEnvironmentVariable ("ANDROID_GUEST_READINESS_TEST_ACQUISITION", "1");
		var configured = CSharpVerifierHelper.ConfigureReferenceAssemblies (original);
		Assert.AreEqual (original.TargetFramework, configured.TargetFramework);
		Assert.AreEqual (original.ReferenceAssemblyPackage, configured.ReferenceAssemblyPackage);
		CollectionAssert.AreEqual (original.Packages, configured.Packages);
		var expected = Environment.OSVersion.Platform == PlatformID.Win32NT ? config : original.NuGetConfigFilePath;
		Assert.AreEqual (expected, configured.NuGetConfigFilePath);
		var references = new [] {
			new CSharpAnalyzerVerifier<EmptyAnalyzer>.Test ().ReferenceAssemblies,
			new CSharpCodeFixVerifier<CustomApplicationAnalyzer, CustomApplicationCodeFixProvider>.Test ().ReferenceAssemblies,
			new CSharpCodeRefactoringVerifier<EmptyRefactoring>.Test ().ReferenceAssemblies,
			new CSharpSuppressorVerifier<EmptyAnalyzer, EmptySuppressor>.Test ().ReferenceAssemblies,
		};
		foreach (var value in references)
			Assert.AreEqual (expected, value.NuGetConfigFilePath);
		foreach (var invalid in new [] { null, "", "relative.config", Path.Combine (directory, "missing.config") }) {
			Environment.SetEnvironmentVariable ("RESTORECONFIGFILE", invalid);
			Assert.Throws<InvalidOperationException> (() => GuestReadinessTestSources.GetNuGetConfig ());
		}
	}

	[Test]
	public void MalformedConfigHasNoFallback ()
	{
		var config = Path.Combine (directory, "malformed.config");
		File.WriteAllText (config, "<configuration>");
		var references = ReferenceAssemblies.Default.WithNuGetConfigFilePath (config);
		var failure = Assert.CatchAsync<Exception> (
			async () => await references.ResolveAsync (LanguageNames.CSharp, default));
		Assert.AreEqual ("NuGet.Configuration.NuGetConfigurationException", failure.GetType ().FullName);
	}

	public class EmptyAnalyzer : DiagnosticAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray<DiagnosticDescriptor>.Empty;
		public override void Initialize (AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis (GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution ();
		}
	}

	public class EmptyRefactoring : CodeRefactoringProvider
	{
		public override Task ComputeRefactoringsAsync (CodeRefactoringContext context) => Task.CompletedTask;
	}

	public class EmptySuppressor : DiagnosticSuppressor
	{
		public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => ImmutableArray<SuppressionDescriptor>.Empty;
		public override void ReportSuppressions (SuppressionAnalysisContext context) { }
	}
}
