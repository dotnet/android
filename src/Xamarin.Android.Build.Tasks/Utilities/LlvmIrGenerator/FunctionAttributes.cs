using System;
using System.Text;
using System.Globalization;

namespace Xamarin.Android.Tasks.LLVMIR
{
	// Not all attributes are currently used throughout the code, but we define them call for potential future use.
	// Documentation can be found here: https://llvm.org/docs/LangRef.html#function-attributes
	abstract class LlvmIrFunctionAttribute : IComparable, IComparable<LlvmIrFunctionAttribute>, IEquatable<LlvmIrFunctionAttribute>
	{
		public string Name { get; }
		public bool Quoted { get; }
		public bool SupportsParams { get; }
		public bool ParamsAreOptional { get; }
		public bool HasValueAsignment { get; }

		protected LlvmIrFunctionAttribute (string name, bool quoted, bool supportsParams, bool optionalParams, bool hasValueAssignment)
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
			var attr = obj as LlvmIrFunctionAttribute;
			if (obj == null) {
				return 1;
			}

			return CompareTo (attr);
		}

		public int CompareTo (LlvmIrFunctionAttribute other)
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
			var attr = obj as LlvmIrFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return Equals (attr);
		}

		public virtual bool Equals (LlvmIrFunctionAttribute other)
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

		public static bool operator > (LlvmIrFunctionAttribute a, LlvmIrFunctionAttribute b)
		{
			return a.CompareTo (b) > 0;
		}

		public static bool operator < (LlvmIrFunctionAttribute a, LlvmIrFunctionAttribute b)
		{
			return a.CompareTo (b) < 0;
		}

		public static bool operator >= (LlvmIrFunctionAttribute a, LlvmIrFunctionAttribute b)
		{
			return a.CompareTo (b) >= 0;
		}

		public static bool operator <= (LlvmIrFunctionAttribute a, LlvmIrFunctionAttribute b)
		{
			return a.CompareTo (b) <= 0;
		}
	}

	abstract class LlvmIrFlagFunctionAttribute : LlvmIrFunctionAttribute
	{
		protected LlvmIrFlagFunctionAttribute (string name, bool quoted = false)
			: base (name, quoted, supportsParams: false, optionalParams: false, hasValueAssignment: false)
		{}
	}

	class AlignstackFunctionAttribute : LlvmIrFunctionAttribute
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

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class AllocFamilyFunctionAttribute : LlvmIrFunctionAttribute
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

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class AllockindFunctionAttribute : LlvmIrFunctionAttribute
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

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class AllocsizeFunctionAttribute : LlvmIrFunctionAttribute
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

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class AlwaysinlineFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public AlwaysinlineFunctionAttribute ()
			: base ("alwaysinline")
		{}
	}

	class ArgmemonlyFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public ArgmemonlyFunctionAttribute ()
			: base ("argmemonly")
		{}
	}

	class BuiltinFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public BuiltinFunctionAttribute ()
			: base ("builtin")
		{}
	}

	class ColdFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public ColdFunctionAttribute ()
			: base ("cold")
		{}
	}

	class ConvergentFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public ConvergentFunctionAttribute ()
			: base ("convergent")
		{}
	}

	class DisableSanitizerInstrumentationFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public DisableSanitizerInstrumentationFunctionAttribute ()
			: base ("disable_sanitizer_instrumentation")
		{}
	}

	class DontcallErrorFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public DontcallErrorFunctionAttribute ()
			: base ("dontcall-error", quoted: true)
		{}
	}

	class DontcallWarnFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public DontcallWarnFunctionAttribute ()
			: base ("dontcall-warn", quoted: true)
		{}
	}

	class FramePointerFunctionAttribute : LlvmIrFunctionAttribute
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

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class HotFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public HotFunctionAttribute ()
			: base ("hot")
		{}
	}

	class InaccessiblememonlyFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public InaccessiblememonlyFunctionAttribute ()
			: base ("inaccessiblememonly")
		{}
	}

	class InaccessiblememOrArgmemonlyFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public InaccessiblememOrArgmemonlyFunctionAttribute ()
			: base ("inaccessiblemem_or_argmemonly")
		{}
	}

	class InlinehintFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public InlinehintFunctionAttribute ()
			: base ("inlinehint")
		{}
	}

	class JumptableFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public JumptableFunctionAttribute ()
			: base ("jumptable")
		{}
	}

	enum MemoryAttributeAccessKind
	{
		None,
		Read,
		Write,
		ReadWrite,
	}

	class MemoryFunctionAttribute : LlvmIrFunctionAttribute
	{
		public MemoryAttributeAccessKind? Default         { get; set; }
		public MemoryAttributeAccessKind? Argmem          { get; set; }
		public MemoryAttributeAccessKind? InaccessibleMem { get; set; }

		public MemoryFunctionAttribute ()
			: base ("memory", quoted: false, supportsParams: true, optionalParams: true, hasValueAssignment: false)
		{}

		protected override bool HasOptionalParams ()
		{
			// All of them are optional, but at least one of them must be specified
			bool ret = Default.HasValue || Argmem.HasValue || InaccessibleMem.HasValue;
			if (!ret) {
				throw new InvalidOperationException ("Internal error: at least one access kind must be specified");
			}

			return ret;
		}

		protected override void RenderParams (StringBuilder sb)
		{
			bool haveSomething = false;

			if (Default.HasValue) {
				AppendParam (GetAccessKindString (Default));
			}

			if (Argmem.HasValue) {
				AppendParam ($"argmem: {GetAccessKindString (Argmem)}");
			}

			if (InaccessibleMem.HasValue) {
				AppendParam ($"inaccessiblemem: {GetAccessKindString (InaccessibleMem)}");
			}

			void AppendParam (string text)
			{
				if (haveSomething) {
					sb.Append (", ");
				}
				sb.Append (text);
				haveSomething = true;
			}
		}

		string GetAccessKindString (MemoryAttributeAccessKind? kind)
		{
			return kind.Value switch {
				MemoryAttributeAccessKind.None      => "none",
				MemoryAttributeAccessKind.Read      => "read",
				MemoryAttributeAccessKind.Write     => "write",
				MemoryAttributeAccessKind.ReadWrite => "readwrite",
				_ => throw new InvalidOperationException ($"Internal error: unsupported access kind {kind}")
			};
		}

		public override bool Equals (LlvmIrFunctionAttribute other)
		{
			if (!base.Equals (other)) {
				return false;
			}

			var attr = other as MemoryFunctionAttribute;
			if (attr == null) {
				return false;
			}

			return Default == attr.Default && Argmem == attr.Argmem && InaccessibleMem == attr.InaccessibleMem;
		}
	}

	class MinsizeFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public MinsizeFunctionAttribute ()
			: base ("minsize")
		{}
	}

	class NakedFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NakedFunctionAttribute ()
			: base ("naked")
		{}
	}

	class NoInlineLineTablesFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NoInlineLineTablesFunctionAttribute ()
			: base ("no-inline-line-tables", quoted: true)
		{}
	}

	class NoJumpTablesFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NoJumpTablesFunctionAttribute ()
			: base ("no-jump-tables")
		{}
	}

	class NobuiltinFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NobuiltinFunctionAttribute ()
			: base ("nobuiltin")
		{}
	}

	class NocallbackFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NocallbackFunctionAttribute ()
			: base ("nocallback")
		{}
	}

	class NoduplicateFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NoduplicateFunctionAttribute ()
			: base ("noduplicate")
		{}
	}

	class NofreeFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NofreeFunctionAttribute ()
			: base ("nofree")
		{}
	}

	class NoimplicitfloatFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NoimplicitfloatFunctionAttribute ()
			: base ("noimplicitfloat")
		{}
	}

	class NoinlineFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NoinlineFunctionAttribute ()
			: base ("noinline")
		{}
	}

	class NomergeFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NomergeFunctionAttribute ()
			: base ("nomerge")
		{}
	}

	class NonlazybindFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NonlazybindFunctionAttribute ()
			: base ("nonlazybind")
		{}
	}

	class NoprofileFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NoprofileFunctionAttribute ()
			: base ("noprofile")
		{}
	}

	class NoredzoneFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NoredzoneFunctionAttribute ()
			: base ("noredzone")
		{}
	}

	class IndirectTlsSegRefsFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public IndirectTlsSegRefsFunctionAttribute ()
			: base ("indirect-tls-seg-refs")
		{}
	}

	class NoreturnFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NoreturnFunctionAttribute ()
			: base ("noreturn")
		{}
	}

	class NorecurseFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NorecurseFunctionAttribute ()
			: base ("norecurse")
		{}
	}

	class WillreturnFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public WillreturnFunctionAttribute ()
			: base ("willreturn")
		{}
	}

	class NosyncFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NosyncFunctionAttribute ()
			: base ("nosync")
		{}
	}

	class NounwindFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NounwindFunctionAttribute ()
			: base ("nounwind")
		{}
	}

	class NosanitizeBoundsFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NosanitizeBoundsFunctionAttribute ()
			: base ("nosanitize_bounds")
		{}
	}

	class NosanitizeCoverageFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NosanitizeCoverageFunctionAttribute ()
			: base ("nosanitize_coverage")
		{}
	}

	class NullPointerIsValidFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NullPointerIsValidFunctionAttribute ()
			: base ("null_pointer_is_valid")
		{}
	}

	class OptforfuzzingFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public OptforfuzzingFunctionAttribute ()
			: base ("optforfuzzing")
		{}
	}

	class OptnoneFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public OptnoneFunctionAttribute ()
			: base ("optnone")
		{}
	}

	class OptsizeFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public OptsizeFunctionAttribute ()
			: base ("optsize")
		{}
	}

	class PatchableFunctionFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public PatchableFunctionFunctionAttribute ()
			: base ("patchable-function", quoted: true)
		{}
	}

	class ProbeStackFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public ProbeStackFunctionAttribute ()
			: base ("probe-stack")
		{}
	}

	class ReadnoneFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public ReadnoneFunctionAttribute ()
			: base ("readnone")
		{}
	}

	class ReadonlyFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public ReadonlyFunctionAttribute ()
			: base ("readonly")
		{}
	}

	class StackProbeSizeFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public StackProbeSizeFunctionAttribute ()
			: base ("stack-probe-size", quoted: true)
		{}
	}

	class NoStackArgProbeFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NoStackArgProbeFunctionAttribute ()
			: base ("no-stack-arg-probe")
		{}
	}

	class WriteonlyFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public WriteonlyFunctionAttribute ()
			: base ("writeonly")
		{}
	}

	class ReturnsTwiceFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public ReturnsTwiceFunctionAttribute ()
			: base ("returns_twice")
		{}
	}

	class SafestackFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public SafestackFunctionAttribute ()
			: base ("safestack")
		{}
	}

	class SanitizeAddressFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public SanitizeAddressFunctionAttribute ()
			: base ("sanitize_address")
		{}
	}

	class SanitizeMemoryFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public SanitizeMemoryFunctionAttribute ()
			: base ("sanitize_memory")
		{}
	}

	class SanitizeThreadFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public SanitizeThreadFunctionAttribute ()
			: base ("sanitize_thread")
		{}
	}

	class SanitizeHwaddressFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public SanitizeHwaddressFunctionAttribute ()
			: base ("sanitize_hwaddress")
		{}
	}

	class SanitizeMemtagFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public SanitizeMemtagFunctionAttribute ()
			: base ("sanitize_memtag")
		{}
	}

	class SpeculativeLoadHardeningFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public SpeculativeLoadHardeningFunctionAttribute ()
			: base ("speculative_load_hardening")
		{}
	}

	class SpeculatableFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public SpeculatableFunctionAttribute ()
			: base ("speculatable")
		{}
	}

	class SspFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public SspFunctionAttribute ()
			: base ("ssp")
		{}
	}

	class SspstrongFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public SspstrongFunctionAttribute ()
			: base ("sspstrong")
		{}
	}

	class SspreqFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public SspreqFunctionAttribute ()
			: base ("sspreq")
		{}
	}

	class StrictfpFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public StrictfpFunctionAttribute ()
			: base ("strictfp")
		{}
	}

	class DenormalFpMathFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public DenormalFpMathFunctionAttribute ()
			: base ("denormal-fp-math", quoted: true)
		{}
	}

	class DenormalFpMathF32FunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public DenormalFpMathF32FunctionAttribute ()
			: base ("denormal-fp-math-f32", quoted: true)
		{}
	}

	class ThunkFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public ThunkFunctionAttribute ()
			: base ("thunk", quoted: true)
		{}
	}

	class TlsLoadHoistFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public TlsLoadHoistFunctionAttribute ()
			: base ("tls-load-hoist")
		{}
	}

	class UwtableFunctionAttribute : LlvmIrFunctionAttribute
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

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class NocfCheckFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public NocfCheckFunctionAttribute ()
			: base ("nocf_check")
		{}
	}

	class ShadowcallstackFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public ShadowcallstackFunctionAttribute ()
			: base ("shadowcallstack")
		{}
	}

	class MustprogressFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public MustprogressFunctionAttribute ()
			: base ("mustprogress")
		{}
	}

	class WarnStackSizeFunctionAttribute : LlvmIrFunctionAttribute
	{
		uint threshold;

		public WarnStackSizeFunctionAttribute (uint threshold)
			: base ("warn-stack-size", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.threshold = threshold;
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (threshold);

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class VscaleRangeFunctionAttribute : LlvmIrFunctionAttribute
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

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class MinLegalVectorWidthFunctionAttribute : LlvmIrFunctionAttribute
	{
		uint size;

		public MinLegalVectorWidthFunctionAttribute (uint size)
			: base ("min-legal-vector-width", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.size = size;
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (size.ToString (CultureInfo.InvariantCulture));

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class StackProtectorBufferSizeFunctionAttribute : LlvmIrFunctionAttribute
	{
		uint size;

		public StackProtectorBufferSizeFunctionAttribute (uint size)
			: base ("stack-protector-buffer-size", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.size = size;
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (size.ToString (CultureInfo.InvariantCulture));

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class TargetCpuFunctionAttribute : LlvmIrFunctionAttribute
	{
		string cpu;

		public TargetCpuFunctionAttribute (string cpu)
			: base ("target-cpu", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.cpu = EnsureNonEmptyParameter (nameof (cpu), cpu);
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (cpu);

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class TuneCpuFunctionAttribute : LlvmIrFunctionAttribute
	{
		string cpu;

		public TuneCpuFunctionAttribute (string cpu)
			: base ("tune-cpu", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.cpu = EnsureNonEmptyParameter (nameof (cpu), cpu);
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (cpu);

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class TargetFeaturesFunctionAttribute : LlvmIrFunctionAttribute
	{
		string features;

		public TargetFeaturesFunctionAttribute (string features)
			: base ("target-features", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.features = EnsureNonEmptyParameter (nameof (features), features);
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (features);

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class NoTrappingMathFunctionAttribute : LlvmIrFunctionAttribute
	{
		bool yesno;

		public NoTrappingMathFunctionAttribute (bool yesno)
			: base ("no-trapping-math", quoted: true, supportsParams: false, optionalParams: false, hasValueAssignment: true)
		{
			this.yesno = yesno;
		}

		protected override void RenderAssignedValue (StringBuilder sb) => sb.Append (yesno.ToString ().ToLowerInvariant ());

		public override bool Equals (LlvmIrFunctionAttribute other)
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

	class StackrealignFunctionAttribute : LlvmIrFlagFunctionAttribute
	{
		public StackrealignFunctionAttribute ()
			: base ("stackrealign", quoted: true)
		{}
	}
}
