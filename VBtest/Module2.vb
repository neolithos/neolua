Option Strict Off

Imports Neo.IronLua

Module Module2
    Public Sub TestDynamic()
        Using l As Lua = New Lua ' create the lua script engine
            Dim g As Object = l.CreateEnvironment(Of LuaGlobal)() ' create a environment
            g.dochunk("a = 'Hallo World!';", "test.lua") ' create a variable in lua
            Console.WriteLine(g.a) ' access a variable in VB
            g.dochunk("function add(b) return b + 3; end;", "test.lua") ' create a function in lua
            Console.WriteLine("Add(3) = {0}", g.add(3)) ' call the function in VB
        End Using
    End Sub
End Module
