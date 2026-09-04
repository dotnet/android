extern alias TestFixtures;
extern alias Lookalikes;

using System;
using Android.Runtime;
using ExternalCollisionParameter = TestFixtures::MyApp.CrossAssemblyCollisionParameter;
using LookalikeInt32 = Lookalikes::System.Int32;
using LookalikeIntPtr = Lookalikes::System.IntPtr;
using LookalikeJniObjectReference = Lookalikes::Java.Interop.JniObjectReference;
using LookalikeJniObjectReferenceOptions = Lookalikes::Java.Interop.JniObjectReferenceOptions;
using LookalikeOwnership = Lookalikes::Android.Runtime.JniHandleOwnership;

namespace MyApp;

public enum ConstructorKind
{
	None,
}

[Register ("my/app/for")]
public class ReservedJavaNameActivity : Android.App.Activity
{
}

[Register ("my/app/EnumCtorActivity")]
public class EnumCtorActivity : Android.App.Activity
{
	public EnumCtorActivity (ConstructorKind value) { }
}

[Register ("my/app/IntSignatureBase", DoNotGenerateAcw = true)]
public class IntSignatureBase : Java.Lang.Object
{
	[Register (".ctor", "(I)V", "")]
	public IntSignatureBase (int value) { }
}

[Register ("my/app/LongSignatureBase", DoNotGenerateAcw = true)]
public class LongSignatureBase : Java.Lang.Object
{
	[Register (".ctor", "(J)V", "")]
	public LongSignatureBase (long value) { }
}

[Register ("my/app/ExplicitPointerCtorActivity")]
public unsafe class ExplicitPointerCtorActivity : LongSignatureBase
{
	[Register (".ctor", "(J)V", "")]
	public ExplicitPointerCtorActivity (int* value) : base ((long)value) { }
}

[Register ("my/app/ExplicitRegisterCollisionActivity")]
public class ExplicitRegisterCollisionActivity : IntSignatureBase
{
	[Register (".ctor", "(I)V", "")]
	public ExplicitRegisterCollisionActivity (int value) : base (value) { }

	[Register (".ctor", "(I)V", "")]
	public ExplicitRegisterCollisionActivity (uint value) : base ((int)value) { }
}

[Register ("my/app/JniSignatureCollisionActivity")]
public class JniSignatureCollisionActivity : IntSignatureBase
{
	[Java.Interop.JniConstructorSignature ("(I)V")]
	public JniSignatureCollisionActivity (int value) : base (value) { }

	[Java.Interop.JniConstructorSignature ("(I)V")]
	public JniSignatureCollisionActivity (uint value) : base ((int)value) { }
}

[Register ("my/app/ExplicitRegisterCompatibleBaseActivity")]
public class ExplicitRegisterCompatibleBaseActivity : IntSignatureBase
{
	[Register (".ctor", "(I)V", "")]
	public ExplicitRegisterCompatibleBaseActivity (uint value) : base ((int)value) { }
}

[Register ("my/app/JniSignatureCompatibleBaseActivity")]
public class JniSignatureCompatibleBaseActivity : IntSignatureBase
{
	[Java.Interop.JniConstructorSignature ("(I)V")]
	public JniSignatureCompatibleBaseActivity (uint value) : base ((int)value) { }
}

[Register ("my/app/ImplicitJniCompatibleBaseActivity")]
public class ImplicitJniCompatibleBaseActivity : IntSignatureBase
{
	public ImplicitJniCompatibleBaseActivity (uint value) : base ((int)value) { }
}

[Register ("my/app/RegisterBeforeExportValidActivity")]
public class RegisterBeforeExportValidActivity : IntSignatureBase
{
	[Register (".ctor", "(I)V", "")]
	[Java.Interop.Export (".ctor")]
	public RegisterBeforeExportValidActivity (uint value) : base ((int)value) { }
}

[Register ("my/app/ExportBeforeRegisterValidActivity")]
public class ExportBeforeRegisterValidActivity : IntSignatureBase
{
	[Java.Interop.Export (".ctor")]
	[Register (".ctor", "(I)V", "")]
	public ExportBeforeRegisterValidActivity (uint value) : base ((int)value) { }
}

[Register ("my/app/JniBeforeExportValidActivity")]
public class JniBeforeExportValidActivity : Android.App.Activity
{
	[Java.Interop.JniConstructorSignature ("(I)V")]
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public JniBeforeExportValidActivity (uint value) { }
}

[Register ("my/app/ExportBeforeJniValidActivity")]
public class ExportBeforeJniValidActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	[Java.Interop.JniConstructorSignature ("(I)V")]
	public ExportBeforeJniValidActivity (uint value) { }
}

[Register ("my/app/BindingPointerCtor", DoNotGenerateAcw = true)]
public unsafe class BindingPointerCtor : Java.Lang.Object
{
	[Register (".ctor", "(J)V", "")]
	public BindingPointerCtor (int* value) { }
}

[Register ("my/app/SignedUnsignedCollisionActivity")]
public class SignedUnsignedCollisionActivity : Android.App.Activity
{
	public SignedUnsignedCollisionActivity (int value) { }
	public SignedUnsignedCollisionActivity (uint value) { }
	public SignedUnsignedCollisionActivity (ConstructorKind value) { }
}

[Register ("my/app/AliasOne", DoNotGenerateAcw = true)]
public class AliasOne : Java.Lang.Object
{
	protected AliasOne (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
}

[Register ("my/app/AliasOne", DoNotGenerateAcw = true)]
public class AliasTwo : Java.Lang.Object
{
	protected AliasTwo (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) { }
}

[Register ("my/app/AliasedTypeCollisionActivity")]
public class AliasedTypeCollisionActivity : Android.App.Activity
{
	public AliasedTypeCollisionActivity (AliasOne value) { }
	public AliasedTypeCollisionActivity (AliasTwo value) { }
}

[Register ("my/app/CrossAssemblyParameter", DoNotGenerateAcw = true)]
public class CrossAssemblyCollisionParameter : Java.Lang.Object
{
}

public sealed class CrossAssemblyBorrowedParameter
{
}

[Register ("my/app/CrossAssemblyCollisionActivity")]
public class CrossAssemblyCollisionActivity : Android.App.Activity
{
	public CrossAssemblyCollisionActivity (CrossAssemblyCollisionParameter value) { }
	public CrossAssemblyCollisionActivity (ExternalCollisionParameter value) { }
}

[Register ("my/app/CrossAssemblyBorrowActivity")]
public class CrossAssemblyBorrowActivity : Android.App.Activity
{
	public CrossAssemblyBorrowActivity (CrossAssemblyBorrowedParameter value) { }
}

[Register ("my/app/PrimitiveLookalikeActivity")]
public class PrimitiveLookalikeActivity
{
	public PrimitiveLookalikeActivity (LookalikeInt32 value) { }
}

[Register ("my/app/XamarinActivationLookalikeActivity")]
public class XamarinActivationLookalikeActivity
{
	public XamarinActivationLookalikeActivity (LookalikeIntPtr handle, LookalikeOwnership ownership) { }
}

[Register ("my/app/JniActivationLookalikeActivity")]
public class JniActivationLookalikeActivity
{
	public JniActivationLookalikeActivity (LookalikeJniObjectReference reference, LookalikeJniObjectReferenceOptions options) { }
}

[Register ("my/app/GenericParameterCtorActivity")]
public class GenericParameterCtorActivity<T> : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public GenericParameterCtorActivity (T value) { }
}

[Register ("my/app/GenericInstantiationCtorActivity")]
public class GenericInstantiationCtorActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public GenericInstantiationCtorActivity (System.Collections.Generic.List<string> value) { }
}

[Register ("my/app/ByRefCtorActivity")]
public class ByRefCtorActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public ByRefCtorActivity (ref int value) { }
}

[Register ("my/app/PointerCtorActivity")]
public unsafe class PointerCtorActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public PointerCtorActivity (int* value) { }
}

[Register ("my/app/FunctionPointerCtorActivity")]
public unsafe class FunctionPointerCtorActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public FunctionPointerCtorActivity (delegate* unmanaged<void> value) { }
}

[Register ("my/app/RectangularArrayCtorActivity")]
public class RectangularArrayCtorActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public RectangularArrayCtorActivity (string[,] value) { }
}

[Register ("my/app/NestedRectangularArrayCtorActivity")]
public class NestedRectangularArrayCtorActivity : NoDefaultBase
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public NestedRectangularArrayCtorActivity (string[][,] value) : base (0) { }
}

[Register ("my/app/PointerArrayCtorActivity")]
public unsafe class PointerArrayCtorActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public PointerArrayCtorActivity (int*[] value) { }
}

[Register ("my/app/FunctionPointerArrayCtorActivity")]
public unsafe class FunctionPointerArrayCtorActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public FunctionPointerArrayCtorActivity (delegate* unmanaged<void>[] value) { }
}

[Register ("my/app/JaggedArrayCtorActivity")]
public class JaggedArrayCtorActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public JaggedArrayCtorActivity (string[][] value) { }
}

[Register ("my/app/ManagedOnlyConstructorImplementor")]
public class ManagedOnlyConstructorImplementor : Android.App.Activity
{
	public ManagedOnlyConstructorImplementor (object handler) { }
}

[Register ("my/app/NoDefaultBase", DoNotGenerateAcw = true)]
public class NoDefaultBase : Java.Lang.Object
{
	[Register (".ctor", "(I)V", "")]
	public NoDefaultBase (int value) { }
}

public sealed class UnsupportedConstructorValueOne
{
}

public sealed class UnsupportedConstructorValueTwo
{
}

[Register ("my/app/UnsupportedExportConstructorOverloadsActivity")]
public class UnsupportedExportConstructorOverloadsActivity : NoDefaultBase
{
	[Java.Interop.Export (".ctor")]
	public UnsupportedExportConstructorOverloadsActivity (
		[Java.Interop.ExportParameter (Java.Interop.ExportParameterKind.InputStream)] UnsupportedConstructorValueOne value) : base (0) { }

	[Java.Interop.Export (".ctor")]
	public UnsupportedExportConstructorOverloadsActivity (
		[Java.Interop.ExportParameter (Java.Interop.ExportParameterKind.InputStream)] UnsupportedConstructorValueTwo value) : base (0) { }
}

[Register ("my/app/RegisterBeforeExportActivity")]
public class RegisterBeforeExportActivity : NoDefaultBase
{
	[Register (".ctor", "(Ljava/lang/Object;)V", "")]
	[Java.Interop.Export (".ctor")]
	public RegisterBeforeExportActivity (
		[Java.Interop.ExportParameter (Java.Interop.ExportParameterKind.InputStream)] UnsupportedConstructorValueOne value) : base (0) { }

	[Register (".ctor", "(Ljava/lang/Object;)V", "")]
	[Java.Interop.Export (".ctor")]
	public RegisterBeforeExportActivity (
		[Java.Interop.ExportParameter (Java.Interop.ExportParameterKind.InputStream)] UnsupportedConstructorValueTwo value) : base (0) { }
}

[Register ("my/app/ExportBeforeRegisterActivity")]
public class ExportBeforeRegisterActivity : NoDefaultBase
{
	[Java.Interop.Export (".ctor")]
	[Register (".ctor", "(Ljava/lang/Object;)V", "")]
	public ExportBeforeRegisterActivity (
		[Java.Interop.ExportParameter (Java.Interop.ExportParameterKind.InputStream)] UnsupportedConstructorValueOne value) : base (0) { }

	[Java.Interop.Export (".ctor")]
	[Register (".ctor", "(Ljava/lang/Object;)V", "")]
	public ExportBeforeRegisterActivity (
		[Java.Interop.ExportParameter (Java.Interop.ExportParameterKind.InputStream)] UnsupportedConstructorValueTwo value) : base (0) { }
}

[Register ("my/app/MissingBaseCtorActivity")]
public class MissingBaseCtorActivity : NoDefaultBase
{
	public MissingBaseCtorActivity (string value) : base (0) { }
}

[Register ("my/app/InvalidSuperArgumentsActivity")]
public class InvalidSuperArgumentsActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "p1")]
	public InvalidSuperArgumentsActivity (string value) { }
}

[Register ("my/app/NonCanonicalSuperArgumentZeroActivity")]
public class NonCanonicalSuperArgumentZeroActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "p00")]
	public NonCanonicalSuperArgumentZeroActivity (string value) { }
}

[Register ("my/app/NonCanonicalSuperArgumentOneActivity")]
public class NonCanonicalSuperArgumentOneActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "p01")]
	public NonCanonicalSuperArgumentOneActivity (string first, string second) { }
}

[Register ("my/app/ValidSuperExpressionActivity")]
public class ValidSuperExpressionActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "p0.hashCode()")]
	public ValidSuperExpressionActivity (string value) { }
}

[Register ("my/app/LexicalSuperArgumentsActivity")]
public class LexicalSuperArgumentsActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "Constants.p1, \"p2\", /* p3 */ p0")]
	public LexicalSuperArgumentsActivity (string value) { }
}

[Register ("my/app/LambdaSuperArgumentsActivity")]
public class LambdaSuperArgumentsActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "p1 -> p1")]
	public LambdaSuperArgumentsActivity () { }
}

[Register ("my/app/ParenthesizedLambdaSuperArgumentsActivity")]
public class ParenthesizedLambdaSuperArgumentsActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "(p1) -> p1")]
	public ParenthesizedLambdaSuperArgumentsActivity () { }
}

[Register ("my/app/TypedLambdaSuperArgumentsActivity")]
public class TypedLambdaSuperArgumentsActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "(String p1, String p2) -> p1 + p2")]
	public TypedLambdaSuperArgumentsActivity () { }
}

[Register ("my/app/LiteralCommaLambdaSuperArgumentsActivity")]
public class LiteralCommaLambdaSuperArgumentsActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "p1 -> \"a,b\"")]
	public LiteralCommaLambdaSuperArgumentsActivity () { }
}

[Register ("my/app/GenericConstructionLambdaSuperArgumentsActivity")]
public class GenericConstructionLambdaSuperArgumentsActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "(p1) -> new SimpleEntry<String, String>(p1, p1)")]
	public GenericConstructionLambdaSuperArgumentsActivity () { }
}

[Register ("my/app/NestedGenericLambdaSuperArgumentsActivity")]
public class NestedGenericLambdaSuperArgumentsActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "(p1) -> new SimpleEntry<String, java.util.List<String>>(p1, java.util.Arrays.asList(\"a,b\", p1))")]
	public NestedGenericLambdaSuperArgumentsActivity () { }
}

[Register ("my/app/InstanceOfGenericLambdaSuperArgumentsActivity")]
public class InstanceOfGenericLambdaSuperArgumentsActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "(p1) -> p1 instanceof java.util.Map<?, ?> ? p1 : p1")]
	public InstanceOfGenericLambdaSuperArgumentsActivity () { }
}

[Register ("my/app/ComparisonLambdaSuperArgumentsActivity")]
public class ComparisonLambdaSuperArgumentsActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "(int p1) -> p1 < 2 ? p1 : 2")]
	public ComparisonLambdaSuperArgumentsActivity () { }
}

[Register ("my/app/ShiftLambdaSuperArgumentsActivity")]
public class ShiftLambdaSuperArgumentsActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "(int p1) -> p1 >> 1")]
	public ShiftLambdaSuperArgumentsActivity () { }
}

[Register ("my/app/MethodReferenceSuperArgumentsActivity")]
public class MethodReferenceSuperArgumentsActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "Helper::p1")]
	public MethodReferenceSuperArgumentsActivity () { }
}

[Register ("my/app/GenericMethodReferenceSuperArgumentsActivity")]
public class GenericMethodReferenceSuperArgumentsActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "Helper::<String>p1")]
	public GenericMethodReferenceSuperArgumentsActivity () { }
}

[Register ("my/app/ExportMappedCtorActivity")]
public class ExportMappedCtorActivity : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public ExportMappedCtorActivity (
		[Java.Interop.ExportParameter (Java.Interop.ExportParameterKind.InputStream)] System.IO.Stream value) { }

	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public ExportMappedCtorActivity (Java.Lang.ICharSequence value) { }

	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public ExportMappedCtorActivity (System.Collections.IList value) { }
}

[Register ("my/app/ExportMissingBaseCtorActivity")]
public class ExportMissingBaseCtorActivity : NoDefaultBase
{
	[Java.Interop.Export (".ctor")]
	public ExportMissingBaseCtorActivity (string value) : base (0) { }
}

[Register ("my/app/ExportEmptySuperMissingBaseActivity")]
public class ExportEmptySuperMissingBaseActivity : NoDefaultBase
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	public ExportEmptySuperMissingBaseActivity (string value) : base (0) { }
}

[Register ("my/app/NonPublicExportCollisionActivity")]
public class NonPublicExportCollisionActivity : IntSignatureBase
{
	[Java.Interop.Export (".ctor")]
	protected NonPublicExportCollisionActivity (int value) : base (value) { }

	[Java.Interop.Export (".ctor")]
	protected NonPublicExportCollisionActivity (uint value) : base ((int)value) { }
}

[Register ("my/app/NonPublicExportUnsupportedActivity")]
public class NonPublicExportUnsupportedActivity<T> : Android.App.Activity
{
	[Java.Interop.Export (".ctor", SuperArgumentsString = "")]
	protected NonPublicExportUnsupportedActivity (T value) { }
}

[Register ("my/app/NonPublicExportMissingBaseActivity")]
public class NonPublicExportMissingBaseActivity : NoDefaultBase
{
	[Java.Interop.Export (".ctor")]
	protected NonPublicExportMissingBaseActivity (string value) : base (0) { }
}
