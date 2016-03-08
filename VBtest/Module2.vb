Option Strict Off

Imports Neo.IronLua

Module Module2
    Public Sub TestDynamicVBOnly()
        Dim f = Function(ByVal o) As LuaResult
                    Return New LuaResult(o + 3)
                End Function

        Dim f2 = DirectCast(f, Object)
        Dim tname = f.GetType()
        Dim tname2 = f2.GetType()
        Console.WriteLine("t: {0}", tname)
        Console.WriteLine("t: {0}", tname2)
        Console.WriteLine("Add(3) = {0}", f(3))
        Console.WriteLine("Add(3) = {0}", f2(3)) ' why is this crashing
    End Sub

    Public Sub TestDynamic()
        Using l As Lua = New Lua ' create the lua script engine
            Dim g As Object = l.CreateEnvironment(Of LuaGlobal)() ' create a environment
            g.dochunk("a = 'Hallo World!';", "test.lua") ' create a variable in lua
            Console.WriteLine(g.a) ' access a variable in VB
            g.dochunk("function add(b) return b + 3; end;", "test.lua") ' create a function in lua
            Console.WriteLine("Add(3) = {0}", (New LuaResult(g.add(3)))(0)) ' call the function in VB
        End Using
    End Sub
End Module
