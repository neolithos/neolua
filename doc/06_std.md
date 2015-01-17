# Standard Libraries 
This sections gives an overview of the implementation state of the lua system libraries. 

## Basic library

* ![Is compatible to the lua reference.][done] `assert`  Calls `System.Diagnostics.Debug.Assert`.
* ![Not full compatible to the lua reference.][noco] `collectgarbage`  Only the parameter "count" and "collect" are supported. "step" and "isrunning" return always "true". "setpause" returns "false".
"* ![Not full compatible to the lua reference.][noco] `dofile`  Redirects to DoChunk to load and run a text file. Optional add parameters for the script.
```Lua
dofile('test.lua', 'a', 1, 'b', 2);
```
stdin is not supported."
* ![Not full compatible to the lua reference.][noco] `dochunk`  Redirects to DoChunk.
* ![Is compatible to the lua reference.][done] `error`  Throws a {{LuaRuntimeException}}.
* ![Is compatible to the lua reference.][done] `_G`  
* ![Not full compatible to the lua reference.][noco] `getmetatable`  No metatable for userdata, operators are used.
* ![Is compatible to the lua reference.][done] `ipairs`  
* ![Not full compatible to the lua reference.][noco] `load`  The "mode" "b" is not supported.
* ![Not full compatible to the lua reference.][noco] `loadfile`  The "mode" "b" is not supported.
* ![Is compatible to the lua reference.][done] `next`  
* ![Is compatible to the lua reference.][done] `pairs`  
* ![Is compatible to the lua reference.][done] `pcall`  
* ![Is compatible to the lua reference.][done] `print`  Prints on the debug output.
* ![Is compatible to the lua reference.][done] `rawequal`  
* ![Is compatible to the lua reference.][done] `rawget`  
* ![Is compatible to the lua reference.][done] `rawlen`  
* ![Is compatible to the lua reference.][done] `rawset`  
* ![Is compatible to the lua reference.][done] `select`  
* ![Not full compatible to the lua reference.][noco] `setmetatable`  No metatable for userdata, operators are used.
* ![Is compatible to the lua reference.][done] `tonumber`  
* ![Is compatible to the lua reference.][done] `tostring`  
* ![Is compatible to the lua reference.][done] `type`  “type” is extended with a second boolean parameter, that replaces “userdata” with the clr-type-name: type(obj, true);
* ![Is compatible to the lua reference.][done] `_VERSION`  
* ![Is compatible to the lua reference.][done] `xpcall`  

## Coroutine library (coroutine)

Coroutines are implemented in the LuaThread class. This class creates a managed thread for every coroutine. The resume-steps can run asynchronous.

* ![Is compatible to the lua reference.][done] `create`  
* ![Is compatible to the lua reference.][done] `resume`  
* ![Is compatible to the lua reference.][done] `running`  
* ![Is compatible to the lua reference.][done] `status`  
* ![Is compatible to the lua reference.][done] `wrap`  
* ![Is compatible to the lua reference.][done] `yield`  
* ![Not full compatible to the lua reference.][noco] `:BeginResume`  Starts the execution of the next part of the thread.
* ![Not full compatible to the lua reference.][noco] `:EndResume`  Waits for the end of currently running part.

## Package library (package)

* ![Not full compatible to the lua reference.][noco] `require`  Different implementation, that fits better with the NeoLua framework.
* ![Is compatible to the lua reference.][done] `config`  
* ![Not implemented. Yet.][noti] `cpath`  
* ![Not implemented. Yet.][noti] `loaded`  
* ![Not implemented. Yet.][noti] `loadlib`  
* ![Is compatible to the lua reference.][done] `path`  
* ![Not implemented. Yet.][noti] `preload`  
* ![Not implemented. Yet.][noti] `searchers`  
* ![Not implemented. Yet.][noti] `searchpath`  

## String manipulation (string)

* ![Is compatible to the lua reference.][done] `byte`  
* ![Is compatible to the lua reference.][done] `char`  
* ![Not implemented. Yet.][noti] `dump`  
* ![Not full compatible to the lua reference.][noco] `find`  .net regex syntax with an exchange of the escape symbol % is \. But you can set string__TranslateRegEx to false to use .net regulare expressions.
* ![Is compatible to the lua reference.][done] `format`  
* ![Not full compatible to the lua reference.][noco] `gmatch`  .net regex syntax with an exchange of the escape symbol % is \. But you can set string__TranslateRegEx to false to use .net regulare expressions.
* ![Not full compatible to the lua reference.][noco] `gsub`  .net regex syntax with an exchange of the escape symbol % is \. But you can set string__TranslateRegEx to false to use .net regulare expressions.
* ![Is compatible to the lua reference.][done] `len`  
* ![Is compatible to the lua reference.][done] `lower`  
* ![Not full compatible to the lua reference.][noco] `match`  
* ![Not implemented. Yet.][noti] `pack`  
* ![Not implemented. Yet.][noti] `packsize`  
* ![Is compatible to the lua reference.][done] `rep`  
* ![Is compatible to the lua reference.][done] `reverse`  
* ![Is compatible to the lua reference.][done] `sub`  
* ![Not implemented. Yet.][noti] `unpack`  
* ![Is compatible to the lua reference.][done] `upper`  

## Table manipulation (table)

* ![Is compatible to the lua reference.][done] `conat`  
* ![Is compatible to the lua reference.][done] `insert`  
* ![Not implemented. Yet.][noti] `move`  
* ![Is compatible to the lua reference.][done] `pack`  
* ![Is compatible to the lua reference.][done] `remove`  
* ![Is compatible to the lua reference.][done] `sort`  
* ![Is compatible to the lua reference.][done] `unpack`  

## Mathematical functions (math)

* ![Is compatible to the lua reference.][done] `abs`  
* ![Is compatible to the lua reference.][done] `acos`  
* ![Is compatible to the lua reference.][done] `asin`  
* ![Is compatible to the lua reference.][done] `atan`  
* ![Is compatible to the lua reference.][done] `atan2`  
* ![Is compatible to the lua reference.][done] `ceil`  
* ![Is compatible to the lua reference.][done] `cos`  
* ![Is compatible to the lua reference.][done] `cosh`  
* ![Is compatible to the lua reference.][done] `deg`  
* ![Is compatible to the lua reference.][done] `e`  
* ![Is compatible to the lua reference.][done] `exp`  
* ![Is compatible to the lua reference.][done] `floar`  
* ![Is compatible to the lua reference.][done] `fmod`  
* ![Not implemented. Yet.][noti] `frexp`  
* ![Not implemented. Yet.][noti] `huge`  
* ![Not implemented. Yet.][noti] `ldexp`  
* ![Is compatible to the lua reference.][done] `log`  
* ![Is compatible to the lua reference.][done] `max`  
* ![Is compatible to the lua reference.][done] `maxinteger`  
* ![Is compatible to the lua reference.][done] `min`  
* ![Is compatible to the lua reference.][done] `mininteger`  
* ![Is compatible to the lua reference.][done] `modf`  
* ![Is compatible to the lua reference.][done] `pi`  
* ![Is compatible to the lua reference.][done] `pow`  
* ![Is compatible to the lua reference.][done] `rad`  
* ![Is compatible to the lua reference.][done] `random`  
* ![Is compatible to the lua reference.][done] `randomseed`  
* ![Is compatible to the lua reference.][done] `sin`  
* ![Is compatible to the lua reference.][done] `sinh`  
* ![Is compatible to the lua reference.][done] `sqrt`  
* ![Is compatible to the lua reference.][done] `tan`  
* ![Is compatible to the lua reference.][done] `tanh`  
* ![Is compatible to the lua reference.][done] `tointeger`  
* ![Is compatible to the lua reference.][done] `type`  
* ![Is compatible to the lua reference.][done] `ult`  

## Bitwise operations (bit32)

* ![Is compatible to the lua reference.][done] `arshift`  
* ![Is compatible to the lua reference.][done] `band`  
* ![Is compatible to the lua reference.][done] `bnot`  
* ![Is compatible to the lua reference.][done] `bor`  
* ![Is compatible to the lua reference.][done] `btest`  
* ![Is compatible to the lua reference.][done] `bxor`  
* ![Is compatible to the lua reference.][done] `extract`  
* ![Is compatible to the lua reference.][done] `replace`  
* ![Is compatible to the lua reference.][done] `lrotate`  
* ![Is compatible to the lua reference.][done] `lshift`  
* ![Is compatible to the lua reference.][done] `rrotate`  
* ![Is compatible to the lua reference.][done] `rshift`  

## Input and output (io)
"Works only with ASCII files.
The file-handle uses the IDisposable-Pattern."
* ![Is compatible to the lua reference.][done] `close`  
* ![Is compatible to the lua reference.][done] `flush`  
* ![Is compatible to the lua reference.][done] `input`  
* ![Is compatible to the lua reference.][done] `lines`  
* ![Is compatible to the lua reference.][done] `open`  
* ![Is compatible to the lua reference.][done] `output`  
* ![Is compatible to the lua reference.][done] `popen`  
* ![Is compatible to the lua reference.][done] `read`  
* ![Is compatible to the lua reference.][done] `tmpfile`  
* ![Not full compatible to the lua reference.][noco] `tmpfilenew`  Creates a temporary file, that is deleted, when it is closed.
* ![Is compatible to the lua reference.][done] `type`  
* ![Is compatible to the lua reference.][done] `write`  
* ![Is compatible to the lua reference.][done] `:close`  
* ![Is compatible to the lua reference.][done] `:flush`  
* ![Is compatible to the lua reference.][done] `:lines`  
* ![Is compatible to the lua reference.][done] `:read`  
* ![Is compatible to the lua reference.][done] `:seek`  
* ![Is compatible to the lua reference.][done] `:setvbuf`  Ignored.
* ![Is compatible to the lua reference.][done] `:write`  

## Operating system facilities (os)

* ![Is compatible to the lua reference.][done] `clock`  Returns TotalProcessorTime in seconds.
* ![Is compatible to the lua reference.][done] `date`  
* ![Is compatible to the lua reference.][done] `difftime`  
* ![Is compatible to the lua reference.][done] `execute`  On windows is no signal-result.
* ![Is compatible to the lua reference.][done] `exit`  
* ![Is compatible to the lua reference.][done] `getenv`  
* ![Is compatible to the lua reference.][done] `remove`  
* ![Is compatible to the lua reference.][done] `rename`  
* ![Not implemented. Yet.][noti] `setlocale`  
* ![Is compatible to the lua reference.][done] `time`  
* ![Is compatible to the lua reference.][done] `tmpname`  

## Debug facilities (debug)

* ![Not implemented. Yet.][noti] `debug`  
* ![Not implemented. Yet.][noti] `getuservalue`  
* ![Not implemented. Yet.][noti] `gethook`  
* ![Not implemented. Yet.][noti] `getinfo`  
* ![Not implemented. Yet.][noti] `getlocal`  
* ![Not implemented. Yet.][noti] `getmetatable`  
* ![Not implemented. Yet.][noti] `getregistry`  
* ![Is compatible to the lua reference.][done] `getupvalue`  Works on closures and classes.
* ![Not implemented. Yet.][noti] `setuservalue`  
* ![Not implemented. Yet.][noti] `sethook`  
* ![Not implemented. Yet.][noti] `setlocal`  
* ![Not implemented. Yet.][noti] `setmetatable`  
* ![Is compatible to the lua reference.][done] `setupvalue`  Works on closures and classes.
* ![Not implemented. Yet.][noti] `traceback`  
* ![Not full compatible to the lua reference.][noco] `upvalueid`  Do not trust the returned integer. The return value is only good for comparison.
* ![Not full compatible to the lua reference.][noco] `upvaluejoin`  Only works on closures (Lambda's). For example, the upvalues of the function that is returned by load are not join-able.


[done]: imgs/done.png
[noco]: imgs/noco.png
[noti]: imgs/noti.png
