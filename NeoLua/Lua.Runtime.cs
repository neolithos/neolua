using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
		internal readonly static MethodInfo TypeGetTypeMethodInfoArgType;
		internal readonly static MethodInfo TypeMakeGenericLuaTypeMethodInfo;
		internal readonly static MethodInfo TypeMakeArrayLuaTypeMethodInfo;
		internal readonly static PropertyInfo TypeTypePropertyInfo;
		internal readonly static PropertyInfo TypeParentPropertyInfo;
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
		internal readonly static MethodInfo ParseNumberObjectMethodInfo;
		internal readonly static MethodInfo ParseNumberTypedMethodInfo;
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
		internal readonly static MethodInfo RtConvertValueDynamicMethodInfo;
		// Object
		internal readonly static MethodInfo ObjectEqualsMethodInfo;
		internal readonly static MethodInfo ObjectReferenceEqualsMethodInfo;
		// Convert
		internal readonly static MethodInfo ConvertToStringMethodInfo;
		// Enum
		internal readonly static MethodInfo EnumParseMethodInfo;
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
		// MethodInfo
		internal readonly static MethodInfo MethodInfoCreateDelegateMethodInfo;

		internal readonly static Type ClosureType;
		internal readonly static FieldInfo ClosureLocalsFieldInfo;

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
			TypeGetTypeMethodInfoArgIndex = tiLuaType.FindDeclaredMethod("GetType", ReflectionFlag.Static, typeof(int));
			TypeGetTypeMethodInfoArgType = tiLuaType.FindDeclaredMethod("GetType", ReflectionFlag.Static, typeof(Type));
			TypeMakeGenericLuaTypeMethodInfo = tiLuaType.FindDeclaredMethod("MakeGenericLuaType", ReflectionFlag.Instance | ReflectionFlag.Public, typeof(LuaType[]), typeof(bool));
			TypeMakeArrayLuaTypeMethodInfo = tiLuaType.FindDeclaredMethod("MakeArrayLuaType", ReflectionFlag.Instance | ReflectionFlag.Public, typeof(int), typeof(bool));
			TypeTypePropertyInfo = tiLuaType.FindDeclaredProperty("Type", ReflectionFlag.None);
			TypeParentPropertyInfo = tiLuaType.FindDeclaredProperty("Parent", ReflectionFlag.None);

			// LuaMethod
			var tiLuaMethod = typeof(LuaMethod).GetTypeInfo();
			MethodConstructorInfo = tiLuaMethod.FindDeclaredConstructor(ReflectionFlag.None, typeof(object), typeof(MethodInfo), typeof(bool));
			MethodMethodPropertyInfo = tiLuaMethod.FindDeclaredProperty("Method", ReflectionFlag.None);

			// LuaOverloadedMethod
			var tiLuaOverloadedMethod = typeof(LuaOverloadedMethod).GetTypeInfo();
			OverloadedMethodConstructorInfo = tiLuaOverloadedMethod.FindDeclaredConstructor(ReflectionFlag.None, typeof(object), typeof(MethodInfo[]), typeof(bool));
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
			ParseNumberObjectMethodInfo = tiLua.FindDeclaredMethod("RtParseNumber", ReflectionFlag.None, typeof(string));
			ParseNumberTypedMethodInfo = tiLua.FindDeclaredMethod("RtParseNumber", ReflectionFlag.None, typeof(string), typeof(Type));
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
			RtConvertValueDynamicMethodInfo = tiLua.FindDeclaredMethod("RtConvertValueDynamic", ReflectionFlag.NoArguments | ReflectionFlag.Static);

			// Object
			var tiObject = typeof(Object).GetTypeInfo();
			ObjectEqualsMethodInfo = tiObject.FindDeclaredMethod("Equals", ReflectionFlag.Public | ReflectionFlag.Static | ReflectionFlag.NoArguments);
			ObjectReferenceEqualsMethodInfo = tiObject.FindDeclaredMethod("ReferenceEquals", ReflectionFlag.Public | ReflectionFlag.Static | ReflectionFlag.NoArguments);

			// Convert
			var tiConvert = typeof(Convert).GetTypeInfo();
			ConvertToStringMethodInfo = tiConvert.FindDeclaredMethod("ToString", ReflectionFlag.Static | ReflectionFlag.Public, typeof(object), typeof(IFormatProvider));

			// Enum
			var tiEnum = typeof(Enum).GetTypeInfo();
			EnumParseMethodInfo = tiEnum.FindDeclaredMethod("Parse", ReflectionFlag.Static | ReflectionFlag.Public, typeof(Type), typeof(string));

			// String
			var tiString = typeof(String).GetTypeInfo();
			StringEmptyFieldInfo = tiString.FindDeclaredField("Empty", ReflectionFlag.Public | ReflectionFlag.Static);
			StringConcatMethodInfo = tiString.FindDeclaredMethod("Concat", ReflectionFlag.None, typeof(string[]));
			StringItemPropertyInfo = tiString.FindDeclaredProperty("Chars", ReflectionFlag.Public | ReflectionFlag.Instance);

			// CulureInfo
			var tiCultureInfo = typeof(CultureInfo).GetTypeInfo();
			CultureInvariantPropertyInfo = tiCultureInfo.FindDeclaredProperty("InvariantCulture", ReflectionFlag.Public | ReflectionFlag.Static);

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

			// MethodInfo
			var tiMethodInfo = typeof(MethodInfo).GetTypeInfo();
			MethodInfoCreateDelegateMethodInfo = tiMethodInfo.FindDeclaredMethod("CreateDelegate", ReflectionFlag.None, typeof(Type), typeof(object));

			// Closure
			string sClosureTypeString = typeof(IStrongBox).AssemblyQualifiedName.Replace(".IStrongBox", ".Closure");
			ClosureType = Type.GetType(sClosureTypeString, false);
			//// WinStore, Desktop
			//ClosureType = Type.GetType("System.Runtime.CompilerServices.Closure, System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", false);
			//if (ClosureType == null) // WinPhone
			//	ClosureType = Type.GetType("System.Runtime.CompilerServices.Closure, System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e", false);
			if (ClosureType != null)
			{
				var tiClosureInfo = ClosureType.GetTypeInfo();

				ClosureLocalsFieldInfo = tiClosureInfo.FindDeclaredField("Locals", ReflectionFlag.Instance);
			}
		} // sctor

		#endregion

		#region -- RtParseNumber ----------------------------------------------------------

		/// <summary>This function convert numbers, that are automatically convert from strings.</summary>
		internal static object RtParseNumber(string sNumber, bool useDouble)
			=> RtParseNumber(sNumber, useDouble, true);

		internal static object RtParseNumber(string number, bool useDouble, bool throwException)
		{
			if (number == null || number.Length == 0)
				return ThrowFormatExpression(throwException, "nil", 10);

			// skip spaces
			var offset = SkipSpaces(number, 0);
			if (offset == number.Length)
				return ThrowFormatExpression(throwException, "nil", 10);

			// test for sign
			bool? isNegative = null;
			if (number[offset] == '+')
			{
				offset++;
				isNegative = false;
			}
			else if (number[offset] == '-')
			{
				offset++;
				isNegative = true;
			}

			// test for base
			var numberBase = 10;
			if (offset == number.Length)
				return null;
			else if (number[offset] == '0')
			{
				offset++;
				if (offset == number.Length)
					return 0;
				else
				{
					char c = number[offset];
					if (c == 'x' || c == 'X')
					{
						numberBase = 16;
						isNegative = isNegative ?? false;
						offset++;
					}
					else if (c == 'b' || c == 'B')
					{
						numberBase = 2;
						isNegative = isNegative ?? false;
						offset++;
					}
					else if (c == 'o' || c == 'O')
					{
						numberBase = 8;
						isNegative = isNegative ?? false;
						offset++;
					}
					else
						offset--;
				}
			}

			return RtParseNumber(isNegative, number, offset, numberBase, useDouble, throwException);
		} // proc RtParseNumber

		internal static object RtParseNumber(string number)
			=> RtParseNumber(number, true, true);

		internal static object RtParseNumber(string number, Type toType)
		{
			var r = RtParseNumber(number);
			if (toType != typeof(object))
				r = Convert.ChangeType(r, toType);
			return r;
		} // func RtParseNumber

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

		private static int SkipSpaces(string number, int offset)
		{
			while (offset < number.Length && Char.IsWhiteSpace(number[offset]))
				offset++;
			return offset;
		} // func SkipSpaces

		internal static object RtParseNumber(bool? isNegative, string number, int offset, int numberBase, bool useDouble, bool throwException)
		{
			if (numberBase < 2 || numberBase > 36)
				throw new ArgumentException("Invalid base");

			bool lNeg;
			bool lNegE = false;

			ulong border = UInt64.MaxValue / (ulong)numberBase;
			ulong fraction = 0;
			var expBorder = Int32.MaxValue / 10;
			var exponent = 0;
			var scale = 0;

			if (isNegative.HasValue)
				lNeg = isNegative.Value;
			else
			{
				// skip white spaces
				offset = SkipSpaces(number, offset);

				// check sign
				if (offset >= number.Length)
					return ThrowFormatExpression(throwException, number, numberBase);
				else if (number[offset] == '+')
				{
					lNeg = false;
					offset++;
				}
				else if (number[offset] == '-')
				{
					lNeg = true;
					offset++;
				}
				else
					lNeg = false;
			}

			// read the numbers
			var state = 0;
			int n;
			var isNumberReaded = false;
			var isExponentReaded = false;
			while (offset < number.Length)
			{
				// convert the char
				char c = number[offset];

				switch (state)
				{
					case 0: // read integer number
						if (c == '.') // goto read decimal
						{
							state = 1;
							break;
						}
						goto case 1;
					case 1: // decimal part
						if ((c == 'e' || c == 'E') && numberBase == 10) // goto read exponent
							state = 4;
						else if ((c == 'p' || c == 'P') && (numberBase == 2 || numberBase == 8 || numberBase == 16)) // goto read binary exponent
							state = 5;
						else if (Char.IsWhiteSpace(c)) // goto read trailing whitespaces
							state = state | 0x100;
						else
						{
							n = GetDigit(c);
							if (n == -1 || n >= numberBase)
								return ThrowFormatExpression(throwException, number, numberBase);

							if (fraction > border) // check for overflow
							{
								state += 2;
								goto case 2; // loop
							}
							else
							{
								isNumberReaded |= true;
								fraction = unchecked(fraction * (ulong)numberBase + (ulong)n);
								if (state == 1)
									scale--;
							}
						}
						break;
					case 2: // integer overflow
					case 3: // decimal overflow
						if (Char.IsWhiteSpace(c)) // goto read trailing whitespaces
							state = state | 0x100;
						else if ((c == 'e' || c == 'E') && numberBase == 10) // goto read exponent
							state = 4;
						else if ((c == 'p' || c == 'P') && numberBase <= 16) // goto read binary exponent
							state = 5;
						else
						{
							n = GetDigit(c);
							if (n >= numberBase)
								return ThrowFormatExpression(throwException, number, numberBase);
							else if (state == 2)
								scale++;
						}
						break;

					case 4: // exponent +/-
					case 5: // bexponent +/-
						if (c == '+')
						{
							lNegE = false;
							state += 2;
						}
						else if (c == '-')
						{
							lNegE = true;
							state += 2;
						}
						else
						{
							state += 2;
							offset--;
						}
						break;
					case 6: // exponent
					case 7: // b exponent
						if (Char.IsWhiteSpace(c)) // goto read trailing whitespaces
							state = state | 0x100;
						else
						{
							n = GetDigit(c);
							if (n == -1 || n >= 10 || exponent > expBorder)
								return ThrowFormatExpression(throwException, number, numberBase);

							isExponentReaded |= true;
							exponent = unchecked(exponent * 10 + n);
						}
						break;
					default:
						if ((state & 0x100) != 0) // read trailing spaces
						{
							if (Char.IsWhiteSpace(c))
								break;
							return ThrowFormatExpression(throwException, number, numberBase);
						}
						else
							throw new InvalidOperationException();
				}
				offset++;
			}

			// check for a value
			if (!isNumberReaded)
				return ThrowFormatExpression(throwException, number, numberBase);

			// correct state
			state = state & 0xFF;

			// return the value
			if (state == 0) // a integer value
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
              return useDouble ? -(double)fraction : -(float)fraction;
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
				if (state >= 4 && !isExponentReaded)
					return ThrowFormatExpression(throwException, number, numberBase);

				double bias = 1;
				if (state == 7)
				{
					if (numberBase == 2)
					{
						bias = 1;
						numberBase = 2;
					}
					else if (numberBase == 8)
					{
						bias = 2;
						numberBase = 2;
					}
					else if (numberBase == 16)
					{
						bias = 4;
						numberBase = 2;
					}
				}

				double t = lNegE ? scale * bias - exponent : scale * bias + exponent;
				double r = fraction * Math.Pow(numberBase, t);
				if (lNeg)
					r = -r;

				if (state == 7 && (r % 1) == 0)
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
				return useDouble ? r : (float)r;
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

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <returns></returns>
		public static object RtConvertValueDynamic<T>(object value)
		{
			var c = CallSite<Func<CallSite, object, T>>.Create(new LuaConvertBinder(null, typeof(T)));
			return c.Target(c, value);
		} // func RtConvertValueDynamic

		/// <summary>Converts the value to the type, like NeoLua will do it.</summary>
		/// <param name="value">value, that should be converted.</param>
		/// <param name="toType">type to which the value should be converted.</param>
		/// <returns>converted value</returns>
		public static object RtConvertValue(object value, Type toType)
		{
			if (value == null)
				if (toType == typeof(string))
					return String.Empty;
				else if (toType == typeof(bool))
					return false;
				else if (toType.GetTypeInfo().IsValueType)
					return Activator.CreateInstance(toType);
				else
					return null;
			else
			{
				var fromType = value.GetType();
				if (fromType == toType)
					return value;
				else if (fromType == typeof(LuaResult))
					return RtConvertValue(((LuaResult)value)[0], toType);
				else if (toType == typeof(LuaResult))
					return new LuaResult(value);
				else if (toType == typeof(object))
					return value;
				else if (toType == typeof(string))
				{
					if (fromType == typeof(bool))
						return (bool)value ? "true" : "false";
					else
					{
						if (value == null)
							return String.Empty;
						else
						{
							var convertToString = LuaEmit.GetTypeCode(fromType) != LuaEmitTypeCode.Object ? null : LuaEmit.FindConvertOperator(fromType, typeof(string));
							if (convertToString != null)
								return RtConvertValue(convertToString.Invoke(null, new object[] { value }), toType);
							else
								return Convert.ToString(value, CultureInfo.InvariantCulture);
						}
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
								var methodInfo = RtConvertValueDynamicMethodInfo.MakeGenericMethod(toType);
								return methodInfo.Invoke(null, new object[] { value });
							}
						}
						else
						{
							// convert from string to number through lua parser
							if (tcFrom == LuaEmitTypeCode.String && tcTo >= LuaEmitTypeCode.SByte && tcTo <= LuaEmitTypeCode.Decimal)
							{
								if (typeinfoTo.IsEnum)
									value = Enum.Parse(toType, (string)value);
								else
									value = Lua.RtParseNumber((string)value, true);
							}

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
								case LuaEmitTypeCode.String:
									value = Convert.ToString(value, CultureInfo.InvariantCulture);
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

		/// <summary></summary>
		/// <param name="ld"></param>
		/// <returns></returns>
		public static bool RtInvokeable(object ld)
		{
			return ld is Delegate || ld is ILuaMethod || ld is IDynamicMetaObjectProvider;
		} // func RtInvokeable

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

		private static TResult UpValueChangeEditor<TResult>(object target, Func<object[], TResult> changeClosure, Func<FieldInfo[], TResult> changeClass)
		{
			// first we check for a closure
			if (target.GetType() == ClosureType)
			{
				return changeClosure(ClosureLocalsFieldInfo.GetValue(target) as object[]);
			}
			else // no closure, thread the members as a closure
			{
				return changeClass(target.GetType().GetRuntimeFields().Where(fi => fi.IsPublic && !fi.IsStatic).ToArray());
			}
		} // func UpValueChangeEditor

		/// <summary>Returns the up-value of the given index.</summary>
		/// <param name="function">Delegate, which upvalue should returned.</param>
		/// <param name="index">1-based index of the upvalue.</param>
		/// <returns>Name, Value pair for the value.</returns>
		public static LuaResult RtGetUpValue(Delegate function, int index)
		{
			if (function == null || function.Target == null)
				return LuaResult.Empty;

			return UpValueChangeEditor(function.Target,
				locals =>
				{
					if (locals != null && index >= 1 && index <= locals.Length)
					{
						object v = locals.GetValue(index - 1);
						if (v is IStrongBox)
							v = ((IStrongBox)v).Value;
						return new LuaResult("var" + index.ToString(), v);
					}
					else
						return LuaResult.Empty;
				},
				fields =>
				{
					if (index >= 1 && index <= fields.Length)
					{
						FieldInfo fi = fields[index - 1];
						return new LuaResult(fi.Name, fi.GetValue(function.Target));
					}
					else
						return LuaResult.Empty;
				});
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

			return UpValueChangeEditor(function.Target,
				locals =>
				{
				if (locals != null && index >= 1 && index <= locals.Length)
					return new UpValueObject(locals[index - 1], 0);
				else
					throw new ArgumentOutOfRangeException();
				},
				fields =>
				{
					if (index >= 1 && index <= fields.Length)
						return new UpValueObject(function.Target, index);
					else
						throw new ArgumentOutOfRangeException();
				});
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

			return UpValueChangeEditor(function.Target,
				locals =>
				{
					object strongBox;
					if (locals != null && index >= 1 && index <= locals.Length && (strongBox = locals[index - 1]) != null)
					{
						var tiStrongBox = strongBox.GetType().GetTypeInfo();
						if (tiStrongBox.IsGenericType && tiStrongBox.GetGenericTypeDefinition() == typeof(StrongBox<>))
						{
							Type typeBoxed = tiStrongBox.GenericTypeArguments[0];
							((IStrongBox)strongBox).Value = Lua.RtConvertValue(value, typeBoxed);
							return "var" + index.ToString();
						}
					}
					return null;
				},
				fields =>
				{
					if (index >= 1 && index <= fields.Length)
					{
						FieldInfo fi = fields[index - 1];
						fi.SetValue(function.Target, Lua.RtConvertValue(value, fi.FieldType));
						return fi.Name;
					}
					else
						return null;
				});
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

			// only closures are allowed
			if (function1.Target.GetType() == ClosureType)
				throw new InvalidOperationException("f1 is not a closure");

			if (function2.Target.GetType() == ClosureType)
				throw new InvalidOperationException("f2 is not a closure");

			// check the indexes
			var locals1 = ClosureLocalsFieldInfo.GetValue(function1.Target) as object[];
			if (locals1 == null || index1 < 1 || index1 > locals1.Length)
				throw new ArgumentOutOfRangeException("index1");

			var locals2 = ClosureLocalsFieldInfo.GetValue(function2.Target) as object[];
			if (locals2 == null || index2 < 1 || index2 > locals2.Length)
				throw new ArgumentOutOfRangeException("index2");

			// re-reference the strongbox
			locals1[index1 - 1] = locals2[index2 - 1];
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
