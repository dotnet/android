@Suppress("NAME_SHADOWING")
public class NameShadowing {
	// Property and method
	private val count: Int = 3
	fun count(): Int = count

	// Field and method
	private var hitCount = 0
	fun hitCount(): Int = hitCount

	// Private property and explicit getter/setter
	private var type = 0
	fun getType(): Int = type
	fun setType(type: Int) { }

	// Private immutable property and explicit getter/setter
	private val type2 = 0
	fun getType2(): Int = type2
	fun setType2(type: Int) { }

	// Internal property and explicit getter/setter
	internal var itype = 0
	fun getItype(): Int = itype
	fun setItype(type: Int) { }

	// Internal immutable property and explicit getter/setter
	internal val itype2 = 0
	fun getItype2(): Int = itype2
	fun setItype2(type: Int) { }
}