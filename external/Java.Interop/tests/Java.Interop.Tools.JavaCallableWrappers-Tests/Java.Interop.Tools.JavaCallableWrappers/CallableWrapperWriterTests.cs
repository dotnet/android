using System;
using System.IO;

using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;

using NUnit.Framework;

namespace Java.Interop.Tools.JavaCallableWrappersTests
{
	[TestFixture]
	public class CallableWrapperWriterTests
	{
		[TestCase (false, false)]
		[TestCase (false, true)]
		[TestCase (true, false)]
		[TestCase (true, true)]
		public void DynamicRegistrationIncludesOnlySelectedTypes (bool registerOuter, bool registerNested)
		{
			var type = CreateType ("Outer");
			type.Methods.Add (CreateMethod (type, registerOuter));
			var nested = CreateType ("Nested");
			nested.Methods.Add (CreateMethod (nested, registerNested));
			type.NestedTypes.Add (nested);

			var writer = new StringWriter ();
			type.Generate (writer);
			var output = writer.ToString ();

			Assert.AreEqual (registerOuter, output.Contains ("public static final String __md_methods;"));
			Assert.AreEqual (registerNested, output.Contains ("static final String __md_1_methods;"));
			Assert.AreEqual (registerOuter, output.Contains ("mono.android.Runtime.register (\"Example.Outer, Example\", Outer.class, __md_methods);"));
			Assert.AreEqual (registerNested, output.Contains ("mono.android.Runtime.register (\"Example.Nested, Example\", Nested.class, __md_1_methods);"));
			Assert.AreEqual (registerOuter || registerNested, output.Contains ("\tstatic {"));
			Assert.AreEqual (1, output.Split ("package example;").Length - 1);
			Assert.AreEqual (2, output.Split ("private native void n_example ();").Length - 1);
			Assert.AreEqual (2, output.Split ("mono.android.IGCUserPeer").Length - 1);
			Assert.AreEqual (2, output.Split ("public void monodroidAddReference").Length - 1);
			Assert.AreEqual (2, output.Split ("public void monodroidClearReferences").Length - 1);
		}

		[Test]
		public void SingleStyleGenerationPreservesVirtualOverrides ()
		{
			var type = CreateType ("Example");
			var method = new CustomMethod (type);
			var constructor = new CustomConstructor (type);
			type.Methods.Add (method);
			type.Constructors.Add (constructor);
			type.Generate (new StringWriter ());

			Assert.AreEqual (1, method.GenerateCount);
			Assert.AreEqual (1, constructor.GenerateCount);
		}

		[Test]
		public void LegacyGenerationPreservesCallerOptionsForNestedOverrides ()
		{
			var directory = Path.Combine (TestContext.CurrentContext.WorkDirectory, Guid.NewGuid ().ToString ("N"));
			try {
				foreach (var options in new CallableWrapperWriterOptions [] { new CustomOptions { State = new object () }, null }) {
					var type = CreateType ("Outer");
					var nested = CreateType ("Nested");
					type.NestedTypes.Add (nested);
					var method = new CustomMethod (type);
					var constructor = new CustomConstructor (type);
					var nestedMethod = new CustomMethod (nested);
					var nestedConstructor = new CustomConstructor (nested);
					type.Methods.Add (method);
					type.Constructors.Add (constructor);
					nested.Methods.Add (nestedMethod);
					nested.Constructors.Add (nestedConstructor);

					if (options == null)
						type.Generate (new StringWriter (), default);
					else
						type.Generate (new StringWriter (), options);
					AssertCallerOptions (1);

					if (options == null)
						type.Generate (directory, default);
					else
						type.Generate (directory, options);
					AssertCallerOptions (2);

					void AssertCallerOptions (int count)
					{
						Assert.AreEqual (count, method.GenerateCount);
						Assert.AreEqual (count, constructor.GenerateCount);
						Assert.AreEqual (count, nestedMethod.GenerateCount);
						Assert.AreEqual (count, nestedConstructor.GenerateCount);
						Assert.AreSame (options, method.Options);
						Assert.AreSame (options, constructor.Options);
						Assert.AreSame (options, nestedMethod.Options);
						Assert.AreSame (options, nestedConstructor.Options);
						if (options is CustomOptions custom)
							Assert.AreSame (custom.State, ((CustomOptions) nestedMethod.Options).State);
					}
				}
			} finally {
				if (Directory.Exists (directory))
					Directory.Delete (directory, true);
			}
		}

		static CallableWrapperType CreateType (string name)
		{
			return new CallableWrapperType (name, "example", $"Example.{name}, Example") {
				ExtendsType = "java.lang.Object",
			};
		}

		static CallableWrapperMethod CreateMethod (CallableWrapperType type, bool dynamicallyRegistered)
		{
			return new CallableWrapperMethod (type, "example", "n_example:()V:GetExampleHandler", "()V") {
				Retval = "void",
				Params = "",
				ActivateCall = "",
				IsDynamicallyRegistered = dynamicallyRegistered,
			};
		}

		class CustomMethod : CallableWrapperMethod
		{
			public int GenerateCount { get; private set; }
			public CallableWrapperWriterOptions Options { get; private set; }

			public CustomMethod (CallableWrapperType type) : base (type, "custom", "", "()V")
			{
			}

			public override void Generate (TextWriter writer, CallableWrapperWriterOptions options)
			{
				Options = options;
				GenerateCount++;
			}
		}

		class CustomConstructor : CallableWrapperConstructor
		{
			public int GenerateCount { get; private set; }
			public CallableWrapperWriterOptions Options { get; private set; }

			public CustomConstructor (CallableWrapperType type) : base (type, type.Name, "", "()V")
			{
			}

			public override void Generate (TextWriter writer, CallableWrapperWriterOptions options)
			{
				Options = options;
				GenerateCount++;
			}
		}

		class CustomOptions : CallableWrapperWriterOptions
		{
			public object State { get; set; }
		}
	}
}
