using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using Mono.Cecil;

using NUnit.Framework;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers;

using Android.App;
using Android.Runtime;
using Android.Content;

namespace Xamarin.Android.ToolsTests
{
	[TestFixture]
	public class TypeNameMapGeneratorTests
	{
		[Test]
		public void ConstructorExceptions ()
		{
			Action<string, object []> logger = (f, o) => { };
			Action<string, object []> nullLogger = null;
			Action<TraceLevel, string> levelLogger = (l, v) => { };
			Action<TraceLevel, string> nullLevelLogger = null;
#pragma warning disable 0618
			Assert.Throws<ArgumentNullException> (() => new TypeNameMapGenerator ((string []) null, levelLogger));
			Assert.Throws<ArgumentNullException> (() => new TypeNameMapGenerator ((TypeDefinition []) null, levelLogger));
			Assert.Throws<ArgumentNullException> (() => new TypeNameMapGenerator (new string [0], nullLevelLogger));
			Assert.Throws<ArgumentNullException> (() => new TypeNameMapGenerator (new TypeDefinition [0], nullLevelLogger));
			Assert.Throws<ArgumentNullException> (() => new TypeNameMapGenerator ((string []) null, logger));
			Assert.Throws<ArgumentNullException> (() => new TypeNameMapGenerator ((TypeDefinition []) null, logger));
			Assert.Throws<ArgumentNullException> (() => new TypeNameMapGenerator (new string [0], nullLogger));
			Assert.Throws<ArgumentNullException> (() => new TypeNameMapGenerator (new TypeDefinition [0], nullLogger));
#pragma warning restore 0618
		}

		[Test]
		public void WriteJavaToManaged ()
		{
			var types = SupportDeclarations.GetTestTypeDefinitions ();
			var v = new TypeNameMapGenerator (types, logger: Diagnostic.CreateConsoleLogger (), cache: null);
			var o = new MemoryStream ();
			v.WriteJavaToManaged (o);
			var a = ToArray (o);
			Save (a, "__j2m");
			var length = 193;
			var offset = 76;
			var e =
				"version=1\u0000" +
				$"entry-count={types.Count - 1}\u0000" +
				"entry-len=" + length + "\u0000" +
				"value-offset=" + offset + "\u0000" +
				GetJ2MEntryLine (typeof (ActivityName),                             "activity/Name",                                                                                offset, length) +
				GetJ2MEntryLine (typeof (ApplicationName),                          "application/Name",                                                                             offset, length) +
				GetJ2MEntryLine (typeof (ApplicationName.ActivityLifecycleCallbacks),   "application/Name_ActivityLifecycleCallbacks",                                              offset, length) +
				GetJ2MEntryLine (typeof (DefaultName),                              "crc64197ae30a36756915/DefaultName",                                                            offset, length) +
				GetJ2MEntryLine (typeof (DefaultName.A),                            "crc64197ae30a36756915/DefaultName_A",                                                          offset, length) +
				GetJ2MEntryLine (typeof (DefaultName.A.B),                          "crc64197ae30a36756915/DefaultName_A_B",                                                        offset, length) +
				GetJ2MEntryLine (typeof (DefaultName.C.D),                          "crc64197ae30a36756915/DefaultName_C_D",                                                        offset, length) +
				GetJ2MEntryLine (typeof (ExampleOuterClass),                        "crc64197ae30a36756915/ExampleOuterClass",                                                      offset, length) +
				GetJ2MEntryLine (typeof (ExampleOuterClass.ExampleInnerClass),      "crc64197ae30a36756915/ExampleOuterClass$ExampleOuterClass_ExampleInnerClass",                  offset, length) +
				GetJ2MEntryLine (typeof (InstrumentationName),                      "instrumentation/Name",                                                                         offset, length) +
				GetJ2MEntryLine (typeof (AbstractClass),                            "my/AbstractClass",                                                                             offset, length) +
				GetJ2MEntryLine (typeof (ExampleActivity),                          "my/ExampleActivity",                                                                           offset, length) +
				GetJ2MEntryLine (typeof (ExampleInstrumentation),                   "my/ExampleInstrumentation",                                                                    offset, length) +
				GetJ2MEntryLine (typeof (ProviderName),                             "provider/Name",                                                                                offset, length) +
				GetJ2MEntryLine (typeof (ReceiverName),                             "receiver/Name",                                                                                offset, length) +
				GetJ2MEntryLine (typeof (RegisterName),                             "register/Name",                                                                                offset, length) +
				GetJ2MEntryLine (typeof (RegisterName.OverrideNestedName),          "register/Name$Override",                                                                       offset, length) +
				GetJ2MEntryLine (typeof (RegisterName.DefaultNestedName),           "register/Name_DefaultNestedName",                                                              offset, length) +
				GetJ2MEntryLine (typeof (NonStaticOuterClass),                      "register/NonStaticOuterClass",                                                                 offset, length) +
				GetJ2MEntryLine (typeof (NonStaticOuterClass.NonStaticInnerClass),  "register/NonStaticOuterClass$NonStaticInnerClass",                                             offset, length) +
				GetJ2MEntryLine (typeof (ServiceName),                              "service/Name",                                                                                 offset, length) +
				"\u0000";
			var ex = Encoding.UTF8.GetBytes (e);
			Save (ex, "__j2m.ex");
			CollectionAssert.AreEqual (ex, a);
		}

		static void Save (byte [] data, string path)
		{
			var dir = Path.GetDirectoryName (typeof (TypeNameMapGeneratorTests).Assembly.Location);
			using (var o = File.OpenWrite (Path.Combine (dir, path))) {
				o.Write (data, 0, data.Length);
			}
		}

		static byte [] ToArray (MemoryStream stream)
		{
			stream.Position = 0;
			var r = new byte [stream.Length];
			Array.Copy (stream.GetBuffer (), r, r.Length);
			return r;
		}

		static string GetJ2MEntryLine (Type type, string jniName, int offset, int length)
		{
			return GetEntryPart (jniName, offset) + GetEntryPart (GetTypeName (type), length - offset);
		}

		static string GetEntryPart (string value, int length)
		{
			return value + new string ('\u0000', length - value.Length);
		}

		[Test]
		public void WriteManagedToJavaWithNoTypes ()
		{
			var v = new TypeNameMapGenerator (new string[0], logger: Diagnostic.CreateConsoleLogger (), cache: null);
			var o = new MemoryStream ();
			v.WriteManagedToJava (o);
			var a = ToArray (o);
			Assert.AreEqual (52, a.Length);
		}

		[Test]
		public void WriteManagedToJava ()
		{
			var types = SupportDeclarations.GetTestTypeDefinitions ();
			var v = new TypeNameMapGenerator (types, logger: Diagnostic.CreateConsoleLogger (), cache: null);
			var o = new MemoryStream ();
			v.WriteManagedToJava (o);
			var a = ToArray (o);
			Save (a, "__m2j");
			var length = 193;
			var offset = 117;
			var e =
				"version=1\u0000" +
				$"entry-count={types.Count}\u0000" +
				"entry-len=" + length + "\u0000" +
				"value-offset=" + offset + "\u0000" +
				GetM2JEntryLine (typeof (AbstractClass),                            "my/AbstractClass",                                                                             offset, length) +
				GetM2JEntryLine (typeof (AbstractClassInvoker),                     "my/AbstractClass",                                                                             offset, length) +
				GetM2JEntryLine (typeof (ActivityName),                             "activity/Name",                                                                                offset, length) +
				GetM2JEntryLine (typeof (ApplicationName.ActivityLifecycleCallbacks),   "application/Name_ActivityLifecycleCallbacks",                                              offset, length) +
				GetM2JEntryLine (typeof (ApplicationName),                          "application/Name",                                                                             offset, length) +
				GetM2JEntryLine (typeof (DefaultName.A.B),                          "crc64197ae30a36756915/DefaultName_A_B",                                                        offset, length) +
				GetM2JEntryLine (typeof (DefaultName.A),                            "crc64197ae30a36756915/DefaultName_A",                                                          offset, length) +
				GetM2JEntryLine (typeof (DefaultName.C.D),                          "crc64197ae30a36756915/DefaultName_C_D",                                                        offset, length) +
				GetM2JEntryLine (typeof (DefaultName),                              "crc64197ae30a36756915/DefaultName",                                                            offset, length) +
				GetM2JEntryLine (typeof (ExampleActivity),                          "my/ExampleActivity",                                                                           offset, length) +
				GetM2JEntryLine (typeof (ExampleInstrumentation),                   "my/ExampleInstrumentation",                                                                    offset, length) +
				GetM2JEntryLine (typeof (ExampleOuterClass.ExampleInnerClass),      "crc64197ae30a36756915/ExampleOuterClass$ExampleOuterClass_ExampleInnerClass",                  offset, length) +
				GetM2JEntryLine (typeof (ExampleOuterClass),                        "crc64197ae30a36756915/ExampleOuterClass",                                                      offset, length) +
				GetM2JEntryLine (typeof (InstrumentationName),                      "instrumentation/Name",                                                                         offset, length) +
				GetM2JEntryLine (typeof (NonStaticOuterClass.NonStaticInnerClass),  "register/NonStaticOuterClass$NonStaticInnerClass",                                             offset, length) +
				GetM2JEntryLine (typeof (NonStaticOuterClass),                      "register/NonStaticOuterClass",                                                                 offset, length) +
				GetM2JEntryLine (typeof (ProviderName),                             "provider/Name",                                                                                offset, length) +
				GetM2JEntryLine (typeof (ReceiverName),                             "receiver/Name",                                                                                offset, length) +
				GetM2JEntryLine (typeof (RegisterName.DefaultNestedName),           "register/Name_DefaultNestedName",                                                              offset, length) +
				GetM2JEntryLine (typeof (RegisterName.OverrideNestedName),          "register/Name$Override",                                                                       offset, length) +
				GetM2JEntryLine (typeof (RegisterName),                             "register/Name",                                                                                offset, length) +
				GetM2JEntryLine (typeof (ServiceName),                              "service/Name",                                                                                 offset, length) +
				"\u0000";
			var ex = Encoding.UTF8.GetBytes (e);
			Save (ex, "__m2j.ex");
			CollectionAssert.AreEqual (ex, a);
		}

		static string GetM2JEntryLine (Type type, string jniName, int offset, int length)
		{
			return GetEntryPart (GetTypeName (type), offset) + GetEntryPart (jniName, length - offset);
		}

		static string GetTypeName (Type type)
		{
			return type.FullName + ", " + type.Assembly.GetName ().Name;
		}
	}
}

