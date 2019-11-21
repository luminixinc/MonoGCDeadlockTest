using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;

namespace MonoGCDeadlockTest
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        public static readonly string TAG = "DEADLOCK-TEST";

        public static Context AppContext { get; private set; }

        // HACK to force loading libBreakpad.Droid.so before MonoGCDeadlockLib -- work around linker problems on problematic devices (ahem, LG Vista 2, etc...)
        [System.Runtime.InteropServices.DllImport("Breakpad.Droid.so", EntryPoint = "droidBreakpadDLLImportEntryPoint")]
        public static extern void PreLoadBreakpad();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            // help out potentially broken Android dyanamic linker ...
            PreLoadBreakpad();

            // Kick off the deadlock test ...
            bool isRecreated = AppContext != null;
            if (!isRecreated)
            {
                //IntPtr jnienv = Android.Runtime.JNIEnv.Handle;

                // Make sure Breakpad is initialized and ready to go ...
                try
                {
                }
                finally
                {
                    Mono.Runtime.RemoveSignalHandlers();
                    try
                    {
                        MonoGCDeadlockLib.MonoGCDeadlockLib.initDroidBreakpad("/sdcard");
                    }
                    finally
                    {
                        Mono.Runtime.InstallSignalHandlers();
                    }
                }

                // Ask for external storage permission so that Google Breakpad can dump there (minidump is accessible for release builds)
                // NOTE: if the deadlock occurs too quickly, just go to the Android Settings app and manually give this app permission ;)
                GetStoragePermissions();

                // Start the native watchdog thread which doesn't get deadlocked when Mono does ...
                MonoGCDeadlockLib.MonoGCDeadlockLib.startNativeWatchdogThread();

                // Setup callback from C++ ...
                _delegateCB = new MonoGCDeadlockLib.CallbackToCLRDelegate(callbackFromCPP);
                MonoGCDeadlockLib.MonoGCDeadlockLib.setCallbackToCLR(_delegateCB);

                // Kick off the test x4 ...
                MonoGCDeadlockLib.MonoGCDeadlockLib.runMonoGCDeadlockTest();
                MonoGCDeadlockLib.MonoGCDeadlockLib.runMonoGCDeadlockTest();
                MonoGCDeadlockLib.MonoGCDeadlockLib.runMonoGCDeadlockTest();
                MonoGCDeadlockLib.MonoGCDeadlockLib.runMonoGCDeadlockTest();

                // Kick off various worker tasks ...
                // (It is unclear to me whether this actually helps the race or not)
                for (int i = 0; i < 32; i++)
                {
                    Task.Run(async () =>
                    {
                        while (true)
                        {
                            // churn ...
                            int count = _leakList.Count;
                            for (int idx = 0; idx < count; idx++)
                            {
                                --_leakList[idx]._boxed;
                            }

                            // sleep ...
                            int millis = new Random().Next(2000);
                            await Task.Delay(millis);
                        }
                    });
                }

                // Kick off a thread that pumps the GC ...
                // (It is unclear to me whether this actually helps the race or not)
                var t = Task.Run(async () =>
                {
                    while (true)
                    {
                        int millis = new Random().Next(500);
                        await Task.Delay(millis);
                        //Log.Info(TAG, $"FORCE GC thread ... {_pressure.GetHashCode()}");
                        GC.Collect();
                    }
                });

            }
            AppContext = ApplicationContext;
        }

        #region Native bridge GC deadlock test
        private MonoGCDeadlockLib.CallbackToCLRDelegate _delegateCB;
        private List<BoxedInt> _leakList = new List<BoxedInt>();
        private static DataWithFinalizer[] _pressure = new DataWithFinalizer[1] { null };
        private static int _cbCount = 0;

        class BoxedInt
        {
            public int _boxed;
            public BoxedInt(int boxed) { _boxed = boxed; }
        }

        private void callbackFromCPP(string mesg)
        {
            callbackAsyncPortion(mesg, async () =>
            {
                await Task.Delay(100);
                Log.Info(TAG, $"CLR postprocess #{_cbCount} ...");
            });
        }

        private async void callbackAsyncPortion(string mesg, Action postProcess)
        {
            Log.Info(TAG, $"{mesg} CLR callbackFromCPP #{_cbCount} {_leakList.Count} ...");
            ++_cbCount;

            _pressure = new DataWithFinalizer[128];
            for (int i = 0; i < 128; i++) // * count (128) == 128MB pressure
            {
                _pressure[i] = new DataWithFinalizer();
            }

            // Keep these alive to give the GC moar things to iterate through ...
            if (_leakList.Count < 1024 * 1024 * 20)
            {
                for (int i = 0; i < 1024; i++)
                {
                    _leakList.Add(new BoxedInt(_leakList.Count + i));
                }
            }

            if ((_cbCount & 0x7) == 0x0)
            {
                // Avoid potential deep stacktrace: every 8th, take a breather and restart from new Task
                await Task.Run(async () =>
                {
                    var tcs = new TaskCompletionSource<bool>();
                    RunOnUiThread(async () =>
                    {
                        MonoGCDeadlockLib.MonoGCDeadlockLib.runMonoGCDeadlockTest();
                        tcs.SetResult(true);
                        await Task.Delay(1000);
                    });
                    await tcs.Task;
                });
            }
            else
            {
                MonoGCDeadlockLib.MonoGCDeadlockLib.runMonoGCDeadlockTest();
            }

            postProcess();
        }
        #endregion

        #region Permissions
        void GetStoragePermissions()
        {
            if ((CheckSelfPermission(Manifest.Permission.ReadExternalStorage) == (int)Permission.Granted) &&
                (CheckSelfPermission(Manifest.Permission.WriteExternalStorage) == (int)Permission.Granted))
            {
                return;
            }

            const int requestId = 0;
            var perms = new string[]
            {
                Manifest.Permission.ReadExternalStorage,
                Manifest.Permission.WriteExternalStorage
            };

            if (ShouldShowRequestPermissionRationale(Manifest.Permission.ReadExternalStorage) ||
                ShouldShowRequestPermissionRationale(Manifest.Permission.WriteExternalStorage))
            {
                var layout = FindViewById(Resource.Layout.activity_main);
                Snackbar.Make(layout, "Access to /sdcard is needed for Breakpad dumping.", Snackbar.LengthIndefinite)
                        .SetAction("OK", v => RequestPermissions(perms, requestId))
                        .Show();
                return;
            }

            RequestPermissions(perms, requestId);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
        #endregion

        #region Options menu UNUSED
        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }
        #endregion
    }
}

