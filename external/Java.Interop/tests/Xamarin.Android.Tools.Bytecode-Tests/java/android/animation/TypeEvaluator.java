package android.animation;

public interface TypeEvaluator<T> {
	T evaluate(float fraction, T startValue, T endValue);
}
