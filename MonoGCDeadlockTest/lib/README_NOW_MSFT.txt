HACK NOTES
==========

The libc++_shared.so libraries sourced 2019/11/20 from `C:\Microsoft\AndroidNDK64\android-ndk-r16b\sources\cxx-stl\llvm-libc++\libs\arm64-v8a\libc++_shared.so`

WARNING : make sure you update the `libc++_shared.so` files for each arch+ABI when you upgrade the NDK or if/when you switch the C++ STL library ... (e.g., copy the new library from the appropriate NDK location, such as:
`C:\Microsoft\AndroidNDK64\android-ndk-r16b\sources\cxx-stl\llvm-libc++\libs\arm64-v8a\libc++_shared.so`)

OMG BUT WHY?!
-------------

I have not found a way to get VS to reliably include the appropriate STL library for all the arches being built (e.g., for batch building a fat APK of all 4 arch+ABI), so therefore the manual hack.

