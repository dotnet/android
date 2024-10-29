using System.Linq;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

[DiagnosticAnalyzer (LanguageNames.CSharp)]
public class ResourceDesignerDiagnosticSuppressor : DiagnosticSuppressor
{
	private const string DesignerNamespace = "_Microsoft.Android.Resource.Designer";
	private static readonly SuppressionDescriptor Rule = new (
		"DNAS0001",
		"IDE0002",
		"The Resource Designer class should not be simplified."
	);

	public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions
		=> ImmutableArray.Create (Rule);

	public override void ReportSuppressions (SuppressionAnalysisContext context)
	{
		foreach (var diagnostic in context.ReportedDiagnostics) {
			if (diagnostic.Id != Rule.SuppressedDiagnosticId)
				continue;
			Location location = diagnostic.Location;
			SyntaxTree syntaxTree = location.SourceTree;
			if (syntaxTree is null)
				continue;

			SyntaxNode root = syntaxTree.GetRoot (context.CancellationToken);
			SyntaxNode syntaxNode = root.FindNode (location.SourceSpan)
				.DescendantNodesAndSelf ()
				.FirstOrDefault ();

			if (syntaxNode is null)
				continue;

			SemanticModel model = context.GetSemanticModel (syntaxTree);
			ISymbol typeSymbol = model.GetSymbolInfo (syntaxNode).Symbol;
			if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
				continue;

			if (IsResourceDesignerDerivedType (namedTypeSymbol)) {
				Suppression suppression = Suppression.Create (Rule, diagnostic);
				context.ReportSuppression (suppression);
			}
		}
	}

	private static bool IsResourceDesignerDerivedType (INamedTypeSymbol typeSymbol)
	{
		return Utilities.IsDerivedFrom (typeSymbol, DesignerNamespace);
	}
}