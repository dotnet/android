interface EnumClassWithInterfacesInterface {
	fun apply(t: Int, u: Int) : Int
	fun applyAsInt(t: Int, u: Int) : Int
}

enum class EnumClassWithInterfaces : EnumClassWithInterfacesInterface {
    PLUS {
        override fun apply(t: Int, u: Int): Int = t + u
    },
    TIMES {
        override fun apply(t: Int, u: Int): Int = t * u
    };

    override fun applyAsInt(t: Int, u: Int) = apply(t, u)
}