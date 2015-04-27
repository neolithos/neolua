using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Neo.IronLua
{
	#region -- class Lua ----------------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>All static methods for the language implementation</summary>
	public partial class Lua
	{
		internal const ExpressionType IntegerDivide = (ExpressionType)(-100);

		// LuaResult
		internal readonly static ConstructorInfo ResultConstructorInfoArg1;
		internal readonly static ConstructorInfo ResultConstructorInfoArgN;
		internal readonly static PropertyInfo ResultIndexPropertyInfo;
		internal readonly static PropertyInfo ResultValuesPropertyInfo;
		internal readonly static PropertyInfo ResultEmptyPropertyInfo;
		// LuaException
		internal readonly static ConstructorInfo RuntimeExceptionConstructorInfo;
		// LuaTable
		internal readonly static MethodInfo TableSetValueKeyStringMethodInfo;
		internal readonly static MethodInfo TableGetValueKeyStringMethodInfo;
		internal readonly static MethodInfo TableSetValueKeyIntMethodInfo;
		internal readonly static MethodInfo TableGetValueKeyIntMethodInfo;
		internal readonly static MethodInfo TableSetValueKeyObjectMethodInfo;
		internal readonly static MethodInfo TableGetValueKeyObjectMethodInfo;
		internal readonly static MethodInfo TableSetValueKeyListMethodInfo;
		internal readonly static MethodInfo TableGetValueKeyListMethodInfo;

		internal readonly static MethodInfo TableGetCallMemberMethodInfo;
		internal readonly static MethodInfo TableSetObjectMemberMethodInfo;
		internal readonly static MethodInfo TableDefineMethodLightMethodInfo;

		internal readonly static FieldInfo TableEntriesFieldInfo;
		internal readonly static MethodInfo TablePropertyChangedMethodInfo;
		internal readonly static FieldInfo TableEntryValueFieldInfo;

		internal readonly static MethodInfo TableAddMethodInfo;
		internal readonly static MethodInfo TableSubMethodInfo;
		internal readonly static MethodInfo TableMulMethodInfo;
		internal readonly static MethodInfo TableDivMethodInfo;
		internal readonly static MethodInfo TableModMethodInfo;
		internal readonly static MethodInfo TablePowMethodInfo;
		internal readonly static MethodInfo TableUnMinusMethodInfo;
		internal readonly static MethodInfo TableIDivMethodInfo;
		internal readonly static MethodInfo TableBAndMethodInfo;
		internal readonly static MethodInfo TableBOrMethodInfo;
		internal readonly static MethodInfo TableBXOrMethodInfo;
		internal readonly static MethodInfo TableBNotMethodInfo;
		internal readonly static MethodInfo TableShlMethodInfo;
		internal readonly static MethodInfo TableShrMethodInfo;
		internal readonly static MethodInfo TableConcatMethodInfo;
		internal readonly static MethodInfo TableLenMethodInfo;
		internal readonly static MethodInfo TableEqualMethodInfo;
		internal readonly static MethodInfo TableLessThanMethodInfo;
		internal readonly static MethodInfo TableLessEqualMethodInfo;
		internal readonly static MethodInfo TableIndexMethodInfo;
		internal readonly static MethodInfo TableNewIndexMethodInfo;
		internal readonly static MethodInfo TableCallMethodInfo;
		// LuaType
		internal readonly static PropertyInfo TypeClrPropertyInfo;
		internal readonly static MethodInfo TypeGetTypeMethodInfoArgIndex;
		internal readonly static MethodInfo TypeGetGenericItemMethodInfo;
		internal readonly static MethodInfo TypeGetTypeMethodInfoArgType;
		internal readonly static PropertyInfo TypeTypePropertyInfo;
		// LuaMethod
		internal readonly static ConstructorInfo MethodConstructorInfo;
		internal readonly static ConstructorInfo OverloadedMethodConstructorInfo;
		internal readonly static MethodInfo OverloadedMethodGetMethodMethodInfo;
		internal readonly static PropertyInfo MethodMethodPropertyInfo;
		internal readonly static PropertyInfo MethodNamePropertyInfo;
		internal readonly static PropertyInfo MethodInstancePropertyInfo;
		internal readonly static PropertyInfo MethodTypePropertyInfo;
		// LuaEvent
		internal readonly static ConstructorInfo EventConstructorInfo;
		internal readonly static PropertyInfo AddMethodInfoPropertyInfo;
		internal readonly static PropertyInfo RemoveMethodInfoPropertyInfo;
		internal readonly static PropertyInfo RaiseMethodInfoPropertyInfo;
		// Lua
		internal readonly static MethodInfo ParseNumberMethodInfo;
		internal readonly static MethodInfo RuntimeLengthMethodInfo;
		internal readonly static MethodInfo ConvertValueMethodInfo;
		internal readonly static MethodInfo GetResultValuesMethodInfo;
		internal readonly static MethodInfo CombineArrayWithResultMethodInfo;
		internal readonly static MethodInfo ConvertArrayMethodInfo;
		internal readonly static MethodInfo TableSetObjectsMethod;
		internal readonly static MethodInfo ConcatStringMethodInfo;
		internal readonly static MethodInfo ConvertDelegateMethodInfo;
		internal readonly static MethodInfo InitArray1MethodInfo;
		internal readonly static MethodInfo InitArrayNMethodInfo;
		// Object
		internal readonly static MethodInfo ObjectEqualsMethodInfo;
		internal readonly static MethodInfo ObjectReferenceEqualsMethodInfo;
		// Convert
		internal readonly static MethodInfo ConvertToStringMethodInfo;
		// String
		internal readonly static FieldInfo StringEmptyFieldInfo;
		internal readonly static MethodInfo StringConcatMethodInfo;
		internal readonly static PropertyInfo StringItemPropertyInfo;
		// CultureInvariant
		internal readonly static PropertyInfo CultureInvariantPropertyInfo;
		// List
		internal readonly static PropertyInfo ListItemPropertyInfo;
		internal readonly static PropertyInfo ListCountPropertyInfo;
    // INotifyPropertyChanged
    internal readonly static EventInfo NotifyPropertyChangedEventInfo;
		// IDispose
		internal readonly static MethodInfo DisposeDisposeMethodInfo;
		// IEnumerable
		internal readonly static MethodInfo EnumerableGetEnumeratorMethodInfo;
		// IEnumerator
		internal readonly static MethodInfo EnumeratorMoveNextMethodInfo;
		internal readonly static PropertyInfo EnumeratorCurrentPropertyInfo;

		#region -- sctor ------------------------------------------------------------------

		static Lua()
		{
			// LuaResult
			var tiLuaResult = typeof(LuaResult).GetTypeInfo();
			ResultConstructorInfoArg1 = tiLuaResult.FindDeclaredConstructor(ReflectionFlag.None, typeof(object));
			ResultConstructorInfoArgN = tiLuaResult.FindDeclaredConstructor(ReflectionFlag.None, typeof(object[]));
			ResultIndexPropertyInfo = tiLuaResult.FindDeclaredProperty("Item", ReflectionFlag.None);
			ResultEmptyPropertyInfo = tiLuaResult.FindDeclaredProperty("Empty", ReflectionFlag.None);
			ResultValuesPropertyInfo = tiLuaResult.FindDeclaredProperty("Values", ReflectionFlag.None);

			// LuaException
			var tiLuaRuntimeException = typeof(LuaRuntimeException).GetTypeInfo();
			RuntimeExceptionConstructorInfo = tiLuaRuntimeException.FindDeclaredConstructor(ReflectionFlag.None, typeof(string), typeof(Exception));

			// LuaTable
			var tiLuaTable = typeof(LuaTable).GetTypeInfo();
			TableSetValueKeyStringMethodInfo = tiLuaTable.FindDeclaredMethod("SetMemberValue", ReflectionFlag.None, typeof(string), typeof(object), typeof(bool), typeof(bool));
			TableGetValueKeyStringMethodInfo = tiLuaTable.FindDeclaredMethod("GetMemberValue", ReflectionFlag.None, typeof(string), typeof(bool), typeof(bool));
			TableSetValueKeyIntMethodInfo = tiLuaTable.FindDeclaredMethod("SetArrayValue", ReflectionFlag.None, typeof(int), typeof(object), typeof(bool));
			TableGetValueKeyIntMethodInfo = tiLuaTable.FindDeclaredMethod("GetArrayValue", ReflectionFlag.None, typeof(int), typeof(bool));
			TableSetValueKeyObjectMethodInfo = tiLuaTable.FindDeclaredMethod("SetValue", ReflectionFlag.None, typeof(object), typeof(object), typeof(bool));
			TableGetValueKeyObjectMethodInfo = tiLuaTable.FindDeclaredMethod("GetValue", ReflectionFlag.None, typeof(object), typeof(bool));
			TableSetValueKeyListMethodInfo = tiLuaTable.FindDeclaredMethod("SetValue", ReflectionFlag.None, typeof(object[]), typeof(object), typeof(bool));
			TableGetValueKeyListMethodInfo = tiLuaTable.FindDeclaredMethod("GetValue", ReflectionFlag.None, typeof(object[]), typeof(bool));

			TableDefineMethodLightMethodInfo = tiLuaTable.FindDeclaredMethod("DefineMethodLight", ReflectionFlag.None, typeof(string), typeof(Delegate));
			TableGetCallMemberMethodInfo = tiLuaTable.FindDeclaredMethod("GetCallMember",  ReflectionFlag.NoArguments);
			TableSetObjectMemberMethodInfo = tiLuaTable.FindDeclaredMethod("SetObjectMember", ReflectionFlag.None, typeof(object));

			TableEntriesFieldInfo = tiLuaTable.FindDeclaredField("entries", ReflectionFlag.None);
			TablePropertyChangedMethodInfo = tiLuaTable.FindDeclaredMethod("OnPropertyChanged", ReflectionFlag.None, typeof(string));
			TableEntryValueFieldInfo = tiLuaTable.GetDeclaredNestedType("LuaTableEntry").FindDeclaredField("value", ReflectionFlag.None);

			TableAddMethodInfo = tiLuaTable.FindDeclaredMethod("OnAdd", ReflectionFlag.NoArguments);
			TableSubMethodInfo = tiLuaTable.FindDeclaredMethod("OnSub", ReflectionFlag.NoArguments);
			TableMulMethodInfo = tiLuaTable.FindDeclaredMethod("OnMul", ReflectionFlag.NoArguments);
			TableDivMethodInfo = tiLuaTable.FindDeclaredMethod("OnDiv", ReflectionFlag.NoArguments);
			TableModMethodInfo = tiLuaTable.FindDeclaredMethod("OnMod", ReflectionFlag.NoArguments);
			TablePowMethodInfo = tiLuaTable.FindDeclaredMethod("OnPow", ReflectionFlag.NoArguments);
			TableUnMinusMethodInfo = tiLuaTable.FindDeclaredMethod("OnUnMinus", ReflectionFlag.NoArguments);
			TableIDivMethodInfo = tiLuaTable.FindDeclaredMethod("OnIDiv", ReflectionFlag.NoArguments);
			TableBAndMethodInfo = tiLuaTable.FindDeclaredMethod("OnBAnd", ReflectionFlag.NoArguments);
			TableBOrMethodInfo = tiLuaTable.FindDeclaredMethod("OnBOr", ReflectionFlag.NoArguments);
			TableBXOrMethodInfo = tiLuaTable.FindDeclaredMethod("OnBXor", ReflectionFlag.NoArguments);
			TableBNotMethodInfo = tiLuaTable.FindDeclaredMethod("OnBNot", ReflectionFlag.NoArguments);
			TableShlMethodInfo = tiLuaTable.FindDeclaredMethod("OnShl", ReflectionFlag.NoArguments);
			TableShrMethodInfo = tiLuaTable.FindDeclaredMethod("OnShr", ReflectionFlag.NoArguments);
			TableConcatMethodInfo = tiLuaTable.FindDeclaredMethod("OnConcat", ReflectionFlag.NoArguments);
			TableLenMethodInfo = tiLuaTable.FindDeclaredMethod("OnLen", ReflectionFlag.NoArguments);
			TableEqualMethodInfo = tiLuaTable.FindDeclaredMethod("OnEqual", ReflectionFlag.NoArguments);
			TableLessThanMethodInfo = tiLuaTable.FindDeclaredMethod("OnLessThan", ReflectionFlag.NoArguments);
			TableLessEqualMethodInfo = tiLuaTable.FindDeclaredMethod("OnLessEqual", ReflectionFlag.NoArguments);
			TableIndexMethodInfo = tiLuaTable.FindDeclaredMethod("OnIndex",  ReflectionFlag.NoArguments);
			TableNewIndexMethodInfo = tiLuaTable.FindDeclaredMethod("OnNewIndex", ReflectionFlag.NoArguments);
			TableCallMethodInfo = tiLuaTable.FindDeclaredMethod("OnCall", ReflectionFlag.NoArguments);

			// LuaType
			var tiLuaType = typeof(LuaType).GetTypeInfo();
			TypeClrPropertyInfo = tiLuaType.FindDeclaredProperty("Clr", ReflectionFlag.None);
			TypeGetGenericItemMethodInfo = tiLuaType.FindDeclaredMethod("GetGenericItem", ReflectionFlag.NoArguments);
			TypeGetTypeMethodInfoArgIndex = tiLuaType.FindDeclaredMethod("GetType", ReflectionFlag.Static, typeof(int));
			TypeGetTypeMethodInfoArgType = tiLuaType.FindDeclaredMethod("GetType", ReflectionFlag.Static, typeof(Type));
			TypeTypePropertyInfo = tiLuaType.FindDeclaredProperty("Type", ReflectionFlag.None);

			// LuaMethod
			var tiLuaMethod = typeof(LuaMethod).GetTypeInfo();
			MethodConstructorInfo = tiLuaMethod.FindDeclaredConstructor(ReflectionFlag.None, typeof(object), typeof(MethodInfo));
			MethodMethodPropertyInfo = tiLuaMethod.FindDeclaredProperty("Method", ReflectionFlag.None);

			// LuaOverloadedMethod
			var tiLuaOverloadedMethod = typeof(LuaOverloadedMethod).GetTypeInfo();
			OverloadedMethodConstructorInfo = tiLuaOverloadedMethod.FindDeclaredConstructor(ReflectionFlag.None, typeof(object), typeof(MethodInfo[]));
			OverloadedMethodGetMethodMethodInfo = tiLuaOverloadedMethod.FindDeclaredMethod("GetMethod", ReflectionFlag.None, typeof(bool), typeof(Type[]));

			// ILuaMethod
			var tiLuaMethodInterface = typeof(ILuaMethod).GetTypeInfo();
			MethodNamePropertyInfo = tiLuaMethodInterface.FindDeclaredProperty("Name", ReflectionFlag.None);
			MethodTypePropertyInfo = tiLuaMethodInterface.FindDeclaredProperty("Type", ReflectionFlag.None);
			MethodInstancePropertyInfo = tiLuaMethodInterface.FindDeclaredProperty("Instance", ReflectionFlag.None);

			// Event
			var tiLuaEvent = typeof(LuaEvent).GetTypeInfo();
			EventConstructorInfo = tiLuaEvent.FindDeclaredConstructor(ReflectionFlag.None, typeof(object), typeof(EventInfo));
			AddMethodInfoPropertyInfo = tiLuaEvent.FindDeclaredProperty("AddMethodInfo", ReflectionFlag.None);
			RemoveMethodInfoPropertyInfo = tiLuaEvent.FindDeclaredProperty("RemoveMethodInfo", ReflectionFlag.None);
			RaiseMethodInfoPropertyInfo = tiLuaEvent.FindDeclaredProperty("RaiseMethodInfo", ReflectionFlag.None);

			// Lua
			var tiLua = typeof(Lua).GetTypeInfo();
			ParseNumberMethodInfo = tiLua.FindDeclaredMethod("RtParseNumber", ReflectionFlag.None, typeof(string), typeof(bool), typeof(bool));
			RuntimeLengthMethodInfo = tiLua.FindDeclaredMethod("RtLength", ReflectionFlag.None | ReflectionFlag.NoArguments);
			ConvertValueMethodInfo = tiLua.FindDeclaredMethod("RtConvertValue", ReflectionFlag.None, typeof(object), typeof(Type));
			GetResultValuesMethodInfo = tiLua.FindDeclaredMethod("RtGetResultValues", ReflectionFlag.None, typeof(LuaResult), typeof(int), typeof(Type));
			CombineArrayWithResultMethodInfo = tiLua.FindDeclaredMethod("RtCombineArrayWithResult", ReflectionFlag.None, typeof(Array), typeof(LuaResult), typeof(Type));
			ConvertArrayMethodInfo = tiLua.FindDeclaredMethod("RtConvertArray", ReflectionFlag.None, typeof(Array), typeof(Type));
			TableSetObjectsMethod = tiLua.FindDeclaredMethod("RtTableSetObjects", ReflectionFlag.None, typeof(LuaTable), typeof(object), typeof(int));
			ConcatStringMethodInfo = tiLua.FindDeclaredMethod("RtConcatString", ReflectionFlag.None | ReflectionFlag.NoArguments);
			ConvertDelegateMethodInfo = tiLua.FindDeclaredMethod("RtConvertDelegate", ReflectionFlag.None | ReflectionFlag.NoArguments);
			InitArray1MethodInfo = tiLua.FindDeclaredMethod("RtInitArray", ReflectionFlag.None, typeof(Type), typeof(object));
			InitArrayNMethodInfo = tiLua.FindDeclaredMethod("RtInitArray", ReflectionFlag.None, typeof(Type), typeof(object[]));

			// Object
			var tiObject = typeof(Object).GetTypeInfo();
			ObjectEqualsMethodInfo = tiObject.FindDeclaredMethod("Equals", ReflectionFlag.Public | ReflectionFlag.Static | ReflectionFlag.NoArguments);
			ObjectReferenceEqualsMethodInfo = tiObject.FindDeclaredMethod("ReferenceEquals", ReflectionFlag.Public | ReflectionFlag.Static | ReflectionFlag.NoArguments);

			// Convert
			var tiConvert = typeof(Convert).GetTypeInfo();
			ConvertToStringMethodInfo = tiConvert.FindDeclaredMethod("ToString", ReflectionFlag.Static | ReflectionFlag.Public, typeof(object), typeof(IFormatProvider));

			// String
			var tiString = typeof(String).GetTypeInfo();
			StringEmptyFieldInfo = tiString.FindDeclaredField("Empty", ReflectionFlag.Public | ReflectionFlag.Static);
			StringConcatMethodInfo = tiString.FindDeclaredMethod("Concat", ReflectionFlag.None, typeof(string[]));
			StringItemPropertyInfo = tiString.FindDeclaredProperty("Chars", ReflectionFlag.Public | ReflectionFlag.Instance);

			// CulureInfo
			var tiCultureInfo = typeof(CultureInfo).GetTypeInfo();
			CultureInvariantPropertyInfo = tiCultureInfo.FindDeclaredProperty("InvariantCulture", ReflectionFlag.Public | ReflectionFlag.Static);

			//// Delegate
			//var tiDelegate = typeof(Delegate).GetTypeInfo();
			//DelegateMethodPropertyInfo = tiDelegate.FindDeclaredProperty("Method", ReflectionFlag.Public | ReflectionFlag.Instance);
			////CreateDelegateMethodInfo = tiDelegate.FindDeclaredMethod("CreateDelegate", BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null, new Type[] { typeof(Type), typeof(object), typeof(MethodInfo) }, null);

			// List<object>
			var tiList = typeof(List<object>).GetTypeInfo();
			ListItemPropertyInfo = tiList.FindDeclaredProperty("Item", ReflectionFlag.Public | ReflectionFlag.Instance);
			ListCountPropertyInfo = tiList.FindDeclaredProperty("Count", ReflectionFlag.Public | ReflectionFlag.Instance);
      
			// INotifyPropertyChanged
			var tiNotifyPropertyChanged = typeof(INotifyPropertyChanged).GetTypeInfo();
			NotifyPropertyChangedEventInfo = tiNotifyPropertyChanged.GetDeclaredEvent("PropertyChanged");
			if (NotifyPropertyChangedEventInfo == null)
				throw new ArgumentException("@NotifyPropertyChangedEventInfo");

			// Dispose
			var tiDispose = typeof(IDisposable).GetTypeInfo();
			DisposeDisposeMethodInfo = tiDispose.FindDeclaredMethod("Dispose", ReflectionFlag.None);

			// IEnumerator
			var tiEnumerable = typeof(System.Collections.IEnumerable).GetTypeInfo();
			EnumerableGetEnumeratorMethodInfo = tiEnumerable.FindDeclaredMethod("GetEnumerator", ReflectionFlag.None);

			// IEnumerator
			var tiEnumerator = typeof(System.Collections.IEnumerator).GetTypeInfo();
			EnumeratorMoveNextMethodInfo = tiEnumerator.FindDeclaredMethod("MoveNext", ReflectionFlag.None);
			EnumeratorCurrentPropertyInfo = tiEnumerator.FindDeclaredProperty("Current", ReflectionFlag.None);
		} // sctor

		#endregion

		#region -- RtParseNumber ----------------------------------------------------------

		/// <summary>This function convert numbers, that are automatically convert from strings.</summary>
		internal static object RtParseNumber(string sNumber, bool lUseDouble)
		{
			return RtParseNumber(sNumber, lUseDouble, true);
		} // func RtParseNumber

		internal static object RtParseNumber(string sNumber, bool lUseDouble, bool lThrowException)
		{
			if (sNumber == null || sNumber.Length == 0)
				return ThrowFormatExpression(lThrowException, "nil", 10);

			// skip spaces
			int iOffset = SkipSpaces(sNumber, 0);
			if (iOffset == sNumber.Length)
				return ThrowFormatExpression(lThrowException, "nil", 10);

			// test for sign
			bool? lNeg = null;
			if (sNumber[iOffset] == '+')
			{
				iOffset++;
				lNeg = false;
			}
			else if (sNumber[iOffset] == '-')
			{
				iOffset++;
				lNeg = true;
			}

			// test for base
			int iBase = 10;
			if (iOffset == sNumber.Length)
				return null;
			else if (sNumber[iOffset] == '0')
			{
				iOffset++;
				if (iOffset == sNumber.Length)
					return 0;
				else
				{
					char c = sNumber[iOffset];
					if (c == 'x' || c == 'X')
					{
						iBase = 16;
						lNeg = lNeg ?? false;
						iOffset++;
					}
					else if (c == 'b' || c == 'B')
					{
						iBase = 2;
						lNeg = lNeg ?? false;
						iOffset++;
					}
					else if (c == 'o' || c == 'O')
					{
						iBase = 8;
						lNeg = lNeg ?? false;
						iOffset++;
					}
					else
						iOffset--;
				}
			}

			return RtParseNumber(lNeg, sNumber, iOffset, iBase, lUseDouble, lThrowException);
		} // proc RtParseNumber

		private static int GetDigit(char c)
		{
			if (c >= '0' && c <= '9')
				return c - '0';
			else if (c >= 'A' && c <= 'Z')
				return c - 'A' + 10;
			else if (c >= 'a' && c <= 'z')
				return c - 'a' + 10;
			else
				return -1;
		} // func GetDigit

		private static int SkipSpaces(string sNumber, int iOffset)
		{
			while (iOffset < sNumber.Length && Char.IsWhiteSpace(sNumber[iOffset]))
				iOffset++;
			return iOffset;
		} // func SkipSpaces

		internal static object RtParseNumber(bool? lNegValue, string sNumber, int iOffset, int iBase, bool lUseDouble, bool lThrowException)
		{
			if (iBase < 2 || iBase > 36)
				throw new ArgumentException("Invalid base");

			bool lNeg;
			bool lNegE = false;

			ulong border = UInt64.MaxValue / (ulong)iBase;
			ulong fraction = 0;
			int expBorder = Int32.MaxValue / 10;
			int exponent = 0;
			int scale = 0;

			if (lNegValue.HasValue)
				lNeg = lNegValue.Value;
			else
			{
				// skip white spaces
				iOffset = SkipSpaces(sNumber, iOffset);

				// check sign
				if (iOffset >= sNumber.Length)
					return ThrowFormatExpression(lThrowException, sNumber, iBase);
				else if (sNumber[iOffset] == '+')
				{
					lNeg = false;
					iOffset++;
				}
				else if (sNumber[iOffset] == '-')
				{
					lNeg = true;
					iOffset++;
				}
				else
					lNeg = false;
			}

			// read the numbers
			int iState = 0;
			int n;
			bool lNumberReaded = false;
			bool lExponentReaded = false;
			while (iOffset < sNumber.Length)
			{
				// convert the char
				char c = sNumber[iOffset];

				switch (iState)
				{
					case 0: // read integer number
						if (c == '.') // goto read decimal
						{
							iState = 1;
							break;
						}
						goto case 1;
					case 1: // decimal part
						if ((c == 'e' || c == 'E') && iBase == 10) // goto read exponent
							iState = 4;
						else if ((c == 'p' || c == 'P') && (iBase == 2 || iBase == 8 || iBase == 16)) // goto read binary exponent
							iState = 5;
						else if (Char.IsWhiteSpace(c)) // goto read trailing whitespaces
							iState = iState | 0x100;
						else
						{
							n = GetDigit(c);
							if (n == -1 || n >= iBase)
								return ThrowFormatExpression(lThrowException, sNumber, iBase);

							if (fraction > border) // check for overflow
							{
								iState += 2;
								goto case 2; // loop
							}
							else
							{
								lNumberReaded |= true;
								fraction = unchecked(fraction * (ulong)iBase + (ulong)n);
								if (iState == 1)
									scale--;
							}
						}
						break;
					case 2: // integer overflow
					case 3: // decimal overflow
						if (Char.IsWhiteSpace(c)) // goto read trailing whitespaces
							iState = iState | 0x100;
						else if ((c == 'e' || c == 'E') && iBase == 10) // goto read exponent
							iState = 4;
						else if ((c == 'p' || c == 'P') && iBase <= 16) // goto read binary exponent
							iState = 5;
						else
						{
							n = GetDigit(c);
							if (n >= iBase)
								return ThrowFormatExpression(lThrowException, sNumber, iBase);
							else if (iState == 2)
								scale++;
						}
						break;

					case 4: // exponent +/-
					case 5: // bexponent +/-
						if (c == '+')
						{
							lNegE = false;
							iState += 2;
						}
						else if (c == '-')
						{
							lNegE = true;
							iState += 2;
						}
						else
						{
							iState += 2;
							iOffset--;
						}
						break;
					case 6: // exponent
					case 7: // b exponent
						if (Char.IsWhiteSpace(c)) // goto read trailing whitespaces
							iState = iState | 0x100;
						else
						{
							n = GetDigit(c);
							if (n == -1 || n >= 10 || exponent > expBorder)
								return ThrowFormatExpression(lThrowException, sNumber, iBase);

							lExponentReaded |= true;
							exponent = unchecked(exponent * 10 + n);
						}
						break;
					default:
						if ((iState & 0x100) != 0) // read trailing spaces
						{
							if (Char.IsWhiteSpace(c))
								break;
							return ThrowFormatExpression(lThrowException, sNumber, iBase);
						}
						else
							throw new InvalidOperationException();
				}
				iOffset++;
			}

			// check for a value
			if (!lNumberReaded)
				return ThrowFormatExpression(lThrowException, sNumber, iBase);

			// correct state
			iState = iState & 0xFF;

			// return the value
			if (iState == 0) // a integer value
      {
        unchecked
        {
          if (lNeg)
          {
            if (fraction < Int32.MaxValue)
              return -(int)fraction;
            else if (fraction < Int64.MaxValue)
              return -(long)fraction;
            else
              return lUseDouble ? -(double)fraction : -(float)fraction;
          }
          else
          {
            if (fraction <= Int32.MaxValue)
              return (int)fraction;
            else if (fraction <= UInt32.MaxValue)
              return (uint)fraction;
            else if (fraction <= Int64.MaxValue)
              return (long)fraction;
            else
              return fraction;
          }
        }
      }
			else
			{
				// check for a exponent
				if (iState >= 4 && !lExponentReaded)
					return ThrowFormatExpression(lThrowException, sNumber, iBase);

				double bias = 1;
				if (iState == 7)
				{
					if (iBase == 2)
					{
						bias = 1;
						iBase = 2;
					}
					else if (iBase == 8)
					{
						bias = 2;
						iBase = 2;
					}
					else if (iBase == 16)
					{
						bias = 4;
						iBase = 2;
					}
				}

				double t = lNegE ? scale * bias - exponent : scale * bias + exponent;
				double r = fraction * Math.Pow(iBase, t);
				if (lNeg)
					r = -r;

				if (iState == 7 && (r % 1) == 0)
				{
					if (r >= 0)
					{
						if (r < Int32.MaxValue)
							return (int)r;
						else if (r < UInt32.MaxValue)
							return (uint)r;
						else if (r < Int64.MaxValue)
							return (long)r;
						else if (r < UInt64.MaxValue)
							return (ulong)r;
					}
					else if (r < 0)
					{
						if (r > Int32.MinValue)
							return (int)r;
						else if (r < Int64.MinValue)
							return (long)r;
					}
				}
				return lUseDouble ? r : (float)r;
			}
		} // func RtParseNumber

		private static object ThrowFormatExpression(bool lThrowException, string sNumber, int iBase)
		{
			if (lThrowException)
			{
				string sType;
				switch (iBase)
				{
					case 2:
						sType = "bin";
						break;
					case 8:
						sType = "oct";
						break;
					case 10:
						sType = "dec";
						break;
					case 16:
						sType = "hex";
						break;
					default:
						sType = "base" + iBase.ToString();
						break;
				}
				throw new FormatException(String.Format(Properties.Resources.rsFormatError, sNumber, sType));
			}
			else
				return null;
		} // func ThrowFormatExpression

		#endregion

		#region -- RtConvertValue, RtConvertDelegate --------------------------------------

		/// <summary>Converts the value to the type, like NeoLua will do it.</summary>
		/// <param name="value">value, that should be converted.</param>
		/// <param name="toType">type to which the value should be converted.</param>
		/// <returns>converted value</returns>
		public static object RtConvertValue(object value, Type toType)
		{
			if (value == null)
				if (toType.GetTypeInfo().IsValueType)
					return Activator.CreateInstance(toType);
				else
					return null;
			else
			{
				Type fromType = value.GetType();
				if (fromType == toType)
					return value;
				else if (fromType == typeof(LuaResult))
					return RtConvertValue(((LuaResult)value)[0], toType);
				else if (toType == typeof(object))
					return value;
				else if (toType == typeof(string))
				{
					if (fromType == typeof(bool))
						return (bool)value ? "true" : "false";
					else
					{
						foreach (MethodInfo mi in fromType.GetTypeInfo().DeclaredMethods)
						{
							if (mi.IsPublic && mi.IsStatic)
							{
								if ((mi.Name == LuaEmit.csExplicit || mi.Name == LuaEmit.csImplicit) &&
									mi.ReturnType == typeof(string))
									return mi.Invoke(null, new object[] { value });
							}
						}

						return value == null ? String.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
					}
				}
				else
				{
					TypeInfo typeinfoTo = toType.GetTypeInfo();
					TypeInfo typeinfoFrom = fromType.GetTypeInfo();

					if (typeinfoTo.BaseType == typeof(MulticastDelegate) && typeinfoTo.BaseType == typeinfoFrom.BaseType)
						return RtConvertDelegate(toType, (Delegate)value);
					else if (toType.IsArray && fromType.IsArray)
						return RtConvertArray((Array)value, toType.GetElementType());
					else
					{
						var tcFrom = LuaEmit.GetTypeCode(fromType);
						var tcTo = LuaEmit.GetTypeCode(toType);
						if (tcTo == LuaEmitTypeCode.Object)
						{
							if (typeinfoTo.IsAssignableFrom(typeinfoFrom))
								return value;
							else
							{
								bool lImplicit = false;
								bool lExactTo = false;
								bool lExactFrom = false;
								MethodInfo mi = LuaEmit.FindConvertMethod(typeinfoTo.DeclaredMethods.Where(c => c.IsPublic && c.IsStatic), fromType, toType, ref lImplicit, ref lExactFrom, ref lExactTo);
								if (mi != null)
								{
									if (!lExactFrom)
										value = RtConvertValue(value, mi.GetParameters()[0].ParameterType);
									value = mi.Invoke(null, new object[] { value });
									if (!lExactTo)
										value = RtConvertValue(value, toType);
								}
								return value;
							}
						}
						else
						{
							// convert from string to number through lua parser
							if (tcFrom == LuaEmitTypeCode.String && tcTo >= LuaEmitTypeCode.SByte && tcTo <= LuaEmitTypeCode.Decimal)
								value = Lua.RtParseNumber((string)value, true);

							// convert to correct type
							switch (tcTo)
							{
								case LuaEmitTypeCode.Boolean:
									value = value != null;
									break;
								case LuaEmitTypeCode.Char:
									value = Convert.ToChar(value, CultureInfo.InvariantCulture);
									break;
								case LuaEmitTypeCode.DateTime:
									value = Convert.ToDateTime(value, CultureInfo.InvariantCulture);
									break;
								case LuaEmitTypeCode.SByte:
									value = Convert.ToSByte(value, CultureInfo.InvariantCulture);
									break;
								case LuaEmitTypeCode.Int16:
									value = Convert.ToInt16(value, CultureInfo.InvariantCulture);
									break;
								case LuaEmitTypeCode.Int32:
									value = Convert.ToInt32(value, CultureInfo.InvariantCulture);
									break;
								case LuaEmitTypeCode.Int64:
									value = Convert.ToInt64(value, CultureInfo.InvariantCulture);
									break;
								case LuaEmitTypeCode.Byte:
									value = Convert.ToByte(value, CultureInfo.InvariantCulture);
									break;
								case LuaEmitTypeCode.UInt16:
									value = Convert.ToUInt16(value, CultureInfo.InvariantCulture);
									break;
								case LuaEmitTypeCode.UInt32:
									value = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
									break;
								case LuaEmitTypeCode.UInt64:
									value = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
									break;
								case LuaEmitTypeCode.Single:
									value = Convert.ToSingle(value, CultureInfo.InvariantCulture);
									break;
								case LuaEmitTypeCode.Double:
									value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
									break;
								case LuaEmitTypeCode.Decimal:
									value = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
									break;
								case LuaEmitTypeCode.String:
									value = Convert.ToString(value, CultureInfo.InvariantCulture);
									break;
								default:
									throw new InvalidOperationException("TypeCode unknown");
							}

							// check for generic and enum
							if (typeinfoTo.IsGenericType && toType.GetGenericTypeDefinition() == typeof(Nullable<>))
								return Activator.CreateInstance(toType, value);
							else if (typeinfoTo.IsEnum)
								return Enum.ToObject(toType, value);
							else
								return value;
						}
					}
				}
			}
		} // func RtConvertValue

		internal static Delegate RtConvertDelegate(Type toType, Delegate dlg)
		{
			try
			{
				MethodInfo miDelegate = dlg.GetMethodInfo();
				if (miDelegate.GetType().Name == "RuntimeMethodInfo") // runtime method -> use create delegate
				{
					return miDelegate.CreateDelegate(toType, dlg.Target);
				}
				else // dynamic method -> create the delegate from the DynamicMethod.Invoke
				{
					var mi = dlg.GetType().GetTypeInfo().GetDeclaredMethod("Invoke");
					return mi.CreateDelegate(toType, dlg);
				}
			}
			catch (Exception e)
			{
				throw new LuaRuntimeException(String.Format(Properties.Resources.rsBindConversionNotDefined, dlg.GetType().Name, toType.Name), e);
			}
		} // func RtConvertDelegate

		#endregion

		#region -- RtGetResultValues, RtCombineArrayWithResult, RtConvertArray ------------

		/// <summary>Get the part of the result as an array. If there are not enough values in the array, it returns a empty array.</summary>
		/// <param name="result"></param>
		/// <param name="iStartIndex"></param>
		/// <param name="typeElementType">Type of the elements of the result array.</param>
		/// <returns></returns>
		internal static Array RtGetResultValues(LuaResult result, int iStartIndex, Type typeElementType)
		{
			object[] values = result.Values;
			int iLength = values.Length - iStartIndex;
			if (iLength > 0)
			{
				Array r = Array.CreateInstance(typeElementType, iLength);
				for (int i = 0; i < iLength; i++)
					r.SetValue(RtConvertValue(values[i + iStartIndex], typeElementType), i);
				return r;
			}
			else
				return Array.CreateInstance(typeElementType, 0); // empty array
		} // func GetResultValues

		/// <summary>Combines a array with the result.</summary>
		/// <param name="args"></param>
		/// <param name="result"></param>
		/// <param name="typeArray"></param>
		/// <returns></returns>
		internal static Array RtCombineArrayWithResult(Array args, LuaResult result, Type typeArray)
		{
			object[] values = result.Values;
			int iArgsLength = args.Length;
			int iValuesLength = values.Length;

			Array r = Array.CreateInstance(typeArray, iArgsLength + iValuesLength);

			// copy args
			for (int i = 0; i < iArgsLength; i++)
				r.SetValue(args.GetValue(i), i);

			// add the result
			for (int i = 0; i < iValuesLength; i++)
				r.SetValue(RtConvertValue(values[i], typeArray), iArgsLength + i);

			return r;
		} // func CombineArrayWithResult

		internal static Array RtConvertArray(Array src, Type typeArray)
		{
			if (src == null)
				return Array.CreateInstance(typeArray, 0);
			else
			{
				Array r = Array.CreateInstance(typeArray, src.Length);

				for (int i = 0; i < src.Length; i++)
					r.SetValue(RtConvertValue(src.GetValue(i), typeArray), i);

				return r;
			}
		} // func ConvertArray

		#endregion

		#region -- RtLength ---------------------------------------------------------------

		/// <summary>Get's the length of an value.</summary>
		/// <param name="v">Value</param>
		/// <returns>Length of the value or 0.</returns>
		public static int RtLength(object v)
		{
			if (v == null)
				return 0;
			else if (v is LuaTable)
				return ((LuaTable)v).InternLen();
			else if (v is String)
				return ((String)v).Length;
			else if (v is System.IO.Stream)
				return unchecked((int)((System.IO.Stream)v).Length);
			else if (v is System.Collections.ICollection)
				return ((System.Collections.ICollection)v).Count;
			else
			{
				TypeInfo t = v.GetType().GetTypeInfo();
				PropertyInfo pi;

				// search for a generic collection
				Type tInterface = t.ImplementedInterfaces.Where(ii => ii.GetTypeInfo().IsGenericTypeDefinition && ii.GetTypeInfo().GetGenericTypeDefinition() == typeof(ICollection<>)).FirstOrDefault();
				if (tInterface != null)
				{
					pi = tInterface.GetTypeInfo().GetDeclaredProperty("Count");
					return (int)pi.GetValue(v, null);
				}

				// try find a Length or Count property
				pi = t.FindDeclaredProperty("Count", ReflectionFlag.NoException | ReflectionFlag.Public | ReflectionFlag.Instance);
				if (pi != null)
					return (int)RtConvertValue(pi.GetValue(v, null), typeof(int));

				pi = t.FindDeclaredProperty("Length", ReflectionFlag.NoException | ReflectionFlag.Public | ReflectionFlag.Instance);
				if (pi != null)
					return (int)RtConvertValue(pi.GetValue(v, null), typeof(int));

				LuaType lt = LuaType.GetType(t.AsType());
				throw new LuaRuntimeException(String.Format(Properties.Resources.rsNoLengthOperator, lt.AliasName ?? lt.Name), null);
			}
		} // func RtLength

		#endregion

		#region -- RtTableSetObjects ------------------------------------------------------

		internal static object RtTableSetObjects(LuaTable t, object value, int iStartIndex)
		{
			if (value != null && value is LuaResult)
			{
				LuaResult v = (LuaResult)value;

				for (int i = 0; i < v.Count; i++)
					t.SetArrayValue(iStartIndex++, v[i], true);
			}
			else
				t.SetArrayValue(iStartIndex, value, true);
			return t;
		} // func RtTableSetObjects

		#endregion

		#region -- RtInvoke ---------------------------------------------------------------

		internal static bool IsCallable(object ld)
		{
			return ld is Delegate || ld is ILuaMethod || ld is IDynamicMetaObjectProvider;
		} // func IsCallable

		internal static object RtInvokeSite(Func<CallInfo, CallSiteBinder> createInvokeBinder, Action<CallInfo, CallSite> updateCache, object[] args)
		{
			if (args[0] == null)
			{
				// create the delegate
				Type[] signature = new Type[args.Length + 1];
				signature[0] = typeof(CallSite); // CallSite
				for (int i = 1; i < args.Length; i++) // target + arguments
					signature[i] = typeof(object);
				signature[signature.Length - 1] = typeof(object); // return type

				// create a call site
				CallInfo callInfo = new CallInfo(args.Length - 1);
				CallSite site;
				args[0] = site = CallSite.Create(Expression.GetFuncType(signature), createInvokeBinder(callInfo));
				if (updateCache != null)
					updateCache(callInfo, site);
			}

			// call the site
			object o = args[0];
			FieldInfo fi = o.GetType().GetTypeInfo().FindDeclaredField("Target", ReflectionFlag.None);
			Delegate dlg = (Delegate)fi.GetValue(o);
			return new LuaResult(dlg.DynamicInvoke(args));
		} // func RtInvokeSite

		/// <summary></summary>
		/// <param name="target"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		public static object RtInvoke(object target, params object[] args)
		{
			return RtInvokeSite(null, callInfo => new Lua.LuaInvokeBinder(null, callInfo), null, target, args);
		} // func RtInvokeSite

		internal static object RtInvokeSite(CallSite site, Func<CallInfo, CallSiteBinder> createInvokeBinder, Action<CallInfo, CallSite> updateCache, object target, params object[] args)
		{
			// expand args for callsite and target
			object[] newArgs = new object[args.Length + 2];
			newArgs[0] = site;
			newArgs[1] = target;
			Array.Copy(args, 0, newArgs, 2, args.Length);

			// call site
			return RtInvokeSite(callInfo => new Lua.LuaInvokeBinder(null, callInfo), updateCache, newArgs);
		} // func RtInvokeSite

		#endregion

		#region -- RtConcatString ---------------------------------------------------------

		private static string RtConcatStringTable(object[] args, int iIndex)
		{
			if (iIndex >= args.Length - 1)
				return (string)RtConvertValue(args[iIndex], typeof(string));
			else if (args[iIndex] is LuaTable)
				return (string)RtConvertValue(((LuaTable)args[iIndex]).InternConcat(RtConcatStringTable(args, iIndex + 1)), typeof(string));
			else
				return (string)RtConvertValue(args[iIndex], typeof(string)) + RtConcatStringTable(args, iIndex + 1);
		} // func RtConcatStringTable

		internal static string RtConcatString(object[] args)
		{
			if (Array.Exists(args, a => a is LuaTable)) // do we have a table, than we use the metatable
			{
				return RtConcatStringTable(args, 0);
			}
			else
			{
				string[] strings = new string[args.Length];
				for (int i = 0; i < args.Length; i++)
					strings[i] = (string)RtConvertValue(args[i], typeof(string));
				return String.Concat(strings);
			}
		} // func RtConcatString

		#endregion

		#region -- RtInitArray ------------------------------------------------------------

		internal static object RtInitArray(Type elementType, object value)
		{
			if (value is LuaTable) // only the array part
			{
				LuaTable t = (LuaTable)value;
				int iLength = t.Length;

				// create the array
				Array r = Array.CreateInstance(elementType, iLength);

				// copy the values
				for (int i = 0; i < iLength; i++)
					r.SetValue(Lua.RtConvertValue(t[i + 1], elementType), i);

				return r;
			}
			else if (value is System.Collections.ICollection) // convert a collection to an array
			{
				System.Collections.ICollection c = (System.Collections.ICollection)value;

				// create the array an copy the values
				Array r = Array.CreateInstance(elementType, c.Count);
				c.CopyTo(r, 0);

				return r;
			}
			else // create a zero-value array
			{
				Array r = Array.CreateInstance(elementType, 1);
				r.SetValue(value, 0);
				return r;
			}
		} // func RtInitArray

		internal static object RtInitArray(Type elementType, object[] values)
		{
			Array r = Array.CreateInstance(elementType, values.Length);
			if (values.Length > 0)
			{
				for (int i = 0; i < values.Length; i++)
					r.SetValue(Lua.RtConvertValue(values[i], elementType), i);
			}
			return r;
		} // func RtInitArray

		#endregion

		#region -- RtSetUpValues, RtGetUpValues, RtJoinUpValues ---------------------------

		/// <summary>Returns the up-value of the given index.</summary>
		/// <param name="function">Delegate, which upvalue should returned.</param>
		/// <param name="index">1-based index of the upvalue.</param>
		/// <returns>Name, Value pair for the value.</returns>
		public static LuaResult RtGetUpValue(Delegate function, int index)
		{
			if (function == null || function.Target == null)
				return LuaResult.Empty;

			throw new NotImplementedException("todo");

			//// first we check for a closure
			//Closure closure = function.Target as Closure;
			//if (closure != null)
			//{
			//	if (closure.Locals != null && index >= 1 && index <= closure.Locals.Length)
			//	{
			//		object v = closure.Locals[index - 1];
			//		if (v is IStrongBox)
			//			v = ((IStrongBox)v).Value;
			//		return new LuaResult("var" + index.ToString(), v);
			//	}
			//	else
			//		return LuaResult.Empty;
			//}
			//else // no closure, thread the members as a closure
			//{
			//	FieldInfo[] fields = function.Target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.SetField);

			//	if (index >= 1 && index <= fields.Length)
			//	{
			//		FieldInfo fi = fields[index - 1];
			//		return new LuaResult(fi.Name, fi.GetValue(function.Target));
			//	}
			//	else
			//		return LuaResult.Empty;
			//}
		} // func RtGetUpValue

		#region -- class UpValueObject ----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class UpValueObject
		{
			private object value;
			private int index;

			public UpValueObject(object value, int index)
			{
				this.value = value;
				this.index = index;
			} // ctor

			public override bool Equals(object obj)
			{
				UpValueObject uvo = obj as UpValueObject;
				if (uvo == null)
					return false;
				else
					return uvo.value == value && uvo.index == index;
			} // func Equals

			public override int GetHashCode()
			{
				return value.GetHashCode();
			} // func GetHashCode

			public override string ToString()
			{
				return (index == 0 ? "strongbox: " : "class: ") + ((IntPtr)this).ToString();
			} // func ToString

			public static explicit operator IntPtr(UpValueObject o)
			{
				GCHandle h = GCHandle.Alloc(o.value, GCHandleType.Normal);
				try
				{
					return GCHandle.ToIntPtr(h) + o.index;
				}
				finally
				{
					h.Free();
				}
			} // func explicit

			public static explicit operator int(UpValueObject o)
			{
				return ((IntPtr)o).ToInt32();
			} // func explicit

			public static explicit operator long(UpValueObject o)
			{
				return ((IntPtr)o).ToInt64();
			} // func explicit
		} // class UpValueObject

		#endregion

		/// <summary>Simulates the upvalueid function. Becareful, the returned numbers are the current GC-Handle.</summary>
		/// <param name="function">Delegate</param>
		/// <param name="index">1-based index of the upvalue.</param>
		/// <returns>Returns not a Number. It returns a object, that enforces that all operations are break down to operations on the objects.</returns>
		public static object RtUpValueId(Delegate function, int index)
		{
			if (function == null || function.Target == null)
				throw new ArgumentOutOfRangeException();

			throw new NotImplementedException("todo");

			//// first we check for a closure
			//Closure closure = function.Target as Closure;
			//if (closure != null)
			//{
			//	if (closure.Locals != null && index >= 1 && index <= closure.Locals.Length)
			//		return new UpValueObject(closure.Locals[index - 1], 0);
			//	else
			//		throw new ArgumentOutOfRangeException();
			//}
			//else // no closure, thread the members as a closure
			//{
			//	FieldInfo[] fields = function.Target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.SetField);

			//	if (index >= 1 && index <= fields.Length)
			//		return new UpValueObject(function.Target, index);
			//	else
			//		throw new ArgumentOutOfRangeException();
			//}
		} // func RtUpValueId

		/// <summary>Changes the up-value of a delegate</summary>
		/// <param name="function">Delegate, which will be changed.</param>
		/// <param name="index">1-based index of the upvalue.</param>
		/// <param name="value">New value</param>
		/// <returns>Name of the value, that is changed or null if the function fails.</returns>
		public static string RtSetUpValue(Delegate function, int index, object value)
		{
			if (function == null)
				return null;

			throw new NotImplementedException("todo");

			//// first we check for a closure
			//Closure closure = function.Target as Closure;
			//if (closure != null)
			//{
			//	object strongBox;
			//	if (closure.Locals != null && index >= 1 && index <= closure.Locals.Length && (strongBox = closure.Locals[index - 1]) != null)
			//	{
			//		Type typeStrongBox = strongBox.GetType();
			//		if (typeStrongBox.IsGenericType && typeStrongBox.GetGenericTypeDefinition() == typeof(StrongBox<>))
			//		{
			//			Type typeBoxed = typeStrongBox.GetGenericArguments()[0];
			//			((IStrongBox)strongBox).Value = Lua.RtConvertValue(value, typeBoxed);
			//			return "var" + index.ToString();
			//		}
			//	}
			//	return null;
			//}
			//else // no closure, thread the members as a closure
			//{
			//	FieldInfo[] fields = function.Target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.SetField);

			//	if (index >= 1 && index <= fields.Length)
			//	{
			//		FieldInfo fi = fields[index - 1];
			//		fi.SetValue(function.Target, Lua.RtConvertValue(value, fi.FieldType));
			//		return fi.Name;
			//	}
			//	else
			//		return null;
			//}
		} // func RtSetUpValue

		/// <summary>Make the index1 upvalue refer to index2 upvalue. This only works for closures.</summary>
		/// <param name="function1">Delegate</param>
		/// <param name="index1">1-based index of the upvalue.</param>
		/// <param name="function2">Delegate</param>
		/// <param name="index2">1-based index of the upvalue.</param>
		public static void RtUpValueJoin(Delegate function1, int index1, Delegate function2, int index2)
		{
			// check the functions
			if (function1 == null)
				throw new ArgumentNullException("f1");
			if (function2 == null)
				throw new ArgumentNullException("f2");

			throw new NotImplementedException("todo");

			//// convert the closures
			//Closure closure1 = function1.Target as Closure;
			//if (closure1 == null)
			//	throw new InvalidOperationException("f1 is not a closure");
			//if (index1 < 1 || index1 > closure1.Locals.Length)
			//	throw new ArgumentOutOfRangeException("index1");

			//Closure closure2 = function2.Target as Closure;
			//if (closure2 == null)
			//	throw new InvalidOperationException("f2 is not a closure");
			//if (index2 < 1 || index2 > closure2.Locals.Length)
			//	throw new ArgumentOutOfRangeException("index2");

			//// re-reference the strongbox
			//closure1.Locals[index1 - 1] = closure2.Locals[index2 - 1];
		} // func RtUpValueJoin

		#endregion

		#region -- Enumerator -------------------------------------------------------------

		private readonly static Func<object, object, LuaResult> funcLuaEnumIterator = new Func<object, object, LuaResult>(LuaEnumIteratorImpl);

		private static LuaResult LuaEnumIteratorImpl(object s, object c)
		{
			System.Collections.IEnumerator e = (System.Collections.IEnumerator)s;
			if (e.MoveNext())
				return new LuaResult(e.Current);
			else
				return LuaResult.Empty;
		} // func LuaEnumIteratorImpl

		/// <summary>Convert IEnumerator's to lua enumerator-functions.</summary>
		/// <param name="e"></param>
		/// <returns></returns>
		public static LuaResult GetEnumIteratorResult(System.Collections.IEnumerator e)
		{
			return new LuaResult(funcLuaEnumIterator, e, null);
		} // func GetEnumIteratorResult

		#endregion
	} // class Lua

	#endregion
}
