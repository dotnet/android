namespace ApplicationUtility;

class MarkdownHeading : MarkdownContainerElement
{
	public uint Level { get; }

	public MarkdownHeading (uint level, string text)
	{
		Level = level == 0 ? 1 : level;
		Text = text;
	}

	public MarkdownHeading AddSubSection (string text)
	{
		var ret = new MarkdownHeading (Level + 1, text);
		AddChild (ret);
		return ret;
	}

	public MarkdownParagraph AddParagraph ()
	{
		var ret = new MarkdownParagraph ();
		AddChild (ret);
		return ret;
	}
}
