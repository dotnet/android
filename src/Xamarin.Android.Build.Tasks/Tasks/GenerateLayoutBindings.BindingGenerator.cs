using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tasks
{
	partial class GenerateLayoutBindings
	{
		internal abstract class BindingGenerator
		{
			public sealed class State
			{
				StringBuilder indentBuilder = new StringBuilder ();
				string indent = String.Empty;
				StreamWriter writer;

				public StreamWriter Writer => writer;
				public string Indent => indent;

				// Name of the class being output
				public string ClassName { get; }

				// Whether the generator needs to output the namespace declaration at the top of the
				// file and properly close that at the end of the file
				public bool IsInNamespace { get; }

				// Name of the *binding* class with properties that access the widgets - used only when
				// generating code for the partial Activity class
				public string BindingClassName { get; }

				public List<string> ExtraImportNamespaces { get; } = new List <string> ();

				public string AndroidFragmentType { get; }

				public State (StreamWriter writer, string className, bool isInNamespace, string androidFragmentType, string bindingClassName = null)
				{
					if (writer == null)
						throw new ArgumentNullException (nameof (writer));
					if (String.IsNullOrWhiteSpace (className))
						throw new ArgumentException (nameof (writer));
					if (String.IsNullOrEmpty (androidFragmentType))
						throw new ArgumentException (nameof (androidFragmentType));

					this.writer = writer;
					ClassName = className;
					BindingClassName = bindingClassName;
					IsInNamespace = isInNamespace;
					AndroidFragmentType = androidFragmentType;
				}

				public void IncreaseIndent ()
				{
					indentBuilder.Append ('\t');
					indent = indentBuilder.ToString ();
				}

				public void DecreaseIndent ()
				{
					if (indentBuilder.Length == 0)
						return;

					indentBuilder.Remove (indentBuilder.Length - 1, 1);
					indent = indentBuilder.ToString ();
				}

				public void WriteLine (string text = null)
				{
					if (text == null)
						writer.WriteLine ();
					else
						writer.WriteLine (text);
				}

				public void Write (string text = null)
				{
					if (text == null)
						return;
					writer.Write (text);
				}

				public void WriteIndent (string text)
				{
					Write ($"{indent}{text}");
				}

				public void WriteLineIndent (string text)
				{
					WriteLine ($"{indent}{text}");
				}
			}

			protected const string XamarinBindingsNamespace = "Xamarin.Android.Design";

			// This must be a fully qualified type name
			protected const string BindingBaseTypeFull = XamarinBindingsNamespace + ".LayoutBinding";
			protected const string ItemNotFoundHandlerType = XamarinBindingsNamespace + ".OnLayoutItemNotFoundHandler";

			protected static readonly List<string> ImportNamespaces = new List <string> {
				"System",
				"Android.App",
				"Android.Widget",
				"Android.Views",
				"Android.OS",
			};

			protected abstract string LineCommentString { get; }
			protected abstract string DocCommentString { get; }
			public abstract string LanguageName { get; }
			public abstract bool CaseSensitive { get; }

			protected BindingGenerator ()
			{}

			public State BeginPartialClassFile (StreamWriter writer, string bindingClassName, string classNamespace, string className, string androidFragmentType)
			{
				if (String.IsNullOrEmpty (bindingClassName))
					throw new ArgumentException (nameof (bindingClassName));
				var state = new State (writer, className, !String.IsNullOrWhiteSpace (classNamespace), androidFragmentType, bindingClassName);
				BeginPartialClassFile (state, classNamespace, className);
				WritePartialClassSetContentViewOverrides (state);
				WriteOnSetContentViewPartials (state);
				state.WriteLine ();
				return state;
			}

			public abstract void SetCodeBehindDir (string path);

			protected abstract void BeginPartialClassFile (State state, string classNamespace, string className);
			public abstract void EndPartialClassFile (State state);
			public abstract void WritePartialClassProperty (State state, LayoutWidget widget);

			protected abstract void WritePartialClassSetContentView_View (State state);
			protected abstract void WritePartialClassSetContentView_View_LayoutParams (State state);
			protected abstract void WritePartialClassSetContentView_Int (State state);

			protected virtual void WritePartialClassSetContentViewOverrides (State state)
			{
				WritePartialClassSetContentView_View (state);
				WritePartialClassSetContentView_View_LayoutParams (state);
				WritePartialClassSetContentView_Int (state);
			}

			protected abstract void WritePartialClassOnSetContentViewPartial_View (State state);
			protected abstract void WritePartialClassOnSetContentViewPartial_View_LayoutParams (State state);
			protected abstract void WritePartialClassOnSetContentViewPartial_Int (State state);

			protected virtual void WriteOnSetContentViewPartials (State state)
			{
				WritePartialClassOnSetContentViewPartial_View (state);
				WritePartialClassOnSetContentViewPartial_View_LayoutParams (state);
				WritePartialClassOnSetContentViewPartial_Int (state);
			}

			public State BeginBindingFile (StreamWriter writer, string layoutResourceId, string classNamespace, string className, string androidFragmentType)
			{
				var state = new State (writer, className, !String.IsNullOrWhiteSpace (classNamespace), androidFragmentType);
				BeginBindingFile (state, layoutResourceId, classNamespace, className);
				WriteBindingConstructors (state, className);
				return state;
			}

			protected abstract void BeginBindingFile (State state, string layoutResourceId, string classNamespace, string className);
			public abstract void EndBindingFile (State state);

			protected abstract void WriteBindingConstructors (State state, string className);
			protected abstract void WriteBindingViewProperty (State state, LayoutWidget widget, string resourceNamespace);
			protected abstract void WriteBindingFragmentProperty (State state, LayoutWidget widget, string resourceNamespace);
			protected abstract void WriteBindingMixedProperty (State state, LayoutWidget widget, string resourceNamespace);
			protected abstract void WriteLocationDirective (State state, LayoutWidget widget);
			protected abstract void WriteResetLocation (State state);
			protected abstract string GetBindingPropertyBackingField (State state, LayoutWidget widget);

			public void WriteBindingProperty (State state, LayoutWidget widget, string resourceNamespace)
			{
				EnsureArgument (state, nameof (state));
				EnsureArgument (widget, nameof (widget));

				state.WriteLine ();
				WriteBindingPropertyBackingField (state, widget);
				state.WriteLine ();
				WriteLocationDirective (state, widget);

				foreach (LayoutLocationInfo loc in widget.Locations) {
					if (loc == null)
						continue;
					WriteComment (state, $" Declared in: {loc.FilePath}:({loc.Line}:{loc.Column})");
				}

				if (widget.TypeFixups != null) {
					foreach (LayoutTypeFixup tf in widget.TypeFixups) {
						WriteComment (state, $" Type fixed up from '{tf.OldType}' to '{widget.Type}'. Element defined in {tf.Location.FilePath}:({tf.Location.Line}:{tf.Location.Column})");
					}
				}

				switch (widget.WidgetType) {
					case LayoutWidgetType.View:
						WriteBindingViewProperty (state, widget, resourceNamespace);
						break;

					case LayoutWidgetType.Fragment:
						WriteBindingFragmentProperty (state, widget, resourceNamespace);
						break;

					case LayoutWidgetType.Mixed:
						WriteBindingMixedProperty (state, widget, resourceNamespace);
						break;

					case LayoutWidgetType.Unknown:
						throw new InvalidOperationException ($"Widget must have a known type (ID: {GetWidgetId (widget)})");

					default:
						throw new InvalidOperationException ($"Unsupported widget type '{widget.WidgetType}' (ID: {GetWidgetId (widget)})");
				}
				WriteResetLocation (state);
				state.WriteLine ();
			}

			protected virtual string GetBindingBackingFieldName (LayoutWidget widget)
			{
				return $"__{widget.Name}";
			}

			protected virtual string GetValidPropertyName (State state, LayoutWidget widget)
			{
				EnsureArgument (state, nameof (state));
				EnsureArgument (state, nameof (widget));

				// It is possible that a widget/element will have the same ID as the name of the
				// encompassing layout. In this case we mustn't use the ID of the widget directly
				// because a class cannot have members (other than constructors) with named the same as
				// the enclosing type. We could rename the generated class but that still doesn't
				// guarantee the lack of naming conflicts (e.g. if we append `Layout` to the class name,
				// there may still be a widget with the same ID). It is better to append a suffix to the
				// property name since we're guaranteed that each property is unique.
				StringComparison comparison = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
				if (String.Compare (state.ClassName, widget.Name, comparison) != 0 && !NameMatchesBindingClass (state, widget, comparison))
					return widget.Name;

				string suffix = null;
				switch (widget.WidgetType) {
					case LayoutWidgetType.View:
						suffix = "View";
						break;

					case LayoutWidgetType.Fragment:
						suffix = "Fragment";
						break;

					case LayoutWidgetType.Mixed:
						suffix = "Object";
						break;

					case LayoutWidgetType.Unknown:
						throw new InvalidOperationException ($"Widget must have a known type (ID: {GetWidgetId (widget)})");

					default:
						throw new InvalidOperationException ($"Unsupported widget type '{widget.WidgetType}' (ID: {GetWidgetId (widget)})");
				}

				return $"{widget.Name}_{suffix}";
			}

			bool NameMatchesBindingClass (State state, LayoutWidget widget, StringComparison comparison)
			{
				if (String.IsNullOrEmpty (state.BindingClassName))
					return false;

				int dot = state.BindingClassName.LastIndexOf ('.');
				if (dot < 0)
					return false;

				return String.Compare (state.BindingClassName.Substring (dot + 1), widget.Name, comparison) == 0;
			}

			protected void WriteBindingPropertyBackingField (State state, LayoutWidget widget)
			{
				EnsureArgument (state, nameof (state));
				EnsureArgument (widget, nameof (widget));
				WriteLocationDirective (state, widget);
				WriteLineIndent (state, GetBindingPropertyBackingField (state, widget));
				WriteResetLocation (state);
			}

			public void WriteComment (State state, params string [] text)
			{
				EnsureArgument (state, nameof (state));
				foreach (string line in text)
					WriteLineIndent (state, $"{LineCommentString}{line}");
			}

			public void WriteComment (State state, ICollection<string> lines)
			{
				if (lines == null)
					return;

				EnsureArgument (state, nameof (state));
				foreach (string line in lines) {
					WriteComment (state, line);
				}
			}

			public void WriteDocComment (State state, string text)
			{
				EnsureArgument (state, nameof (state));
				WriteLineIndent (state, $"{DocCommentString}{text}");
			}

			public void WriteDocComment (State state, ICollection<string> lines)
			{
				if (lines == null)
					return;

				EnsureArgument (state, nameof (state));
				foreach (string line in lines) {
					WriteComment (state, line);
				}
			}

			protected void WriteIndent (State state, string text)
			{
				EnsureArgument (state, nameof (state));
				state.WriteIndent (text);
			}

			protected void WriteLineIndent (State state, string text)
			{
				EnsureArgument (state, nameof (state));
				state.WriteLineIndent (text);
			}

			protected void EnsureArgument <T> (T parameter, string name) where T: class
			{
				if (parameter == null)
					throw new ArgumentNullException (name);
			}

			protected string GetWidgetId (LayoutWidget widget)
			{
				return GetWidgetId (widget, out _);
			}

			protected string GetWidgetId (LayoutWidget widget, out bool isGlobal)
			{
				if (String.IsNullOrEmpty (widget?.Id)) {
					isGlobal = false;
					return String.Empty;
				}

				isGlobal = widget.Id.StartsWith (CalculateLayoutCodeBehind.GlobalIdPrefix, StringComparison.Ordinal);
				if (!isGlobal)
					return widget.Id;

				return widget.Id.Substring (CalculateLayoutCodeBehind.GlobalIdPrefix.Length);
			}
		}
	}
}
