using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[ExportCodeFixProvider (LanguageNames.CSharp, Name = nameof (CustomApplicationCodeFixProvider)), Shared]
public class CustomApplicationCodeFixProvider : CodeFixProvider
{
    private const string title = "Fix Activation Constructor";
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create (CustomApplicationAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider ()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync (CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync (context.CancellationToken).ConfigureAwait (false);
        var diagnostic = context.Diagnostics.First ();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var classDeclaration = root.FindToken (diagnosticSpan.Start).Parent.AncestorsAndSelf ()
                .OfType<ClassDeclarationSyntax> ().First ();
        context.RegisterCodeFix (CodeAction.Create (title, c =>
            InjectConstructorAsync (context.Document, classDeclaration, c), equivalenceKey: title), diagnostic);
    }

    private async Task<Document> InjectConstructorAsync (Document document, ClassDeclarationSyntax classDeclaration, CancellationToken cancellationToken)
    {
        var constructor = CreateConstructorWithParameters (classDeclaration.Identifier);
        var newClassDeclaration = classDeclaration.AddMembers (constructor);
        var root = await document.GetSyntaxRootAsync (cancellationToken);
        var newRoot = root.ReplaceNode (classDeclaration, newClassDeclaration);
        return document.WithSyntaxRoot (newRoot);
    }

    private ConstructorDeclarationSyntax CreateConstructorWithParameters (SyntaxToken identifier)
    {
        var parameters = SyntaxFactory.ParameterList (SyntaxFactory.SeparatedList (new [] {
                SyntaxFactory.Parameter (SyntaxFactory.Identifier ("javaReference"))
                    .WithType (SyntaxFactory.ParseTypeName ("IntPtr")),
                SyntaxFactory.Parameter (SyntaxFactory.Identifier ("transfer"))
                    .WithType (SyntaxFactory.ParseTypeName ("JniHandleOwnership"))
            }));
        var baseArguments = SyntaxFactory.ArgumentList (SyntaxFactory.SeparatedList (new [] {
                SyntaxFactory.Argument (SyntaxFactory.IdentifierName ("javaReference")),
                SyntaxFactory.Argument (SyntaxFactory.IdentifierName ("transfer"))
            }));
        var constructorInitializer = SyntaxFactory.ConstructorInitializer (SyntaxKind.BaseConstructorInitializer, baseArguments);
        var body = SyntaxFactory.Block ();
        var constructor = SyntaxFactory.ConstructorDeclaration (identifier)
            .WithModifiers (SyntaxFactory.TokenList (SyntaxFactory.Token (SyntaxKind.PublicKeyword)))
            .WithParameterList (parameters)
            .WithInitializer (constructorInitializer)
            .WithBody (body);
        return constructor;
    }
}