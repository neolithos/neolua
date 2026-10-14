using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
    [TestClass]
    public class TableLibraryMetamethodTests
    {
        [TestMethod]
		[DataRow(@"local t=setmetatable({42},{__len=function() return 0 end})
assert(select('#',table.unpack(t))==0)", "unpack_zero")]
		[DataRow(@"local count=0
local t=setmetatable({10,20,30},{__len=function() count=count+1; return 2 end})
local out=table.pack(table.unpack(t,2))
assert(out.n==1 and out[1]==20 and count==1)", "unpack_start")]
		[DataRow(@"local count=0
local t=setmetatable({10,20,30},{__len=function() count=count+1; return 2 end})
local out=table.pack(table.unpack(t,1,nil))
assert(out.n==2 and out[2]==20 and count==1)", "unpack_nil_end")]
		[DataRow(@"local t=setmetatable({10,20},{__len=function() error('unused length') end})
local out=table.pack(table.unpack(t,1,2))
assert(out.n==2 and out[1]==10 and out[2]==20)", "unpack_explicit")]
		[DataRow(@"local count=0
local t=setmetatable({},{__len=function() return 3 end,
 __index=function(_,k) count=count+1; return k*10 end})
local out=table.pack(table.unpack(t))
assert(out.n==3 and out[1]==10 and out[2]==20 and out[3]==30 and count==3)", "unpack_proxy")]
		[DataRow(@"local t=setmetatable({},{__index=function() error('unused read') end})
assert(select('#',table.unpack(t,3,2))==0)", "unpack_empty_range")]
		[DataRow(@"local t=setmetatable({10,[3]=30},{__len=function() return 4 end})
local out=table.pack(table.unpack(t))
assert(out.n==4 and out[1]==10 and out[2]==nil and out[3]==30 and out[4]==nil)", "unpack_holes")]
		[DataRow(@"local called=false; local t=setmetatable({42},{__len=function() called=true; error('length sentinel') end})
local ok,err=pcall(table.unpack,t)
assert(not ok and called)", "unpack_length_error")]
		[DataRow(@"local out=table.pack(table.unpack({10,20,30},2,3))
assert(out.n==2 and out[1]==20 and out[2]==30)", "unpack_plain")]
		[DataRow(@"local key,value
local t=setmetatable({},{__newindex=function(_,k,v) key=k; value=v end})
table.insert(t,42)
assert(key==1 and value==42 and rawget(t,1)==nil)", "insert_append")]
		[DataRow(@"local data={10,20}; local count=0
local t=setmetatable({},{__len=function() count=count+1; return 2 end,
 __index=function(_,k) return data[k] end,__newindex=function(_,k,v) data[k]=v end})
table.insert(t,1,5)
assert(data[1]==5 and data[2]==10 and data[3]==20 and count==1 and rawget(t,1)==nil)", "insert_proxy")]
		[DataRow(@"local t=setmetatable({10,20,30},{__len=function() return 1 end})
table.insert(t,42)
assert(rawget(t,1)==10 and rawget(t,2)==42 and rawget(t,3)==30)", "insert_length")]
		[DataRow(@"local calls=0
local t=setmetatable({10,20},{__newindex=function(t,k,v) calls=calls+1; rawset(t,k,v) end})
table.insert(t,1,5)
assert(calls==1 and t[1]==5 and t[2]==10 and t[3]==20)", "insert_existing_slots")]
		[DataRow(@"local log={}; local data={10,20}
local t=setmetatable({},{__len=function() return 2 end,
 __index=function(_,k) log[#log+1]='r'..k; return data[k] end,
 __newindex=function(_,k,v) log[#log+1]='w'..k; data[k]=v end})
table.insert(t,1,5)
assert(table.concat(log,',')=='r2,w3,r1,w2,w1')", "insert_read_write_order")]
		[DataRow(@"local called=false; local t=setmetatable({},{__newindex=function() called=true; error('write sentinel') end})
local ok,err=pcall(table.insert,t,42)
assert(not ok and called and rawget(t,1)==nil)", "insert_write_error")]
		[DataRow(@"local called=false; local t=setmetatable({},{__len=function() called=true; error('length sentinel') end})
local ok,err=pcall(table.insert,t,42)
assert(not ok and called and rawget(t,1)==nil)", "insert_length_error")]
		[DataRow(@"local t={10,20}; table.insert(t,2,42); table.insert(t,99)
assert(table.concat(t,',')=='10,42,20,99')", "insert_plain")]
        public void TableLibraryHonorsMetamethods(string code, string caseName)
        {
            using (var lua = new Lua())
            {
                var environment = lua.CreateEnvironment<LuaGlobal>();
                var result = environment.DoChunk(code + "\nreturn 'ok'", caseName + ".lua");
                Assert.AreEqual(1, result.Values.Length, caseName);
                Assert.AreEqual("ok", result[0], caseName);
            }
        }
    }
}
