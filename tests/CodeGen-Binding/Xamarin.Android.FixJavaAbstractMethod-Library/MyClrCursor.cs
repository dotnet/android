using System;
using Test.Bindings;

namespace Library
{
	public class MyClrCursor : Java.Lang.Object, global::Test.Bindings.ICursor
	{
		public void Method ()
		{
		}

		public int MethodWithParams (int p0, string p1)
		{
			return 1;
		}

		public int MethodWithParams (int p0, string p1, float p2)
		{
			return 2;
		}

		public int MethodWithRT ()
		{
			return 3;
		}

		int global::Test.Bindings.ICursor.MethodWithCursor (global::Test.Bindings.ICursor cursor)
		{
			var a = 2;
			var b = 2;
			return a + b;
		}
	}
}

