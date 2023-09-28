using System;
using System.IO;
using System.Linq;
using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

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

				if (!state.ExtraImportNamespaces.Contains ("System.Reflection"))
					state.ExtraImportNamespaces.Add ("System.Reflection");

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
				WriteSetContentView (state, "global::Android.Views.View view", "view", "view");
			}

			protected override void WritePartialClassSetContentView_View_LayoutParams (State state)
			{
				WriteSetContentView (state, "global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params", "view, @params", "view");
			}

			protected override void WritePartialClassSetContentView_Int (State state)
			{
				WriteSetContentView (state, "int layoutResID", "layoutResID", "this");
			}

			void WriteSetContentView (State state, string declarationParams, string callParams, string bindingConstructorParams)
			{
				WriteMethodStart (state, $"public override void", "SetContentView", declarationParams);
				WriteCommonSetContentViewBody (state, callParams, bindingConstructorParams, true, false);

				WriteMethodStart (state, "void", "SetContentView", $"{declarationParams}, global::{ItemNotFoundHandlerType} onLayoutItemNotFound");
				WriteCommonSetContentViewBody (state, callParams, bindingConstructorParams, false, true);
			}

			void WriteCommonSetContentViewBody (State state, string setContentViewParams, string bindingConstructorParams, bool baseCall, bool outputNotFoundHandlers)
			{
				string notFoundHandlers = outputNotFoundHandlers ? ", onLayoutItemNotFound" : String.Empty;
				WriteBindingInstantiation (state, $"{bindingConstructorParams}{notFoundHandlers}");
				WriteLineIndent (state, "bool callBase = true;");
				WriteLineIndent (state, $"OnSetContentView ({setContentViewParams}, ref callBase);");
				WriteLineIndent (state, "if (callBase) {");
				state.IncreaseIndent ();
				WriteLineIndent (state, $"base.SetContentView ({setContentViewParams});");
				state.DecreaseIndent ();
				WriteLineIndent (state, "}");
				WriteMethodEnd (state);
			}

			protected override void WritePartialClassOnSetContentViewPartial_View (State state)
			{
				WriteMethodStart (state, "partial void", "OnSetContentView", "global::Android.Views.View view, ref bool callBaseAfterReturn", startBody: false);
			}

			protected override void WritePartialClassOnSetContentViewPartial_View_LayoutParams (State state)
			{
				WriteMethodStart (state, "partial void", "OnSetContentView", "global::Android.Views.View view, global::Android.Views.ViewGroup.LayoutParams @params, ref bool callBaseAfterReturn", startBody: false);
			}

			protected override void WritePartialClassOnSetContentViewPartial_Int (State state)
			{
				WriteMethodStart (state, "partial void", "OnSetContentView", "int layoutResID, ref bool callBaseAfterReturn", startBody: false);
			}

			void WriteBindingInstantiation (State state, string parameters)
			{
				WriteLineIndent (state, $"{BindingPartialClassBackingFieldName} = new global::{state.BindingClassName} ({parameters});");
			}

			void WriteMethodStart (State state, string lead, string name, string parameters, string genericConstraint = null, bool startBody = true)
			{
				string declaration = $"{lead} {name} ({parameters})";
				if (!String.IsNullOrEmpty (genericConstraint))
					declaration = $"{declaration} where {genericConstraint}";
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
					WriteUsing (state, ns);

				if (state.ExtraImportNamespaces.Count == 0)
					return;

				foreach (string ns in state.ExtraImportNamespaces)
					WriteUsing (state, ns);
			}

			void WriteUsing (State state, string ns)
			{
				state.WriteLine ($"using global::{ns};");
			}

			protected override void WriteBindingConstructors (State state, string className, bool linkerPreserve)
			{
				WriteConstructor (state, className, "Android.App.Activity", linkerPreserve);
				WriteConstructor (state, className, "Android.Views.View", linkerPreserve);
			}

			void WriteConstructor (State state, string className, string clientType, bool linkerPreserve)
			{
				WritePreserveAtribute (state, linkerPreserve);
				WriteLineIndent (state, $"public {className} (");
				state.IncreaseIndent ();
				WriteLineIndent (state, $"global::{clientType} client,");
				WriteLineIndent (state, $"global::{ItemNotFoundHandlerType} itemNotFoundHandler = null)");
				state.IncreaseIndent ();
				WriteLineIndent (state, ": base (client, itemNotFoundHandler)");
				state.DecreaseIndent ();
				state.DecreaseIndent ();
				WriteLineIndent (state, "{}");
				state.WriteLine ();
			}

			void WritePreserveAtribute (State state, bool linkerPreserve)
			{
				if (!linkerPreserve)
					return;

				WriteLineIndent (state, $"[global::Android.Runtime.PreserveAttribute (Conditional=true)]");
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

				WriteLineIndent (state, $"#line {loc.Line} \"{Path.GetFullPath (loc.FilePath)}\"");
				state.WriteLine ();
			}

			protected override void WriteResetLocation (State state)
			{
				EnsureArgument (state, nameof (state));
				state.WriteLine ();
				WriteLineIndent (state, "#line default");
				WriteLineIndent (state, "#line hidden");
			}

			protected override string GetBindingPropertyBackingField (State state, LayoutWidget widget)
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

				WriteBindingLambdaPropertyGetter (state, widget, resourceNamespace, "FindFragment", true);
			}

			void WritePropertyDeclaration (State state, LayoutWidget widget, bool isLambda)
			{
				WriteIndent (state, $"public {widget.Type} {GetValidPropertyName (state, widget)} ");
				if (isLambda)
					state.Write ("=> ");
				else
					state.WriteLine ("{");
			}

			void WriteBindingFinderCall (State state, LayoutWidget widget, string resourceNamespace, string finderMethodName, bool isFragment = false)
			{
				bool isIdGlobal;
				string id = GetWidgetId (widget, out isIdGlobal);

				if (!isIdGlobal)
					id = $"{resourceNamespace}{id}";

				string backingFieldName = GetBindingBackingFieldName (widget);
				string extraParam;
				if (isFragment)
					extraParam = $" {backingFieldName},";
				else
					extraParam = null;
				state.Write ($"{finderMethodName} (global::{id},{extraParam} ref {backingFieldName})");
			}

			void WriteBindingLambdaPropertyGetter (State state, LayoutWidget widget, string resourceNamespace, string finderMethodName, bool isFragment = false)
			{
				WritePropertyDeclaration (state, widget, true);
				WriteBindingFinderCall (state, widget, resourceNamespace, finderMethodName, isFragment);
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
				WriteBindingFinderCall (state, widget, resourceNamespace, "FindFragment", true);
				state.WriteLine (";");
				state.DecreaseIndent ();
				WriteLineIndent (state, "}");
				state.DecreaseIndent ();
				WriteLineIndent (state, "}");
			}
		}
	}
}
