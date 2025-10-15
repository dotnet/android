using System;
using System.Collections.Generic;
using System.Text;

namespace ApplicationUtility;

class MarkdownDocument
{
	const int DefaultLineWidth = 100;

	readonly int lineWidth;
	readonly StringBuilder doc = new ();
	readonly Stack<int> indent = new ();

	public string Text => doc.ToString ();

	public MarkdownDocument (int lineWidth = DefaultLineWidth)
	{
		this.lineWidth = lineWidth >= 0 ? lineWidth : DefaultLineWidth;
		ResetIndent ();
	}

	public MarkdownDocument AddHeading (uint level, string text)
	{
		// Headings don't break on `lineWidth`...
		if (doc.Length > 0) {
			AddNewline ();
		}

		// ...and they always start at column 0...
		doc.Append ('#', (int)(level == 0 ? 1 : level));
		doc.Append (' ');
		AppendText (text, breakLine: false);
		AddNewline ();

		// ...and they reset the indent
		ResetIndent ();

		return this;
	}

	public MarkdownDocument AddText (string text, MarkdownTextStyle style = MarkdownTextStyle.Plain, bool addIndent = true)
	{
		AppendText (text, style, breakLine: true, addIndent);
		return this;
	}

	public MarkdownDocument AddNewline (int count = 1)
	{
		if (count < 1) {
			return this;
		}

		for (int i = 0; i < count; i++) {
			doc.AppendLine ();
		}

		return this;
	}

	int AppendIndent ()
	{
		int indent = GetIndent ();
		if (indent > 0) {
			doc.Append (' ', indent);
		}

		return indent;
	}

	void AppendText (string text, MarkdownTextStyle style = MarkdownTextStyle.Plain, bool breakLine = true, bool addIndent = true)
	{
		int indent;

		if (addIndent) {
			indent = AppendIndent ();
		} else {
			indent = GetIndent ();
		}

		if (!breakLine) {
			doc.Append (text);
			return;
		}

		var textToAppend = new StringBuilder (text);
		if (style != MarkdownTextStyle.Plain) {
			if (style.HasFlag (MarkdownTextStyle.Monospace)) {
				textToAppend.Append ('`');
				textToAppend.Insert (0, '`');
			}

			if (style.HasFlag (MarkdownTextStyle.Italic)) {
				textToAppend.Append ('_');
				textToAppend.Insert (0, '_');
			}

			if (style.HasFlag (MarkdownTextStyle.Bold)) {
				textToAppend.Append ('*');
				textToAppend.Insert (0, '*');
			}
		}

		// TODO: implement breaking the line at the last whitespace character before maximum line width.
		//       Indent is included in calculations.
		doc.Append (textToAppend);
	}

	public MarkdownDocument BeginList ()
	{
		doc.AppendLine ();
		SetNewIndent (2);
		return this;
	}

	public MarkdownDocument StartListItem (string? text = null, MarkdownTextStyle style = MarkdownTextStyle.Plain)
	{
		AppendIndent ();
		doc.Append ("* ");

		if (!String.IsNullOrEmpty (text)) {
			AppendText (text, style, addIndent: false);
		}

		return this;
	}

	public MarkdownDocument EndListItem (bool appendLine = true)
	{
		if (appendLine) {
			doc.AppendLine ();
		}
		return this;
	}

	public MarkdownDocument AddListItem (string? text = null, MarkdownTextStyle style = MarkdownTextStyle.Plain, bool appendLine = true)
	{
		StartListItem (text, style);
		EndListItem (appendLine);
		return this;
	}

	public MarkdownDocument AddLabeledListItem (string label, string text, MarkdownTextStyle textStyle = MarkdownTextStyle.Plain, bool appendLine = true)
	{
		StartListItem ($"{label}:", MarkdownTextStyle.Bold);
		AppendText ($" {text}", textStyle, addIndent: false);
		EndListItem (appendLine);
		return this;
	}

	public MarkdownDocument EndList ()
	{
		RestorePreviousIndent ();
		return this;
	}

	int GetIndent () => indent.Peek ();

	int SetNewIndent (int delta = 2)
	{
		int newIndent = GetIndent () + delta;
		indent.Push (newIndent);
		return newIndent;
	}

	int RestorePreviousIndent ()
	{
		indent.Pop ();
		return indent.Peek ();
	}

	void ResetIndent ()
	{
		indent.Clear ();
		indent.Push (0);
	}
}

// FIXME: replace all of this with https://www.nuget.org/packages/Microsoft.PowerShell.MarkdownRender
//
// TODO: abstract out StringBuilder into a class which will have the same API but will be able
//       to either write to a StringBuilder or render directly to the console, with color (if
//       supported)
// class MarkdownDocumentOld
// {
// 	List<MarkdownElement> elements = new ();

// 	public bool IsEmpty => elements.Count == 0;

// 	public MarkdownHeading AddHeading (uint level, string text)
// 	{
// 		var heading = new MarkdownHeading (level, text);
// 		elements.Add (heading);
// 		return heading;
// 	}

// 	public static MarkdownTextSpan CreateNewLine () => new MarkdownTextSpan (Environment.NewLine);
// 	public static MarkdownParagraph CreateParagraph () => new MarkdownParagraph ();
// 	public static MarkdownTextSpan CreateText (string text, bool bold = false) => new MarkdownTextSpan (text) { Bold = bold };

// 	public void AddNewLine ()
// 	{
// 		elements.Add (CreateNewLine ());
// 	}

// 	public MarkdownPresenter Render (bool toConsole, bool useColor, bool renderPlainText)
// 	{
// 		var presenter = new MarkdownPresenter (toConsole, useColor, renderPlainText);
// 		if (IsEmpty) {
// 			return presenter;
// 		}

// 		foreach (MarkdownElement element in elements) {
// 			RenderSafe (presenter, element, renderPlainText);
// 		}

// 		return presenter;
// 	}

// 	void RenderSafe (MarkdownPresenter presenter, MarkdownElement element, bool plain)
// 	{
// 		try {
// 			Render (presenter, element, plain);
// 		} catch (Exception ex) {
// 			Log.Warning ($"Failed to render element {element}.", ex);
// 		}
// 	}

// 	void RenderChildren (MarkdownPresenter presenter, MarkdownContainerElement element, bool plain,
// 	                     Action<MarkdownElement>? beforeElement = null, Action<MarkdownElement>? afterElement = null)
// 	{
// 		if (element.Children == null || element.Children.Count == 0) {
// 			return;
// 		}

// 		foreach (MarkdownElement child in element.Children) {
// 			beforeElement?.Invoke (child);
// 			RenderSafe (presenter, child, plain);
// 			afterElement?.Invoke (child);
// 		}
// 	}

// 	void Render (MarkdownPresenter presenter, MarkdownElement element, bool plain)
// 	{
// 		if (element is MarkdownTextSpan textSpan) {
// 			Render (presenter, textSpan, plain);
// 		} else if (element is MarkdownHeading section) {
// 			Render (presenter, section, plain);
// 		} else if (element is MarkdownParagraph para) {
// 			Render (presenter, para, plain);
// 		} else if (element is MarkdownList list) {
// 			Render (presenter, list, plain);
// 		} else {
// 			throw new InvalidOperationException ($"Internal error: Markdown element {element.GetType ()} not supported when rendering.");
// 		}
// 	}

// 	void Render (MarkdownPresenter presenter, MarkdownList list, bool plain)
// 	{
// 		presenter.AddNewLine ();
// 		RenderChildren (
// 			presenter,
// 			list,
// 			plain,
// 			beforeElement: (MarkdownElement element) => {
// 				if (element is MarkdownList) {
// 					return;
// 				}
// 				presenter.Append ("  * ");
// 			},
// 			afterElement: (MarkdownElement _) => presenter.AddNewLine ()
// 		);
// 		presenter.AddNewLine ();
// 		presenter.AddNewLine ();
// 	}

// 	void Render (MarkdownPresenter presenter, MarkdownParagraph para, bool plain)
// 	{
// 		RenderChildren (presenter, para, plain);
// 		int newLines = para.GetNumberOfNewLinesNeeded ();
// 		if (newLines > 0) {
// 			for (int i = 0; i < newLines; i++) {
// 				presenter.AddNewLine ();
// 			}
// 		}
// 	}

// 	void Render (MarkdownPresenter presenter, MarkdownHeading section, bool plain)
// 	{
// 		presenter.Append ('#', (int)section.Level);
// 		presenter.Append (' ');
// 		presenter.Append (section.Text);
// 		presenter.AddNewLine ();
// 		presenter.AddNewLine ();

// 		RenderChildren (presenter, section, plain);
// 	}

// 	void Render (MarkdownPresenter presenter, MarkdownTextSpan textSpan, bool plain)
// 	{
// 		if (textSpan.IsEmpty) {
// 			return;
// 		}

// 		RenderSpan (textSpan);
// 		if (textSpan.Fragments == null) {
// 			return;
// 		}

// 		foreach (MarkdownTextSpan fragment in textSpan.Fragments) {
// 			RenderSpan (fragment);
// 		}

// 		void RenderSpan (MarkdownTextSpan span)
// 		{
// 			string? text = span.RemoveTailWhitespace ? span.Text?.TrimEnd () : span.Text;

// 			presenter.Append (span.Text, span.Style);
// 		}
// 	}

// 	void AddNewLine (StringBuilder sb) => sb.Append (Environment.NewLine);
// }
