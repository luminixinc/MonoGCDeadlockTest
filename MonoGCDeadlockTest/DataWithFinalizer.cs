using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoGCDeadlockTest
{
    public class DataWithFinalizer : IDisposable
    {
        private static int count = 1024 * 256;
        private int[] oneMBInts = new int[count]; // * 4 == 1MB

        public DataWithFinalizer()
        {
            // populate data on pages to foil C-o-W ...
            for (int i = 0; i < count; i += 1024)
            {
                oneMBInts[i + 0  ] = i;
                oneMBInts[i + 256] = i;
                oneMBInts[i + 512] = i;
                oneMBInts[i + 768] = i;
            }
        }

        ~DataWithFinalizer()
        {
            Dispose(false);
        }

        public virtual void Dispose()
        {
            Dispose(true);
        }

        //private object _disposeLock = new object();
        private bool _isDisposed;
        public void Dispose(bool disposing)
        {
            //lock (this) -- unnecessary, dangerous, and a red herring in this demo!
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
            }

            if (disposing)
            {
                // do something specific for calling Dispose() ...
            }
            else
            {
                // this is somewhat contrived, but imagine calling across the SWIG bridge to C++ to free() malloc()d memory ...
                oneMBInts = null;
            }
        }
    }
}