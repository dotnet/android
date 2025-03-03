#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {
	[TestFixture, Category ("Mono")]
	public class AndroidValueManagerContractTests : JniRuntimeJniValueManagerContract {

		protected override Type ValueManagerType => typeof (Android.Runtime.AndroidValueManager);
	}
}
