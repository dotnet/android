using System;
using System.Collections.Generic;
using System.Linq;

using Mono.Cecil;

using Java.Interop;
using Java.Interop.Tools.Cecil;

using Android.App;
using Android.Content;
using Android.Runtime;

using RegisterAttribute = Android.Runtime.RegisterAttribute;

namespace Android.App {

	[Register ("android/app/Application", DoNotGenerateAcw = true)]
	class Application : Java.Lang.Object
	{
		[Register ("onCreate", "()V", "Application.OnCreate")]
		protected virtual void OnCreate ()
		{
		}
	}

	[Register ("android/app/Activity", DoNotGenerateAcw = true)]
	class Activity : Java.Lang.Object
	{
		[Register ("onCreate", "(Ljava/lang/Object;)V", "Activity.OnCreate")]
		public virtual void OnCreate (Java.Lang.Object savedInstanceState)
		{
		}
	}

	[Register ("android/app/Instrumentation", DoNotGenerateAcw = true)]
	class Instrumentation : Java.Lang.Object
	{
		[Register ("onCreate", "(Ljava/lang/Object;)V", "Instrumentation.OnCreate")]
		public virtual void OnCreate (Java.Lang.Object arguments)
		{
		}
	}
}

namespace Android.Runtime {

	interface IJavaObject
	{
	}
}

namespace Java.Lang {

	[Register ("java/lang/Object", DoNotGenerateAcw = true)]
	class Object : Android.Runtime.IJavaObject
	{
	}

	[Register ("java/lang/Throwable", DoNotGenerateAcw = true)]
	class Throwable : Exception, Android.Runtime.IJavaObject
	{
	}
}

namespace Xamarin.Android.ToolsTests {

	static class SupportDeclarations
	{
		static readonly Type [] Types = new []{
			typeof (AbstractClassInvoker),
			typeof (AbstractClass),
			typeof (ActivityName),
			typeof (ApplicationName),
			typeof (DefaultName),
			typeof (DefaultName.A),
			typeof (DefaultName.A.B),
			typeof (DefaultName.C.D),
			typeof (ExampleActivity),
			typeof (ExampleInstrumentation),
			typeof (ExampleOuterClass),
			typeof (ExampleOuterClass.ExampleInnerClass),
			typeof (InstrumentationName),
			typeof (NonStaticOuterClass),
			typeof (NonStaticOuterClass.NonStaticInnerClass),
			typeof (ProviderName),
			typeof (ReceiverName),
			typeof (RegisterName),
			typeof (RegisterName.DefaultNestedName),
			typeof (RegisterName.OverrideNestedName),
			typeof (ServiceName),
		};

		public static List<TypeDefinition> GetTestTypeDefinitions ()
		{
			var a = AssemblyDefinition.ReadAssembly (typeof (DefaultName).Assembly.Location);
			var r = new List<TypeDefinition> ();
			foreach (var t in Types) {
				r.Add (GetTypeDefinition (t, a));
			}
			return r;
		}

		public static TypeDefinition GetTypeDefinition (Type type, AssemblyDefinition assemblyDef = null)
		{
			assemblyDef = assemblyDef ?? AssemblyDefinition.ReadAssembly (type.Assembly.Location);

			if (!type.IsNested) {
				return assemblyDef.MainModule.FindType (type.FullName);
			}
			var declTypes = new Stack<Type> ();
			for (var d = type; d != null; d = d.DeclaringType) {
				declTypes.Push (d);
			}
			var def = assemblyDef.MainModule.FindType (declTypes.Pop ().FullName);
			while (declTypes.Count != 0) {
				var n = declTypes.Pop ();
				def = def.NestedTypes.Single (nt => nt.Name == n.Name);
			}
			return def;
		}
	}

	[Register ("my.AbstractClass")]
	abstract class AbstractClass : Java.Lang.Object
	{
	}

	[Register ("my.AbstractClass")]
	class AbstractClassInvoker : AbstractClass
	{
	}

	[Activity (Name = "activity.Name")]
	class ActivityName : Java.Lang.Object
	{
	}

	[Application (Name = "application.Name")]
	class ApplicationName : Application
	{
	}

	class IndirectApplication : ApplicationName
	{
		protected override void OnCreate ()
		{
			base.OnCreate ();
		}
	}

	class DefaultName : Java.Lang.Object
	{
		public class A : Java.Lang.Object
		{
			public class B : Java.Lang.Object
			{
			}
		}
		public class C
		{
			public class D : Java.Lang.Object
			{
			}
		}
	}

	[Instrumentation (Name = "instrumentation.Name")]
	class InstrumentationName : Java.Lang.Object
	{
	}

	[ContentProvider (Name = "provider.Name")]
	class ProviderName : Java.Lang.Object
	{
	}

	[Register ("register/NonStaticOuterClass", DoNotGenerateAcw = true)]
	class NonStaticOuterClass : Java.Lang.Object
	{

		[Register ("register/NonStaticOuterClass$NonStaticInnerClass", DoNotGenerateAcw = true)]
		public class NonStaticInnerClass : Java.Lang.Object
		{
			public NonStaticInnerClass (NonStaticOuterClass __self)
			{
			}
		}
	}

	class ExampleOuterClass : NonStaticOuterClass
	{
		public class ExampleInnerClass : NonStaticInnerClass
		{
			public ExampleInnerClass (ExampleOuterClass outer)
				: base (outer)
			{
			}
		}
	}

	[Activity (Name = "my.ExampleActivity")]
	class ExampleActivity : Activity
	{
	}

	[Instrumentation (Name = "my.ExampleInstrumentation")]
	class ExampleInstrumentation : Instrumentation
	{
	}

	[BroadcastReceiver (Name = "receiver.Name")]
	class ReceiverName : Java.Lang.Object
	{
	}

	[Register ("register.Name")]
	class RegisterName : Java.Lang.Object
	{
		public class DefaultNestedName : Java.Lang.Object
		{
		}

		[Register ("register.Name$Override")]
		public class OverrideNestedName : Java.Lang.Object
		{
		}
	}

	[Service (Name = "service.Name")]
	class ServiceName : Java.Lang.Object
	{
	}

	class ExportsMembers : Java.Lang.Object
	{
		[ExportField ("STATIC_INSTANCE")]
		public static ExportsMembers GetInstance ()
		{
			return null;
		}

		[ExportField ("VALUE")]
		public string GetValue ()
		{
			return "value";
		}

		[Export]
		public void methodNamesNotMangled ()
		{
		}

		[Export ("attributeOverridesNames")]
		public string CompletelyDifferentName (string value, int count)
		{
			return value;
		}

		[Export (Throws = new [] { typeof (Java.Lang.Throwable) })]
		public void methodThatThrows() 
		{
		}

		[Export (Throws = new Type [0])]
		public void methodThatThrowsEmptyArray ()
		{
		}
	}

	[Register ("register.ExportsConstructors")]
	class ExportsConstructors : Java.Lang.Object
	{
		[Export]
		public ExportsConstructors () { }

		[Export]
		public ExportsConstructors (int value) { }
	}

	[Register ("register.ExportsThrowsConstructors")]
	class ExportsThrowsConstructors : Java.Lang.Object
	{
		[Export (Throws = new [] { typeof (Java.Lang.Throwable) })]
		public ExportsThrowsConstructors () { }

		[Export (Throws = new [] { typeof (Java.Lang.Throwable) })]
		public ExportsThrowsConstructors (int value) { }

		[Export (Throws = new Type [0])]
		public ExportsThrowsConstructors (string value) { }
	}
}
