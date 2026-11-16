package xat.bytecode.tests

// Kotlin default arguments. kotlinc emits `$default` helpers (and a
// DefaultConstructorMarker constructor) that the binding generator uses to
// create C# overloads which omit trailing defaulted parameters.
class DefaultArguments (val name: String, val count: Int = 1) {
    fun greet (greeting: String, times: Int = 2, loud: Boolean = false): String = greeting

    fun middle (first: Int = 1, second: Int): Int = first + second
}

fun createDefaultArguments (name: String = "bob"): DefaultArguments = DefaultArguments (name)

interface DefaultArgumentsInterface {
    fun send (message: String, retries: Int = 3)
}
