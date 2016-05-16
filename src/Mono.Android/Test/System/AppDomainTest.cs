using System;
using System.Globalization;

using Android.App;
using Android.Content;
using Android.Runtime;

using NUnit.Framework;

namespace SystemTests {

	[TestFixture]
	public class AppDomainTest {

		[Test]
		public void DateTime_Now_Works ()
		{
			new Boom().Bang();
			

			var otherDomain = AppDomain.CreateDomain ("other domain");

			var otherType = typeof (Boom);
			var obj = (Boom) otherDomain.CreateInstanceAndUnwrap (
					otherType.Assembly.FullName,
					otherType.FullName);
			obj.Bang ();
		}
	}

	class Boom : MarshalByRefObject
	{
		public void Bang()
		{
			var x = DateTime.Now;
			Console.WriteLine ("Within AppDomain {0}, DateTime.Now={1}.", AppDomain.CurrentDomain.FriendlyName, x);
		}

		public override object InitializeLifetimeService ()
		{
			return null;
		}
	}
}
