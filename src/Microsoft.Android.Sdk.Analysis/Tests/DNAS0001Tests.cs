using System;
using NUnit.Framework;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using System.IO;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using System.Linq;
using VerifyCSAnalyser = CSharpAnalyzerVerifier <DNAS0001Tests.IDE0002AnalyserWrapper>;
using VerifyCSSuppressor = CSharpSuppressorVerifier <DNAS0001Tests.IDE0002AnalyserWrapper, ResourceDesignerDiagnosticSuppressor>;
using System.Security.Cryptography;

[TestFixture]
public class DNAS0001Tests
{
	static string brokenCode = @"
using System;
using System.Diagnostics;

namespace ConsoleApplication1
{
    public static class Program
    {
        public static void Main (string[] args)
        {
            var foo = Resource.Id.Foo;
        }
    }
    public class Resource : _Microsoft.Android.Resource.Designer.Resource
    {   
    }
}
namespace _Microsoft.Android.Resource.Designer {
    public class Resource {
        public class Id
        {
            public static int Foo = 0;
        }
    }
}
";
	[Test]
	public async Task IDE0001IsNotSuppressed ()
	{
		var expected = VerifyCSAnalyser.Diagnostic (new DiagnosticDescriptor ("IDE0002", "", "Name can be simplified", "", DiagnosticSeverity.Hidden, isEnabledByDefault: true)).WithSpan (11, 23, 11, 31);
		await VerifyCSAnalyser.VerifyAnalyzerAsync (brokenCode, expected);
	}

	[Test]
	public async Task IDE0001IsSuppressed ()
	{
		var expected = VerifyCSSuppressor.Diagnostic (new DiagnosticDescriptor ("IDE0002", "", "Name can be simplified", "", DiagnosticSeverity.Hidden, isEnabledByDefault: true)).WithSpan (11, 23, 11, 31).WithIsSuppressed (true);
		await VerifyCSSuppressor.VerifySuppressorAsync (brokenCode, expected);
	}

	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	// disable warnings as we are not actually creating a DiagnosticAnalyzer
#pragma warning disable RS1036 // Specify analyzer banned API enforcement setting
#pragma warning disable RS1025 // Configure generated code analysis
#pragma warning disable RS1026 // Enable concurrent execution
	public class IDE0002AnalyserWrapper : DiagnosticAnalyzer
	{
		DiagnosticAnalyzer analyzer;
		ImmutableArray<DiagnosticDescriptor> diagnostics;
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => diagnostics;

		public IDE0002AnalyserWrapper ()
		{
			var a = new AnalyzerFileReference (Path.GetFullPath ("Microsoft.CodeAnalysis.CSharp.Features.dll"), assemblyLoader: AssemblyLoader.Instance);
			foreach (var a1 in a.GetAnalyzers (LanguageNames.CSharp))
			{
				if (a1.SupportedDiagnostics.Any (x => x.Id == "IDE0002"))
				{
					analyzer = a1;
					diagnostics = ImmutableArray.Create (analyzer.SupportedDiagnostics.First(x => x.Id == "IDE0002"));
					break;
				}
			}
		}

		public override void Initialize (AnalysisContext context)
		{
			analyzer.Initialize (context);
		}
	}
#pragma warning restore RS1026
#pragma warning restore RS1025
#pragma warning restore RS1036 // Specify analyzer banned API enforcement settingX
}