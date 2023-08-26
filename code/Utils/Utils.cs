using Sandbox;
using System;
using System.Collections.Generic;

namespace GuessIt
{
    public enum WORD_DIFFICULTY
    {
        EASY,
        MEDIUM,
        HARD
    }

    public static class Utils
    {
        /// <summary>
        /// Maps a value from one range to another
        /// </summary>
        /// <param name="_val">The value to map</param>
        /// <param name="_inputMin">The minimum value of the current range</param>
        /// <param name="_inputMax">The maximum value of the current range</param>
        /// <param name="_outputMin">The minimum value of the desired range</param>
        /// <param name="_outputMax">The maximum value of the desired range</param>
        /// <returns>The mapped value</returns>
        public static float Map(float _val, float _inputMin, float _inputMax, float _outputMin, float _outputMax)
        {
            if (_outputMin > _outputMax)
            {
                float temp = _inputMin;
                _inputMin = _inputMax;
                _inputMax = temp;
            }

            float mapped = (_val - _inputMin) / (_inputMax - _inputMin);
            
            if (_outputMin > _outputMax)
            {
                return _outputMin + ((1 - mapped) * (_outputMax - _outputMin));
            }
            else
            {
                return _outputMin + (mapped * (_outputMax - _outputMin));
            }
        }

        public static string[] EasyWords = new string[0];
        public static string[] MediumWords = new string[0];
        public static string[] HardWords = new string[0];
        public static string GetRandomWord(WORD_DIFFICULTY difficulty)
        {
            Random rand = new Random();
            if(difficulty == WORD_DIFFICULTY.MEDIUM)
            {
                if(rand.Next(0, 100) < 20)
                {
                    difficulty = WORD_DIFFICULTY.EASY;
                }
            }
            else if(difficulty == WORD_DIFFICULTY.HARD)
            {
                if(rand.Next(0, 100) < 20)
                {
                    difficulty = WORD_DIFFICULTY.MEDIUM;
                }
            }
        
            if(difficulty == WORD_DIFFICULTY.EASY)
            {
                if(EasyWords.Length == 0)
                {
                    EasyWords = FileSystem.Mounted.ReadAllText("words/words-easy.txt").Split("\n");
                }
                return EasyWords[rand.Next(0, EasyWords.Length)].Trim();
            }
            else if(difficulty == WORD_DIFFICULTY.MEDIUM)
            {
                if(MediumWords.Length == 0)
                {
                    MediumWords = FileSystem.Mounted.ReadAllText("words/words-medium.txt").Split("\n");
                }
                return MediumWords[rand.Next(0, MediumWords.Length)].Trim();
            }
            else if(difficulty == WORD_DIFFICULTY.HARD)
            {
                if(HardWords.Length == 0)
                {
                    HardWords = FileSystem.Mounted.ReadAllText("words/words-hard.txt").Split("\n");
                }
                return HardWords[rand.Next(0, HardWords.Length)].Trim();
            }
            return "Broken Video Game";
        }
    }
}
