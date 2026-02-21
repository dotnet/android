using System.Collections.Generic;

namespace ApplicationUtility;

abstract class MarkdownContainerElement : MarkdownElement
{
	public List<MarkdownElement>? Children { get; private set; }

	public virtual void AddChild (MarkdownElement element)
	{
		if (Children == null) {
			Children = new ();
		}

		Children.Add (element);
	}
}
