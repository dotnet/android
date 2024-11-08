using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

public static partial class CSharpAnalyzerVerifier<TAnalyzer>
	where TAnalyzer : DiagnosticAnalyzer, new()
{
	public class Test : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
	{
		List<DiagnosticAnalyzer> analyzers = new List<DiagnosticAnalyzer>();

		public List<DiagnosticAnalyzer> Analyzers => analyzers;
		public Test()
		{
			SolutionTransforms.Add((solution, projectId) =>
			{
				var project = solution.GetProject(projectId);
				var compilationOptions = project.CompilationOptions;
				compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
					compilationOptions.SpecificDiagnosticOptions.SetItems(CSharpVerifierHelper.NullableWarnings));
				solution = solution.WithProjectCompilationOptions(projectId, compilationOptions);
				return solution;
			});

			var a = new AnalyzerFileReference(Path.GetFullPath("Microsoft.CodeAnalysis.CSharp.Features.dll"), assemblyLoader: AssemblyLoader.Instance);
			foreach (var a1 in a.GetAnalyzers(LanguageNames.CSharp))
			{
				if (a1.SupportedDiagnostics.Any(x => x.Id == "IDE0002"))
					analyzers.Add(a1);
			}
		}

		protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers()
		{
			return Analyzers;
		}
	}
}
