#include "MonoGCDeadlockLib.h"

#include <sys/stat.h>

#include <thread>

#define TAG "DEADLOCK-TEST"

#define LOGI(...) ((void)__android_log_print(ANDROID_LOG_INFO, TAG, __VA_ARGS__))
#define LOGW(...) ((void)__android_log_print(ANDROID_LOG_WARN, TAG, __VA_ARGS__))

#if defined(__arm__)
#   if defined(__ARM_ARCH_7A__)
#       if defined(__ARM_NEON__)
#           define ABI "armeabi-v7a/NEON"
#       else
#           define ABI "armeabi-v7a"
#       endif
#   else
#       define ABI "armeabi"
#   endif
#elif defined (__aarch64__)
#   define ABI "arm64-v8a"
#elif defined(__i386__)
#   define ABI "x86"
#else
#   define ABI "unknown"
#endif

// Define this to non-zero to force ANR/deadlock codepath, zero (0) will not ANR/deadlock
#define RUN_THREADED_TO_DEADLOCK_MONO 1

static CallbackToCLR_fn _cb = nullptr;

void setCallbackToCLR(CallbackToCLR_fn cb) {
    _cb = cb;
}

void runMonoGCDeadlockTest(void) {
    static int num = 0;

    __android_log_print(ANDROID_LOG_INFO, "DEADLOCK-TEST", ABI " native runMonoGCDeadlockTest #%d ...", num);
    ++num;

    // CALLBACK TO C# side ...

    if (RUN_THREADED_TO_DEADLOCK_MONO) {
        auto thr = std::thread([=]() {
            // This will eventually get stuck ...
            _cb("thread hello");
        });
        thr.detach();
    }
    else {
        _cb = nullptr;
        // this does not deadlock ...
        _cb("hello");
    }
}

// The purpose here is basically to showcase that another native thread (without any interaction with Mono/CLR) does not get blocked
static void _watchdogThread(void) {
    while (true) {
        __android_log_print(ANDROID_LOG_INFO, "WATCHDOG-THREAD", "heartbeat...");

        // Check for presence of kill.app file ...
        // (TODO: From Android settings, give app permission to read/write external storage!)
        struct stat statbuf = { 0 };
        if (stat("/sdcard/kill.app", &statbuf) == 0) {

            __android_log_print(ANDROID_LOG_INFO, "WATCHDOG-THREAD", "Hey there, don't forget to remove /sdcard/kill.app for next time!");
            __android_log_print(ANDROID_LOG_INFO, "WATCHDOG-THREAD", "killing...");
            // if Google Breakpad is integrated, this kill() will create a minidump file that will show the deadlocked Mono threads
            kill(getpid(), SIGBUS);
        }

        sleep(4);
    }
}

void startNativeWatchdogThread(void) {
    std::thread watchdog(_watchdogThread);
    watchdog.detach();
}
