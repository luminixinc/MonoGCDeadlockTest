//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (http://www.swig.org).
// Version 4.0.1
//
// Do not make changes to this file unless you know what you are doing--modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------

namespace MonoGCDeadlockLib {

public class MonoGCDeadlockLib {
  public static void setCallbackToCLR(CallbackToCLRDelegate cb) {
    MonoGCDeadlockLibPINVOKE.setCallbackToCLR(cb);
  }

  public static void runMonoGCDeadlockTest() {
    MonoGCDeadlockLibPINVOKE.runMonoGCDeadlockTest();
  }

  public static void startNativeWatchdogThread() {
    MonoGCDeadlockLibPINVOKE.startNativeWatchdogThread();
  }

  public static void initDroidBreakpad(string dumpDir) {
    MonoGCDeadlockLibPINVOKE.initDroidBreakpad(dumpDir);
  }

}

}