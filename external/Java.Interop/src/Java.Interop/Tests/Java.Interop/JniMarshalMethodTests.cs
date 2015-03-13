using System;
using System.Reflection;

using Java.Interop;

#if !__ANDROID__
using Mono.Linq.Expressions;
#endif  // !__ANDROID__

using NUnit.Framework;

namespace Java.InteropTests {

#if !__ANDROID__
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
	JniEnvironment __envp;

	__envp = new JniEnvironment(arg1);
	try
	{
		TestType.MethodThrowsHandler(arg1, arg2);
	}
	catch (Exception __e)
	{
		__envp.SetPendingException(__e);
	}
	finally
	{
		__envp.Dispose();
	}
}");
		}

		[Test]
		public void CreateMarshalMethodExpression_GetStringValueHandler ()
		{
			var t = typeof (Func<IntPtr, IntPtr, int, IntPtr>);
			var m = typeof (TestType).GetMethod ("GetStringValueHandler", BindingFlags.NonPublic | BindingFlags.Static);
			var d = Delegate.CreateDelegate (t, m);
			CheckCreateMarshalMethodExpression (d, t,
					@"IntPtr (IntPtr arg1, IntPtr arg2, int arg3)
{
	JniEnvironment __envp;

	__envp = new JniEnvironment(arg1);
	try
	{
		return TestType.GetStringValueHandler(arg1, arg2, arg3);
	}
	catch (Exception __e)
	{
		__envp.SetPendingException(__e);
		return default(IntPtr);
	}
	finally
	{
		__envp.Dispose();
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
#endif  // !__ANDROID__
}

