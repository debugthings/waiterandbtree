using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NETBTree
{

    interface LockHelper
    {
        void EnterRead();
        void EnterWrite();
        void LeaveRead();
        void LeaveWrite();

        LockHelper CreateNew();
    }

    class SRWLockHelper : LockHelper
    {
        private System.Threading.ReaderWriterLockSlim locktype;
        public SRWLockHelper()
        {
            locktype = new System.Threading.ReaderWriterLockSlim(System.Threading.LockRecursionPolicy.SupportsRecursion);
        }
        public void EnterRead()
        {
            locktype.EnterReadLock();
        }

        public void EnterWrite()
        {
            locktype.EnterWriteLock();
        }

        public void LeaveRead()
        {
            locktype.ExitReadLock();
        }

        public void LeaveWrite()
        {
            locktype.ExitWriteLock();
        }

        public LockHelper CreateNew()
        {
            return new SRWLockHelper();
        }
    }

    class MonitorLockHelper : LockHelper
    {
        private object locktype;
        private bool shareslock = false;
        public MonitorLockHelper()
        {
            locktype = new object();
        }

        public MonitorLockHelper(bool sharelock) : this()
        {
            shareslock = sharelock;
        }

        public void EnterRead()
        {
            System.Threading.Monitor.Enter(locktype);
        }

        public void EnterWrite()
        {
            System.Threading.Monitor.Enter(locktype);
        }

        public void LeaveRead()
        {
            System.Threading.Monitor.Exit(locktype);

        }

        public void LeaveWrite()
        {
            System.Threading.Monitor.Exit(locktype);
        }

        public LockHelper CreateNew()
        {
            if (!shareslock)
            {
                return new MonitorLockHelper();
            }
            return this;
        }
    }

    class MutexLockHelper : LockHelper
    {
        System.Threading.Mutex mtx;
        public MutexLockHelper()
        {
            mtx = new System.Threading.Mutex();
        }
        public void EnterRead()
        {
            mtx.WaitOne();
        }

        public void EnterWrite()
        {
            mtx.WaitOne();
        }

        public void LeaveRead()
        {
            mtx.ReleaseMutex();
        }

        public void LeaveWrite()
        {
            mtx.ReleaseMutex();
        }

        public LockHelper CreateNew()
        {
            return new MutexLockHelper();
        }
    }

    class btree
    {
        LockHelper srwlck;
        volatile int insertions = 0;
        public btreenode root;

        public btree(LockHelper lockhelper)
        {
            srwlck = lockhelper;
        }
        public bool Insert(int val)
        {
            if (root == null)
            {
                root = new btreenode(val, srwlck.CreateNew());
            }
            btreenode btn = root;
            LockHelper prevLock = null;
            try
            {
                while (true)
                {
                    btn.srwlck.EnterWrite();
                    prevLock = btn.srwlck;
                    if (btn.value == val)
                    {
                        return true;
                    }
                    if (val > btn.value)
                    {
                        if (btn.right != null)
                        {
                            btn = btn.right;
                        }
                        else
                        {
                            btn.right = new btreenode(val, srwlck.CreateNew());
                            return true;
                        }
                    }
                    else if (val < btn.value)
                    {
                        if (btn.left != null)
                        {
                            btn = btn.left;
                        }
                        else
                        {
                            btn.left = new btreenode(val, srwlck.CreateNew());
                            return true;
                        }
                    }
                    prevLock.LeaveWrite();

                }
            }
            finally
            {
                prevLock.LeaveWrite();
            }
        }
        public bool Contains(int val)
        {
            btreenode nv = null;
            if (FindNodeAndParent(val, this.root, out nv) != null)
            {
                return true;
            }
            return false;
        }
        public bool Remove(int val)
        {
            btreenode parentOfNodeToDelete = null;
            btreenode nodeToDelete = FindNodeAndParent(val, this.root, out parentOfNodeToDelete);
            parentOfNodeToDelete.srwlck.EnterWrite();
            if (nodeToDelete == null)
            {
                parentOfNodeToDelete.srwlck.LeaveWrite();
                return false;
            }
            // Lock above found node as we will alter parent.

            if (nodeToDelete.left == null && nodeToDelete.right == null)
            {
                if (parentOfNodeToDelete.left == nodeToDelete)
                    parentOfNodeToDelete.left = null;
                else
                    parentOfNodeToDelete.right = null;
            }
            else if (nodeToDelete.right != null && nodeToDelete.left == null)
            {
                if (parentOfNodeToDelete.left == nodeToDelete)
                    parentOfNodeToDelete.left = nodeToDelete.right;
                else
                    parentOfNodeToDelete.right = nodeToDelete.right;
            }
            else if (nodeToDelete.left != null && nodeToDelete.right == null)
            {
                if (parentOfNodeToDelete.left == nodeToDelete)
                    parentOfNodeToDelete.left = nodeToDelete.left;
                else
                    parentOfNodeToDelete.right = nodeToDelete.left;
            }
            else if (nodeToDelete.left != null && nodeToDelete.right != null)
            {
                btreenode srch = nodeToDelete.right;
                btreenode prev = null;
                while (srch.left != null)
                {
                    prev = srch;
                    srch = srch.left;
                }
                nodeToDelete.value = srch.value;
                if (prev != null)
                {
                    prev.left = null;
                }
                else
                {
                    nodeToDelete.right = srch.right;
                }
            }
            parentOfNodeToDelete.srwlck.LeaveWrite();
            return true;
        }
        private btreenode FindNodeAndParent(int val, btreenode btn, out btreenode parent)
        {
            btn.srwlck.EnterRead();
            try
            {
                parent = null;
                while (btn != null)
                {

                    if (btn.value == val)
                    {
                        return btn;
                    }
                    if (btn.left == null && btn.right == null)
                        return null;
                    if (btn.left == null && val < btn.value)
                        return null;
                    if (btn.right == null && val > btn.value)
                        return null;

                    if (btn.left != null && val < btn.value)
                    {
                        parent = btn;
                        btn = btn.left;
                        parent.srwlck.LeaveRead();
                        btn.srwlck.EnterRead();
                    }
                    else if (btn.right != null && val > btn.value)
                    {
                        parent = btn;
                        btn = btn.right;
                        parent.srwlck.LeaveRead();
                        btn.srwlck.EnterRead();
                    }
                }
            }
            finally
            {
                btn.srwlck.LeaveRead();
            }
            parent = btn;
            return null;
        }
    }

    class btreenode
    {
        public int value;
        public btreenode left;
        public btreenode right;
        public LockHelper srwlck;

        public btreenode(int val, LockHelper lockhelper)
        {
            value = val;
            srwlck = lockhelper;
        }

    }
    class Program
    {
        static System.Collections.Generic.List<Task> wh = new List<Task>();
        static void Main(string[] args)
        {
            var rand = new Random();
            LockHelper lockhelp = new MonitorLockHelper();

            var sw = new System.Diagnostics.Stopwatch();

            sw.Restart();
            var btTestNoLookupsST = new btree(lockhelp);
            BalancedRangeInsert(btTestNoLookupsST, Enumerable.Range(1, (int)Math.Pow(2.0, 23)).ToArray(), true);
            sw.Stop();
            Console.WriteLine("Balanced insert single threaded time {0}ms for {1} nodes", sw.Elapsed.TotalMilliseconds, (int)Math.Pow(2.0, 24));


            sw.Restart();
            var btTestNoLookups = new btree(lockhelp);
            BalancedRangeInsert(btTestNoLookups, Enumerable.Range(1, (int)Math.Pow(2.0, 23)).ToArray());
            Task.WaitAll(wh.ToArray());
            sw.Stop();
            wh.Clear();
            Console.WriteLine("Balanced insert multi threaded time {0}ms for {1} nodes", sw.Elapsed.TotalMilliseconds, (int)Math.Pow(2.0, 24));

            sw.Restart();
            var bttest = new btree(lockhelp);
            BalancedRangeInsert(bttest, Enumerable.Range(1, (int)Math.Pow(2.0, 23)).ToArray());

            var cts = new System.Threading.CancellationTokenSource();
            var t = new Task(() =>
            {
                while (!cts.IsCancellationRequested)
                {
                    bttest.Contains(rand.Next(1, (int)Math.Pow(2.0, 22)));
                    System.Threading.Thread.Sleep(1);
                }
            }, cts.Token);

            var t2 = new Task(() =>
            {
                while (!cts.IsCancellationRequested)
                {
                    bttest.Remove(rand.Next(1, (int)Math.Pow(2.0, 22)));
                    System.Threading.Thread.Sleep(1);
                }
            }, cts.Token);
            t2.Start();
            Task.WaitAll(wh.ToArray());
            sw.Stop();
            cts.Cancel();
            Console.WriteLine("Balanced insert multi threaded, w/ lookup and removal time: {0}", sw.Elapsed.TotalMilliseconds);

            Console.ReadLine();

        }

        static void BalancedRangeInsert(btree bt, int[] range, bool recursv = false)
        {
            int constthread = 4;
            if (range.Length > (2 ^ 8) && !recursv)
            {
                for (int i = 2, count = 1; count < 5; i *= 2, count++)
                {
                    for (int j = (range.Length / i); j < range.Length; j += (range.Length / i))
                    {
                        bt.Insert(range[j]);
                    }
                }


                for (int i = 1, cnt = 1; cnt <= constthread; i += (range.Length / constthread), cnt++)
                {
                    int start = i;
                    int end = (range.Length / constthread) * cnt;
                    var t = new Task(() =>
                    {
                        int[] rng = Enumerable.Range(start, end).ToArray();
                        BalancedRangeInsert(bt, rng, true);
                        Console.WriteLine("Balanced insert of {0} to {1} complete.", start, end);
                    });
                    t.Start();
                    wh.Add(t);
                }

            }
            else
            {
                for (int i = 2; i < range.Length; i *= 2)
                {
                    for (int j = (range.Length / i); j < range.Length; j += (range.Length / i))
                    {
                        int skip = 0;
                        if (i > 2)
                        {
                            // Skip the previous round
                            skip = (range.Length / (i / 2));
                        }
                        if (j != skip)
                        {
                            bt.Insert(range[j]);
                        }

                    }
                }

                //for (int i = 1; i < range.Length; i++)
                //{
                //    bt.Insert(range[i]);
                //}
            }

        }

        static void UnBalancedRangeInsert(btree bt, int[] range, bool recursv = false)
        {
            int constthread = 4;
            if (range.Length > (2 ^ 12) && !recursv)
            {
                for (int i = 2, count = 1; count < 5; i *= 2, count++)
                {
                    for (int j = (range.Length / i); j < range.Length; j += (range.Length / i))
                    {
                        bt.Insert(range[j]);
                    }
                }
                System.Collections.Generic.List<Task> wh = new List<Task>();

                for (int i = 1, cnt = 1; cnt <= constthread; i += (range.Length / constthread), cnt++)
                {
                    int start = i;
                    int end = (range.Length / constthread) * cnt;
                    var t = new Task(() =>
                    {
                        int[] rng = Enumerable.Range(start, end).ToArray();
                        UnBalancedRangeInsert(bt, rng, true);
                    });
                    t.Start();
                    wh.Add(t);
                }
                //Task.WaitAll(wh.ToArray());
            }
            else
            {
                for (int i = 1; i < range.Length; i++)
                {
                    bt.Insert(range[i]);
                }
            }
        }

    }
}


