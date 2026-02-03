# TypeMap V4: A Proposal for AOT-Safe Type Mapping

**Status:** Proof of Concept  
**Date:** February 2026

---

## Background

.NET for Android maps Java types to .NET types at runtime. When Java calls a method on `MainActivity`, we locate the corresponding .NET type and create an instance.

The current implementation relies on:

- `Activator.CreateInstance()` for type instantiation
- `Type.GetType()` for type resolution
- Runtime reflection for constructor discovery

These patterns are incompatible with NativeAOT and increasingly problematic for trimming.

---

## The Constraint

NativeAOT and aggressive trimming require that all types and their constructors be statically known at build time. Reflection-based activation cannot satisfy this requirement.

We need a type mapping system where:

1. All activatable types are known at build time
2. Type instantiation doesn't use reflection
3. The trimmer can safely remove unused types

---

## Proposed Approach

Generate type mapping information at build time instead of discovering it at runtime.

**Build time:**
- Scan assemblies for types extending Java classes
- Generate factory methods for each activatable type
- Emit a lookup table mapping JNI names to factories

**Runtime:**
- Look up JNI name in pre-built table
- Call factory method directly (no reflection)

This eliminates the `[RequiresUnreferencedCode]` annotation from the activation path.

---

## What This Changes

| Aspect | Current | Proposed |
|--------|---------|----------|
| Type discovery | Runtime reflection | Build-time scanning |
| Instance creation | `Activator.CreateInstance` | Generated factory |
| Type lookup | Native binary + reflection | Attribute-based table |
| Trimmer safety | `[RequiresUnreferencedCode]` | Fully safe |

The public API and developer experience remain unchanged. This is an internal implementation change.

---

## Known Limitations

**Generic collections across JNI boundary:** `IList<T>` and similar cannot be pre-generated for all possible `T`. Apps using these patterns will need to use concrete types.

**Dynamic type loading:** Types loaded via `Assembly.Load` at runtime won't be in the pre-built map. This is an inherent limitation of AOT.

**Build time cost:** Generating proxies for all types adds build overhead. We plan to pre-generate SDK types during workload build to mitigate this.

---

## Current State

The PoC demonstrates the approach works:
- Sample app runs on device with V4 type mapping
- Activity lifecycle, callbacks, and exports function correctly
- No reflection used in activation path

Outstanding work:
- Test coverage
- Performance characterization
- Build time optimization (SDK pre-generation)
- Security review of code generation approach

---

## Open Questions

1. **Performance:** Is the managed dictionary lookup competitive with native binary search?
2. **Build time:** What's the actual impact without SDK pre-generation?
3. **Migration:** How do we handle apps with unsupported patterns?
4. **Timeline:** When can we deprecate the legacy system?

---

## Related Documents

- `type-mapping-api-v4-spec.md` — Technical specification
- `REVIEW.md` — Critical analysis and identified issues
- `IMPLEMENTATION_PLAN.md` — Task breakdown
