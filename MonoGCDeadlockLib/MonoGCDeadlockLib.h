#pragma once

typedef void (*CallbackToCLR_fn)(const char *mesg);

extern void setCallbackToCLR(CallbackToCLR_fn cb);

extern void runMonoGCDeadlockTest(void);

extern void startNativeWatchdogThread(void);

extern void initDroidBreakpad(const char *dumpDir);