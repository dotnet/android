@file:JvmName("DefaultArguments")

package net.dot.android.test.kotlin

// Kotlin computes these default values in `$default` helpers. Only Int is used,
// so the test app does not need the Kotlin standard library.

fun scale(value: Int, factor: Int = 2, offset: Int = factor + 1): Int = value * factor + offset

class Counter(val start: Int, val step: Int = start * 2)

open class Calculator {
    open fun compute(a: Int, b: Int = 10): Int = a + b
}

class Multiplier : Calculator() {
    override fun compute(a: Int, b: Int): Int = a * b
}
