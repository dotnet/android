using System;
using System.Collections.Generic;

namespace ApplicationUtility;

class MarkdownTextSpan : MarkdownElement
{
	readonly List<MarkdownTextSpan> fragments = new ();
	MarkdownTextStyle textStyle;

	public List<MarkdownTextSpan> Fragments => fragments;
	public bool IsEmpty => fragments.Count == 0 && String.IsNullOrEmpty (Text);
	public MarkdownTextStyle Style => textStyle;

	public bool Bold {
		get => HasStyle (MarkdownTextStyle.Bold);
		set => SetStyle (MarkdownTextStyle.Bold, value);
	}

	public bool Italic {
		get => HasStyle (MarkdownTextStyle.Italic);
		set => SetStyle (MarkdownTextStyle.Italic, value);
	}

	public bool Monospace {
		get => HasStyle (MarkdownTextStyle.Monospace);
		set => SetStyle (MarkdownTextStyle.Monospace, value);
	}

	public bool RemoveTailWhitespace { get; set; }

	public MarkdownTextSpan (string text, MarkdownTextStyle style = MarkdownTextStyle.Plain)
	{
		Console.WriteLine ($"New span '{text}'; style: {style}");
		Text = text;
		textStyle = style;
	}

	public void AddNewline ()
	{
		fragments.Add (MarkdownDocument.CreateNewLine ());
	}

	public void AddText (string text)
	{
		fragments.Add (new MarkdownTextSpan (text));
	}

	bool HasStyle (MarkdownTextStyle style) => textStyle.HasFlag (style);

	void SetStyle (MarkdownTextStyle style, bool enable)
	{
		if (enable) {
			textStyle |= style;
		} else {
			textStyle &= ~style;
		}
	}
}
