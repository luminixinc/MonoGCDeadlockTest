
#if defined(__ANDROID__)

#   include "client/linux/handler/exception_handler.h"
#   include "client/linux/handler/minidump_descriptor.h"

#   include <dlfcn.h>
#   include <android/log.h>
#   include <sys/types.h>
#   include <sys/stat.h>
#   include <unistd.h>

// WARNING : these need to be kept consistent with the same varialbe in exception_handler.cc
static const int kExceptionSignals[] = { SIGSEGV, SIGABRT, SIGFPE, SIGILL, SIGBUS, SIGTRAP };
static const int kNumHandledSignals = sizeof(kExceptionSignals) / sizeof(kExceptionSignals[0]);

// 2017/08/02 NOTE : this may be called multiple times, but will only install signal handlers if Mono's are not present
void initDroidBreakpad(const char *dumpDir) {

    // Sanity-check existing handlers are not Mono ones ...
    bool install_handler = true;
    for (int i = 0; i < kNumHandledSignals; ++i) {
        struct sigaction oldact;
        if (sigaction(kExceptionSignals[i], NULL, &oldact) == 0) {

            Dl_info info = { 0 };
            ::dladdr((const void*)oldact.sa_sigaction, &info);
#ifndef NDEBUG
            __android_log_print(ANDROID_LOG_INFO, "BREAKPAD", "NOTE : previous handler for signal %d : %p/%p:%s %s", kExceptionSignals[i], info.dli_fbase, oldact.sa_sigaction, info.dli_sname, info.dli_fname);
#endif

            if (info.dli_fname && strcasestr(info.dli_fname, "mono")) {
#ifndef NDEBUG
                __android_log_print(ANDROID_LOG_INFO, "BREAKPAD", "NOTE : found Mono signal handler, NOT installing Breakpad signal handlers!");
#endif
                install_handler = false;
            }
        }
    }

    if (!install_handler) {
        return;
    }

    mkdir(dumpDir, S_IRWXU | S_IRGRP | S_IXGRP | S_IROTH | S_IXOTH); // shouldn't care about failures here

#ifndef NDEBUG
    __android_log_print(ANDROID_LOG_INFO, "BREAKPAD", "Breakpad dump dir : %s", dumpDir);
#endif

    google_breakpad::MinidumpDescriptor descriptor(dumpDir);
    static google_breakpad::ExceptionHandler eh(descriptor, /*FilterCallback:*/NULL, /*MinidumpCallback:*/NULL, /*callback_context:*/NULL, install_handler, /*server_fd:*/-1);
}

// 2017/08/02 HACK NOTE : for redundancy and in case of prior pre-load failure, this is here to support "problematic devices" DLLImport -- (triggered from MainActivity)
extern "C" void droidBreakpadDLLImportEntryPoint(void) {
}

#endif
