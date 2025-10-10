using System;
using System.Text;

namespace ApplicationUtility;

partial class MarkdownPresenter
{
	class StringPresenter : BasePresenter
	{
		public override bool RendersToString => false;

		StringBuilder builder = new ();

		public override string AsString () => builder.ToString ();

		public override void Append (string? text)
		{
			throw new NotImplementedException ();
		}

		public override void Append (char ch)
		{
			throw new NotImplementedException ();
		}
	}
}
