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
	class ExportedMemberBuilderTest : JVM
	{
		[Test]
		public void AddExportMethods ()
		{
			using (var t = CreateExportTestType ()) {
				var methods = new ExportedMemberBuilder ()
					.GetExportedMemberRegistrations (typeof (ExportTest))
					.ToList ();
				Assert.AreEqual (4, methods.Count);

				Assert.AreEqual ("action",  methods [0].Name);
				Assert.AreEqual ("()V",     methods [0].Signature);
				Assert.IsTrue (methods [0].Marshaler is Action<IntPtr, IntPtr>);

				Assert.AreEqual ("staticAction",    methods [1].Name);
				Assert.AreEqual ("()V",             methods [1].Signature);
				Assert.IsTrue (methods [1].Marshaler is Action<IntPtr, IntPtr>);

				t.RegisterNativeMethods (methods.ToArray ());

				t.GetStaticMethod ("staticAction", "()V").CallVoidMethod (t.SafeHandle);
				Assert.IsTrue (ExportTest.StaticHelloCalled);

				using (var o = CreateExportTest (t)) {
					t.GetInstanceMethod ("action", "()V").CallVirtualVoidMethod (o.SafeHandle);
					Assert.IsTrue (o.HelloCalled);
					o.Dispose ();
				}
			}
		}

		static JniType CreateExportTestType ()
		{
			return new JniType ("com/xamarin/interop/export/ExportType");
		}

		static ExportTest CreateExportTest (JniType type)
		{
			var c = type.GetConstructor ("()V");
			return new ExportTest (type.NewObject (c), JniHandleOwnership.Transfer);
		}

		[Test]
		public void GetExportedMemberRegistrations_NullChecks ()
		{
			var builder = new ExportedMemberBuilder ();
			Assert.Throws<ArgumentNullException> (() => builder.GetExportedMemberRegistrations (null));
		}

		[Test]
		public void CreateMarshalFromJniMethodRegistration_NullChecks ()
		{
			Action a    = ExportTest.StaticAction;
			var builder = new ExportedMemberBuilder ();
			Assert.Throws<ArgumentNullException> (() => builder.CreateMarshalFromJniMethodRegistration (null, typeof (ExportTest), a.Method));
			Assert.Throws<ArgumentNullException> (() => builder.CreateMarshalFromJniMethodRegistration (new ExportAttribute (null), null, a.Method));
			Assert.Throws<ArgumentNullException> (() => builder.CreateMarshalFromJniMethodRegistration (new ExportAttribute (null), typeof (ExportTest), null));
		}

		[Test]
		public void CreateInvocationExpression_NullChecks ()
		{
			Action      a = ExportTest.StaticAction;
			var builder = new ExportedMemberBuilder ();
			Assert.Throws<ArgumentNullException> (() => builder.CreateMarshalFromJniMethodExpression (null, typeof (ExportTest), a.Method));
			Assert.Throws<ArgumentNullException> (() => builder.CreateMarshalFromJniMethodExpression (new ExportAttribute (null), null, a.Method));
			Assert.Throws<ArgumentNullException> (() => builder.CreateMarshalFromJniMethodExpression (new ExportAttribute (null), typeof (ExportTest), null));
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_InstanceAction ()
		{
			var t = typeof (ExportTest);
			var m = t.GetMethod ("InstanceAction");
			CheckCreateInvocationExpression (null, t, m, typeof (Action<IntPtr, IntPtr>),
					@"void (IntPtr __jnienv, IntPtr __context)
{
	JavaVM __jvm;
	ExportTest __this;

	JniEnvironment.CheckCurrent(__jnienv);
	__jvm = JniEnvironment.Current.JavaVM;
	__this = __jvm.GetObject<ExportTest>(__context);
	__this.InstanceAction();
}");
		}

		static void CheckCreateInvocationExpression (ExportAttribute export, Type type, MethodInfo method, Type expectedDelegateType, string expectedBody)
		{
			export  = export ?? new ExportAttribute ();
			var b   = new ExportedMemberBuilder ();
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
			da.Save(_name);
			Assert.AreEqual (expectedDelegateType, l.Type);
			Assert.AreEqual (expectedBody, l.ToCSharpCode ());
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_StaticAction ()
		{
			var t       = typeof (ExportTest);
			Action a    = ExportTest.StaticAction;
			CheckCreateInvocationExpression (null, t, a.Method, typeof(Action<IntPtr, IntPtr>),
					@"void (IntPtr __jnienv, IntPtr __context)
{
	JavaVM __jvm;

	JniEnvironment.CheckCurrent(__jnienv);
	__jvm = JniEnvironment.Current.JavaVM;
	ExportTest.StaticAction();
}");
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_ActionInt32String ()
		{
			var t = typeof (ExportTest);
			var m = t.GetMethod ("ActionInt32String");
			var e = new ExportAttribute () {
				Signature = "(ILjava/lang/String;)V",
			};
			CheckCreateInvocationExpression (e, t, m, typeof (Action<IntPtr, IntPtr, int, IntPtr>),
					@"void (IntPtr __jnienv, IntPtr __context, int i, IntPtr native_v)
{
	JavaVM __jvm;
	ExportTest __this;
	string v;

	JniEnvironment.CheckCurrent(__jnienv);
	__jvm = JniEnvironment.Current.JavaVM;
	__this = __jvm.GetObject<ExportTest>(__context);
	v = Strings.ToString(native_v);
	__this.ActionInt32String(i, v);
}");
		}

		[Test]
		public void CreateMarshalFromJniMethodExpression_FuncInt64 ()
		{
			var t = typeof (ExportTest);
			var m = t.GetMethod ("FuncInt64");
			var e = new ExportAttribute () {
				Signature = "()J",
			};
			CheckCreateInvocationExpression (e, t, m, typeof (Func<IntPtr, IntPtr, long>),
					@"long (IntPtr __jnienv, IntPtr __context)
{
	JavaVM __jvm;
	ExportTest __this;
	long __ret;

	JniEnvironment.CheckCurrent(__jnienv);
	__jvm = JniEnvironment.Current.JavaVM;
	__this = __jvm.GetObject<ExportTest>(__context);
	__ret = __this.FuncInt64();
	return __ret;
}");
		}
	}
}

