using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Linq;
namespace Deplagiarizer
{
    public class TextReader : IEnumerator, IEnumerable, IDisposable
    {
        FileStream inputFileStream;
        BufferedStream inputBufferedStream;
        StreamReader inputStreamReader;
        string _inputTextFileName;
        SearchableWord currentWord;
        long currentCharacterIndex = 0;

        public bool atEndOfFile
        {
            get
            {
                return inputStreamReader.EndOfStream;
            }
        }
        public TextReader(string inputTextFileName, long initialWordIndex = 0)
        {
            currentCharacterIndex = initialWordIndex;
            _inputTextFileName = inputTextFileName;
            inputFileStream = File.Open(_inputTextFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            inputBufferedStream = new BufferedStream(inputFileStream);
            inputStreamReader = new StreamReader(inputBufferedStream);
            ReadUntilWordIndex(initialWordIndex);
        }
        void ReadUntilWordIndex(long indexToReadUntil)
        {
            long wordCount = 0;
            while (wordCount < indexToReadUntil)
            {
                if (!MoveNext())
                {
                    break;
                }
                wordCount++;
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)GetEnumerator();
        }
        public TextReader GetEnumerator()
        {
            return new TextReader(_inputTextFileName);
        }
        public bool MoveNext()
        {
            string wordFromReader = ReadWordFromReader();
            if (wordFromReader == null)
            {
                currentWord = null;
                return false;
            }
            currentWord = new SearchableWord(wordFromReader, currentCharacterIndex);
            currentCharacterIndex += currentWord.inputText.Length;
            return true; // REPLACE THIS
        }
        public void Reset()
        {
            currentWord = null;
            inputFileStream.Position = 0;
            inputStreamReader.DiscardBufferedData();
        }
        object IEnumerator.Current
        {
            get
            {
                return Current;
            }
        }
        public SearchableWord Current
        {
            get
            {
                return currentWord;
            }
        }
        void IDisposable.Dispose()
        {
            inputStreamReader.Dispose();
            inputBufferedStream.Dispose();
            inputFileStream.Dispose();
        }
        string ReadWordFromReader()
        {
            if (inputStreamReader.EndOfStream)
            {
                return null;
            }
            StringBuilder wordBuilder = new StringBuilder();
            char c;
            do
            {
                int nextCharIndex = inputStreamReader.Read();
                if (nextCharIndex == -1)
                {
                    break;
                }
                c = Convert.ToChar(nextCharIndex);
                wordBuilder.Append(c);
            } while (!Char.IsWhiteSpace(c));

            int peekedChar;
            while (true)
            {
                peekedChar = inputStreamReader.Peek();
                if (peekedChar == -1)
                {
                    break;
                }
                if (!Char.IsWhiteSpace(Convert.ToChar(peekedChar)))
                {
                    break;
                }
                c = Convert.ToChar(inputStreamReader.Read());
                wordBuilder.Append(c);
            }


            return wordBuilder.ToString();
        }
    }
}