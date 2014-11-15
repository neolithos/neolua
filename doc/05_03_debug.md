# Debugging

It is possible to implement your own trace line debugger.

Inherit from `LuaTraceLineDebugger` and pass a instance of this class to the `CompileChunk` debug argument.