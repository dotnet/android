#if ENABLE_MARSHAL_METHODS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;

using Java.Interop.Tools.TypeNameMappings;
using Java.Interop.Tools.JavaCallableWrappers;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks
{
	class MarshalMethodsNativeAssemblyGenerator : LlvmIrComposer
	{
		// This is here only to generate strongly-typed IR
		internal sealed class MonoClass
		{}

		[NativeClass]
		sealed class _JNIEnv
		{}

		// Empty class must have at least one member so that the class address can be obtained
		[NativeClass]
		class _jobject
		{
			public byte b;
		}

		sealed class _jclass : _jobject
		{}

		sealed class _jstring : _jobject
		{}

		sealed class _jthrowable : _jobject
		{}

		class _jarray : _jobject
		{}

		sealed class _jobjectArray : _jarray
		{}

		sealed class _jbooleanArray : _jarray
		{}

		sealed class _jbyteArray : _jarray
		{}

		sealed class _jcharArray : _jarray
		{}

		sealed class _jshortArray : _jarray
		{}

		sealed class _jintArray : _jarray
		{}

		sealed class _jlongArray : _jarray
		{}

		sealed class _jfloatArray : _jarray
		{}

		sealed class _jdoubleArray : _jarray
		{}

		sealed class MarshalMethodInfo
		{
			public MarshalMethodEntry Method                { get; }
			public string NativeSymbolName                  { get; set; }
			public List<LlvmIrFunctionParameter> Parameters { get; }
			public Type ReturnType                          { get; }
			public uint ClassCacheIndex                     { get; }

			// This one isn't known until the generation time, which happens after we instantiate the class
			// in Init and it may be different between architectures/ABIs, hence it needs to be settable from
			// the outside.
			public uint AssemblyCacheIndex                  { get; set; }

			public MarshalMethodInfo (MarshalMethodEntry method, Type returnType, string nativeSymbolName, int classCacheIndex)
			{
				Method = method ?? throw new ArgumentNullException (nameof (method));
				ReturnType = returnType ?? throw new ArgumentNullException (nameof (returnType));
				if (String.IsNullOrEmpty (nativeSymbolName)) {
					throw new ArgumentException ("must not be null or empty", nameof (nativeSymbolName));
				}
				NativeSymbolName = nativeSymbolName;
				Parameters = new List<LlvmIrFunctionParameter> {
					new LlvmIrFunctionParameter (typeof (_JNIEnv), "env", isNativePointer: true), // JNIEnv *env
					new LlvmIrFunctionParameter (typeof (_jclass), "klass", isNativePointer: true), // jclass klass
				};
				ClassCacheIndex = (uint)classCacheIndex;
			}
		}

		sealed class MarshalMethodsManagedClassDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var klass = EnsureType<MarshalMethodsManagedClass> (data);

				if (String.Compare ("token", fieldName, StringComparison.Ordinal) == 0) {
					return $"token 0x{klass.token:x}; class name: {klass.ClassName}";
				}

				return String.Empty;
			}
		}

		[NativeAssemblerStructContextDataProvider (typeof(MarshalMethodsManagedClassDataProvider))]
		sealed class MarshalMethodsManagedClass
		{
			[NativeAssembler (UsesDataProvider = true)]
			public uint       token;

			[NativePointer (IsNull = true)]
			public MonoClass  klass;

			[NativeAssembler (Ignore = true)]
			public string ClassName;
		};

		static readonly Dictionary<char, Type> jniSimpleTypeMap = new Dictionary<char, Type> {
			{ 'Z', typeof(bool) },
			{ 'B', typeof(byte) },
			{ 'C', typeof(char) },
			{ 'S', typeof(short) },
			{ 'I', typeof(int) },
			{ 'J', typeof(long) },
			{ 'F', typeof(float) },
			{ 'D', typeof(double) },
		};

		static readonly Dictionary<char, Type> jniArrayTypeMap = new Dictionary<char, Type> {
			{ 'Z', typeof(_jbooleanArray) },
			{ 'B', typeof(_jbyteArray) },
			{ 'C', typeof(_jcharArray) },
			{ 'S', typeof(_jshortArray) },
			{ 'I', typeof(_jintArray) },
			{ 'J', typeof(_jlongArray) },
			{ 'F', typeof(_jfloatArray) },
			{ 'D', typeof(_jdoubleArray) },
			{ 'L', typeof(_jobjectArray) },
		};

		public ICollection<string> UniqueAssemblyNames                       { get; set; }
		public int NumberOfAssembliesInApk                                   { get; set; }
		public IDictionary<string, IList<MarshalMethodEntry>> MarshalMethods { get; set; }

		StructureInfo<TypeMappingReleaseNativeAssemblyGenerator.MonoImage> monoImage;
		StructureInfo<MarshalMethodsManagedClass> marshalMethodsClass;
		StructureInfo<MonoClass> monoClass;
		StructureInfo<_JNIEnv> _jniEnvSI;
		StructureInfo<_jobject> _jobjectSI;
		StructureInfo<_jclass> _jclassSI;
		StructureInfo<_jstring> _jstringSI;
		StructureInfo<_jthrowable> _jthrowableSI;
		StructureInfo<_jarray> _jarraySI;
		StructureInfo<_jobjectArray> _jobjectArraySI;
		StructureInfo<_jbooleanArray> _jbooleanArraySI;
		StructureInfo<_jbyteArray> _jbyteArraySI;
		StructureInfo<_jcharArray> _jcharArraySI;
		StructureInfo<_jshortArray> _jshortArraySI;
		StructureInfo<_jintArray> _jintArraySI;
		StructureInfo<_jlongArray> _jlongArraySI;
		StructureInfo<_jfloatArray> _jfloatArraySI;
		StructureInfo<_jdoubleArray> _jdoubleArraySI;

		List<MarshalMethodInfo> methods;
		List<StructureInstance<MarshalMethodsManagedClass>> classes = new List<StructureInstance<MarshalMethodsManagedClass>> ();

		public override void Init ()
		{
			Console.WriteLine ($"Marshal methods count: {MarshalMethods?.Count ?? 0}");
			if (MarshalMethods == null || MarshalMethods.Count == 0) {
				return;
			}

			var seenClasses = new Dictionary<string, int> (StringComparer.Ordinal);
			var allMethods = new List<MarshalMethodInfo> ();

			// It's possible that several otherwise different methods (from different classes, but with the same
			// names and similar signatures) will actually share the same **short** native symbol name. In this case we must
			// ensure that they all use long symbol names.  This has to be done as a post-processing step, after we
			// have already iterated over the entire method collection.
			var overloadedNativeSymbolNames = new Dictionary<string, List<MarshalMethodInfo>> (StringComparer.Ordinal);
			foreach (IList<MarshalMethodEntry> entryList in MarshalMethods.Values) {
				bool useFullNativeSignature = entryList.Count > 1;
				foreach (MarshalMethodEntry entry in entryList) {
					ProcessAndAddMethod (allMethods, entry, useFullNativeSignature, seenClasses, overloadedNativeSymbolNames);
				}
			}

			foreach (List<MarshalMethodInfo> mmiList in overloadedNativeSymbolNames.Values) {
				if (mmiList.Count <= 1) {
					continue;
				}

				Console.WriteLine ($"Overloaded MM: {mmiList[0].NativeSymbolName}");
				foreach (MarshalMethodInfo overloadedMethod in mmiList) {
					Console.WriteLine ($"  implemented in: {overloadedMethod.Method.DeclaringType.FullName} ({overloadedMethod.Method.RegisteredMethod.FullName})");
					overloadedMethod.NativeSymbolName = MakeNativeSymbolName (overloadedMethod.Method, useFullNativeSignature: true);
					Console.WriteLine ($"     new native symbol name: {overloadedMethod.NativeSymbolName}");
				}
			}

			// In some cases it's possible that a single type implements two different interfaces which have methods with the same native signature:
			//
			//   Microsoft.Maui.Controls.Handlers.TabbedPageManager/Listeners
			//      System.Void AndroidX.ViewPager.Widget.ViewPager/IOnPageChangeListener::OnPageSelected(System.Int32)
			//      System.Void AndroidX.ViewPager2.Widget.ViewPager2/OnPageChangeCallback::OnPageSelected(System.Int32)
			//
			// Both of the above methods will have the same native implementation and symbol name. e.g. (Java type name being `crc649ff77a65592e7d55/TabbedPageManager_Listeners`):
			//     Java_crc649ff77a65592e7d55_TabbedPageManager_1Listeners_n_1onPageSelected__I
			//
			// We need to de-duplicate the entries or the generated native code will fail to build.
			var seenNativeSymbols = new HashSet<string> (StringComparer.Ordinal);
			methods = new List<MarshalMethodInfo> ();

			foreach (MarshalMethodInfo method in allMethods) {
				if (seenNativeSymbols.Contains (method.NativeSymbolName)) {
					// TODO: log properly
					Console.WriteLine ($"Removed MM duplicate '{method.NativeSymbolName}' (implemented: {method.Method.ImplementedMethod.FullName}; registered: {method.Method.RegisteredMethod.FullName}");
					continue;
				}

				seenNativeSymbols.Add (method.NativeSymbolName);
				methods.Add (method);
			}
		}

		string MakeNativeSymbolName (MarshalMethodEntry entry, bool useFullNativeSignature)
		{
			var sb = new StringBuilder ("Java_");
			sb.Append (MangleForJni (entry.JniTypeName));
			sb.Append ('_');
			sb.Append (MangleForJni ($"n_{entry.JniMethodName}"));

			if (useFullNativeSignature) {
				Console.WriteLine ("  Using FULL signature");
				string signature = entry.JniMethodSignature;
				if (signature.Length < 2) {
					ThrowInvalidSignature (signature, "must be at least two characters long");
				}

				if (signature[0] != '(') {
					ThrowInvalidSignature (signature, "must start with '('");
				}

				int sigEndIdx = signature.LastIndexOf (')');
				if (sigEndIdx < 1) { // the first position where ')' can appear is 1, for a method without parameters
					ThrowInvalidSignature (signature, "missing closing parenthesis");
				}

				string sigParams = signature.Substring (1, sigEndIdx - 1);
				if (sigParams.Length > 0) {
					sb.Append ("__");
					sb.Append (MangleForJni (sigParams));
				}
			}

			return sb.ToString ();

			void ThrowInvalidSignature (string signature, string reason)
			{
				throw new InvalidOperationException ($"Invalid JNI signature '{signature}': {reason}");
			}
		}

		void ProcessAndAddMethod (List<MarshalMethodInfo> allMethods, MarshalMethodEntry entry, bool useFullNativeSignature, Dictionary<string, int> seenClasses, Dictionary<string, List<MarshalMethodInfo>> overloadedNativeSymbolNames)
		{
			Console.WriteLine ("marshal method:");
			Console.WriteLine ($"  top type: {entry.DeclaringType.FullName}");
			Console.WriteLine ($"  registered method: [{entry.RegisteredMethod.DeclaringType.FullName}] {entry.RegisteredMethod.FullName}");
			Console.WriteLine ($"  implemented method: [{entry.ImplementedMethod.DeclaringType.FullName}] {entry.ImplementedMethod.FullName}");
			Console.WriteLine ($"  native callback: {entry.NativeCallback.FullName}");
			Console.WriteLine ($"  connector: {entry.Connector.FullName}");
			Console.WriteLine ($"  JNI name: {entry.JniMethodName}");
			Console.WriteLine ($"  JNI signature: {entry.JniMethodSignature}");

			string nativeSymbolName = MakeNativeSymbolName (entry, useFullNativeSignature);
			string klass = $"{entry.NativeCallback.DeclaringType.FullName}, {entry.NativeCallback.Module.Assembly.FullName}";
			Console.WriteLine ($"  klass == {klass}");
			if (!seenClasses.TryGetValue (klass, out int classIndex)) {
				classIndex = classes.Count;
				seenClasses.Add (klass, classIndex);

				var mc = new MarshalMethodsManagedClass {
					token = entry.NativeCallback.DeclaringType.MetadataToken.ToUInt32 (),
					ClassName = klass,
				};

				classes.Add (new StructureInstance<MarshalMethodsManagedClass> (mc));
			}

			Console.WriteLine ("  about to parse JNI sig");
			(Type returnType, List<LlvmIrFunctionParameter>? parameters) = ParseJniSignature (entry.JniMethodSignature, entry.ImplementedMethod);
			Console.WriteLine ("  parsed!");

			var method = new MarshalMethodInfo (entry, returnType, nativeSymbolName: nativeSymbolName, classIndex);
			if (parameters != null && parameters.Count > 0) {
				method.Parameters.AddRange (parameters);
			}

			Console.WriteLine ($"  Generated native symbol: {method.NativeSymbolName}");
			Console.WriteLine ($"  Parsed return type: {returnType}");
			if (method.Parameters.Count > 0) {
				Console.WriteLine ("  Parsed parameters:");
				foreach (LlvmIrFunctionParameter p in method.Parameters) {
					Console.WriteLine ($"    {p.Type} {p.Name}");
				}
			}
			Console.WriteLine ();

			if (!overloadedNativeSymbolNames.TryGetValue (method.NativeSymbolName, out List<MarshalMethodInfo> overloadedMethods)) {
				overloadedMethods = new List<MarshalMethodInfo> ();
				overloadedNativeSymbolNames.Add (method.NativeSymbolName, overloadedMethods);
			}
			overloadedMethods.Add (method);

			allMethods.Add (method);
		}

		string MangleForJni (string name)
		{
			Console.WriteLine ($"    mangling '{name}'");
			var sb = new StringBuilder (name);

			sb.Replace ("_", "_1");
			sb.Replace ('/', '_');
			sb.Replace (";", "_2");
			sb.Replace ("[", "_3");
			// TODO: process unicode chars

			return sb.ToString ();
		}

		(Type returnType, List<LlvmIrFunctionParameter>? functionParams) ParseJniSignature (string signature, Mono.Cecil.MethodDefinition implementedMethod)
		{
			Type returnType = null;
			List<LlvmIrFunctionParameter>? parameters = null;
			bool paramsDone = false;
			int idx = 0;
			while (!paramsDone && idx < signature.Length) {
				char jniType = signature[idx];

				if (jniType == '(') {
					idx++;
					continue;
				}

				if (jniType == ')') {
					paramsDone = true;
					continue;
				}

				Type? managedType = JniTypeToManaged (jniType);
				if (managedType != null) {
					AddParameter (managedType);
					continue;
				}

				throw new InvalidOperationException ($"Unsupported JNI type '{jniType}' at position {idx} of signature '{signature}'");
			}

			if (!paramsDone || idx >= signature.Length || signature[idx] != ')') {
				throw new InvalidOperationException ($"Missing closing arguments parenthesis: '{signature}'");
			}

			idx++;
			if (signature[idx] == 'V') {
				returnType = typeof(void);
			} else {
				returnType = JniTypeToManaged (signature[idx]);
			}

			return (returnType, parameters);

			Type? JniTypeToManaged (char jniType)
			{
				Console.WriteLine ($"  turning JNI type '{jniType}' into managed type");
				if (jniSimpleTypeMap.TryGetValue (jniType, out Type managedType)) {
					idx++;
					Console.WriteLine ($"    will return {managedType}");
					return managedType;
				}

				if (jniType == 'L') {
					return JavaClassToManaged (justSkip: false);
				}

				if (jniType == '[') {
					Console.WriteLine ("    an array");
					idx++;
					jniType = signature[idx];
					if (jniArrayTypeMap.TryGetValue (jniType, out managedType)) {
						if (jniType == 'L') {
							Console.WriteLine ("    skipping");
							JavaClassToManaged (justSkip: true);
						} else {
							idx++;
						}
						Console.WriteLine ($"    will return {managedType}");
						return managedType;
					}

					throw new InvalidOperationException ($"Unsupported JNI array type '{jniType}' at index {idx} of signature '{signature}'");
				}

				Console.WriteLine ("  returning NULL managed type");
				return null;
			}

			Type? JavaClassToManaged (bool justSkip)
			{
				idx++;
				StringBuilder sb = null;
				if (!justSkip) {
					sb = new StringBuilder ();
				}

				while (idx < signature.Length) {
					if (signature[idx] == ')') {
						throw new InvalidOperationException ($"Syntax error: unterminated class type (missing ';' before closing parenthesis) in signature '{signature}'");
					}

					if (signature[idx] == ';') {
						idx++;
						break;
					}

					sb?.Append (signature[idx]);
					idx++;
				}

				if (justSkip) {
					return null;
				}

				string typeName = sb.ToString ();
				if (String.Compare (typeName, "java/lang/Class", StringComparison.Ordinal) == 0) {
					return typeof(_jclass);
				}

				if (String.Compare (typeName, "java/lang/String", StringComparison.Ordinal) == 0) {
					return typeof(_jstring);
				}

				if (String.Compare (typeName, "java/lang/Throwable", StringComparison.Ordinal) == 0) {
					return typeof(_jthrowable);
				}

				return typeof(_jobject);
			}

			void AddParameter (Type type)
			{
				if (parameters == null) {
					parameters = new List<LlvmIrFunctionParameter> ();
				}

				if (implementedMethod.Parameters.Count <= parameters.Count) {
					throw new InvalidOperationException ($"Method {implementedMethod.FullName} managed signature doesn't match its JNI signature '{signature}' (not enough parameters)");
				}

				// Every parameter which isn't a primitive type becomes a pointer
				parameters.Add (new LlvmIrFunctionParameter (type, implementedMethod.Parameters[parameters.Count].Name, isNativePointer: type.IsNativeClass ()));
			}
		}

		protected override void InitGenerator (LlvmIrGenerator generator)
		{
			generator.InitCodeOutput ();
		}

		protected override void MapStructures (LlvmIrGenerator generator)
		{
			monoImage = generator.MapStructure<TypeMappingReleaseNativeAssemblyGenerator.MonoImage> ();
			monoClass = generator.MapStructure<MonoClass> ();
			marshalMethodsClass = generator.MapStructure<MarshalMethodsManagedClass> ();
			_jniEnvSI = generator.MapStructure<_JNIEnv> ();
			_jobjectSI = generator.MapStructure<_jobject> ();
			_jclassSI = generator.MapStructure<_jclass> ();
			_jstringSI = generator.MapStructure<_jstring> ();
			_jthrowableSI = generator.MapStructure<_jthrowable> ();
			_jarraySI = generator.MapStructure<_jarray> ();
			_jobjectArraySI = generator.MapStructure<_jobjectArray> ();
			_jbooleanArraySI = generator.MapStructure<_jbooleanArray> ();
			_jbyteArraySI = generator.MapStructure<_jbyteArray> ();
			_jcharArraySI = generator.MapStructure<_jcharArray> ();
			_jshortArraySI = generator.MapStructure<_jshortArray> ();
			_jintArraySI = generator.MapStructure<_jintArray> ();
			_jlongArraySI = generator.MapStructure<_jlongArray> ();
			_jfloatArraySI = generator.MapStructure<_jfloatArray> ();
			_jdoubleArraySI = generator.MapStructure<_jdoubleArray> ();
		}

		protected override void Write (LlvmIrGenerator generator)
		{
			Dictionary<string, uint> asmNameToIndex = WriteAssemblyImageCache (generator);
			WriteClassCache (generator);
			LlvmIrVariableReference get_function_pointer_ref = WriteXamarinAppInitFunction (generator);
			WriteNativeMethods (generator, asmNameToIndex, get_function_pointer_ref);
		}

		void WriteNativeMethods (LlvmIrGenerator generator, Dictionary<string, uint> asmNameToIndex, LlvmIrVariableReference get_function_pointer_ref)
		{
			if (methods == null || methods.Count == 0) {
				return;
			}

			var usedBackingFields = new HashSet<string> (StringComparer.Ordinal);
			foreach (MarshalMethodInfo mmi in methods) {
				string asmName = mmi.Method.NativeCallback.DeclaringType.Module.Assembly.Name.Name;
				if (!asmNameToIndex.TryGetValue (asmName, out uint asmIndex)) {
					throw new InvalidOperationException ($"Unable to translate assembly name '{asmName}' to its index");
				}
				mmi.AssemblyCacheIndex = asmIndex;
				WriteMarshalMethod (generator, mmi, get_function_pointer_ref, usedBackingFields);
			}
		}

		void WriteMarshalMethod (LlvmIrGenerator generator, MarshalMethodInfo method, LlvmIrVariableReference get_function_pointer_ref, HashSet<string> usedBackingFields)
		{
			var backingFieldSignature = new LlvmNativeFunctionSignature (
				returnType: method.ReturnType,
				parameters: method.Parameters
			) {
				FieldValue = "null",
			};

			string backingFieldName = $"native_cb_{method.Method.JniMethodName}_{method.AssemblyCacheIndex}_{method.ClassCacheIndex}_{method.Method.NativeCallback.MetadataToken.ToUInt32():x}";
			var backingFieldRef = new LlvmIrVariableReference (backingFieldSignature, backingFieldName, isGlobal: true);

			if (!usedBackingFields.Contains (backingFieldName)) {
				generator.WriteVariable (backingFieldName, backingFieldSignature, LlvmIrVariableOptions.LocalWritableInsignificantAddr);
				usedBackingFields.Add (backingFieldName);
			}

			var func = new LlvmIrFunction (
				name: method.NativeSymbolName,
				returnType: method.ReturnType,
				attributeSetID: LlvmIrGenerator.FunctionAttributesJniMethods,
				parameters: method.Parameters
			);

			generator.WriteFunctionStart (func);

			LlvmIrFunctionLocalVariable callbackVariable1 = generator.EmitLoadInstruction (func, backingFieldRef, "cb1");
			var callbackVariable1Ref = new LlvmIrVariableReference (callbackVariable1, isGlobal: false);

			LlvmIrFunctionLocalVariable isNullVariable = generator.EmitIcmpInstruction (func, LlvmIrIcmpCond.Equal, callbackVariable1Ref, expectedValue: "null", resultVariableName: "isNull");
			var isNullVariableRef = new LlvmIrVariableReference (isNullVariable, isGlobal: false);

			const string loadCallbackLabel = "loadCallback";
			const string callbackLoadedLabel = "callbackLoaded";

			generator.EmitBrInstruction (func, isNullVariableRef, loadCallbackLabel, callbackLoadedLabel);

			generator.WriteEOL ();
			generator.EmitLabel (func, loadCallbackLabel);
			LlvmIrFunctionLocalVariable getFunctionPointerVariable = generator.EmitLoadInstruction (func, get_function_pointer_ref, "get_func_ptr");
			var getFunctionPtrRef = new LlvmIrVariableReference (getFunctionPointerVariable, isGlobal: false);

			generator.EmitCall (
				func,
				getFunctionPtrRef,
				new List<LlvmIrFunctionArgument> {
					new LlvmIrFunctionArgument (typeof(uint), method.AssemblyCacheIndex),
					new LlvmIrFunctionArgument (typeof(uint), method.ClassCacheIndex),
					new LlvmIrFunctionArgument (typeof(uint), method.Method.NativeCallback.MetadataToken.ToUInt32 ()),
					new LlvmIrFunctionArgument (typeof(LlvmIrVariableReference), backingFieldRef),
				}
			);

			LlvmIrFunctionLocalVariable callbackVariable2 = generator.EmitLoadInstruction (func, backingFieldRef, "cb2");
			var callbackVariable2Ref = new LlvmIrVariableReference (callbackVariable2, isGlobal: false);

			generator.EmitBrInstruction (func, callbackLoadedLabel);

			generator.WriteEOL ();
			generator.EmitLabel (func, callbackLoadedLabel);

			LlvmIrFunctionLocalVariable fnVariable = generator.EmitPhiInstruction (
				func,
				backingFieldRef,
				new List<(LlvmIrVariableReference variableRef, string label)> {
					(callbackVariable1Ref, func.ImplicitFuncTopLabel),
					(callbackVariable2Ref, loadCallbackLabel),
				},
				resultVariableName: "fn"
			);
			var fnVariableRef = new LlvmIrVariableReference (fnVariable, isGlobal: false);

			LlvmIrFunctionLocalVariable? result = generator.EmitCall (
				func,
				fnVariableRef,
				func.ParameterVariables.Select (pv => new LlvmIrFunctionArgument (pv)).ToList ()
			);

			if (result != null) {
				generator.EmitReturnInstruction (func, result);
			}

			generator.WriteFunctionEnd (func);
		}

		LlvmIrVariableReference WriteXamarinAppInitFunction (LlvmIrGenerator generator)
		{
			var get_function_pointer_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(void),
				parameters: new List<LlvmIrFunctionParameter> {
					new LlvmIrFunctionParameter (typeof(uint), "mono_image_index"),
					new LlvmIrFunctionParameter (typeof(uint), "class_index"),
					new LlvmIrFunctionParameter (typeof(uint), "method_token"),
					new LlvmIrFunctionParameter (typeof(IntPtr), "target_ptr", isNativePointer: true, isCplusPlusReference: true)
				}
			) {
				FieldValue = "null",
			};

			const string GetFunctionPointerFieldName = "get_function_pointer";
			generator.WriteVariable (GetFunctionPointerFieldName, get_function_pointer_sig, LlvmIrVariableOptions.LocalWritableInsignificantAddr);

			var fnParameter = new LlvmIrFunctionParameter (get_function_pointer_sig, "fn");
			var func = new LlvmIrFunction (
				name: "xamarin_app_init",
				returnType: typeof (void),
				attributeSetID: LlvmIrGenerator.FunctionAttributesXamarinAppInit,
				parameters: new List<LlvmIrFunctionParameter> {
					fnParameter,
				}
			);

			generator.WriteFunctionStart (func);
			generator.EmitStoreInstruction (func, fnParameter, new LlvmIrVariableReference (get_function_pointer_sig, GetFunctionPointerFieldName, isGlobal: true));
			generator.WriteFunctionEnd (func);

			return new LlvmIrVariableReference (get_function_pointer_sig, GetFunctionPointerFieldName, isGlobal: true);
		}

		void WriteClassCache (LlvmIrGenerator generator)
		{
			uint marshal_methods_number_of_classes = (uint)classes.Count;

			generator.WriteVariable (nameof (marshal_methods_number_of_classes), marshal_methods_number_of_classes);
			generator.WriteStructureArray (marshalMethodsClass, classes,  LlvmIrVariableOptions.GlobalWritable, "marshal_methods_class_cache");
		}

		Dictionary<string, uint> WriteAssemblyImageCache (LlvmIrGenerator generator)
		{
			if (UniqueAssemblyNames == null) {
				throw new InvalidOperationException ("Internal error: unique assembly names not provided");
			}

			if (UniqueAssemblyNames.Count != NumberOfAssembliesInApk) {
				throw new InvalidOperationException ("Internal error: number of assemblies in the apk doesn't match the number of unique assembly names");
			}

			bool is64Bit = generator.Is64Bit;
			generator.WriteStructureArray (monoImage, (ulong)NumberOfAssembliesInApk, "assembly_image_cache", isArrayOfPointers: true);

			var asmNameToIndex = new Dictionary<string, uint> (StringComparer.Ordinal);
			if (is64Bit) {
				WriteHashes<ulong> ();
			} else {
				WriteHashes<uint> ();
			}

			return asmNameToIndex;

			void WriteHashes<T> () where T: struct
			{
				var hashes = new Dictionary<T, (string name, uint index)> ();
				uint index = 0;
				foreach (string name in UniqueAssemblyNames) {
					string clippedName = Path.GetFileNameWithoutExtension (name);
					ulong hashFull = HashName (name, is64Bit);
					ulong hashClipped = HashName (clippedName, is64Bit);

					//
					// If the number of name forms changes, xamarin-app.hh MUST be updated to set value of the
					// `number_of_assembly_name_forms_in_image_cache` constant to the number of forms.
					//
					hashes.Add ((T)Convert.ChangeType (hashFull, typeof(T)), (name, index));
					hashes.Add ((T)Convert.ChangeType (hashClipped, typeof(T)), (clippedName, index));

					index++;
				}
				List<T> keys = hashes.Keys.ToList ();
				keys.Sort ();

				generator.WriteCommentLine ("Each entry maps hash of an assembly name to an index into the `assembly_image_cache` array");
				generator.WriteArray (
					keys,
					LlvmIrVariableOptions.GlobalConstant,
					"assembly_image_cache_hashes",
					(int idx, T value) => $"{idx}: {hashes[value].name} => 0x{value:x} => {hashes[value].index}"
				);

				var indices = new List<uint> ();
				for (int i = 0; i < keys.Count; i++) {
					(string name, uint idx) = hashes[keys[i]];
					indices.Add (idx);
					asmNameToIndex.Add (name, idx);
				}
				generator.WriteArray (
					indices,
					LlvmIrVariableOptions.GlobalConstant,
					"assembly_image_cache_indices"
				);
			}
		}
	}
}
#endif
