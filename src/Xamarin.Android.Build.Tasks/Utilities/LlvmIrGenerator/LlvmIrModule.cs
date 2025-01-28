using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVMIR
{
	partial class LlvmIrModule
	{
		/// <summary>
		/// Global variable type to be used to output name:value string arrays. This is a notational shortcut,
		/// do **NOT** change the type without understanding how it affects the rest of code.
		/// </summary>
		public static readonly Type NameValueArrayType = typeof(IDictionary<string, string>);

		public IList<LlvmIrFunction>? ExternalFunctions         { get; private set; }
		public IList<LlvmIrFunction>? Functions                 { get; private set; }
		public IList<LlvmIrFunctionAttributeSet>? AttributeSets { get; private set; }
		public IList<StructureInfo>? Structures                 { get; private set; }
		public IList<LlvmIrGlobalVariable>? GlobalVariables     { get; private set; }
		public IList<LlvmIrStringGroup>? Strings                { get; private set; }

		/// <summary>
		/// TBAA stands for "Type Based Alias Analysis" and is used by LLVM to implemente a description of
		/// a higher level language typesystem to LLVM IR (in which memory doesn't have types).  This metadata
		/// item describes pointer usage for certain instructions we output and is common enough to warrant
		/// a shortcut property like that.  More information about TBAA can be found at https://llvm.org/docs/LangRef.html#tbaa-metadata
		/// </summary>
		public LlvmIrMetadataItem TbaaAnyPointer                => tbaaAnyPointer;

		Dictionary<LlvmIrFunctionAttributeSet, LlvmIrFunctionAttributeSet>? attributeSets;
		Dictionary<LlvmIrFunction, LlvmIrFunction>? externalFunctions;
		Dictionary<LlvmIrFunction, LlvmIrFunction>? functions;
		Dictionary<Type, StructureInfo>? structures;
		LlvmIrStringManager? stringManager;
		LlvmIrMetadataManager metadataManager;
		LlvmIrMetadataItem tbaaAnyPointer;
		LlvmIrBufferManager? bufferManager;

		List<LlvmIrGlobalVariable>? globalVariables;

		LlvmIrFunction? puts;
		LlvmIrFunction? abort;

		TaskLoggingHelper log;

		public readonly LlvmIrTypeCache TypeCache;

		public LlvmIrModule (LlvmIrTypeCache cache, TaskLoggingHelper log)
		{
			this.log = log;
			TypeCache = cache;
			metadataManager = new LlvmIrMetadataManager (cache);

			// Only model agnostic items can be added here
			LlvmIrMetadataItem flags = metadataManager.Add (LlvmIrKnownMetadata.LlvmModuleFlags);
			flags.AddReferenceField (metadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Error, "wchar_size", 4));
			flags.AddReferenceField (metadataManager.AddNumbered (LlvmIrModuleMergeBehavior.Max, "PIC Level", 2));

			LlvmIrMetadataItem ident = metadataManager.Add (LlvmIrKnownMetadata.LlvmIdent);
			LlvmIrMetadataItem identValue = metadataManager.AddNumbered ($".NET for Android {XABuildConfig.XamarinAndroidBranch} @ {XABuildConfig.XamarinAndroidCommitHash}");
			ident.AddReferenceField (identValue.Name);

			tbaaAnyPointer = metadataManager.AddNumbered ();
			LlvmIrMetadataItem anyPointer = metadataManager.AddNumbered ("any pointer");
			LlvmIrMetadataItem omnipotentChar = metadataManager.AddNumbered ("omnipotent char");
			LlvmIrMetadataItem simpleCppTBAA = metadataManager.AddNumbered ("Simple C++ TBAA");

			anyPointer.AddReferenceField (omnipotentChar.Name);
			anyPointer.AddField ((ulong)0);

			omnipotentChar.AddReferenceField (simpleCppTBAA);
			omnipotentChar.AddField ((ulong)0);

			tbaaAnyPointer.AddReferenceField (anyPointer);
			tbaaAnyPointer.AddReferenceField (anyPointer);
			tbaaAnyPointer.AddField ((ulong)0);
		}

		/// <summary>
		/// Return a metadata manager instance which includes copies of all the target-agnostic metadata items.
		/// We must not modify the original manager since each target may have conflicting values for certain
		/// flags.
		/// </summary>
		public LlvmIrMetadataManager GetMetadataManagerCopy () => new LlvmIrMetadataManager (metadataManager);

		/// <summary>
		/// Perform any tasks that need to be done after construction is complete.
		/// </summary>
		public void AfterConstruction ()
		{
			if (externalFunctions != null) {
				List<LlvmIrFunction> list = externalFunctions.Values.ToList ();
				list.Sort ((LlvmIrFunction a, LlvmIrFunction b) => a.Signature.Name.CompareTo (b.Signature.Name));
				ExternalFunctions = list.AsReadOnly ();
			}

			if (functions != null) {
				List<LlvmIrFunction> list = functions.Values.ToList ();
				// TODO: sort or not?
				Functions = list.AsReadOnly ();
			}

			if (attributeSets != null) {
				List<LlvmIrFunctionAttributeSet> list = attributeSets.Values.ToList ();
				list.Sort ((LlvmIrFunctionAttributeSet a, LlvmIrFunctionAttributeSet b) => a.Number.CompareTo (b.Number));
				AttributeSets = list.AsReadOnly ();
			}

			if (structures != null) {
				List<StructureInfo> list = structures.Values.ToList ();
				list.Sort ((StructureInfo a, StructureInfo b) => a.Name.CompareTo (b.Name));
				Structures = list.AsReadOnly ();
			}

			if (stringManager != null && stringManager.StringGroups.Count > 0) {
				Strings = stringManager.StringGroups.AsReadOnly ();
			}

			GlobalVariables = globalVariables?.AsReadOnly ();
		}

		public void Add (LlvmIrFunction func)
		{
			if (functions == null) {
				functions = new Dictionary<LlvmIrFunction, LlvmIrFunction> ();
			}

			if (functions.TryGetValue (func, out LlvmIrFunction existingFunc)) {
				throw new InvalidOperationException ($"Internal error: identical function has already been added (\"{func.Signature.Name}\")");
			}

			functions.Add (func, func);
		}

		public LlvmIrInstructions.Call CreatePuts (string text, LlvmIrVariable result)
		{
			EnsurePuts ();
			RegisterString (text);
			return new LlvmIrInstructions.Call (puts, result, new List<object?> { text });
		}

		/// <summary>
		/// Generate code to call the `puts(3)` C library function to print a simple string to standard output.
		/// </summary>
		public LlvmIrInstructions.Call AddPuts (LlvmIrFunction function, string text, LlvmIrVariable result)
		{
			EnsurePuts ();
			RegisterString (text);
			return function.Body.Call (puts, result, new List<object?> { text });
		}

		void EnsurePuts ()
		{
			if (puts != null) {
				return;
			}

			var puts_params = new List<LlvmIrFunctionParameter> {
				new (typeof(string), "s"),
			};

			var puts_sig = new LlvmIrFunctionSignature (
				name: "puts",
				returnType: typeof(int),
				parameters: puts_params
			);
			puts_sig.ReturnAttributes.NoUndef = true;

			puts = DeclareExternalFunction (puts_sig, MakePutsAttributeSet ());
		}

		LlvmIrFunctionAttributeSet MakePutsAttributeSet ()
		{
			var ret = new LlvmIrFunctionAttributeSet {
				new NofreeFunctionAttribute (),
				new NounwindFunctionAttribute (),
			};

			ret.DoNotAddTargetSpecificAttributes = true;
			return AddAttributeSet (ret);
		}

		public LlvmIrInstructions.Call CreateAbort ()
		{
			EnsureAbort ();
			return new LlvmIrInstructions.Call (abort);
		}

		public LlvmIrInstructions.Call AddAbort (LlvmIrFunction function)
		{
			EnsureAbort ();
			LlvmIrInstructions.Call ret = function.Body.Call (abort);
			function.Body.Unreachable ();

			return ret;
		}

		void EnsureAbort ()
		{
			if (abort != null) {
				return;
			}

			var abort_sig = new LlvmIrFunctionSignature (name: "abort", returnType: typeof(void));
			abort = DeclareExternalFunction (abort_sig, MakeAbortAttributeSet ());
		}

		LlvmIrFunctionAttributeSet MakeAbortAttributeSet ()
		{
			var ret = new LlvmIrFunctionAttributeSet {
				new NoreturnFunctionAttribute (),
				new NounwindFunctionAttribute (),
				new NoTrappingMathFunctionAttribute (true),
				new StackProtectorBufferSizeFunctionAttribute (8),
			};

			return AddAttributeSet (ret);
		}

		public void AddIfThenElse (LlvmIrFunction function, LlvmIrVariable result, LlvmIrIcmpCond condition, LlvmIrVariable conditionVariable, object? conditionComparand, ICollection<LlvmIrInstruction> codeIfThen, ICollection<LlvmIrInstruction>? codeIfElse = null)
		{
			function.Body.Icmp (condition, conditionVariable, conditionComparand, result);

			var labelIfThen = new LlvmIrFunctionLabelItem ();
			LlvmIrFunctionLabelItem? labelIfElse = codeIfElse != null ? new LlvmIrFunctionLabelItem () : null;
			var labelIfDone = new LlvmIrFunctionLabelItem ();

			function.Body.Br (result, labelIfThen, labelIfElse == null ? labelIfDone : labelIfElse);
			function.Body.Add (labelIfThen);

			AddInstructions (codeIfThen);

			if (codeIfElse != null) {
				function.Body.Add (labelIfElse);
				AddInstructions (codeIfElse);
			}

			function.Body.Add (labelIfDone);

			void AddInstructions (ICollection<LlvmIrInstruction> instructions)
			{
				foreach (LlvmIrInstruction ins in instructions) {
					function.Body.Add (ins);
				}
			}
		}

		/// <summary>
		/// A shortcut way to add a global variable without first having to create an instance of <see cref="LlvmIrGlobalVariable"/> first. This overload
		/// requires the <paramref name="value"/> parameter to not be <c>null</c>.
		/// </summary>
		public LlvmIrGlobalVariable AddGlobalVariable (string name, object value, LlvmIrVariableOptions? options = null, string? comment = null)
		{
			if (value == null) {
				throw new ArgumentNullException (nameof (value));
			}

			return AddGlobalVariable (value.GetType (), name, value, options, comment);
		}

		/// <summary>
		/// A shortcut way to add a global variable without first having to create an instance of <see cref="LlvmIrGlobalVariable"/> first.
		/// </summary>
		public LlvmIrGlobalVariable AddGlobalVariable (Type type, string name, object? value, LlvmIrVariableOptions? options = null, string? comment = null)
		{
			LlvmIrGlobalVariable ret;
			if (type == typeof(string)) {
				// The cast to `string?` is intentionally meant to throw if `value` type isn't a string.
				ret = new LlvmIrStringVariable (name, new StringHolder ((string?)value), options) {
					Comment = comment,
				};
				AddStringGlobalVariable ((LlvmIrStringVariable)ret);
			} else {
				ret = new LlvmIrGlobalVariable (type, name, options) {
					Value = value,
					Comment = comment,
				};
				Add (ret);
			}

			return ret;
		}

		public void Add (LlvmIrGlobalVariable variable, string stringGroupName, string? stringGroupComment = null, string? symbolSuffix = null)
		{
			EnsureValidGlobalVariableType (variable);

			if (IsStringVariable (variable)) {
				AddStringGlobalVariable ((LlvmIrStringVariable)variable, stringGroupName, stringGroupComment, symbolSuffix);
				return;
			}

			if (IsStringArrayVariable (variable)) {
				AddStringArrayGlobalVariable (variable, stringGroupName, stringGroupComment, symbolSuffix);
				return;
			}

			throw new InvalidOperationException ("Internal error: this overload is ONLY for adding string or array-of-string variables");
		}

		public void Add (IList<LlvmIrGlobalVariable> variables)
		{
			foreach (LlvmIrGlobalVariable variable in variables) {
				Add (variable);
			}
		}

		public void Add (LlvmIrGlobalVariable variable)
		{
			EnsureValidGlobalVariableType (variable);

			if (IsStringVariable (variable)) {
				AddStringGlobalVariable ((LlvmIrStringVariable)variable);
				return;
			}

			if (IsStringArrayVariable (variable)) {
				AddStringArrayGlobalVariable (variable);
				return;
			}

			if (IsStructureArrayVariable (variable)) {
				AddStructureArrayGlobalVariable (variable);
				return;
			}

			if (IsStructureVariable (variable)) {
				PrepareStructure (variable);
			}

			AddStandardGlobalVariable (variable);
		}

		void PrepareStructure (LlvmIrGlobalVariable variable)
		{
			var structure = variable.Value as StructureInstance;
			if (structure == null) {
				return;
			}

			PrepareStructure (structure);
		}

		void PrepareStructure (StructureInstance structure)
		{
			foreach (StructureMemberInfo smi in structure.Info.Members) {
				if (smi.IsIRStruct (TypeCache)) {
					object? instance = structure.Obj == null ? null : smi.GetValue (structure.Obj);
					if (instance == null) {
						continue;
					}

					StructureInfo si = GetStructureInfo (smi.MemberType);
					PrepareStructure (new GeneratorStructureInstance (si, instance));
					continue;
				}

				if (smi.Info.IsNativePointerToPreallocatedBuffer (TypeCache, out ulong bufferSize)) {
					if (bufferSize == 0) {
						bufferSize = structure.Info.GetBufferSizeFromProvider (smi, structure);
					}

					AddAutomaticBuffer (structure, smi, bufferSize);
					continue;
				}

				if (smi.MemberType != typeof(string)) {
					continue;
				}

				string? value = smi.GetValue (structure.Obj) as string;
				if (value != null) {
					RegisterString (value, stringGroupName: structure.Info.Name, symbolSuffix: smi.Info.Name, encoding: smi.Info.GetStringEncoding (TypeCache));
				}
			}
		}

		void AddAutomaticBuffer (StructureInstance structure, StructureMemberInfo smi, ulong bufferSize)
		{
			if (bufferManager == null) {
				bufferManager = new LlvmIrBufferManager ();
			}

			string bufferName = bufferManager.Allocate (structure, smi, bufferSize);
			var buffer = new LlvmIrGlobalVariable (typeof(List<byte>), bufferName, LlvmIrVariableOptions.LocalWritable) {
				ZeroInitializeArray = true,
				ArrayItemCount = bufferSize,
			};
			Add (buffer);
		}

		void AddStandardGlobalVariable (LlvmIrGlobalVariable variable)
		{
			if (globalVariables == null) {
				globalVariables = new List<LlvmIrGlobalVariable> ();
			}

			globalVariables.Add (variable);
		}

		void AddStringGlobalVariable (LlvmIrStringVariable variable, string? stringGroupName = null, string? stringGroupComment = null, string? symbolSuffix = null)
		{
			RegisterString ((string)variable.Value, stringGroupName, stringGroupComment, symbolSuffix, variable.Encoding);
			AddStandardGlobalVariable (variable);
		}

		public void RegisterString (string value, string? stringGroupName = null, string? stringGroupComment = null, string? symbolSuffix = null,
			LlvmIrStringEncoding encoding = LlvmIrStringEncoding.UTF8, StringComparison comparison = StringComparison.Ordinal)
		{
			if (stringManager == null) {
				stringManager = new LlvmIrStringManager (log);
			}

			stringManager.Add (value, stringGroupName, stringGroupComment, symbolSuffix, encoding, comparison);
		}

		void AddStructureArrayGlobalVariable (LlvmIrGlobalVariable variable)
		{
			if (variable.Value == null) {
				AddStandardGlobalVariable (variable);
				return;
			}

			// For simplicity we support only arrays with homogenous entry types
			StructureInfo? info = null;
			ulong index = 0;

			foreach (StructureInstance structure in (IEnumerable<StructureInstance>)variable.Value) {
				if (info == null) {
					info = structure.Info;
					if (info.HasPreAllocatedBuffers) {
						// let's group them...
						Add (new LlvmIrGroupDelimiterVariable ());
					}
				}

				if (structure.Type != info.Type) {
					throw new InvalidOperationException ($"Internal error: only arrays with homogenous element types are currently supported.  All entries were expected to be of type '{info.Type}', but the '{structure.Type}' type was encountered.");
				}

				// This is a bit of a kludge to make a specific corner case work seamlessly from the LlvmIrModule user's point of view.
				// The scenario is used in ApplicationConfigNativeAssemblyGenerator and it involves an array of structures where each
				// array index contains the same object in structure.Obj but each instance needs to allocate a unique buffer at runtime.
				// LlvmIrBufferManager makes it possible, but it must be able to uniquely identify each instance, which in this scenario
				// wouldn't be possible if we had to rely only on the StructureInstance contents.  Enter `StructureInstance.IndexInArray`,
				// which is used to create unique buffers and unambiguously assign them to each structure instance.
				//
				// See LlvmIrBufferManager for how it is used.
				structure.IndexInArray = index++;

				PrepareStructure (structure);
			}

			if (info != null && info.HasPreAllocatedBuffers) {
				Add (new LlvmIrGroupDelimiterVariable ());
			}

			AddStandardGlobalVariable (variable);
		}

		void AddStringArrayGlobalVariable (LlvmIrGlobalVariable variable, string? stringGroupName = null, string? stringGroupComment = null, string? symbolSuffix = null)
		{
			if (variable.Value == null) {
				AddStandardGlobalVariable (variable);
				return;
			}

			List<string>? entries = null;
			if (NameValueArrayType.IsAssignableFrom (variable.Type)) {
				entries = new List<string> ();
				var dict = (IDictionary<string, string>)variable.Value;
				foreach (var kvp in dict) {
					Register (kvp.Key);
					Register (kvp.Value);
				}
			} else if (typeof(ICollection<string>).IsAssignableFrom (variable.Type)) {
				foreach (string s in (ICollection<string?>)variable.Value) {
					Register (s);
				}
			}  else {
				throw new InvalidOperationException ($"Internal error: unsupported string array type `{variable.Type}'");
			}

			AddStandardGlobalVariable (variable);

			void Register (string? value)
			{
				if (value == null) {
					return;
				}

				RegisterString (value, stringGroupName, stringGroupComment, symbolSuffix);
			}
		}

		bool IsStringArrayVariable (LlvmIrGlobalVariable variable)
		{
			if (NameValueArrayType.IsAssignableFrom (variable.Type)) {
				if (variable.Value != null &&  !NameValueArrayType.IsAssignableFrom (variable.Value.GetType ())) {
					throw new InvalidOperationException ($"Internal error: name:value array variable must have its value set to either `null` or `{NameValueArrayType}`");
				}

				return true;
			}

			var ctype = typeof(ICollection<string>);
			if (ctype.IsAssignableFrom (variable.Type)) {
				if (variable.Value != null && !ctype.IsAssignableFrom (variable.Value.GetType ())) {
					throw new InvalidOperationException ($"Internal error: string array variable must have its value set to either `null` or implement `{ctype}`");
				}

				return true;
			}

			if (variable.Type == typeof(string[])) {
				if (variable.Value != null && variable.Value.GetType () != typeof(string[])) {
					throw new InvalidOperationException ($"Internal error: string array variable must have its value set to either `null` or be `{typeof(string[])}`");
				}

				return true;
			}

			return false;
		}

		bool IsStringVariable (LlvmIrGlobalVariable variable)
		{
			if (variable.Type != typeof(string)) {
				return false;
			}

			if (variable.Value != null && variable.Value.GetType () != typeof(string)) {
				throw new InvalidOperationException ("Internal error: variable of string type must have its value set to either `null` or a string");
			}

			return true;
		}

		bool IsStructureArrayVariable (LlvmIrGlobalVariable variable)
		{
			if (typeof(StructureInstance[]).IsAssignableFrom (variable.Type)) {
				return true;
			}

			if (!variable.Type.IsArray ()) {
				return false;
			}

			Type elementType = variable.Type.GetArrayElementType ();
			return typeof(StructureInstance).IsAssignableFrom (elementType);
		}

		bool IsStructureVariable (LlvmIrGlobalVariable variable)
		{
			if (!typeof(StructureInstance).IsAssignableFrom (variable.Type)) {
				return false;
			}

			if (variable.Value != null && !typeof(StructureInstance).IsAssignableFrom (variable.Value.GetType ())) {
				throw new InvalidOperationException ("Internal error: variable referring to a structure instance must have its value set to either `null` or an instance of the StructureInstance class");
			}

			return true;
		}

		void EnsureValidGlobalVariableType (LlvmIrGlobalVariable variable)
		{
			if (variable is LlvmIrStringVariable) {
				throw new ArgumentException ("Internal error: do not add instances of LlvmIrStringVariable, simply set variable value to the desired string instead");
			}
		}

		/// <summary>
		/// Looks up LLVM variable for a previously registered string given in <paramref name="value"/>.  If a variable isn't found,
		/// an exception is thrown.  This is primarily used by <see cref="LlvmIrGenerator"/> to look up variables related to strings which
		/// are part of structure instances.  Such strings **MUST** be registered by <see cref="LlvmIrModule"/> and, thus, failure to do
		/// so is an internal error.
		/// </summary>
		public LlvmIrStringVariable LookupRequiredVariableForString (StringHolder value)
		{
			LlvmIrStringVariable? sv = stringManager?.Lookup (value);
			if (sv == null) {
				throw new InvalidOperationException ($"Internal error: string '{value}' wasn't registered with string manager");
			}

			return sv;
		}

		public string LookupRequiredBufferVariableName (StructureInstance structure, StructureMemberInfo smi)
		{
			if (bufferManager == null) {
				throw new InvalidOperationException ("Internal error: no buffer variables have been registed with the buffer manager");
			}

			string? variableName = bufferManager.GetBufferVariableName (structure, smi);
			if (String.IsNullOrEmpty (variableName)) {
				throw new InvalidOperationException ($"Internal error: buffer for member '{smi.Info.Name}' of structure '{structure.Info.Name}' (index {structure.IndexInArray}) not found");
			}

			return variableName;
		}

		/// <summary>
		/// Add a new attribute set.  The caller MUST use the returned value to refer to the set, instead of the one passed
		/// as parameter, since this function de-duplicates sets and may return a previously added one that's identical to
		/// the new one.
		/// </summary>
		public LlvmIrFunctionAttributeSet AddAttributeSet (LlvmIrFunctionAttributeSet attrSet)
		{
			if (attributeSets == null) {
				attributeSets = new Dictionary<LlvmIrFunctionAttributeSet, LlvmIrFunctionAttributeSet> ();
			}

			if (attributeSets.TryGetValue (attrSet, out LlvmIrFunctionAttributeSet existingSet)) {
				return existingSet;
			}
			attrSet.Number = (uint)attributeSets.Count;
			attributeSets.Add (attrSet, attrSet);

			return attrSet;
		}

		/// <summary>
		/// Add a new external function declaration.  The caller MUST use the returned value to refer to the function, instead
		/// of the one passed as parameter, since this function de-duplicates function declarations and may return a previously
		/// added one that's identical to the new one.
		/// </summary>
		public LlvmIrFunction DeclareExternalFunction (LlvmIrFunction func)
		{
			if (externalFunctions == null) {
				externalFunctions = new Dictionary<LlvmIrFunction, LlvmIrFunction> ();
			}

			if (externalFunctions.TryGetValue (func, out LlvmIrFunction existingFunc)) {
				return existingFunc;
			}

			externalFunctions.Add (func, func);
			return func;
		}

		public LlvmIrFunction DeclareExternalFunction (LlvmIrFunctionSignature sig, LlvmIrFunctionAttributeSet? attrSet = null)
		{
			return DeclareExternalFunction (new LlvmIrFunction (sig, attrSet));
		}

		/// <summary>
		/// Since LLVM IR is strongly typed, it requires each structure to be properly declared before it is
		/// used throughout the code.  This method uses reflection to scan the managed type <typeparamref name="T"/>
		/// and record the information for future use.  The returned <see cref="StructureInfo<T>"/> structure contains
		/// the description.  It is used later on not only to declare the structure in output code, but also to generate
		/// data from instances of <typeparamref name="T"/>.  This method is typically called from the <see cref="LlvmIrGenerator.MapStructures"/>
		/// method.
		/// </summary>
		public StructureInfo MapStructure<T> ()
		{
			if (structures == null) {
				structures = new Dictionary<Type, StructureInfo> ();
			}

			Type t = typeof(T);
			if (!t.IsClass && !t.IsValueType) {
				throw new InvalidOperationException ($"{t} must be a class or a struct");
			}

			// TODO: check if already there
			if (structures.TryGetValue (t, out StructureInfo sinfo)) {
				return (StructureInfo)sinfo;
			}

			var ret = new StructureInfo (this, t, TypeCache);
			structures.Add (t, ret);

			return ret;
		}

		internal StructureInfo GetStructureInfo (Type type)
		{
			if (structures == null) {
				throw new InvalidOperationException ($"Internal error: no structures have been mapped, cannot return info for {type}");
			}

			foreach (var kvp in structures) {
				StructureInfo si = kvp.Value;
				if (si.Type != type) {
					continue;
				}

				return si;
			}

			throw new InvalidOperationException ($"Internal error: unmapped structure {type}");
		}
	}
}
