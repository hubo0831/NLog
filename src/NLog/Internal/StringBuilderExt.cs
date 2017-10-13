// 
// Copyright (c) 2004-2017 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen
// 
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without 
// modification, are permitted provided that the following conditions 
// are met:
// 
// * Redistributions of source code must retain the above copyright notice, 
//   this list of conditions and the following disclaimer. 
// 
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution. 
// 
// * Neither the name of Jaroslaw Kowalski nor the names of its 
//   contributors may be used to endorse or promote products derived from this
//   software without specific prior written permission. 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
// ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF 
// THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.IO;
using System.Text;
using NLog.Config;

namespace NLog.Internal
{
    /// <summary>
    /// Helpers for <see cref="StringBuilder"/>, which is used in e.g. layout renderers.
    /// </summary>
    internal static class StringBuilderExt
    {
        /// <summary>
        /// Renders the specified log event context item and appends it to the specified <see cref="StringBuilder" />.
        /// </summary>
        /// <param name="builder">append to this</param>
        /// <param name="value">value to be appended</param>
        /// <param name="format">formatstring. If @, then serialize the value with the Default JsonConverter.</param>
        /// <param name="formatProvider">provider, for example culture</param>
        public static void AppendFormattedValue(this StringBuilder builder, object value, string format, IFormatProvider formatProvider)
        {
            string stringValue = value as string;
            if (stringValue != null && string.IsNullOrEmpty(format))
            {
                builder.Append(value);  // Avoid automatic quotes
            }
            else if (value != null || !string.IsNullOrEmpty(format))
            {
                MessageTemplates.ValueSerializer.Instance.SerializeObject(value, format, formatProvider, builder);
            }
        }

        /// <summary>
        /// Appends int without using culture, and most importantly without garbage
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value">value to append</param>
        public static void AppendInvariant(this StringBuilder builder, int value)
        {
            // Deal with negative numbers
            if (value < 0)
            {
                builder.Append('-');
                uint uint_value = UInt32.MaxValue - ((uint)value) + 1; //< This is to deal with Int32.MinValue
                AppendInvariant(builder, uint_value);
            }
            else
            {
                AppendInvariant(builder, (uint)value);
            }
        }

        /// <summary>
        /// Appends uint without using culture, and most importantly without garbage
        /// 
        /// Credits Gavin Pugh  - http://www.gavpugh.com/2010/04/01/xnac-avoiding-garbage-when-working-with-stringbuilder/
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value">value to append</param>
        public static void AppendInvariant(this StringBuilder builder, uint value)
        {
            if (value == 0)
            {
                builder.Append('0');
                return;
            }

            // Calculate length of integer when written out
            int length = 0;
            uint length_calc = value;

            do
            {
                length_calc /= 10;
                length++;
            }
            while (length_calc > 0);

            // Pad out space for writing.
            builder.Append('0', length);

            int strpos = builder.Length;

            // We're writing backwards, one character at a time.
            while (length > 0)
            {
                strpos--;

                // Lookup from static char array, to cover hex values too
                builder[strpos] = charToInt[value % 10];

                value /= 10;
                length--;
            }
        }
        private static readonly char[] charToInt = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

        /// <summary>
        /// Clears the provider StringBuilder
        /// </summary>
        /// <param name="builder"></param>
        public static void ClearBuilder(this StringBuilder builder)
        {
#if !SILVERLIGHT && !NET3_5
            builder.Clear();
#else
            builder.Length = 0;
#endif
        }

        /// <summary>
        /// Copies the contents of the StringBuilder to the MemoryStream using the specified encoding (Without BOM/Preamble)
        /// </summary>
        /// <param name="builder">StringBuilder source</param>
        /// <param name="ms">MemoryStream destination</param>
        /// <param name="encoding">Encoding used for converter string into byte-stream</param>
        /// <param name="transformBuffer">Helper char-buffer to minimize memory allocations</param>
        public static void CopyToStream(this StringBuilder builder, MemoryStream ms, Encoding encoding, char[] transformBuffer)
        {
#if !SILVERLIGHT
            if (transformBuffer != null)
            {
                int charCount = 0;
                int byteCount = 0;
                for (int i = 0; i < builder.Length; i += transformBuffer.Length)
                {
                    charCount = Math.Min(builder.Length - i, transformBuffer.Length);
                    builder.CopyTo(i, transformBuffer, 0, charCount);
                    byteCount = encoding.GetMaxByteCount(charCount);
                    ms.SetLength(ms.Position + byteCount);
                    byteCount = encoding.GetBytes(transformBuffer, 0, charCount, ms.GetBuffer(), (int)ms.Position);
                    if ((ms.Position += byteCount) != ms.Length)
                    {
                        ms.SetLength(ms.Position);
                    }
                }
            }
            else
#endif
            {
                // Faster than MemoryStream, but generates garbage
                var str = builder.ToString();
                byte[] bytes = encoding.GetBytes(str);
                ms.Write(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Append a number and pad with 0 to 2 digits
        /// </summary>
        /// <param name="builder">append to this</param>
        /// <param name="number">the number</param>
        internal static void Append2DigitsZeroPadded(this StringBuilder builder, int number)
        {
            builder.Append((char)((number / 10) + '0'));
            builder.Append((char)((number % 10) + '0'));
        }

        /// <summary>
        /// Append a number and pad with 0 to 4 digits
        /// </summary>
        /// <param name="builder">append to this</param>
        /// <param name="number">the number</param>
        internal static void Append4DigitsZeroPadded(this StringBuilder builder, int number)
        {
            builder.Append((char)(((number / 1000) % 10) + '0'));
            builder.Append((char)(((number / 100) % 10) + '0'));
            builder.Append((char)(((number / 10) % 10) + '0'));
            builder.Append((char)(((number / 1) % 10) + '0'));
        }
    }
}
