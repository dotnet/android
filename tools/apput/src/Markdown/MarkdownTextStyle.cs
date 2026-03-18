using System;

namespace ApplicationUtility;

/// <summary>
/// Defines Markdown text formatting styles that can be combined as flags.
/// </summary>
[Flags]
enum MarkdownTextStyle
{
	Plain     = 0x00,
	Bold      = 0x01,
	Italic    = 0x02,
	Monospace = 0x04
}
