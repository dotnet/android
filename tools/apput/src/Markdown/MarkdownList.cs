namespace ApplicationUtility;

class MarkdownList : MarkdownContainerElement
{
	public MarkdownListKind Kind { get; }

	public MarkdownList (MarkdownListKind kind = MarkdownListKind.Bullet)
	{
		Kind = kind;
	}
}
