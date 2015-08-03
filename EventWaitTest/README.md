##Event Broadcast
 1. The solution appears to be scalable up to any number of waiters and “pulsers”. However, there is an upper bound. Right now I have each thread waking up ever 1ms to check it's status. This is inefficient at the user level  and would be better served by an event/signal mechanism. There is a good amount of contention against the monitor, so if we were to release thousands upon thousands of these callbacks it would only get worse.
 
 2. Depending on how things are JITted it's possible a cached version of a variable could be used when passing it across the thread boundaries. This is easily fixed by adding more locking or using a volatile variable.
 
 3. Right now there is simple fairness based on the distribution of time. But, there is no guarantee the application will release the threads in order, nor will it guarantee a higher priority thread will get picked first. A simple way to change this are using a priority queue for fairness. This would allow us to move higher priority threads to the top  
