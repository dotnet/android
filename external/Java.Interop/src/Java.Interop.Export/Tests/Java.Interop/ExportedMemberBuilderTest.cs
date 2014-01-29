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
				Assert.AreEqual (2, methods.Count);

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
	JniEnvironment.CheckCurrent(__jnienv);
	JniEnvironment.Current.JavaVM.GetObject<ExportTest>(__context).InstanceAction();
}");
		}

		static void CheckCreateInvocationExpression (ExportAttribute export, Type type, MethodInfo method, Type expectedDelegateType, string expectedBody)
		{
			export  = export ?? new ExportAttribute ();
			var b   = new ExportedMemberBuilder ();
			var l   = b.CreateMarshalFromJniMethodExpression (export, type, method);
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
	JniEnvironment.CheckCurrent(__jnienv);
	ExportTest.StaticAction();
}");
		}
	}
}

