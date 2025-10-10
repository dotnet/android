using System;
using System.Text;

namespace ApplicationUtility;

partial class MarkdownPresenter
{
	class ConsolePresenter : BasePresenter
	{
		sealed class ColorState
		{
			public ConsoleColor Foreground;
			public ConsoleColor Background;
			public bool BoldEnabled;
			public bool ItalicEnabled;
		}

		const ConsoleColor Bold = ConsoleColor.White;
		const ConsoleColor Italic = ConsoleColor.Cyan;
		const ConsoleColor BoldItalicFg = ConsoleColor.Gray;
		const ConsoleColor BoldItalicBg = ConsoleColor.DarkRed;

		readonly bool useColor;

		public override bool RendersToString => false;

		public ConsolePresenter (bool useColor)
		{
			this.useColor = useColor;
		}

		public override void Append (string? text) => Console.Write (text);
		public override void Append (char ch) => Console.Write (ch);

		public override object? SaveState ()
		{
			if (!useColor) {
				return null;
			}

			return new ColorState {
				Foreground = Console.ForegroundColor,
				Background = Console.BackgroundColor,
			};
		}

		public override void RestoreState (object? state)
		{
			var colors = state as ColorState;
			if (colors == null) {
				return;
			}

			Console.ForegroundColor = colors.Foreground;
			Console.BackgroundColor = colors.Background;
		}

		public override void StartBold (object? state)
		{
			if (!useColor) {
				base.StartBold (state);
				return;
			}

			var colors = state as ColorState;
			if (colors == null) {
				Console.ForegroundColor = Bold;
				return;
			}

			colors.BoldEnabled = true;
			if (colors.ItalicEnabled) {
				Console.ForegroundColor = BoldItalicFg;
				Console.BackgroundColor = BoldItalicBg;
			} else {
				Console.ForegroundColor = Bold;
			}
		}

		public override void EndBold (object? state)
		{
			var colors = state as ColorState;
			if (colors == null) {
				base.EndBold (state);
				return;
			}

			if (colors.ItalicEnabled) {
				Console.ForegroundColor = Italic;
				Console.BackgroundColor = colors.Background;
			} else {
				Console.ForegroundColor = colors.Foreground;
			}
			colors.BoldEnabled = false;
		}

		public override void StartItalic (object? state)
		{
			if (!useColor) {
				base.StartItalic (state);
				return;
			}

			var colors = state as ColorState;
			if (colors == null) {
				Console.ForegroundColor = Italic;
				return;
			}

			colors.ItalicEnabled = true;
			if (colors.BoldEnabled) {
				Console.ForegroundColor = BoldItalicFg;
				Console.BackgroundColor = BoldItalicBg;
			} else {
				Console.ForegroundColor = Italic;
			}
		}

		public override void EndItalic (object? state)
		{
			var colors = state as ColorState;
			if (colors == null) {
				base.EndBold (state);
				return;
			}

			if (colors.BoldEnabled) {
				Console.ForegroundColor = Bold;
				Console.BackgroundColor = colors.Background;
			} else {
				Console.ForegroundColor = colors.Foreground;
			}
			colors.ItalicEnabled = false;
		}
	}
}
