using System;
using System.Collections.Generic;
using System.Text;

namespace ApplicationUtility;

/// <summary>
/// A simple Markdown document builder supporting headings, styled text, and lists
/// with configurable line width and indentation.
/// </summary>
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

	/// <summary>
	/// Appends a Markdown heading of the given level.
	/// </summary>
	/// <param name="level">Heading level (1–6).</param>
	/// <param name="text">The heading text.</param>
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

	/// <summary>
	/// Appends styled text to the document.
	/// </summary>
	public MarkdownDocument AddText (string text, MarkdownTextStyle style = MarkdownTextStyle.Plain, bool addIndent = true)
	{
		AppendText (text, style, breakLine: true, addIndent);
		return this;
	}

	/// <summary>
	/// Appends one or more blank lines to the document.
	/// </summary>
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

	/// <summary>
	/// Begins a new list context with increased indentation.
	/// </summary>
	public MarkdownDocument BeginList (bool appendLine = true)
	{
		if (appendLine) {
			doc.AppendLine ();
		}
		SetNewIndent (2);
		return this;
	}

	/// <summary>
	/// Starts a new list item prefix (<c>* </c>), optionally with initial text.
	/// </summary>
	public MarkdownDocument StartListItem (string? text = null, MarkdownTextStyle style = MarkdownTextStyle.Plain)
	{
		AppendIndent ();
		doc.Append ("* ");

		if (!String.IsNullOrEmpty (text)) {
			AppendText (text, style, addIndent: false);
		}

		return this;
	}

	/// <summary>
	/// Ends the current list item, optionally appending a newline.
	/// </summary>
	public MarkdownDocument EndListItem (bool appendLine = true)
	{
		if (appendLine) {
			doc.AppendLine ();
		}
		return this;
	}

	/// <summary>
	/// Add a list item in one call. If `style` is different to `MarkdownTextStyle.Plain`, the `styleWorkaroundTail` string
	/// is appended to the item. It is necessary because the markdown renderer has a bug where, if the list item is fully styled,
	/// it will render the next list item on the same line as the previous one. This bug is worked around by appending non-styled
	/// and non-whitespace text to the entry.
	/// </summary>
	public MarkdownDocument AddListItem (string? text = null, MarkdownTextStyle style = MarkdownTextStyle.Plain, bool appendLine = true, string styledWorkaroundTail = ".")
	{
		StartListItem (text, style);
		if (style != MarkdownTextStyle.Plain) {
			AddText(styledWorkaroundTail, addIndent: false);
		}
		EndListItem (appendLine);
		return this;
	}

	/// <summary>
	/// Adds a labeled list item with a bold label followed by styled text.
	/// </summary>
	public MarkdownDocument AddLabeledListItem (string label, string text, MarkdownTextStyle textStyle = MarkdownTextStyle.Plain, bool appendLine = true)
	{
		StartListItem ($"{label}:", MarkdownTextStyle.Bold);
		AppendText ($" {text}", textStyle, addIndent: false);
		EndListItem (appendLine);
		return this;
	}

	/// <summary>
	/// Ends the current list context, restoring the previous indentation.
	/// </summary>
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
