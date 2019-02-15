NeoLua
======

A Lua implementation for the Dynamic Language Runtime (DLR).

## Introduction

NeoLua is an implementation of the Lua language. Currently, the implementation is on 
the level of [Lua_5.3](http://www.lua.org/) (http://www.lua.org/manual/5.3/manual.html). 
The goal is to follow the reference of the C-Lua implementation and combine this with full 
.NET Framework support. That means, it should be easy to call .NET functions from Lua and it should 
be easy access variables and functions from a .net language (e.g. C#, VB.NET, ...).

NeoLua is implemented in C# and uses the [Dynamic Language Runtime](https://dlr.codeplex.com/). It therefore 
integrates very well with the .NET Framework.

### Quickstart

You can play and test the language with the tool NeoCmd.

![NeoCmd](doc/imgs/Image.png)

Or there are two easy ways to use NeoLua in your project.

* Download the Neo.Lua.dll and add the reference. For full desktop support also a reference to Neo.Lua.Desktop.dll is useful.
* Install the [NuGet package](http://www.nuget.org/packages/NeoLua/) of NeoLua.


Simple example:
```C#
using (Lua lua = new Lua()) // Create the Lua script engine
{
    dynamic env = lua.CreateEnvironment(); // Create a environment
    env.dochunk("a = 'Hallo World!';", "test.lua"); // Create a variable in Lua
    Console.WriteLine(env.a); // Access a variable in C#
    env.dochunk("function add(b) return b + 3; end;", "test.lua"); // Create a function in Lua
    Console.WriteLine("Add(3) = {0}", env.add(3)); // Call the function in C#
}
```

```VB
Using lua As Lua = New Lua ' Create the Lua script engine
    Dim env As Object = lua.CreateEnvironment(Of LuaGlobal)() ' Create a environment
    env.dochunk("a = 'Hallo World!';", "test.lua") ' Create a variable in Lua
    Console.WriteLine(env.a) ' Access a variable in VB
    env.dochunk("function add(b) return b + 3; end;", "test.lua") ' Create a function in Lua
    Console.WriteLine("Add(3) = {0}", (New LuaResult(env.add(3)))(0)) ' Call the function in VB
End Using
```

A more "complex" script, that shows the interaction between lua and .net:
```Lua
local t = clr.System.Int32[](6, 8, 9, 19);",
return t:Sum(), t:Where(function(c : int) : bool return c < 10 end):Sum()
```

NeoLua is a .NET portable assembly (IL) for 
* .NET Framework 4.5.1
* Windows Phone 8.1
* Windows Store Apps 8.1
* Xamarin.Android

There will be no support for .NET Frameworks lower than 4.5. 

It does not work with
* Xamarin.iOS.
or any .NET runtime that does not support code generation.


### What NeoLua is useful for

* Outsource the logic of your application into scripts
* Structuring of logic
* Build a dynamic configuration system, with functions and variables
* As a formula parser
* ...

So, this could be reliable partner for your compiled .NET application or engine (e.g. game engines).

### What I did not have in mind

* Compiling libraries
* Standalone applications

### Advantages of NeoLua

* Dynamic access between Lua script and and the host application/.NET Framework and vice-versa.
* NeoLua is based on the DLR. So you get compiled code that is collectable and well-optimized.
* It is compatible with the .NET world (e.g. C#, VB.NET, IronPython, ...).
* Full and easy access to the .NET Framework or your own libraries (with no stub code).
* A rich implementation of the Lua table, for a got integration in the .NET world e.g. binding, enumeration, ...
* A .NET Framework garbage collector that is well-tested and very fast.
* Pure IL (x86, x64 support)

### Drawbacks of NeoLua

* It is not [100% compatible](doc/06_std.md) to Lua. But I will try very hard.
* No deployment of precompiled scripts.

### Drawbacks of bridges to c-lua, that are solved with NeoLua

* You have two memory managers and so you have to marshal every data between these two worlds. That takes time and there are a lot pitfalls to get memory leaks.
* C-Lua interprets its own bytecode. The code is not compiled to machine code.

## Documentation

*This documention has the same structure like the official reference ([Lua 5.3](http://www.lua.org/manual/5.3/manual.html)), so it should be easy to compare the two worlds.*

1. Introduction
2. [Basic concepts](doc/02_basics.md)
3. [Language](doc/03_language.md)
4. Application Program Interface
    1. [Getting started](doc/04_01_start.md)
    2. [Script engine](doc/04_02_engine.md)
    3. [Chunks](doc/04_03_chunk.md)
    4. [Table's](doc/04_04_table.md)
5. The Auxiliary Library
    1. [clr library](doc/05_01_clr.md)
    2. [Extent lua table](doc/05_02_extent.md)
    3. [Debugging](doc/05_03_debug.md)
6. [Standard libraries](doc/06_std.md)
7. [NeoCmd](doc/07_neocmd.md)

If there is something unclear, wrong or misunderstanding please use the discussions.

## Projects that use this library

* Data Exchange Server: https://github.com/twdes/des

## Links

* Article on CodeProject: http://www.codeproject.com/Articles/674128/NeoLua-Lua-for-the-net-dynamic-lanuguage-runtime
* NuGet package: https://www.nuget.org/packages/NeoLua/
