using System;
using System.Linq.Expressions;

namespace Xamarin.ProjectTools
{
	public class Assertion
	{
		readonly Expression<Func<string, bool>> expression;
		Func<string, bool> func;

		public bool Passed { get; private set; }

		public Assertion (Expression<Func<string, bool>> expression)
		{
			this.expression = expression;
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
			return expression.Body.ToString ();
		}
	}
}
