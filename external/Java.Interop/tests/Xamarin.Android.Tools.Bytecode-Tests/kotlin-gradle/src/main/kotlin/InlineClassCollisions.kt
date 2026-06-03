@file:JvmName("InlineClassCollisionsKt")

package xat.bytecode.tests

// Two distinct Kotlin inline classes that erase to the same JVM primitive (long).
// Both `tint(MyColor)` and `tint(MyAlpha)` mangle to `tint-<hash>(J)V`, so they
// collide once class-parse drops the hash suffix. This is the exact scenario
// that Jetpack Compose triggers with Color/TextUnit/etc. and is the case
// step (1) of dotnet/java-interop#1431 must handle.
@JvmInline
value class MyColor(val value: ULong)

@JvmInline
value class MyAlpha(val value: ULong)

// A second inline class with a different backing primitive, so we can verify
// that *non*-colliding hash siblings still survive.
@JvmInline
value class MyDp(val value: Float)

object Widgets {

    // Colliding pair: both erase to `tint-XXXXXXX(J)V`.
    fun tint(color: MyColor) { /* no-op */ }
    fun tint(alpha: MyAlpha) { /* no-op */ }

    // Distinct hash-mangled sibling of the same source name — should survive
    // alongside one of the `tint(long)` overloads.
    fun tint(dp: MyDp) { /* no-op */ }

    // A non-colliding pair: different arity, both hash-mangled.
    fun pad(dp: MyDp): MyDp = dp
    fun pad(dp1: MyDp, dp2: MyDp): MyDp = dp1
}
