/*
 * Copyright (C) 2010 ZXing authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace com.google.zxing.common
{
   /// <summary>
   /// Common string-related functions.
   /// </summary>
   /// <author>Sean Owen</author>
   /// <author>Alex Dupre</author>
   public sealed class StringUtils
   {
#if (WINDOWS_PHONE70 || WINDOWS_PHONE71 || SILVERLIGHT4)
      private static String PLATFORM_DEFAULT_ENCODING = "UTF-8";
#else
      private static String PLATFORM_DEFAULT_ENCODING = Encoding.Default.WebName;
#endif
      public static String SHIFT_JIS = "SJIS";
      public static String GB2312 = "GB2312";
      private static String EUC_JP = "EUC-JP";
      private static String UTF8 = "UTF-8";
      private static String ISO88591 = "ISO8859-1";
      private static bool ASSUME_SHIFT_JIS = 
         String.Compare(SHIFT_JIS, PLATFORM_DEFAULT_ENCODING, StringComparison.OrdinalIgnoreCase) == 0 ||
         String.Compare(EUC_JP, PLATFORM_DEFAULT_ENCODING, StringComparison.OrdinalIgnoreCase) == 0;

      private StringUtils() { }

      /**
       * @param bytes bytes encoding a string, whose encoding should be guessed
       * @param hints decode hints if applicable
       * @return name of guessed encoding; at the moment will only guess one of:
       *  {@link #SHIFT_JIS}, {@link #UTF8}, {@link #ISO88591}, or the platform
       *  default encoding if none of these can possibly be correct
       */
      public static String guessEncoding(sbyte[] bytes, IDictionary<DecodeHintType, object> hints)
      {
         if (hints != null && hints.ContainsKey(DecodeHintType.CHARACTER_SET))
         {
            String characterSet = (String)hints[DecodeHintType.CHARACTER_SET];
            if (characterSet != null)
            {
               return characterSet;
            }
         }
         // Does it start with the UTF-8 byte order mark? then guess it's UTF-8
         if (bytes.Length > 3 &&
             bytes[0] == -17 /*0xEF*/ &&
             bytes[1] == -69 /*0xBB*/ &&
             bytes[2] == -65 /*0xBF*/)
         {
            return UTF8;
         }
         // For now, merely tries to distinguish ISO-8859-1, UTF-8 and Shift_JIS,
         // which should be by far the most common encodings. ISO-8859-1
         // should not have bytes in the 0x80 - 0x9F range, while Shift_JIS
         // uses this as a first byte of a two-byte character. If we see this
         // followed by a valid second byte in Shift_JIS, assume it is Shift_JIS.
         // If we see something else in that second byte, we'll make the risky guess
         // that it's UTF-8.
         int length = bytes.Length;
         bool canBeISO88591 = true;
         bool canBeShiftJIS = true;
         bool canBeUTF8 = true;
         int utf8BytesLeft = 0;
         int maybeDoubleByteCount = 0;
         int maybeSingleByteKatakanaCount = 0;
         bool sawLatin1Supplement = false;
         bool sawUTF8Start = false;
         bool lastWasPossibleDoubleByteStart = false;

         for (int i = 0;
              i < length && (canBeISO88591 || canBeShiftJIS || canBeUTF8);
              i++)
         {

            int value = bytes[i] & 0xFF;

            // UTF-8 stuff
            if (value >= 0x80 && value <= 0xBF)
            {
               if (utf8BytesLeft > 0)
               {
                  utf8BytesLeft--;
               }
            }
            else
            {
               if (utf8BytesLeft > 0)
               {
                  canBeUTF8 = false;
               }
               if (value >= 0xC0 && value <= 0xFD)
               {
                  sawUTF8Start = true;
                  int valueCopy = value;
                  while ((valueCopy & 0x40) != 0)
                  {
                     utf8BytesLeft++;
                     valueCopy <<= 1;
                  }
               }
            }

            // ISO-8859-1 stuff

            if ((value == 0xC2 || value == 0xC3) && i < length - 1)
            {
               // This is really a poor hack. The slightly more exotic characters people might want to put in
               // a QR Code, by which I mean the Latin-1 supplement characters (e.g. u-umlaut) have encodings
               // that start with 0xC2 followed by [0xA0,0xBF], or start with 0xC3 followed by [0x80,0xBF].
               int nextValue = bytes[i + 1] & 0xFF;
               if (nextValue <= 0xBF &&
                   ((value == 0xC2 && nextValue >= 0xA0) || (value == 0xC3 && nextValue >= 0x80)))
               {
                  sawLatin1Supplement = true;
               }
            }
            if (value >= 0x7F && value <= 0x9F)
            {
               canBeISO88591 = false;
            }

            // Shift_JIS stuff

            if (value >= 0xA1 && value <= 0xDF)
            {
               // count the number of characters that might be a Shift_JIS single-byte Katakana character
               if (!lastWasPossibleDoubleByteStart)
               {
                  maybeSingleByteKatakanaCount++;
               }
            }
            if (!lastWasPossibleDoubleByteStart &&
                ((value >= 0xF0 && value <= 0xFF) || value == 0x80 || value == 0xA0))
            {
               canBeShiftJIS = false;
            }
            if ((value >= 0x81 && value <= 0x9F) || (value >= 0xE0 && value <= 0xEF))
            {
               // These start double-byte characters in Shift_JIS. Let's see if it's followed by a valid
               // second byte.
               if (lastWasPossibleDoubleByteStart)
               {
                  // If we just checked this and the last byte for being a valid double-byte
                  // char, don't check starting on this byte. If this and the last byte
                  // formed a valid pair, then this shouldn't be checked to see if it starts
                  // a double byte pair of course.
                  lastWasPossibleDoubleByteStart = false;
               }
               else
               {
                  // ... otherwise do check to see if this plus the next byte form a valid
                  // double byte pair encoding a character.
                  lastWasPossibleDoubleByteStart = true;
                  if (i >= bytes.Length - 1)
                  {
                     canBeShiftJIS = false;
                  }
                  else
                  {
                     int nextValue = bytes[i + 1] & 0xFF;
                     if (nextValue < 0x40 || nextValue > 0xFC)
                     {
                        canBeShiftJIS = false;
                     }
                     else
                     {
                        maybeDoubleByteCount++;
                     }
                     // There is some conflicting information out there about which bytes can follow which in
                     // double-byte Shift_JIS characters. The rule above seems to be the one that matches practice.
                  }
               }
            }
            else
            {
               lastWasPossibleDoubleByteStart = false;
            }
         }
         if (utf8BytesLeft > 0)
         {
            canBeUTF8 = false;
         }

         // Easy -- if assuming Shift_JIS and no evidence it can't be, done
         if (canBeShiftJIS && ASSUME_SHIFT_JIS)
         {
            return SHIFT_JIS;
         }
         if (canBeUTF8 && sawUTF8Start)
         {
            return UTF8;
         }
         // Distinguishing Shift_JIS and ISO-8859-1 can be a little tough. The crude heuristic is:
         // - If we saw
         //   - at least 3 bytes that starts a double-byte value (bytes that are rare in ISO-8859-1), or
         //   - over 5% of bytes could be single-byte Katakana (also rare in ISO-8859-1),
         // - and, saw no sequences that are invalid in Shift_JIS, then we conclude Shift_JIS
         if (canBeShiftJIS && (maybeDoubleByteCount >= 3 || 20 * maybeSingleByteKatakanaCount > length))
         {
            return SHIFT_JIS;
         }
         // Otherwise, we default to ISO-8859-1 unless we know it can't be
         if (!sawLatin1Supplement && canBeISO88591)
         {
            return ISO88591;
         }
         // Otherwise, we take a wild guess with platform encoding
         return PLATFORM_DEFAULT_ENCODING;
      }
   }
}