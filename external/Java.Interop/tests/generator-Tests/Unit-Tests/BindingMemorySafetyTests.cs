using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	class BindingMemorySafetyTests : CodeGeneratorTestBase
	{
		const string RequiresUnsafe = "System.Diagnostics.CodeAnalysis.RequiresUnsafeAttribute";
		const string Rules = "System.Runtime.CompilerServices.MemorySafetyRulesAttribute";

		protected override CodeGenerationOptions CreateOptions () => new CodeGenerationOptions {
			SupportNullableReferenceTypes = true,
			UseGlobal = true,
		};

		const string Api = """
			<api>
			  <package name='java.lang'>
			    <class name='Object' visibility='public' />
			    <interface name='CharSequence' visibility='public' abstract='true' />
			  </package>
			  <package name='com.example'>
			    <interface name='Contract' visibility='public' abstract='true'>
			      <method name='transform' return='java.lang.String' visibility='public' abstract='true'>
			        <parameter name='text' type='java.lang.String' />
			        <parameter name='bytes' type='byte[]' />
			        <parameter name='flags' type='boolean[]' />
			        <parameter name='values' type='int[]' />
			      </method>
			      <method name='ping' return='int' visibility='public' abstract='true' />
			      <method name='getName' return='java.lang.String' visibility='public' abstract='true' />
			      <method name='setName' return='void' visibility='public' abstract='true'>
			        <parameter name='name' type='java.lang.String' />
			      </method>
			    </interface>
			    <class name='Base' extends='java.lang.Object' visibility='public' abstract='true'>
			      <implements name='com.example.Contract' name-generic-aware='com.example.Contract' />
			      <constructor name='Base' visibility='public'>
			        <parameter name='text' type='java.lang.String' />
			        <parameter name='values' type='int[]' />
			      </constructor>
			      <method name='transform' return='java.lang.String' visibility='public' abstract='true'>
			        <parameter name='text' type='java.lang.String' />
			        <parameter name='bytes' type='byte[]' />
			        <parameter name='flags' type='boolean[]' />
			        <parameter name='values' type='int[]' />
			      </method>
			      <method name='ping' return='int' visibility='public' abstract='true' />
			      <method name='getName' return='java.lang.String' visibility='public' abstract='true' />
			      <method name='setName' return='void' visibility='public' abstract='true'>
			        <parameter name='name' type='java.lang.String' />
			      </method>
			    </class>
			    <class name='Widget' extends='java.lang.Object' visibility='public'>
			      <constructor name='Widget' visibility='public' />
			      <constructor name='Widget' visibility='public'>
			        <parameter name='text' type='java.lang.CharSequence' />
			      </constructor>
			      <field name='label' type='java.lang.String' visibility='public' static='false' final='false' />
			      <method name='describe' return='java.lang.String' visibility='public' abstract='false' final='false'>
			        <parameter name='text' type='java.lang.String' />
			      </method>
			      <method name='getText' return='java.lang.CharSequence' visibility='public' abstract='false' final='false' />
			      <method name='setText' return='void' visibility='public' abstract='false' final='false'>
			        <parameter name='text' type='java.lang.CharSequence' />
			      </method>
			      <method name='getFastText' return='java.lang.CharSequence' visibility='public' abstract='false' final='true' />
			      <method name='setFastText' return='void' visibility='public' abstract='false' final='true'>
			        <parameter name='text' type='java.lang.CharSequence' />
			      </method>
			      <method name='echo' return='java.lang.CharSequence' visibility='public' abstract='false' final='false'>
			        <parameter name='text' type='java.lang.CharSequence' />
			      </method>
			      <method name='ping' return='int' visibility='public' abstract='false' final='false' />
			    </class>
			    <interface name='OnChangedListener' visibility='public' abstract='true'>
			      <method name='onChanged' return='void' visibility='public' abstract='true'>
			        <parameter name='text' type='java.lang.String' />
			      </method>
			    </interface>
			  </package>
			</api>
			""";

		string GenerateBindings (bool directCallbacks)
		{
			options.UseUnmanagedCallersOnlyCallbacks = directCallbacks;
			foreach (var gen in ParseApiDefinition (Api).Where (g => g.Namespace == "Com.Example"))
				GetGeneratedTypeOutput (gen);
			return "using System; using System.Collections.Generic; using Android.Runtime; using Java.Interop;\nnamespace Com.Example {\n" +
				writer.ToString () + """
				}
				delegate int _JniMarshal_PP_I (IntPtr env, IntPtr self);
				delegate IntPtr _JniMarshal_PP_L (IntPtr env, IntPtr self);
				delegate IntPtr _JniMarshal_PPL_L (IntPtr env, IntPtr self, IntPtr text);
				delegate void _JniMarshal_PPL_V (IntPtr env, IntPtr self, IntPtr text);
				delegate IntPtr _JniMarshal_PPLLLL_L (IntPtr env, IntPtr self, IntPtr text, IntPtr bytes, IntPtr flags, IntPtr values);
				""";
		}

		[TestCase (false)]
		[TestCase (true)]
		public void ManagedContractsAndMarshalBodiesCompileInBothModes (bool directCallbacks)
		{
			var source = GenerateBindings (directCallbacks);
			Assert.That (source, Does.Contain ("JNIEnv.CopyArray (native_values, values);"));
			Assert.That (source, Does.Contain ("JNIEnv.CopyArray (values, native_values);"));
			Assert.That (source, Does.Contain ("JNIEnv.GetString"));
			Assert.That (source, Does.Contain ("global::System.GC.KeepAlive (values);"));
			Assert.That (source, Does.Contain ("public override string"));
			Assert.That (source, Does.Contain ("OnChangedListenerImplementor"));

			using (var compiler = new CommandLineCompiler ()) {
				foreach (var updatedRules in new [] { false, true }) {
					var contracts = compiler.Compile (Compiler.GetSupportFilePaths ().Select (File.ReadAllText)
						.Append ("using System;\n" + UnmanagedCallersOnlyCallbackTests.TypedCompilationSupport), updatedRules);
					Assert.That (contracts.ExitCode, Is.Zero, contracts.Output);
					AssertContractMetadata (contracts.AssemblyPath, updatedRules);
					AssertContractMetadata (contracts.ReferencePath, updatedRules);

					var references = Compiler.GetReferencePaths ().Append (contracts.ReferencePath);
					var bindings = compiler.Compile (new [] { source }, updatedRules, references);
					Assert.That (bindings.ExitCode, Is.Zero, bindings.Output + "\n" + source);
					AssertBindingMetadata (bindings.AssemblyPath, updatedRules, directCallbacks);
					AssertBindingMetadata (bindings.ReferencePath, updatedRules, directCallbacks);
				}
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RequiresUnsafeMetadataControlsRawCallsites (bool useReferenceAssembly)
		{
			using (var compiler = new CommandLineCompiler ()) {
				var contracts = compiler.Compile (Compiler.GetSupportFilePaths ().Select (File.ReadAllText), updatedRules: true);
				Assert.That (contracts.ExitCode, Is.Zero, contracts.Output);
				var references = Compiler.GetReferencePaths ().Append (useReferenceAssembly ? contracts.ReferencePath : contracts.AssemblyPath);
				const string unsafeCall = """
					using System;
					using Android.Runtime;
					public class Consumer {
						public string Read (IntPtr handle) {
							return JNIEnv.GetString (handle, JniHandleOwnership.DoNotTransfer);
						}
					}
					""";
				var negative = compiler.Compile (new [] { unsafeCall }, updatedRules: true, references);
				Assert.That (negative.ExitCode, Is.Not.Zero);
				Assert.That (negative.Output, Does.Contain ("CS9362"), negative.Output);

				var audited = unsafeCall.Replace (
					"return JNIEnv.GetString (handle, JniHandleOwnership.DoNotTransfer);",
					"unsafe { return JNIEnv.GetString (handle, JniHandleOwnership.DoNotTransfer); }");
				var positive = compiler.Compile (new [] { audited }, updatedRules: true, references);
				Assert.That (positive.ExitCode, Is.Zero, positive.Output);
				var legacy = compiler.Compile (new [] { unsafeCall }, updatedRules: false, references);
				Assert.That (legacy.ExitCode, Is.Zero, legacy.Output);
			}
		}

		const string ThrowableApi = """
			<api>
			  <package name='java.lang'>
			    <class name='Object' visibility='public' deprecated='not deprecated' />
			    <class name='Throwable' extends='java.lang.Object' visibility='public' deprecated='not deprecated' />
			  </package>
			  <package name='com.example'>
			    <class name='ParentError' extends='java.lang.Throwable' visibility='public' deprecated='not deprecated'>
			      <constructor name='ParentError' visibility='public' deprecated='not deprecated'>
			        <parameter name='message' type='java.lang.String' />
			      </constructor>
			    </class>
			    <class name='MiddleError' extends='com.example.ParentError' visibility='public' abstract='true' deprecated='not deprecated'>
			      <constructor name='MiddleError' visibility='public' deprecated='not deprecated'>
			        <parameter name='message' type='java.lang.String' />
			      </constructor>
			      <method name='ping' return='int' visibility='public' abstract='true' deprecated='not deprecated' />
			    </class>
			    <class name='LeafError' extends='com.example.MiddleError' visibility='public' deprecated='not deprecated'>
			      <constructor name='LeafError' visibility='public' deprecated='not deprecated'>
			        <parameter name='message' type='java.lang.String' />
			      </constructor>
			      <method name='ping' return='int' visibility='public' abstract='false' deprecated='not deprecated' />
			    </class>
			  </package>
			</api>
			""";

		[Test]
		public void ThrowableHierarchySeparatesDeferredConstructionFromRawActivation ()
		{
			var gens = ParseApiDefinition (ThrowableApi);
			var parent = gens.OfType<ClassGen> ().Single (g => g.Name == "ParentError");
			parent.PeerConstructorPartialMethod = "OnPeerConstructed";
			foreach (var gen in gens.Where (g => g.Namespace == "Com.Example"))
				GetGeneratedTypeOutput (gen);
			var source = "using System; using Android.Runtime; using Java.Interop;\nnamespace Com.Example {\n" +
				writer.ToString () + "\npublic partial class ParentError { partial void OnPeerConstructed () { } }\n}\ndelegate int _JniMarshal_PP_I (IntPtr env, IntPtr self);";
			Assert.That (source, Does.Contain ("protected LeafError (global::Java.Interop.JniObjectReferenceOptions options) : base (options)"));
			Assert.That (source, Does.Contain ("public LeafError (string? message) : base (global::Java.Interop.JniObjectReferenceOptions.None)"));
			Assert.That (source, Does.Contain ("protected unsafe LeafError (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer)"));

			using (var compiler = new CommandLineCompiler ()) {
				foreach (var updatedRules in new [] { false, true }) {
					var contracts = compiler.Compile (Compiler.GetSupportFilePaths ().Select (File.ReadAllText), updatedRules);
					Assert.That (contracts.ExitCode, Is.Zero, contracts.Output);
					var references = Compiler.GetReferencePaths ().Append (contracts.ReferencePath);
					var bindings = compiler.Compile (new [] { source }, updatedRules, references);
					Assert.That (bindings.ExitCode, Is.Zero, bindings.Output + "\n" + source);
					foreach (var path in new [] { bindings.AssemblyPath, bindings.ReferencePath }) {
						using (var module = ModuleDefinition.ReadModule (path)) {
							AssertRulesMetadata (module, updatedRules);
							foreach (var type in module.Types.Where (t => t.Namespace == "Com.Example")) {
								foreach (var ctor in type.Methods.Where (m => m.IsConstructor)) {
									var raw = ctor.Parameters.Count == 2 && ctor.Parameters [0].ParameterType.FullName == "System.IntPtr";
									Assert.That (ctor.CustomAttributes.Any (a => a.AttributeType.FullName == RequiresUnsafe),
										Is.EqualTo (updatedRules && raw), ctor.FullName);
									if (ctor.Parameters.Count == 1 && ctor.Parameters [0].ParameterType.FullName == "Java.Interop.JniObjectReferenceOptions")
										Assert.That (ctor.CustomAttributes.Any (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute"), Is.False);
									if (path == bindings.AssemblyPath && type.Name == "ParentError" && ctor.HasBody &&
											(raw || ctor.Parameters.Count == 1 && ctor.Parameters [0].ParameterType.FullName == "Java.Interop.JniObjectReferenceOptions"))
										Assert.That (ctor.Body.Instructions.Count (i => i.Operand is MethodReference method && method.Name == "OnPeerConstructed"), Is.EqualTo (1), ctor.FullName);
								}
							}
						}
					}
					references = references.Append (bindings.ReferencePath).ToArray ();
					const string rawConsumer = """
						using System;
						using Android.Runtime;
						public class Consumer : Com.Example.LeafError {
							public Consumer (IntPtr handle) : base (handle, JniHandleOwnership.DoNotTransfer) { }
						}
						""";
					var rawCall = compiler.Compile (new [] { rawConsumer }, updatedRules, references);
					Assert.That (rawCall.ExitCode == 0, Is.EqualTo (!updatedRules), rawCall.Output);
					if (updatedRules)
						Assert.That (rawCall.Output, Does.Contain ("CS9362"));
					var audited = compiler.Compile (new [] { rawConsumer.Replace ("public Consumer", "public unsafe Consumer") }, updatedRules, references);
					Assert.That (audited.ExitCode, Is.Zero, audited.Output);
					var managed = compiler.Compile (new [] { "public class Consumer : Com.Example.LeafError { public Consumer () : base (\"message\") { } }" }, updatedRules, references);
					Assert.That (managed.ExitCode, Is.Zero, managed.Output);
				}
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void ReferencedThrowableBaseUsesActualDeferredConstructorCapability (bool hasDeferredConstructor)
		{
			using (var compiler = new CommandLineCompiler ()) {
				var support = compiler.Compile (Compiler.GetSupportFilePaths ().Select (File.ReadAllText), updatedRules: false);
				Assert.That (support.ExitCode, Is.Zero, support.Output);
				var references = Compiler.GetReferencePaths ().Append (support.ReferencePath).ToArray ();
				var foreignSource = $$"""
					using System;
					using Android.Runtime;
					using Java.Interop;
					namespace Foreign {
						[Register ("foreign/BaseError")]
						public class BaseError : Java.Lang.Throwable {
							protected unsafe BaseError (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
							{{(hasDeferredConstructor ? "protected BaseError (JniObjectReferenceOptions options) : base (options) { }" : "")}}
						}
					}
					""";
				var foreign = compiler.Compile (new [] { foreignSource }, updatedRules: false, references);
				Assert.That (foreign.ExitCode, Is.Zero, foreign.Output);

				ParseApiDefinition ("""
					<api><package name='java.lang'>
					  <class name='Object' visibility='public' />
					  <class name='Throwable' extends='java.lang.Object' visibility='public' />
					</package></api>
					""");
				using (var module = ModuleDefinition.ReadModule (foreign.ReferencePath)) {
					var imported = CecilApiImporter.CreateClass (module.GetType ("Foreign.BaseError"), options);
					Assert.That (imported.HasDeferredThrowableConstructor, Is.EqualTo (hasDeferredConstructor));
					options.SymbolTable.AddType (imported);
					Assert.That (imported.Validate (options, new GenericParameterDefinitionList (), generator.Context), Is.True);
					var leaf = ParseApiDefinition ("""
						<api><package name='com.example'>
						  <class name='LeafError' extends='foreign.BaseError' visibility='public' deprecated='not deprecated'>
						    <constructor name='LeafError' visibility='public' deprecated='not deprecated' />
						  </class>
						</package></api>
						""").Single ();
					var source = GetGeneratedTypeOutput (leaf);
					Assert.That (source.Contains ("base (global::Java.Interop.JniObjectReferenceOptions.None)"), Is.EqualTo (hasDeferredConstructor), source);
					Assert.That (source.Contains ("base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)"), Is.EqualTo (!hasDeferredConstructor), source);
					var bindingSource = "using System; using Android.Runtime; using Java.Interop;\nnamespace Com.Example {\n" + source + "\n}";
					var binding = compiler.Compile (new [] { bindingSource }, updatedRules: false, references.Append (foreign.ReferencePath));
					Assert.That (binding.ExitCode, Is.Zero, binding.Output);

					var updatedSupport = compiler.Compile (Compiler.GetSupportFilePaths ().Select (File.ReadAllText), updatedRules: true);
					Assert.That (updatedSupport.ExitCode, Is.Zero, updatedSupport.Output);
					var updatedReferences = Compiler.GetReferencePaths ().Append (updatedSupport.ReferencePath).ToArray ();
					var updatedForeign = compiler.Compile (new [] { foreignSource }, updatedRules: true, updatedReferences);
					Assert.That (updatedForeign.ExitCode, Is.Zero, updatedForeign.Output);
					var updatedBinding = compiler.Compile (new [] { bindingSource }, updatedRules: true, updatedReferences.Append (updatedForeign.ReferencePath));
					Assert.That (updatedBinding.ExitCode == 0, Is.EqualTo (hasDeferredConstructor), updatedBinding.Output);
					if (!hasDeferredConstructor)
						Assert.That (updatedBinding.Output, Does.Contain ("CS9362"), "A requires-unsafe raw base must not silently become safe.");
				}
			}
		}

		static void AssertRulesMetadata (ModuleDefinition module, bool updatedRules)
		{
			var marker = module.CustomAttributes.SingleOrDefault (a => a.AttributeType.FullName == Rules);
			Assert.That (marker != null, Is.EqualTo (updatedRules), module.FileName);
			if (marker != null)
				Assert.That (marker.ConstructorArguments.Single ().Value, Is.EqualTo (2));
		}

		static void AssertContractMetadata (string path, bool updatedRules)
		{
			using (var module = ModuleDefinition.ReadModule (path)) {
				AssertRulesMetadata (module, updatedRules);
				var rawString = module.Types.Single (t => t.FullName == "Android.Runtime.JNIEnv").Methods.Single (m => m.Name == "GetString");
				Assert.That (rawString.CustomAttributes.Any (a => a.AttributeType.FullName == RequiresUnsafe), Is.EqualTo (updatedRules), path);
				foreach (var copy in module.Types.Single (t => t.FullName == "Android.Runtime.JNIEnv").Methods.Where (m => m.Name == "CopyArray"))
					Assert.That (copy.CustomAttributes.Any (a => a.AttributeType.FullName == RequiresUnsafe), Is.EqualTo (updatedRules), copy.FullName);
			}
		}

		static void AssertBindingMetadata (string path, bool updatedRules, bool directCallbacks)
		{
			using (var module = ModuleDefinition.ReadModule (path)) {
				AssertRulesMetadata (module, updatedRules);
				var generated = module.Types.Where (t => t.Namespace == "Com.Example").ToArray ();
				Assert.That (generated.Length, Is.GreaterThan (3));
				foreach (var method in generated.SelectMany (t => t.Methods))
					Assert.That (method.CustomAttributes.Any (a => a.AttributeType.FullName == RequiresUnsafe), Is.False, method.FullName);
				var describe = generated.Single (t => t.Name == "Widget").Methods.Single (m => m.Name == "Describe");
				var register = describe.CustomAttributes.Single (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute");
				Assert.That (register.ConstructorArguments [0].Value, Is.EqualTo ("describe"));
				Assert.That (register.ConstructorArguments [1].Value, Is.EqualTo ("(Ljava/lang/String;)Ljava/lang/String;"));
				Assert.That (register.ConstructorArguments [2].Value, Is.EqualTo (directCallbacks ? "n_Describe" : "GetDescribe_Ljava_lang_String_Handler"));
			}
		}
	}
}
