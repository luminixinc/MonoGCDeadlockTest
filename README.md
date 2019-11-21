MonoGCDeadlockTest
==================

Attempts to force a deadlock in [Mono](https://github.com/mono/mono) garbage collection.

Test case is for an app running on an Android device, but is not suspected to be Android-specific.

Suspected scenario for deadlock race
------------------------------------

It appears to occur when a new native thread calls back into the CLR, while another thread races to start a garbage collection (GC).

Consistently, there are two suspicous thread backtraces shown in Breakpad minidumps captured after the deadlock (including symbols from `libmonosgen-2.0.so`):
- CLR *ThreadC* appears to be performing a GC and is consistently waiting in [`mono_gc_wait_for_bridge_processing()`](https://github.com/mono/mono/blob/12de10007cf9a2973452c9cf2df36692808fefb6/mono/metadata/sgen-bridge.c#L63).
- Native *ThreadN* appears to be in the slowpath of [`mono_thread_attach()`](https://github.com/mono/mono/blob/10ac2750e2b29c812e553778aa854e8653e0dadb/mono/metadata/threads.c#L1535), which is suspected of ending up in [`sgen_alloc_obj_mature()`](https://github.com/mono/mono/blob/10ac2750e2b29c812e553778aa854e8653e0dadb/mono/sgen/sgen-alloc.c#L493) which through more Mono API calls appears to eventually wait on a lock.

This test attempts to create the conditions for the deadlock race to occur:
- A dedicated CLR task running `GC.Collect()`
- Various CLR tasks running creating memory pressure, including objects with finalizers
- Spawned/detached native threads that call back into the CLR
- Test keeps running/racing the calls across the C# <-> C++ SWIG bridge until deadlock occurs

Additionally, this test has some tooling to help introspect into the app state post-deadlock:
- A native watchdog thread is initially spun off that performs a "heartbeat" writing to Android logcat.
- After the deadlock occurs in Mono, the native watchdog thread remains unaffected
- Writing a *kill.app* file to */sdcard* will trigger the watchdog thread to kill the app (triggering an integrated Google Breakpad to create a *minidump.dmp* file in */sdcard*)

Requirements
------------

- Windows 10 development environment (build runs `./swigwin-4.0.1/swig.exe`) -- possibly you could remove this build step, keeping the original generated files included here (and build from Mac/Linux environment)
- Visual Studio 2019 Community Edition with Xamarin.Android and Android NDK and Android SDKs
- Android device (ARM or ARM64).  We have not tested in simulator.

Building the demo
-----------------

- In VS2019, select the appropriate architecture for your device (usually ARM or ARM64), and either Debug or Release configuration will work
- You can run with either the debugger attached or not
- Easiest to read backtraces come with Debug build and debugger not attached
- If all goes well, the build will create an APK with native libraries from *MonoGCDeadlockLib* and Google Breakpad and deploy to device

Running the demo
----------------

- When the app first starts on your device, ensure you give it the read/write external storage permission
- Watch the Android logcat (which by default will have a ton of Mono GC messages)
- You may need wait several minutes to trigger the deadlock.  Please be patient.  After the deadlock is triggered, you will see all the Mono messages stop, but you will continue to see the **WATCHDOG-THREAD: heartbeat...** message.  This native thread is unaffected by the Mono GC deadlock because it doesn't call back into Mono.
- Possibly tapping on the app will now trigger an Android Not Responding (ANR).  DO NOT close the app from the ANR dialog!  Instead...

Post ANR/Deadlock trigger a minidump
------------------------------------

Once you get an ANR and/or Mono GC deadlock, you can communicate with the native watchdog thread to signal the app and trigger the Google Breakpad to create a minidump (dropped in the */sdcard* directory).

- Tell the watchdog thread to kill the app : `adb shell cp /dev/null /sdcard/kill.app`
- Wait for the app to close...
- So you don't immediately crash on next start : `adb shell rm /sdcard/kill.app`
- `adb shell ls -l /sdcard/` - You should see some file like *ab7a68ba-397b-4c55-10d0bda3-a3340271.dmp*
- Now pull the minidump to your computer `adb shell pull /sdcard/ab7a68ba-397b-4c55-10d0bda3-a3340271.dmp`

Homework assignment!
--------------------

For post-processing the minidump file (to see the symbolicated thread backtraces), you will need both the `dump_syms` and `minidump_stackwalk` from Breakpad compiled for your computer.  Compile these tools from either the [Google Breakpad](https://github.com/google/breakpad) repo (or from our included version here).

Sample stacktraces from our devices
-----------------------------------

*ThreadC* crash in Debug build configuration (Release is similar).  Note the call to `mono_gc_wait_for_bridge_processing()`:
```
Thread 23
 0  libc.so + 0x7f23c
     x0 = 0x000000769649b240    x1 = 0x0000000000000089
     x2 = 0x0000000000000002    x3 = 0x0000000000000000
     x4 = 0x0000000000000000    x5 = 0x00000000ffffffff
     x6 = 0x00000000ffffffff    x7 = 0x7f7f7f7fff7fff7f
     x8 = 0x0000000000000062    x9 = 0x0000000000000089
    x10 = 0x0000000000000009   x11 = 0x00000076c0000000
    x12 = 0x0000000000000040   x13 = 0x00000000003489f8
    x14 = 0x0000000000000004   x15 = 0xffffffffffffffff
    x16 = 0x00000077803fc950   x17 = 0x000000778038c220
    x18 = 0x0000000000000033   x19 = 0x0000000000000002
    x20 = 0x0000000000000000   x21 = 0x000000769649b240
    x22 = 0x0000000000000089   x23 = 0x0000007680886008
    x24 = 0x0000007680886020   x25 = 0x0000000000000004
    x26 = 0x000000000000014a   x27 = 0x0000000000000000
    x28 = 0x0000007680886020    fp = 0x0000007680884eb0
     lr = 0x000000778038f750    sp = 0x0000007680884e50
     pc = 0x000000778038c23c
    Found by: given as instruction pointer in context
 1  libc.so + 0x8274c
     fp = 0x0000007680884f30    lr = 0x00000077803eee9c
     sp = 0x0000007680884ec0    pc = 0x000000778038f750
    Found by: previous frame's frame pointer
 2  libc.so + 0xe1e98
     fp = 0x0000007680884f60    lr = 0x0000007696349db8
     sp = 0x0000007680884f40    pc = 0x00000077803eee9c
    Found by: previous frame's frame pointer
 3  libmonosgen-2.0.so!mono_gc_get_heap_size + 0xbe04
     fp = 0x0000007680884f70    lr = 0x000000769632fd44
     sp = 0x0000007680884f70    pc = 0x0000007696349db8
    Found by: previous frame's frame pointer
 4  libmonosgen-2.0.so!mono_gc_wait_for_bridge_processing + 0x20
     fp = 0x0000007680884fb0    lr = 0x000000769634fd24
     sp = 0x0000007680884f80    pc = 0x000000769632fd44
    Found by: previous frame's frame pointer
 5  libmonosgen-2.0.so!mono_gc_get_heap_size + 0x11d70
     fp = 0x0000007680884fe0    lr = 0x00000076962f9fb4
     sp = 0x0000007680884fc0    pc = 0x000000769634fd24
    Found by: previous frame's frame pointer
 6  libmonosgen-2.0.so!mono_free_verify_list + 0x3378
     fp = 0x0000007680885020    lr = 0x000000769628aa34
     sp = 0x0000007680884ff0    pc = 0x00000076962f9fb4
    Found by: previous frame's frame pointer
 7  libmonosgen-2.0.so!mono_lookup_icall_symbol + 0xc300
     fp = 0x0000007680885030    lr = 0x00000076e51e99bc
     sp = 0x0000007680885030    pc = 0x000000769628aa34
    Found by: previous frame's frame pointer
 8  0x76e51e99b8
     fp = 0x0000007680885140    lr = 0x00000076e51e9898
     sp = 0x0000007680885040    pc = 0x00000076e51e99bc
    Found by: previous frame's frame pointer
```

*ThreadN* crash in Debug build configuration (Release is similar).  Note the call to `mono_gc_pending_finalizers()`:
```
Thread 35
 0  libc.so + 0x7f23c
     x0 = 0x000000769649b240    x1 = 0x0000000000000089
     x2 = 0x0000000000000002    x3 = 0x0000000000000000
     x4 = 0x0000000000000000    x5 = 0x00000000ffffffff
     x6 = 0x00000000ffffffff    x7 = 0x000000765ec13740
     x8 = 0x0000000000000062    x9 = 0x0000000000000089
    x10 = 0x0000000000000009   x11 = 0x0000000000000092
    x12 = 0x0000007696500500   x13 = 0x00000076965004d6
    x14 = 0x000000769651a3e8   x15 = 0x00000076965004ef
    x16 = 0x00000077803fc950   x17 = 0x000000778038c220
    x18 = 0x000000777e9680d8   x19 = 0x0000000000000002
    x20 = 0x0000000000000000   x21 = 0x000000769649b240
    x22 = 0x0000000000000089   x23 = 0x000000766ddaf008
    x24 = 0x000000766ddaf020   x25 = 0x0000000000000000
    x26 = 0x0000012d5d8b2031   x27 = 0x000000766ddaf020
    x28 = 0x000000769649aeb0    fp = 0x000000766ddae3f0
     lr = 0x000000778038f750    sp = 0x000000766ddae390
     pc = 0x000000778038c23c
    Found by: given as instruction pointer in context
 1  libc.so + 0x8274c
     fp = 0x000000766ddae470    lr = 0x00000077803eee9c
     sp = 0x000000766ddae400    pc = 0x000000778038f750
    Found by: previous frame's frame pointer
 2  libc.so + 0xe1e98
     fp = 0x000000766ddae4a0    lr = 0x0000007696349db8
     sp = 0x000000766ddae480    pc = 0x00000077803eee9c
    Found by: previous frame's frame pointer
 3  libmonosgen-2.0.so!mono_gc_get_heap_size + 0xbe04
     fp = 0x000000766ddae4d0    lr = 0x000000769634012c
     sp = 0x000000766ddae4b0    pc = 0x0000007696349db8
    Found by: previous frame's frame pointer
 4  libmonosgen-2.0.so!mono_gc_get_heap_size + 0x2178
     fp = 0x000000766ddae4f0    lr = 0x000000769633bc08
     sp = 0x000000766ddae4e0    pc = 0x000000769634012c
    Found by: previous frame's frame pointer
 5  libmonosgen-2.0.so!mono_gc_pending_finalizers + 0x9d4
     fp = 0x000000766ddae520    lr = 0x00000076962d0390
     sp = 0x000000766ddae500    pc = 0x000000769633bc08
    Found by: previous frame's frame pointer
 6  libmonosgen-2.0.so!mono_object_new_fast + 0x88
     fp = 0x000000766ddae5b0    lr = 0x00000076962e8b1c
     sp = 0x000000766ddae530    pc = 0x00000076962d0390
    Found by: previous frame's frame pointer
 7  libmonosgen-2.0.so!mono_threads_get_default_stacksize + 0x15c
     fp = 0x000000766ddae5e0    lr = 0x00000076962e9338
     sp = 0x000000766ddae5c0    pc = 0x00000076962e8b1c
    Found by: previous frame's frame pointer
 8  libmonosgen-2.0.so!mono_thread_attach + 0x68
     fp = 0x000000766ddae620    lr = 0x00000076964d80b8
     sp = 0x000000766ddae5f0    pc = 0x00000076962e9338
    Found by: previous frame's frame pointer
 9  libmonodroid.so + 0x100b4
     fp = 0x000000766ddae670    lr = 0x00000076964ecaf8
     sp = 0x000000766ddae630    pc = 0x00000076964d80b8
    Found by: previous frame's frame pointer
10  libmonodroid.so + 0x24af4
     fp = 0x000000766ddae740    lr = 0x00000076964e97f8
     sp = 0x000000766ddae680    pc = 0x00000076964ecaf8
    Found by: previous frame's frame pointer
11  libmonodroid.so + 0x217f4
     fp = 0x000000766ddae770    lr = 0x00000076964e94f4
     sp = 0x000000766ddae750    pc = 0x00000076964e97f8
    Found by: previous frame's frame pointer
12  libmonodroid.so + 0x214f0
     fp = 0x000000766ddae850    lr = 0x000000769633010c
     sp = 0x000000766ddae780    pc = 0x00000076964e94f4
    Found by: previous frame's frame pointer
13  libmonosgen-2.0.so!mono_gc_register_bridge_callbacks + 0x314
     fp = 0x000000766ddae890    lr = 0x000000769634c744
     sp = 0x000000766ddae860    pc = 0x000000769633010c
    Found by: previous frame's frame pointer
14  libmonosgen-2.0.so!mono_gc_get_heap_size + 0xe790
     fp = 0x000000766ddae9a0    lr = 0x0000007696349730
     sp = 0x000000766ddae8a0    pc = 0x000000769634c744
    Found by: previous frame's frame pointer
15  libmonosgen-2.0.so!mono_gc_get_heap_size + 0xb77c
     fp = 0x000000766ddae9d0    lr = 0x0000007696340160
     sp = 0x000000766ddae9b0    pc = 0x0000007696349730
    Found by: previous frame's frame pointer
16  libmonosgen-2.0.so!mono_gc_get_heap_size + 0x21ac
     fp = 0x000000766ddae9f0    lr = 0x000000769633bc08
     sp = 0x000000766ddae9e0    pc = 0x0000007696340160
    Found by: previous frame's frame pointer
17  libmonosgen-2.0.so!mono_gc_pending_finalizers + 0x9d4
     fp = 0x000000766ddaea20    lr = 0x00000076962d0390
     sp = 0x000000766ddaea00    pc = 0x000000769633bc08
    Found by: previous frame's frame pointer
18  libmonosgen-2.0.so!mono_object_new_fast + 0x88
     fp = 0x000000766ddaeab0    lr = 0x00000076962e8d08
     sp = 0x000000766ddaea30    pc = 0x00000076962d0390
    Found by: previous frame's frame pointer
19  libmonosgen-2.0.so!mono_threads_get_default_stacksize + 0x348
     fp = 0x000000766ddaeae0    lr = 0x00000076962e9344
     sp = 0x000000766ddaeac0    pc = 0x00000076962e8d08
    Found by: previous frame's frame pointer
20  libmonosgen-2.0.so!mono_thread_attach + 0x74
     fp = 0x000000766ddaeb20    lr = 0x00000076962f0f2c
     sp = 0x000000766ddaeaf0    pc = 0x00000076962e9344
    Found by: previous frame's frame pointer
21  libmonosgen-2.0.so!mono_thread_is_foreign + 0x8e4
     fp = 0x000000766ddaeb40    lr = 0x00000076962f1024
     sp = 0x000000766ddaeb30    pc = 0x00000076962f0f2c
    Found by: previous frame's frame pointer
22  libmonosgen-2.0.so!mono_threads_attach_coop + 0x1c
     fp = 0x000000766ddaeb50    lr = 0x00000076e51bf188
     sp = 0x000000766ddaeb50    pc = 0x00000076962f1024
    Found by: previous frame's frame pointer
23  0x76e51bf184
     fp = 0x000000766ddaeb90    lr = 0x000000768ed2b0c4
     sp = 0x000000766ddaeb60    pc = 0x00000076e51bf188
    Found by: previous frame's frame pointer
24  libMonoGCDeadlockLib.so!runMonoGCDeadlockTest()::$_0::operator()() const [MonoGCDeadlockLib.cpp : 50 + 0x8]
     fp = 0x000000766ddaecf0    lr = 0x000000768ed2afa0
     sp = 0x000000766ddaeba0    pc = 0x000000768ed2b0c4
    Found by: previous frame's frame pointer
25  libc.so + 0xe1100
     fp = 0x000000766ddaed10    sp = 0x000000766ddaed00
     pc = 0x00000077803ee104
    Found by: call frame info
```
