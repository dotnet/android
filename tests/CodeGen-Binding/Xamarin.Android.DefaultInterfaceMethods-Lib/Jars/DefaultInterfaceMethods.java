package com.xamarin.test;

public interface DefaultInterfaceMethods
{
    default int foo () { return 0; }
    default int getBar () { return 1; }
    default int getBaz () { return 2; }
    default void setBaz (int value) { }
    default int toImplement () { throw new UnsupportedOperationException (); }
}
