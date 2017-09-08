using System;
using System.Collections.Generic;
using System.Diagnostics;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class AndroidSdkInfoTests
	{
		[Test]
		public void Constructor_NullLogger ()
		{
			Action<TraceLevel, string> logger = null;
			Assert.Throws<ArgumentNullException> (() => new AndroidSdkInfo (logger));
		}
	}
}
