using System;

using Android.Runtime;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	internal static class TrimmableTypeMapTestHelpers
	{
		public static IntPtr GetRequiredMethodID (IntPtr classHandle, string name, string signature)
		{
			var method = JNIEnv.GetMethodID (classHandle, name, signature);
			Assert.AreNotEqual (IntPtr.Zero, method, $"JNI method '{name}{signature}' was not found.");
			return method;
		}
	}
}
