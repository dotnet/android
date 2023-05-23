using System;
using System.Globalization;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	abstract class LlvmIrVariable : IEquatable<LlvmIrVariable>
	{
		public abstract bool Global { get; }
		public abstract string NamePrefix { get; }

		public string? Name { get; protected set; }
		public Type Type { get; }

		/// <summary>
		/// Both global and local variables will want their names to matter in equality checks, but function
		/// parameters must not take it into account, thus this property.  If set to <c>false</c>, <see cref="Equals(LlvmIrVariable)"/>
		/// will ignore name when checking for equality.
		protected bool NameMatters { get; set; } = true;

		/// <summary>
		/// Returns a string which constitutes a reference to a local (using the <c>%</c> prefix character) or a global
		/// (using the <c>@</c> prefix character) variable, ready for use in the generated code wherever variables are
		/// referenced.
		/// </summary>
		public string Reference {
			get {
				if (String.IsNullOrEmpty (Name)) {
					throw new InvalidOperationException ("Variable doesn't have a name, it cannot be referenced");
				}

				return $"{NamePrefix}{Name}";
			}
		}

		/// <summary>
		/// Constructs an abstract variable. <paramref name="type"/> is translated to one of the LLVM IR first class types (see
		/// https://llvm.org/docs/LangRef.html#t-firstclass) only if it's an integral or floating point type.  In all other cases it
		/// is treated as an opaque pointer type.
		/// </summary>
		protected LlvmIrVariable (Type type, string? name = null)
		{
			Type = type;
			Name = name;
		}

		public override int GetHashCode ()
		{
			return Type.GetHashCode () ^ (Name?.GetHashCode () ?? 0);
		}

		public override bool Equals (object obj)
		{
			var irVar = obj as LlvmIrVariable;
			if (irVar == null) {
				return false;
			}

			return Equals (irVar);
		}

		public virtual bool Equals (LlvmIrVariable other)
		{
			if (other == null) {
				return false;
			}

			return
				Global == other.Global &&
				Type == other.Type &&
				String.Compare (NamePrefix, other.NamePrefix, StringComparison.Ordinal) == 0 &&
				(!NameMatters || String.Compare (Name, other.Name, StringComparison.Ordinal) == 0);
		}
	}

	class LlvmIrLocalVariable : LlvmIrVariable
	{
		public override bool Global => false;
		public override string NamePrefix => "%";

		/// <summary>
		/// Constructs a local variable. <paramref name="type"/> is translated to one of the LLVM IR first class types (see
		/// https://llvm.org/docs/LangRef.html#t-firstclass) only if it's an integral or floating point type.  In all other cases it
		/// is treated as an opaque pointer type.  <paramref name="name"/> is optional because local variables can be unnamed, in
		/// which case they will be assigned a sequential number when function code is generated.
		/// </summary>
		public LlvmIrLocalVariable (Type type, string? name = null)
			: base (type, name)
		{}

		public void AssignNumber (uint n)
		{
			Name = n.ToString (CultureInfo.InvariantCulture);
		}
	}

	class LlvmIrGlobalVariable : LlvmIrVariable
	{
		public override bool Global => true;
		public override string NamePrefix => "@";

		/// <summary>
		/// Constructs a local variable. <paramref name="type"/> is translated to one of the LLVM IR first class types (see
		/// https://llvm.org/docs/LangRef.html#t-firstclass) only if it's an integral or floating point type.  In all other cases it
		/// is treated as an opaque pointer type.  <paramref name="name"/> is required because global variables must not be unnamed.
		/// </summary>
		public LlvmIrGlobalVariable (Type type, string name)
			: base (type, name)
		{
			if (String.IsNullOrEmpty (name)) {
				throw new ArgumentException ("must not be null or empty", nameof (name));
			}
		}
	}
}

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Base class for all the variable (local and global) as well as function parameter classes.
	/// </summary>
	abstract class LlvmIrVariable
	{
		public LlvmNativeFunctionSignature? NativeFunction { get; }
		public string? Name                                { get; }
		public Type Type                                   { get; }

		// Used when we need a pointer to pointer (etc) or when the type itself is not a pointer but we need one
		// in a given context (e.g. function parameters)
		public bool IsNativePointer                        { get; }

		protected LlvmIrVariable (Type type, string name, LlvmNativeFunctionSignature? signature, bool isNativePointer)
		{
			Type = type ?? throw new ArgumentNullException (nameof (type));
			Name = name;
			NativeFunction = signature;
			IsNativePointer = isNativePointer;
		}

		protected LlvmIrVariable (LlvmIrVariable variable, string name, bool isNativePointer)
		{
			Type = variable?.Type ?? throw new ArgumentNullException (nameof (variable));
			Name = name;
			NativeFunction = variable.NativeFunction;
			IsNativePointer = isNativePointer;
		}
	}
}
