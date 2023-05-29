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
		public IList<IStructureInfo>? Structures                { get; private set; }
		public IList<LlvmIrGlobalVariable>? GlobalVariables     { get; private set; }
		public IList<LlvmIrStringGroup>? Strings                { get; private set; }

		Dictionary<LlvmIrFunctionAttributeSet, LlvmIrFunctionAttributeSet>? attributeSets;
		Dictionary<LlvmIrFunction, LlvmIrFunction>? externalFunctions;
		Dictionary<Type, IStructureInfo>? structures;
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
				List<IStructureInfo> list = structures.Values.ToList ();
				list.Sort ((IStructureInfo a, IStructureInfo b) => a.Name.CompareTo (b.Name));
				Structures = list.AsReadOnly ();
			}

			if (stringManager != null && stringManager.StringGroups.Count > 0) {
				Strings = stringManager.StringGroups.AsReadOnly ();
			}

			GlobalVariables = globalVariables?.AsReadOnly ();
		}

		/// <summary>
		/// A shortcut way to add a global variable without first having to create an instance of <see cref="LlvmIrGlobalVariable"/> first.
		/// </summary>
		public LlvmIrGlobalVariable AddGlobalVariable (Type type, string name, object? value, LlvmIrVariableOptions? options = null)
		{
			var ret = new LlvmIrGlobalVariable (type, name, options) { Value = value };
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

			throw new InvalidOperationException ("Internal error: this overload is for adding ONLY string or array-of-string variables");
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

			AddStandardGlobalVariable (variable);
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
			if (stringManager == null) {
				stringManager = new LlvmIrStringManager ();
			}

			LlvmIrStringVariable sv = RegisterString (variable, stringGroupName, stringGroupComment, symbolSuffix);
			variable.Value = sv;

			AddStandardGlobalVariable (variable);
		}

		LlvmIrStringVariable RegisterString (LlvmIrGlobalVariable variable, string? stringGroupName = null, string? stringGroupComment = null, string? symbolSuffix = null)
		{
			return RegisterString ((string)variable.Value, stringGroupName, stringGroupComment, symbolSuffix);
		}

		LlvmIrStringVariable RegisterString (string value, string? stringGroupName = null, string? stringGroupComment = null, string? symbolSuffix = null)
		{
			LlvmIrStringVariable sv = stringManager.Add (value, stringGroupName, stringGroupComment, symbolSuffix);
			return sv;
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
					entries.Add (kvp.Key);
					entries.Add (kvp.Value);
				}
			} else if (typeof(ICollection<string>).IsAssignableFrom (variable.Type)) {
				entries = new List<string> ((ICollection<string>)variable.Value);
			} else if (variable.Type == typeof(string[])) {
				entries = new List<string> ((string[])variable.Value);
			} else {
				throw new InvalidOperationException ($"Internal error: unsupported array string type `{variable.Type}'");
			}

			var strings = new List<LlvmIrStringVariable> ();
			foreach (string entry in entries) {
				var sv = RegisterString (entry, stringGroupName, stringGroupComment, symbolSuffix);
				strings.Add (sv);
			}

			var arrayInfo = new LlvmIrArrayVariableInfo (typeof(LlvmIrStringVariable), strings, variable.Value);
			variable.OverrideValue (typeof(LlvmIrArrayVariableInfo), arrayInfo);
			AddStandardGlobalVariable (variable);
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

			if (variable.Type.IsArray && variable.Type.GetElementType () == typeof(string)) {
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

		void EnsureValidGlobalVariableType (LlvmIrGlobalVariable variable)
		{
			if (variable is LlvmIrStringVariable) {
				throw new ArgumentException ("Internal error: do not add instances of LlvmIrStringVariable, simply set variable value to the desired string instead");
			}
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
		public StructureInfo<T> MapStructure<T> ()
		{
			Console.WriteLine ($"Mapping structure: {typeof(T)}");
			if (structures == null) {
				structures = new Dictionary<Type, IStructureInfo> ();
			}

			Type t = typeof(T);
			if (!t.IsClass && !t.IsValueType) {
				throw new InvalidOperationException ($"{t} must be a class or a struct");
			}

			// TODO: check if already there
			if (structures.TryGetValue (t, out IStructureInfo sinfo)) {
				return (StructureInfo<T>)sinfo;
			}

			var ret = new StructureInfo<T> (this);
			structures.Add (t, ret);

			return ret;
		}

		internal IStructureInfo GetStructureInfo (Type type)
		{
			if (structures == null) {
				throw new InvalidOperationException ($"Internal error: no structures have been mapped, cannot return info for {type}");
			}

			foreach (var kvp in structures) {
				IStructureInfo si = kvp.Value;
				if (si.Type != type) {
					continue;
				}

				return si;
			}

			throw new InvalidOperationException ($"Unmapped structure {type}");
		}
	}
}
