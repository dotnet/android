using System;

namespace ApplicationUtility;

[Flags]
enum MarkdownTextStyle
{
	Plain     = 0x00,
	Bold      = 0x01,
	Italic    = 0x02,
	Monospace = 0x04
}
