using System;

namespace ApplicationUtility;

partial class MarkdownPresenter
{
	abstract class BasePresenter
	{
		protected const string BoldMarker = "**";
		protected const string ItalicMarker = "_";
		protected const string MonospaceMarker = "`";

		public abstract bool RendersToString { get; }

		public abstract void Append (string? text);
		public abstract void Append (char ch);

		public virtual string AsString ()
		{
			throw new InvalidOperationException ("Internal error: inner presenter cannot render to string.");
		}

		public void AddNewLine () => Append (Environment.NewLine);

		public void Append (char ch, int count) => Append (new String (ch, count));

		public virtual void StartBold (object? state) => Append (BoldMarker);
		public virtual void EndBold (object? state) => Append (BoldMarker);

		public virtual void StartItalic (object? state) => Append (ItalicMarker);
		public virtual void EndItalic (object? state) => Append (ItalicMarker);

		public virtual void StartMonospace (object? state) => Append (MonospaceMarker);
		public virtual void EndMonospace (object? state) => Append (MonospaceMarker);

		public virtual object? SaveState () => null;
		public virtual void RestoreState (object? state) {}
	}
}
