BatchFlow is a .NET library that takes care of all the plumbing you need around large batch processing, especially the producer/consumer pattern that you (probably) should use.

Version 0.5, which is released on April 14th, 2012, is reasonably stable. Has been tested with a number of real world applications.

In most environments, some batch processing happens. Importing the new products into the catalog, archiving old conversations, precalculating recommendations for all users, those boring things. These tools tend to be naively simple at the start. The need for multiple processing threads, working with queues instead of loading everything in memory, logging operational information, etc... arises only later and causes for unnecessary complex code. BatchFlow lets you focus on the data processing and handles all of the complex flow stuff.

####Coolest feature
Most of BatchFlow is just plain usefull. There is only one feature that is also cool: ASCII Art flow definition. Like this:

    Flow flow = Flow.FromAsciiArt(@"
              a----+
                   |
                   V
                   b
                   +---->c
       ", ...);


####Now what?

- Check out the wiki, with a tutorial and explanation of how BatchFlow works
- Play with it. You can add BatchFlow to you project using NuGet:
    Install-Package BatchFlow
    
    
