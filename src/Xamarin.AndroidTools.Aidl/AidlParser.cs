using System;
using System.Linq;
using Irony.Parsing;
using Irony.Ast;

namespace Xamarin.AndroidTools.Aidl
{
	[Language ("AIDL", "1.0", "AIDL pseudo grammar")]
	public partial class AidlGrammar : Grammar
	{
		NonTerminal DefaultNonTerminal (string label)
		{
			var nt = new NonTerminal (label);
			nt.AstConfig.NodeCreator = delegate { throw new NotImplementedException (label); };
			return nt;
		}

		KeyTerm Keyword (string label)
		{
			var ret = ToTerm (label);
			ret.AstConfig.NodeCreator = delegate (AstContext ctx, ParseTreeNode node) {
				node.AstNode = node.Token.ValueString;
			};
			return ret;
		}
		
		AstNodeCreator CreateArrayCreator<T> ()
		{
			return delegate (AstContext ctx, ParseTreeNode node) {
				ProcessChildren (ctx, node);
				node.AstNode = (from n in node.ChildNodes select (T) n.AstNode).ToArray ();
			};
		}
		
		void SelectSingleChild (AstContext ctx, ParseTreeNode node)
		{
			ProcessChildren (ctx, node);
			if (node.ChildNodes.Count == 1)
				node.AstNode = node.ChildNodes.First ().AstNode;
		}
		
		void ProcessChildren (AstContext ctx, ParseTreeNode node)
		{
			foreach (var cn in node.ChildNodes) {
				if (cn.Term.AstConfig.NodeCreator != null)
					cn.Term.AstConfig.NodeCreator (ctx, cn);
			}
		}

		void DoNothing (AstContext ctx, ParseTreeNode node)
		{
			// do nothing.
		}

		public AidlGrammar ()
		{
			CommentTerminal single_line_comment = new CommentTerminal ("SingleLineComment", "//", "\r", "\n");
			CommentTerminal delimited_comment = new CommentTerminal ("DelimitedComment", "/*", "*/");

			NonGrammarTerminals.Add (single_line_comment);
			NonGrammarTerminals.Add (delimited_comment);

			IdentifierTerminal identifier = TerminalFactory.CreateCSharpIdentifier ("Identifier");
			identifier.AstConfig.NodeCreator = delegate (AstContext ctx, ParseTreeNode node) {
				node.AstNode = node.Token.ValueString;
			};

			KeyTerm keyword_package = Keyword ("package");
			KeyTerm keyword_import = Keyword ("import");
			KeyTerm keyword_parcelable = Keyword ("parcelable");
			KeyTerm keyword_interface = Keyword ("interface");
			KeyTerm keyword_oneway = Keyword ("oneway");
			KeyTerm keyword_in = Keyword ("in");
			KeyTerm keyword_out = Keyword ("out");
			KeyTerm keyword_inout = Keyword ("inout");

			var compile_unit = DefaultNonTerminal ("compile_unit");
			var opt_package_decl = DefaultNonTerminal ("opt_package_declaration");
			var package_decl = DefaultNonTerminal ("package_declaration");
			var imports = DefaultNonTerminal ("imports");
			var import = DefaultNonTerminal ("import");
			var type_decls = DefaultNonTerminal ("type_decls");
			var type_decl = DefaultNonTerminal ("type_decl");
			var parcelable_decl = DefaultNonTerminal ("parcelable_decl");
			var interface_decl = DefaultNonTerminal ("interface_declaration");
			var interface_external_decl = DefaultNonTerminal ("interface_external_declaration");
			var interface_local_decl = DefaultNonTerminal ("interface_local_declaration");
			var interface_body = DefaultNonTerminal ("interface_body");
			var interface_members = DefaultNonTerminal ("interface_members");
			var method_decl = DefaultNonTerminal ("method_decl");
			var opt_interface_modifier = DefaultNonTerminal ("opt_interface_modifier");
			var opt_method_modifier = DefaultNonTerminal ("opt_method_modifier");
			var argument_decls = DefaultNonTerminal ("argument_declarations");
			var argument_decl = DefaultNonTerminal ("argument_declaration");
			var type_name = DefaultNonTerminal ("type_name");
			var dotted_identifier = DefaultNonTerminal ("dotted_identifier");
			var array_type = DefaultNonTerminal ("array_type");
			var generic_type = DefaultNonTerminal ("generic_type");
			var generic_arguments = DefaultNonTerminal ("generic_arguments");
			var opt_parameter_modifier = DefaultNonTerminal ("opt_parameter_modifier");

// <construction_rules>

			compile_unit.Rule = opt_package_decl + imports + type_decls;
			imports.Rule = MakeStarRule (imports, null, import);
			compile_unit.AstConfig.NodeCreator = delegate (AstContext ctx, ParseTreeNode node) {
				ProcessChildren (ctx, node);
				node.AstNode = new CompilationUnit ((TypeName) node.ChildNodes [0].AstNode,
					(TypeName []) node.ChildNodes [1].AstNode,
					(ITypeDeclaration []) node.ChildNodes [2].AstNode);
			};

			opt_package_decl.Rule = package_decl | Empty;
			opt_package_decl.AstConfig.NodeCreator = SelectSingleChild;
			package_decl.Rule = keyword_package + type_name + ";";
			package_decl.AstConfig.NodeCreator = delegate (AstContext ctx, ParseTreeNode node) {
				ProcessChildren (ctx, node);
				node.AstNode = node.ChildNodes [1].AstNode;
			};
			
			imports.Rule = MakeStarRule (imports, null, import);
			imports.AstConfig.NodeCreator = CreateArrayCreator<TypeName> ();

			import.Rule = keyword_import + type_name + ";";
			import.AstConfig.NodeCreator = delegate (AstContext ctx, ParseTreeNode node) {
				ProcessChildren (ctx, node);
				node.AstNode = node.ChildNodes [1].AstNode;
			};
			
			type_decls.Rule = MakePlusRule (type_decls, type_decl);
			type_decls.AstConfig.NodeCreator = CreateArrayCreator<ITypeDeclaration> ();;
			type_decl.Rule = parcelable_decl | interface_decl;
			type_decl.AstConfig.NodeCreator = SelectSingleChild;
			
			parcelable_decl.Rule = keyword_parcelable + dotted_identifier + ";";
			parcelable_decl.AstConfig.NodeCreator = delegate (AstContext ctx, ParseTreeNode node) {
				ProcessChildren (ctx, node);
				node.AstNode = new Parcelable ((TypeName) node.ChildNodes [1].AstNode);
			};

			interface_decl.Rule = interface_local_decl | interface_external_decl;
			interface_decl.AstConfig.NodeCreator = SelectSingleChild;
			interface_external_decl.Rule = opt_interface_modifier + keyword_interface + dotted_identifier + ";";
			interface_external_decl.AstConfig.NodeCreator = DoNothing;
			interface_local_decl.Rule = opt_interface_modifier + keyword_interface + identifier + interface_body;
			interface_body.Rule = "{" + interface_members + "}";
			interface_local_decl.AstConfig.NodeCreator = delegate (AstContext ctx, ParseTreeNode node) {
				ProcessChildren (ctx, node);
				node.AstNode = new Interface ((string) node.ChildNodes [0].AstNode,
					(string) node.ChildNodes [2].AstNode,
					(Method []) node.ChildNodes [3].AstNode);
			};
			interface_body.AstConfig.NodeCreator = delegate (AstContext ctx, ParseTreeNode node) {
				ProcessChildren (ctx, node);
				node.AstNode = (Method []) node.ChildNodes [1].AstNode;
			};
			opt_interface_modifier.Rule = Empty | keyword_oneway;
			opt_interface_modifier.AstConfig.NodeCreator = SelectSingleChild;

			interface_members.Rule = MakeStarRule (interface_members, null, method_decl);
			interface_members.AstConfig.NodeCreator = CreateArrayCreator<Method> ();

			method_decl.Rule = opt_method_modifier + type_name + identifier + "(" + argument_decls + ")" + ";";
			method_decl.AstConfig.NodeCreator = delegate (AstContext ctx, ParseTreeNode node) {
				ProcessChildren (ctx, node);
				node.AstNode = new Method (
					(string) node.ChildNodes [0].AstNode,
					(TypeName) node.ChildNodes [1].AstNode,
					(string) node.ChildNodes [2].AstNode,
					(Argument []) node.ChildNodes [4].AstNode);
			};

			opt_method_modifier.Rule = Empty | keyword_oneway;
			opt_method_modifier.AstConfig.NodeCreator = SelectSingleChild;
			
			argument_decls.Rule = MakeStarRule (argument_decls, ToTerm (","), argument_decl);
			argument_decls.AstConfig.NodeCreator = CreateArrayCreator<Argument> ();
			
			argument_decl.Rule = opt_parameter_modifier + type_name + identifier;
			argument_decl.AstConfig.NodeCreator = delegate (AstContext ctx, ParseTreeNode node) {
				ProcessChildren (ctx, node);
				node.AstNode = new Argument ((string) node.ChildNodes [0].AstNode, (TypeName) node.ChildNodes [1].AstNode, (string) node.ChildNodes [2].AstNode);
			};
			
			opt_parameter_modifier.Rule = Empty | keyword_in | keyword_out | keyword_inout;
			opt_parameter_modifier.AstConfig.NodeCreator = SelectSingleChild;
			
			type_name.Rule = dotted_identifier | array_type | generic_type;
			type_name.AstConfig.NodeCreator = SelectSingleChild;
			
			array_type.Rule = type_name + "[" + "]";
			array_type.AstConfig.NodeCreator = delegate (AstContext ctx, ParseTreeNode node) {
				ProcessChildren (ctx, node);
				var tn = (TypeName) node.ChildNodes [0].AstNode;
				tn.ArrayDimension++;
				node.AstNode = tn;
			};
			
			generic_type.Rule = dotted_identifier + "<" + generic_arguments + ">";
			generic_type.AstConfig.NodeCreator = delegate (AstContext ctx, ParseTreeNode node) {
				ProcessChildren (ctx, node);
				node.AstNode = new TypeName (((TypeName) node.ChildNodes [0].AstNode).Identifiers, (TypeName []) node.ChildNodes [2].AstNode);
			};
			
			generic_arguments.Rule = MakePlusRule (generic_arguments, ToTerm (","), type_name);
			generic_arguments.AstConfig.NodeCreator = CreateArrayCreator<TypeName> ();
			
			dotted_identifier.Rule = MakePlusRule (dotted_identifier, ToTerm ("."), identifier);
			dotted_identifier.AstConfig.NodeCreator = delegate (AstContext ctx, ParseTreeNode node) {
				ProcessChildren (ctx, node);
				var last = node.ChildNodes.Last ().Term.Name;
				last = Char.ToUpper (last [0]) + last.Substring (1);
				node.AstNode = new TypeName ((from n in node.ChildNodes select n.Token.ValueString).ToArray ());
			};
			
			this.Root = compile_unit;
		}
	}
}

