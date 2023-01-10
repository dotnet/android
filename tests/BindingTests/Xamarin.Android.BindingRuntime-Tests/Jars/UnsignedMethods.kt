// Compile with:
// kotlinc UnsignedMethods.kt -d KotlinUnsignedTypes.jar

package foo;

@kotlin.ExperimentalUnsignedTypes
const val SUBSYSTEM_DEPRECATED: UInt = 3u

@kotlin.ExperimentalUnsignedTypes
open class UnsignedInstanceMethods {
    public open fun unsignedInstanceMethod (value: UInt) : UInt { return value; }
    
    public open fun ushortInstanceMethod (value: UShort) : UShort { return value; }
    
    public open fun ulongInstanceMethod (value: ULong) : ULong { println ("kotlin: " + value); return value; }
    
    public open fun ubyteInstanceMethod (value: UByte) : UByte { return value; }
    
    var signedInstanceProperty: Int = 3
  
    var unsignedInstanceProperty: UInt = 3u
  
    var ushortInstanceProperty: UShort = 3u
  
    var ulongInstanceProperty: ULong = 3u
  
    var ubyteInstanceProperty: UByte = 3u

    public open fun uintArrayInstanceMethod (value: UIntArray) : UIntArray { return value; }

    public open fun ushortArrayInstanceMethod (value: UShortArray) : UShortArray { return value; }

    public open fun ulongArrayInstanceMethod (value: ULongArray) : ULongArray { return value; }

    public open fun ubyteArrayInstanceMethod (value: UByteArray) : UByteArray { return value; }

    public open fun intArrayInstanceMethod (value: IntArray) : IntArray { return value; }
}

@kotlin.ExperimentalUnsignedTypes
open class UnsignedInterfaceImplementedMethods : UnsignedInterface {
    public override open fun unsignedInterfaceMethod (value: UInt) : UInt { return value; }
    
    public override open fun unsignedArrayInterfaceMethod (value: UIntArray) : UIntArray { return value; }

    override var unsignedInterfaceProperty: UInt = 3u
    
    override var signedInterfaceProperty: Int = 3
}

@kotlin.ExperimentalUnsignedTypes
open class UnsignedAbstractImplementedMethods : UnsignedAbstractClass () {
    public override open fun unsignedAbstractMethod (value: UInt) : UInt { return value; }

    override var unsignedAbstractClassProperty: UInt = 3u
}

@kotlin.ExperimentalUnsignedTypes
abstract class UnsignedAbstractClass {
    abstract fun unsignedAbstractMethod (value: UInt) : UInt
    
    abstract var unsignedAbstractClassProperty: UInt
}

@kotlin.ExperimentalUnsignedTypes
interface UnsignedInterface {
    fun unsignedInterfaceMethod (value: UInt) : UInt
  
    fun unsignedArrayInterfaceMethod (value: UIntArray) : UIntArray
    
    var unsignedInterfaceProperty: UInt

    var signedInterfaceProperty: Int
}