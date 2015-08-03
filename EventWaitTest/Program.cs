using System;

namespace EventWithCallbackDemo
{
    class EventWithCallback
    {
        private System.Object lockobj = new System.Object();
        private System.Collections.Generic.Dictionary<System.Threading.Thread, bool> tlist = 
            new System.Collections.Generic.Dictionary<System.Threading.Thread, bool>();
        Action<object> cb;
        public EventWithCallback()
        {
            
        }
        public void Wait(object arg)
        {
            System.Threading.Monitor.Enter(lockobj);
            tlist.Add(System.Threading.Thread.CurrentThread, false);
            System.Threading.Monitor.Exit(lockobj);

            while (true)
            {
                System.Threading.Monitor.Enter(lockobj);
                if (tlist[System.Threading.Thread.CurrentThread])
                {
                    tlist.Remove(System.Threading.Thread.CurrentThread);
                    System.Threading.Monitor.Exit(lockobj);
                    break;
                }
                System.Threading.Monitor.Exit(lockobj);
                System.Threading.Thread.Sleep(1); 
            }
            if (cb != null)
            {
                cb(arg);
            }
        }
        public void Pulse(Action<object> callback)
        {
            cb = callback;
            System.Threading.Monitor.Enter(lockobj);
            if (tlist.Count > 0)
            {
                System.Threading.Thread t = null;
                foreach (var item in tlist)
                {
                    t = item.Key;
                    break;
                }
                tlist[t] = true;
            }
            System.Threading.Monitor.Exit(lockobj);

        }
        public void PulseAll(Action<object> callback) {
            cb = callback;
            System.Threading.Monitor.Enter(lockobj);
            if (tlist.Count > 0)
            {
                System.Collections.Generic.List<System.Threading.Thread> t 
                    = new System.Collections.Generic.List<System.Threading.Thread>();
                foreach (var item in tlist)
                {
                    t.Add(item.Key);
                }
                foreach (var thr in t)
                {
                    tlist[thr] = true;
                }
            }
            System.Threading.Monitor.Exit(lockobj);
        }

    }

    class MainClass
    {
        public static void Main(string[] args)
        {

            EventWithCallback ec = new EventWithCallback();
            for (int i = 0; i < 500; i++)
            {
                System.Threading.Thread t = new System.Threading.Thread(new System.Threading.ThreadStart(() =>
                {
                    ec.Wait(String.Format("world. From Thread {0:X}", System.Threading.Thread.CurrentThread.GetHashCode()));

                }));
                t.Start();
            }
           
            ec.Pulse(o => { Console.WriteLine("Hello {0} | Actual Thread {1:X}", o, System.Threading.Thread.CurrentThread.GetHashCode()); });
            
            Console.ReadLine();
            GC.Collect();
            ec.Pulse(o => { Console.WriteLine("Hello {0} | Actual Thread {1:X}", o, System.Threading.Thread.CurrentThread.GetHashCode()); });
            Console.ReadLine();
            GC.Collect();
            ec.Pulse(o => { Console.WriteLine("Hello {0} | Actual Thread {1:X}", o, System.Threading.Thread.CurrentThread.GetHashCode()); });
            Console.ReadLine();
            GC.Collect();
            ec.PulseAll(o => { Console.WriteLine("Pulse all Hello {0} | Actual Thread {1:X}", o, System.Threading.Thread.CurrentThread.GetHashCode()); });
            Console.ReadLine();
            GC.Collect();
        }
    }
}
