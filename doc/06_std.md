# Standard Libraries 
This sections gives an overview of the implementation state of the lua system libraries. 

## Basic library

* [Is compatible to the lua reference.](done) `assert`  Calls `System.Diagnostics.Debug.Assert`.
* [Not full compatible to the lua reference.](noco) `collectgarbage`  Only the parameter "count" and "collect" are supported. "step" and "isrunning" return always "true". "setpause" returns "false".
"* [Not full compatible to the lua reference.](noco) `dofile`  Redirects to DoChunk to load and run a text file. Optional add parameters for the script.
```Lua
dofile('test.lua', 'a', 1, 'b', 2);
```
stdin is not supported."
* [Not full compatible to the lua reference.](noco) `dochunk`  Redirects to DoChunk.
* [Is compatible to the lua reference.](done) `error`  Throws a {{LuaRuntimeException}}.
* [Is compatible to the lua reference.](done) `_G`  
* [Not full compatible to the lua reference.](noco) `getmetatable`  No metatable for userdata, operators are used.
* [Is compatible to the lua reference.](done) `ipairs`  
* [Not full compatible to the lua reference.](noco) `load`  The "mode" "b" is not supported.
* [Not full compatible to the lua reference.](noco) `loadfile`  The "mode" "b" is not supported.
* [Is compatible to the lua reference.](done) `next`  
* [Is compatible to the lua reference.](done) `pairs`  
* [Is compatible to the lua reference.](done) `pcall`  
* [Is compatible to the lua reference.](done) `print`  Prints on the debug output.
* [Is compatible to the lua reference.](done) `rawequal`  
* [Is compatible to the lua reference.](done) `rawget`  
* [Is compatible to the lua reference.](done) `rawlen`  
* [Is compatible to the lua reference.](done) `rawset`  
* [Is compatible to the lua reference.](done) `select`  
* [Not full compatible to the lua reference.](noco) `setmetatable`  No metatable for userdata, operators are used.
* [Is compatible to the lua reference.](done) `tonumber`  
* [Is compatible to the lua reference.](done) `tostring`  
* [Is compatible to the lua reference.](done) `type`  “type” is extended with a second boolean parameter, that replaces “userdata” with the clr-type-name: type(obj, true);
* [Is compatible to the lua reference.](done) `_VERSION`  
* [Is compatible to the lua reference.](done) `xpcall`  


[done]: imgs/done.png
[noco]: imgs/noco.png
[noti]: imgs/noti.png