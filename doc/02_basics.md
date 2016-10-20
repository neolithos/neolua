# Basic Concepts
## Values and Types

NeoLua is not a dynamically typed language, it just looks like. Variables have always a type (at least `System.Object`). That behavior differs to c-lua.

NeoLua supports all CLR types. If there is type conversion necessary, it is done automatically. Also dynamic types are supported.

```Lua
local a = "5"; -- a string is assigned to a local variable of the type object
local b = {}; -- object assigned with a empty table 
b.c = a + 8; -- the variable "a" is converted to an integer and assigned to the dynamic member of an table
```

The following table shows the types of the lua constants:

| Lua         | Example 	| CLR 	| Difference |
| :-----------|:---------:|:------|:------------ |
| nil         |           | `System.Object` | |
| false       |           | `System.Boolean` | |
| true        |           | `System.Boolean` | |	
| number      |    `1.45` | `System.Double` | |
| number      |      `23` | `System.IntXX` | Lua will choose the correct size of the interger. |
| string      |  `"Test"` | `System.String` | Difference to 8bit string of lua. |
| function    | `function() end;` | `System.Delegate` | All lua functions are compiled to delegates. |
| userdata    |           | Does not exists. | All values have there initial CLR-type. |
| thread      |           | Not implemented. | |
| table       |     `{1}` | `Neo.IronLua.LuaTable` | |

For the conversion between types NeoLua uses the:
* rules of Lua or C#
* dynamic type rules
* `implicit`/`explicit` operators (add static parse methods)

```Lua
local a = 23 + "42"; -- "42" will be converted to integer
local b = 23 .. "42"; -- 23  will be converted to string
local c : byte = "23"; -- 23 will converted to a unsigned 8 bit integer
local d : int = nil; -- will be converted to 0
```

The type casting/converting is one of the most complex part of this implemention. It will help to hold a script clean, please help to improve it.

## Environment

There are two classes they important to create/run a lua script.

First there is the script engine (`Lua`) that is responsible for compiling the scripts into chunks and caching the dynamic calls/requires.

The second is global environment (`LuaGlobal`) on which chunks are executed. This table holds all global variables and defines the basic functions and packages.

###### Create the script engine and a environment:
```C#
using (Lua l = new Lua())
{
  var g = l.CreateEnvironment();
...
```

Normally, you need only one script engine per application. In rare cases it is useful to have more than one.

For accessing and manipulation the environment see under [Global/Table's](doc/04_04_table.md), create your own environment see under extent tables.

It is also possible to create plain delegates without any environment (see [Script engine](doc/04_02_engine.md)).

## Error Handling

NeoLua uses the CLR exception handling and introduces two new exception classes. LuaParseException and LuaRuntimeException.

The parse exception is thrown during the parsing, to inform about syntactical errors. The 
runtime exception is for all runtime errors. Overflow- or DivByZero exception will not 
be catch or converted. The lua error-function creates a LuaRuntimeException.

If you need a stack trace to the exception, you need to compile the script with 
debug information. With the method `LuaExceptionData.GetData` you can retrieve for all exceptions a lua stacktrace.

It is also possible to implement your own concept, for backtraces and debugging.

## Metatables and Metamethods

Every lua table has a member, that holds the metatable (`__metatable`).

```C#
ta = { __add = function(a, b)
		return a.v + b.v;
	end
};

t1 = { __metatable = ta, v = 40 };
t2 = { __metatable = ta, v = 2 };

return t1 + t2;

```

| Member       | Signature                           |
|--------------|-------------------------------------|
| `__add`      | `(table, object) : object`          |
|              | `:(object) : object`                |
| `__sub`      | `(table, object) : object`          |
|              | `:(object) : object`                |
| `__mul`      | `(table, object) : object`          |
|              | `:(object) : object`                |
| `__div`      | `(table, object) : object`          |
|              | `:(object) : object`                |
| `__mod`      | `(table, object) : object`          |
|              | `:(object) : object`                |
| `__pow`      | `(table, object) : object`          |
|              | `:(object) : object`                |
| `__idiv`     | `(table, object) : object`          |
|              | `:(object) : object`                |
| `__unm`      | `(table) : object`                  |
|              | `:()object`                         |
| `__band`     | `(table, object) : object`          |
|              | `:(object) : object`                |
| `__bor`      | `(table, object) : object`          |
|              | `:(object) : object`                |
| `__bxor`     | `(table, object) : object`          |
|              | `:(object) : object`                |
| `__bnot`     | `(table) : object`                  |
|              | `:() : object`                      |
| `__shl`      | `(table, object) : object`          |
|              | `:(object) : object`                |
| `__shr`      | `(table, object) : object`          |
|              | `:(object) : object`                |
| `__concat`   | `(table, object) : object`          |
|              | `:(object) : object`                |
| `__len`      | `(table) : object`                  |
|              | `:() : object`                      |
| `__lt`       | `(table, object) : bool`            |
|              | `:(object) : object`                |
| `__le`       | `(table, object) : bool`            |
|              | `:(object) : object`                |
| `__index`    | `(table, key) : object`             |
|              | `:(key) : object`                   |
| `__newindex` | `(table, key, value) : object`      |
|              | `:(key, value) : object`            |
| `__call`     | `(table, object, ...) : result`     |
|              | `:(object, ...) : result`           |
| `__changed` | `:(object):void`               |

## Garbage Collection

** - is done by the clr -**

## Coroutines

Coroutines are implemented in the LuaThread class. This class creates a managed thread for every coroutine. 
The resume-steps can run asynchronous (`:BeginResume`, `:EndResume`).

The NeoLua-Runtime is threadsafe, so it is possible to use the multithreading features from the .net framework.