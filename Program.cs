using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text;
namespace Deplagiarizer
{

    class Program
    {

        const int queueLength = 60;
        const int averageHistory = 60;

        public static Dictionary<string, string> wordSynonyms = new Dictionary<string, string>();

        static void Main(string[] args)
        {
            string inputFolder = Directory.GetCurrentDirectory() + "\\InputTexts";
            string outputFolder = Directory.GetCurrentDirectory() + "\\OutputTexts";

            string[] inputTexts = Directory.GetFiles(inputFolder);
            for (int i = 0; i < inputTexts.Length; i++)
            {
                string inputFile = Path.GetFileName(inputTexts[i]);
                string inputFileWithoutExtension = inputFile.Split('.')[0];

                string outputFileNameDeplagiarized = "";
                List<string> outputFileNameWordsInput = GetFileNameWords(inputFileWithoutExtension);
                for (int j = 0; j < outputFileNameWordsInput.Count; j++)
                {
                    SearchableWord outputFileNameWord = new SearchableWord(outputFileNameWordsInput[j]);
                    outputFileNameWord.GetWordSynonym(true);
                    outputFileNameDeplagiarized += outputFileNameWord.outputText;
                }

                string outputFileName = outputFolder + "\\" + outputFileNameDeplagiarized + ".txt";

                if (!File.Exists(outputFileName))
                {
                    File.Create(outputFileName).Close();
                }

                DeplagiarizeFile(inputTexts[i], outputFileName);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Completed deplagiarizing file: " + outputFileNameDeplagiarized + ".txt");
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            Console.ReadLine();
        }

        static void DeplagiarizeFile(string inputFileName, string outputFileName)
        {


            PopulateDictionary(inputFileName, outputFileName);
            long startingWordIndex = GetFileWordCount(outputFileName);

            TextReader inputFileTextReader = new TextReader(inputFileName, startingWordIndex);

            List<SearchableWord> wordQueue = new List<SearchableWord>();

            for (int j = 0; j < queueLength; j++)
            {
                EnqueueNewWord(inputFileTextReader, wordQueue);
            }
            Stopwatch queueStopWatch = new Stopwatch();
            queueStopWatch.Restart();

            Queue<double> wordCalculationTimesQueue = new Queue<double>();
            do
            {
                int completedWordsCount = WriteAndRefreshQueue(inputFileTextReader, outputFileName, ref wordQueue);

                if (completedWordsCount > 0)
                {
                    ShowDebugText(queueLength, completedWordsCount, queueStopWatch, wordCalculationTimesQueue);
                }

                queueStopWatch.Restart();
            }
            while (!inputFileTextReader.atEndOfFile);

            WriteAndRefreshQueue(inputFileTextReader, outputFileName, ref wordQueue, true);
        }

        static int WriteAndRefreshQueue(TextReader inputTextReader, string outputFileName, ref List<SearchableWord> currentWordQueue, bool synchronous = false)
        {
            int completedWordsCount = 0;
            if (synchronous)
            {
                Task.WaitAll(currentWordQueue.Select(s => s.deplagiarizeTask).ToArray());
            }
            using (StreamWriter outputStreamWriter = File.AppendText(outputFileName))
            {
                List<SearchableWord> completedBegininingWords = currentWordQueue.TakeWhile(s => s.deplagiarizeTask.IsCompleted).ToList();
                foreach (SearchableWord word in completedBegininingWords)
                {
                    completedWordsCount++;
                    outputStreamWriter.Write(word.outputText);
                }


                currentWordQueue = currentWordQueue.Skip(completedWordsCount).ToList();

                while (currentWordQueue.Count < queueLength)
                {
                    if (!EnqueueNewWord(inputTextReader, currentWordQueue))
                    {
                        break;
                    }
                }

            }
            return completedWordsCount;
        }

        static bool EnqueueNewWord(TextReader reader, List<SearchableWord> currentWordQueue)
        {
            reader.MoveNext();
            if (reader.Current == null)
            {
                return false;
            }
            reader.Current.GetWordSynonym();
            int indexToInsertAt = currentWordQueue.FindLastIndex(s => reader.Current.startIndex > s.startIndex) + 1;
            currentWordQueue.Insert(indexToInsertAt, reader.Current);
            return true;
        }

        static void ShowDebugText(int wordQueueCount, long completedWordsCount, Stopwatch queueStopWatch, Queue<double> wordTimes)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Wrote " + completedWordsCount + " words of " + wordQueueCount);
            Console.ForegroundColor = ConsoleColor.Magenta;
            double completionPercent = ((double)completedWordsCount) / (double)wordQueueCount;
            completionPercent *= 100;
            Console.WriteLine("{0}%", Math.Round(completionPercent));
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Dictionary Size: " + wordSynonyms.Count);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("in {0} ms", queueStopWatch.ElapsedMilliseconds);
            Console.ForegroundColor = ConsoleColor.Yellow;
            double perWordtime = (double)queueStopWatch.ElapsedMilliseconds / completedWordsCount;

            wordTimes.Enqueue(perWordtime);
            if (wordTimes.Count > averageHistory)
            {
                wordTimes.Dequeue();
            }
            string formattedAverage = (wordTimes.Sum() / wordTimes.Count).ToString("F3");
            Console.WriteLine("Average ms per word: " + formattedAverage + ", average queue history size {0}", wordTimes.Count);

            Console.ForegroundColor = ConsoleColor.Gray;
        }


        static long GetFileWordCount(string outputTextFileName)
        {
            long currentWordCount = 0;
            TextReader outputReader = new TextReader(outputTextFileName);
            while (true)
            {
                if (!outputReader.MoveNext())
                {
                    break;
                }
                currentWordCount++;

            }
            return currentWordCount;
        }

        static void PopulateDictionary(string inputTextFileName, string outputTextFileName)
        {
            using (FileStream inputFileStream = File.Open(inputTextFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BufferedStream inputBufferedStream = new BufferedStream(inputFileStream))
            using (StreamReader inputStreamReader = new StreamReader(inputBufferedStream))
            using (FileStream outputFileStream = File.Open(outputTextFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BufferedStream outputBufferedStream = new BufferedStream(outputFileStream))
            using (StreamReader outputStreamReader = new StreamReader(outputBufferedStream))
            {
                string line;
                while ((line = outputStreamReader.ReadLine()) != null)
                {
                    char[] punctuation = line.Where(Char.IsPunctuation).Distinct().ToArray();
                    string[] outputWords = line.Split().Select(s => s.Trim(punctuation)).ToArray();
                    string inputLine = inputStreamReader.ReadLine();
                    string[] inputWords = inputLine.Split().Select(s => s.Trim(punctuation)).ToArray();

                    for (int i = 0; i < inputWords.Length; i++)
                    {
                        if (outputWords[i] == "" || outputWords[i] == null)
                        {
                            break;
                        }
                        if (inputWords[i].ToLowerInvariant() != outputWords[i].ToLowerInvariant())
                        {
                            if (!wordSynonyms.ContainsKey(inputWords[i].ToLowerInvariant()))
                            {
                                wordSynonyms.Add(inputWords[i].ToLowerInvariant(), outputWords[i].ToLowerInvariant());
                            }
                        }
                    }
                }
            }
        }

        static List<string> GetFileNameWords(string fileName)
        {
            List<string> outputWords = new List<string>();
            int capitalCount = fileName.Where(char.IsUpper).Count();
            int currentCharacterIndex = 0;
            for (int i = 0; i < capitalCount; i++)
            {
                string currentWord = fileName[currentCharacterIndex].ToString();
                currentWord += new string(fileName.Skip(currentCharacterIndex + 1).TakeWhile(char.IsLower).ToArray());
                currentCharacterIndex += currentWord.Length;
                outputWords.Add(currentWord);
            }


            return outputWords;
        }
    }
}
