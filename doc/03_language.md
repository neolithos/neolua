# Language

Because there exists a official [lua language reference](http://www.lua.org/manual/5.3/manual.html#3), I will only cover in this document the differences.

## Explicit types

```
vardecl ::= name [ ´:´ type ]
type ::= ‘byte’ | ‘sbyte’ | ‘short’ | ‘ushort’ | ‘int’ | ‘uint’ | ‘long’ | ‘ulong’ | ‘float’ | ‘double’ | ‘decimal’ | ‘datetime’ | ‘char’ | ‘string’ | ‘object’ | ‘type’ | ‘luatype’ | ‘thread’ | ‘table’ | name { ‘.’ name }
```

How [mention](02_basics.md#values-and-types), NeoLua uses the whole set of the .net type system. 

It is possible to assign local variables a type. This is very important if you want to enforce a type in your script.
```Lua
local a : byte = 23; -- declare an integer variable and assign the value 23
local b : System.String = 42; -- declare a string variable and assign the 
                              -- the value 42, that will be convert to a string
```

You can add the full type name (namespace + type ame) or you can use a alias.

Aliases can be define on the `LuaType` class, with the static method `RegisterTypeAlias` or use the `´const´ name ´typeof´ type` .

Another reason to use typed locals is to avoid  unboxing and boxing operations (performance!).

###### Example:
```Lua
local a : int, b : int = 23, 42;
return a + b;
```
Because, both types are well known the NeoLua compiler can emit a integer add operation. If you write it in normal lua like
```Lua
local a, b = 23, 42;
return a + b;
```
the compile emits a dynamic add operation, that will be resolve the integer add operation on the first hit during runtime.

There are also some automatic type decissions, like
```Lua
-- i is an integer (no declaration is necessary)
for i = 0,9,1 do
  print(i);
end;
```

Global values are always declared as objects, because they are members on the global table/environment.

## Keyword `cast`

```
cast ::= ‘cast’ ‘(‘ type ‘,’ expr ‘)’
```

To enforce a type in a expression, you can use the keyword cast. The first argument is the type and the second is the expression, that will be converted.

```Lua
cast(int, variable);
cast(byte, 34);
cast(System.Text.StringBuilder, var);
```

The type must be known during the compile time, so it is not possible to use a variable that holds a type.

###### Example (find correct overload):
```Lua
local sb = clr.System.Text.StringBuilder();

function GetText()
  return "Hallo";
end;

sb:Append(cast(string, GetText()));
sb:Append(cast(string, string.upper(" Welt")));

print(sb:ToString());
```
The return type of an lua-function is always a multi set of values (`LuaResult`). There is no 
overload for this type, there is only one, who is closed to it (Append(char[])). But a 
string can not converted to an character value. Without the explicit cast you will result in an cast-exception.

## Keyword `const`

```
const ::= `const´ vardecl [ `=´ expr | `typeof´ type ]
```

With the const keyword it is possible to declare a value, that is not set to the global environment. So, it will not produce any overhead at runtime, and will not accessible to the host program.

```Lua
const a = 3 - 2; -- the result of the expression will be assigned to a (int)
return  a * 2; -- the result will be 2 and not -1, because 1 is included and not 3 - 2
```

###### Type shortcuts:
```Lua
const StringBuilder typeof System.Test.StringBuilder;
local sb : StringBuilder = StringBuilder(); -- declare a variable of the type StringBuilder, none dynamic
sb:Append('Hello'); -- call the method append, none dynamic
```
`typeof` declares a constant for the compiler with the `LuaType` object for the clr type. It is possible to use constant in `cast` expressions, type declarations and you can call static methods of the type.

```Lua
const Env typeof System.Environment;
return Env.MachineName, Env:GetEnvironmentVariable("PATH");
```

## Keyword `foreach`

```
foreach ::= ‘foreach’ vardecl ‘in’ expr ‘do’ block ‘end’
```

`foreach` is easy way to enumerate lists or arrays. If the result of the expression implements the System.Collections.IEnumerable interface, 
it will go throw the whole list an stores the current value in the loop-variable.

```Lua
foreach c in "Hallo" do
  print(c); -- prints every letter
end;
```

The result of an function is enumerable. So, this is a nice way to access unknown result.

```Lua
function text()
  return "Hallo", "Welt";
end;

foreach c in text() do
  print(c); -- prints the two words
end;
foreach c in cast(string, text()) do
  print(c); -- prints every letter
end;
```

## `do` loop

```
doloop ::= ‘do’ ‘(‘ vardecl { ‘,’ vardecl } ‘=’ expr { ‘,’ expr }) block ‘end’
```

NeoLua extents the `do` to an using statement. Every variable, that is declared between the braces will automatically disposed at the end of the loop.

```Lua
do (sw = clr.System.IO.StreamReader([[C:\Projects\NeoLua\trunk\NeoCmd\Samples\FileRead.lua]]))
  -- error("test"); 
  print(sw:ReadToEnd());
end;
```

The file handle will be released, also when a error is thrown.
Functions

## function signatures

```
lambda ::=  ‘(‘ vardecl { ‘,’ vardecl } ´)´ ´:´ type block ‘end’
```

It is possible to create a typed function signature.

```Lua
function add(a : int, b : int) : int -- produces Func<int, int, int>
  return a + b;
end;
```

It is also possible to enforce only some arguments.

```Lua
function add(a, b : int) -- Func<object, int, LuaResult>
  return a + b;
end;
```

## `return`

If you do not declare a return type, then NeoLua functions always return a `LuaResult`.

```C#
g.dochunk("function f() return 1,2,3; end;", "test.lua");
LuaResult r = g.test(); -- returns a LuaResult
Console.WriteLine("{0}", r[0] + r[1] + r[2]); -- prints "6"
Console.WriteLine("{0}", r[4]); -- prints nothing
```

`LuaResult` implements the dynamic interface:

```C#
dynamic r2 = g.test();
Console.WriteLine("{0}", r2); -- prints "1"
```

