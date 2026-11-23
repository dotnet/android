package xat.bytecode.tests

import kotlin.coroutines.Continuation

class SuspendFunctions {
    suspend fun getInt(value: Int): Int = value
    suspend fun getNullableString(): String? = null
    suspend fun getNullableInt(): Int? = null
    suspend fun doWork() { }
    suspend fun <T> echo(value: T): T = value

    // Not `suspend`: it only looks like the suspend ABI.
    fun notSuspend(continuation: Continuation<Int>): Any? = null
}

// Parameter names that the generated extension method also uses.
interface SuspendCallback {
    suspend fun merge(self: Boolean, cancellationToken: Int): Int
}
