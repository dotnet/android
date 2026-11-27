@file:JvmName("ValueClasses")

package net.dot.android.test.kotlin

// These functions only use JVM primitives at runtime, so the test app does not need the Kotlin standard library.

@JvmInline
value class Meters(val value: Double) {
    init {
        require(value >= 0) { "Meters cannot be negative" }
    }
}

@JvmInline
value class Id(val value: Long)

fun add(a: Meters, b: Meters): Meters = Meters(a.value + b.value)

fun Meters.doubled(): Meters = Meters(value * 2)

fun orZero(meters: Meters?): Meters = meters ?: Meters(0.0)

fun findId(value: Long): Id? = if (value >= 0) Id(value) else null

var defaultLength: Meters = Meters(1.0)

fun lengths(count: Int): List<Meters> {
    val list = java.util.ArrayList<Meters>()
    for (i in 0 until count) {
        list.add(Meters(i.toDouble()))
    }
    return list
}
