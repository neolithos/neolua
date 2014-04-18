using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
  #region -- enum LuaIntegerType ------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public enum LuaIntegerType : byte
  {
    /// <summary></summary>
    Int16 = 0x01,
    /// <summary></summary>
    Int32 = 0x02,
    /// <summary></summary>
    Int64 = 0x03,
    /// <summary></summary>
    Mask = 0x07
  } // enum LuaIntegerType

  #endregion

  #region -- enum LuaFloatType --------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public enum LuaFloatType : byte
  {
    /// <summary></summary>
    Float = 0x10,
    /// <summary></summary>
    Double = 0x20,
    /// <summary></summary>
    Mask = 0x70
  } // enum LuaFloatType

  #endregion

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Manages the Lua-Script-Environment. At the time it holds the
  /// binder cache between the compiled scripts.</summary>
  public partial class Lua : IDisposable
  {
    #region -- class LuaDebugInfoGenerator --------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaDebugInfoGenerator : DebugInfoGenerator
    {
      private Lua lua;
      private LuaChunk currentChunk;

      public LuaDebugInfoGenerator(Lua lua, LuaChunk currentChunk)
      {
        this.lua = lua;
        this.currentChunk = currentChunk;
      } // ctor

      public override void MarkSequencePoint(LambdaExpression method, int ilOffset, DebugInfoExpression sequencePoint)
      {
        LuaChunk c;
        if (currentChunk.Name != method.Name)
        {
          c = Lua.GetChunk(method.Name);
          if (c == null)
            c = lua.CreateEmptyChunk(method.Name);
          currentChunk.AssignChunk(c);
        }
        else
          c = currentChunk;
        c.AddDebugInfo(sequencePoint.Document, ilOffset, sequencePoint.StartLine, sequencePoint.StartColumn);
      } // proc MarkSequencePoint
    } // class LuaDebugInfoGenerator

    #endregion

    #region -- class ReduceDynamic ----------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class ReduceDynamic : ExpressionVisitor, IEqualityComparer<object>
    {
      private TypeBuilder type;
      private Dictionary<object, FieldBuilder> fields = null;

      public ReduceDynamic(TypeBuilder type)
      {
        this.type = type;
        this.fields = new Dictionary<object, FieldBuilder>(this);
      } // ctor

      bool IEqualityComparer<object>.Equals(object x, object y)
      {
        return Object.ReferenceEquals(x, y);
      } // func IEqualityComparer<object>.Equals

      int IEqualityComparer<object>.GetHashCode(object obj)
      {
        return RuntimeHelpers.GetHashCode(obj);
      } // func IEqualityComparer<object>.GetHashCode

      public void InitializeFields(Type init)
      {
        foreach (var f in fields)
          init.GetField(f.Value.Name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.SetField).SetValue(null, f.Key);
      } // proc InitializeFields

      private Type GetVisibleType(Type type)
      {
        while (!type.IsVisible)
          type = type.BaseType;
        return type;
      } // func GetVisibleType

      private bool CanEmitValue(object value, Type type)
      {
        // Emit value types
        if (value == null)
          return true;
        else
          switch (Type.GetTypeCode(type))
          {
            case TypeCode.Boolean:
            case TypeCode.Char:
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
            case TypeCode.DateTime:
            case TypeCode.String:
              return true;
          }

        // Emit Methods
        MethodBase mb = value as MethodBase;
        if (mb != null && !(mb is DynamicMethod))
          return true;

        return false;

      } // func CanEmitValue
      protected override Expression VisitConstant(ConstantExpression node)
      {
        if (CanEmitValue(node.Value, node.Type))
          return node;

        FieldBuilder field;
        if (!fields.TryGetValue(node.Value, out field))
        {
          field = type.DefineField("$c" + fields.Count.ToString(), GetVisibleType(node.Value.GetType()), FieldAttributes.Static | FieldAttributes.Private);
          fields.Add(node.Value, field);
        }

        // create the expression
        Expression expr = Expression.Field(null, field);
        if (expr.Type != node.Type)
          expr = Expression.Convert(expr, node.Type);

        return expr;
      } // func VisitConstant

      protected override Expression VisitDynamic(DynamicExpression node)
      {
        Expression site = Expression.Constant(CallSite.Create(node.DelegateType, node.Binder));

        // ($site = site).Target.Invoke($site, args)
        ParameterExpression tmp = Expression.Variable(site.Type, "$site");
        
        // create args
        Expression[] args = new Expression[node.Arguments.Count + 1];
        args[0] = tmp;
        node.Arguments.CopyTo(args, 1);

        // create call
        Expression expr = Expression.Block(new ParameterExpression[] { tmp },
          Expression.Call(
            Expression.Field(
              Expression.Assign(tmp, site),
              site.Type.GetField("Target")
            ),
            node.DelegateType.GetMethod("Invoke"),
            args
          )
        );
        return Visit(expr);
      }
    } // class ReduceDynamic

    #endregion

    private bool lPrintExpressionTree = false;

    private AssemblyBuilder assembly = null;
    private ModuleBuilder module = null;
    private object lockCompile = new object();
    private Dictionary<string, LuaChunk> chunks = null;

    private int iNumberType = (int)LuaIntegerType.Int32 | (int)LuaFloatType.Double;

    #region -- Ctor/Dtor --------------------------------------------------------------

    /// <summary>Create a new lua-script-manager.</summary>
    public Lua()
    {
    } // ctor

    /// <summary>Create a new lua-script-manager.</summary>
    /// <param name="integerType"></param>
    /// <param name="floatType"></param>
    public Lua(LuaIntegerType integerType, LuaFloatType floatType)
    {
      this.IntegerType = integerType;
      this.FloatType = floatType;
    } // ctor

    /// <summary>Clear the cache.</summary>
    ~Lua()
    {
      Dispose(false);
    } // dtor

    /// <summary>Destroy script manager</summary>
    public void Dispose()
    {
      Dispose(true);
    } // proc Dispose

    /// <summary></summary>
    /// <param name="disposing"></param>
    protected virtual void Dispose(bool disposing)
    {
      Clear();
    } // proc Dispose

    /// <summary>Removes all chunks, binders and compiled assemblies.</summary>
    public virtual void Clear()
    {
      lock (lockCompile)
      {
        if (chunks != null)
        {
          while (chunks.Count > 0)
            chunks.Values.First().Dispose();
        }
        assembly = null;
        module = null;
        chunks = null;
      }
      ClearBinderCache();
      ClearUnreverencedChunks();
    } // proc Clear

    #endregion

    #region -- Compile ----------------------------------------------------------------

    /// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
    /// <param name="sFileName">Dateiname die gelesen werden soll.</param>
    /// <param name="lDebug">Compile with debug infos</param>
    /// <param name="args">Parameter für den Codeblock</param>
    /// <returns>Compiled chunk.</returns>
    public LuaChunk CompileChunk(string sFileName, bool lDebug, params KeyValuePair<string, Type>[] args)
    {
      return CompileChunk(sFileName, lDebug, new StreamReader(sFileName), args);
    } // func CompileChunk

    /// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
    /// <param name="tr">Inhalt</param>
    /// <param name="sName">Name der Datei</param>
    /// <param name="lDebug">Compile with debug infos</param>
    /// <param name="args">Parameter für den Codeblock</param>
    /// <returns>Compiled chunk.</returns>
    public LuaChunk CompileChunk(TextReader tr, string sName, bool lDebug, params KeyValuePair<string, Type>[] args)
    {
      return CompileChunk(sName, lDebug, tr, args);
    } // func CompileChunk

    /// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
    /// <param name="sCode">Code, der das Delegate darstellt.</param>
    /// <param name="sName">Name des Delegates</param>
    /// <param name="lDebug">Compile with debug infos</param>
    /// <param name="args">Argumente</param>
    /// <returns>Compiled chunk.</returns>
    public LuaChunk CompileChunk(string sCode, string sName, bool lDebug, params KeyValuePair<string, Type>[] args)
    {
      return CompileChunk(sName, lDebug, new StringReader(sCode), args);
    } // func CompileChunk

    internal LuaChunk CompileChunk(string sChunkName, bool lDebug, TextReader tr, IEnumerable<KeyValuePair<string, Type>> args)
    {
      if (String.IsNullOrEmpty(sChunkName))
        throw new ArgumentNullException("chunkname");

      using (LuaLexer l = new LuaLexer(sChunkName, tr))
      {
        LambdaExpression expr = Parser.ParseChunk(this, lDebug, true, l, null, typeof(LuaResult), args);

        if (lPrintExpressionTree)
        {
          Console.WriteLine(Parser.ExpressionToString(expr));
          Console.WriteLine(new string('=', 79));
        }
        
        LuaChunk chunk;
        try
        {
          // Get the chunk
          lock (lockCompile)
          {
            chunk = Lua.GetChunk(expr.Name);
            if (chunk == null)
              chunk = CreateEmptyChunk(expr.Name);
          }

          // compile the chunk
          if (lDebug)
            lock (lockCompile)
            {
              // create the assembly
              if (assembly == null)
              {
                  assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(GetLuaDynamicName(), AssemblyBuilderAccess.RunAndCollect);
                module = assembly.DefineDynamicModule("lua", true);
              }

              // create the type, that holds the script
              string sTypeName = chunk.ChunkName;
              int iTypeIndex = 0;
              while (Array.Exists(module.GetTypes(), c => String.Compare(c.Name, sTypeName, true) == 0))
                sTypeName = chunk.ChunkName + (++iTypeIndex).ToString();

              TypeBuilder type = module.DefineType(sTypeName, TypeAttributes.NotPublic | TypeAttributes.Sealed);
              var reduce = new ReduceDynamic(type);

              // compile the function
              MethodBuilder method = type.DefineMethod(chunk.Name, MethodAttributes.Static | MethodAttributes.Public);
              expr = (LambdaExpression)reduce.Visit(expr);
              expr.CompileToMethod(method, new LuaDebugInfoGenerator(this, chunk));

              // create the type and get the delegate for the function
              Type typeFinished = type.CreateType();
              reduce.InitializeFields(typeFinished);

              chunk.Chunk = Delegate.CreateDelegate(expr.Type, typeFinished.GetMethod(chunk.Name));
            }
          else
            chunk.Chunk = expr.Compile();

          // complete the chunk
          chunk.ChunkName = sChunkName;
        }
        catch
        {
          throw;
        }

        // Remove Empty Chunks
        ClearEmptyChunks();
        return chunk;
      }
    } // func CompileChunk

    /// <summary>Creates a simple lua-lambda-expression without any environment.</summary>
    /// <param name="sName">Name of the delegate</param>
    /// <param name="sCode">Code of the delegate.</param>
    /// <param name="typeDelegate">Delegate type. <c>null</c> is allowed.</param>
    /// <param name="returnType">Return-Type of the delegate</param>
    /// <param name="arguments">Arguments of the delegate.</param>
    /// <returns></returns>
    public Delegate CreateLambda(string sName, string sCode, Type typeDelegate, Type returnType, params KeyValuePair<string, Type>[] arguments)
    {
      using (LuaLexer l = new LuaLexer(sName, new StringReader(sCode)))
      {
        LambdaExpression expr = Parser.ParseChunk(this, false, false, l, typeDelegate, returnType, arguments);

        if (lPrintExpressionTree)
        {
          Console.WriteLine(Parser.ExpressionToString(expr));
          Console.WriteLine(new string('=', 79));
        }

        return expr.Compile();
      }
    } // func CreateLambda

    /// <summary>Creates a simple lua-delegate without any environment.</summary>
    /// <param name="sName">Name of the delegate</param>
    /// <param name="sCode">Code of the delegate.</param>
    /// <param name="argumentNames">Possible to override the argument names.</param>
    /// <returns></returns>
    public T CreateLambda<T>(string sName, string sCode, params string[] argumentNames)
      where T : class
    {
      Type typeDelegate = typeof(T);
      MethodInfo mi = typeDelegate.GetMethod("Invoke");
      ParameterInfo[] parameters = mi.GetParameters();
      KeyValuePair<string, Type>[] arguments = new KeyValuePair<string,Type>[parameters.Length];

      // create the argument list
      for (int i = 0; i < parameters.Length; i++)
      {
        ParameterInfo p = parameters[i];

        if (p.ParameterType.IsByRef)
          throw new ArgumentException(Properties.Resources.rsDelegateCouldNotHaveOut);

        arguments[i] = new KeyValuePair<string, Type>(
          argumentNames != null && i < argumentNames.Length ? argumentNames[i] : p.Name,
          p.ParameterType);
      }

      return (T)(object)CreateLambda(sName, sCode, typeDelegate, mi.ReturnParameter.ParameterType, arguments);
    } // func CreateLambda

    #endregion

    #region -- Chunks -----------------------------------------------------------------

    internal LuaChunk CreateEmptyChunk(string sName)
    {
      sName = (String.IsNullOrEmpty(sName) ? "lambda" : sName).Replace('.', '_')
        .Replace(';', '_')
        .Replace(',', '_')
        .Replace('+', '_')
        .Replace(':', '_');

      lock (lockCompile)
      {
        if (chunks == null)
          chunks = new Dictionary<string, LuaChunk>();

        int iId = 0;
        string sCurrentName = sName;

        // create a unique name
        lock (allChunks)
        {
          ClearUnreverencedChunks();
          while (ContainsChunkKey(sCurrentName))
            sCurrentName = String.Format("{0}#{1}", sName, ++iId);
        }

        // add the empty chunk, to reserve the chunk name
        LuaChunk chunk = chunks[sCurrentName] = new LuaChunk(this, sCurrentName, sName);

        RegisterChunk(chunk);

        return chunk;
      }
    } // func CreateEmptyChunk

    internal void RemoveChunk(string sName)
    {
      lock (lockCompile)
        chunks.Remove(sName);
    } // proc RemoveChunk

    private void ClearEmptyChunks()
    {
      List<string> s = new List<string>();
      lock (lockCompile)
      {
        // collect the empty chunks
        foreach (LuaChunk c in chunks.Values)
          if (c.IsEmpty)
            s.Add(c.Name);

        // remove the empty chunks
        for (int i = 0; i < s.Count; i++)
          chunks.Remove(s[i]);
      }
    } // proc ClearEmptyChunks

    #endregion

    /// <summary>Creates an empty environment for the lua functions.</summary>
    /// <returns>Initialized environment</returns>
    public virtual LuaGlobal CreateEnvironment()
    {
      return new LuaGlobal(this);
    } // func CreateEnvironment

    #region -- Numbers ----------------------------------------------------------------

    internal static Type GetIntegerType(int iNumberType)
    {
      switch ((LuaIntegerType)(iNumberType & (int)LuaIntegerType.Mask))
      {
        case LuaIntegerType.Int16:
          return typeof(short);
        case LuaIntegerType.Int32:
          return typeof(int);
        case LuaIntegerType.Int64:
          return typeof(long);
        default:
          throw new ArgumentException();
      }
    } // func GetIntegerType

    internal static Type GetFloatType(int iNumberType)
    {
      switch ((LuaFloatType)(iNumberType & (int)LuaFloatType.Mask))
      {
        case LuaFloatType.Float:
          return typeof(float);
        case LuaFloatType.Double:
          return typeof(double);
        default:
          throw new ArgumentException();
      }
    } // func GetFloatType

    private static object ParseInteger(string sNumber, int integerType)
    {
      NumberStyles style = ((byte)integerType & 8) != 0 ? NumberStyles.HexNumber : NumberStyles.Integer;
      switch ((LuaIntegerType)(integerType & (byte)LuaIntegerType.Mask))
      {
        case LuaIntegerType.Int16:
          {
            short t;
            return Int16.TryParse(sNumber, style, CultureInfo.InvariantCulture, out t) ? (object)t : null;
          }
        case LuaIntegerType.Int32:
          {
            int t;
            return Int32.TryParse(sNumber, style, CultureInfo.InvariantCulture, out t) ? (object)t : null;
          }
        case LuaIntegerType.Int64:
          {
            long t;
            return Int64.TryParse(sNumber, style, CultureInfo.InvariantCulture, out t) ? (object)t : null;
          }
        default:
          throw new InvalidOperationException();
      }
    } // func ParseInteger

    private static object ParseFloat(string sNumber, int floatType)
    {
      switch ((LuaFloatType)(floatType & (byte)LuaFloatType.Mask))
      {
        case LuaFloatType.Float:
          {
            float t;
            return Single.TryParse(sNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out t) ? (object)t : null;
          }
        case LuaFloatType.Double:
          {
            double t;
            return Double.TryParse(sNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out t) ? (object)t : null;
          }
        default:
          throw new InvalidOperationException();
      }
    } // func ParseFloat

    /// <summary>Parses a string to a lua number.</summary>
    /// <param name="sNumber">String representation of the number.</param>
    /// <param name="lHexNumber">Is the string a hex number</param>
    /// <returns></returns>
    public object ParseNumber(string sNumber, bool lHexNumber = false)
    {
      int numberType = iNumberType;
      if(lHexNumber)
        numberType |= 8;
      return Lua.RtParseNumber(sNumber, numberType);
    } // func ParseNumber

    internal int NumberType { get { return iNumberType; } }

    /// <summary>Default type for the non floating point numbers. Only short, int, long is allowed.</summary>
    public LuaIntegerType IntegerType
    {
      get { return (LuaIntegerType)(iNumberType & (int)LuaIntegerType.Mask); }
      private set
      {
        if (value == LuaIntegerType.Int16 ||
          value == LuaIntegerType.Int32 ||
          value == LuaIntegerType.Int64)
          iNumberType = (iNumberType & (int)LuaFloatType.Mask) | (int)value;
        else
          throw new ArgumentException();
      }
    } // prop IntegerType

    /// <summary>Default type for the floating point numbers. Only float, double, decimal is allowed.</summary>
    public LuaFloatType FloatType
    {
      get { return (LuaFloatType)(iNumberType & (int)LuaFloatType.Mask); }
      private set
      {
        if (value == LuaFloatType.Float ||
          value == LuaFloatType.Double)
          iNumberType = (iNumberType & (int)LuaIntegerType.Mask) | (int)value;
        else
          throw new ArgumentException();
      }
    } // prop FloatType

    #endregion

    internal bool PrintExpressionTree { get { return lPrintExpressionTree; } set { lPrintExpressionTree = value; } }

    // -- Static --------------------------------------------------------------

    private static Dictionary<string, WeakReference> allChunks = new Dictionary<string, WeakReference>();
    private static AssemblyName luaDynamicName = null;

    private static AssemblyName GetLuaDynamicName()
    {
      lock (allChunks)
      {
        if (luaDynamicName == null)
        {
          byte[] bKey;
          using (Stream src = typeof(Lua).Assembly.GetManifestResourceStream("Neo.IronLua.NeoLua.snk"))
          {
            bKey = new byte[src.Length];
            src.Read(bKey, 0, bKey.Length);
          }

          // create the strong name
          luaDynamicName = new AssemblyName();
          luaDynamicName.Name = "lua.dynamic";
          luaDynamicName.Version = new Version();
          luaDynamicName.Flags = AssemblyNameFlags.PublicKey;
          luaDynamicName.HashAlgorithm = System.Configuration.Assemblies.AssemblyHashAlgorithm.SHA1;
          luaDynamicName.KeyPair = new StrongNameKeyPair(bKey);
        }

        return luaDynamicName;
      }
    } // func GetLuaDynamicName

    internal static void RegisterChunk(LuaChunk chunk)
    {
      lock (allChunks)
        allChunks[chunk.Name] = new WeakReference(chunk, false);
    } // proc RegisterChunk

    internal static void ClearUnreverencedChunks()
    {
      GC.Collect();

      List<string> s = new List<string>();
      lock (allChunks)
      {
        foreach (var c in allChunks)
          if (!c.Value.IsAlive)
            s.Add(c.Key);

        for (int i = 0; i < s.Count; i++)
          allChunks.Remove(s[i]);
      }
    } // proc ClearUnreverencedChunks

    internal static bool ContainsChunkKey(string sName)
    {
      lock (allChunks)
        return allChunks.ContainsKey(sName);
    } // func ContainsChunkKey

    internal static LuaChunk GetChunk(string sName)
    {
      lock (allChunks)
      {
        WeakReference chunk;
        if (allChunks.TryGetValue(sName, out chunk))
          return (LuaChunk)chunk.Target;
        else
          return null;
      }
    } // func GetChunk
  } // class Lua
}
