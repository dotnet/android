using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using Java.Interop;

using Mono.Linq.Expressions;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	class ExportedMemberBuilderTest : JavaVMFixture
	{
		[Test]
		public void AddExportMethods ()
		{
			using (var t = CreateExportTestType ()) {
				var methods = CreateBuilder ()
					.GetExportedMemberRegistrations (typeof (ExportTest))
					.ToList ();
				Assert.AreEqual (5, methods.Count);

				Assert.AreEqual ("action",  methods [0].Name);
				Assert.AreEqual ("()V",     methods [0].Signature);
				Assert.IsTrue (methods [0].Marshaler is Action<IntPtr, IntPtr>);

				Assert.AreEqual ("staticAction",    methods [1].Name);
				Assert.AreEqual ("()V",             methods [1].Signature);
				Assert.IsTrue (methods [1].Marshaler is Action<IntPtr, IntPtr>);

				t.RegisterNativeMethods (methods.ToArray ());

				t.GetStaticMethod ("testStaticMethods", "()V").CallVoidMethod (t.PeerReference);
				Assert.IsTrue (ExportTest.StaticHelloCalled);
				Assert.IsTrue (ExportTest.StaticActionInt32StringCalled);

				using (var o = CreateExportTest (t)) {
					t.GetInstanceMethod ("testMethods", "()V").InvokeVirtualVoidMethod (o.PeerReference);
					Assert.IsTrue (o.HelloCalled);
					o.Dispose ();
				}
			}
		}

		static ExportedMemberBuilder CreateBuilder ()
		{
			return new ExportedMemberBuilder (JniRuntime.Current);
		}

		static JniType CreateExportTestType ()
		{
			return new JniType ("com/xamarin/interop/export/ExportType");
		}

		static unsafe ExportTest CreateExportTest (JniType type)
		{
			var c = type.GetConstructor ("()V");
			var p = type.NewObject (c, null);
			return new ExportTest (ref p, JniObjectReferenceOptions.DisposeSourceReference);
		}

		[Test]
		public void GetExportedMemberRegistrations_NullChecks ()
		{
			var builder = CreateBuilder ();
			Assert.Throws<ArgumentNullException> (() => builder.GetExportedMemberRegistrations (null));
		}

		[Test]
		public void GetJniMethodSignature_NullChecks ()
		{
			var builder = CreateBuilder ();
			Action a    = () => {};
			Assert.Throws<ArgumentNullException> (() => builder.GetJniMethodSignature (null, a.Method));
			Assert.Throws<ArgumentNullException> (() => builder.GetJniMethodSignature (new JavaCallableAttribute (), null));
		}

		[Test]
		public void GetJniMethodSignature ()
		{
			var builder = CreateBuilder ();
			Action a    = () => {};
			var export  = new JavaCallableAttribute () {
				Signature = "(I)V",
			};
			// Note: no validation between actual MethodInfo & existing signature
			// Validation would be done by CreateMarshalFromJniMethodRegistration().
			Assert.AreEqual ("(I)V", builder.GetJniMethodSignature (export, a.Method));
			Assert.AreEqual ("(I)V", export.Signature);

			export = new JavaCallableAttribute () {
				Signature = null,
			};
			Assert.AreEqual ("()V", builder.GetJniMethodSignature (export, a.Method));
			// Note: export.Signature updated
			Assert.AreEqual ("()V", export.Signature);

			Action<string> s    = v => {};
			Assert.AreEqual ("(Ljava/lang/String;)V", builder.GetJniMethodSignature (new JavaCallableAttribute (), s.Method));

			Func<string> fs     = () => null;
			Assert.AreEqual ("()Ljava/lang/String;", builder.GetJniMethodSignature (new JavaCallableAttribute (), fs.Method));

			// Note: AppDomain currently has no builtin marshaling defaults
			// TODO: but should it? We could default wrap to JavaProxyObject...?
			Action<AppDomain> aad    = v => {};
			Assert.Throws<NotSupportedException> (() => builder.GetJniMethodSignature (new JavaCallableAttribute (), aad.Method));

			Func<AppDomain> fad    = () => null;
			Assert.Throws<NotSupportedException> (() => builder.GetJniMethodSignature (new JavaCallableAttribute (), fad.Method));
		}

		[Test]
		public void CreateMarshalFromJniMethodRegistration_NullChecks ()
		{
			Action a    = ExportTest.StaticAction;
			var builder = CreateBuilder ();
			Assert.Throws<ArgumentNullException> (() => builder.CreateMarshalFromJniMethodRegistration (null, typeof (ExportTest), a.Method));
			Assert.Throws<ArgumentNullException> (() => builder.CreateMarshalFromJniMethodRegistration (new JavaCallableAttribute (null), null, a.Method));
			Assert.Throws<ArgumentNullException> (() => builder.CreateMarshalFromJniMethodRegistration (new JavaCallableAttribute (null), typeof (ExportTest), null));
		}

		[Test]
		public void CreateInvocationExpression_NullChecks ()
		{
			Action    a = ExportTest.StaticAction;
			var builder = CreateBuilder ();
			Assert.Throws<ArgumentNullException> (() => builder.CreateMarshalFromJniMethodExpression (null, typeof (ExportTest), a.Method));
			Assert.Throws<ArgumentNullException> (() => builder.CreateMarshalFromJniMethodExpression (new JavaCallableAttribute (null), null, a.Method));
			Assert.Throws<ArgumentNullException> (() => builder.CreateMarshalFromJniMethodExpression (new JavaCallableAttribute (null), typeof (ExportTest), null));
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_SignatureMismatch ()
		{
			Action<int, string> a   = ExportTest.StaticActionInt32String;
			var builder             = CreateBuilder ();

			// Parameter count mismatch: 0 != 2
			Assert.Throws<ArgumentException>(() => builder.CreateMarshalFromJniMethodExpression (
					new JavaCallableAttribute () { Signature = "()V" }, a.Method.DeclaringType, a.Method));
			// Parameter count mismatch: 1 != 2
			Assert.Throws<ArgumentException>(() => builder.CreateMarshalFromJniMethodExpression (
					new JavaCallableAttribute () { Signature = "(I)V" }, a.Method.DeclaringType, a.Method));
			// Parameter type mismatch: (int, int) != (int, string)
			Assert.Throws<ArgumentException>(() => builder.CreateMarshalFromJniMethodExpression (
					new JavaCallableAttribute () { Signature = "(II)V" }, a.Method.DeclaringType, a.Method));
			// return type mismatch: int != void
			Assert.Throws<ArgumentException>(() => builder.CreateMarshalFromJniMethodExpression (
					new JavaCallableAttribute () { Signature = "(ILjava/lang/String;)I" }, a.Method.DeclaringType, a.Method));
			// invalid JNI signatures
			Assert.Throws<ArgumentException>(() => builder.CreateMarshalFromJniMethodExpression (
					new JavaCallableAttribute () { Signature = "(IL)I" }, a.Method.DeclaringType, a.Method));
			Assert.Throws<ArgumentException>(() => builder.CreateMarshalFromJniMethodExpression (
					new JavaCallableAttribute () { Signature = "(I[)I" }, a.Method.DeclaringType, a.Method));
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_InstanceAction ()
		{
			var t = typeof (ExportTest);
			var m = t.GetMethod ("InstanceAction");
			CheckCreateInvocationExpression (null, t, m, typeof (Action<IntPtr, IntPtr>),
					@"void (IntPtr __jnienv, IntPtr __context)
{
	JniTransition __envp;

	__envp = new JniTransition(__jnienv);
	try
	{
		JniRuntime __jvm;
		ExportTest __this;

		__jvm = JniEnvironment.Runtime;
		__this = __jvm.GetObject<ExportTest>(__context);
		__this.InstanceAction();
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

		static void CheckCreateInvocationExpression (JavaCallableAttribute export, Type type, MethodInfo method, Type expectedDelegateType, string expectedBody)
		{
			export  = export ?? new JavaCallableAttribute ();
			var b   = CreateBuilder ();
			var l   = b.CreateMarshalFromJniMethodExpression (export, type, method);
			Console.WriteLine ("## method: {0}", method.Name);
			Console.WriteLine (l.ToCSharpCode ());
			var da = AppDomain.CurrentDomain.DefineDynamicAssembly(
				new AssemblyName("dyn"), // call it whatever you want
				System.Reflection.Emit.AssemblyBuilderAccess.Save);

			var _name = "dyn-" + method.Name + ".dll";
			var dm = da.DefineDynamicModule("dyn_mod", _name);
			var dt = dm.DefineType ("dyn_type", TypeAttributes.Public);
			var mb = dt.DefineMethod(
				method.Name,
				MethodAttributes.Public | MethodAttributes.Static);

			l.CompileToMethod(mb);
			dt.CreateType();
			Assert.AreEqual (expectedDelegateType, l.Type);
			Assert.AreEqual (expectedBody, l.ToCSharpCode ());
#if !__ANDROID__
			da.Save (_name);
#endif  // !__ANDROID__
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_StaticAction ()
		{
			var t       = typeof (ExportTest);
			Action a    = ExportTest.StaticAction;
			CheckCreateInvocationExpression (null, t, a.Method, typeof(Action<IntPtr, IntPtr>),
					@"void (IntPtr __jnienv, IntPtr __context)
{
	JniTransition __envp;

	__envp = new JniTransition(__jnienv);
	try
	{
		JniRuntime __jvm;

		__jvm = JniEnvironment.Runtime;
		ExportTest.StaticAction();
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
		public void CreateMarshalFromJniMethodExpression_StaticActionInt32String ()
		{
			var t = typeof (ExportTest);
			var m = ((Action<int, string>) ExportTest.StaticActionInt32String);
			var e = new JavaCallableAttribute () {
				Signature = "(ILjava/lang/String;)V",
			};
			CheckCreateInvocationExpression (e, t, m.Method, typeof (Action<IntPtr, IntPtr, int, IntPtr>),
					@"void (IntPtr __jnienv, IntPtr __context, int i, IntPtr native_v)
{
	JniTransition __envp;

	__envp = new JniTransition(__jnienv);
	try
	{
		JniRuntime __jvm;
		string v;

		__jvm = JniEnvironment.Runtime;
		v = Strings.ToString(native_v);
		ExportTest.StaticActionInt32String(i, v);
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
		public void CreateMarshalFromJniMethodExpression_FuncInt64 ()
		{
			var t = typeof (ExportTest);
			var m = t.GetMethod ("FuncInt64");
			var e = new JavaCallableAttribute () {
				Signature = "()J",
			};
			CheckCreateInvocationExpression (e, t, m, typeof (Func<IntPtr, IntPtr, long>),
					@"long (IntPtr __jnienv, IntPtr __context)
{
	JniTransition __envp;
	long __jret;

	__envp = new JniTransition(__jnienv);
	try
	{
		JniRuntime __jvm;
		ExportTest __this;
		long __mret;

		__jvm = JniEnvironment.Runtime;
		__this = __jvm.GetObject<ExportTest>(__context);
		__mret = __this.FuncInt64();
		__jret = __mret;
		return __jret;
	}
	catch (Exception __e)
	{
		__envp.SetPendingException(__e);
		return default(long);
	}
	finally
	{
		__envp.Dispose();
	}
}");
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_FuncIJavaObject ()
		{
			var t = typeof (ExportTest);
			var m = t.GetMethod ("FuncIJavaObject");
			var e = new JavaCallableAttribute () {
				Signature = "()Ljava/lang/Object;",
			};
			CheckCreateInvocationExpression (e, t, m, typeof (Func<IntPtr, IntPtr, IntPtr>),
					@"IntPtr (IntPtr __jnienv, IntPtr __context)
{
	JniTransition __envp;
	IntPtr __jret;

	__envp = new JniTransition(__jnienv);
	try
	{
		JniRuntime __jvm;
		ExportTest __this;
		JavaObject __mret;

		__jvm = JniEnvironment.Runtime;
		__this = __jvm.GetObject<ExportTest>(__context);
		__mret = __this.FuncIJavaObject();
		__jret = References.NewReturnToJniRef(__mret);
		return __jret;
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
	}
}
