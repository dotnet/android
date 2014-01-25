using System;

using Java.Interop;

namespace Java.InteropTests
{
	public class ExportTest : JavaObject
	{
		public ExportTest (JniReferenceSafeHandle handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		public bool HelloCalled;

		[Export ("action", Signature="()V")]
		public void InstanceAction ()
		{
			HelloCalled = true;
		}

		public static bool StaticHelloCalled;

		[Export ("staticAction", Signature="()V")]
		public static void StaticAction ()
		{
			StaticHelloCalled = true;
		}
	}
}

