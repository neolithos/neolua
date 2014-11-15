# LuaTable

The `LuaTable`is the most important type of Lua. It is also implemented with an dynamic 
interface for an easy use in the host application and the script, too.

The lua environment (`LuaGlobal`) is a sub class of the lua table. It holds 
the basic environment (global variables, basic functions, packages) of 
the lua script. All chunks (except Lambda's) run on environment and manipulate them.

## Create a environment

To create a environment you have to call the `CreateEnvironment` function of the script engine.

```C#
using (Lua l = new Lua())
{
  var g = l.CreateEnvironment();
  dynamic dg = g;
}
```

In all examples `l` is a script engine and `g` is a environment (`dg` is the dynamic environment).

## Execute script

To execute a single script on an environment you have to call `DoChunk`. Every script returns a `LuaResult`. If there is no return-command in the script, the result is empty.

```C#
LuaResult r = g.DoChunk("return 2", "test.lua");
Console.WriteLine(r[0]);
```

It is also possible to define parameters for a script.

```C#
LuaResult r = g.DoChunk("return a + b", "test.lua",
  new KeyValuePair<string, object>("a", 2),
  new KeyValuePair<string, object>("b", 4));
Console.WriteLine(r[0]);
```

Or run precompiled scripts more than one time.

```C#
var c = l.CompileChunk("return b * 2", "test.lua", false, new KeyValuePair<string, Type>("b", typeof(int)));
Console.WriteLine(g.DoChunk(c, 2)[0]);
Console.WriteLine(g.DoChunk(c, 3)[0]);
Console.WriteLine(g.DoChunk(c, 4)[0]);
```

## Dynamic environment

The environment exposes all functionality, that you have in a lua script as an dynamic interface. Use the functions, that you know from lua script.

```C#
LuaResult r = dg.dochunk("return 2", "test.lua");
Console.WriteLine(r[0]);
```

The same with the dynamic interface.

```C#
dynamic dr = dg.dochunk("return a + b", "test.lua", "a", 2, "b", 4);
Console.WriteLine((int)dr);
```

And with an argument.

```C#
var c = l.CompileChunk("return b * 2", "test.lua", false, new KeyValuePair<string, Type>("b", typeof(int)));
Console.WriteLine(dg.dochunk(c, 2)[0]);
Console.WriteLine(dg.dochunk(c, 3)[0]);
Console.WriteLine(dg.dochunk(c, 4)[0]);
```

## Member/Variables

Define or access variables on the environment or tables.

```C#
dg.a = 2; // dynamic way to set a variable
g["b"] = 4; // explicit way to access variable
g.DoChunk("c = a + b", "test.lua");
Console.WriteLine(dg.c);
Console.WriteLine(g["c"]);
```

Define or access a lua table.

```C#
dynamic dg = l.CreateEnvironment();
dg.t = new LuaTable(); -- create global variable t
dg.t.a = 2; -- create a member a on table t
dg.t.b = 4;
dg.dochunk("t.c = t.a + t.b", "test.lua");
Console.WriteLine(dg.t.c);
```

## Index access

It is also easy to work with indices.

```C#
dynamic dg = l.CreateEnvironment();
dg.t = new LuaTable();
dg.t[0] = 2;
dg.t[1] = 4;
dg.dochunk("t[2] = t[0] + t[1]", "test.lua");
Console.WriteLine(dg.t[2]);
```

## Define/call functions

```C#
dg.myadd = new Func<int, int, int>((a, b) => a + b); // define a new function in c#
dg.dochunk("function Add(a, b) return myadd(a, b) end;", "test.lua"); // define a new function in lua that calls the c# function

Console.WriteLine((int)dg.Add(2, 4)); //call the lua function

var f = (Func<object, object, LuaResult>)dg.Add; // get the lua function
Console.WriteLine(f(2, 4).ToInt32());
```

Is there no result defined. Lua always let return functions a LuaResult.

But it also possible to give a explicit definition.

```C#
dg.myadd = new Func<int, int, int>((a, b) => a + b);
dg.dochunk("function Add(a : int, b : int) : int return myadd(a, b) end;", "test.lua");

Console.WriteLine((int)dg.Add(2, 4));

var f = (Func<int, int, int>)dg.Add;
Console.WriteLine(f(2, 4));
```

## Declaring Methods in the host application

The next example define three members. a, b are members, they are holding 
a ordinary integer. And add is holding a function, but this definition is not 
a method. Because if you try to call the function like a method in e.g. c#, 
it will throw a NullReferenceException. You must always pass the table as a 
first parameter to it. To declare a real method, you have to call the explicit 
method DefineMethod.

Lua don't care about the difference, but c# or VB.NET knows it.

```C#
dg.t = new LuaTable();
dg.t.a = 2;
dg.t.b = 4;
dg.t.add = new Func<dynamic, int>(self => 
  {
    return self.a + self.b;
  });
((LuaTable)dg.t).DefineMethod("add2", (Delegate)dg.t.add);

Console.WriteLine(dg.dochunk("return t:add()", "test.lua")[0]);
Console.WriteLine(dg.dochunk("return t:add2()", "test.lua")[0]);
Console.WriteLine(dg.t.add(dg.t));
Console.WriteLine(dg.t.add2());
```

# Declaring methods in Lua

* `add` is a normal function, created from a delegate.
* `add1` is declared as a function.
* `add2` is a method, that is created from a lambda. Becareful a lambda definition doesn't know anything about the concept of methods, so you have to declare the self parameter.
* `add3` shows a method declaration.

```C#
LuaResult r = dg.dochunk("t = { a = 2, b = 4 };" +
  "t.add = function(self)" +
  "  return self.a + self.b;" +
  "end;" +
  "function t.add1(self)" +
  "  return self.a + self.b;" +
  "end;" +
  "t:add2 = function (self)" +
  "  return self.a + self.b;" +
  "end;" +
  "function t:add3()" +
  "  return self.a + self.b;" +
  "end;" +
  "return t:add(), t:add2(), t:add3(), t.add(t), t.add2(t), t.add3(t);", 
  "test.lua");
Console.WriteLine(r[0]);
Console.WriteLine(r[1]);
Console.WriteLine(r[2]);
Console.WriteLine(r[3]);
Console.WriteLine(r[4]);
Console.WriteLine(r[5]);
Console.WriteLine(dg.t.add(dg.t)[0]);
Console.WriteLine(dg.t.add2()[0]);
Console.WriteLine(dg.t.add3()[0]);
```

## Classes/Objects

To create a class you have to write a function, that creates a new object. The function is by definition the class and the result of the function is the object.

```C#
dg.dochunk("function classA()" +
  "  local c = { sum = 0 };" +
  "  function c:add(a)" +
  "    self.sum = self.sum + a;" +
  "  end;" +
  "  return c;" +
  "end;", "classA.lua");

dynamic o = dg.classA()[0];
o.add(1);
o.add(2);
o.add(3);
Console.WriteLine(o.sum);
```
