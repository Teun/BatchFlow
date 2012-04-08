using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BatchFlow;
using System.Net;
using System.Xml;
using System.Web;
using System.Text.RegularExpressions;
using System.Threading;
using Duynstee.PuzzleSolving;

namespace Samples
{
    class Program
    {
        protected static readonly log4net.ILog log = log4net.LogManager.GetLogger("Samples");
        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            //HelloWorld();
            //CalculatePrimes();
            //FindAssociatedTwitterTags("#dotnet");
            LoggingAndTweakingPerformance();
            //CalculateDifficultSudokusClassic();
            //CalculateDifficultSudokusBatchFlow();
            //Regenwormen.Calculate();
        }

        private static void LoggingAndTweakingPerformance()
        {
            Random rnd = new Random(DateTime.Now.Millisecond);
            StartPoint<int> counter = new StartPoint<int>(
                (IWritableQueue<int> output) =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        output.Send(rnd.Next(1000));
                    }
                }
                );
            TaskNode<int, int> performLengthyTask1 = new TaskNode<int, int>(
                (int input, IWritableQueue<int> output) =>
                {
                    int time = rnd.Next(100);
                    Thread.Sleep(time);
                    output.Send(input);
                }
                );
            TaskNode<int, int> performLengthyTask2 = new TaskNode<int, int>(
                (int input, IWritableQueue<int> output) =>
                {
                    int time = rnd.Next(100);
                    Thread.Sleep(time);
                    output.Send(input);
                }
                );
            EndPoint<int> end = new EndPoint<int>(
                (int result) => Console.WriteLine("Received: {0}", result));


            Flow f = Flow.FromAsciiArt("a-->b-->c-->d",
                counter, performLengthyTask1, performLengthyTask2, end);

            end.ItemProcessed += (sender, e) =>
                {
                    if (end.ItemsProcessed % 20 == 0)
                    {
                        Console.WriteLine("Processed {0} items {1:0.##}%", 
                            end.ItemsProcessed, 
                            end.ItemsProcessed * 100/1000);

                        log.InfoFormat("State:\n{0}", f.GetStateSnapshot().ToStringAsciiArt());
                    }
                };
            
            
            DateTime start = DateTime.Now;
            f.Start();
            f.RunToCompletion();
            Console.WriteLine("1000 items processed in {0:0.##} seconds", (DateTime.Now - start).TotalSeconds);

        }

        private static void CalculateDifficultSudokusBatchFlow()
        {
            string flow = "a-->b-->c"; /*
                                        * a = generate solutions
                                        * b = strip as much numbers as possible
                                        * c = check if hard enough, print and collect
                                        * */

            StartPoint<string> generateSolutions = new StartPoint<string>(
                (IWritableQueue<string> output) =>
                {
                    string solution = @"
                                            |314562798|
                                            |685973421|
                                            |927841563|
                                            |248396157|
                                            |531724986|
                                            |769158234|
                                            |873215649|
                                            |492637815|
                                            |156489372|
                    ";
                    while (true)
                    {
                        solution = Utilities.ChangeSolution(solution); // create a new solution by shuffling
                        output.Send(solution);
                    }
                }
                );
            TaskNode<string, Tuple<string, SolveResult>> stripNumbers = new TaskNode<string, Tuple<string, SolveResult>>(
                (string input, IWritableQueue<Tuple<string, SolveResult>> output) =>
                {
                    PuzzleGrid puzzle = null;
                    SolveResult result = null;
                    string problem = input;
                    // remove 50 out of 81 random digits
                    for (int i = 0; i < 50; i++)
                    {
                        string tempProblem = RemoveRandomNumber(problem);
                        puzzle = new PuzzleGrid();
                        Utilities.SetupBasicSudokuGrid(puzzle, 3);
                        puzzle.SetValues(problem);
                        SolveResult tempresult = puzzle.Solve();
                        if (tempresult.Solved && tempresult.Valid)
                        {
                            // still solvable
                            problem = tempProblem;
                            result = tempresult;

                        }
                    }
                    output.Send(new Tuple<string,SolveResult>(problem, result));
                }
                );

            OutputPoint<Tuple<string,SolveResult>> outputPoint = new OutputPoint<Tuple<string,SolveResult>>();
            Flow f = Flow.FromAsciiArt(flow, generateSolutions, stripNumbers, outputPoint);
            f.Start();

            for (int i = 0; i < 10; i++)
			{
                Tuple<string,SolveResult> problem = outputPoint.Output.Receive();
                if(problem.Second.StepsUsed > 60)
                {
                    Console.WriteLine("Found:\n{0}\nSolvable in {1} steps", problem.First, problem.Second.StepsUsed);
                }
			}
            f.Stop();
        }

        #region sudoku
        private static void CalculateDifficultSudokusClassic()
        {
            string solution = @"
                                            |314562798|
                                            |685973421|
                                            |927841563|
                                            |248396157|
                                            |531724986|
                                            |769158234|
                                            |873215649|
                                            |492637815|
                                            |156489372|
                    ";

            List<PuzzleGrid> hardPuzzles = new List<PuzzleGrid>();
            while (hardPuzzles.Count < 10)
            {
                solution = Utilities.ChangeSolution(solution); // create a new solution by shuffling
                PuzzleGrid puzzle = null;
                SolveResult result = null;
                string problem = solution;
                // remove 50 out of 81 random digits
                for (int i = 0; i < 50; i++)
                {
                    string tempProblem = RemoveRandomNumber(problem);
                    puzzle = new PuzzleGrid();
                    Utilities.SetupBasicSudokuGrid(puzzle, 3);
                    puzzle.SetValues(problem);
                    SolveResult tempresult = puzzle.Solve();
                    if (tempresult.Solved && tempresult.Valid)
                    {
                        // still solvable
                        problem = tempProblem;
                        result = tempresult;
                        
                    }
                }
                if (result.Solved && result.StepsUsed > 60 )
                {
                    hardPuzzles.Add(puzzle);
                    Console.WriteLine("Found:\n{0}\nSolvable in {1} steps", problem, result.StepsUsed);
                }
            }

        }
        private static Regex _anyDigit = new Regex("\\d");
        static Random rnd = new Random();
        private static string RemoveRandomNumber(string currentProblem)
        {
            MatchCollection remainingDigits = _anyDigit.Matches(currentProblem);
            int loc = rnd.Next(remainingDigits.Count);
            Match digit = remainingDigits[loc];
            return currentProblem.Substring(0, digit.Index) + " " + currentProblem.Substring(digit.Index + 1);
        }
        public class Tuple<T>
        {
            public Tuple(T first)
            {
                First = first;
            }

            public T First { get; set; }
        }

        public class Tuple<T, T2> : Tuple<T>
        {
            public Tuple(T first, T2 second)
                : base(first)
            {
                Second = second;
            }

            public T2 Second { get; set; }
        }
        #endregion


        #region twitter
        private static void FindAssociatedTwitterTags(string term)
        {
            /*
             * 1. Get a fixed number of tweets from the search API using some search term
             * 2. Filter out the user names and write to stream
             * 3. fetch the last 100 tweets for each user
             * 4. Filter out used HashTags
             * 5. Count them 
             * */
            StartPoint<string> getNames = new StartPoint<string>(
                (IWritableQueue<string> output) =>
                {
                    string url = "http://search.twitter.com/search.atom?q=" + HttpUtility.UrlEncode(term) + "&rpp=100";
                    XmlDocument doc = new XmlDocument();
                    doc.Load(url);

                    XmlNodeList authors = doc.SelectNodes("/atom:feed/atom:entry/atom:author/atom:uri", NsMgr);
                    foreach (XmlElement node in authors)
                    {
                        if (node.InnerText.StartsWith("http://twitter.com"))
                        {
                            output.Send(node.InnerText.Substring(19));
                        }
                    }
                }
                );
            TaskNode<string, string> getTagsForAccount = new TaskNode<string, string>(
                (string input, IWritableQueue<string> output) =>
                {
                    
                    string url = "http://search.twitter.com/search.atom?q=" + HttpUtility.UrlEncode("from:" + input) + "&rpp=100";
                    XmlDocument doc = new XmlDocument();
                    doc.Load(url);

                    XmlNodeList tweets = doc.SelectNodes("/atom:feed/atom:entry/atom:title", NsMgr);
                    foreach (XmlElement item in tweets)
                    {
                        string tweet = item.InnerText;
                        foreach (string tag in TagsFromText(tweet))
                        {
                            output.Send(tag);
                            Console.WriteLine("{0}: {1}", input, tag);
                        }
                    }
                }
                ) { ThreadNumber = 10 };
            DistinctCollector<string> collect = new DistinctCollector<string>();
            Flow f = Flow.FromAsciiArt("a->b-->c-->d", getNames, StandardTasks.GetUniqueFilter<string>(), getTagsForAccount, collect);
            f.Start();
            f.RunToCompletion();
            foreach (var item in collect.Totals.OrderBy(o=> -o.Value).Take(20))
            {
                Console.WriteLine("{0:##0} - {1}", item.Value, item.Key);
            }
        }

        private static Regex hashTag = new Regex("\\B#\\w+\\b", RegexOptions.Compiled | RegexOptions.Singleline);
        private static IEnumerable<string> TagsFromText(string tweet)
        {
            foreach (Match m in hashTag.Matches(tweet))
            {
                yield return m.Value;
            }

        }

        private static XmlNamespaceManager _nsmgr;
        private static XmlNamespaceManager NsMgr
        {
            get
            {
                if (_nsmgr == null)
                {
                    XmlDocument d = new XmlDocument();
                    XmlNamespaceManager nsmgr = new XmlNamespaceManager(d.NameTable);
                    nsmgr.AddNamespace("atom", "http://www.w3.org/2005/Atom");
                    nsmgr.AddNamespace("twitter", "http://api.twitter.com/");
                    _nsmgr = nsmgr;
                }
                return _nsmgr;
            }
        }
        #endregion

        #region primes

        private static void CalculatePrimes()
        {
            StartPoint<int> counter = new StartPoint<int>(
                (IWritableQueue<int> output) =>
                {
                    int i = 1000;
                    while (true)
                    {
                        output.Send(i);
                        i++;
                    }
                }
                );
            TaskNode<int, int> checkPrime = new TaskNode<int, int>(
                (int input, IWritableQueue<int> output) =>
                {
                    for (int div = 2; div < Math.Sqrt(input); div++)
                    {
                        if (input % div == 0) return;
                    }
                    output.Send(input);
                }
                ) { ThreadNumber = 2 };
            
            OutputPoint<int> outPoint = new OutputPoint<int>();
            Flow f = Flow.FromAsciiArt("c<--b<--a",
                new Dictionary<char, TaskNode>() {{'a', counter }, {'b', checkPrime} , {'c', outPoint}});

            checkPrime.ItemProcessed += (object o, TaskNode.ItemEventArgs e) =>
            {
                if (checkPrime.ItemsProcessed % 100 == 0)
                {
                    string ascii = f.GetStateSnapshot().ToString();
                    Console.WriteLine(ascii);
                }

            };


            f.Start();
            //f.RunToCompletion(); never do this here
            
            // Now we pull out as many results as we need
            for (int count = 0; count < 1000; count++)
            {
                //if (count % 100 == 0)
                //{
                //    string ascii = f.GetStateSnapshot().ToStringAsciiArt();
                //    Console.SetCursorPosition(0,0);
                //    Console.WriteLine(ascii);
                //}
                outPoint.Output.Receive();
                Console.WriteLine("Prime number {0}: {1}", count + 1, outPoint.Output.Receive());
            }

        }
        #endregion

        #region HelloWorld

        private static void HelloWorld()
        {
            StartPoint<string> start = new StartPoint<string>(
                (IWritableQueue<string> output) => {output.Send("Hello world!");}
                );

            EndPoint<string> end = new EndPoint<string>(
                (string input) => { Console.WriteLine(input); }
                );

            Flow f = Flow.FromAsciiArt("a-->b", new Dictionary<char, TaskNode>() { {'a', start }, {'b', end} });
            f.Start();
            f.RunToCompletion();

        }
        #endregion
    }
}
