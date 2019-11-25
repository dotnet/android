interface MyInterface {
    fun bar()

    val prop: Int // abstract

    val propertyWithImplementation: String
        get() = "foo"

    fun foo() {
        print(prop)
    }
}

class MyInterfaceChild : MyInterface {
	override val prop: Int = 29

    override fun bar() {
        // body
    }
}

interface MyInterface2 : MyInterface {
	val value2 : Int

	override val prop: Int get() = 30
}