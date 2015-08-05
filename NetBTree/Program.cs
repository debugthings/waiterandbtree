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
            if (parentOfNodeToDelete != null)
                parentOfNodeToDelete.srwlck.EnterWrite();
            if (nodeToDelete == null)
            {
                parentOfNodeToDelete.srwlck.LeaveWrite();
                return false;
            }
            // Lock above found node as we will alter parent.
            nodeToDelete.srwlck.EnterWrite();
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
                    srch.srwlck.EnterRead();
                    if (prev != null)
                        prev.srwlck.LeaveRead();
                    prev = srch;
                    srch = srch.left;
                }
                nodeToDelete.value = srch.value;
                if (prev != null)
                {
                    prev.left = null;
                    prev.srwlck.LeaveRead();
                }
                else
                {
                    nodeToDelete.right = srch.right;
                }
            }
            nodeToDelete.srwlck.LeaveWrite();
            if (parentOfNodeToDelete != null)
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
        static object swlock = new object();
        static void Main(string[] args)
        {
            for (int i = 0; i < 20; i++)
            {

                var rand = new Random();
                LockHelper lockhelp = new MonitorLockHelper();

                var sw = new System.Diagnostics.Stopwatch();

                int[] range = Enumerable.Range(1, (int)Math.Pow(2.0, 23)).ToArray();
                //sw.Restart();
                //var btTestNoLookupSTRand = new btree(lockhelp);
                //RandomRangeInsert(btTestNoLookupSTRand, range, sw, true);
                //sw.Stop();
                //Console.WriteLine("Random insert single threaded time {0}ms for {1} nodes", sw.Elapsed.TotalMilliseconds, (int)Math.Pow(2.0, 24));

                //sw.Restart();
                //var btTestNoLookupRand = new btree(lockhelp);
                //RandomRangeInsert(btTestNoLookupRand, range, sw);
                //Task.WaitAll(wh.ToArray());
                //sw.Stop();
                //wh.Clear();
                //Console.WriteLine("Random insert multi threaded time {0}ms for {1} nodes", sw.Elapsed.TotalMilliseconds, (int)Math.Pow(2.0, 24));

                //sw.Restart();
                //var btTestNoLookupsST = new btree(lockhelp);
                //BalancedRangeInsert(btTestNoLookupsST, range, sw, true);
                //sw.Stop();
                //Console.WriteLine("Balanced insert single threaded time {0}ms for {1} nodes", sw.Elapsed.TotalMilliseconds, (int)Math.Pow(2.0, 24));


                //sw.Restart();
                //var btTestNoLookups = new btree(lockhelp);
                //BalancedRangeInsert(btTestNoLookups, range, sw);
                //Task.WaitAll(wh.ToArray());
                //sw.Stop();
                //wh.Clear();
                //Console.WriteLine("Balanced insert multi threaded time {0}ms for {1} nodes", sw.Elapsed.TotalMilliseconds, (int)Math.Pow(2.0, 24));

                sw.Restart();
                var bttest = new btree(lockhelp);
                BalancedRangeInsert(bttest, range, sw);

                var cts = new System.Threading.CancellationTokenSource();
                var t = new Task(() =>
                {
                    bool contains = true;
                    while (!cts.IsCancellationRequested)
                    {
                        contains &= bttest.Contains(rand.Next(1, (int)Math.Pow(2.0, 22)));
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
            }
            Console.ReadLine();
        }

        static void BalancedRangeInsert(btree bt, int[] range, System.Diagnostics.Stopwatch sw, bool recursv = false)
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


                for (int i = 1, cnt = 1; cnt <= constthread; cnt++)
                {
                    int start = i;
                    int end = (range.Length / constthread) * cnt;
                    i = end;
                    var t = new Task(() =>
                    {
                        int[] rng = Enumerable.Range(start, end - start).ToArray();
                        BalancedRangeInsert(bt, rng, sw, true);
                    });
                    t.Start();
                    wh.Add(t);
                }

            }
            else
            {
                var sw2 = new System.Diagnostics.Stopwatch();
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
                            sw2.Start();
                            bt.Insert(range[j]);
                            sw2.Stop();
                        }
                    }
                    //Console.WriteLine("Time taken to balance insert {0} nodes: {1}ms", range.Length, sw2.Elapsed.TotalMilliseconds);
                }


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

        static void RandomRangeInsert(btree bt, int[] range, System.Diagnostics.Stopwatch sw, bool recursv = false)
        {
            int constthread = 4;
            if (range.Length > (2 ^ 8) && !recursv)
            {
                for (int i = 1, cnt = 1; cnt <= constthread; cnt++)
                {
                    int start = i;
                    int end = (range.Length / constthread) * cnt;
                    i = end;
                    var t = new Task(() =>
                    {
                        int[] rng = Enumerable.Range(start, end - start).ToArray();
                        RandomRangeInsert(bt, rng, sw, true);
                    });
                    t.Start();
                    wh.Add(t);
                }

            }
            else
            {
                var sw2 = new System.Diagnostics.Stopwatch();
                var rand = new Random();
                for (int j = 0; j < range.Length; j++)
                {
                    int rnd = rand.Next(range.First(), range.Last());
                    sw2.Start();
                    bt.Insert(rnd);
                    sw2.Stop();
                }
                //Console.WriteLine("Time taken to randomly insert {0} nodes: {1}ms", range.Length, sw2.Elapsed.TotalMilliseconds);
            }

        }
    }
}


