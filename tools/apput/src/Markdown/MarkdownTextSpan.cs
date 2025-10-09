using System.Collections.Generic;

namespace ApplicationUtility;

class MarkdownTextSpan : MarkdownElement
{
	List<MarkdownTextSpan> fragments = new ();

	public List<MarkdownTextSpan> Fragments => fragments;
	public bool IsEmpty => fragments.Count == 0;

	public bool Bold { get; set; }

	public MarkdownTextSpan (string text)
	{
		Text = text;
	}

	public void AddNewline ()
	{
		fragments.Add (MarkdownDocument.CreateNewLine ());
	}

	public void AddText (string text)
	{
		fragments.Add (new MarkdownTextSpan (text));
	}
}
