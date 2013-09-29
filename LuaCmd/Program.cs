using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Dynamic;
using Neo.IronLua;
using TecWare.Core.Compile;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace LuaCmd
{
  public class Person
  {
    public string Name { get; set; }
    public short Age { get; set; }

    public void SetData(string sName, ref object a)
    {
    }

    public override string ToString()
    {
      return String.Format("[Person] Name={0}; Age={1}", Name, Age);
    }
  }

  public class Program
  {
    static void Main(string[] args)
    {
      //object b = -(int)1;
      //object a = (double)b + (double)1.0;

      //TestExpression();
      //TestMemory();
      TestParser();
      //TestBdeParser();
      //TestLexer();
      Console.ReadLine();
    }

    private static void LuaPrint(object[] args)
    {
      for (int i = 0; i < args.Length; i++)
        Console.Write(args[i]);
      Console.WriteLine();
    }


    private static void TestMemory()
    {
      for (int i = 0; i < 100; i++)
      {
        TestParser();
        for (int j = 0; j < 10; j++)
        {
          GC.Collect(2, GCCollectionMode.Forced, true);
          Thread.Sleep(10);
        }
      }
    }

    private static void TestExpression()
    {

       //(1 + 2):ToString().Length
      //Expression x = Expression.Property(
      //  Expression.Call(
      //    Expression.Add(
          
      //        Expression.Constant(1, typeof(int)),
      //        Expression.Constant(211, typeof(int))),

      //       typeof(int).GetMethod("ToString", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance, null, new Type[] { }, null)),
        
        
      //      typeof(string), "Length");

      Expression x = Expression.Add(Expression.Constant(200, typeof(int)), Expression.Constant(100, typeof(int)));

      LambdaExpression lambda = Expression.Lambda<Func<int>>(x);
      Delegate dlg = lambda.Compile();
      Console.WriteLine(dlg.DynamicInvoke());
      Console.WriteLine(x.ToString());
    }

    private static void TestParser()
    {
      Lua lua = new Lua();
      lua.RegisterFunction("print", new Action<object[]>(LuaPrint));
      lua.PrintExpressionTree = true;
      object[] r = lua.DoChunk(@"C:\Projects\LuaDLR\LuaCmd\Tests\Parser.lua", new KeyValuePair<string, object>("person", new Person { Name = "Test", Age = 28 }));
      if (r != null && r.Length > 0)
        for (int i = 0; i < r.Length; i++)
        {
          Console.WriteLine("Result[{0},{2}] = {1}", i, r[i], r[i] == null ? "<null>" : r[i].GetType().Name);
        }
      else
        Console.WriteLine("NoResult"); ;
    }

    private static void TestBdeParser()
    {
      Lua l = new Lua();
      l.PrintExpressionTree = true;
     
      Delegate dlg = l.CompileChunk(@"C:\Projects\LuaDLR\LuaCmd\Bde.Kersten.lua");

    }

    private static void TestLexer()
    {
      string sText = File.ReadAllText(@"C:\Projects\LuaDLR\LuaCmd\Bde.Kersten.lua");
      //string sText = File.ReadAllText(@"C:\Projects\LuaDLR\LuaCmd\Tests\Lexer.txt");
      Stopwatch sw = new Stopwatch();
      sw.Start();
      for (int i = 0; i < 1000; i++)
      {
        Console.Write(".");
        using (LuaLexer l = new LuaLexer())
        {
          l.Init(ScannerBuffer.CreateFromString(sText, "Test.txt"));
          l.Next();
          while (l.Current.Typ != LuaToken.Eof)
          {
            //Console.WriteLine(l.Current.ToString());
            l.Next();
          }
        }
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced);
      }
      sw.Stop();
      Console.WriteLine("{0:N0} ms", sw.ElapsedMilliseconds / 1000);
    } // proc TestLexer
  }
}
