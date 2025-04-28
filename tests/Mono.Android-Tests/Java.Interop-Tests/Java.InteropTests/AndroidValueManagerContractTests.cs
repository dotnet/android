#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {
	[TestFixture, Category ("NativeTypeMap")]
	public class AndroidValueManagerContractTests : JniRuntimeJniValueManagerContract {

		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
		protected override Type ValueManagerType => typeof (Android.Runtime.AndroidValueManager);
	}
}
