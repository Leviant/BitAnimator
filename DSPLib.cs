using System;
using System.Numerics;
//using System.Threading.Tasks;
using System.Diagnostics;
using UnityEngine;


// =====[ Revision History ]==========================================
// 17Jun16 - 1.0 - First release - Steve Hageman
// 20Jun16 - 1.01 - Made some variable terms consistent - Steve Hageman
// 16Jul16 - 1.02 - Calculated sign of DFT phase was not consistent with that of the FFT. ABS() of phase was right.
//                  FFT with zero padding did not correctly clean up first runs results.
//                  Added UnwrapPhaseDegrees() and UnwrapPhaseRadians() to Analysis Class.
// 04Jul17 - 1.03 - Added zero or negative check to all Log10 operations.
// 15Oct17 - 1.03.1 - Slight interoperability correction to V1.03, same results, different design pattern.
//


namespace DSPLib
{

    #region =====[ DFT Core Class ]======================================================

    /**
     * Performs a complex DFT w/Optimizations for .NET >= 4.
     *
     * Released under the MIT License
     *
     * DFT Core Functions Copyright (c) 2016 Steven C. Hageman
     *
     * Permission is hereby granted, free of charge, to any person obtaining a copy
     * of this software and associated documentation files (the "Software"), to
     * deal in the Software without restriction, including without limitation the
     * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
     * sell copies of the Software, and to permit persons to whom the Software is
     * furnished to do so, subject to the following conditions:
     *
     * The above copyright notice and this permission notice shall be included in
     * all copies or substantial portions of the Software.
     *
     * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
     * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
     * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
     * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
     * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
     * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
     * IN THE SOFTWARE.
     */

    /// <summary>
    /// DFT Base Class
    /// </summary>
    public class DFT
    {
        /// <summary>
        /// DFT Class
        /// </summary>
        public DFT() { }

        #region Properties

        private float mDFTScale;       // DFT ONLY Scale Factor
        private UInt32 mLengthTotal;    // mN + mZp
        private UInt32 mLengthHalf;     // (mN + mZp) / 2

        private float[,] mCosTerm;      // Caching of multiplication terms to save time
        private float[,] mSinTerm;      // on smaller DFT's
        private bool mOutOfMemory;       // True = Caching ran out of memory.

        
        /// <summary>
        /// Read only Boolean property. True meas the currently defined DFT is using cached memory to speed up calculations.
        /// </summary>
        public bool IsUsingCached
        {
            private set { }
            get { return !mOutOfMemory; }
        }

        #endregion

        #region Core DFT Routines

        /// <summary>
        /// Pre-Initializes the DFT.
        /// Must call first and this anytime the FFT setup changes.
        /// </summary>
        /// <param name="inputDataLength"></param>
        /// <param name="zeroPaddingLength"></param>
        /// <param name="forceNoCache">True will force the DFT to not use pre-calculated caching.</param>
        public void Initialize(UInt32 inputDataLength, UInt32 zeroPaddingLength = 0, bool forceNoCache = false)
        {
            // Save the sizes for later
            mLengthTotal = inputDataLength + zeroPaddingLength;
            mLengthHalf = (mLengthTotal / 2) + 1;

            // Set the overall scale factor for all the terms
            mDFTScale = Mathf.Sqrt(2) / (float)(inputDataLength + zeroPaddingLength);                 // Natural DFT Scale Factor                                           // Window Scale Factor
            mDFTScale *= ((float)(inputDataLength + zeroPaddingLength)) / (float)inputDataLength;   // Account For Zero Padding                           // Zero Padding Scale Factor


            if (forceNoCache == true)
            {
                // If optional No Cache - just flag that the cache failed 
                // then the routines will use the brute force DFT methods.
                mOutOfMemory = true;
                return;
            }

            // Try to make pre-calculated sin/cos arrays. If not enough memory, then 
            // use a brute force DFT.
            // Note: pre-calculation speeds the DFT up by about 5X (on a core i7)
            mOutOfMemory = false;
            try
            {
                mCosTerm = new float[mLengthTotal, mLengthTotal];
                mSinTerm = new float[mLengthTotal, mLengthTotal];

                float scaleFactor = 2.0f * Mathf.PI / mLengthTotal;

                //Parallel.For(0, mLengthHalf, (j) =>
                for (int j = 0; j < mLengthHalf; j++)
                {
                    float a = j * scaleFactor;
                    for (int k = 0; k < mLengthTotal; k++)
                    {
                        mCosTerm[j, k] = Mathf.Cos(a * k) * mDFTScale;
                        mSinTerm[j, k] = Mathf.Sin(a * k) * mDFTScale;
                    }
                } //);
            }
            catch (OutOfMemoryException)
            {
                // Could not allocate enough room for the cache terms
                // So, will use brute force DFT
                mOutOfMemory = true;
            }
        }


        /// <summary>
        /// Execute the DFT.
        /// </summary>
        /// <param name="timeSeries"></param>
        /// <returns>Complex[] FFT Result</returns>
        public Complex[] Execute(float[] timeSeries)
        {
			UnityEngine.Debug.Assert(timeSeries.Length <= mLengthTotal, "The input timeSeries length was greater than the total number of points that was initialized. DFT.Exectue()");

            // Account for zero padding in size of DFT input array
            float[] totalInputData = new float[mLengthTotal];
            Array.Copy(timeSeries, totalInputData, timeSeries.Length);

            Complex[] output;
            if (mOutOfMemory)
                output = Dft(totalInputData);
            else
                output = DftCached(totalInputData);

            return output;
        }

        #region Private DFT Implementation details

        /// <summary>
        /// A brute force DFT - Uses Task / Parallel pattern
        /// </summary>
        /// <param name="timeSeries"></param>
        /// <returns>Complex[] result</returns>
        private Complex[] Dft(float[] timeSeries)
        {
            UInt32 n = mLengthTotal;
            UInt32 m = mLengthHalf;
            float[] re = new float[m];
            float[] im = new float[m];
            Complex[] result = new Complex[m];
            float sf = 2.0f * Mathf.PI / n;

            //Parallel.For(0, m, (j) =>
            for (UInt32 j = 0; j < m; j++)
            {
                float a = j * sf;
                for (UInt32 k = 0; k < n; k++)
                {
                    re[j] += timeSeries[k] * Mathf.Cos(a * k) * mDFTScale;
                    im[j] -= timeSeries[k] * Mathf.Sin(a * k) * mDFTScale;
                }

                result[j] = new Complex(re[j], im[j]);
            }

            // DC and Fs/2 Points are scaled differently, since they have only a real part
            result[0] = new Complex(result[0].Real / Mathf.Sqrt(2.0f), 0.0f);
            result[mLengthHalf - 1] = new Complex(result[mLengthHalf - 1].Real / Mathf.Sqrt(2.0f), 0.0f);

            return result;
        }

        /// <summary>
        /// DFT with Pre-calculated Sin/Cos arrays + Task / Parallel pattern.
        /// DFT can only be so big before the computer runs out of memory and has to use
        /// the brute force DFT.
        /// </summary>
        /// <param name="timeSeries"></param>
        /// <returns>Complex[] result</returns>
        private Complex[] DftCached(float[] timeSeries)
        {
            UInt32 n = mLengthTotal;
            UInt32 m = mLengthHalf;
            float[] re = new float[m];
            float[] im = new float[m];
            Complex[] result = new Complex[m];

            //Parallel.For(0, m, (j) =>
            for (UInt32 j = 0; j < m; j++)
            {
                for (UInt32 k = 0; k < n; k++)
                {
                    re[j] += timeSeries[k] * mCosTerm[j, k];
                    im[j] -= timeSeries[k] * mSinTerm[j, k];
                }
                result[j] = new Complex(re[j], im[j]);
            }

            // DC and Fs/2 Points are scaled differently, since they have only a real part
            result[0] = new Complex(result[0].Real / Mathf.Sqrt(2.0f), 0.0f);
            result[mLengthHalf - 1] = new Complex(result[mLengthHalf - 1].Real / Mathf.Sqrt(2.0f), 0.0f);

            return result;
        }

        #endregion

        #endregion

        #region Utility Functions

        /// <summary>
        /// Return the Frequency Array for the currently defined DFT.
        /// Takes into account the total number of points and zero padding points that were defined.
        /// </summary>
        /// <param name="samplingFrequencyHz"></param>
        /// <returns></returns>
        public float[] FrequencySpan(float samplingFrequencyHz)
        {
            UInt32 points = mLengthHalf;
            float[] result = new float[points];
            float stopValue = samplingFrequencyHz / 2.0f;
            float increment = stopValue / ((float)points - 1.0f);

            for (UInt32 i = 0; i < points; i++)
                result[i] += increment * i;

            return result;
        }

    }

    #endregion

    #endregion


    #region =====[ FFT Core Class ]======================================================

    /**
     * Performs an in-place complex FFT.
     *
     * Released under the MIT License
     *
     * Core FFT class based on,
     *      Fast C# FFT - Copyright (c) 2010 Gerald T. Beauregard
     *
     * Changes to: Interface, scaling, zero padding, return values.
     * Change to .NET Complex output types and integrated with my DSP Library. 
     * Note: Complex Number Type requires .NET >= 4.0
     * 
     * These changes as noted above Copyright (c) 2016 Steven C. Hageman
     *
     * 
     * Permission is hereby granted, free of charge, to any person obtaining a copy
     * of this software and associated documentation files (the "Software"), to
     * deal in the Software without restriction, including without limitation the
     * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
     * sell copies of the Software, and to permit persons to whom the Software is
     * furnished to do so, subject to the following conditions:
     *
     * The above copyright notice and this permission notice shall be included in
     * all copies or substantial portions of the Software.
     *
     * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
     * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
     * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
     * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
     * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
     * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
     * IN THE SOFTWARE.
     */


    /// <summary>
    /// FFT Base Class
    /// </summary>
    public class FFT
    {
        /// <summary>
        /// FFT Class
        /// </summary>
        public FFT() { }

        #region Private Properties

        private float mFFTScale = 1.0f;
        private UInt32 mLogN = 0;       // log2 of FFT size
        private UInt32 mN = 0;          // Time series length
        private UInt32 mLengthTotal;    // mN + mZp
        private UInt32 mLengthHalf;     // (mN + mZp) / 2
        private FFTElement[] mX;        // Vector of linked list elements

        // Element for linked list to store input/output data.
        private class FFTElement
        {
            public float re = 0.0f;     // Real component
            public float im = 0.0f;     // Imaginary component
            public FFTElement next;     // Next element in linked list
            public UInt32 revTgt;       // Target position post bit-reversal
        }

        #endregion

        #region FFT Core Functions

        /// <summary>
        /// Initialize the FFT. Must call first and this anytime the FFT setup changes.
        /// </summary>
        /// <param name="inputDataLength"></param>
        /// <param name="zeroPaddingLength"></param>
        public void Initialize(UInt32 inputDataLength, UInt32 zeroPaddingLength = 0)
        {
            mN = inputDataLength;

            // Find the power of two for the total FFT size up to 2^32
            bool foundIt = false;
            for (mLogN = 1; mLogN <= 32; mLogN++)
            {
                float n = Mathf.Pow(2.0f, mLogN);
                if ((inputDataLength + zeroPaddingLength) == n)
                {
                    foundIt = true;
                    break;
                }
            }

            if (foundIt == false)
                throw new ArgumentOutOfRangeException("inputDataLength + zeroPaddingLength was not an even power of 2! FFT cannot continue.");

            // Set global parameters.
            mLengthTotal = inputDataLength + zeroPaddingLength;
            mLengthHalf = (mLengthTotal / 2) + 1;

            // Set the overall scale factor for all the terms
            mFFTScale = Mathf.Sqrt(2) / (float)(mLengthTotal);                // Natural FFT Scale Factor                                           // Window Scale Factor
            mFFTScale *= ((float)mLengthTotal) / (float)inputDataLength;    // Zero Padding Scale Factor

            // Allocate elements for linked list of complex numbers.
            mX = new FFTElement[mLengthTotal];
            for (UInt32 k = 0; k < (mLengthTotal); k++)
                mX[k] = new FFTElement();

            // Set up "next" pointers.
            for (UInt32 k = 0; k < (mLengthTotal) - 1; k++)
                mX[k].next = mX[k + 1];

            // Specify target for bit reversal re-ordering.
            for (UInt32 k = 0; k < (mLengthTotal); k++)
                mX[k].revTgt = BitReverse(k, mLogN);
        }


        /// <summary>
        /// Executes a FFT of the input time series.
        /// </summary>
        /// <param name="timeSeries"></param>
        /// <returns>Complex[] Spectrum</returns>
        public Complex[] Execute(float[] timeSeries)
        {
            UInt32 numFlies = mLengthTotal >> 1;  // Number of butterflies per sub-FFT
            UInt32 span = mLengthTotal >> 1;      // Width of the butterfly
            UInt32 spacing = mLengthTotal;        // Distance between start of sub-FFTs
            UInt32 wIndexStep = 1;          // Increment for twiddle table index

			UnityEngine.Debug.Assert(timeSeries.Length <= mLengthTotal, "The input timeSeries length was greater than the total number of points that was initialized. FFT.Exectue()");

            // Copy data into linked complex number objects
            FFTElement x = mX[0];
            UInt32 k = 0;
            for (UInt32 i = 0; i < mN; i++)
            {
                x.re = timeSeries[k];
                x.im = 0.0f;
                x = x.next;
                k++;
            }

            // If zero padded, clean the 2nd half of the linked list from previous results
            if( mN != mLengthTotal)
            {
                for (UInt32 i = mN; i < mLengthTotal; i++)
                {
                    x.re = 0.0f; 
                    x.im = 0.0f;
                    x = x.next;
                }
            }

            // For each stage of the FFT
            for (UInt32 stage = 0; stage < mLogN; stage++)
            {
                // Compute a multiplier factor for the "twiddle factors".
                // The twiddle factors are complex unit vectors spaced at
                // regular angular intervals. The angle by which the twiddle
                // factor advances depends on the FFT stage. In many FFT
                // implementations the twiddle factors are cached, but because
                // array lookup is relatively slow in C#, it's just
                // as fast to compute them on the fly.
                float wAngleInc = wIndexStep * -2.0f * Mathf.PI / (mLengthTotal);
                float wMulRe = Mathf.Cos(wAngleInc);
                float wMulIm = Mathf.Sin(wAngleInc);

                for (UInt32 start = 0; start < (mLengthTotal); start += spacing)
                {
                    FFTElement xTop = mX[start];
                    FFTElement xBot = mX[start + span];

                    float wRe = 1.0f;
                    float wIm = 0.0f;

                    // For each butterfly in this stage
                    for (UInt32 flyCount = 0; flyCount < numFlies; ++flyCount)
                    {
                        // Get the top & bottom values
                        float xTopRe = xTop.re;
                        float xTopIm = xTop.im;
                        float xBotRe = xBot.re;
                        float xBotIm = xBot.im;

                        // Top branch of butterfly has addition
                        xTop.re = xTopRe + xBotRe;
                        xTop.im = xTopIm + xBotIm;

                        // Bottom branch of butterfly has subtraction,
                        // followed by multiplication by twiddle factor
                        xBotRe = xTopRe - xBotRe;
                        xBotIm = xTopIm - xBotIm;
                        xBot.re = xBotRe * wRe - xBotIm * wIm;
                        xBot.im = xBotRe * wIm + xBotIm * wRe;

                        // Advance butterfly to next top & bottom positions
                        xTop = xTop.next;
                        xBot = xBot.next;

                        // Update the twiddle factor, via complex multiply
                        // by unit vector with the appropriate angle
                        // (wRe + j wIm) = (wRe + j wIm) x (wMulRe + j wMulIm)
                        float tRe = wRe;
                        wRe = wRe * wMulRe - wIm * wMulIm;
                        wIm = tRe * wMulIm + wIm * wMulRe;
                    }
                }

                numFlies >>= 1;   // Divide by 2 by right shift
                span >>= 1;
                spacing >>= 1;
                wIndexStep <<= 1;     // Multiply by 2 by left shift
            }

            // The algorithm leaves the result in a scrambled order.
            // Unscramble while copying values from the complex
            // linked list elements to a complex output vector & properly apply scale factors.

            x = mX[0];
            Complex[] unswizzle = new Complex[mLengthTotal];
            while (x != null)
            {
                UInt32 target = x.revTgt;
                unswizzle[target] = new Complex(x.re * mFFTScale, x.im * mFFTScale);
                x = x.next;
            }

            // Return 1/2 the FFT result from DC to Fs/2 (The real part of the spectrum)
            //UInt32 halfLength = ((mN + mZp) / 2) + 1;
            Complex[] result = new Complex[mLengthHalf];
            Array.Copy(unswizzle, result, mLengthHalf);

            // DC and Fs/2 Points are scaled differently, since they have only a real part
            result[0] = new Complex(result[0].Real / Mathf.Sqrt(2), 0.0f);
            result[mLengthHalf - 1] = new Complex(result[mLengthHalf - 1].Real / Mathf.Sqrt(2), 0.0f);

            return result;
        }

        #region Private FFT Routines

        /**
         * Do bit reversal of specified number of places of an int
         * For example, 1101 bit-reversed is 1011
         *
         * @param   x       Number to be bit-reverse.
         * @param   numBits Number of bits in the number.
         */
        private UInt32 BitReverse(UInt32 x, UInt32 numBits)
        {
            UInt32 y = 0;
            for (UInt32 i = 0; i < numBits; i++)
            {
                y <<= 1;
                y |= x & 0x0001;
                x >>= 1;
            }
            return y;
        }

        #endregion

        #endregion

        #region Utility Functions

        /// <summary>
        /// Return the Frequency Array for the currently defined FFT.
        /// Takes into account the total number of points and zero padding points that were defined.
        /// </summary>
        /// <param name="samplingFrequencyHz"></param>
        /// <returns></returns>
        public float[] FrequencySpan(float samplingFrequencyHz)
        {
            UInt32 points = (UInt32)mLengthHalf;
            float[] result = new float[points];
            float stopValue = samplingFrequencyHz / 2.0f;
            float increment = stopValue / ((float)points - 1.0f);

            for (Int32 i = 0; i < points; i++)
                result[i] += increment * i;

            return result;
        }

        #endregion

    }

    #endregion


    #region =====[ Generation, Conversion, Analysis and Array Manipulations ]============

    public class DSP
    {
        /*
        * Released under the MIT License
        *
        * DSP Library for C# - Copyright(c) 2016 Steven C. Hageman.
        *
        * Permission is hereby granted, free of charge, to any person obtaining a copy
        * of this software and associated documentation files (the "Software"), to
        * deal in the Software without restriction, including without limitation the
        * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
        * sell copies of the Software, and to permit persons to whom the Software is
        * furnished to do so, subject to the following conditions:
        *
        * The above copyright notice and this permission notice shall be included in
        * all copies or substantial portions of the Software.
        *
        * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
        * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
        * IN THE SOFTWARE.
        */


        #region Generate Signals & Noise

        public static class Generate
        {
            /// <summary>
            /// Generate linearly spaced array. Like the Octave function of the same name.
            /// EX: DSP.Generate.LinSpace(1, 10, 10) -> Returns array: 1, 2, 3, 4....10.
            /// </summary>
            /// <param name="startVal">Any value</param>
            /// <param name="stopVal">Any value > startVal</param>
            /// <param name="points">Number of points to generate</param>
            /// <returns>float[] array</returns>
            public static float[] LinSpace(float startVal, float stopVal, UInt32 points)
            {
                float[] result = new float[points];
                float increment = (stopVal - startVal) / ((float)points - 1.0f);

                for (UInt32 i = 0; i < points; i++)
                    result[i] = startVal + increment * i;

                return result;
            }


            /// <summary>
            /// Generates a Sine Wave Tone using Sampling Terms.
            /// </summary>
            /// <param name="amplitudeVrms"></param>
            /// <param name="frequencyHz"></param>
            /// <param name="samplingFrequencyHz"></param>
            /// <param name="points"></param>
            /// <param name="dcV">[Optional] DC Voltage offset</param>
            /// <param name="phaseDeg">[Optional] Phase of signal in degrees</param>
            /// <returns>float[] array</returns>
            public static float[] ToneSampling(float amplitudeVrms, float frequencyHz, float samplingFrequencyHz, UInt32 points, float dcV = 0, float phaseDeg = 0)
            {
                float ph_r = phaseDeg * Mathf.PI / 180.0f;
                //float ampPeak = Mathf.Sqrt(2.0f) * amplitudeVrms;

                float[] rval = new float[points];
                for (UInt32 i = 0; i < points; i++)
                {
                    float time = (float)i / samplingFrequencyHz;
                    rval[i] = Mathf.Sqrt(2.0f) * amplitudeVrms * Mathf.Sin(2.0f * Mathf.PI * time * frequencyHz + ph_r) + dcV;
                }
                return rval;
            }


            /// <summary>
            /// Generates a Sine Wave Tone using Number of Cycles Terms.
            /// </summary>
            /// <param name="amplitudeVrms"></param>
            /// <param name="cycles"></param>
            /// <param name="points"></param>
            /// <param name="dcV">[Optional] DC Voltage offset</param>
            /// <param name="phaseDeg">[Optional] Phase of signal in degrees</param>
            /// <returns>float[] array</returns>
            public static float[] ToneCycles(float amplitudeVrms, float cycles, UInt32 points, float dcV = 0, float phaseDeg = 0)
            {
                float ph_r = phaseDeg * Mathf.PI / 180.0f;
                float ampPeak = Mathf.Sqrt(2.0f) * amplitudeVrms;

                float[] rval = new float[points];
                for (UInt32 i = 0; i < points; i++)
                {
                    rval[i] = ampPeak * Mathf.Sin((2.0f * Mathf.PI * (float)i / (float)points * cycles) + ph_r) + dcV;
                }
                return rval;
            }


            /// <summary>
            /// Generates a normal distribution noise signal of the specified power spectral density (Vrms / rt-Hz).
            /// </summary>
            /// <param name="amplitudePsd (Vrms / rt-Hz)"></param>
            /// <param name="samplingFrequencyHz"></param>
            /// <param name="points"></param>
            /// <returns>float[] array</returns>
            public static float[] NoisePsd(float amplitudePsd, float samplingFrequencyHz, UInt32 points)
            {
                // Calculate what the noise amplitude needs to be in Vrms/rt_Hz
                float arms = amplitudePsd * Mathf.Sqrt(samplingFrequencyHz / 2.0f);

                // Make an n length noise vector
                float[] rval = NoiseRms(arms, points);

                return rval;
            }



            /// <summary>
            /// Generates a normal distribution noise signal of the specified Volts RMS.
            /// </summary>
            /// <param name="amplitudeVrms"></param>
            /// <param name="points"></param>
            /// <param name="dcV"></param>
            /// <returns>float[] array</returns>
            public static float[] NoiseRms(float amplitudeVrms, UInt32 points, float dcV = 0)
            {
                float[] rval = new float[points];

                // Make an n length noise vector
                rval = Noise(points, amplitudeVrms);

                rval = DSPLib.DSP.Math.Add(rval, dcV);

                return rval;
            }

            #region Private - Random Number Generator Core

            //=====[ Gaussian Noise ]=====

			//private static UnityEngine.Random mRandom = new UnityEngine.Random(); // Class level variable

            private static float[] Noise(UInt32 size, float scaling_vrms)
            {

                // Based on - Polar method (Marsaglia 1962)

                // Scaling used,
                // * For DFT Size => "Math.Sqrt(size)"
                // * The Sqrt(2) is a scaling factor to get the
                // output spectral power to be what the desired "scaling_vrms"
                // was as requested. The scaling will produce a "scaling_vrms"
                // value that is correct for Vrms/Rt(Hz) in the frequency domain
                // of a "1/N" scaled DFT or FFT.
                // Most DFT / FFT's are 1/N scaled - check your documentation to be sure...

                float output_scale = scaling_vrms;

                float[] data = new float[size];
                float sum = 0;

                for (UInt32 n = 0; n < size; n++)
                {
                    float s;
                    float v1;
                    do
                    {
						v1 = UnityEngine.Random.Range(-1f, 1f);
						float v2 = UnityEngine.Random.Range(-1f, 1f);

                        s = v1 * v1 + v2 * v2;
                    } while (s >= 1.0f);

                    if (s == 0.0f)
                        data[n] = 0.0f;
                    else
                        data[n] = (v1 * Mathf.Sqrt(-2.0f * Mathf.Log(s) / s)) * output_scale;

                    sum += data[n];
                }

                // Remove the average value
                float average = sum / size;
                for (UInt32 n = 0; n < size; n++)
                {
                    data[n] -= average;
                }

                // Return the Gaussian noise
                return data;
            }

            #endregion
        }

        #endregion


        #region Windows Functions & Scaling Functions

        /**
        *
        * Many of the windows functions are based on the article,
        *
        *   Spectrum and spectral density estimation by the Discrete Fourier
        *   transform (DFT), including a comprehensive list of window
        *   functions and some new ﬂat-top windows.
        *
        *   G. Heinzel, A. Rudiger and R. Schilling,
        *   Max-Planck-Institut fur Gravitationsphysik
        * 
        *   February 15, 2002
        *
        **/

        public static class Window
        {
            /// <summary>
            /// ENUM Types for included Windows.
            /// </summary>
            public enum Type
            {
                None,
                Rectangular,
                Bartlett,
                Welch,
                Sine,
                Hann,
                Hanning,
                Hamming,
                BH92,
                Nutall3,
                Nutall3A,
                Nutall3B,
                Nutall4,
                Nutall4A,
                Nutall4B,
                SFT3F,
                SFT4F,
                SFT5F,
                SFT3M,
                SFT4M,
                SFT5M,
                FTNI,
                FTHP,
                FTSRS,
                HFT70,
                HFT90D,
                HFT95,
                HFT116D,
                HFT144D,
                HFT169D,
                HFT196D,
                HFT223D,
                HFT248D,
                Exp,
                HannPoisson,
                DolphChebyshev
            }
            public static bool IsParametrizedWindow(Type type)
            {
                return type == Type.DolphChebyshev
                    || type == Type.Exp
                    || type == Type.HannPoisson;
            }

            #region Window Scale Factor

            public static class ScaleFactor
            {
                /// <summary>
                /// Calculate Signal scale factor from window coefficient array.
                /// Designed to be applied to the "Magnitude" result.
                /// </summary>
                /// <param name="windowCoefficients"></param>
                /// <returns>float scaleFactor</returns>
                public static float Signal(float[] windowCoefficients)
                {
                    float s1 = 0;
                    foreach (float coeff in windowCoefficients)
                    {
                        s1 += coeff;
                    }

                    s1 = s1 / windowCoefficients.Length;

                    return 1.0f / s1;
                }


                /// <summary>
                ///  Calculate Noise scale factor from window coefficient array.
                ///  Takes into account the bin width in Hz for the final result also.
                ///  Designed to be applied to the "Magnitude" result.
                /// </summary>
                /// <param name="windowCoefficients"></param>
                /// <param name="samplingFrequencyHz"></param>
                /// <returns>float scaleFactor</returns>
                public static float Noise(float[] windowCoefficients, float samplingFrequencyHz)
                {
                    float s2 = 0;
                    foreach (float coeff in windowCoefficients)
                    {
                        s2 = s2 + (coeff * coeff);
                    }

                    float n = windowCoefficients.Length;
                    float fbin = samplingFrequencyHz / n;

                    float sf = Mathf.Sqrt(1.0f / ((s2 / n) * fbin));

                    return sf;
                }


                /// <summary>
                ///  Calculate Normalized, Equivalent Noise BandWidth from window coefficient array.
                /// </summary>
                /// <param name="windowCoefficients"></param>
                /// <returns>float NENBW</returns>
                public static float NENBW(float[] windowCoefficients)
                {
                    float s1 = 0;
                    float s2 = 0;
                    foreach (float coeff in windowCoefficients)
                    {
                        s1 = s1 + coeff;
                        s2 = s2 + (coeff * coeff);
                    }

                    float n = windowCoefficients.Length;
                    s1 = s1 / n;

                    float nenbw = (s2 / (s1 * s1)) / n;

                    return nenbw;
                }
            }
            #endregion

            #region Window Coefficient Calculations

            /// <summary>
            /// Calculates a set of Windows coefficients for a given number of points and a window type to use.
            /// </summary>
            /// <param name="windowName"></param>
            /// <param name="points"></param>
            /// <returns>float[] array of the calculated window coefficients</returns>
            public static float[] Coefficients(Type windowName, UInt32 points)
            {
                float[] winCoeffs = new float[points];
                float N = points;

                switch (windowName)
                {
                    case Window.Type.None:
                    case Window.Type.Rectangular:
                        //wc = ones(N,1);
                        for (UInt32 i = 0; i < points; i++)
                            winCoeffs[i] = 1.0f;

                        break;

                    case Window.Type.Bartlett:
                        //n = (0:N-1)';
                        //wc = 2/N*(N/2-abs(n-(N-1)/2));
                        for (UInt32 i = 0; i < points; i++)
                            winCoeffs[i] = 2.0f / N * (N / 2.0f - Mathf.Abs(i - (N - 1.0f) / 2.0f));

                        break;

                    case Window.Type.Welch:
                        //n = (0:N-1)';
                        //wc = 1 - ( ((2*n)/N) - 1).^2;
                        for (UInt32 i = 0; i < points; i++)
                            winCoeffs[i] = 1.0f - Mathf.Pow(((2.0f * i) / N) - 1.0f, 2.0f);
                        break;
                    case Window.Type.Sine:
                        for (UInt32 i = 0; i < points; i++)
                            winCoeffs[i] = Mathf.Sin(Mathf.PI * i / N);
                        break;
                    case Window.Type.DolphChebyshev:
                        double db = 60.0;
                        double d = System.Math.Pow(10.0, db / 20.0);
                        double acosh_db = System.Math.Log(d + System.Math.Sqrt(d * d - 1.0));
                        int m = (int)points - 1;
                        for (int j = 0; j < points; j++)
                        {
                            double sum = 0;
                            for (int i = 0; i < points; i++)
                            {
                                double t = System.Math.PI * (i - m * 0.5) / N;
                                double x = System.Math.Cosh(acosh_db / m) * System.Math.Cos(t);
                                double acosh_x = System.Math.Log(x + System.Math.Sqrt(x * x - 1.0));
                                double w = System.Math.Abs(x) <= 1.0 ? System.Math.Cos(m * System.Math.Acos(x)) : System.Math.Cosh(m * acosh_x);
                                sum += w * System.Math.Cos(t * (j - m * 0.5) * 2);
                            }
                            winCoeffs[j] = (float)sum;
                        }
                        float scale = 1.0f / winCoeffs[points / 2];
                        for (uint i = 0; i < points; i++)
                        {
                            winCoeffs[i] *= scale;
                        }
                        break;
                    case Window.Type.Hann:
                    case Window.Type.Hanning:
                        //wc = (0.5 - 0.5*cos (z));
                        winCoeffs = SineExpansion(points, 0.5f, -0.5f);
                        break;

                    case Window.Type.Hamming:
                        //wc = (0.54 - 0.46*cos (z));
                        winCoeffs = SineExpansion(points, 0.54f, -0.46f);
                        break;

                    case Window.Type.BH92: // Also known as: Blackman-Harris
                                 //wc = (0.35875 - 0.48829*cos(z) + 0.14128*cos(2*z) - 0.01168*cos(3*z));
                        winCoeffs = SineExpansion(points, 0.35875f, -0.48829f, 0.14128f, -0.01168f);
                        break;

                    case Window.Type.Nutall3:
                        //c0 = 0.375; c1 = -0.5; c2 = 0.125;
                        //wc = c0 + c1*cos(z) + c2*cos(2*z);
                        winCoeffs = SineExpansion(points, 0.375f, -0.5f, 0.125f);
                        break;

                    case Window.Type.Nutall3A:
                        //c0 = 0.40897; c1 = -0.5; c2 = 0.09103;
                        //wc = c0 + c1*cos(z) + c2*cos(2*z);
                        winCoeffs = SineExpansion(points, 0.40897f, -0.5f, 0.09103f);
                        break;

                    case Window.Type.Nutall3B:
                        //c0 = 0.4243801; c1 = -0.4973406; c2 = 0.0782793;
                        //wc = c0 + c1*cos(z) + c2*cos(2*z);
                        winCoeffs = SineExpansion(points, 0.4243801f, -0.4973406f, 0.0782793f);
                        break;

                    case Window.Type.Nutall4:
                        //c0 = 0.3125; c1 = -0.46875; c2 = 0.1875; c3 = -0.03125;
                        //wc = c0 + c1*cos(z) + c2*cos(2*z) + c3*cos(3*z);
                        winCoeffs = SineExpansion(points, 0.3125f, -0.46875f, 0.1875f, -0.03125f);
                        break;

                    case Window.Type.Nutall4A:
                        //c0 = 0.338946; c1 = -0.481973; c2 = 0.161054; c3 = -0.018027;
                        //wc = c0 + c1*cos(z) + c2*cos(2*z) + c3*cos(3*z);
                        winCoeffs = SineExpansion(points, 0.338946f, -0.481973f, 0.161054f, -0.018027f);
                        break;

                    case Window.Type.Nutall4B:
                        //c0 = 0.355768; c1 = -0.487396; c2 = 0.144232; c3 = -0.012604;
                        //wc = c0 + c1*cos(z) + c2*cos(2*z) + c3*cos(3*z);
                        winCoeffs = SineExpansion(points, 0.355768f, -0.487396f, 0.144232f, -0.012604f);
                        break;

                    case Window.Type.SFT3F:
                        //c0 = 0.26526; c1 = -0.5; c2 = 0.23474;
                        //wc = c0 + c1*cos(z) + c2*cos(2*z);
                        winCoeffs = SineExpansion(points, 0.26526f, -0.5f, 0.23474f);
                        break;

                    case Window.Type.SFT4F:
                        //c0 = 0.21706; c1 = -0.42103; c2 = 0.28294; c3 = -0.07897;
                        //wc = c0 + c1*cos(z) + c2*cos(2*z) + c3*cos(3*z);
                        winCoeffs = SineExpansion(points, 0.21706f, -0.42103f, 0.28294f, -0.07897f);
                        break;

                    case Window.Type.SFT5F:
                        //c0 = 0.1881; c1 = -0.36923; c2 = 0.28702; c3 = -0.13077; c4 = 0.02488;
                        //wc = c0 + c1*cos(z) + c2*cos(2*z) + c3*cos(3*z) + c4*cos(4*z);
                        winCoeffs = SineExpansion(points, 0.1881f, -0.36923f, 0.28702f, -0.13077f, 0.02488f);
                        break;

                    case Window.Type.SFT3M:
                        //c0 = 0.28235; c1 = -0.52105; c2 = 0.19659;
                        //wc = c0 + c1*cos(z) + c2*cos(2*z);
                        winCoeffs = SineExpansion(points, 0.28235f, -0.52105f, 0.19659f);
                        break;

                    case Window.Type.SFT4M:
                        //c0 = 0.241906; c1 = -0.460841; c2 = 0.255381; c3 = -0.041872;
                        //wc = c0 + c1*cos(z) + c2*cos(2*z) + c3*cos(3*z);
                        winCoeffs = SineExpansion(points, 0.241906f, -0.460841f, 0.255381f, -0.041872f);
                        break;

                    case Window.Type.SFT5M:
                        //c0 = 0.209671; c1 = -0.407331; c2 = 0.281225; c3 = -0.092669; c4 = 0.0091036;
                        //wc = c0 + c1*cos(z) + c2*cos(2*z) + c3*cos(3*z) + c4*cos(4*z);
                        winCoeffs = SineExpansion(points, 0.209671f, -0.407331f, 0.281225f, -0.092669f, 0.0091036f);
                        break;

                    case Window.Type.FTNI:
                        //wc = (0.2810639 - 0.5208972*cos(z) + 0.1980399*cos(2*z));
                        winCoeffs = SineExpansion(points, 0.2810639f, -0.5208972f, 0.1980399f);
                        break;

                    case Window.Type.FTHP:
                        //wc = 1.0 - 1.912510941*cos(z) + 1.079173272*cos(2*z) - 0.1832630879*cos(3*z);
                        winCoeffs = SineExpansion(points, 1.0f, -1.912510941f, 1.079173272f, -0.1832630879f);
                        break;

                    case Window.Type.HFT70:
                        //wc = 1 - 1.90796*cos(z) + 1.07349*cos(2*z) - 0.18199*cos(3*z);
                        winCoeffs = SineExpansion(points, 1f, -1.90796f, 1.07349f, -0.18199f);
                        break;

                    case Window.Type.FTSRS:
                        //wc = 1.0 - 1.93*cos(z) + 1.29*cos(2*z) - 0.388*cos(3*z) + 0.028*cos(4*z);
                        winCoeffs = SineExpansion(points, 1.0f, -1.93f, 1.29f, -0.388f, 0.028f);
                        break;

                    case Window.Type.HFT90D:
                        //wc = 1 - 1.942604*cos(z) + 1.340318*cos(2*z) - 0.440811*cos(3*z) + 0.043097*cos(4*z);
                        winCoeffs = SineExpansion(points, 1.0f, -1.942604f, 1.340318f, -0.440811f, 0.043097f);
                        break;

                    case Window.Type.HFT95:
                        //wc = 1 - 1.9383379*cos(z) + 1.3045202*cos(2*z) - 0.4028270*cos(3*z) + 0.0350665*cos(4*z);
                        winCoeffs = SineExpansion(points, 1f, -1.9383379f, 1.3045202f, -0.4028270f, 0.0350665f);
                        break;

                    case Window.Type.HFT116D:
                        //wc = 1 - 1.9575375*cos(z) + 1.4780705*cos(2*z) - 0.6367431*cos(3*z) + 0.1228389*cos(4*z) - 0.0066288*cos(5*z);
                        winCoeffs = SineExpansion(points, 1.0f, -1.9575375f, 1.4780705f, -0.6367431f, 0.1228389f, -0.0066288f);
                        break;

                    case Window.Type.HFT144D:
                        //wc = 1 - 1.96760033*cos(z) + 1.57983607*cos(2*z) - 0.81123644*cos(3*z) + 0.22583558*cos(4*z) - 0.02773848*cos(5*z) + 0.00090360*cos(6*z);
                        winCoeffs = SineExpansion(points, 1.0f, -1.96760033f, 1.57983607f, -0.81123644f, 0.22583558f, -0.02773848f, 0.00090360f);
                        break;

                    case Window.Type.HFT169D:
                        //wc = 1 - 1.97441842*cos(z) + 1.65409888*cos(2*z) - 0.95788186*cos(3*z) + 0.33673420*cos(4*z) - 0.06364621*cos(5*z) + 0.00521942*cos(6*z) - 0.00010599*cos(7*z);
                        winCoeffs = SineExpansion(points, 1.0f, -1.97441842f, 1.65409888f, -0.95788186f, 0.33673420f, -0.06364621f, 0.00521942f, -0.00010599f);
                        break;

                    case Window.Type.HFT196D:
                        //wc = 1 - 1.979280420*cos(z) + 1.710288951*cos(2*z) - 1.081629853*cos(3*z)+ 0.448734314*cos(4*z) - 0.112376628*cos(5*z) + 0.015122992*cos(6*z) - 0.000871252*cos(7*z) + 0.000011896*cos(8*z);
                        winCoeffs = SineExpansion(points, 1.0f, -1.979280420f, 1.710288951f, -1.081629853f, 0.448734314f, -0.112376628f, 0.015122992f, -0.000871252f, 0.000011896f);
                        break;

                    case Window.Type.HFT223D:
                        //wc = 1 - 1.98298997309*cos(z) + 1.75556083063*cos(2*z) - 1.19037717712*cos(3*z) + 0.56155440797*cos(4*z) - 0.17296769663*cos(5*z) + 0.03233247087*cos(6*z) - 0.00324954578*cos(7*z) + 0.00013801040*cos(8*z) - 0.00000132725*cos(9*z);
                        winCoeffs = SineExpansion(points, 1.0f, -1.98298997309f, 1.75556083063f, -1.19037717712f, 0.56155440797f, -0.17296769663f, 0.03233247087f, -0.00324954578f, 0.00013801040f, -0.00000132725f);
                        break;

                    case Window.Type.HFT248D:
                        //wc = 1 - 1.985844164102*cos(z) + 1.791176438506*cos(2*z) - 1.282075284005*cos(3*z) + 0.667777530266*cos(4*z) - 0.240160796576*cos(5*z) + 0.056656381764*cos(6*z) - 0.008134974479*cos(7*z) + 0.000624544650*cos(8*z) - 0.000019808998*cos(9*z) + 0.000000132974*cos(10*z);
                        winCoeffs = SineExpansion(points, 1f, -1.985844164102f, 1.791176438506f, -1.282075284005f, 0.667777530266f, -0.240160796576f, 0.056656381764f, -0.008134974479f, 0.000624544650f, -0.000019808998f, 0.000000132974f);
                        break;

                    default:
                        //throw new NotImplementedException("Window type fell through to 'Default'.");
                        break;
                }

                return winCoeffs;
            }

            private static float[] SineExpansion(UInt32 points, float c0, float c1 = 0, float c2 = 0, float c3 = 0, float c4 = 0, float c5 = 0, float c6 = 0, float c7 = 0, float c8 = 0, float c9 = 0, float c10 = 0)
            {
                // z = 2 * pi * (0:N-1)' / N;   // Cosine Vector
                float[] z = new float[points];
                for (UInt32 i = 0; i < points; i++)
                    z[i] = 2.0f * Mathf.PI * i / points;

                float[] winCoeffs = new float[points];

                for (UInt32 i = 0; i < points; i++)
                {
                    float wc = c0;
                    wc += c1 * Mathf.Cos(z[i]);
                    wc += c2 * Mathf.Cos(2.0f * z[i]);
                    wc += c3 * Mathf.Cos(3.0f * z[i]);
                    wc += c4 * Mathf.Cos(4.0f * z[i]);
                    wc += c5 * Mathf.Cos(5.0f * z[i]);
                    wc += c6 * Mathf.Cos(6.0f * z[i]);
                    wc += c7 * Mathf.Cos(7.0f * z[i]);
                    wc += c8 * Mathf.Cos(8.0f * z[i]);
                    wc += c9 * Mathf.Cos(9.0f * z[i]);
                    wc += c10 * Mathf.Cos(10.0f * z[i]);

                    winCoeffs[i] = wc;
                }

                return winCoeffs;
            }

            #endregion

        }

        #endregion


        #region Convert Magnitude format to user friendly formats

        /// <summary>
        /// DFT / FFT Format Conversion Functions
        /// </summary>
        public static class ConvertMagnitude
        {
            /// <summary>
            /// Convert Magnitude FT Result to: Magnitude Squared Format
            /// </summary>
            /// <param name="magnitude"></param>
            /// <returns></returns>
            public static float[] ToMagnitudeSquared(float[] magnitude)
            {
                UInt32 np = (UInt32)magnitude.Length;
                float[] mag2 = new float[np];
                for (UInt32 i = 0; i < np; i++)
                {
                    mag2[i] = magnitude[i] * magnitude[i];
                }

                return mag2;
            }


            /// <summary>
            /// Convert Magnitude FT Result to: Magnitude dBVolts
            /// </summary>
            /// <param name="magnitude"></param>
            /// <returns>float[] array</returns>
            public static float[] ToMagnitudeDBV(float[] magnitude)
            {
                UInt32 np = (UInt32)magnitude.Length;
                float[] magDBV = new float[np];
                for (UInt32 i = 0; i < np; i++)
                {
                    float magVal = magnitude[i];
                    if(magVal <= 0.0f)
                        magVal = float.Epsilon;

                    magDBV[i] = 20.0f * Mathf.Log10(magVal);
                }

                return magDBV;
            }

        }

        #endregion


        #region Convert Magnitude Squared format to user friendly formats

        /// <summary>
        /// DFT / FFT Format Conversion Functions
        /// </summary>
        public static class ConvertMagnitudeSquared
        {

            /// <summary>
            /// Convert Magnitude Squared FFT Result to: Magnitude Vrms
            /// </summary>
            /// <param name="magSquared"></param>
            /// <returns>float[] array</returns>
            public static float[] ToMagnitude(float[] magSquared)
            {
                UInt32 np = (UInt32)magSquared.Length;
                float[] mag = new float[np];
                for (UInt32 i = 0; i < np; i++)
                {
                    mag[i] = Mathf.Sqrt(magSquared[i]);
                }

                return mag;
            }

            /// <summary>
            /// Convert Magnitude Squared FFT Result to: Magnitude dBVolts
            /// </summary>
            /// <param name="magSquared"></param>
            /// <returns>float[] array</returns>
            public static float[] ToMagnitudeDBV(float[] magSquared)
            {
                UInt32 np = (UInt32)magSquared.Length;
                float[] magDBV = new float[np];
                for (UInt32 i = 0; i < np; i++)
                {
                    float magSqVal = magSquared[i];
                    if (magSqVal <= 0.0f)
                        magSqVal = float.Epsilon;

                    magDBV[i] = 10.0f * Mathf.Log10(magSqVal);
                }

                return magDBV;
            }
        }

        #endregion


        #region Convert Complex format to user friendly formats

        /// <summary>
        /// DFT / FFT Format Conversion Functions.
        /// </summary>
        public static class ConvertComplex
        {
            /// <summary>
            /// Convert Complex DFT/FFT Result to: Magnitude Squared V^2 rms
            /// </summary>
            /// <param name="rawFFT"></param>
            /// <returns>float[] MagSquared Format</returns>
            public static float[] ToMagnitudeSquared(Complex[] rawFFT)
            {
                UInt32 np = (UInt32)rawFFT.Length;
                float[] magSquared = new float[np];
                for (UInt32 i = 0; i < np; i++)
                {
                    float mag = rawFFT[i].Magnitude;
                    magSquared[i] = mag * mag;
                }

                return magSquared;
            }


            /// <summary>
            /// Convert Complex DFT/FFT Result to: Magnitude Vrms
            /// </summary>
            /// <param name="rawFFT"></param>
            /// <returns>float[] Magnitude Format (Vrms)</returns>
            public static float[] ToMagnitude(Complex[] rawFFT)
            {
                UInt32 np = (UInt32)rawFFT.Length;
                float[] mag = new float[np];
                for (UInt32 i = 0; i < np; i++)
                {
                    mag[i] = rawFFT[i].Magnitude;
                }

                return mag;
            }


            /// <summary>
            /// Convert Complex DFT/FFT Result to: Log Magnitude dBV
            /// </summary>
            /// <param name="rawFFT"> Complex[] input array"></param>
            /// <returns>float[] Magnitude Format (dBV)</returns>
            public static float[] ToMagnitudeDBV(Complex[] rawFFT)
            {
                UInt32 np = (UInt32)rawFFT.Length;
                float[] mag = new float[np];
                for (UInt32 i = 0; i < np; i++)
                {
                    float magVal = rawFFT[i].Magnitude;

                    if (magVal <= 0.0f)
                        magVal = float.Epsilon;

                    mag[i] = 20.0f * Mathf.Log10(magVal);
                }

                return mag;
            }


            /// <summary>
            /// Convert Complex DFT/FFT Result to: Phase in Degrees
            /// </summary>
            /// <param name="rawFFT"> Complex[] input array"></param>
            /// <returns>float[] Phase (Degrees)</returns>
            public static float[] ToPhaseDegrees(Complex[] rawFFT)
            {
                float sf = 180.0f / Mathf.PI; // Degrees per Radian scale factor

                UInt32 np = (UInt32)rawFFT.Length;
                float[] phase = new float[np];
                for (UInt32 i = 0; i < np; i++)
                {
                    phase[i] = rawFFT[i].Phase * sf;
                }

                return phase;
            }


            /// <summary>
            /// Convert Complex DFT/FFT Result to: Phase in Radians
            /// </summary>
            /// <param name="rawFFT"> Complex[] input array"></param>
            /// <returns>float[] Phase (Degrees)</returns>
            public static float[] ToPhaseRadians(Complex[] rawFFT)
            {
                UInt32 np = (UInt32)rawFFT.Length;
                float[] phase = new float[np];
                for (UInt32 i = 0; i < np; i++)
                {
                    phase[i] = rawFFT[i].Phase;
                }

                return phase;
            }
        }
        #endregion


        #region Analyze Spectrum Data

        /// <summary>
        /// DFT / FFT Output Analysis Functions
        /// </summary>
        public static class Analyze
        {
            /// <summary>
            /// Find the RMS value of a[].
            /// </summary>
            /// <param name="inData"> = of N data points, 0 based.</param>
            /// <param name="startBin"> = Bin to start the counting at (0 based)."></param>
            /// <param name="stopBin"> = Bin FROM END to stop counting at (Max = N - 1)."></param>
            /// <returns>RMS value of input array between start and stop bins.</returns>
            public static float FindRms(float[] a, UInt32 startBin = 10, UInt32 stopBin = 10)
            {
                float sum2 = 0.0f;
                UInt32 actualSumCount = 0;
                UInt32 n = (UInt32)a.Length;
                for (UInt32 i = startBin; i < n - stopBin; i++)
                {
                    sum2 += a[i] * a[i];
                    actualSumCount++;
                }

                float avg2 = sum2 / actualSumCount;
                float rms = Mathf.Sqrt(avg2);

                return rms;
            }

            /// <summary>
            /// Finds the mean of the input array.
            /// </summary>
            /// <param name="inData"> = of N data points, 0 based.</param>
            /// <param name="startBin"> = Bin to start the counting at (0 based)."></param>
            /// <param name="stopBin"> = Bin FROM END to stop counting at (Max = N - 1)."></param>
            /// <returns>Mean value of input array between start and stop bins.</returns>
            public static float FindMean(float[] inData, UInt32 startBin = 10, UInt32 stopBin = 10)
            {
                float sum = 0;
                float n = inData.Length;
                UInt32 actualSumCount = 0;

                for (UInt32 i = startBin; i < n - stopBin; i++)
                {
                    sum += inData[i];
                    actualSumCount++;
                }
                return sum / actualSumCount;
            }


            /// <summary>
            /// Finds the maximum value in an array.
            /// </summary>
            /// <param name="inData"></param>
            /// <returns>Maximum value of input array</returns>
            public static float FindMaxAmplitude(float[] inData)
            {
                float n = inData.Length;
                float maxVal = -1e30f;

                for (UInt32 i = 0; i < n; i++)
                {
                    if (inData[i] > maxVal)
                    {
                        maxVal = inData[i];
                    }
                }

                return maxVal;
            }


            /// <summary>
            /// Finds the position in the inData array where the maximum value happens.
            /// </summary>
            /// <param name="inData"></param>
            /// <returns>Position of maximum value in input array</returns>
            public static UInt32 FindMaxPosition(float[] inData)
            {
                float n = inData.Length;
                float maxVal = -1e30f;
                UInt32 maxIndex = 0;

                for (UInt32 i = 0; i < n; i++)
                {
                    if (inData[i] > maxVal)
                    {
                        maxIndex = i;
                        maxVal = inData[i];
                    }
                }

                return maxIndex;
            }

            /// <summary>
            /// Finds the maximum frequency from the given inData and fSpan arrays.
            /// </summary>
            /// <param name="inData"></param>
            /// <param name="fSpan"></param>
            /// <returns>Maximum frequency from input arrays</returns>
            public static float FindMaxFrequency(float[] inData, float[] fSpan)
            {
                float n = inData.Length;
                float maxVal = -1e30f;
                UInt32 maxIndex = 0;

                for (UInt32 i = 0; i < n; i++)
                {
                    if (inData[i] > maxVal)
                    {
                        maxIndex = i;
                        maxVal = inData[i];
                    }
                }

                return fSpan[maxIndex];
            }


            /// <summary>
            /// Unwraps the phase so that it is continuous, without jumps.
            /// </summary>
            /// <param name="inPhaseDeg">Array of Phase Data from FT in Degrees</param>
            /// <returns>Continuous Phase data</returns>
            public static float[] UnwrapPhaseDegrees(float[] inPhaseDeg)
            {
                UInt32 N = (UInt32)inPhaseDeg.Length;
                float[] unwrappedPhase = new float[N];

                float[] tempInData = new float[N];
                inPhaseDeg.CopyTo(tempInData, 0);

                // First point is unchanged
                unwrappedPhase[0] = tempInData[0];

                for (UInt32 i = 1; i < N; i++)
                {
                    float delta = Mathf.Abs(tempInData[i - 1] - tempInData[i]);
                    if (delta >= 180.0f)
                    {
                        // Phase jump!
                        if (tempInData[i - 1] < 0.0f)
                        {
                            for (UInt32 j = i; j < N; j++)
                                tempInData[j] += -360.0f;
                        }
                        else
                        {
                            for (UInt32 j = i; j < N; j++)
                                tempInData[j] += 360.0f;
                        }
                    }
                    unwrappedPhase[i] = tempInData[i];
                }
                return unwrappedPhase;
            }


            /// <summary>
            /// Unwraps the phase so that it is continuous, without jumps.
            /// </summary>
            /// <param name="inPhaseRad">Array of Phase Data from FT in Radians</param>
            /// <returns>Continuous Phase data</returns>
            public static float[] UnwrapPhaseRadians(float[] inPhaseRad)
            {
                float pi = Mathf.PI;
                float twoPi = Mathf.PI * 2.0f;

                UInt32 N = (UInt32)inPhaseRad.Length;

                float[] tempInData = new float[N];
                inPhaseRad.CopyTo(tempInData, 0);

                float[] unwrappedPhase = new float[N];

                // First point is unchanged
                unwrappedPhase[0] = tempInData[0];

                for (UInt32 i = 1; i < N; i++)
                {
                    float delta = Mathf.Abs(tempInData[i - 1] - tempInData[i]);
                    if (delta >= pi)
                    {
                        // Phase jump!
                        if (tempInData[i - 1] < 0.0f)
                        {
                            for (UInt32 j = i; j < N; j++)
                                tempInData[j] += -twoPi;
                        }
                        else
                        {
                            for (UInt32 j = i; j < N; j++)
                                tempInData[j] += twoPi;
                        }
                    }
                    unwrappedPhase[i] = tempInData[i];
                }
                return unwrappedPhase;
            }
        }

        #endregion


        #region float[] Array Math Operators

        /// <summary>
        /// float[] Array Math Operations (All Static)
        /// </summary>
        public static class Math
        {

            /// <summary>
            /// result[] = a[] * b[]
            /// </summary>
            public static float[] Multiply(float[] a, float[] b)
            {
                UnityEngine.Debug.Assert(a.Length == b.Length, "Length of arrays a[] and b[] must match.");

                float[] result = new float[a.Length];
                for (UInt32 i = 0; i < a.Length; i++)
                    result[i] = a[i] * b[i];

                return result;
            }

            /// <summary>
            /// result[] = a[] * b
            /// </summary>
            public static float[] Multiply(float[] a, float b)
            {
                float[] result = new float[a.Length];
                for (UInt32 i = 0; i < a.Length; i++)
                    result[i] = a[i] * b;

                return result;
            }

            /// <summary>
            /// result[] = a[] + b[]
            /// </summary>
            public static float[] Add(float[] a, float[] b)
            {
				UnityEngine.Debug.Assert(a.Length == b.Length, "Length of arrays a[] and b[] must match.");

                float[] result = new float[a.Length];
                for (UInt32 i = 0; i < a.Length; i++)
                    result[i] = a[i] + b[i];

                return result;
            }

            /// <summary>
            /// result[] = a[] + b
            /// </summary>
            public static float[] Add(float[] a, float b)
            {
                float[] result = new float[a.Length];
                for (UInt32 i = 0; i < a.Length; i++)
                    result[i] = a[i] + b;

                return result;
            }

            /// <summary>
            /// result[] = a[] - b[]
            /// </summary>
            public static float[] Subtract(float[] a, float[] b)
            {
				UnityEngine.Debug.Assert(a.Length == b.Length, "Length of arrays a[] and b[] must match.");

                float[] result = new float[a.Length];
                for (UInt32 i = 0; i < a.Length; i++)
                    result[i] = a[i] - b[i];

                return result;
            }

            /// <summary>
            /// result[] = a[] - b
            /// </summary>
            public static float[] Subtract(float[] a, float b)
            {
                float[] result = new float[a.Length];
                for (UInt32 i = 0; i < a.Length; i++)
                    result[i] = a[i] - b;

                return result;
            }

            /// <summary>
            /// result[] = a[] / b[]
            /// </summary>
            public static float[] Divide(float[] a, float[] b)
            {
				UnityEngine.Debug.Assert(a.Length == b.Length, "Length of arrays a[] and b[] must match.");

                float[] result = new float[a.Length];
                for (UInt32 i = 0; i < a.Length; i++)
                    result[i] = a[i] / b[i];

                return result;
            }

            /// <summary>
            /// result[] = a[] / b
            /// </summary>
            public static float[] Divide(float[] a, float b)
            {
                float[] result = new float[a.Length];
                for (UInt32 i = 0; i < a.Length; i++)
                    result[i] = a[i] / b;

                return result;
            }

            /// <summary>
            /// Square root of a[].
            /// </summary>
            public static float[] Sqrt(float[] a)
            {
                float[] result = new float[a.Length];
                for (UInt32 i = 0; i < a.Length; i++)
                    result[i] = Mathf.Sqrt(a[i]);

                return result;
            }

            /// <summary>
            /// Squares a[].
            /// </summary>
            public static float[] Square(float[] a)
            {
                float[] result = new float[a.Length];
                for (UInt32 i = 0; i < a.Length; i++)
                    result[i] = a[i] * a[i];

                return result;
            }

            /// <summary>
            /// Log10 a[].
            /// </summary>
            public static float[] Log10(float[] a)
            {
                float[] result = new float[a.Length];
                for (UInt32 i = 0; i < a.Length; i++)
                {
                    float val = a[i];
                    if (val <= 0.0f)
                        val = float.Epsilon;

                    result[i] = Mathf.Log10(val);
                }

                return result;
            }

            /// <summary>
            /// Removes mean value from a[].
            /// </summary>
            public static float[] RemoveMean(float[] a)
            {
                float sum = 0.0f;
                for (UInt32 i = 0; i < a.Length; i++)
                    sum += a[i];

                float mean = sum / a.Length;

                return (DSP.Math.Subtract(a, mean));
            }

        }

        #endregion

    }
    #endregion

}
