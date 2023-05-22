using System;
using System.Text;
using System.Globalization;

namespace Xamarin.Android.Tasks.LLVMIR
{
	// Not all attributes are currently used throughout the code, but we define them call for potential future use.
	// Documentation can be found here: https://llvm.org/docs/LangRef.html#function-attributes
	abstract class LLVMFunctionAttribute : IComparable, IComparable<LLVMFunctionAttribute>, IEquatable<LLVMFunctionAttribute>
	{
		public string Name { get; }
		public bool Quoted { get; }
		public bool SupportsParams { get; }
		public bool ParamsAreOptional { get; }
		public bool HasValueAsignment { get; }

		protected LLVMFunctionAttribute (string name, bool quoted, bool supportsParams, bool optionalParams, bool hasValueAssignment)
		{
			Name = EnsureNonEmptyParameter (nameof (name), name);

			if (supportsParams && hasValueAssignment) {
				throw new InvalidOperationException ($"Function attribute '{name}' cannot have both parameters and an assigned value");
			}

			ParamsAreOptional = optionalParams;
			SupportsParams = supportsParams;
			HasValueAsignment = hasValueAssignment;
			Quoted = quoted;
		}

		public string Render ()
		{
			var sb = new StringBuilder ();

			if (Quoted) {
				sb.Append ('"');
			}

			sb.Append (Name);

			if (Quoted) {
				sb.Append ('"');
			}

			if (SupportsParams) {
				if (!ParamsAreOptional || HasOptionalParams ()) {
					sb.Append ('(');
					RenderParams (sb);
					sb.Append (')');
				}
			} else if (HasValueAsignment) {
				sb.Append ('=');
				if (Quoted) {
					sb.Append ('"');
				}

				var value = new StringBuilder ();
				RenderAssignedValue (value);

				// LLVM IR escapes characters as \xx where xx is hexadecimal ASCII code
				value.Replace ("\"", "\\22");
				sb.Append (value);

				if (Quoted) {
					sb.Append ('"');
				}

			}

			return sb.ToString ();
		}

		protected virtual void RenderParams (StringBuilder sb)
		{}

		protected virtual void RenderAssignedValue (StringBuilder sb)
		{}

		protected virtual bool HasOptionalParams ()
		{
			return false;
		}

		protected string EnsureNonEmptyParameter (string name, string value)
		{
			if (String.IsNullOrEmpty (name)) {
				throw new ArgumentException ("must not be null or empty", name);
			}

			return value;
		}

		public int CompareTo (object obj)
		{
			var attr = obj as LLVMFunctionAttribute;
			if (obj == null) {
				return 1;
			}

			return CompareTo (attr);
		}

		public int CompareTo (LLVMFunctionAttribute other)
		{
			return Name.CompareTo (other?.Name);
		}

		public override int GetHashCode()
		{
			int hc = 0;
			if (Name != null) {
				hc ^= Name.GetHashCode ();
			}

			return
				hc ^
				Quoted.GetHashCode () ^
				SupportsParams.GetHashCode () ^
				ParamsAreOptional.GetHashCode () ^
				HasValueAsignment.GetHashCode ();
		}

		public override bool Equals (object obj)
		{
			var attr = obj as LLVMFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return Equals (attr);
		}

		public virtual bool Equals (LLVMFunctionAttribute other)
		{
			if (other == null) {
				return false;
			}

			if (String.Compare (Name, other.Name, StringComparison.Ordinal) != 0) {
				return false;
			}

			return
				Quoted == other.Quoted &&
				SupportsParams == other.SupportsParams &&
				ParamsAreOptional == other.ParamsAreOptional &&
				HasValueAsignment == other.HasValueAsignment;
		}

		public static bool operator > (LLVMFunctionAttribute a, LLVMFunctionAttribute b)
		{
			return a.CompareTo (b) > 0;
		}

		public static bool operator < (LLVMFunctionAttribute a, LLVMFunctionAttribute b)
		{
			return a.CompareTo (b) < 0;
		}

		public static bool operator >= (LLVMFunctionAttribute a, LLVMFunctionAttribute b)
		{
			return a.CompareTo (b) >= 0;
		}

		public static bool operator <= (LLVMFunctionAttribute a, LLVMFunctionAttribute b)
		{
			return a.CompareTo (b) <= 0;
		}
	}

	abstract class LLVMFlagFunctionAttribute : LLVMFunctionAttribute
	{
		protected LLVMFlagFunctionAttribute (string name, bool quoted = false)
			: base (name, quoted, supportsParams: false, optionalParams: false, hasValueAssignment: false)
		{}
	}

	class AlignstackFunctionAttribute : LLVMFunctionAttribute
	{
		uint alignment;

		public AlignstackFunctionAttribute (uint powerOfTwoAlignment)
			: base ("alignstack", quoted: false, supportsParams: true, optionalParams: false, hasValueAssignment: false)
		{
			if ((powerOfTwoAlignment % 2) != 0) {
				throw new ArgumentException ("must be power of two", nameof (powerOfTwoAlignment));
			}

			alignment = powerOfTwoAlignment;
		}

		protected override void RenderParams (StringBuilder sb)
		{
			sb.Append (alignment.ToString (CultureInfo.InvariantCulture));
		}

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as AlignstackFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return alignment == attr.alignment;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ alignment.GetHashCode ();
		}
	}

	class AllocFamilyFunctionAttribute : LLVMFunctionAttribute
	{
		string family;

		public AllocFamilyFunctionAttribute (string familyName)
			: base ("alloc-family", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			family = EnsureNonEmptyParameter (nameof (familyName), familyName);
		}

		protected override void RenderAssignedValue (StringBuilder sb)
		{
			sb.Append (family);
		}

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as AllocFamilyFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return String.Compare (family, attr.family, StringComparison.Ordinal) == 0;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ (family?.GetHashCode () ?? 0);
		}
	}

	class AllockindFunctionAttribute : LLVMFunctionAttribute
	{
		string kind;

		public AllockindFunctionAttribute (string allocKind)
			: base ("allockind", quoted: false, supportsParams: true, optionalParams: false, hasValueAssignment: false)
		{
			kind = EnsureNonEmptyParameter (nameof (allocKind), allocKind);
		}

		protected override void RenderParams (StringBuilder sb)
		{
			sb.Append ('"');
			sb.Append (kind);
			sb.Append ('"');
		}

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as AllockindFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return String.Compare (kind, attr.kind, StringComparison.Ordinal) == 0;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ (kind?.GetHashCode () ?? 0);
		}
	}

	class AllocsizeFunctionAttribute : LLVMFunctionAttribute
	{
		uint elementSize;
		uint? numberOfElements;

		public AllocsizeFunctionAttribute (uint elementSize, uint? numberOfElements = null)
			: base ("allocsize", quoted: false, supportsParams: true, optionalParams: false, hasValueAssignment: false)
		{
			this.elementSize = elementSize;
			this.numberOfElements = numberOfElements;
		}

		protected override void RenderParams (StringBuilder sb)
		{
			sb.Append (elementSize.ToString (CultureInfo.InvariantCulture));
			if (!numberOfElements.HasValue) {
				return;
			}

			sb.Append (", ");
			sb.Append (numberOfElements.Value.ToString (CultureInfo.InvariantCulture));
		}

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as AllocsizeFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return elementSize == attr.elementSize && numberOfElements == attr.numberOfElements;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ elementSize.GetHashCode () ^ (numberOfElements?.GetHashCode () ?? 0);
		}
	}

	class AlwaysinlineFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public AlwaysinlineFunctionAttribute ()
			: base ("alwaysinline")
		{}
	}

	class ArgmemonlyFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public ArgmemonlyFunctionAttribute ()
			: base ("argmemonly")
		{}
	}

	class BuiltinFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public BuiltinFunctionAttribute ()
			: base ("builtin")
		{}
	}

	class ColdFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public ColdFunctionAttribute ()
			: base ("cold")
		{}
	}

	class ConvergentFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public ConvergentFunctionAttribute ()
			: base ("convergent")
		{}
	}

	class DisableSanitizerInstrumentationFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public DisableSanitizerInstrumentationFunctionAttribute ()
			: base ("disable_sanitizer_instrumentation")
		{}
	}

	class DontcallErrorFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public DontcallErrorFunctionAttribute ()
			: base ("dontcall-error", quoted: true)
		{}
	}

	class DontcallWarnFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public DontcallWarnFunctionAttribute ()
			: base ("dontcall-warn", quoted: true)
		{}
	}

	class FramePointerFunctionAttribute : LLVMFunctionAttribute
	{
		string fpMode;

		public FramePointerFunctionAttribute (string fpMode = "none")
			: base ("frame-pointer", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			switch (fpMode) {
				case "none":
				case "non-leaf":
				case "all":
					this.fpMode = fpMode;
					break;

				default:
					throw new ArgumentException ($"unsupported mode value '{fpMode}'", nameof (fpMode));
			}
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (fpMode);

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as FramePointerFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return String.Compare (fpMode, attr.fpMode, StringComparison.Ordinal) == 0;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ (fpMode?.GetHashCode () ?? 0);
		}
	}

	class HotFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public HotFunctionAttribute ()
			: base ("hot")
		{}
	}

	class InaccessiblememonlyFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public InaccessiblememonlyFunctionAttribute ()
			: base ("inaccessiblememonly")
		{}
	}

	class InaccessiblememOrArgmemonlyFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public InaccessiblememOrArgmemonlyFunctionAttribute ()
			: base ("inaccessiblemem_or_argmemonly")
		{}
	}

	class InlinehintFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public InlinehintFunctionAttribute ()
			: base ("inlinehint")
		{}
	}

	class JumptableFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public JumptableFunctionAttribute ()
			: base ("jumptable")
		{}
	}

	class MinsizeFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public MinsizeFunctionAttribute ()
			: base ("minsize")
		{}
	}

	class NakedFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NakedFunctionAttribute ()
			: base ("naked")
		{}
	}

	class NoInlineLineTablesFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NoInlineLineTablesFunctionAttribute ()
			: base ("no-inline-line-tables", quoted: true)
		{}
	}

	class NoJumpTablesFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NoJumpTablesFunctionAttribute ()
			: base ("no-jump-tables")
		{}
	}

	class NobuiltinFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NobuiltinFunctionAttribute ()
			: base ("nobuiltin")
		{}
	}

	class NoduplicateFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NoduplicateFunctionAttribute ()
			: base ("noduplicate")
		{}
	}

	class NofreeFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NofreeFunctionAttribute ()
			: base ("nofree")
		{}
	}

	class NoimplicitfloatFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NoimplicitfloatFunctionAttribute ()
			: base ("noimplicitfloat")
		{}
	}

	class NoinlineFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NoinlineFunctionAttribute ()
			: base ("noinline")
		{}
	}

	class NomergeFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NomergeFunctionAttribute ()
			: base ("nomerge")
		{}
	}

	class NonlazybindFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NonlazybindFunctionAttribute ()
			: base ("nonlazybind")
		{}
	}

	class NoprofileFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NoprofileFunctionAttribute ()
			: base ("noprofile")
		{}
	}

	class NoredzoneFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NoredzoneFunctionAttribute ()
			: base ("noredzone")
		{}
	}

	class IndirectTlsSegRefsFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public IndirectTlsSegRefsFunctionAttribute ()
			: base ("indirect-tls-seg-refs")
		{}
	}

	class NoreturnFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NoreturnFunctionAttribute ()
			: base ("noreturn")
		{}
	}

	class NorecurseFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NorecurseFunctionAttribute ()
			: base ("norecurse")
		{}
	}

	class WillreturnFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public WillreturnFunctionAttribute ()
			: base ("willreturn")
		{}
	}

	class NosyncFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NosyncFunctionAttribute ()
			: base ("nosync")
		{}
	}

	class NounwindFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NounwindFunctionAttribute ()
			: base ("nounwind")
		{}
	}

	class NosanitizeBoundsFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NosanitizeBoundsFunctionAttribute ()
			: base ("nosanitize_bounds")
		{}
	}

	class NosanitizeCoverageFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NosanitizeCoverageFunctionAttribute ()
			: base ("nosanitize_coverage")
		{}
	}

	class NullPointerIsValidFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NullPointerIsValidFunctionAttribute ()
			: base ("null_pointer_is_valid")
		{}
	}

	class OptforfuzzingFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public OptforfuzzingFunctionAttribute ()
			: base ("optforfuzzing")
		{}
	}

	class OptnoneFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public OptnoneFunctionAttribute ()
			: base ("optnone")
		{}
	}

	class OptsizeFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public OptsizeFunctionAttribute ()
			: base ("optsize")
		{}
	}

	class PatchableFunctionFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public PatchableFunctionFunctionAttribute ()
			: base ("patchable-function", quoted: true)
		{}
	}

	class ProbeStackFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public ProbeStackFunctionAttribute ()
			: base ("probe-stack")
		{}
	}

	class ReadnoneFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public ReadnoneFunctionAttribute ()
			: base ("readnone")
		{}
	}

	class ReadonlyFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public ReadonlyFunctionAttribute ()
			: base ("readonly")
		{}
	}

	class StackProbeSizeFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public StackProbeSizeFunctionAttribute ()
			: base ("stack-probe-size", quoted: true)
		{}
	}

	class NoStackArgProbeFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NoStackArgProbeFunctionAttribute ()
			: base ("no-stack-arg-probe")
		{}
	}

	class WriteonlyFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public WriteonlyFunctionAttribute ()
			: base ("writeonly")
		{}
	}

	class ReturnsTwiceFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public ReturnsTwiceFunctionAttribute ()
			: base ("returns_twice")
		{}
	}

	class SafestackFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public SafestackFunctionAttribute ()
			: base ("safestack")
		{}
	}

	class SanitizeAddressFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public SanitizeAddressFunctionAttribute ()
			: base ("sanitize_address")
		{}
	}

	class SanitizeMemoryFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public SanitizeMemoryFunctionAttribute ()
			: base ("sanitize_memory")
		{}
	}

	class SanitizeThreadFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public SanitizeThreadFunctionAttribute ()
			: base ("sanitize_thread")
		{}
	}

	class SanitizeHwaddressFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public SanitizeHwaddressFunctionAttribute ()
			: base ("sanitize_hwaddress")
		{}
	}

	class SanitizeMemtagFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public SanitizeMemtagFunctionAttribute ()
			: base ("sanitize_memtag")
		{}
	}

	class SpeculativeLoadHardeningFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public SpeculativeLoadHardeningFunctionAttribute ()
			: base ("speculative_load_hardening")
		{}
	}

	class SpeculatableFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public SpeculatableFunctionAttribute ()
			: base ("speculatable")
		{}
	}

	class SspFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public SspFunctionAttribute ()
			: base ("ssp")
		{}
	}

	class SspstrongFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public SspstrongFunctionAttribute ()
			: base ("sspstrong")
		{}
	}

	class SspreqFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public SspreqFunctionAttribute ()
			: base ("sspreq")
		{}
	}

	class StrictfpFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public StrictfpFunctionAttribute ()
			: base ("strictfp")
		{}
	}

	class DenormalFpMathFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public DenormalFpMathFunctionAttribute ()
			: base ("denormal-fp-math", quoted: true)
		{}
	}

	class DenormalFpMathF32FunctionAttribute : LLVMFlagFunctionAttribute
	{
		public DenormalFpMathF32FunctionAttribute ()
			: base ("denormal-fp-math-f32", quoted: true)
		{}
	}

	class ThunkFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public ThunkFunctionAttribute ()
			: base ("thunk", quoted: true)
		{}
	}

	class TlsLoadHoistFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public TlsLoadHoistFunctionAttribute ()
			: base ("tls-load-hoist")
		{}
	}

	class UwtableFunctionAttribute : LLVMFunctionAttribute
	{
		bool? isSync;

		public UwtableFunctionAttribute (bool? sync = null)
			: base ("uwtable", quoted: false, supportsParams: true, optionalParams: true, hasValueAssignment: false)
		{
			isSync = sync;
		}

		protected override bool HasOptionalParams () => isSync.HasValue;

		protected override void RenderParams (StringBuilder sb)
		{
			if (!isSync.HasValue) {
				throw new InvalidOperationException ("Unable to render parameters, none given");
			}

			sb.Append (isSync.Value ? "sync" : "async");
		}

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as UwtableFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return isSync == attr.isSync;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ (isSync?.GetHashCode () ?? 0);
		}
	}

	class NocfCheckFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public NocfCheckFunctionAttribute ()
			: base ("nocf_check")
		{}
	}

	class ShadowcallstackFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public ShadowcallstackFunctionAttribute ()
			: base ("shadowcallstack")
		{}
	}

	class MustprogressFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public MustprogressFunctionAttribute ()
			: base ("mustprogress")
		{}
	}

	class WarnStackSizeFunctionAttribute : LLVMFunctionAttribute
	{
		uint threshold;

		public WarnStackSizeFunctionAttribute (uint threshold)
			: base ("warn-stack-size", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.threshold = threshold;
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (threshold);

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as WarnStackSizeFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return threshold == attr.threshold;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ threshold.GetHashCode ();
		}
	}

	class VscaleRangeFunctionAttribute : LLVMFunctionAttribute
	{
		uint min;
		uint? max;

		public VscaleRangeFunctionAttribute (uint min, uint? max = null)
			: base ("vscale_range", quoted: false, supportsParams: true, optionalParams: false, hasValueAssignment: false)
		{
			this.min = min;
			this.max = max;
		}

		protected override void RenderParams (StringBuilder sb)
		{
			sb.Append (min.ToString (CultureInfo.InvariantCulture));
			if (!max.HasValue) {
				return;
			}

			sb.Append (", ");
			sb.Append (max.Value.ToString (CultureInfo.InvariantCulture));
		}

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as VscaleRangeFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return min == attr.min && max == attr.max;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ min.GetHashCode () ^ (max?.GetHashCode () ?? 0);
		}
	}

	class MinLegalVectorWidthFunctionAttribute : LLVMFunctionAttribute
	{
		uint size;

		public MinLegalVectorWidthFunctionAttribute (uint size)
			: base ("min-legal-vector-width", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.size = size;
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (size.ToString (CultureInfo.InvariantCulture));

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as MinLegalVectorWidthFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return size == attr.size;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ size.GetHashCode ();
		}
	}

	class StackProtectorBufferSizeFunctionAttribute : LLVMFunctionAttribute
	{
		uint size;

		public StackProtectorBufferSizeFunctionAttribute (uint size)
			: base ("stack-protector-buffer-size", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.size = size;
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (size.ToString (CultureInfo.InvariantCulture));

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as StackProtectorBufferSizeFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return size == attr.size;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ size.GetHashCode ();
		}
	}

	class TargetCpuFunctionAttribute : LLVMFunctionAttribute
	{
		string cpu;

		public TargetCpuFunctionAttribute (string cpu)
			: base ("target-cpu", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.cpu = EnsureNonEmptyParameter (nameof (cpu), cpu);
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (cpu);

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as TargetCpuFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return String.Compare (cpu, attr.cpu, StringComparison.Ordinal) == 0;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ (cpu?.GetHashCode () ?? 0);
		}
	}

	class TuneCpuFunctionAttribute : LLVMFunctionAttribute
	{
		string cpu;

		public TuneCpuFunctionAttribute (string cpu)
			: base ("tune-cpu", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.cpu = EnsureNonEmptyParameter (nameof (cpu), cpu);
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (cpu);

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as TuneCpuFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return String.Compare (cpu, attr.cpu, StringComparison.Ordinal) == 0;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ (cpu?.GetHashCode () ?? 0);
		}
	}

	class TargetFeaturesFunctionAttribute : LLVMFunctionAttribute
	{
		string features;

		public TargetFeaturesFunctionAttribute (string features)
			: base ("target-features", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.features = EnsureNonEmptyParameter (nameof (features), features);
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (features);

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as TargetFeaturesFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return String.Compare (features, attr.features, StringComparison.Ordinal) == 0;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ (features?.GetHashCode () ?? 0);
		}
	}

	class NoTrappingMathFunctionAttribute : LLVMFunctionAttribute
	{
		bool yesno;

		public NoTrappingMathFunctionAttribute (bool yesno)
			: base ("no-trapping-math", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.yesno = yesno;
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (yesno.ToString ().ToLowerInvariant ());

		public override bool Equals (LLVMFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as NoTrappingMathFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return yesno == attr.yesno;
		}

		public override int GetHashCode ()
		{
			return base.GetHashCode () ^ yesno.GetHashCode ();
		}
	}

	class StackrealignFunctionAttribute : LLVMFlagFunctionAttribute
	{
		public StackrealignFunctionAttribute ()
			: base ("stackrealign", quoted: true)
		{}
	}
}
