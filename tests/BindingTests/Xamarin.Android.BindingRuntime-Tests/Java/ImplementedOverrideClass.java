package com.xamarin.android;

public class ImplementedOverrideClass implements DefaultMethodsInterface
{
    @Override
    public int foo () {
        return 6;
    }

    @Override
    public int getBar () {
        return 100;
    }

    @Override
    public void setBar (int value) {
    }
}
