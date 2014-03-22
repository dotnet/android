using System;
using System.Reflection;

using Java.Interop;

using Mono.Linq.Expressions;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	public class JniMarshalMethodTests {

		[Test]
		public void CreateMarshalMethodExpression_NullChecks ()
		{
			Assert.Throws<ArgumentNullException> (() => JniMarshalMethod.CreateMarshalMethodExpression (null));
		}

		[Test]
		public void CreateMarshalMethodExpression ()
		{
			var t = typeof (Action<IntPtr, IntPtr>);
			var m = typeof (TestType).GetMethod ("MethodThrowsHandler", BindingFlags.NonPublic | BindingFlags.Static);
			var d = Delegate.CreateDelegate (t, m);
			CheckCreateMarshalMethodExpression (d, t,
					@"void (IntPtr arg1, IntPtr arg2)
{
	JniEnvironment.CheckCurrent(arg1);
	try
	{
		TestType.MethodThrowsHandler(arg1, arg2);
	}
	catch (Exception __e)
	{
		Errors.Throw(__e);
	}
}");
		}

		static void CheckCreateMarshalMethodExpression (Delegate value, Type expectedDelegateType, string expectedBody)
		{
			var l   = JniMarshalMethod.CreateMarshalMethodExpression (value);
			Console.WriteLine ("## method: {0}", value.Method.Name);
			Console.WriteLine (l.ToCSharpCode ());
			var da = AppDomain.CurrentDomain.DefineDynamicAssembly(
				new AssemblyName ("dyn"), // call it whatever you want
				System.Reflection.Emit.AssemblyBuilderAccess.Save);

			var _name = "dyn-" + value.Method.Name + ".dll";
			var dm = da.DefineDynamicModule("dyn_mod", _name);
			var dt = dm.DefineType ("dyn_type", TypeAttributes.Public);
			var mb = dt.DefineMethod(
				value.Method.Name,
				MethodAttributes.Public | MethodAttributes.Static);

			l.CompileToMethod(mb);
			dt.CreateType();
			da.Save(_name);
			Assert.AreEqual (expectedDelegateType, l.Type);
			Assert.AreEqual (expectedBody, l.ToCSharpCode ());
		}
	}
}

