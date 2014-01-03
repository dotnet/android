using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaVMTest
	{
		[Test]
		public void CreateJavaVM ()
		{
			using (var vm = CreateVM ()) {
				Assert.IsNotNull (vm.SafeHandle);
				Assert.IsNotNull (JniEnvironment.Current);
			}
		}

		[Test, ExpectedException (typeof (InvalidOperationException))]
		public void JavaVM_Current_Throws_InvalidOperationException ()
		{
			var vm = JavaVM.Current;
			GC.KeepAlive (vm);
		}

		static JavaVM CreateVM (bool trackIds = false)
		{
			return new JavaVMBuilder () {
				TrackIDs = trackIds,
			}.CreateJavaVM ();
		}
	}
}

