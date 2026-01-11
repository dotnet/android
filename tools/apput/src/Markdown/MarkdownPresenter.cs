using System;

namespace ApplicationUtility;

partial class MarkdownPresenter
{
	readonly BasePresenter presenter;
	readonly bool renderPlainText;

	public bool RendersToString => presenter.RendersToString;

	public MarkdownPresenter (bool toConsole, bool useColor, bool renderPlainText)
	{
		if (!toConsole || Console.IsOutputRedirected) {
			presenter = new StringPresenter ();
		} else {
			presenter = new ConsolePresenter (useColor);
		}
		this.renderPlainText = renderPlainText;
	}

	public MarkdownPresenter AddNewLine ()
	{
		presenter.AddNewLine ();
		return this;
	}

	public MarkdownPresenter Append (char ch, int count, MarkdownTextStyle style = MarkdownTextStyle.Plain)
	{
		object? state = AddStartMarkup (style);
		presenter.Append (ch, count);
		AddEndMarkup (style, state);
		return this;
	}

	public MarkdownPresenter Append (char ch, MarkdownTextStyle style = MarkdownTextStyle.Plain)
	{
		object? state = AddStartMarkup (style);
		presenter.Append (ch);
		AddEndMarkup (style, state);
		return this;
	}

	public MarkdownPresenter Append (string? text, MarkdownTextStyle style = MarkdownTextStyle.Plain)
	{
		if (String.IsNullOrEmpty (text)) {
			return this;
		}

		object? state = AddStartMarkup (style);
		presenter.Append (text);
		AddEndMarkup (style, state);
		return this;
	}

	public string AsString () => presenter.AsString ();

	// These two MUST always be called in pairs, with the same `style`
	object? AddStartMarkup (MarkdownTextStyle style)
	{
		if (renderPlainText) {
			return null;
		}

		object? state = presenter.SaveState ();
		if (style.HasFlag (MarkdownTextStyle.Italic)) {
			presenter.StartItalic (state);
		}

		if (style.HasFlag (MarkdownTextStyle.Bold)) {
			presenter.StartBold (state);
		}

		if (style.HasFlag (MarkdownTextStyle.Monospace)) {
			presenter.StartMonospace (state);
		}

		return state;
	}

	void AddEndMarkup (MarkdownTextStyle style, object? state)
	{
		if (renderPlainText) {
			return;
		}

		if (style.HasFlag (MarkdownTextStyle.Monospace)) {
			presenter.EndMonospace (state);
		}

		if (style.HasFlag (MarkdownTextStyle.Bold)) {
			presenter.EndBold (state);
		}

		if (style.HasFlag (MarkdownTextStyle.Italic)) {
			presenter.EndItalic (state);
		}
	}
}
