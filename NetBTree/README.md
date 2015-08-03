# BST

 1. I used a simple monitor because it performed the best over all. The way it is used is a new lock for each node. When making a chnage to the tree structure, such as a delete, I would lock the parent node and the child node. This prevents any new actions being permitted down the lock chain, but it still allows any pending operations futher up or down to be completed.
 
 2. Essentially my tree will be single threaded near the top as each insert, contains and delete starts from root. This can cause a serialized effect when doing multiple operations in a tight loop; however this is over come once we move down the tree into the child nodes. From here concurrency will scale out to the number of processors available. This "hotspot" is unavoidable when starting from the root node each time unless I was willing to accept dirty reads. Or I could implement skip lists that would let me seek a range before performing any operations. Along with this a balanced tree provides the best performance for all operations since the locks are evenly distributed.
 
 
