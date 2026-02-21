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
			public bool MonospaceEnabled;
		}

		const ConsoleColor Normal = ConsoleColor.Gray;
		const ConsoleColor NormalBold = ConsoleColor.White;
		const ConsoleColor Italic = ConsoleColor.DarkCyan;
		const ConsoleColor ItalicBold = ConsoleColor.Cyan;
		const ConsoleColor Monospace = ConsoleColor.DarkGreen;
		const ConsoleColor MonospaceBold = ConsoleColor.Green;

		readonly bool useColor;

		public override bool RendersToString => false;

		public ConsolePresenter (bool useColor)
		{
			this.useColor = useColor;
			if (useColor) {
				Console.ForegroundColor = Normal;
			}
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
				Console.ForegroundColor = NormalBold;
				return;
			}

			colors.BoldEnabled = true;
			UpdateColors (colors);
		}

		public override void EndBold (object? state)
		{
			var colors = state as ColorState;
			if (colors == null) {
				Console.ForegroundColor = Normal;
				base.EndBold (state);
				return;
			}

			colors.BoldEnabled = false;
			UpdateColors (colors);
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
			UpdateColors (colors);
		}

		public override void EndItalic (object? state)
		{
			var colors = state as ColorState;
			if (colors == null) {
				base.EndItalic (state);
				return;
			}

			colors.ItalicEnabled = false;
			UpdateColors (colors);
		}

		public override void StartMonospace (object? state)
		{
			if (!useColor) {
				base.StartMonospace (state);
				return;
			}

			var colors = state as ColorState;
			if (colors == null) {
				Console.ForegroundColor = Monospace;
				return;
			}

			colors.MonospaceEnabled = true;
			UpdateColors (colors);
		}

		public override void EndMonospace (object? state)
		{
			var colors = state as ColorState;
			if (colors == null) {
				base.EndMonospace (state);
				return;
			}

			colors.MonospaceEnabled = false;
			UpdateColors (colors);
		}

		void UpdateColors (ColorState colors)
		{
			if (colors.MonospaceEnabled) {
				Console.ForegroundColor = colors.BoldEnabled ? MonospaceBold : Monospace;
			} else if (colors.ItalicEnabled) {
				Console.ForegroundColor = colors.BoldEnabled ? ItalicBold : Italic;
			} else {
				Console.ForegroundColor = colors.BoldEnabled ? NormalBold : Normal;
			}
		}
	}
}
