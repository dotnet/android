# TypeMaps Assembly Analysis: Nested Type Encoding

## Overview

This document details the findings from disassembling the `_Microsoft.Android.TypeMaps.dll` assembly to understand how nested types (e.g., `Java.Lang.Thread/IUncaughtExceptionHandler`) are encoded and represented.

## Key Finding: Dual Encoding Approach

The TypeMaps generator uses a **dual encoding strategy** for nested types:

1. **Class-level naming**: Uses underscores to flatten the hierarchy into a single class name
2. **IL-level references**: Uses standard .NET forward-slash notation for nested type references

### Example: IUncaughtExceptionHandler

```
Source nested type:           Java.Lang.Thread/IUncaughtExceptionHandler
↓
Generated proxy class name:   Java_Lang_Thread_IUncaughtExceptionHandler_Proxy
Generated namespace:          _Microsoft.Android.TypeMaps
↓
IL type reference syntax:     [Mono.Android]Java.Lang.Thread/IUncaughtExceptionHandler
```

## Encoding Details

### 1. Proxy Class Naming Convention

**Pattern**: `{OuterType}_{NestedType}_Proxy`

Components:
- `Java_Lang_Thread` — The outer type (Java.Lang.Thread)
- `IUncaughtExceptionHandler` — The nested type name
- `Proxy` — Suffix indicating this is a generated proxy class

**Namespace**: `_Microsoft.Android.TypeMaps` (underscore-prefixed)

**Rationale**:
- Allows nested types to be represented as top-level classes
- Prevents naming conflicts with actual nested classes
- Simplifies code generation (no need for actual nesting)

### 2. IL Metadata Type References

In generated IL bytecode, the nested type is referenced using standard .NET notation:

```csharp
ldtoken [Mono.Android]Java.Lang.Thread/IUncaughtExceptionHandlerInvoker
```

Breaking this down:
- `[Mono.Android]` — Assembly reference
- `Java.Lang.Thread` — Outer type
- `/IUncaughtExceptionHandlerInvoker` — Nested type (with forward slash separator)

The forward slash (`/`) is the standard IL notation for nested types, maintaining .NET compliance.

### 3. Proxy Class Structure

The generated proxy class extends `JavaPeerProxy` and implements key methods:

```csharp
.class public auto ansi sealed beforefieldinit 
       Java_Lang_Thread_IUncaughtExceptionHandler_Proxy
       extends [Mono.Android]Java.Interop.JavaPeerProxy
{
  // Constructor stores the invoker type reference
  .method public hidebysig specialname rtspecialname 
         instance default void '.ctor' ()  cil managed 
  {
    ldarg.0 
    call instance void [Mono.Android]Java.Interop.JavaPeerProxy::.ctor()
    ldarg.0 
    // Reference the nested invoker type
    ldtoken [Mono.Android]Java.Lang.Thread/IUncaughtExceptionHandlerInvoker
    call class [System.Private.CoreLib]System.Type 
         class [System.Private.CoreLib]System.Type::GetTypeFromHandle(...)
    call instance void [Mono.Android]Java.Interop.JavaPeerProxy
         ::set_InvokerType(class [System.Private.CoreLib]System.Type)
    ret 
  }

  // CreateInstance uses the nested invoker type
  .method public virtual hidebysig 
         instance default class [Java.Interop]Java.Interop.IJavaPeerable 
         CreateInstance (native int handle, 
                        [Mono.Android]Android.Runtime.JniHandleOwnership transfer)  
  {
    ldarg.1 
    ldarg.2 
    // Instantiate the nested invoker type
    newobj instance void 
      [Mono.Android]Java.Lang.Thread/IUncaughtExceptionHandlerInvoker::.ctor(...)
    ret 
  }

  // GetDerivedTypeFactory uses the nested type as generic parameter
  .method public virtual hidebysig 
         instance default class [Mono.Android]Java.Interop.DerivedTypeFactory 
         GetDerivedTypeFactory()  
  {
    ldsfld class [Mono.Android]Java.Interop.DerivedTypeFactory`1
          <class [Mono.Android]Java.Lang.Thread/IUncaughtExceptionHandler>
          [Mono.Android]Java.Interop.DerivedTypeFactory`1<...>::Instance
    ret 
  }
}
```

### 4. String Encoding in Binary

Strings extracted from the assembly include:
- `IUncaughtExceptionHandlerInvoker` — The invoker class name
- `IUncaughtExceptionHandler` — The nested interface name
- `Java_Lang_Thread_IUncaughtExceptionHandler_Proxy` — The proxy class name

These are embedded as UTF-8 strings in the .NET metadata.

## Encoding Summary

| Component | Encoding Format | Example |
|-----------|-----------------|---------|
| **Proxy Class Name** | `{Outer}_{Nested}_Proxy` (flattened with underscores) | `Java_Lang_Thread_IUncaughtExceptionHandler_Proxy` |
| **Namespace** | `_Microsoft.Android.TypeMaps` | Fixed for all TypeMaps classes |
| **IL Type Reference** | `[Assembly]{Outer}/{Nested}` (standard .NET nested syntax) | `[Mono.Android]Java.Lang.Thread/IUncaughtExceptionHandler` |
| **Generic Parameter** | Full nested type in angle brackets | `DerivedTypeFactory<[Mono.Android]Java.Lang.Thread/IUncaughtExceptionHandler>` |
| **Binary Strings** | Full class names as UTF-8 | Stored in metadata string table |

## Design Rationale

### Why Dual Encoding?

1. **Compatibility**: IL uses standard .NET nested type syntax (`/`) for type references
2. **Namespace separation**: Class names use underscores to flatten the hierarchy
3. **Disambiguation**: Prevents conflicts between nested types and regular types
4. **Simplicity**: Generators don't need to create actual nested classes

### Benefits of Flattening

✓ **Simpler generation**: No need to handle nested class definitions  
✓ **Clearer naming**: Type relationship is explicit in class name  
✓ **Easier reflection**: All TypeMaps classes are top-level in their namespace  
✓ **Consistent structure**: All proxies follow the same naming pattern  

### Runtime Resolution

The proxy stores a reference to the invoker type at runtime:
- Uses `ldtoken` to get a `RuntimeTypeHandle`
- Calls `System.Type.GetTypeFromHandle()` to resolve it
- Stores the result via `set_InvokerType()`
- Enables dynamic instantiation through `CreateInstance()`

## Related Structures

### Invoker Types
- Referenced as nested types: `[Mono.Android]Java.Lang.Thread/IUncaughtExceptionHandlerInvoker`
- Wrapper classes that bridge Java/Kotlin and .NET
- Instantiated in proxy's `CreateInstance()` method

### Generic Factories
- Type parameter preserves the full nested type path
- Example: `DerivedTypeFactory<[Mono.Android]Java.Lang.Thread/IUncaughtExceptionHandler>`
- Enables proper factory instantiation of nested interface implementations

## Practical Example Flow

1. **Source**: Java class `Thread` with nested interface `IUncaughtExceptionHandler`
2. **Generation**: TypeMaps creates proxy class `Java_Lang_Thread_IUncaughtExceptionHandler_Proxy`
3. **Storage**: Proxy namespace is `_Microsoft.Android.TypeMaps`
4. **References**: 
   - IL code uses: `[Mono.Android]Java.Lang.Thread/IUncaughtExceptionHandlerInvoker`
   - Generic types use: `DerivedTypeFactory<[Mono.Android]Java.Lang.Thread/IUncaughtExceptionHandler>`
5. **Runtime**: Proxy instantiates invoker and returns it via factory

## Files Inspected

- **Assembly**: `samples/NativeAOT/obj/Release/net11.0-android/android-arm64/linked/_Microsoft.Android.TypeMaps.dll`
- **Tools used**: `ikdasm`, `monodis`, `strings`
- **Size**: 2,176,512 bytes (2.1 MB)

## Conclusion

The TypeMaps assembly uses a sophisticated dual-encoding strategy to handle nested types:
- Flat class names with underscore separators for .NET representation
- Standard IL forward-slash notation for type references
- Generic parameters that preserve full nested type information
- Runtime type resolution through `ldtoken` and `GetTypeFromHandle()`

This approach seamlessly bridges Java/Kotlin nested types with .NET's nested type system while maintaining simplicity in code generation.
