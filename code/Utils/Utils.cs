using System;

namespace GuessIt
{
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
    }
}
