package xat.bytecode.tests

abstract class AmbiguousBase<T> {
    abstract fun put(value: T)
}

// kotlinc emits a synthetic bridge `put(Ljava/lang/Object;)V` for the generic
// override. Both Kotlin `put` functions match the erased bridge. The internal
// overload is first in the Kotlin metadata, so picking the first match hides the bridge.
class AmbiguousOverloads : AmbiguousBase<String>() {
    @PublishedApi
    internal fun put(count: Int) { }
    override fun put(value: String) { }
}
