package com.xamarin;

import java.util.Map;

public class NestedInterface
{
    public interface DnsSdTxtRecordListener
    {
        void onDnsSdTxtRecordAvailable(String p1, Map<String, String> p2, String p3);
    }
}
