package com.xamarin.android;

public interface DefaultMethodsInterface
{
    default int foo () { return 0; }
    default int getBar () { return 2; }
    default void setBar (int value) { }
    default int toImplement () { throw new UnsupportedOperationException (); }
    default int invokeFoo () { return foo (); }
}
