using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	// TODO: remove these aliases once the refactoring is done
	using LlvmIrVariableOptions = LLVMIR.LlvmIrVariableOptions;

	partial class LlvmIrModule
	{
		/// <summary>
		/// Global variable type to be used to output name:value string arrays. This is a notational shortcut,
		/// do **NOT** change the type without understanding how it affects the rest of code.
		/// </summary>
		public static readonly Type NameValueArrayType = typeof(IDictionary<string, string>);

		public IList<LlvmIrFunction>? ExternalFunctions         { get; private set; }
		public IList<LlvmIrFunctionAttributeSet>? AttributeSets { get; private set; }
		public IList<StructureInfo>? Structures                { get; private set; }
		public IList<LlvmIrGlobalVariable>? GlobalVariables     { get; private set; }
		public IList<LlvmIrStringGroup>? Strings                { get; private set; }

		Dictionary<LlvmIrFunctionAttributeSet, LlvmIrFunctionAttributeSet>? attributeSets;
		Dictionary<LlvmIrFunction, LlvmIrFunction>? externalFunctions;
		Dictionary<Type, StructureInfo>? structures;
		LlvmIrStringManager? stringManager;

		List<LlvmIrGlobalVariable>? globalVariables;

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
			var ret = new LlvmIrGlobalVariable (type, name, options) {
				Value = value,
				Comment = comment,
			};
			Add (ret);
			return ret;
		}

		public void Add (LlvmIrGlobalVariable variable, string stringGroupName, string? stringGroupComment = null, string? symbolSuffix = null)
		{
			EnsureValidGlobalVariableType (variable);

			if (IsStringVariable (variable)) {
				AddStringGlobalVariable (variable, stringGroupName, stringGroupComment, symbolSuffix);
				return;
			}

			if (IsStringArrayVariable (variable)) {
				AddStringArrayGlobalVariable (variable, stringGroupName, stringGroupComment, symbolSuffix);
				return;
			}

			throw new InvalidOperationException ("Internal error: this overload is ONLY for adding string or array-of-string variables");
		}

		public void Add (LlvmIrGlobalVariable variable)
		{
			EnsureValidGlobalVariableType (variable);

			if (IsStringVariable (variable)) {
				AddStringGlobalVariable (variable);
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
				if (smi.MemberType != typeof(string)) {
					continue;
				}

				string? value = smi.GetValue (structure.Obj) as string;
				if (!String.IsNullOrEmpty (value)) {
					RegisterString (value, stringGroupName: structure.Info.Name, symbolSuffix: smi.Info.Name);
				}
			}
		}

		void AddStandardGlobalVariable (LlvmIrGlobalVariable variable)
		{
			if (globalVariables == null) {
				globalVariables = new List<LlvmIrGlobalVariable> ();
			}

			globalVariables.Add (variable);
		}

		void AddStringGlobalVariable (LlvmIrGlobalVariable variable, string? stringGroupName = null, string? stringGroupComment = null, string? symbolSuffix = null)
		{
			RegisterString (variable, stringGroupName, stringGroupComment, symbolSuffix);
			AddStandardGlobalVariable (variable);
		}

		void RegisterString (LlvmIrGlobalVariable variable, string? stringGroupName = null, string? stringGroupComment = null, string? symbolSuffix = null)
		{
			RegisterString ((string)variable.Value, stringGroupName, stringGroupComment, symbolSuffix);
		}

		void RegisterString (string value, string? stringGroupName = null, string? stringGroupComment = null, string? symbolSuffix = null)
		{
			if (stringManager == null) {
				stringManager = new LlvmIrStringManager ();
			}

			stringManager.Add (value, stringGroupName, stringGroupComment, symbolSuffix);
		}

		void AddStructureArrayGlobalVariable (LlvmIrGlobalVariable variable)
		{
			if (variable.Value == null) {
				AddStandardGlobalVariable (variable);
				return;
			}

			// For simplicity we support only arrays with homogenous entry types
			StructureInfo? info = null;
			foreach (StructureInstance structure in (IEnumerable<StructureInstance>)variable.Value) {
				if (info == null) {
					info = structure.Info;
				}

				if (structure.Type != info.Type) {
					throw new InvalidOperationException ($"Internal error: only arrays with homogenous element types are currently supported.  All entries were expected to be of type '{info.Type}', but the '{structure.Type}' type was encountered.");
				}

				PrepareStructure (structure);
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
				foreach (string s in (ICollection<string>)variable.Value) {
					Register (s);
				}
			}  else {
				throw new InvalidOperationException ($"Internal error: unsupported string array type `{variable.Type}'");
			}

			AddStandardGlobalVariable (variable);

			void Register (string value)
			{
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
		public LlvmIrStringVariable LookupRequiredVariableForString (string value)
		{
			LlvmIrStringVariable? sv = stringManager?.Lookup (value);
			if (sv == null) {
				throw new InvalidOperationException ($"Internal error: string '{value}' wasn't registered with string manager");
			}

			return sv;
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
			Console.WriteLine ($"Mapping structure: {typeof(T)}");
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

			var ret = new StructureInfo (this, typeof(T));
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
