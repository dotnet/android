using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tasks.LLVMIR;

using CecilMethodDefinition = global::Mono.Cecil.MethodDefinition;
using CecilParameterDefinition = global::Mono.Cecil.ParameterDefinition;

// TODO: generate code to check for pending Java exceptions (maybe?)
// TODO: check whether delegates not converted to marshale methods work correctly.  It's possible something isn't called when it should be and that's
//      why Blazor hangs.
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

		sealed class MarshalMethodNameDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var methodName = EnsureType<MarshalMethodName> (data);

				if (String.Compare ("id", fieldName, StringComparison.Ordinal) == 0) {
					return $"id 0x{methodName.id:x}; name: {methodName.name}";
				}

				return String.Empty;
			}
		}

		[NativeAssemblerStructContextDataProvider (typeof(MarshalMethodNameDataProvider))]
		sealed class MarshalMethodName
		{
			[NativeAssembler (UsesDataProvider = true)]
			public ulong  id;
			public string name;
		}

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

		const string mm_trace_func_enter_name = "_mm_trace_func_enter";
		const string mm_trace_func_leave_name = "_mm_trace_func_leave";
		const string asprintf_name = "asprintf";
		const string free_name = "free";

		ICollection<string> uniqueAssemblyNames;
		int numberOfAssembliesInApk;
		IDictionary<string, IList<MarshalMethodEntry>> marshalMethods;
		TaskLoggingHelper logger;

		StructureInfo<TypeMappingReleaseNativeAssemblyGenerator.MonoImage> monoImage;
		StructureInfo<MarshalMethodsManagedClass> marshalMethodsClass;
		StructureInfo<MarshalMethodName> marshalMethodName;
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

		// Tracing
		List<LlvmIrFunctionParameter>? mm_trace_func_enter_or_leave_params;
		List<LlvmIrFunctionParameter>? get_function_pointer_params;
		LlvmIrVariableReference? mm_trace_func_enter_ref;
		LlvmIrVariableReference? mm_trace_func_leave_ref;
		LlvmIrVariableReference? asprintf_ref;
		LlvmIrVariableReference? free_ref;

		readonly bool generateEmptyCode;
		readonly MarshalMethodsTracingMode tracingMode;

		/// <summary>
		/// Constructor to be used ONLY when marshal methods are DISABLED
		/// </summary>
		public MarshalMethodsNativeAssemblyGenerator (int numberOfAssembliesInApk, ICollection<string> uniqueAssemblyNames)
		{
			this.numberOfAssembliesInApk = numberOfAssembliesInApk;
			this.uniqueAssemblyNames = uniqueAssemblyNames ?? throw new ArgumentNullException (nameof (uniqueAssemblyNames));
			generateEmptyCode = true;
		}

		/// <summary>
		/// Constructor to be used ONLY when marshal methods are ENABLED
		/// </summary>
		public MarshalMethodsNativeAssemblyGenerator (int numberOfAssembliesInApk, ICollection<string> uniqueAssemblyNames, IDictionary<string, IList<MarshalMethodEntry>> marshalMethods, TaskLoggingHelper logger, MarshalMethodsTracingMode tracingMode)
		{
			this.numberOfAssembliesInApk = numberOfAssembliesInApk;
			this.uniqueAssemblyNames = uniqueAssemblyNames ?? throw new ArgumentNullException (nameof (uniqueAssemblyNames));
			this.marshalMethods = marshalMethods;
			this.logger = logger ?? throw new ArgumentNullException (nameof (logger));

			generateEmptyCode = false;
			this.tracingMode = tracingMode;
		}

		public override void Init ()
		{
			if (generateEmptyCode || marshalMethods == null || marshalMethods.Count == 0) {
				return;
			}

			var seenClasses = new Dictionary<string, int> (StringComparer.Ordinal);
			var allMethods = new List<MarshalMethodInfo> ();

			// It's possible that several otherwise different methods (from different classes, but with the same
			// names and similar signatures) will actually share the same **short** native symbol name. In this case we must
			// ensure that they all use long symbol names.  This has to be done as a post-processing step, after we
			// have already iterated over the entire method collection.
			//
			// A handful of examples from the Hello World MAUI app:
			//
			// Overloaded MM: Java_crc64e1fb321c08285b90_CellAdapter_n_1onActionItemClicked
			//   implemented in: Microsoft.Maui.Controls.Handlers.Compatibility.CellAdapter (System.Boolean Android.Views.ActionMode/ICallback::OnActionItemClicked(Android.Views.ActionMode,Android.Views.IMenuItem))
			//   implemented in: Microsoft.Maui.Controls.Handlers.Compatibility.CellAdapter (System.Boolean AndroidX.AppCompat.View.ActionMode/ICallback::OnActionItemClicked(AndroidX.AppCompat.View.ActionMode,Android.Views.IMenuItem))
			//   new native symbol name: Java_crc64e1fb321c08285b90_CellAdapter_n_1onActionItemClicked__Landroidx_appcompat_view_ActionMode_2Landroid_view_MenuItem_2
			//
			// Overloaded MM: Java_crc64e1fb321c08285b90_CellAdapter_n_1onCreateActionMode
			//   implemented in: Microsoft.Maui.Controls.Handlers.Compatibility.CellAdapter (System.Boolean Android.Views.ActionMode/ICallback::OnCreateActionMode(Android.Views.ActionMode,Android.Views.IMenu))
			//   implemented in: Microsoft.Maui.Controls.Handlers.Compatibility.CellAdapter (System.Boolean AndroidX.AppCompat.View.ActionMode/ICallback::OnCreateActionMode(AndroidX.AppCompat.View.ActionMode,Android.Views.IMenu))
			//   new native symbol name: Java_crc64e1fb321c08285b90_CellAdapter_n_1onCreateActionMode__Landroidx_appcompat_view_ActionMode_2Landroid_view_Menu_2
			//
			// Overloaded MM: Java_crc64e1fb321c08285b90_CellAdapter_n_1onDestroyActionMode
			//   implemented in: Microsoft.Maui.Controls.Handlers.Compatibility.CellAdapter (System.Void Android.Views.ActionMode/ICallback::OnDestroyActionMode(Android.Views.ActionMode))
			//   implemented in: Microsoft.Maui.Controls.Handlers.Compatibility.CellAdapter (System.Void AndroidX.AppCompat.View.ActionMode/ICallback::OnDestroyActionMode(AndroidX.AppCompat.View.ActionMode))
			//   new native symbol name: Java_crc64e1fb321c08285b90_CellAdapter_n_1onDestroyActionMode__Landroidx_appcompat_view_ActionMode_2
			//
			// Overloaded MM: Java_crc64e1fb321c08285b90_CellAdapter_n_1onPrepareActionMode
			//   implemented in: Microsoft.Maui.Controls.Handlers.Compatibility.CellAdapter (System.Boolean Android.Views.ActionMode/ICallback::OnPrepareActionMode(Android.Views.ActionMode,Android.Views.IMenu))
			//   implemented in: Microsoft.Maui.Controls.Handlers.Compatibility.CellAdapter (System.Boolean AndroidX.AppCompat.View.ActionMode/ICallback::OnPrepareActionMode(AndroidX.AppCompat.View.ActionMode,Android.Views.IMenu))
			//   new native symbol name: Java_crc64e1fb321c08285b90_CellAdapter_n_1onPrepareActionMode__Landroidx_appcompat_view_ActionMode_2Landroid_view_Menu_2
			//
			var overloadedNativeSymbolNames = new Dictionary<string, List<MarshalMethodInfo>> (StringComparer.Ordinal);
			foreach (IList<MarshalMethodEntry> entryList in marshalMethods.Values) {
				bool useFullNativeSignature = entryList.Count > 1;
				foreach (MarshalMethodEntry entry in entryList) {
					ProcessAndAddMethod (allMethods, entry, useFullNativeSignature, seenClasses, overloadedNativeSymbolNames);
				}
			}

			foreach (List<MarshalMethodInfo> mmiList in overloadedNativeSymbolNames.Values) {
				if (mmiList.Count <= 1) {
					continue;
				}

				foreach (MarshalMethodInfo overloadedMethod in mmiList) {
					overloadedMethod.NativeSymbolName = MakeNativeSymbolName (overloadedMethod.Method, useFullNativeSignature: true);
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
					logger.LogDebugMessage ($"Removed MM duplicate '{method.NativeSymbolName}' (implemented: {method.Method.ImplementedMethod.FullName}; registered: {method.Method.RegisteredMethod.FullName}");
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
			CecilMethodDefinition nativeCallback = entry.NativeCallback;
			string nativeSymbolName = MakeNativeSymbolName (entry, useFullNativeSignature);
			string klass = $"{nativeCallback.DeclaringType.FullName}, {nativeCallback.Module.Assembly.FullName}";

			if (!seenClasses.TryGetValue (klass, out int classIndex)) {
				classIndex = classes.Count;
				seenClasses.Add (klass, classIndex);

				var mc = new MarshalMethodsManagedClass {
					token = nativeCallback.DeclaringType.MetadataToken.ToUInt32 (),
					ClassName = klass,
				};

				classes.Add (new StructureInstance<MarshalMethodsManagedClass> (mc));
			}

			// Methods with `IsSpecial == true` are "synthetic" methods - they contain only the callback reference
			(Type returnType, List<LlvmIrFunctionParameter>? parameters) = ParseJniSignature (entry.JniMethodSignature, entry.IsSpecial ? entry.NativeCallback : entry.ImplementedMethod);

			var method = new MarshalMethodInfo (entry, returnType, nativeSymbolName: nativeSymbolName, classIndex);
			if (parameters != null && parameters.Count > 0) {
				method.Parameters.AddRange (parameters);
			}

			if (!overloadedNativeSymbolNames.TryGetValue (method.NativeSymbolName, out List<MarshalMethodInfo> overloadedMethods)) {
				overloadedMethods = new List<MarshalMethodInfo> ();
				overloadedNativeSymbolNames.Add (method.NativeSymbolName, overloadedMethods);
			}
			overloadedMethods.Add (method);

			allMethods.Add (method);
		}

		string MangleForJni (string name)
		{
			var sb = new StringBuilder ();

			foreach (char ch in name) {
				switch (ch) {
					case '/':
					case '.':
						sb.Append ('_');
						break;

					case '_':
						sb.Append ("_1");
						break;

					case ';':
						sb.Append ("_2");
						break;

					case '[':
						sb.Append ("_3");
						break;

					default:
						if ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) {
							sb.Append (ch);
						} else {
							sb.Append ("_0");
							sb.Append (((int)ch).ToString ("x04"));
						}
						break;
				}
			}

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
				if (jniSimpleTypeMap.TryGetValue (jniType, out Type managedType)) {
					idx++;
					return managedType;
				}

				if (jniType == 'L') {
					return JavaClassToManaged (justSkip: false);
				}

				if (jniType == '[') {
					// Arrays of arrays (any rank) are bound as a simple pointer, which makes the generated code much simpler (no need to generate pointers to
					// pointers to pointers etc), especially that we don't need to dereference these pointers in generated code, we simply pass them along to
					// the managed land after all.
					while (signature[idx] == '[') {
						idx++;
					}

					jniType = signature[idx];
					if (jniArrayTypeMap.TryGetValue (jniType, out managedType)) {
						if (jniType == 'L') {
							JavaClassToManaged (justSkip: true);
						} else {
							idx++;
						}

						return managedType;
					}

					throw new InvalidOperationException ($"Unsupported JNI array type '{jniType}' at index {idx} of signature '{signature}'");
				}

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
			marshalMethodName = generator.MapStructure<MarshalMethodName> ();
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
			WriteAssemblyImageCache (generator, out Dictionary<string, uint> asmNameToIndex);
			WriteClassCache (generator);
			WriteInitTracing (generator);
			LlvmIrVariableReference get_function_pointer_ref = WriteXamarinAppInitFunction (generator);
			WriteNativeMethods (generator, asmNameToIndex, get_function_pointer_ref);

			var mm_class_names = new List<string> ();
			foreach (StructureInstance<MarshalMethodsManagedClass> klass in classes) {
				mm_class_names.Add (klass.Obj.ClassName);
			}
			generator.WriteArray (mm_class_names, "mm_class_names", "Names of classes in which marshal methods reside");

			var uniqueMethods = new Dictionary<ulong, MarshalMethodInfo> ();
			if (!generateEmptyCode && methods != null) {
				foreach (MarshalMethodInfo mmi in methods) {
					string asmName = Path.GetFileName (mmi.Method.NativeCallback.Module.Assembly.MainModule.FileName);
					if (!asmNameToIndex.TryGetValue (asmName, out uint idx)) {
						throw new InvalidOperationException ($"Internal error: failed to match assembly name '{asmName}' to cache array index");
					}

					ulong id = ((ulong)idx << 32) | (ulong)mmi.Method.NativeCallback.MetadataToken.ToUInt32 ();
					if (uniqueMethods.ContainsKey (id)) {
						continue;
					}
					uniqueMethods.Add (id, mmi);
				}
			}

			MarshalMethodName name;
			var methodName = new StringBuilder ();
			var mm_method_names = new List<StructureInstance<MarshalMethodName>> ();
			foreach (var kvp in uniqueMethods) {
				ulong id = kvp.Key;
				MarshalMethodInfo mmi = kvp.Value;

				RenderMethodNameWithParams (mmi.Method.NativeCallback, methodName);
				name = new MarshalMethodName {
					// Tokens are unique per assembly
					id = id,
					name = methodName.ToString (),
				};
				mm_method_names.Add (new StructureInstance<MarshalMethodName> (name));
			}

			// Must terminate with an "invalid" entry
			name = new MarshalMethodName {
				id = 0,
				name = String.Empty,
			};
			mm_method_names.Add (new StructureInstance<MarshalMethodName> (name));

			generator.WriteStructureArray (marshalMethodName, mm_method_names, LlvmIrVariableOptions.GlobalConstant, "mm_method_names");

			void RenderMethodNameWithParams (CecilMethodDefinition md, StringBuilder buffer)
			{
				buffer.Clear ();
				buffer.Append (md.Name);
				buffer.Append ('(');

				if (md.HasParameters) {
					bool first = true;
					foreach (CecilParameterDefinition pd in md.Parameters) {
						if (!first) {
							buffer.Append (',');
						} else {
							first = false;
						}

						buffer.Append (pd.ParameterType.Name);
					}
				}

				buffer.Append (')');
			}
		}

		void WriteInitTracing (LlvmIrGenerator generator)
		{
			if (tracingMode == MarshalMethodsTracingMode.None) {
				return;
			}

			// Function names and declarations must match those in src/monodroid/jni/marshal-methods-tracing.hh
			mm_trace_func_enter_or_leave_params = new List<LlvmIrFunctionParameter> {
				new LlvmIrFunctionParameter (typeof(_JNIEnv), "env", isNativePointer: true), // JNIEnv *env
				new LlvmIrFunctionParameter (typeof(int), "tracing_mode"),
				new LlvmIrFunctionParameter (typeof(uint), "mono_image_index"),
				new LlvmIrFunctionParameter (typeof(uint), "class_index"),
				new LlvmIrFunctionParameter (typeof(uint), "method_token"),
				new LlvmIrFunctionParameter (typeof(string), "native_method_name"),
			};

			var mm_trace_func_enter_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(void),
				parameters: mm_trace_func_enter_or_leave_params

			);
			mm_trace_func_enter_ref = new LlvmIrVariableReference (mm_trace_func_enter_sig, mm_trace_func_enter_name, isGlobal: true);

			var mm_trace_func_leave_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(void),
				parameters: mm_trace_func_enter_or_leave_params
			);
			mm_trace_func_leave_ref = new LlvmIrVariableReference (mm_trace_func_leave_sig, mm_trace_func_leave_name, isGlobal: true);

			var asprintf_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(int),
				parameters: new List<LlvmIrFunctionParameter> {
					new LlvmIrFunctionParameter (typeof(string), isNativePointer: true) {
						NoUndef = true,
					},
					new LlvmIrFunctionParameter (typeof(string)) {
						NoUndef = true,
					},
					new LlvmIrFunctionParameter (typeof(void)) {
						IsVarargs = true,
					}
			        }
			);
			asprintf_ref = new LlvmIrVariableReference (asprintf_sig, asprintf_name, isGlobal: true);

			var free_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(void),
				parameters: new List<LlvmIrFunctionParameter> {
					new LlvmIrFunctionParameter (typeof(string)) {
						NoCapture = true,
						NoUndef = true,
					},
			        }
			);
			free_ref = new LlvmIrVariableReference (free_sig, free_name, isGlobal: true);

			AddTraceFunctionDeclaration (asprintf_name, asprintf_sig, LlvmIrGenerator.FunctionAttributesJniMethods);
			AddTraceFunctionDeclaration (free_name, free_sig, LlvmIrGenerator.FunctionAttributesLibcFree);
			AddTraceFunctionDeclaration (mm_trace_func_enter_name, mm_trace_func_enter_sig, LlvmIrGenerator.FunctionAttributesJniMethods);
			AddTraceFunctionDeclaration (mm_trace_func_leave_name, mm_trace_func_leave_sig, LlvmIrGenerator.FunctionAttributesJniMethods);

			void AddTraceFunctionDeclaration (string name, LlvmNativeFunctionSignature sig, int attributeSetID)
			{
				var func = new LlvmIrFunction (
					name: name,
					returnType: sig.ReturnType,
					attributeSetID: attributeSetID,
					parameters: sig.Parameters
				);
				generator.AddExternalFunction (func);
			}
		}

		void WriteNativeMethods (LlvmIrGenerator generator, Dictionary<string, uint> asmNameToIndex, LlvmIrVariableReference get_function_pointer_ref)
		{
			if (generateEmptyCode || methods == null || methods.Count == 0) {
				return;
			}

			var usedBackingFields = new HashSet<string> (StringComparer.Ordinal);
			foreach (MarshalMethodInfo mmi in methods) {
				CecilMethodDefinition nativeCallback = mmi.Method.NativeCallback;
				string asmName = nativeCallback.DeclaringType.Module.Assembly.Name.Name;
				if (!asmNameToIndex.TryGetValue (asmName, out uint asmIndex)) {
					throw new InvalidOperationException ($"Unable to translate assembly name '{asmName}' to its index");
				}
				mmi.AssemblyCacheIndex = asmIndex;
				WriteMarshalMethod (generator, mmi, get_function_pointer_ref, usedBackingFields);
			}
		}

		(string asprintfFormat, List<Type?> paramUpcast) GetPrintfFormatForFunctionParams (LlvmIrFunction func)
		{
			var ret = new StringBuilder ("(");
			bool first = true;

			var upcast = new List<Type?> ();
			foreach (LlvmIrFunctionParameter parameter in func.Parameters) {
				if (!first) {
					ret.Append (", ");
				} else {
					first = false;
				}

				string format;
				if (parameter.Type == typeof(string)) {
					format = "\"%s\"";
					upcast.Add (null);
				} else if (parameter.Type == typeof(IntPtr) || typeof(_jobject).IsAssignableFrom (parameter.Type) || parameter.Type == typeof(_JNIEnv)) {
					format = "%p";
					upcast.Add (null);
				} else if (parameter.Type == typeof(bool) || parameter.Type == typeof(byte) || parameter.Type == typeof(ushort)) {
					format = "%u";
					upcast.Add (typeof(uint));
				} else if (parameter.Type == typeof(sbyte) || parameter.Type == typeof(short)) {
					format = "%d";
					upcast.Add (typeof(int));
				} else if (parameter.Type == typeof(char)) {
					format = "'\\%x'";
					upcast.Add (typeof(uint));
				} else if (parameter.Type == typeof(int)) {
					format = "%d";
					upcast.Add (null);
				} else if (parameter.Type == typeof(uint)) {
					format = "%u";
					upcast.Add (null);
				} else if (parameter.Type == typeof(long)) {
					format = "%ld";
					upcast.Add (null);
				} else if (parameter.Type == typeof(ulong)) {
					format = "%lu";
					upcast.Add (null);
				} else if (parameter.Type == typeof(float)) {
					format = "%g";
					upcast.Add (typeof(double));
				} else if (parameter.Type == typeof(double)) {
					format = "%g";
					upcast.Add (null);
				} else {
					throw new InvalidOperationException ($"Unsupported type '{parameter.Type}'");
				};

				ret.Append (format);
			}

			ret.Append (')');
			return (ret.ToString (), upcast);
		}

		LlvmIrVariableReference WriteAsprintfCall (LlvmIrGenerator generator, LlvmIrFunction func, string format, List<LlvmIrFunctionArgument> variadicArgs, List<Type?> parameterUpcasts, LlvmIrVariableReference allocatedStringVarRef)
		{
			if (variadicArgs.Count != parameterUpcasts.Count) {
				throw new ArgumentException (nameof (parameterUpcasts), $"Number of upcasts ({parameterUpcasts.Count}) is not equal to the number of variadic arguments ({variadicArgs.Count})");
			}

			LlvmIrGenerator.StringSymbolInfo asprintfFormatSym = generator.AddString (format, $"asprintf_fmt_{func.Name}");

			var asprintf_args = new List<LlvmIrFunctionArgument> {
				new LlvmIrFunctionArgument (allocatedStringVarRef) {
					NonNull = true,
					NoUndef = true,
				},
				new LlvmIrFunctionArgument (asprintfFormatSym) {
					NoUndef = true,
				},
			};

			// TODO: add upcasts code here and update args accordingly
			for (int i = 0; i < variadicArgs.Count; i++) {
				if (parameterUpcasts[i] == null) {
					continue;
				}

				LlvmIrVariableReference paramRef;
				if (variadicArgs[i].Value is LlvmIrFunctionLocalVariable paramVar) {
					paramRef = new LlvmIrVariableReference (paramVar, isGlobal: false);
				} else {
					throw new InvalidOperationException ($"Unexpected argument type {variadicArgs[i].Type}");
				}

				LlvmIrFunctionLocalVariable upcastVar = generator.EmitUpcast (func, paramRef, parameterUpcasts[i]);
			}

			asprintf_args.AddRange (variadicArgs);

			generator.WriteEOL ();
			generator.WriteCommentLine ($"Format: {format}", indent: true);
			LlvmIrFunctionLocalVariable? result = generator.EmitCall (func, asprintf_ref, asprintf_args, marker: LlvmIrCallMarker.None, AttributeSetID: -1);
			LlvmIrVariableReference? resultRef = new LlvmIrVariableReference (result, isGlobal: false);

			// Check whether asprintf returned a negative value (it returns -1 at failure, but we widen the check just in case)
			LlvmIrFunctionLocalVariable asprintfResultVariable = generator.EmitIcmpInstruction (func, LlvmIrIcmpCond.SignedLessThan, resultRef, "0");
			var asprintfResultVariableRef = new LlvmIrVariableReference (asprintfResultVariable, isGlobal: false);

			string asprintfIfThenLabel = func.MakeUniqueLabel ("if.then");
			string asprintfIfElseLabel = func.MakeUniqueLabel ("if.else");
			string ifElseDoneLabel = func.MakeUniqueLabel ("if.done");

			generator.EmitBrInstruction (func, asprintfResultVariableRef, asprintfIfThenLabel, asprintfIfElseLabel);

			// Condition is true if asprintf **failed**
			generator.EmitLabel (func, asprintfIfThenLabel);
			generator.EmitStoreInstruction<string> (func, allocatedStringVarRef, null);
			generator.EmitBrInstruction (func, ifElseDoneLabel);

			generator.EmitLabel (func, asprintfIfElseLabel);
			LlvmIrFunctionLocalVariable bufferPointerVar = generator.EmitLoadInstruction (func, allocatedStringVarRef);
			LlvmIrVariableReference bufferPointerVarRef = new LlvmIrVariableReference (bufferPointerVar, isGlobal: false);
			generator.EmitBrInstruction (func, ifElseDoneLabel);

			generator.EmitLabel (func, ifElseDoneLabel);
			LlvmIrFunctionLocalVariable allocatedStringValueVar = generator.EmitPhiInstruction (
				func,
				allocatedStringVarRef,
				new List<(LlvmIrVariableReference? variableRef, string label)> {
					(null, func.PreviousBlockStartLabel),
					(bufferPointerVarRef, func.PreviousBlockEndLabel),
				}
			);

			return new LlvmIrVariableReference (allocatedStringValueVar, isGlobal: false);
		}

		void WriteMarshalMethod (LlvmIrGenerator generator, MarshalMethodInfo method, LlvmIrVariableReference get_function_pointer_ref, HashSet<string> usedBackingFields)
		{
			var backingFieldSignature = new LlvmNativeFunctionSignature (
				returnType: method.ReturnType,
				parameters: method.Parameters
			) {
				FieldValue = "null",
			};

			CecilMethodDefinition nativeCallback = method.Method.NativeCallback;
			string backingFieldName = $"native_cb_{method.Method.JniMethodName}_{method.AssemblyCacheIndex}_{method.ClassCacheIndex}_{nativeCallback.MetadataToken.ToUInt32():x}";
			var backingFieldRef = new LlvmIrVariableReference (backingFieldSignature, backingFieldName, isGlobal: true, isNativePointer: true);

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

			generator.WriteFunctionStart (func, $"Method: {nativeCallback.FullName}\nAssembly: {nativeCallback.Module.Assembly.Name}");

			List<LlvmIrFunctionArgument>? trace_enter_leave_args = null;
			LlvmIrFunctionLocalVariable? tracingParamsStringLifetimeTracker = null;
			List<LlvmIrFunctionArgument>? asprintfVariadicArgs = null;
			LlvmIrVariableReference? asprintfAllocatedStringAccessorRef = null;
			LlvmIrVariableReference? asprintfAllocatedStringVarRef = null;

			if (tracingMode != MarshalMethodsTracingMode.None) {
				const string paramsLocalVarName = "func_params_render";

				generator.WriteCommentLine ("Tracing code start", indent: true);
				(LlvmIrFunctionLocalVariable asprintfAllocatedStringVar, tracingParamsStringLifetimeTracker) = generator.EmitAllocStackVariable (func, typeof(string), paramsLocalVarName);
				asprintfAllocatedStringVarRef = new LlvmIrVariableReference (asprintfAllocatedStringVar, isGlobal: false);
				generator.EmitStoreInstruction<string> (func, asprintfAllocatedStringVarRef, null);

				asprintfVariadicArgs = new List<LlvmIrFunctionArgument> ();
				foreach (LlvmIrFunctionLocalVariable lfv in func.ParameterVariables) {
					asprintfVariadicArgs.Add (
						new LlvmIrFunctionArgument (lfv) {
							NoUndef = true,
						}
					);
				}

				(string asprintfFormat, List<Type?> upcasts) = GetPrintfFormatForFunctionParams (func);
				asprintfAllocatedStringAccessorRef = WriteAsprintfCall (generator, func, asprintfFormat, asprintfVariadicArgs, upcasts, asprintfAllocatedStringVarRef);

				trace_enter_leave_args = new List<LlvmIrFunctionArgument> {
					new LlvmIrFunctionArgument (func.ParameterVariables[0]), // JNIEnv* env
					new LlvmIrFunctionArgument (mm_trace_func_enter_or_leave_params[1], (int)tracingMode),
					new LlvmIrFunctionArgument (mm_trace_func_enter_or_leave_params[2], method.AssemblyCacheIndex),
					new LlvmIrFunctionArgument (mm_trace_func_enter_or_leave_params[3], method.ClassCacheIndex),
					new LlvmIrFunctionArgument (mm_trace_func_enter_or_leave_params[4], nativeCallback.MetadataToken.ToUInt32 ()),
					new LlvmIrFunctionArgument (mm_trace_func_enter_or_leave_params[5], method.NativeSymbolName),
				};

				generator.EmitCall (func, mm_trace_func_enter_ref, trace_enter_leave_args);
				asprintfAllocatedStringVar = generator.EmitLoadInstruction (func, asprintfAllocatedStringVarRef);

				generator.EmitCall (
					func,
					free_ref,
					new List<LlvmIrFunctionArgument> {
						new LlvmIrFunctionArgument (asprintfAllocatedStringVar) {
							NoUndef = true,
						},
					}
				);
				generator.WriteCommentLine ("Tracing code end", indent: true);
				generator.WriteEOL ();
			}

			LlvmIrFunctionLocalVariable callbackVariable1 = generator.EmitLoadInstruction (func, backingFieldRef, "cb1");
			var callbackVariable1Ref = new LlvmIrVariableReference (callbackVariable1, isGlobal: false);

			LlvmIrFunctionLocalVariable isNullVariable = generator.EmitIcmpInstruction (func, LlvmIrIcmpCond.Equal, callbackVariable1Ref, expectedValue: "null", resultVariableName: "isNull");
			var isNullVariableRef = new LlvmIrVariableReference (isNullVariable, isGlobal: false);

			const string loadCallbackLabel = "loadCallback";
			const string callbackLoadedLabel = "callbackLoaded";

			generator.EmitBrInstruction (func, isNullVariableRef, loadCallbackLabel, callbackLoadedLabel);
			generator.EmitLabel (func, loadCallbackLabel);

			LlvmIrFunctionLocalVariable getFunctionPointerVariable = generator.EmitLoadInstruction (func, get_function_pointer_ref, "get_func_ptr");
			var getFunctionPtrRef = new LlvmIrVariableReference (getFunctionPointerVariable, isGlobal: false);

			generator.EmitCall (
				func,
				getFunctionPtrRef,
				new List<LlvmIrFunctionArgument> {
					new LlvmIrFunctionArgument (get_function_pointer_params[0], method.AssemblyCacheIndex),
					new LlvmIrFunctionArgument (get_function_pointer_params[1], method.ClassCacheIndex),
					new LlvmIrFunctionArgument (get_function_pointer_params[2], nativeCallback.MetadataToken.ToUInt32 ()),
					new LlvmIrFunctionArgument (backingFieldRef),
				}
			);

			LlvmIrFunctionLocalVariable callbackVariable2 = generator.EmitLoadInstruction (func, backingFieldRef, "cb2");
			var callbackVariable2Ref = new LlvmIrVariableReference (callbackVariable2, isGlobal: false);

			generator.EmitBrInstruction (func, callbackLoadedLabel);
			generator.EmitLabel (func, callbackLoadedLabel);

			LlvmIrFunctionLocalVariable fnVariable = generator.EmitPhiInstruction (
				func,
				backingFieldRef,
				new List<(LlvmIrVariableReference variableRef, string label)> {
					(callbackVariable1Ref, func.PreviousBlockStartLabel),
					(callbackVariable2Ref, func.PreviousBlockEndLabel),
				},
				resultVariableName: "fn"
			);
			var fnVariableRef = new LlvmIrVariableReference (fnVariable, isGlobal: false);

			LlvmIrFunctionLocalVariable? result = generator.EmitCall (
				func,
				fnVariableRef,
				func.ParameterVariables.Select (pv => new LlvmIrFunctionArgument (pv)).ToList ()
			);

			if (tracingMode != MarshalMethodsTracingMode.None) {
				generator.WriteCommentLine ("Tracing code start", indent: true);

				generator.EmitCall (func, mm_trace_func_leave_ref, trace_enter_leave_args);
				generator.EmitDeallocStackVariable (func, tracingParamsStringLifetimeTracker);

				generator.WriteCommentLine ("Tracing code end", indent: true);
			}

			if (result != null) {
				generator.EmitReturnInstruction (func, result);
			}

			generator.WriteFunctionEnd (func);
		}

		LlvmIrVariableReference WriteXamarinAppInitFunction (LlvmIrGenerator generator)
		{
			get_function_pointer_params = new List<LlvmIrFunctionParameter> {
				new LlvmIrFunctionParameter (typeof(uint), "mono_image_index"),
				new LlvmIrFunctionParameter (typeof(uint), "class_index"),
				new LlvmIrFunctionParameter (typeof(uint), "method_token"),
				new LlvmIrFunctionParameter (typeof(IntPtr), "target_ptr", isNativePointer: true, isCplusPlusReference: true)
			};

			var get_function_pointer_sig = new LlvmNativeFunctionSignature (
				returnType: typeof(void),
				parameters: get_function_pointer_params
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
					new LlvmIrFunctionParameter (typeof(_JNIEnv), "env", isNativePointer: true), // JNIEnv *env
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

		// TODO: this should probably be moved to a separate writer, since not only marshal methods use the cache
		void WriteAssemblyImageCache (LlvmIrGenerator generator, out Dictionary<string, uint> asmNameToIndex)
		{
			bool is64Bit = generator.Is64Bit;
			generator.WriteStructureArray (monoImage, (ulong)numberOfAssembliesInApk, "assembly_image_cache", isArrayOfPointers: true);

			var asmNameToIndexData = new Dictionary<string, uint> (StringComparer.Ordinal);
			if (is64Bit) {
				WriteHashes<ulong> ();
			} else {
				WriteHashes<uint> ();
			}

			asmNameToIndex = asmNameToIndexData;

			void WriteHashes<T> () where T: struct
			{
				var hashes = new Dictionary<T, (string name, uint index)> ();
				uint index = 0;

				foreach (string name in uniqueAssemblyNames) {
					// We must make sure we keep the possible culture prefix, which will be treated as "directory" path here
					string clippedName = Path.Combine (Path.GetDirectoryName (name) ?? String.Empty, Path.GetFileNameWithoutExtension (name));
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
					asmNameToIndexData.Add (name, idx);
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
