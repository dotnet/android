using System;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tasks
{
	partial class GenerateLayoutBindings
	{
		sealed class CSharpBindingGenerator : BindingGenerator
		{
			const string BindingPartialClassBackingFieldName = "__layout_binding";

			protected override string LineCommentString => "//";
			protected override string DocCommentString => "///";
			public override string LanguageName => "C#";
			public override bool CaseSensitive => true;

			protected override void BeginPartialClassFile (State state, string classNamespace, string className)
			{
				EnsureArgument (state, nameof (state));

				WriteFilePreamble (state, classNamespace);
				WriteClassOpen (state, $"partial class {className}");
				WriteLineIndent (state, $"{state.BindingClassName} {BindingPartialClassBackingFieldName};");
				state.WriteLine ();
			}

			public override void EndPartialClassFile (State state)
			{
				EndBindingFile (state); // currently they're identical
			}

			public override void WritePartialClassProperty (State state, LayoutWidget widget)
			{
				WritePropertyDeclaration (state, widget, true);
				state.WriteLine ($"{BindingPartialClassBackingFieldName}?.{GetValidPropertyName (state, widget)};");
			}

			protected override void WritePartialClassSetContentView_View (State state)
			{
				WriteMethodStart (state, "public override void", "SetContentView", "global::Android.Views.View view");
				WriteCommonSetContentViewBody (state, "view", "view");
			}

			protected override void WritePartialClassSetContentView_View_LayoutParams (State state)
			{
				WriteMethodStart (state, "public override void", "SetContentView", "global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params");
				WriteCommonSetContentViewBody (state, "view, @params", "view");
			}

			protected override void WritePartialClassSetContentView_Int (State state)
			{
				WriteMethodStart (state, "public override void", "SetContentView", "int layoutResID");
				WriteCommonSetContentViewBody (state, "layoutResID", "this");
			}

			void WriteCommonSetContentViewBody (State state, string setContentViewParams, string bindingConstructorParams)
			{
				WriteBindingInstantiation (state, bindingConstructorParams);
				WriteLineIndent (state, "bool callBase = true;");
				WriteLineIndent (state, $"OnSetContentView ({setContentViewParams}, ref callBase);");
				WriteLineIndent (state, "if (callBase) {");
				state.IncreaseIndent ();
				WriteCallBase (state, "SetContentView", setContentViewParams);
				state.DecreaseIndent ();
				WriteLineIndent (state, "}");
				WriteMethodEnd (state);
			}

			protected override void WritePartialClassOnSetContentViewPartial_View (State state)
			{
				WriteMethodStart (state, "partial void", "OnSetContentView", "global::Android.Views.View view, ref bool callBaseAfterReturn", false);
			}

			protected override void WritePartialClassOnSetContentViewPartial_View_LayoutParams (State state)
			{
				WriteMethodStart (state, "partial void", "OnSetContentView", "global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, ref bool callBaseAfterReturn", false);
			}

			protected override void WritePartialClassOnSetContentViewPartial_Int (State state)
			{
				WriteMethodStart (state, "partial void", "OnSetContentView", "int layoutResID, ref bool callBaseAfterReturn", false);
			}

			void WriteBindingInstantiation (State state, string parameters)
			{
				WriteLineIndent (state, $"{BindingPartialClassBackingFieldName} = new global::{state.BindingClassName} ({parameters});");
			}

			void WriteCallBase (State state, string methodName, string parameters)
			{
				WriteLineIndent (state, $"base.{methodName} ({parameters});");
			}

			void WriteMethodStart (State state, string lead, string name, string parameters, bool startBody = true)
			{
				string declaration = $"{lead} {name} ({parameters})";
				if (startBody) {
					WriteLineIndent (state, declaration);
					WriteLineIndent (state, "{");
					state.IncreaseIndent ();
				} else {
					WriteLineIndent (state, $"{declaration};");
				}
			}

			void WriteMethodEnd (State state, string returnExpression = null)
			{
				if (!String.IsNullOrWhiteSpace (returnExpression))
					WriteLineIndent (state, $"return {returnExpression};");

				state.DecreaseIndent ();
				WriteLineIndent (state, "}");
				state.WriteLine ();
			}

			protected override void BeginBindingFile (State state, string layoutResourceId, string classNamespace, string className)
			{
				EnsureArgument (state, nameof (state));

				WriteFilePreamble (state, classNamespace);

				// The class shouldn't be public because some properties may have managed types that
				// aren't public (e.g. custom controls).
				//
				// Perhaps we need a way to communicate that the class should/should not be public?
				//
				WriteClassOpen (state, $"sealed class {className} : global::{BindingBaseTypeFull}");
				WriteLineIndent (state, $"public override int ResourceLayoutID => {layoutResourceId};");
				state.WriteLine ();
			}

			void WriteClassOpen (State state, string classDeclaration)
			{
				WriteLineIndent (state, classDeclaration);
				WriteLineIndent (state, "{");
				state.IncreaseIndent ();
			}

			void WriteClassClose (State state)
			{
				state.DecreaseIndent ();
				WriteLineIndent (state, "}");
			}

			void WriteFilePreamble (State state, string classNamespace)
			{
				WriteUsings (state);
				state.WriteLine ();
				WriteNamespaceOpen (state, classNamespace);
			}

			void WriteNamespaceOpen (State state, string classNamespace)
			{
				if (!state.IsInNamespace)
					return;

				state.WriteLine ($"namespace {classNamespace}");
				state.WriteLine ("{");
				state.IncreaseIndent ();
			}

			void WriteNamespaceClose (State state)
			{
				if (!state.IsInNamespace)
					return;

				state.DecreaseIndent ();
				state.WriteLine ("}");
			}

			void WriteUsings (State state)
			{
				foreach (string ns in ImportNamespaces)
					state.WriteLine ($"using global::{ns};");
			}

			protected override void WriteBindingConstructor (State state, string className)
			{
				WriteLineIndent (state, $"public {className} (global::{BindingClientInterfaceFull} client) : base (client)");
				WriteLineIndent (state, "{}");
				state.WriteLine ();
			}

			public override void EndBindingFile (State state)
			{
				EnsureArgument (state, nameof (state));
				WriteClassClose (state);
				WriteNamespaceClose (state);
			}

			protected override void WriteLocationDirective (State state, LayoutWidget widget)
			{
				EnsureArgument (state, nameof (state));
				EnsureArgument (widget, nameof (widget));

				// There might be several locations, we will write only the first one since we can have
				// only one and first is as good as any
				LayoutLocationInfo loc = widget.Locations?.FirstOrDefault ();
				if (loc == null)
					return;

				WriteLineIndent (state, $"#line {loc.Line} \"{loc.FilePath}\"");
				state.WriteLine ();
			}

			protected override void WriteResetLocation (State state)
			{
				EnsureArgument (state, nameof (state));
				state.WriteLine ();
				WriteLineIndent (state, "#line default");
				WriteLineIndent (state, "#line hidden");
			}

			protected override string GetBindingPropertyBackingField (LayoutWidget widget)
			{
				EnsureArgument (widget, nameof (widget));
				return $"{widget.Type} {GetBindingBackingFieldName (widget)};";
			}

			protected override void WriteBindingViewProperty (State state, LayoutWidget widget, string resourceNamespace)
			{
				EnsureArgument (state, nameof (state));
				EnsureArgument (widget, nameof (widget));

				WriteBindingLambdaPropertyGetter (state, widget, resourceNamespace, "FindView");
			}

			protected override void WriteBindingFragmentProperty (State state, LayoutWidget widget, string resourceNamespace)
			{
				EnsureArgument (state, nameof (state));
				EnsureArgument (widget, nameof (widget));

				WriteBindingLambdaPropertyGetter (state, widget, resourceNamespace, "FindFragment");
			}

			void WritePropertyDeclaration (State state, LayoutWidget widget, bool isLambda)
			{
				WriteIndent (state, $"public {widget.Type} {GetValidPropertyName (state, widget)} ");
				if (isLambda)
					state.Write ("=> ");
				else
					state.WriteLine ("{");
			}

			void WriteBindingFinderCall (State state, LayoutWidget widget, string resourceNamespace, string finderMethodName)
			{
				state.Write ($"{finderMethodName} (global::{resourceNamespace}{widget.Id}, ref {GetBindingBackingFieldName (widget)})");
			}

			void WriteBindingLambdaPropertyGetter (State state, LayoutWidget widget, string resourceNamespace, string finderMethodName)
			{
				WritePropertyDeclaration (state, widget, true);
				WriteBindingFinderCall (state, widget, resourceNamespace, finderMethodName);
				state.WriteLine (";");
			}

			protected override void WriteBindingMixedProperty (State state, LayoutWidget widget, string resourceNamespace)
			{
				EnsureArgument (state, nameof (state));
				EnsureArgument (widget, nameof (widget));

				WritePropertyDeclaration (state, widget, false);
				state.IncreaseIndent ();
				WriteLineIndent (state, "get {");
				state.IncreaseIndent ();

				WriteIndent (state, "object ret = ");
				WriteBindingFinderCall (state, widget, resourceNamespace, "FindView");
				state.WriteLine (";");
				WriteLineIndent (state, "if (ret != null) return ret;");
				WriteIndent (state, "return ");
				WriteBindingFinderCall (state, widget, resourceNamespace, "FindFragment");
				state.WriteLine (";");
				state.DecreaseIndent ();
				WriteLineIndent (state, "}");
				state.DecreaseIndent ();
				WriteLineIndent (state, "}");
			}
		}
	}
}
