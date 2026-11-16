package xat.bytecode.tests

// Kotlin metadata tells which types can be null. KotlinFixups copies this
// information to the api.xml `not-null` attributes.
class Nullability(val name: String, val nickname: String?) {

    var title: String = ""
    var subtitle: String? = null

    lateinit var late: String

    @JvmField
    var field: String = ""

    @JvmField
    var nullableField: String? = null

    fun join(text: String, other: String?, count: Int): String = text

    fun find(text: String): String? = null

    fun <T> generic(value: T): T = value

    fun String.extension(): String = this
}
