package xat.bytecode.tests

// Kotlin default arguments. kotlinc emits `$default` helpers (and a
// DefaultConstructorMarker constructor) that the binding generator uses to
// create C# overloads which omit trailing defaulted parameters.
class DefaultArguments (val name: String, val count: Int = 1) {
    fun greet (greeting: String, times: Int = 2, loud: Boolean = false): String = greeting

    fun middle (first: Int = 1, second: Int): Int = first + second

    fun toString (indent: Int = 0): String = name

    // `format ()` also gets a `string Format ()` overload, so `format (indent)` must not add one.
    fun format (): CharSequence = name

    fun format (indent: Int = 0): String = name

    // `params string[]` and `string[]` are the same C# signature.
    fun log (vararg items: String) {}

    fun log (items: Array<String>, level: Int = 1) {}
}

fun createDefaultArguments (name: String = "bob"): DefaultArguments = DefaultArguments (name)

interface DefaultArgumentsInterface {
    fun send (message: String, retries: Int = 3)
}

interface DefaultArgumentsChild : DefaultArgumentsInterface {
    // Their overloads would hide the inherited `send (String, Int)` and `send (String)`.
    fun send (message: String, retries: Int, urgent: Boolean = false)

    fun send (message: String, urgent: Boolean = false)
}
