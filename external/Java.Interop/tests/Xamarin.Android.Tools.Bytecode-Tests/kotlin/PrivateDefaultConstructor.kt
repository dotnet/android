public open class PrivateDefaultConstructor private constructor (internal val isFoo: Boolean) {
	init { }

	public companion object Default : PrivateDefaultConstructor (isFoo = false) { }
}
