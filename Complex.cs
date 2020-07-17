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

namespace DSPLib
{ 
#if !SILVERLIGHT
    [Serializable]
#endif // !SILVERLIGHT
    public struct ComplexFloat : IEquatable<ComplexFloat>, IFormattable {

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
                return ComplexFloat.Abs(this);
            }
        }

        public float Phase {
            get {
                return Mathf.Atan2(m_imaginary, m_real);
            }
        }

        // --------------SECTION: Attributes -------------- //

        public static readonly ComplexFloat Zero = new ComplexFloat(0.0f, 0.0f);
        public static readonly ComplexFloat One = new ComplexFloat(1.0f, 0.0f);
        public static readonly ComplexFloat ImaginaryOne = new ComplexFloat(0.0f, 1.0f);

        // --------------SECTION: Constructors and factory methods -------------- //

        public ComplexFloat(float real, float imaginary)  /* Constructor to create a complex number with rectangular co-ordinates  */
        {
            this.m_real = real;
            this.m_imaginary = imaginary;
        }

        public static ComplexFloat FromPolarCoordinates(float magnitude, float phase) /* Factory method to take polar inputs and create a Complex object */
        {
            return new ComplexFloat((magnitude * Mathf.Cos(phase)), (magnitude * Mathf.Sin(phase)));
        }

        public static ComplexFloat Negate(ComplexFloat value) {
            return -value;
        }

        public static ComplexFloat Add(ComplexFloat left, ComplexFloat right) {
            return left + right;
        }

        public static ComplexFloat Subtract(ComplexFloat left, ComplexFloat right) {
            return left - right;
        }

        public static ComplexFloat Multiply(ComplexFloat left, ComplexFloat right) {
            return left * right;
        }

        public static ComplexFloat Divide(ComplexFloat dividend, ComplexFloat divisor) {
            return dividend / divisor;
        }

        // --------------SECTION: Arithmetic Operator(unary) Overloading -------------- //
        public static ComplexFloat operator -(ComplexFloat value)  /* Unary negation of a complex number */
        {

            return (new ComplexFloat((-value.m_real), (-value.m_imaginary)));
        }

        // --------------SECTION: Arithmetic Operator(binary) Overloading -------------- //       
        public static ComplexFloat operator +(ComplexFloat left, ComplexFloat right) {
            return (new ComplexFloat((left.m_real + right.m_real), (left.m_imaginary + right.m_imaginary)));

        }

        public static ComplexFloat operator -(ComplexFloat left, ComplexFloat right) {
            return (new ComplexFloat((left.m_real - right.m_real), (left.m_imaginary - right.m_imaginary)));
        }

        public static ComplexFloat operator *(ComplexFloat left, ComplexFloat right) {
            // Multiplication:  (a + bi)(c + di) = (ac -bd) + (bc + ad)i
            float result_Realpart = (left.m_real * right.m_real) - (left.m_imaginary * right.m_imaginary);
            float result_Imaginarypart = (left.m_imaginary * right.m_real) + (left.m_real * right.m_imaginary);
            return (new ComplexFloat(result_Realpart, result_Imaginarypart));
        }

        public static ComplexFloat operator /(ComplexFloat left, ComplexFloat right) {
            // Division : Smith's formula.
            float a = left.m_real;
            float b = left.m_imaginary;
            float c = right.m_real;
            float d = right.m_imaginary;

            if (Mathf.Abs(d) < Mathf.Abs(c)) {
                float doc = d / c;
                return new ComplexFloat((a + b * doc) / (c + d * doc), (b - a * doc) / (c + d * doc));
            } else {
                float cod = c / d;
                return new ComplexFloat((b + a * cod) / (d + c * cod), (-a + b * cod) / (d + c * cod));
            }
        }


        // --------------SECTION: Other arithmetic operations  -------------- //

        public static float Abs(ComplexFloat value) {

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
        public static ComplexFloat Conjugate(ComplexFloat value) {
            // Conjugate of a Complex number: the conjugate of x+i*y is x-i*y 

            return (new ComplexFloat(value.m_real, (-value.m_imaginary)));

        }
        public static ComplexFloat Reciprocal(ComplexFloat value) {
            // Reciprocal of a Complex number : the reciprocal of x+i*y is 1/(x+i*y)
            if ((value.m_real == 0) && (value.m_imaginary == 0)) {
                return ComplexFloat.Zero;
            }

            return ComplexFloat.One / value;
        }

        // --------------SECTION: Comparison Operator(binary) Overloading -------------- //

        public static bool operator ==(ComplexFloat left, ComplexFloat right) {
            return ((left.m_real == right.m_real) && (left.m_imaginary == right.m_imaginary));


        }
        public static bool operator !=(ComplexFloat left, ComplexFloat right) {
            return ((left.m_real != right.m_real) || (left.m_imaginary != right.m_imaginary));

        }

        // --------------SECTION: Comparison operations (methods implementing IEquatable<ComplexNumber>,IComparable<ComplexNumber>) -------------- //

        public override bool Equals(object obj) {
            if (!(obj is ComplexFloat)) return false;
            return this == ((ComplexFloat)obj);
        }
        public bool Equals(ComplexFloat value) {
            return ((this.m_real.Equals(value.m_real)) && (this.m_imaginary.Equals(value.m_imaginary)));

        }

        // --------------SECTION: Type-casting basic numeric data-types to ComplexNumber  -------------- //

        public static implicit operator ComplexFloat(Int16 value) {
            return (new ComplexFloat(value, 0));
        }
        public static implicit operator ComplexFloat(Int32 value) {
            return (new ComplexFloat(value, 0));
        }
        public static implicit operator ComplexFloat(Int64 value) {
            return (new ComplexFloat(value, 0));
        }
        //[CLSCompliant(false)]
        public static implicit operator ComplexFloat(UInt16 value) {
            return (new ComplexFloat(value, 0));
        }
        //[CLSCompliant(false)]
        public static implicit operator ComplexFloat(UInt32 value) {
            return (new ComplexFloat(value, 0));
        }
        //[CLSCompliant(false)]
        public static implicit operator ComplexFloat(UInt64 value) {
            return (new ComplexFloat(value, 0));
        }
        //[CLSCompliant(false)]
        public static implicit operator ComplexFloat(SByte value) {
            return (new ComplexFloat(value, 0));
        }
        public static implicit operator ComplexFloat(Byte value) {
            return (new ComplexFloat(value, 0));
        }
        public static implicit operator ComplexFloat(Single value) {
            return (new ComplexFloat(value, 0));
        }
        /*public static implicit operator Complex(double value) {
            return (new Complex(value, 0));
        }*/
        public static explicit operator ComplexFloat(Decimal value) {
            return (new ComplexFloat((float)value, 0));
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

        public static ComplexFloat Sin(ComplexFloat value) {
            float a = value.m_real;
            float b = value.m_imaginary;
			return new ComplexFloat(Mathf.Sin(a) * HyperbolicMath.Cosh(b), Mathf.Cos(a) * HyperbolicMath.Sinh(b));
        }

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Sinh", Justification = "Microsoft: Existing Name")]
        public static ComplexFloat Sinh(ComplexFloat value) /* Hyperbolic sin */
        {
            float a = value.m_real;
            float b = value.m_imaginary;
			return new ComplexFloat(HyperbolicMath.Sinh(a) * Mathf.Cos(b), HyperbolicMath.Cosh(a) * Mathf.Sin(b));

        }
        public static ComplexFloat Asin(ComplexFloat value) /* Arcsin */
        {
            return (-ImaginaryOne) * Log(ImaginaryOne * value + Sqrt(One - value * value));
        }

        public static ComplexFloat Cos(ComplexFloat value) {
            float a = value.m_real;
			float b = value.m_imaginary;
			return new ComplexFloat(Mathf.Cos(a) * HyperbolicMath.Cosh(b), - (Mathf.Sin(a) * HyperbolicMath.Sinh(b)));
        }

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Cosh", Justification = "Microsoft: Existing Name")]
        public static ComplexFloat Cosh(ComplexFloat value) /* Hyperbolic cos */
        {
            float a = value.m_real;
            float b = value.m_imaginary;
			return new ComplexFloat(HyperbolicMath.Cosh(a) * Mathf.Cos(b), HyperbolicMath.Sinh(a) * Mathf.Sin(b));
        }
        public static ComplexFloat Acos(ComplexFloat value) /* Arccos */
        {
            return (-ImaginaryOne) * Log(value + ImaginaryOne*Sqrt(One - (value * value)));

        }
        public static ComplexFloat Tan(ComplexFloat value) {
            return (Sin(value) / Cos(value));
        }

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Tanh", Justification = "Microsoft: Existing Name")]
        public static ComplexFloat Tanh(ComplexFloat value) /* Hyperbolic tan */
        {
            return (Sinh(value) / Cosh(value));
        }
        public static ComplexFloat Atan(ComplexFloat value) /* Arctan */
        {
            ComplexFloat Two = new ComplexFloat(2.0f, 0.0f);
            return (ImaginaryOne / Two) * (Log(One - ImaginaryOne * value) - Log(One + ImaginaryOne * value));
        }

        // --------------SECTION: Other numerical functions  -------------- //        

        public static ComplexFloat Log(ComplexFloat value) /* Log of the complex number value to the base of 'e' */
        {
            return (new ComplexFloat((Mathf.Log(Abs(value))), (Mathf.Atan2(value.m_imaginary, value.m_real))));

        }
        public static ComplexFloat Log(ComplexFloat value, float baseValue) /* Log of the complex number to a the base of a float */
        {
            return (Log(value) / Log(baseValue));
        }
        public static ComplexFloat Log10(ComplexFloat value) /* Log to the base of 10 of the complex number */
        {

            ComplexFloat temp_log = Log(value);
            return (Scale(temp_log, (float)LOG_10_INV));

        }
        public static ComplexFloat Exp(ComplexFloat value) /* The complex number raised to e */
        {
            float temp_factor = Mathf.Exp(value.m_real);
            float result_re = temp_factor * Mathf.Cos(value.m_imaginary);
            float result_im = temp_factor * Mathf.Sin(value.m_imaginary);
            return (new ComplexFloat(result_re, result_im));
        }

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Sqrt", Justification = "Microsoft: Existing Name")]
        public static ComplexFloat Sqrt(ComplexFloat value) /* Square root ot the complex number */
        {
            return ComplexFloat.FromPolarCoordinates(Mathf.Sqrt(value.Magnitude), value.Phase / 2.0f);
        }

        public static ComplexFloat Pow(ComplexFloat value, ComplexFloat power) /* A complex number raised to another complex number */
        {

            if (power == ComplexFloat.Zero) {
                return ComplexFloat.One;
            }

            if (value == ComplexFloat.Zero) {
                return ComplexFloat.Zero;
            }

            float a = value.m_real;
            float b = value.m_imaginary;
            float c = power.m_real;
            float d = power.m_imaginary;

            float rho = ComplexFloat.Abs(value);
            float theta = Mathf.Atan2(b, a);
            float newRho = c * theta + d * Mathf.Log(rho);

			float t = Mathf.Pow(rho, c) * Mathf.Pow((float)Math.E, -d * theta);

            return new ComplexFloat(t * Mathf.Cos(newRho), t * Mathf.Sin(newRho));
        }

        public static ComplexFloat Pow(ComplexFloat value, float power) // A complex number raised to a real number 
        {
            return Pow(value, new ComplexFloat(power, 0));
        }



        //--------------- SECTION: Private member functions for internal use -----------------------------------//

        private static ComplexFloat Scale(ComplexFloat value, float factor) {

            float result_re = factor * value.m_real;
            float result_im = factor * value.m_imaginary;
            return (new ComplexFloat(result_re, result_im));
        }

    }
}