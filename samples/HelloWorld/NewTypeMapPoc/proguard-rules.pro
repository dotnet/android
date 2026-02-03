# Override the blanket mono.android.** keep rule
# Only keep classes that are actually needed

# Allow R8 to remove unused Implementor classes
# The blanket -keep class mono.android.** in proguard_xamarin.cfg is too broad
# We selectively keep only the essential classes below

# Note: This file is processed AFTER proguard_xamarin.cfg, so we can't actually 
# remove the keep rules, but we document the issue here.
