using System;

using Java.Interop;

namespace Java.InteropTests
{
	[JniTypeSignature ("com/xamarin/interop/export/ExportType")]
	public class ExportTest : JavaObject
	{
		public ExportTest (ref JniObjectReference reference, JniObjectReferenceOptions transfer)
			: base (ref reference, transfer)
		{
		}

		public bool HelloCalled;

		[JavaCallable ("action", Signature="()V")]
		public void InstanceAction ()
		{
			HelloCalled = true;
		}

		public static bool StaticHelloCalled;

		[JavaCallable ("staticAction", Signature="()V")]
		public static void StaticAction ()
		{
			StaticHelloCalled = true;
		}

		public static bool StaticActionInt32StringCalled;

		[JavaCallable ("staticActionInt32String", Signature = "(ILjava/lang/String;)V")]
		public static void StaticActionInt32String (int i, string v)
		{
			StaticActionInt32StringCalled = i == 1 && v == "2";
		}

		[JavaCallable ("funcInt64", Signature = "()J")]
		public long FuncInt64 ()
		{
			return 42;
		}

		[JavaCallable ("funcIJavaObject", Signature = "()Ljava/lang/Object;")]
		public JavaObject FuncIJavaObject ()
		{
			return this;
		}
	}
}

