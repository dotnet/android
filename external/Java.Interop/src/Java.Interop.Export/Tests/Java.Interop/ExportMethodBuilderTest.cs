using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using Java.Interop;

using Mono.Linq.Expressions;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	class ExportMethodBuilderTest : JVM
	{
		[Test]
		public void AddExportMethods ()
		{
			using (var t = CreateExportTestType ()) {
				var methods = new List<JniNativeMethodRegistration> ();
				ExportMethodBuilder.AddExportMethods (typeof (ExportTest), methods);
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
		public void AddExportMethods_NullChecks ()
		{
			Assert.Throws<ArgumentNullException> (() => ExportMethodBuilder.AddExportMethods (null, new List<JniNativeMethodRegistration> ()));
			Assert.Throws<ArgumentNullException> (() => ExportMethodBuilder.AddExportMethods (typeof (object), null));
		}

		[Test]
		public void CreateNativeMethodRegistration_NullChecks ()
		{
			Action a = ExportTest.StaticAction;
			Assert.Throws<ArgumentNullException> (() => ExportMethodBuilder.CreateNativeMethodRegistration (null, typeof (ExportTest), a.Method));
			Assert.Throws<ArgumentNullException> (() => ExportMethodBuilder.CreateNativeMethodRegistration (new ExportAttribute (null), null, a.Method));
			Assert.Throws<ArgumentNullException> (() => ExportMethodBuilder.CreateNativeMethodRegistration (new ExportAttribute (null), typeof (ExportTest), null));
		}

		[Test]
		public void CreateInvocationExpression_NullChecks ()
		{
			Action a = ExportTest.StaticAction;
			Assert.Throws<ArgumentNullException> (() => ExportMethodBuilder.CreateInvocationExpression (null, typeof (ExportTest), a.Method));
			Assert.Throws<ArgumentNullException> (() => ExportMethodBuilder.CreateInvocationExpression (new ExportAttribute (null), null, a.Method));
			Assert.Throws<ArgumentNullException> (() => ExportMethodBuilder.CreateInvocationExpression (new ExportAttribute (null), typeof (ExportTest), null));
		}

		[Test]
		public void CreateInvocationExpression_InstanceAction ()
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
			var l   = ExportMethodBuilder.CreateInvocationExpression (export, type, method);
			Assert.AreEqual (expectedDelegateType, l.Type);
			Assert.AreEqual (expectedBody, l.ToCSharpCode ());
		}

		[Test]
		public void CreateInvocationExpression_StaticAction ()
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

