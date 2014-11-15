# Packages (!todo!)

It is possible to add more packages to the environment. There are two ways to add packages. Static packages and dynamic packages.

Static Packages are basically static classes. The static members will be available in Lua.

Restrictions:

* No overload allowed.
* No Instance members allowed. 


Example declaration:

```C#
public static class Test
{ 
  public static int add(int a, int b) 
  { 
    return a + b; 
  } 
}
```

Example register:

```C#
LuaGlobal g = ...
g.RegisterPackage("test", typeof(Test));
```

Example usage:

```Lua
return test.add(1, 2);
```

For dynamic packages you have to implement IDynamicMetaObjectProvider. More information can
be find under http://dlr.codeplex.com/documentation (library-authors-introduction.pdf)

For registration of dynamic package, just create a global variable.

# Extent the global namespace (LuaGlobal)

The easiest way to add new functions or members to the Lua environment is to add a global 
variable. It is also the only way to override the standard implementation of the function. If 
you want the standard back, just set the global to nil/null.

```Lua
-- change print
print = function (...)
  -- fancy new print
end;
-- restore print
print = nil;
```

It is also possible to create your own Lua environment. For this you have to subclass the standard environment LuaGlobal. To extent your environment add a private function to the class and mark it with LuaMemberAttribute.

Example:

```C#
[LuaMember("rawset")] 
private LuaTableLuaRawSet(LuaTablet, objectindex, objectvalue)
```

A third way is to override GetMemberAccess and return a expression and restriction for the member.