package com.xamarin.android.test.msbuildtest;

public class JavaSourceJarTest
{
    /**
     * Returns greeting message.
     * <p>
     * Returns "Morning, ", "Hello, " or "Evening, " with name argument,
     * depending on the argument hour. Includes a {@docRoot}test.html element.
     * </p>
     * @param name name to display.
     * @param date time to determine the greeting message.
     * @return the resulting message.
     */
    public String greet (String name, java.util.Date date)
    {
        String head = date.getHours () < 11 ? "Morning, " : date.getHours () < 17 ? "Hello, " : "Evening, ";
        return head + name;
    }
}
