using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVMIR
{
	interface ILlvmIrSavedFunctionParameterState {}

	class LlvmIrFunctionParameter : LlvmIrLocalVariable
	{
		sealed class SavedParameterState : ILlvmIrSavedFunctionParameterState
		{
			public readonly LlvmIrFunctionParameter Owner;

			public uint? Align;
			public bool? AllocPtr;
			public uint? Dereferenceable;
			public bool? ImmArg;
			public bool? NoCapture;
			public bool? NonNull;
			public bool? NoUndef;
			public bool? ReadNone;
			public bool? SignExt;
			public bool? ZeroExt;
			public bool? WriteOnly;
			public bool? IsCplusPlusReference;
			public bool IsVarArgs;

			public SavedParameterState (LlvmIrFunctionParameter owner, SavedParameterState? previousState = null)
			{
				Owner = owner;
				if (previousState == null) {
					return;
				}

				Align = previousState.Align;
				AllocPtr = previousState.AllocPtr;
				Dereferenceable = previousState.Dereferenceable;
				ImmArg = previousState.ImmArg;
				NoCapture = previousState.NoCapture;
				NonNull = previousState.NonNull;
				ReadNone = previousState.ReadNone;
				SignExt = previousState.SignExt;
				ZeroExt = previousState.ZeroExt;
				WriteOnly = previousState.WriteOnly;
				IsCplusPlusReference = previousState.IsCplusPlusReference;
				IsVarArgs = previousState.IsVarArgs;
			}
		}

		SavedParameterState state;

		// To save on time, we declare only attributes that are actually used in our generated code.  More will be added, as needed.

		/// <summary>
		/// <c>align(n)</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1239"/>.
		/// As a special case for us, a value of <c>0</c> means use the natural target pointer alignment.
		/// </summary>
		public uint? Align {
			get => state.Align;
			set => state.Align = value;
		}

		/// <summary>
		/// <c>allocptr</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1413"/>
		/// </summary>
		public bool? AllocPtr {
			get => state.AllocPtr;
			set => state.AllocPtr = value;
		}

		/// <summary>
		/// <c>dereferenceable(n)</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1324"/>.
		/// As a special case for us, a value of <c>0</c> means use the natural target pointer alignment.
		/// </summary>
		public uint? Dereferenceable {
			get => state.Dereferenceable;
			set => state.Dereferenceable = value;
		}

		/// <summary>
		/// <c>immarg</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1383"/>
		/// </summary>
		public bool? ImmArg {
			get => state.ImmArg;
			set => state.ImmArg = value;
		}

		/// <summary>
		/// <c>nocapture</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1279"/>
		/// </summary>
		public bool? NoCapture {
			get => state.NoCapture;
			set => state.NoCapture = value;
		}

		/// <summary>
		/// <c>nonnull</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1316"/>
		/// </summary>
		public bool? NonNull {
			get => state.NonNull;
			set => state.NonNull = value;
		}

		/// <summary>
		/// <c>noundef</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1390"/>
		/// </summary>
		public bool? NoUndef {
			get => state.NoUndef;
			set => state.NoUndef = value;
		}

		/// <summary>
		/// <c>readnone</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1420"/>
		/// </summary>
		public bool? ReadNone {
			get => state.ReadNone;
			set => state.ReadNone = value;
		}

		/// <summary>
		/// <c>signext</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1094"/>
		/// </summary>
		public bool? SignExt {
			get => state.SignExt;
			set => state.SignExt = value;
		}

		/// <summary>
		/// <c>zeroext</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1090"/>
		/// </summary>
		public bool? ZeroExt {
			get => state.ZeroExt;
			set => state.ZeroExt = value;
		}

		/// <summary>
		/// <c>writeonly</c> attribute, see <see href="https://github.com/llvm/llvm-project/blob/5729e63ac7b47c6ad40f904fedafad3c07cf71ea/llvm/docs/LangRef.rst#L1436"/>
		/// </summary>
		public bool? WriteOnly {
			get => state.WriteOnly;
			set => state.WriteOnly = value;
		}

		/// <summary>
		/// This serves a purely documentational purpose, when generating comments about types.  It describes a parameter that is a C++ reference, something we can't
		/// reflect on the managed side.
		/// </summary>
		public bool? IsCplusPlusReference {
			get => state.IsCplusPlusReference;
			set => state.IsCplusPlusReference = value;
		}

		/// <summary>
		/// Indicates that the argument is a C variable arguments placeholder (`...`)
		/// </summary>
		public bool IsVarArgs {
			get => state.IsVarArgs;
			set => state.IsVarArgs = value;
		}

		public LlvmIrFunctionParameter (Type type, string? name = null)
			: base (type, name)
		{
			NameMatters = false;
			state = new SavedParameterState (this);

			// TODO: check why it doesn't work as expected - can't see the flags set in the output
			if (type == typeof(sbyte) || type == typeof (short)) {
				SignExt = true;
			} else if (type == typeof(byte) || type == typeof (ushort)) {
				ZeroExt = true;
			}
		}

		/// <summary>
		/// Save (opaque) parameter state.  This is necessary because we generate code from the same model (module) for different
		/// targets.  At the same time, function, signature and parameter instances are shared between the different code generation
		/// sessions, so we must sure the state as set by the model is properly preserved. NOTE: it does NOT make the code thread-safe!
		/// Instances are **still** shared and thus different threads would step on each other's toes should they saved and restored
		/// state without synchronization.
		/// </summary>
		public ILlvmIrSavedFunctionParameterState SaveState ()
		{
			SavedParameterState ret = state;
			state = new SavedParameterState (this, ret);
			return ret;
		}

		/// <summary>
		/// Restore (opaque) state. <see cref="SaveState()"/> for more info
		/// </summary>
		public void RestoreState (ILlvmIrSavedFunctionParameterState savedState)
		{
			var oldState = savedState as SavedParameterState;
			if (oldState == null) {
				throw new InvalidOperationException ("Internal error: savedState not an instance of ParameterState");
			}

			if (oldState.Owner != this) {
				throw new InvalidOperationException ("Internal error: savedState not saved by this instance");
			}

			state = oldState;
		}
	}

	interface ILlvmIrSavedFunctionSignatureState {}

	class LlvmIrFunctionSignature : IEquatable<LlvmIrFunctionSignature>
	{
		public sealed class ReturnTypeAttributes
		{
			public bool? InReg;
			public bool? NoUndef;
			public bool? SignExt;
			public bool? ZeroExt;
			public bool? NonNull;

			public ReturnTypeAttributes ()
			{}

			public ReturnTypeAttributes (ReturnTypeAttributes other)
			{
				InReg = other.InReg;
				NoUndef = other.NoUndef;
				NonNull = other.NonNull;
				SignExt = other.SignExt;
				ZeroExt = other.ZeroExt;
			}
		}

		sealed class SavedSignatureState : ILlvmIrSavedFunctionSignatureState
		{
			public readonly LlvmIrFunctionSignature Owner;
			public readonly IList<ILlvmIrSavedFunctionParameterState> ParameterStates;
			public readonly ReturnTypeAttributes ReturnAttributes;

			public SavedSignatureState (LlvmIrFunctionSignature owner, IList<ILlvmIrSavedFunctionParameterState> parameterStates, ReturnTypeAttributes returnAttributes)
			{
				Owner = owner;
				ParameterStates = parameterStates;
				ReturnAttributes = returnAttributes;
			}
		}

		ReturnTypeAttributes returnAttributes;

		public string Name                               { get; }
		public Type ReturnType                           { get; }
		public ReturnTypeAttributes ReturnAttributes     => returnAttributes;
		public IList<LlvmIrFunctionParameter> Parameters { get; }

		public LlvmIrFunctionSignature (string name, Type returnType, IList<LlvmIrFunctionParameter>? parameters = null, ReturnTypeAttributes? returnAttributes = null)
		{
			if (String.IsNullOrEmpty (name)) {
				throw new ArgumentException ("must not be null or empty", nameof (name));
			}

			Name = name;
			ReturnType = returnType;
			this.returnAttributes = returnAttributes ?? new ReturnTypeAttributes ();
			Parameters = parameters ?? new List<LlvmIrFunctionParameter> ();
		}

		/// <summary>
		/// Create new signature using data from the <see cref="templateSignature"/> one, with the exception of name.
		/// Useful when there are several functions with different names but identical parameters and return types.
		/// </summary>
		public LlvmIrFunctionSignature (string name, LlvmIrFunctionSignature templateSignature)
			: this (name, templateSignature.ReturnType, templateSignature.Parameters)
		{}

		/// <summary>
		/// Save (opaque) signature state.  This includes states of all the parameters. <see cref="LlvmIrFunctionParameter.SaveState()"/>
		/// for more information.
		/// </summary>
		public ILlvmIrSavedFunctionSignatureState SaveState ()
		{
			var list = new List<ILlvmIrSavedFunctionParameterState> ();

			foreach (LlvmIrFunctionParameter parameter in Parameters) {
				list.Add (parameter.SaveState ());
			}

			var ret = new SavedSignatureState (this, list.AsReadOnly (), returnAttributes);
			returnAttributes = new ReturnTypeAttributes (returnAttributes);
			return ret;
		}

		/// <summary>
		/// Restore (opaque) signature state.  This includes states of all the parameters. <see cref="LlvmIrFunctionParameter.RestoreState(ILlvmIrSavedFunctionParameterState)"/>
		/// for more information.
		/// </summary>
		public void RestoreState (ILlvmIrSavedFunctionSignatureState savedState)
		{
			var oldState = savedState as SavedSignatureState;
			if (oldState == null) {
				throw new InvalidOperationException ($"Internal error: savedState not an instance of {nameof(SavedSignatureState)}");
			}

			if (oldState.Owner != this) {
				throw new InvalidOperationException ("Internal error: savedState not saved by this instance");
			}

			for (int i = 0; i < oldState.ParameterStates.Count; i++) {
				ILlvmIrSavedFunctionParameterState parameterState = oldState.ParameterStates[i];
				Parameters[i].RestoreState (parameterState);
			}
			returnAttributes = new ReturnTypeAttributes (oldState.ReturnAttributes);
		}

		public override int GetHashCode ()
		{
			int hc =
				Name.GetHashCode () ^
				Parameters.GetHashCode () ^
				ReturnType.GetHashCode ();

			foreach (LlvmIrFunctionParameter p in Parameters) {
				hc ^= p.GetHashCode ();
			}

			return hc;
		}

		public override bool Equals (object obj)
		{
			var sig = obj as LlvmIrFunctionSignature;
			if (sig == null) {
				return false;
			}

			return Equals (sig);
		}

		public bool Equals (LlvmIrFunctionSignature other)
		{
			if (other == null) {
				return false;
			}

			if (Parameters.Count != other.Parameters.Count ||
			    ReturnType != other.ReturnType ||
			    String.Compare (Name, other.Name, StringComparison.Ordinal) != 0
			) {
				return false;
			}

			for (int i = 0; i < Parameters.Count; i++) {
				if (Parameters[i] != other.Parameters[i]) {
					return false;
				}
			}

			return true;
		}
	}

	interface ILlvmIrSavedFunctionState {}

	/// <summary>
	/// Describes a native function to be emitted or declared and keeps code emitting state between calls to various generator.
	/// methods.
	/// </summary>
	class LlvmIrFunction : IEquatable<LlvmIrFunction>
	{
		public class FunctionState
		{
			// Counter shared by unnamed local variables (including function parameters) and unnamed labels.
			ulong unnamedTemporaryCounter = 0;

			// Implicit unnamed label at the start of the function
			ulong? startingBlockNumber;

			public ulong StartingBlockNumber {
				get {
					if (startingBlockNumber.HasValue) {
						return startingBlockNumber.Value;
					}

					throw new InvalidOperationException ($"Internal error: starting block number not set");
				}
			}

			public FunctionState ()
			{}

			public ulong NextTemporary ()
			{
				ulong ret = unnamedTemporaryCounter++;
				return ret;
			}

			public void ConfigureStartingBlockNumber ()
			{
				if (startingBlockNumber.HasValue) {
					return;
				}

				startingBlockNumber = unnamedTemporaryCounter++;
			}
		}

		sealed class SavedFunctionState : ILlvmIrSavedFunctionState
		{
			public readonly LlvmIrFunction Owner;
			public readonly ILlvmIrSavedFunctionSignatureState SignatureState;

			public SavedFunctionState (LlvmIrFunction owner, ILlvmIrSavedFunctionSignatureState signatureState)
			{
				Owner = owner;
				SignatureState = signatureState;
			}
		}

		FunctionState functionState;

		public LlvmIrFunctionSignature Signature             { get; }
		public LlvmIrAddressSignificance AddressSignificance { get; set; } = LlvmIrAddressSignificance.LocalUnnamed;
		public LlvmIrFunctionAttributeSet? AttributeSet      { get; set; }
		public LlvmIrLinkage Linkage                         { get; set; } = LlvmIrLinkage.Default;
		public LlvmIrRuntimePreemption RuntimePreemption     { get; set; } = LlvmIrRuntimePreemption.Default;
		public LlvmIrVisibility Visibility                   { get; set; } = LlvmIrVisibility.Default;
		public LlvmIrCallingConvention CallingConvention     { get; set; } = LlvmIrCallingConvention.Default;
		public LlvmIrFunctionBody Body                       { get; }
		public string? Comment                               { get; set; }
		public bool ReturnsValue                             => Signature.ReturnType != typeof(void);
		public bool UsesVarArgs                              { get; }

		public LlvmIrFunction (LlvmIrFunctionSignature signature, LlvmIrFunctionAttributeSet? attributeSet = null)
		{
			Signature = signature;
			AttributeSet = attributeSet;

			functionState = new FunctionState ();
			foreach (LlvmIrFunctionParameter parameter in signature.Parameters) {
				if (UsesVarArgs) {
					throw new InvalidOperationException ($"Internal error: function '{signature.Name}' uses variable arguments and it has at least one argument following the varargs (...) one. This is not allowed.");
				}

				if (parameter.IsVarArgs) {
					UsesVarArgs = true;
					continue;
				}

				if (!String.IsNullOrEmpty (parameter.Name)) {
					continue;
				}

				parameter.AssignNumber (functionState.NextTemporary ());
			}
			functionState.ConfigureStartingBlockNumber ();

			Body = new LlvmIrFunctionBody (this, functionState);
		}

		/// <summary>
		/// Create new function using data from the <see cref="templateSignature"/> signature, with the exception of name.
		/// Useful when there are several functions with different names but identical parameters and return types.
		/// </summary>
		public LlvmIrFunction (string name, LlvmIrFunctionSignature templateSignature, LlvmIrFunctionAttributeSet? attributeSet = null)
			: this (new LlvmIrFunctionSignature (name, templateSignature), attributeSet)
		{}

		public LlvmIrFunction (string name, Type returnType, List<LlvmIrFunctionParameter>? parameters = null, LlvmIrFunctionAttributeSet? attributeSet = null)
			: this (new LlvmIrFunctionSignature (name, returnType, parameters), attributeSet)
		{}

		/// <summary>
		/// Creates a local variable which, if <paramref name="name"/> is <c>null</c> or empty, is assinged the correct
		/// name based on a counter local to the function.
		/// </summary>
		public LlvmIrLocalVariable CreateLocalVariable (Type type, string? name = null)
		{
			var ret = new LlvmIrLocalVariable (type, name);
			if (String.IsNullOrEmpty (name)) {
				ret.AssignNumber (functionState.NextTemporary ());
			}

			return ret;
		}

		/// <summary>
		/// Save (opaque) function state.  This includes signature state. <see cref="LlvmIrFunctionSignature.SaveState()"/>
		/// for more information.
		/// </summary>
		public ILlvmIrSavedFunctionState SaveState ()
		{
			return new SavedFunctionState (this, Signature.SaveState ());
		}

		/// <summary>
		/// Restore (opaque) function state.  This includes signature state. <see cref="LlvmIrFunctionSignature.RestoreState(ILlvmIrSavedFunctionSignatureState)"/>
		/// for more information.
		/// </summary>
		public void RestoreState (ILlvmIrSavedFunctionState savedState)
		{
			var oldState = savedState as SavedFunctionState;
			if (oldState == null) {
				throw new InvalidOperationException ($"Internal error: savedState not an instance of {nameof(SavedFunctionState)}");
			}

			if (oldState.Owner != this) {
				throw new InvalidOperationException ("Internal error: savedState not saved by this instance");
			}

			Signature.RestoreState (oldState.SignatureState);
		}

		public override int GetHashCode ()
		{
			return Signature.GetHashCode ();
		}

		public override bool Equals (object obj)
		{
			var func = obj as LlvmIrFunction;
			if (func == null) {
				return false;
			}

			return Equals (func);
		}

	        public bool Equals (LlvmIrFunction other)
		{
			if (other == null) {
				return false;
			}

			return Signature == other.Signature;
		}
	}
}
