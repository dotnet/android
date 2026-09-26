package net.dot.android.test;

public class MyLayoutInflater extends android.view.LayoutInflater {
    protected MyLayoutInflater (android.content.Context context) {
        super (context);
    }

    @Override
    public android.view.LayoutInflater cloneInContext (android.content.Context newContext) {
        return new MyLayoutInflater (newContext);
    }
}
