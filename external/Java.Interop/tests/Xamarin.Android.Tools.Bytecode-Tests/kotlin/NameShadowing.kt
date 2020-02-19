@Suppress("NAME_SHADOWING")
public class NameShadowing {
	// Property and method
	private val count: Int = 3
	fun count(): Int = count

	// Field and method
	private var hitCount = 0
	fun hitCount(): Int = hitCount

	// Property and setter
	private var type = 0
	fun setType(type: Int) = { println (type); }
}