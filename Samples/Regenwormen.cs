using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Samples
{
    public class Regenwormen
    {
        public static void Calculate()
        {
            Dictionary<string, DiceHand> allHands = new Dictionary<string, DiceHand>();
            for (byte i = 8; i > 0; i--)
            {
                var list = DiceHand.GetAllPossibleHands(i);
                foreach (var hand in list)
                {
                    hand.CalcExpectedValue(8, allHands);
                    allHands[hand.ToString()] = hand;
                }
            }
            OutputAll(allHands);
            //Console.ReadLine();
        }

        private static void OutputAll(Dictionary<string, DiceHand> allHands)
        {
            for (int i = 1; i < 5; i++)
            {
                Console.Out.WriteLine("After throw {0}", i);
                Console.Out.WriteLine("-------------\n");
                foreach (var hand in allHands.Values.Where(h => h.CountTakes == i).OrderByDescending(h => h.ToString()))
                {
                    SendHandDetailsToOut(hand, Console.Out);
                }
            }
        }

        private static void SendHandDetailsToOut(DiceHand hand, System.IO.TextWriter textWriter)
        {
            textWriter.WriteLine("{0}: {1}", hand, hand.WouldThrowDice ? "THROW" : "STOP");
            textWriter.WriteLine("  current value : {0}", hand.TotalPoints);
            textWriter.WriteLine("  expected value: {0}", hand.GetExpectedValue());
            textWriter.WriteLine();
        }
    }
    public class DiceHand
    {
        public const byte NR_OF_DICE_SIDES = 6;
        private DiceHand() { _dead = true; }
        public DiceHand(params byte[] values) : this((IList<byte>)values) { }
        public DiceHand(IList<byte> values)
        {
            if(values.Count != NR_OF_DICE_SIDES)
            {
                throw new InvalidOperationException(String.Format("dice have {0} values", NR_OF_DICE_SIDES));
            }
            for (int i = 0; i < values.Count; i++)
			{
                _numbers[i] = values[i];
			}
        }
        public static DiceHand Dead { get { return new DiceHand(); } }
        byte[] _numbers = new byte[NR_OF_DICE_SIDES];
        bool _dead = false;
        public int this[int eyes]
        {
            get
            {
                if (eyes < 1 || eyes > NR_OF_DICE_SIDES)
                {
                    throw new IndexOutOfRangeException("Dice can have 1 up to 6 points");
                }
                return _numbers[eyes - 1];
            }
        }
        public int NumberOfDice
        {
            get
            {
                return _numbers.Sum(b => (int)b);
            }
        }
        public bool IsDead { get { return _dead; } }
        public int TotalPoints
        {
            get
            {
                if (IsDead) return 0;
                if (_numbers[5] == 0) return 0;
                int total = 0;
                for (int i = 0; i < NR_OF_DICE_SIDES - 1; i++)
                {
                    total += _numbers[i] * (i + 1);
                }
                total += _numbers[5] * 5; //exception
                if (total < 21) return 0;
                return total;
            }
        }
        public double BareProbability
        {
            get
            {
                double baseChance = 1 / Math.Pow(NR_OF_DICE_SIDES, NumberOfDice);
                // now calculate the number of permutations for this 
                long nrPermutations = CalcPermutations(_numbers, NumberOfDice);
                return baseChance * nrPermutations;
            }
        }

        private static long CalcPermutations(byte[] _numbers, int NumberOfDice)
        {
            long collect = 1;
            int remainingDice = NumberOfDice;
            foreach (byte number in _numbers)
            {
                collect *= Over(remainingDice, remainingDice - number);
                remainingDice = remainingDice - number;
            }
            return collect;
        }
        private static long Over(int upper, int lower)
        {
            // (upper!)/(lower!)*(upper - lower)!
            return Faculty(upper) / (Faculty(lower) * Faculty(upper - lower));
        }

        private static long Faculty(int n)
        {
            long collect = 1;
            for (int i = 2; i <= n; i++)
            {
                collect *= i;
            }
            return collect;
        }
        public DiceHand Add(Take t)
        {
            byte[] scores = (byte[])_numbers.Clone();
            if (scores[t.DiceScore] == 0)
            {
                scores[t.DiceScore] = t.Number;
            }
            else
            {
                return DiceHand.Dead;
            }
            return new DiceHand(scores);
        }
        private double? _expectedValue = null;
        private Dictionary<int, double> _probableOutcomes = null;
        
        public void CalcExpectedValue(byte nrOfDice, IDictionary<string, DiceHand> allhands)
        {
            if (_probableOutcomes == null)
            {
                _probableOutcomes = new Dictionary<int, double>();
                for (int i = 0; i < 41; i++)
                {
                    _probableOutcomes[i] = 0;
                }
                // Calculate the expected value if we would throw the dice
                if (this.NumberOfDice == nrOfDice)
                {
                    _probableOutcomes[this.TotalPoints] = 1;
                }
                else if (this.NumberOfDice < nrOfDice)
                {
                    foreach (var outcome in DiceHand.GetAllPossibleHands((byte)(nrOfDice - this.NumberOfDice)))
                    {
                        List<Take> takes = new List<Take>(outcome.GetTakes());
                        Take bestTake = takes.OrderByDescending(t => CalcValue(allhands, t)).First();
                        if (this.Add(bestTake).IsDead) continue;
                        DiceHand newHand = allhands[ this.Add(bestTake).ToString()];
                        foreach (var item in newHand.GetExpectedOutcomes())
                        {

                            _probableOutcomes[item.Key] += item.Value * outcome.BareProbability;
                        }
                    }
                }
            }
        }
        public bool WouldThrowDice
        {
            get
            {
                if (_probableOutcomes != null)
                {
                    throw new InvalidOperationException("Don't call WouldThrowDice before GetExpectedValue");
                }
                return (GetExpectedValue() > TotalPoints);
            }
        }

        private double CalcValue(IDictionary<string, DiceHand> allhands, Take t)
        {
            DiceHand newhand = this.Add(t);
            if(newhand.IsDead)return 0;
            return allhands[newhand.ToString()].GetExpectedValue();
        }
        public double GetExpectedValue()
        {
            return ExpectedValueFromExpectedOutcomes();
        }
        public Dictionary<int, double> GetExpectedOutcomes()
        {
            double expectedWhenThrow = ExpectedValueFromExpectedOutcomes();
            if (expectedWhenThrow > TotalPoints)
            {
                return _probableOutcomes;
            }
            else
            {
                return new Dictionary<int, double>() { { this.TotalPoints, 1 } };
            }
        }

        private double ExpectedValueFromExpectedOutcomes()
        {
            double expectedWhenThrow = 0;
            foreach (var item in _probableOutcomes)
            {
                expectedWhenThrow += item.Key * item.Value;
            }
            return expectedWhenThrow;
        }
        public IEnumerable<Take> GetTakes()
        {
            for (int i = 0; i < _numbers.Length; i++)
            {
                if(_numbers[i] > 0)
                    yield return new Take() { DiceScore = (byte)i, Number = _numbers[i] };
            }
        }
        public int CountTakes
        {
            get
            {
                return _numbers.Count(i => i > 0);
            }
        }

        public static IEnumerable<DiceHand> GetAllPossibleHands(byte numberOfDice)
        {
            byte[] values = new byte[6];
            List<DiceHand> fullHands = new List<DiceHand>();
            FillNumberFrom(values, 0, 0, numberOfDice, fullHands);
            return fullHands;
        }

        private static void FillNumberFrom(byte[] values, int position, byte alreadyUsed , byte maxNumber, List<DiceHand> hands)
        {
            for (byte i = 0; i <= maxNumber - alreadyUsed; i++)
            {
                values[position] = i;
                byte currentUsed = (byte)(alreadyUsed + i);
                if (currentUsed == maxNumber)
                {
                    hands.Add(new DiceHand(values.ToArray()));
                }
                else
                {
                    if (currentUsed < maxNumber)
                    {
                        if (position + 1 < values.Length)
                        {
                            FillNumberFrom(values, position + 1, currentUsed, maxNumber, hands);
                        }
                        else
                        {
                            // used too few dice
                            //return;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("should never get here");
                    }

                }
            }
            values[position] = 0;
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(this._numbers.Length);
            for (int i = _numbers.Length-1; i >= 0; i--)
            {
                for (int j = 0; j < _numbers[i]; j++)
                {
                    string c = (i + 1).ToString();
                    if (i == 5) c = "w";
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
    public class Take 
    { 
        public byte DiceScore { get; set; }
        public byte Number { get; set; }
        public override bool Equals(object obj)
        {
            Take other = obj as Take;
            if (other == null) return false;
            return (DiceScore == other.DiceScore && Number == other.Number);
        }
        public override int GetHashCode()
        {
            return DiceScore.GetHashCode() + (10*Number).GetHashCode();
        }
    }
}
