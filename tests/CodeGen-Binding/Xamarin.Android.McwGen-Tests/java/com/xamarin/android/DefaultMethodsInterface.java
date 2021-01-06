package com.xamarin.android;

/** An interface which contains interface default methods. */
public interface DefaultMethodsInterface
{
    default int foo () { return 0; }
    default int getBar () { return 2; }
    default void setBar (int value) { }
    default int toImplement () { throw new UnsupportedOperationException (); }
    default int invokeFoo () { return foo (); }
    static int staticFoo () { return 0; }
}
