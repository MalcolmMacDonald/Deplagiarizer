using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Net;
using System.IO;

namespace Deplagiarizer
{
    public class SearchableWord
    {
        const int minimumWordLength = 3;
        const int maxSynonymReturnCount = 10;
        const int serverErrorSleepTime = 400;


        public string startPunctuation;
        public string endPunctuation;
        public string wordWithCase;
        public string trailingWhitespace;

        public string inputText;
        public string outputText;


        public Task deplagiarizeTask;

        public long startIndex;

        public SearchableWord(string _inputText, long _startIndex = 0)
        {
            startIndex = _startIndex;
            inputText = _inputText;
            char[] punctuation = inputText.Where(Char.IsPunctuation).Distinct().ToArray();
            bool containsPunctuation = inputText.Any(Char.IsPunctuation);
            bool containsLetters = inputText.Any(Char.IsLetter);

            if (containsPunctuation && containsLetters)
            {
                startPunctuation = new string(inputText.TakeWhile(s => Char.IsPunctuation(s)).ToArray());
                endPunctuation = new string(inputText.Reverse().SkipWhile(Char.IsWhiteSpace).TakeWhile(Char.IsPunctuation).ToArray());
            }
            wordWithCase = inputText;
            if (wordWithCase.Any(Char.IsWhiteSpace))
            {
                trailingWhitespace = new string(wordWithCase.SkipWhile(s => !Char.IsWhiteSpace(s)).ToArray());

            }
            wordWithCase = new string(wordWithCase.Where(s => !Char.IsWhiteSpace(s)).ToArray());

            if (containsLetters)
            {
                wordWithCase = wordWithCase.Trim(punctuation);
            }


        }
        public void GetWordSynonym(bool runSynchronously = false)
        {
            if (runSynchronously)
            {
                Task.WaitAll(GetFormatedSynonym());
            }
            else
            {
                deplagiarizeTask = GetFormatedSynonym();
            }
        }
        async Task GetFormatedSynonym()
        {
            if (wordWithCase.Length < minimumWordLength)
            {
                outputText = FormatWord(wordWithCase);
                return;
            }
            if (Program.wordSynonyms.ContainsKey(wordWithCase.ToLowerInvariant()))
            {
                string dictionaryWord = Program.wordSynonyms[wordWithCase.ToLowerInvariant()].ToLowerInvariant();
                dictionaryWord = CopyFormatting(dictionaryWord, wordWithCase);
                outputText = FormatWord(dictionaryWord);
                return;
            }

            string[] wordPartsOfSpeech = await GetWordPartsfSpeech(wordWithCase);

            string wordSynonym = await GetSynonym(wordWithCase, wordPartsOfSpeech);

            wordSynonym = wordSynonym.ToLowerInvariant();
            wordSynonym = CopyFormatting(wordSynonym, wordWithCase);

            outputText = FormatWord(wordSynonym);
        }

        string FormatWord(string ouputWithCase)
        {
            return startPunctuation + ouputWithCase + endPunctuation + trailingWhitespace;
        }
        string CopyFormatting(string inputText, string formattingText)
        {
            if (Char.IsUpper(formattingText[0]))
            {
                if (formattingText.All(Char.IsUpper))
                {
                    inputText = new string(inputText.Select(Char.ToUpper).ToArray());
                }
                else
                {
                    inputText = Char.ToUpper(inputText[0]) + inputText.Substring(1);
                }
            }

            return inputText;
        }

        async Task<string> GetSynonym(string word, string[] partsOfSpeech)
        {
            Thread.Sleep(new Random().Next(serverErrorSleepTime));

            string datamuseQuery = "words?rel_syn=" + word + "&md=p" + "&max=" + maxSynonymReturnCount;
            string datamuseJSON = await GetDatamuseJSON(datamuseQuery);

            DatamuseWordData[] wordData = JsonConvert.DeserializeObject<DatamuseWordData[]>(datamuseJSON);
            if (wordData.Length == 0)
            {
                return word;
            }
            wordData = wordData.Where(s => s.tags.Intersect(partsOfSpeech).Count() > 0).ToArray();
            if (wordData.Length == 0)
            {
                return word;
            }
            string outputWord = wordData[0].word.Split(' ').Last();
            Console.WriteLine(word + " -> " + outputWord);
            if (!Program.wordSynonyms.ContainsKey(word.ToLowerInvariant()))
            {
                Program.wordSynonyms.Add(word.ToLowerInvariant(), outputWord.ToLowerInvariant());
            }
            return outputWord;
        }

        async Task<string[]> GetWordPartsfSpeech(string word)
        {
            Thread.Sleep(new Random().Next(serverErrorSleepTime));
            string datamuseQuery = "words?sp=" + word + "&md=p";
            string datamuseJSON = await GetDatamuseJSON(datamuseQuery);

            DatamuseWordData[] wordData = JsonConvert.DeserializeObject<DatamuseWordData[]>(datamuseJSON);
            if (wordData.Length == 0)
            {
                return new string[0];
            }
            return wordData[0].tags;
        }

        class DatamuseWordData
        {
            public string word = "";
            public int score = 0;
            public string[] tags = new string[0];
        }

        async Task<string> GetDatamuseJSON(string query)
        {
            string uri = "https://api.datamuse.com/" + query;

            WebResponse response = null;
            while (response == null)
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                try
                {
                    response = await request.GetResponseAsync();
                }
                catch (WebException e)
                {
                    Console.WriteLine(e.Message);
                    Thread.Sleep(serverErrorSleepTime);
                }
            }
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }

        }
    }

}