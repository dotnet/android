using Android.Runtime;
using NUnit.Framework;
using System.Linq.Expressions;

namespace System.LinqTests
{
	// https://github.com/xamarin/xamarin-macios/blob/6b5870d668fe98b61b707101a0b9491480a535fa/tests/linker/ios/link%20all/LinqExpressionTest.cs
	[TestFixture]
	// we want the tests to be available because we use the linker
	[Preserve (AllMembers = true)]
	public class LinqExpressionTest
	{
		delegate object Bug14863Delegate ();

		[Test]
		public void Expression_T_Ctor ()
		{
			var ctor = typeof (LinqExpressionTest).GetConstructor (Type.EmptyTypes);
			var expr = Expression.New (ctor, new Expression[0]);
			Assert.NotNull (Expression.Lambda (typeof (Bug14863Delegate), expr, null), "Lambda");
			// note: reflection is used to create an instance of Expression<TDelegate> using an internal ctor
			// it can be indirectly "preserved" by other code (in Expression) but it can fail in other cases
			// ref: https://bugzilla.xamarin.com/show_bug.cgi?id=14863
		}
	}
}
