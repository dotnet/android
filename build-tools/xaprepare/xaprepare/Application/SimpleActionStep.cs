using System;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class SimpleActionStep : Step
	{
		Func<Context, bool> runner;

#pragma warning disable CS1998
		protected override async Task<bool> Execute (Context context) => runner (context);
#pragma warning restore CS1998

		public SimpleActionStep (string description, Func<Context, bool> stepRunner)
			: base (description)
		{
			if (stepRunner == null)
				throw new ArgumentNullException (nameof (stepRunner));

			runner = stepRunner;
		}
	}
}
