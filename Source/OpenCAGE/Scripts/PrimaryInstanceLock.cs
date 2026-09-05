using System.Threading;

namespace OpenCAGE
{
    /// <summary>
    /// Which OpenCAGE process is the primary one - the one that owns Manage Game Directories and the
    /// settings the others are launched from. Held as a named mutex for the life of the process rather
    /// than decided once at startup, so that when the primary goes away (closed first, or crashed - the
    /// OS releases the mutex either way) a child instance can take the role over and manage the
    /// directories itself, instead of the user having to close everything and relaunch.
    /// </summary>
    public static class PrimaryInstanceLock
    {
        private const string MutexName = @"Local\OpenCAGE_PrimaryInstance";
        private static Mutex _mutex;

        /// <summary>Take the primary role if nobody holds it. True once held, including on repeat calls.</summary>
        public static bool TryAcquire()
        {
            if (_mutex != null)
                return true;

            Mutex mutex = new Mutex(false, MutexName);
            bool acquired;
            try
            {
                acquired = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                acquired = true; //the last holder died without releasing it, which makes it ours
            }

            if (!acquired)
            {
                mutex.Dispose();
                return false;
            }

            _mutex = mutex;
            Singleton.IsPrimaryInstance = true;
            return true;
        }
    }
}
