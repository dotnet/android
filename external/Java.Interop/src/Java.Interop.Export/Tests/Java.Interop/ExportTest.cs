using System;

using Java.Interop;

namespace Java.InteropTests
{
	[JniTypeInfo ("com/xamarin/interop/export/ExportType")]
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

		public static bool StaticActionInt32StringCalled;

		[Export ("staticActionInt32String", Signature = "(ILjava/lang/String;)V")]
		public static void StaticActionInt32String (int i, string v)
		{
			StaticActionInt32StringCalled = i == 1 && v == "2";
		}

		[Export ("funcInt64", Signature = "()J")]
		public long FuncInt64 ()
		{
			return 42;
		}

		[Export ("funcIJavaObject", Signature = "()Ljava/lang/Object;")]
		public JavaObject FuncIJavaObject ()
		{
			return this;
		}
	}
}

