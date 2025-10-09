using System;
using System.Collections.Generic;
using System.Text;

namespace ApplicationUtility;

// TODO: abstract out StringBuilder into a class which will have the same API but will be able
//       to either write to a StringBuilder or render directly to the console, with color (if
//       supported)
class MarkdownDocument
{
	List<MarkdownElement> elements = new ();

	public bool IsEmpty => elements.Count == 0;

	public MarkdownHeading AddHeading (uint level, string text)
	{
		var heading = new MarkdownHeading (level, text);
		elements.Add (heading);
		return heading;
	}

	public static MarkdownTextSpan CreateNewLine () => new MarkdownTextSpan (Environment.NewLine);
	public static MarkdownParagraph CreateParagraph () => new MarkdownParagraph ();
	public static MarkdownTextSpan CreateText (string text, bool bold = false) => new MarkdownTextSpan (text) { Bold = bold };

	public void AddNewLine ()
	{
		elements.Add (CreateNewLine ());
	}

	public string Render (bool renderPlainText)
	{
		if (IsEmpty) {
			return String.Empty;
		}

		var sb = new StringBuilder ();
		foreach (MarkdownElement element in elements) {
			RenderSafe (sb, element, renderPlainText);
		}

		return sb.ToString ();
	}

	void RenderSafe (StringBuilder sb, MarkdownElement element, bool plain)
	{
		try {
			Render (sb, element, plain);
		} catch (Exception ex) {
			Log.Warning ($"Failed to render element {element}.", ex);
		}
	}

	void RenderChildren (StringBuilder sb, MarkdownContainerElement element, bool plain)
	{
		if (element.Children == null || element.Children.Count == 0) {
			return;
		}

		foreach (MarkdownElement child in element.Children) {
			RenderSafe (sb, child, plain);
		}
	}

	void Render (StringBuilder sb, MarkdownElement element, bool plain)
	{
		if (element is MarkdownTextSpan textSpan) {
			Render (sb, textSpan, plain);
		} else if (element is MarkdownHeading section) {
			Render (sb, section, plain);
		} else if (element is MarkdownParagraph para) {
			Render (sb, para, plain);
		} else {
			throw new InvalidOperationException ($"Internal error: Markdown element {element.GetType ()} not supported when rendering.");
		}
	}

	void Render (StringBuilder sb, MarkdownParagraph para, bool plain)
	{
		RenderChildren (sb, para, plain);
		int newLines = para.GetNumberOfNewLinesNeeded ();
		if (newLines > 0) {
			for (int i = 0; i < newLines; i++) {
				AddNewLine (sb);
			}
		}
	}

	void Render (StringBuilder sb, MarkdownHeading section, bool plain)
	{
		sb.Append ('#', (int)section.Level);
		sb.Append (' ');
		sb.Append (section.Text);
		AddNewLine (sb);
		AddNewLine (sb);

		RenderChildren (sb, section, plain);
	}

	void Render (StringBuilder sb, MarkdownTextSpan textSpan, bool plain)
	{
		if (textSpan.IsEmpty) {
			return;
		}

		const string Bold = "**";

		RenderSpan (textSpan);
		foreach (MarkdownTextSpan fragment in textSpan.Fragments) {
			RenderSpan (fragment);
		}

		void RenderSpan (MarkdownTextSpan span)
		{
			if (!plain && span.Bold) {
				sb.Append (Bold);
			}

			sb.Append (span.Text);

			if (!plain && span.Bold) {
				sb.Append (Bold);
			}
		}
	}

	void AddNewLine (StringBuilder sb) => sb.Append (Environment.NewLine);
}
