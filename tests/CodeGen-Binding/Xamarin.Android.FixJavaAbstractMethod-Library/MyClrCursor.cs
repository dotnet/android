using System;
using Test.Bindings;

namespace Library
{
	public class MyClrCursor : Java.Lang.Object, ICursor
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
	}
}

