Option Strict On
Imports Neo.IronLua

Module Module1

    Private Sub TestStrict()
        Using l As Lua = New Lua ' create the lua script engine
            Dim g As LuaGlobal = l.CreateEnvironment(Of LuaGlobal)() ' create a environment
            g.DoChunk("a = 'Hallo World!';", "test.lua") ' create a variable in lua
            Console.WriteLine(g("a").ToString()) ' access a variable in VB
            g.DoChunk("function add(b) return b + 3; end;", "test.lua") ' create a function in lua
            Console.WriteLine("Add(3) = {0}", g.CallMember("add", 3)) ' call the function in VB
        End Using
    End Sub

    Sub Main()
        Console.WriteLine("Strict on")
        TestStrict()
        Console.WriteLine("Strict off")
        Module2.TestDynamic()
        Console.ReadLine()
    End Sub

End Module
