#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	abstract partial class MarshalMethodsNativeAssemblyGenerator : LlvmIrComposer
	{
		const string GetFunctionPointerVariableName = "get_function_pointer";

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
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value - likely populated by native code
			public byte b;
#pragma warning restore CS0649
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

		protected sealed class MarshalMethodInfo
		{
			public MarshalMethodEntryObject Method          { get; }
			public string NativeSymbolName                  { get; set; }
			public List<LlvmIrFunctionParameter> Parameters { get; }
			public Type ReturnType                          { get; }
			public uint ClassCacheIndex                     { get; }

			// This one isn't known until the generation time, which happens after we instantiate the class
			// in Init and it may be different between architectures/ABIs, hence it needs to be settable from
			// the outside.
			public uint AssemblyCacheIndex                  { get; set; }

			public MarshalMethodInfo (MarshalMethodEntryObject method, Type returnType, string nativeSymbolName, int classCacheIndex)
			{
				Method = method ?? throw new ArgumentNullException (nameof (method));
				ReturnType = returnType ?? throw new ArgumentNullException (nameof (returnType));
				if (String.IsNullOrEmpty (nativeSymbolName)) {
					throw new ArgumentException ("must not be null or empty", nameof (nativeSymbolName));
				}
				NativeSymbolName = nativeSymbolName;
				Parameters = new List<LlvmIrFunctionParameter> {
					new LlvmIrFunctionParameter (typeof (_JNIEnv), "env"), // JNIEnv *env
					new LlvmIrFunctionParameter (typeof (_jclass), "klass"), // jclass klass
				};
				ClassCacheIndex = (uint)classCacheIndex;
			}
		}

		sealed class MarshalMethodsManagedClassDataProvider : NativeAssemblerStructContextDataProvider
		{
			public override string GetComment (object data, string fieldName)
			{
				var klass = EnsureType<MarshalMethodsManagedClass> (data);

				if (MonoAndroidHelper.StringEquals ("token", fieldName)) {
					return $" class name: {klass.ClassName}";
				}

				return String.Empty;
			}
		}

		[NativeAssemblerStructContextDataProvider (typeof(MarshalMethodsManagedClassDataProvider))]
		protected sealed class MarshalMethodsManagedClass
		{
			[NativeAssembler (UsesDataProvider = true, NumberFormat = LlvmIrVariableNumberFormat.Hexadecimal)]
			public uint       token;

			[NativePointer (IsNull = true)]
			public MonoClass  klass;

			[NativeAssembler (Ignore = true)]
			public string ClassName;
		};

		protected sealed class AssemblyCacheState
		{
			public Dictionary<string, uint> AsmNameToIndexData32 = new (StringComparer.Ordinal);
			public Dictionary<uint, (string name, uint index)> Hashes32 = new ();
			public List<uint> Keys32 = [];
			public List<uint> Indices32 = new ();

			public Dictionary<string, uint> AsmNameToIndexData64 = new (StringComparer.Ordinal);
			public Dictionary<ulong, (string name, uint index)> Hashes64 = new ();
			public List<ulong> Keys64 = [];
			public List<uint> Indices64 = new ();
		}

		sealed class MarshalMethodsWriteState
		{
			public AssemblyCacheState AssemblyCacheState;
			public LlvmIrFunctionAttributeSet AttributeSet;
			public Dictionary<string, ulong> UniqueAssemblyId;
			public Dictionary<string, LlvmIrVariable> UsedBackingFields;
			public LlvmIrVariable GetFunctionPtrVariable;
			public LlvmIrFunction GetFunctionPtrFunction;
		}

		sealed class MarshalMethodAssemblyIndexValuePlaceholder : LlvmIrInstructionArgumentValuePlaceholder
		{
			MarshalMethodInfo mmi;
			AssemblyCacheState acs;

			public MarshalMethodAssemblyIndexValuePlaceholder (MarshalMethodInfo mmi, AssemblyCacheState acs)
			{
				this.mmi = mmi;
				this.acs = acs;
			}

			public override object? GetValue (LlvmIrModuleTarget target)
			{
				// What a monstrosity...
				string asmName = mmi.Method.NativeCallback.DeclaringType.Module.Assembly.NameName;
				Dictionary<string, uint> asmNameToIndex = target.Is64Bit ? acs.AsmNameToIndexData64 : acs.AsmNameToIndexData32;
				if (!asmNameToIndex.TryGetValue (asmName, out uint asmIndex)) {
					throw new InvalidOperationException ($"Unable to translate assembly name '{asmName}' to its index");
				}
				return asmIndex;
			}
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

		readonly ICollection<string> uniqueAssemblyNames;

		StructureInfo marshalMethodsManagedClassStructureInfo;

		List<MarshalMethodInfo> methods;
		protected List<StructureInstance<MarshalMethodsManagedClass>> classes = new List<StructureInstance<MarshalMethodsManagedClass>> ();

#pragma warning disable CS0414 // Field is assigned but its value is never used - might be used for debugging or future functionality
		readonly LlvmIrCallMarker defaultCallMarker;
#pragma warning restore CS0414
		readonly bool generateEmptyCode;
		readonly bool managedMarshalMethodsLookupEnabled;
		readonly AndroidTargetArch targetArch;
		readonly NativeCodeGenStateObject? codeGenState;

		protected bool GenerateEmptyCode => generateEmptyCode;
		protected List<MarshalMethodInfo> Methods => methods;

		/// <summary>
		/// Constructor to be used ONLY when marshal methods are DISABLED
		/// </summary>
		protected MarshalMethodsNativeAssemblyGenerator (TaskLoggingHelper log, AndroidTargetArch targetArch, ICollection<string> uniqueAssemblyNames)
			: base (log)
		{
			this.targetArch = targetArch;
			this.uniqueAssemblyNames = uniqueAssemblyNames ?? throw new ArgumentNullException (nameof (uniqueAssemblyNames));
			generateEmptyCode = true;
			defaultCallMarker = LlvmIrCallMarker.Tail;
		}

		/// <summary>
		/// Constructor to be used ONLY when marshal methods are ENABLED
		/// </summary>
		protected MarshalMethodsNativeAssemblyGenerator (TaskLoggingHelper log, ICollection<string> uniqueAssemblyNames, NativeCodeGenStateObject codeGenState, bool managedMarshalMethodsLookupEnabled)
			: base (log)
		{
			this.uniqueAssemblyNames = uniqueAssemblyNames ?? throw new ArgumentNullException (nameof (uniqueAssemblyNames));
			this.codeGenState = codeGenState ?? throw new ArgumentNullException (nameof (codeGenState));
			this.managedMarshalMethodsLookupEnabled = managedMarshalMethodsLookupEnabled;

			generateEmptyCode = false;
			defaultCallMarker = LlvmIrCallMarker.Tail;
		}

		void Init ()
		{
			if (generateEmptyCode || codeGenState.MarshalMethods.Count == 0) {
				return;
			}

			var seenClasses = new Dictionary<string, int> (StringComparer.Ordinal);
			var allMethods = new List<MarshalMethodInfo> ();
			IDictionary<string, IList<MarshalMethodEntryObject>> marshalMethods = codeGenState.MarshalMethods;

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
			foreach (IList<MarshalMethodEntryObject> entryList in marshalMethods.Values) {
				bool useFullNativeSignature = entryList.Count > 1;
				foreach (MarshalMethodEntryObject entry in entryList) {
					Log.LogDebugMessage ($"MM: processing {entry.DeclaringType.FullName} {entry.NativeCallback.FullName}");
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
					Log.LogDebugMessage ($"Removed MM duplicate '{method.NativeSymbolName}' (implemented: {method.Method.ImplementedMethod.FullName}; registered: {method.Method.RegisteredMethod.FullName}");
					continue;
				}

				seenNativeSymbols.Add (method.NativeSymbolName);
				methods.Add (method);
			}
		}

		string MakeNativeSymbolName (MarshalMethodEntryObject entry, bool useFullNativeSignature)
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
				bool haveParams = sigParams.Length > 0;
				if (useFullNativeSignature || haveParams) {
					// We always append the underscores for overloaded methods, see https://github.com/dotnet/android/issues/10417#issuecomment-3210789627
					// for the reason why
					sb.Append ("__");
					if (haveParams) {
						sb.Append (MangleForJni (sigParams));
					}
				}
			}

			return sb.ToString ();

			void ThrowInvalidSignature (string signature, string reason)
			{
				throw new InvalidOperationException ($"Invalid JNI signature '{signature}': {reason}");
			}
		}

		void ProcessAndAddMethod (List<MarshalMethodInfo> allMethods, MarshalMethodEntryObject entry, bool useFullNativeSignature, Dictionary<string, int> seenClasses, Dictionary<string, List<MarshalMethodInfo>> overloadedNativeSymbolNames)
		{
			MarshalMethodEntryMethodObject nativeCallback = entry.NativeCallback;
			string nativeSymbolName = MakeNativeSymbolName (entry, useFullNativeSignature);
			string klass = $"{nativeCallback.DeclaringType.FullName}, {nativeCallback.DeclaringType.Module.Assembly.FullName}";

			if (!seenClasses.TryGetValue (klass, out int classIndex)) {
				classIndex = classes.Count;
				seenClasses.Add (klass, classIndex);

				var mc = new MarshalMethodsManagedClass {
					token = nativeCallback.DeclaringType.MetadataToken,
					ClassName = klass,
				};

				classes.Add (new StructureInstance<MarshalMethodsManagedClass> (marshalMethodsManagedClassStructureInfo, mc));
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

		(Type returnType, List<LlvmIrFunctionParameter>? functionParams) ParseJniSignature (string signature, MarshalMethodEntryMethodObject implementedMethod)
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
				if (MonoAndroidHelper.StringEquals (typeName, "java/lang/Class")) {
					return typeof(_jclass);
				}

				if (MonoAndroidHelper.StringEquals (typeName, "java/lang/String")) {
					return typeof(_jstring);
				}

				if (MonoAndroidHelper.StringEquals (typeName, "java/lang/Throwable")) {
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
				parameters.Add (new LlvmIrFunctionParameter (type, implementedMethod.Parameters[parameters.Count].Name));
			}
		}

		protected override void Construct (LlvmIrModule module)
		{
			module.DefaultStringGroup = "mm";

			MapStructures (module);

			Init ();
			AssemblyCacheState acs = CreateAssemblyCache ();
			AddAssemblyImageCache (module, acs);
			AddClassCache (module);
			AddClassNames (module);

			AddMarshalMethodNames (module, acs);
			(LlvmIrVariable getFunctionPtrVariable, LlvmIrFunction getFunctionPtrFunction) = AddXamarinAppInitFunction (module);

			AddMarshalMethods (module, acs, getFunctionPtrVariable, getFunctionPtrFunction);
		}

		protected virtual void MapStructures (LlvmIrModule module)
		{
			marshalMethodsManagedClassStructureInfo = module.MapStructure<MarshalMethodsManagedClass> ();
		}

		protected virtual void AddClassNames (LlvmIrModule module)
		{}

		protected virtual void AddClassCache (LlvmIrModule module)
		{}

	string GetSignatureKey (MarshalMethodInfo method)
	{
		var sb = new StringBuilder ();
		sb.Append (method.ReturnType.FullName);
		sb.Append ('(');
		for (int i = 0; i < method.Parameters.Count; i++) {
			if (i > 0) {
				sb.Append (',');
			}
			sb.Append (method.Parameters[i].Type.FullName);
		}
		sb.Append (')');
		return sb.ToString ();
	}

	LlvmIrFunction CreateSharedTrampoline (LlvmIrModule module, string signatureKey, MarshalMethodInfo templateMethod, MarshalMethodsWriteState writeState)
	{
		string trampolineName = $"shared_trampoline_{signatureKey.GetHashCode():x8}";

		var trampolineParams = new List<LlvmIrFunctionParameter> ();
		// Add original JNI parameters (env, klass)
		trampolineParams.AddRange (templateMethod.Parameters);

		// Add extra parameters for the trampoline: callback_global, param1, param2, param3
		trampolineParams.Add (new LlvmIrFunctionParameter (typeof(IntPtr), "callback_global") { NoUndef = true, NonNull = true });
		trampolineParams.Add (new LlvmIrFunctionParameter (typeof(uint), "param1") { NoUndef = true });
		trampolineParams.Add (new LlvmIrFunctionParameter (typeof(uint), "param2") { NoUndef = true });
		trampolineParams.Add (new LlvmIrFunctionParameter (typeof(uint), "param3") { NoUndef = true });

		var trampoline = new LlvmIrFunction (trampolineName, templateMethod.ReturnType, trampolineParams) {
			Comment = $" Shared trampoline for signature: {signatureKey}",
		};

		// Mark as internal linkage so it's not exported
		trampoline.Linkage = LlvmIrLinkage.Internal;

		WriteTrampolineBody (trampoline.Body, templateMethod, writeState);
		module.Add (trampoline);

		return trampoline;
	}

	void WriteTrampolineBody (LlvmIrFunctionBody body, MarshalMethodInfo templateMethod, MarshalMethodsWriteState writeState)
	{
		var func = body.Owner;
		int paramCount = templateMethod.Parameters.Count;
		var callbackGlobalParam = func.Signature.Parameters[paramCount];
		var param1 = func.Signature.Parameters[paramCount + 1];
		var param2 = func.Signature.Parameters[paramCount + 2];
		var param3 = func.Signature.Parameters[paramCount + 3];

		LlvmIrLocalVariable cb1 = func.CreateLocalVariable (typeof(IntPtr), "cb1");
		body.Load (callbackGlobalParam, cb1, tbaa: body.Owner.Module.TbaaAnyPointer);

		LlvmIrLocalVariable isNullResult = func.CreateLocalVariable (typeof(bool), "isNull");
		body.Icmp (LlvmIrIcmpCond.Equal, cb1, null, isNullResult);

		var loadCallbackLabel = new LlvmIrFunctionLabelItem ("loadCallback");
		var callbackLoadedLabel = new LlvmIrFunctionLabelItem ("callbackLoaded");
		body.Br (isNullResult, loadCallbackLabel, callbackLoadedLabel);

		// Callback variable was null
		body.Add (loadCallbackLabel);

		LlvmIrLocalVariable getFuncPtrResult = func.CreateLocalVariable (typeof(IntPtr), "get_func_ptr");
		body.Load (writeState.GetFunctionPtrVariable, getFuncPtrResult, tbaa: body.Owner.Module.TbaaAnyPointer);

		var getFunctionPointerArguments = new List<object?> {
			param1,
			param2,
			param3,
			callbackGlobalParam
		};

		LlvmIrInstructions.Call call = body.Call (writeState.GetFunctionPtrFunction, arguments: getFunctionPointerArguments, funcPointer: getFuncPtrResult);

		LlvmIrLocalVariable cb2 = func.CreateLocalVariable (typeof(IntPtr), "cb2");
		body.Load (callbackGlobalParam, cb2, tbaa: body.Owner.Module.TbaaAnyPointer);
		body.Br (callbackLoadedLabel);

		// Callback variable has just been set or it wasn't null
		body.Add (callbackLoadedLabel);
		LlvmIrLocalVariable fn = func.CreateLocalVariable (typeof(IntPtr), "fn");

		// Preceding blocks are ordered from the newest to the oldest, so we need to pass the variables referring to our callback in "reverse" order
		body.Phi (fn, cb2, body.PrecedingBlock1, cb1, body.PrecedingBlock2);

		var nativeFunc = new LlvmIrFunction (func.Name, templateMethod.ReturnType, templateMethod.Parameters);
		nativeFunc.Signature.ReturnAttributes.NoUndef = true;

		// Call the actual function with only the original parameters
		var arguments = new List<object?> ();
		for (int i = 0; i < paramCount; i++) {
			arguments.Add (func.Signature.Parameters[i]);
		}
		LlvmIrLocalVariable? result = nativeFunc.ReturnsValue ? func.CreateLocalVariable (nativeFunc.Signature.ReturnType) : null;
		call = body.Call (nativeFunc, result, arguments, funcPointer: fn);
		call.CallMarker = LlvmIrCallMarker.Tail;

		body.Ret (nativeFunc.Signature.ReturnType, result);
	}

		void AddMarshalMethods (LlvmIrModule module, AssemblyCacheState acs, LlvmIrVariable getFunctionPtrVariable, LlvmIrFunction getFunctionPtrFunction)
		{
			if (generateEmptyCode || methods == null || methods.Count == 0) {
				return;
			}

			// This will make all the backing fields to appear in a block without empty lines separating them.
			module.Add (
				new LlvmIrGroupDelimiterVariable () {
					Comment = " Marshal methods backing fields, pointers to native functions"
				}
			);

			var writeState = new MarshalMethodsWriteState {
				AssemblyCacheState = acs,
				AttributeSet = MakeMarshalMethodAttributeSet (module),
				UsedBackingFields = new Dictionary<string, LlvmIrVariable> (StringComparer.Ordinal),
				UniqueAssemblyId = new Dictionary<string, ulong> (StringComparer.OrdinalIgnoreCase),
				GetFunctionPtrVariable = getFunctionPtrVariable,
				GetFunctionPtrFunction = getFunctionPtrFunction,
				SharedTrampolines = new Dictionary<string, LlvmIrFunction> (StringComparer.Ordinal),
			};

			// Group methods by signature to identify candidates for shared trampolines
			var methodsBySignature = new Dictionary<string, List<MarshalMethodInfo>> (StringComparer.Ordinal);
			foreach (MarshalMethodInfo mmi in methods) {
				string signatureKey = GetSignatureKey (mmi);
				if (!methodsBySignature.TryGetValue (signatureKey, out List<MarshalMethodInfo>? methodList)) {
					methodList = new List<MarshalMethodInfo> ();
					methodsBySignature.Add (signatureKey, methodList);
				}
				methodList.Add (mmi);
			}

			// Generate shared trampolines for signatures used by multiple methods
			module.Add (
				new LlvmIrGroupDelimiterVariable () {
					Comment = " Shared trampoline functions for marshal methods"
				}
			);

			foreach (var kvp in methodsBySignature) {
				// Only create shared trampolines for signatures used by more than one method
				if (kvp.Value.Count > 1) {
					string signatureKey = kvp.Key;
					MarshalMethodInfo templateMethod = kvp.Value[0];
					LlvmIrFunction trampoline = CreateSharedTrampoline (module, signatureKey, templateMethod, writeState);
					writeState.SharedTrampolines.Add (signatureKey, trampoline);
					Log.LogDebugMessage ($"MM: created shared trampoline '{trampoline.Name}' for signature '{signatureKey}' used by {kvp.Value.Count} methods");
				}
			}

			module.Add (new LlvmIrGroupDelimiterVariable ());

			foreach (MarshalMethodInfo mmi in methods) {
				MarshalMethodEntryMethodObject nativeCallback = mmi.Method.NativeCallback;
				string asmName = nativeCallback.DeclaringType.Module.Assembly.NameName;

				if (!writeState.UniqueAssemblyId.TryGetValue (asmName, out ulong asmId)) {
					asmId = (ulong)writeState.UniqueAssemblyId.Count;
					writeState.UniqueAssemblyId.Add (asmName, asmId);
				}

				AddMarshalMethod (module, mmi, asmId, writeState);
			}

			module.Add (new LlvmIrGroupDelimiterVariable ());
		}

		void AddMarshalMethod (LlvmIrModule module, MarshalMethodInfo method, ulong asmId, MarshalMethodsWriteState writeState)
		{
			Log.LogDebugMessage ($"MM: generating code for {method.Method.DeclaringType.FullName} {method.Method.NativeCallback.FullName}");
			MarshalMethodEntryMethodObject nativeCallback = method.Method.NativeCallback;
			string backingFieldName = $"native_cb_{method.Method.JniMethodName}_{asmId}_{method.ClassCacheIndex}_{nativeCallback.MetadataToken:x}";

			if (!writeState.UsedBackingFields.TryGetValue (backingFieldName, out LlvmIrVariable backingField)) {
				backingField = module.AddGlobalVariable (typeof(IntPtr), backingFieldName, null, LlvmIrVariableOptions.LocalWritableInsignificantAddr);
				writeState.UsedBackingFields.Add (backingFieldName, backingField);
			}

			var funcComment = new StringBuilder (" Method: ");
			funcComment.AppendLine (nativeCallback.FullName);
			funcComment.Append (" Assembly: ");
			funcComment.AppendLine (nativeCallback.DeclaringType.Module.Assembly.NameFullName);
			funcComment.Append (" Registered: ");
			funcComment.AppendLine (method.Method.RegisteredMethod?.FullName ?? "none");
			funcComment.Append (" Implemented: ");
			funcComment.AppendLine (method.Method.ImplementedMethod?.FullName ?? "none");

			var func = new LlvmIrFunction (method.NativeSymbolName, method.ReturnType, method.Parameters, writeState.AttributeSet) {
				Comment = funcComment.ToString (),
			};

			// Check if we have a shared trampoline for this signature
			string signatureKey = GetSignatureKey (method);
			if (writeState.SharedTrampolines.TryGetValue (signatureKey, out LlvmIrFunction? sharedTrampoline)) {
				WriteWrapperBody (func.Body, method, backingField, nativeCallback, sharedTrampoline, writeState);
			} else {
				WriteStandaloneBody (func.Body, method, backingField, nativeCallback, writeState);
			}
			module.Add (func);

			void WriteWrapperBody (LlvmIrFunctionBody body, MarshalMethodInfo method, LlvmIrVariable backingField, MarshalMethodEntryMethodObject nativeCallback, LlvmIrFunction trampoline, MarshalMethodsWriteState writeState)
			{
				// Generate a simple wrapper that calls the shared trampoline
				var arguments = new List<object?> ();

				// Add original parameters (env, klass, etc.)
				foreach (LlvmIrFunctionParameter parameter in func.Signature.Parameters) {
					arguments.Add (new LlvmIrLocalVariable (parameter.Type, parameter.Name));
				}

				// Add trampoline-specific arguments
				arguments.Add (backingField);

				// Add the three parameters for get_function_pointer
				if (managedMarshalMethodsLookupEnabled) {
					(uint assemblyIndex, uint classIndex, uint methodIndex) = GetManagedMarshalMethodsLookupIndexes (nativeCallback);
					arguments.Add (assemblyIndex);
					arguments.Add (classIndex);
					arguments.Add (methodIndex);
				} else {
					var placeholder = new MarshalMethodAssemblyIndexValuePlaceholder (method, writeState.AssemblyCacheState);
					arguments.Add (placeholder);
					arguments.Add (method.ClassCacheIndex);
					arguments.Add (nativeCallback.MetadataToken);
				}

				LlvmIrLocalVariable? result = func.ReturnsValue ? func.CreateLocalVariable (func.Signature.ReturnType) : null;
				var call = body.Call (trampoline, result, arguments);
				call.CallMarker = LlvmIrCallMarker.Tail;

				body.Ret (func.Signature.ReturnType, result);
			}

			void WriteStandaloneBody (LlvmIrFunctionBody body, MarshalMethodInfo method, LlvmIrVariable backingField, MarshalMethodEntryMethodObject nativeCallback, MarshalMethodsWriteState writeState)

			{
				LlvmIrLocalVariable cb1 = func.CreateLocalVariable (typeof(IntPtr), "cb1");
				body.Load (backingField, cb1, tbaa: module.TbaaAnyPointer);

				LlvmIrLocalVariable isNullResult = func.CreateLocalVariable (typeof(bool), "isNull");
				body.Icmp (LlvmIrIcmpCond.Equal, cb1, null, isNullResult);

				var loadCallbackLabel = new LlvmIrFunctionLabelItem ("loadCallback");
				var callbackLoadedLabel = new LlvmIrFunctionLabelItem ("callbackLoaded");
				body.Br (isNullResult, loadCallbackLabel, callbackLoadedLabel);

				// Callback variable was null
				body.Add (loadCallbackLabel);

				LlvmIrLocalVariable getFuncPtrResult = func.CreateLocalVariable (typeof(IntPtr), "get_func_ptr");
				body.Load (writeState.GetFunctionPtrVariable, getFuncPtrResult, tbaa: module.TbaaAnyPointer);

				List<object?> getFunctionPointerArguments;
				if (managedMarshalMethodsLookupEnabled) {
					(uint assemblyIndex, uint classIndex, uint methodIndex) = GetManagedMarshalMethodsLookupIndexes (nativeCallback);
					getFunctionPointerArguments = new List<object?> { assemblyIndex, classIndex, methodIndex, backingField };
				} else {
					var placeholder = new MarshalMethodAssemblyIndexValuePlaceholder (method, writeState.AssemblyCacheState);
					getFunctionPointerArguments = new List<object?> { placeholder, method.ClassCacheIndex, nativeCallback.MetadataToken, backingField };
				}

				LlvmIrInstructions.Call call = body.Call (writeState.GetFunctionPtrFunction, arguments: getFunctionPointerArguments, funcPointer: getFuncPtrResult);

				LlvmIrLocalVariable cb2 = func.CreateLocalVariable (typeof(IntPtr), "cb2");
				body.Load (backingField, cb2, tbaa: module.TbaaAnyPointer);
				body.Br (callbackLoadedLabel);

				// Callback variable has just been set or it wasn't null
				body.Add (callbackLoadedLabel);
				LlvmIrLocalVariable fn = func.CreateLocalVariable (typeof(IntPtr), "fn");

				// Preceding blocks are ordered from the newest to the oldest, so we need to pass the variables referring to our callback in "reverse" order
				body.Phi (fn, cb2, body.PrecedingBlock1, cb1, body.PrecedingBlock2);

				var nativeFunc = new LlvmIrFunction (method.NativeSymbolName, method.ReturnType, method.Parameters);
				nativeFunc.Signature.ReturnAttributes.NoUndef = true;

				var arguments = new List<object?> ();
				foreach (LlvmIrFunctionParameter parameter in nativeFunc.Signature.Parameters) {
					arguments.Add (new LlvmIrLocalVariable (parameter.Type, parameter.Name));
				}
				LlvmIrLocalVariable? result = nativeFunc.ReturnsValue ? func.CreateLocalVariable (nativeFunc.Signature.ReturnType) : null;
				call = body.Call (nativeFunc, result, arguments, funcPointer: fn);
				call.CallMarker = LlvmIrCallMarker.Tail;

				body.Ret (nativeFunc.Signature.ReturnType, result);
			}

			(uint assemblyIndex, uint classIndex, uint methodIndex) GetManagedMarshalMethodsLookupIndexes (MarshalMethodEntryMethodObject nativeCallback)
			{
				var assemblyIndex = nativeCallback.AssemblyIndex ?? throw new InvalidOperationException ("ManagedMarshalMethodsLookupInfo missing");
				var classIndex = nativeCallback.ClassIndex ?? throw new InvalidOperationException ("ManagedMarshalMethodsLookupInfo missing");
				var methodIndex = nativeCallback.MethodIndex ?? throw new InvalidOperationException ("ManagedMarshalMethodsLookupInfo missing");

				return (assemblyIndex, classIndex, methodIndex);
			}
		}

		LlvmIrFunctionAttributeSet MakeMarshalMethodAttributeSet (LlvmIrModule module)
		{
			var attrSet = new LlvmIrFunctionAttributeSet {
				new MustprogressFunctionAttribute (),
				new UwtableFunctionAttribute (),
				new MinLegalVectorWidthFunctionAttribute (0),
				new NoTrappingMathFunctionAttribute (true),
				new StackProtectorBufferSizeFunctionAttribute (8),
			};

			return module.AddAttributeSet (attrSet);
		}

		(LlvmIrVariable getFuncPtrVariable, LlvmIrFunction getFuncPtrFunction) AddXamarinAppInitFunction (LlvmIrModule module)
		{
			var getFunctionPtrParams = new List<LlvmIrFunctionParameter> {
				new (typeof(uint), "mono_image_index") {
					NoUndef = true,
				},
				new (typeof(uint), "class_index") {
					NoUndef = true,
				},
				new (typeof(uint), "method_token") {
					NoUndef = true,
				},
				new (typeof(IntPtr), "target_ptr") {
					NoUndef = true,
					NonNull = true,
					Align = 0, // 0 means use natural pointer alignment
					Dereferenceable = 0, // ditto 👆
					IsCplusPlusReference = true,
				},
			};

			var getFunctionPtrComment = new StringBuilder (" ");
			getFunctionPtrComment.Append (GetFunctionPointerVariableName);
			getFunctionPtrComment.Append (" (");
			for (int i = 0; i < getFunctionPtrParams.Count; i++) {
				if (i > 0) {
					getFunctionPtrComment.Append (", ");
				}
				LlvmIrFunctionParameter parameter = getFunctionPtrParams[i];
				getFunctionPtrComment.Append (LlvmIrGenerator.MapManagedTypeToNative (parameter.Type));
				if (parameter.IsCplusPlusReference.HasValue && parameter.IsCplusPlusReference.Value) {
				 	getFunctionPtrComment.Append ('&');
				}
				getFunctionPtrComment.Append (' ');
				getFunctionPtrComment.Append (parameter.Name);
			}
			getFunctionPtrComment.Append (')');

			LlvmIrFunction getFunctionPtrFunc = new LlvmIrFunction (
				name: GetFunctionPointerVariableName,
				returnType: typeof(void),
				parameters: getFunctionPtrParams
			);

			LlvmIrVariable getFunctionPtrVariable = module.AddGlobalVariable (
				typeof(IntPtr),
				GetFunctionPointerVariableName,
				null,
				LlvmIrVariableOptions.LocalWritableInsignificantAddr,
				getFunctionPtrComment.ToString ()
			);

			var init_params = new List<LlvmIrFunctionParameter> {
				new (typeof(_JNIEnv), "env") {
					NoCapture = true,
					NoUndef = true,
					ReadNone = true,
				},
				new (typeof(IntPtr), "fn") {
					NoUndef = true,
				},
			};

			var init_signature = new LlvmIrFunctionSignature (
				name: "xamarin_app_init",
				returnType: typeof(void),
				parameters: init_params
			);

			LlvmIrFunctionAttributeSet attrSet = MakeXamarinAppInitAttributeSet (module);
			var xamarin_app_init = new LlvmIrFunction (init_signature, attrSet);

			// If `fn` is nullptr, print a message and abort...
			//
			// We must allocate result variables for both the null comparison and puts call here and with names, because
			// labels and local unnamed variables must be numbered sequentially otherwise and the `AddIfThenElse` call will
			// allocate up to 3 labels which would have been **defined** after these labels, but **used** before them - and
			// thus the numbering sequence would be out of order and the .ll file wouldn't build.
			var fnNullResult = xamarin_app_init.CreateLocalVariable (typeof(bool), "fnIsNull");
			LlvmIrVariable putsResult = xamarin_app_init.CreateLocalVariable (typeof(int), "putsResult");
			var ifThenInstructions = new List<LlvmIrInstruction> {
				module.CreatePuts ("get_function_pointer MUST be specified\n", putsResult),
				module.CreateAbort (),
				new LlvmIrInstructions.Unreachable (),
			};

			module.AddIfThenElse (xamarin_app_init, fnNullResult, LlvmIrIcmpCond.Equal, init_params[1], null, ifThenInstructions);

			// ...otherwise store the pointer and return
			xamarin_app_init.Body.Store (init_params[1], getFunctionPtrVariable, module.TbaaAnyPointer);
			xamarin_app_init.Body.Ret (typeof(void));

			module.Add (xamarin_app_init);

			return (getFunctionPtrVariable, getFunctionPtrFunc);
		}

		LlvmIrFunctionAttributeSet MakeXamarinAppInitAttributeSet (LlvmIrModule module)
		{
			var attrSet = new LlvmIrFunctionAttributeSet {
				new MustprogressFunctionAttribute (),
				new NofreeFunctionAttribute (),
				new NorecurseFunctionAttribute (),
				new NosyncFunctionAttribute (),
				new NounwindFunctionAttribute (),
				new WillreturnFunctionAttribute (),
				new MemoryFunctionAttribute {
					Default = MemoryAttributeAccessKind.Write,
					Argmem = MemoryAttributeAccessKind.None,
					InaccessibleMem = MemoryAttributeAccessKind.None,
				},
				new UwtableFunctionAttribute (),
				new MinLegalVectorWidthFunctionAttribute (0),
				new NoTrappingMathFunctionAttribute (true),
				new StackProtectorBufferSizeFunctionAttribute (8),
			};

			return module.AddAttributeSet (attrSet);
		}

		protected virtual void AddMarshalMethodNames (LlvmIrModule module, AssemblyCacheState acs)
		{}

		AssemblyCacheState CreateAssemblyCache ()
		{
			var acs = new AssemblyCacheState ();
			uint index = 0;

			foreach (string name in uniqueAssemblyNames) {
				// We must make sure we keep the possible culture prefix, which will be treated as "directory" path here
				string cultureName = Path.GetDirectoryName (name) ?? String.Empty;
				string clippedName = Path.Combine (cultureName, Path.GetFileNameWithoutExtension (name)).Replace (@"\", "/");
				string inArchiveName;

				if (cultureName.Length == 0) {
					// Regular assemblies get the 'lib_' prefix
					inArchiveName = $"{MonoAndroidHelper.MANGLED_ASSEMBLY_REGULAR_ASSEMBLY_MARKER}{name}{MonoAndroidHelper.MANGLED_ASSEMBLY_NAME_EXT}";
				} else {
					// Satellite assemblies get the 'lib-{CULTURE}-' prefix
					inArchiveName = $"{MonoAndroidHelper.MANGLED_ASSEMBLY_SATELLITE_ASSEMBLY_MARKER}{cultureName}-{Path.GetFileName (name)}{MonoAndroidHelper.MANGLED_ASSEMBLY_NAME_EXT}";
				}

				ulong hashFull32 = MonoAndroidHelper.GetXxHash (name, is64Bit: false);
				ulong hashInArchive32 = MonoAndroidHelper.GetXxHash (inArchiveName, is64Bit: false);
				ulong hashClipped32 = MonoAndroidHelper.GetXxHash (clippedName, is64Bit: false);

				ulong hashFull64 = MonoAndroidHelper.GetXxHash (name, is64Bit: true);
				ulong hashInArchive64 = MonoAndroidHelper.GetXxHash (inArchiveName, is64Bit: true);
				ulong hashClipped64 = MonoAndroidHelper.GetXxHash (clippedName, is64Bit: true);

				//
				// If the number of name forms changes, xamarin-app.hh MUST be updated to set value of the
				// `number_of_assembly_name_forms_in_image_cache` constant to the number of forms.
				//
				acs.Hashes32.Add ((uint)Convert.ChangeType (hashFull32, typeof(uint)), (name, index));
				acs.Hashes32.Add ((uint)Convert.ChangeType (hashInArchive32, typeof(uint)), (inArchiveName, index));
				acs.Hashes32.Add ((uint)Convert.ChangeType (hashClipped32, typeof(uint)), (clippedName, index));

				acs.Hashes64.Add (hashFull64, (name, index));
				acs.Hashes64.Add (hashInArchive64, (inArchiveName, index));
				acs.Hashes64.Add (hashClipped64, (clippedName, index));

				index++;
			}

			acs.Keys32 = acs.Hashes32.Keys.ToList ();
			acs.Keys32.Sort ();
			for (int i = 0; i < acs.Keys32.Count; i++) {
				(string name, uint idx) = acs.Hashes32[acs.Keys32[i]];
				acs.Indices32.Add (idx);
				acs.AsmNameToIndexData32.Add (name, idx);
			}

			acs.Keys64 = acs.Hashes64.Keys.ToList ();
			acs.Keys64.Sort ();
			for (int i = 0; i < acs.Keys64.Count; i++) {
				(string name, uint idx) = acs.Hashes64[acs.Keys64[i]];
				acs.Indices64.Add (idx);
				acs.AsmNameToIndexData64.Add (name, idx);
			}

			return acs;
		}

		protected virtual void AddAssemblyImageCache (LlvmIrModule module, AssemblyCacheState acs)
		{}
	}
}
