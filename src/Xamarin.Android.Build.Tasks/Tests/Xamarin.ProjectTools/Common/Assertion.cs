using System;
using System.Linq.Expressions;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Represents an "assertion" that a condition is true as the build output streams through a callback
	/// </summary>
	public class Assertion
	{
		readonly Expression<Func<string, bool>> expression;
		Func<string, bool> func;

		public bool Passed { get; private set; }

		public string Message { get; private set; }

		public Assertion (Expression<Func<string, bool>> expression, string message = null)
		{
			this.expression = expression;
			Message = message;
		}

		public void Assert(string line)
		{
			if (!Passed) {
				if (func == null)
					func = expression.Compile ();
				Passed = func (line);
			}
		}

		public override string ToString ()
		{
			if (!string.IsNullOrEmpty (Message))
				return Message;

			return $"Expression was false: {expression.Body}";
		}
	}
}
