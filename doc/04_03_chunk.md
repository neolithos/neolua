# Chunks

The lua script engine compiles methods, they are encapsled in a chunk (`LuaChunk`). It is 
possible to call the method direct, but it is easier to use the `DoChunk`, `Run` (or `dochunk`) method 
of the environment.

## `LuaResult Run(LuaTable env, object[] callArgs)`

Executes the chunk on the given environment.

## `Lua Lua { get; }`

The associated lua script engine.

## `string ChunkName { get; }`

The name of the compiled chunk. It is normally the filename without the extension.

## `bool IsCompiled { get; }`

Is the chunk compiled and executable.

## `bool HasDebugInfo { get; }`

Is the chunk compiled with debug info. If this value is true, it is able to build a stack trace for exceptions within the chunk.

## `MethodInfo Method { get; }`

Access to the compiled chunk and it's declaration.

## `int Size { get; }`

Get's the IL-size.
