namespace ApplicationUtility;

abstract class MarkdownElement
{
	public string? Text { get; set; }
	public MarkdownContainerElement? Parent { get; protected set; }
}
