// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*=========================================================================
**
** Class: Complex
**
**
** Purpose: 
** This feature is intended to create Complex Number as a type 
** that can be a part of the .NET framework (base class libraries).  
** A complex number z is a number of the form z = x + yi, where x and y 
** are real numbers, and i is the imaginary unit, with the property i2= -1.
**
**
===========================================================================*/

using System;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace System.Numerics {

#if !SILVERLIGHT
    [Serializable]
#endif // !SILVERLIGHT
    public struct Complex : IEquatable<Complex>, IFormattable {

        // --------------SECTION: Private Data members ----------- //

        private float m_real;
        private float m_imaginary;

        // ---------------SECTION: Necessary Constants ----------- //

        private const float LOG_10_INV = 0.43429448190325f;


        // --------------SECTION: Public Properties -------------- //

        public float Real {
            get {
                return m_real;
            }
        }

        public float Imaginary {
            get {
                return m_imaginary;
            }
        }

        public float Magnitude {
            get {
                return Complex.Abs(this);
            }
        }

        public float Phase {
            get {
                return Mathf.Atan2(m_imaginary, m_real);
            }
        }

        // --------------SECTION: Attributes -------------- //

        public static readonly Complex Zero = new Complex(0.0f, 0.0f);
        public static readonly Complex One = new Complex(1.0f, 0.0f);
        public static readonly Complex ImaginaryOne = new Complex(0.0f, 1.0f);

        // --------------SECTION: Constructors and factory methods -------------- //

        public Complex(float real, float imaginary)  /* Constructor to create a complex number with rectangular co-ordinates  */
        {
            this.m_real = real;
            this.m_imaginary = imaginary;
        }

        public static Complex FromPolarCoordinates(float magnitude, float phase) /* Factory method to take polar inputs and create a Complex object */
        {
            return new Complex((magnitude * Mathf.Cos(phase)), (magnitude * Mathf.Sin(phase)));
        }

        public static Complex Negate(Complex value) {
            return -value;
        }

        public static Complex Add(Complex left, Complex right) {
            return left + right;
        }

        public static Complex Subtract(Complex left, Complex right) {
            return left - right;
        }

        public static Complex Multiply(Complex left, Complex right) {
            return left * right;
        }

        public static Complex Divide(Complex dividend, Complex divisor) {
            return dividend / divisor;
        }

        // --------------SECTION: Arithmetic Operator(unary) Overloading -------------- //
        public static Complex operator -(Complex value)  /* Unary negation of a complex number */
        {

            return (new Complex((-value.m_real), (-value.m_imaginary)));
        }

        // --------------SECTION: Arithmetic Operator(binary) Overloading -------------- //       
        public static Complex operator +(Complex left, Complex right) {
            return (new Complex((left.m_real + right.m_real), (left.m_imaginary + right.m_imaginary)));

        }

        public static Complex operator -(Complex left, Complex right) {
            return (new Complex((left.m_real - right.m_real), (left.m_imaginary - right.m_imaginary)));
        }

        public static Complex operator *(Complex left, Complex right) {
            // Multiplication:  (a + bi)(c + di) = (ac -bd) + (bc + ad)i
            float result_Realpart = (left.m_real * right.m_real) - (left.m_imaginary * right.m_imaginary);
            float result_Imaginarypart = (left.m_imaginary * right.m_real) + (left.m_real * right.m_imaginary);
            return (new Complex(result_Realpart, result_Imaginarypart));
        }

        public static Complex operator /(Complex left, Complex right) {
            // Division : Smith's formula.
            float a = left.m_real;
            float b = left.m_imaginary;
            float c = right.m_real;
            float d = right.m_imaginary;

            if (Mathf.Abs(d) < Mathf.Abs(c)) {
                float doc = d / c;
                return new Complex((a + b * doc) / (c + d * doc), (b - a * doc) / (c + d * doc));
            } else {
                float cod = c / d;
                return new Complex((b + a * cod) / (d + c * cod), (-a + b * cod) / (d + c * cod));
            }
        }


        // --------------SECTION: Other arithmetic operations  -------------- //

        public static float Abs(Complex value) {

            if(float.IsInfinity(value.m_real) || float.IsInfinity(value.m_imaginary)) {
                return float.PositiveInfinity;
            }

            // |value| == sqrt(a^2 + b^2)
            // sqrt(a^2 + b^2) == a/a * sqrt(a^2 + b^2) = a * sqrt(a^2/a^2 + b^2/a^2)
            // Using the above we can factor out the square of the larger component to dodge overflow.


            float c = Mathf.Abs(value.m_real);
            float d = Mathf.Abs(value.m_imaginary);

            if (c > d) {
                float r = d / c;
                return c * Mathf.Sqrt(1.0f + r * r);
            } else if (d == 0.0f) {
                return c;  // c is either 0.0f or NaN
            } else {
                float r = c / d;
                return d * Mathf.Sqrt(1.0f + r * r);
            }
        }
        public static Complex Conjugate(Complex value) {
            // Conjugate of a Complex number: the conjugate of x+i*y is x-i*y 

            return (new Complex(value.m_real, (-value.m_imaginary)));

        }
        public static Complex Reciprocal(Complex value) {
            // Reciprocal of a Complex number : the reciprocal of x+i*y is 1/(x+i*y)
            if ((value.m_real == 0) && (value.m_imaginary == 0)) {
                return Complex.Zero;
            }

            return Complex.One / value;
        }

        // --------------SECTION: Comparison Operator(binary) Overloading -------------- //

        public static bool operator ==(Complex left, Complex right) {
            return ((left.m_real == right.m_real) && (left.m_imaginary == right.m_imaginary));


        }
        public static bool operator !=(Complex left, Complex right) {
            return ((left.m_real != right.m_real) || (left.m_imaginary != right.m_imaginary));

        }

        // --------------SECTION: Comparison operations (methods implementing IEquatable<ComplexNumber>,IComparable<ComplexNumber>) -------------- //

        public override bool Equals(object obj) {
            if (!(obj is Complex)) return false;
            return this == ((Complex)obj);
        }
        public bool Equals(Complex value) {
            return ((this.m_real.Equals(value.m_real)) && (this.m_imaginary.Equals(value.m_imaginary)));

        }

        // --------------SECTION: Type-casting basic numeric data-types to ComplexNumber  -------------- //

        public static implicit operator Complex(Int16 value) {
            return (new Complex(value, 0));
        }
        public static implicit operator Complex(Int32 value) {
            return (new Complex(value, 0));
        }
        public static implicit operator Complex(Int64 value) {
            return (new Complex(value, 0));
        }
        //[CLSCompliant(false)]
        public static implicit operator Complex(UInt16 value) {
            return (new Complex(value, 0));
        }
        //[CLSCompliant(false)]
        public static implicit operator Complex(UInt32 value) {
            return (new Complex(value, 0));
        }
        //[CLSCompliant(false)]
        public static implicit operator Complex(UInt64 value) {
            return (new Complex(value, 0));
        }
        //[CLSCompliant(false)]
        public static implicit operator Complex(SByte value) {
            return (new Complex(value, 0));
        }
        public static implicit operator Complex(Byte value) {
            return (new Complex(value, 0));
        }
        public static implicit operator Complex(Single value) {
            return (new Complex(value, 0));
        }
        /*public static implicit operator Complex(double value) {
            return (new Complex(value, 0));
        }*/
        public static explicit operator Complex(Decimal value) {
            return (new Complex((float)value, 0));
        }


        // --------------SECTION: Formattig/Parsing options  -------------- //

        public override String ToString() {
            return (String.Format(CultureInfo.CurrentCulture, "({0}, {1})", this.m_real, this.m_imaginary));
        }

        public String ToString(String format) {
            return (String.Format(CultureInfo.CurrentCulture, "({0}, {1})", this.m_real.ToString(format, CultureInfo.CurrentCulture), this.m_imaginary.ToString(format, CultureInfo.CurrentCulture)));
        }

        public String ToString(IFormatProvider provider) {
            return (String.Format(provider, "({0}, {1})", this.m_real, this.m_imaginary));
        }

        public String ToString(String format, IFormatProvider provider) {
            return (String.Format(provider, "({0}, {1})", this.m_real.ToString(format, provider), this.m_imaginary.ToString(format, provider)));
        }


        public override Int32 GetHashCode() {
            Int32 n1 = 99999997;
            Int32 hash_real = this.m_real.GetHashCode() % n1;
            Int32 hash_imaginary = this.m_imaginary.GetHashCode();
            Int32 final_hashcode = hash_real ^ hash_imaginary;
            return (final_hashcode);
        }



        // --------------SECTION: Trigonometric operations (methods implementing ITrigonometric)  -------------- //

        public static Complex Sin(Complex value) {
            float a = value.m_real;
            float b = value.m_imaginary;
			return new Complex(Mathf.Sin(a) * HyperbolicMath.Cosh(b), Mathf.Cos(a) * HyperbolicMath.Sinh(b));
        }

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Sinh", Justification = "Microsoft: Existing Name")]
        public static Complex Sinh(Complex value) /* Hyperbolic sin */
        {
            float a = value.m_real;
            float b = value.m_imaginary;
			return new Complex(HyperbolicMath.Sinh(a) * Mathf.Cos(b), HyperbolicMath.Cosh(a) * Mathf.Sin(b));

        }
        public static Complex Asin(Complex value) /* Arcsin */
        {
            return (-ImaginaryOne) * Log(ImaginaryOne * value + Sqrt(One - value * value));
        }

        public static Complex Cos(Complex value) {
            float a = value.m_real;
			float b = value.m_imaginary;
			return new Complex(Mathf.Cos(a) * HyperbolicMath.Cosh(b), - (Mathf.Sin(a) * HyperbolicMath.Sinh(b)));
        }

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Cosh", Justification = "Microsoft: Existing Name")]
        public static Complex Cosh(Complex value) /* Hyperbolic cos */
        {
            float a = value.m_real;
            float b = value.m_imaginary;
			return new Complex(HyperbolicMath.Cosh(a) * Mathf.Cos(b), HyperbolicMath.Sinh(a) * Mathf.Sin(b));
        }
        public static Complex Acos(Complex value) /* Arccos */
        {
            return (-ImaginaryOne) * Log(value + ImaginaryOne*Sqrt(One - (value * value)));

        }
        public static Complex Tan(Complex value) {
            return (Sin(value) / Cos(value));
        }

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Tanh", Justification = "Microsoft: Existing Name")]
        public static Complex Tanh(Complex value) /* Hyperbolic tan */
        {
            return (Sinh(value) / Cosh(value));
        }
        public static Complex Atan(Complex value) /* Arctan */
        {
            Complex Two = new Complex(2.0f, 0.0f);
            return (ImaginaryOne / Two) * (Log(One - ImaginaryOne * value) - Log(One + ImaginaryOne * value));
        }

        // --------------SECTION: Other numerical functions  -------------- //        

        public static Complex Log(Complex value) /* Log of the complex number value to the base of 'e' */
        {
            return (new Complex((Mathf.Log(Abs(value))), (Mathf.Atan2(value.m_imaginary, value.m_real))));

        }
        public static Complex Log(Complex value, float baseValue) /* Log of the complex number to a the base of a float */
        {
            return (Log(value) / Log(baseValue));
        }
        public static Complex Log10(Complex value) /* Log to the base of 10 of the complex number */
        {

            Complex temp_log = Log(value);
            return (Scale(temp_log, (float)LOG_10_INV));

        }
        public static Complex Exp(Complex value) /* The complex number raised to e */
        {
            float temp_factor = Mathf.Exp(value.m_real);
            float result_re = temp_factor * Mathf.Cos(value.m_imaginary);
            float result_im = temp_factor * Mathf.Sin(value.m_imaginary);
            return (new Complex(result_re, result_im));
        }

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Sqrt", Justification = "Microsoft: Existing Name")]
        public static Complex Sqrt(Complex value) /* Square root ot the complex number */
        {
            return Complex.FromPolarCoordinates(Mathf.Sqrt(value.Magnitude), value.Phase / 2.0f);
        }

        public static Complex Pow(Complex value, Complex power) /* A complex number raised to another complex number */
        {

            if (power == Complex.Zero) {
                return Complex.One;
            }

            if (value == Complex.Zero) {
                return Complex.Zero;
            }

            float a = value.m_real;
            float b = value.m_imaginary;
            float c = power.m_real;
            float d = power.m_imaginary;

            float rho = Complex.Abs(value);
            float theta = Mathf.Atan2(b, a);
            float newRho = c * theta + d * Mathf.Log(rho);

			float t = Mathf.Pow(rho, c) * Mathf.Pow((float)Math.E, -d * theta);

            return new Complex(t * Mathf.Cos(newRho), t * Mathf.Sin(newRho));
        }

        public static Complex Pow(Complex value, float power) // A complex number raised to a real number 
        {
            return Pow(value, new Complex(power, 0));
        }



        //--------------- SECTION: Private member functions for internal use -----------------------------------//

        private static Complex Scale(Complex value, float factor) {

            float result_re = factor * value.m_real;
            float result_im = factor * value.m_imaginary;
            return (new Complex(result_re, result_im));
        }

    }
}