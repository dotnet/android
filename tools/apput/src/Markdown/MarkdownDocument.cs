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

	public MarkdownPresenter Render (bool toConsole, bool useColor, bool renderPlainText)
	{
		var presenter = new MarkdownPresenter (toConsole, useColor, renderPlainText);
		if (IsEmpty) {
			return presenter;
		}

		foreach (MarkdownElement element in elements) {
			RenderSafe (presenter, element, renderPlainText);
		}

		return presenter;
	}

	void RenderSafe (MarkdownPresenter presenter, MarkdownElement element, bool plain)
	{
		try {
			Render (presenter, element, plain);
		} catch (Exception ex) {
			Log.Warning ($"Failed to render element {element}.", ex);
		}
	}

	void RenderChildren (MarkdownPresenter presenter, MarkdownContainerElement element, bool plain)
	{
		if (element.Children == null || element.Children.Count == 0) {
			return;
		}

		foreach (MarkdownElement child in element.Children) {
			RenderSafe (presenter, child, plain);
		}
	}

	void Render (MarkdownPresenter presenter, MarkdownElement element, bool plain)
	{
		if (element is MarkdownTextSpan textSpan) {
			Render (presenter, textSpan, plain);
		} else if (element is MarkdownHeading section) {
			Render (presenter, section, plain);
		} else if (element is MarkdownParagraph para) {
			Render (presenter, para, plain);
		} else {
			throw new InvalidOperationException ($"Internal error: Markdown element {element.GetType ()} not supported when rendering.");
		}
	}

	void Render (MarkdownPresenter presenter, MarkdownParagraph para, bool plain)
	{
		RenderChildren (presenter, para, plain);
		int newLines = para.GetNumberOfNewLinesNeeded ();
		if (newLines > 0) {
			for (int i = 0; i < newLines; i++) {
				presenter.AddNewLine ();
			}
		}
	}

	void Render (MarkdownPresenter presenter, MarkdownHeading section, bool plain)
	{
		presenter.Append ('#', (int)section.Level);
		presenter.Append (' ');
		presenter.Append (section.Text);
		presenter.AddNewLine ();
		presenter.AddNewLine ();

		RenderChildren (presenter, section, plain);
	}

	void Render (MarkdownPresenter presenter, MarkdownTextSpan textSpan, bool plain)
	{
		if (textSpan.IsEmpty) {
			return;
		}

		RenderSpan (textSpan);
		foreach (MarkdownTextSpan fragment in textSpan.Fragments) {
			RenderSpan (fragment);
		}

		void RenderSpan (MarkdownTextSpan span)
		{
			MarkdownTextStyle style = (!plain && span.Bold) switch {
				true => MarkdownTextStyle.Bold,
				false => MarkdownTextStyle.Plain
			};

			presenter.Append (span.Text, style);
		}
	}

	void AddNewLine (StringBuilder sb) => sb.Append (Environment.NewLine);
}
