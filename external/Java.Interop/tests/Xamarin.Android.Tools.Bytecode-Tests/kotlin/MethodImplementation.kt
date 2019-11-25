public inline class MethodImplementation constructor(@PublishedApi internal val data: Short) : Comparable<Short> {
	public override fun toString(): String = "woof"

	override operator fun compareTo(other: Short): Int {
		return 0;
	}
}