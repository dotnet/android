namespace ApplicationUtility;

class MarkdownList : MarkdownContainerElement
{
	public MarkdownListKind Kind { get; }

	public MarkdownList (MarkdownListKind kind = MarkdownListKind.Bullet)
	{
		Kind = kind;
	}

	public void Add (string text) => AddChild (new MarkdownTextSpan (text) { RemoveTailWhitespace = true });
}
