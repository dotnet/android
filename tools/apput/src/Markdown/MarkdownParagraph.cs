using System;
using System.Linq;

namespace ApplicationUtility;

class MarkdownParagraph : MarkdownContainerElement
{
	public override void AddChild (MarkdownElement element)
	{
		if (element is MarkdownTextSpan || element is MarkdownList) {
			base.AddChild (element);
			return;
		}

		throw new InvalidOperationException ($"Internal error: element {element.GetType ()} cannot be added to a paragraph.");
	}

	public int GetNumberOfNewLinesNeeded ()
	{
		if (Children == null || Children.Count == 0) {
			return 0;
		}

		// Paragraph must be followed by at least one empty line
		var span = Children.LastOrDefault () as MarkdownTextSpan;
		if (span == null || span.Fragments.Count == 0) {
			return 0;
		}

		span = span.Fragments.LastOrDefault ();
		if (span == null || span.Text == null || span.Text.Length == 0) {
			return 0;
		}

		int count = 0;
		for (int idx = span.Text.Length - 1; idx >= 0; idx--) {
			if (!IsNewLine (span.Text[idx])) {
				break;
			}
			count++;
		}

		const int MaxNumOfNewlines = 2;
		return count >= MaxNumOfNewlines ? 0 : MaxNumOfNewlines - count;

		bool IsNewLine (char ch) => ch switch {
			'\r' => true,
			'\n' => true,
			_ => false
		};
	}
}
